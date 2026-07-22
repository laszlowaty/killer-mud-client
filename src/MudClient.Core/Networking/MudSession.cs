using System.Buffers;
using System.IO.Compression;
using System.Net.Sockets;
using System.Text;
using MudClient.Core.Gmcp;
using MudClient.Core.Telnet;
using MudClient.Core.Text;

namespace MudClient.Core.Networking;

public sealed class MudSession : IAsyncDisposable
{
    private readonly TelnetParser _parser = new();
    private string _encodingMode = MudTextEncodings.Auto;
    private Encoding _mudEncoding = MudTextEncodings.Resolve(MudTextEncodings.Utf8);
    private Decoder _textDecoder = MudTextEncodings.Resolve(MudTextEncodings.Utf8).GetDecoder();

    // In Auto mode we assume UTF-8 until the server's first non-ASCII bytes arrive, then
    // check whether they're actually valid UTF-8 and fall back to legacy Windows-1250 if not.
    // ASCII-only bytes never leave decoder state behind, so it's safe to keep detecting
    // across chunks until a non-ASCII byte finally shows up.
    private bool _autoDetecting = true;
    private readonly LineAccumulator _lineAccumulator = new();
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private readonly HashSet<byte> _enabledLocalOptions = [];
    private readonly HashSet<byte> _enabledRemoteOptions = [];

    private TcpClient? _client;
    private NetworkStream? _stream;

    // Incoming bytes are read from _readStream: the raw NetworkStream until MCCP2 starts,
    // then a ZLibStream layered over it. Outgoing data always goes to the raw _stream.
    private Stream? _readStream;
    private CancellationTokenSource? _sessionCancellation;
    private Task? _receiveTask;
    private bool _gmcpHandshakeSent;

    // Wersja klienta zgłaszana w GMCP Core.Hello; pochodzi z Directory.Build.props (nadpisywana przy publish przez /p:Version).
    private static readonly string ClientVersion =
        typeof(MudSession).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";

    public event Action<string>? TextReceived;

    public event Action<string>? LineReceived;

    public event Action<GmcpMessage>? GmcpReceived;

    public event Action<GmcpMessage>? GmcpSent;

    /// <summary>Raised after a complete player command has been written to the MUD transport.</summary>
    public event Action<string>? CommandSent;

    public event Action<string>? StatusChanged;

    public event Action<Exception>? ConnectionError;

    /// <summary>
    /// Raised after the transport has been fully cleaned up, regardless of whether
    /// the peer closed the connection or the client disconnected explicitly.
    /// </summary>
    public event Action? ConnectionClosed;

    public bool IsConnected => _stream is not null;

    /// <summary>
    /// Encoding used to decode incoming bytes and encode outgoing commands: one of the
    /// <see cref="MudTextEncodings"/> names. Defaults to <see cref="MudTextEncodings.Auto"/>,
    /// which assumes UTF-8 until the server's first non-ASCII bytes prove otherwise, then
    /// locks onto Windows-1250 — set an explicit encoding to skip detection entirely.
    /// </summary>
    public string EncodingMode
    {
        get => _encodingMode;
        set
        {
            _encodingMode = value;
            _autoDetecting = value == MudTextEncodings.Auto;
            _mudEncoding = MudTextEncodings.Resolve(_autoDetecting ? MudTextEncodings.Utf8 : value);
            _textDecoder = _mudEncoding.GetDecoder();
        }
    }

    public async Task ConnectAsync(string host, int port, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(host);
        ArgumentOutOfRangeException.ThrowIfLessThan(port, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(port, 65535);

        await DisconnectAsync().ConfigureAwait(false);

        StatusChanged?.Invoke($"Łączenie z {host}:{port}...");

        var client = new TcpClient
        {
            NoDelay = true,
        };

        try
        {
            await client.ConnectAsync(host, port, cancellationToken).ConfigureAwait(false);

            _client = client;
            _stream = client.GetStream();
            _readStream = _stream;
            _sessionCancellation = new CancellationTokenSource();
            _gmcpHandshakeSent = false;
            _enabledLocalOptions.Clear();
            _enabledRemoteOptions.Clear();

            if (_encodingMode == MudTextEncodings.Auto)
            {
                // Redo detection from scratch on every new connection.
                _mudEncoding = MudTextEncodings.Resolve(MudTextEncodings.Utf8);
                _textDecoder = _mudEncoding.GetDecoder();
                _autoDetecting = true;
            }
            else
            {
                _textDecoder.Reset();
            }

            StatusChanged?.Invoke($"Połączono z {host}:{port}");

            _receiveTask = ReceiveLoopAsync(_sessionCancellation.Token);
            await SendInitialNegotiationAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            client.Dispose();
            _client = null;
            _stream = null;
            _readStream = null;
            throw;
        }
    }

    public async Task DisconnectAsync()
    {
        var cancellation = Interlocked.Exchange(ref _sessionCancellation, null);
        var receiveTask = Interlocked.Exchange(ref _receiveTask, null);
        var readStream = Interlocked.Exchange(ref _readStream, null);
        var stream = Interlocked.Exchange(ref _stream, null);
        var client = Interlocked.Exchange(ref _client, null);

        cancellation?.Cancel();
        if (!ReferenceEquals(readStream, stream))
        {
            readStream?.Dispose();
        }

        stream?.Dispose();
        client?.Dispose();

        if (receiveTask is not null)
        {
            try
            {
                await receiveTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected during disconnect.
            }
            catch (ObjectDisposedException)
            {
                // Expected when closing the stream interrupts ReadAsync.
            }
        }

        cancellation?.Dispose();
        StatusChanged?.Invoke("Rozłączono");
    }

    public async Task SendCommandAsync(string command, CancellationToken cancellationToken = default)
    {
        var bytes = _mudEncoding.GetBytes(command + "\r\n");
        await SendRawAsync(TelnetWriter.EscapeData(bytes), cancellationToken).ConfigureAwait(false);
        CommandSent?.Invoke(command);
    }

    public async Task SendGmcpAsync(
        string package,
        string? json = null,
        CancellationToken cancellationToken = default)
    {
        var text = string.IsNullOrWhiteSpace(json) ? package : $"{package} {json}";
        var payload = Encoding.UTF8.GetBytes(text);
        await SendRawAsync(
            TelnetWriter.Subnegotiation(TelnetConstants.Gmcp, payload),
            cancellationToken).ConfigureAwait(false);

        GmcpSent?.Invoke(new GmcpMessage(package, json ?? string.Empty));
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(16 * 1024);

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var readStream = _readStream;
                if (readStream is null)
                {
                    break;
                }

                var bytesRead = await readStream
                    .ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)
                    .ConfigureAwait(false);

                if (bytesRead == 0)
                {
                    if (!ReferenceEquals(readStream, _stream) && _stream is not null)
                    {
                        // The server finished the zlib stream (MCCP2 allows ending compression
                        // without closing the connection). Fall back to the raw stream.
                        _readStream = _stream;
                        readStream.Dispose();
                        StatusChanged?.Invoke("MCCP: kompresja wyłączona przez serwer");
                        continue;
                    }

                    break;
                }

                var offset = 0;
                while (offset < bytesRead)
                {
                    var tokens = _parser.Feed(buffer.AsSpan(offset, bytesRead - offset), out var consumed);
                    offset += consumed;

                    var startCompression = false;
                    foreach (var token in tokens)
                    {
                        if (token is TelnetSubnegotiationToken { Option: TelnetConstants.Mccp2 })
                        {
                            startCompression = true;
                            continue;
                        }

                        await HandleTokenAsync(token, cancellationToken).ConfigureAwait(false);
                    }

                    if (startCompression)
                    {
                        EnableCompression(buffer.AsSpan(offset, bytesRead - offset));
                        offset = bytesRead;
                    }
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Normal disconnect.
        }
        catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested)
        {
            // Normal disconnect caused by disposing NetworkStream.
        }
        catch (Exception exception)
        {
            ConnectionError?.Invoke(exception);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);

            var readStream = Interlocked.Exchange(ref _readStream, null);
            var rawStream = Interlocked.Exchange(ref _stream, null);
            if (!ReferenceEquals(readStream, rawStream))
            {
                readStream?.Dispose();
            }

            rawStream?.Dispose();
            Interlocked.Exchange(ref _client, null)?.Dispose();
            StatusChanged?.Invoke("Połączenie zakończone");
            ConnectionClosed?.Invoke();
        }
    }

    private void EnableCompression(ReadOnlySpan<byte> leftover)
    {
        var rawStream = _stream;
        if (rawStream is null)
        {
            return;
        }

        // Bytes already read after IAC SB 86 IAC SE are the beginning of the zlib stream,
        // so they must be fed to the decompressor before any further network reads.
        _readStream = new ZLibStream(
            new PrefixedReadStream(leftover.ToArray(), rawStream),
            CompressionMode.Decompress);

        StatusChanged?.Invoke("MCCP: kompresja włączona");
    }

    private async Task HandleTokenAsync(TelnetToken token, CancellationToken cancellationToken)
    {
        switch (token)
        {
            case TelnetDataToken dataToken:
                HandleTextData(dataToken.Data);
                break;

            case TelnetNegotiationToken negotiationToken:
                await HandleNegotiationAsync(negotiationToken, cancellationToken).ConfigureAwait(false);
                break;

            case TelnetSubnegotiationToken subnegotiationToken:
                await HandleSubnegotiationAsync(subnegotiationToken, cancellationToken).ConfigureAwait(false);
                break;

            case TelnetCommandToken commandToken:
                if (commandToken.Command is TelnetConstants.GoAhead or TelnetConstants.EndOfRecord)
                {
                    // A prompt may not end with a newline. The UI already receives the text chunk;
                    // later this command can become an explicit PromptReceived event.
                }

                break;
        }
    }

    private void HandleTextData(byte[] data)
    {
        if (_autoDetecting)
        {
            DetectEncodingIfNeeded(data);
        }

        var chars = ArrayPool<char>.Shared.Rent(_mudEncoding.GetMaxCharCount(data.Length + 4));

        try
        {
            _textDecoder.Convert(
                data.AsSpan(),
                chars.AsSpan(),
                flush: false,
                out _,
                out var charsUsed,
                out _);

            if (charsUsed == 0)
            {
                return;
            }

            var text = new string(chars, 0, charsUsed);
            TextReceived?.Invoke(text);

            foreach (var line in _lineAccumulator.Feed(text))
            {
                LineReceived?.Invoke(line);
            }
        }
        finally
        {
            ArrayPool<char>.Shared.Return(chars);
        }
    }

    /// <summary>
    /// ASCII gives no signal about the server's real encoding, so detection waits for the
    /// first non-ASCII byte before deciding. Once decided, <see cref="_autoDetecting"/> is
    /// cleared and this never runs again for the connection.
    /// </summary>
    private void DetectEncodingIfNeeded(byte[] data)
    {
        var hasNonAscii = false;
        foreach (var b in data)
        {
            if (b >= 0x80)
            {
                hasNonAscii = true;
                break;
            }
        }

        if (!hasNonAscii)
        {
            return;
        }

        _autoDetecting = false;

        if (!MudTextEncodings.LooksLikeUtf8(data))
        {
            _mudEncoding = MudTextEncodings.Resolve(MudTextEncodings.AutoFallback);
            _textDecoder = _mudEncoding.GetDecoder();
            StatusChanged?.Invoke($"Wykryto kodowanie serwera: {MudTextEncodings.AutoFallback}");
        }
        else
        {
            StatusChanged?.Invoke($"Wykryto kodowanie serwera: {MudTextEncodings.Utf8}");
        }
    }

    private async Task HandleNegotiationAsync(
        TelnetNegotiationToken token,
        CancellationToken cancellationToken)
    {
        switch (token.Command)
        {
            case TelnetConstants.Will:
                await HandleRemoteWillAsync(token.Option, cancellationToken).ConfigureAwait(false);
                break;

            case TelnetConstants.Wont:
                _enabledRemoteOptions.Remove(token.Option);
                break;

            case TelnetConstants.Do:
                await HandleRemoteDoAsync(token.Option, cancellationToken).ConfigureAwait(false);
                break;

            case TelnetConstants.Dont:
                _enabledLocalOptions.Remove(token.Option);
                break;
        }
    }

    private async Task HandleRemoteWillAsync(byte option, CancellationToken cancellationToken)
    {
        var supported = option is
            TelnetConstants.Binary or
            TelnetConstants.Echo or
            TelnetConstants.SuppressGoAhead or
            TelnetConstants.EndOfRecord or
            TelnetConstants.Mccp2 or
            TelnetConstants.Gmcp;

        if (!supported)
        {
            await SendNegotiationAsync(TelnetConstants.Dont, option, cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (_enabledRemoteOptions.Add(option))
        {
            await SendNegotiationAsync(TelnetConstants.Do, option, cancellationToken)
                .ConfigureAwait(false);
        }

        if (option == TelnetConstants.Gmcp && !_gmcpHandshakeSent)
        {
            _gmcpHandshakeSent = true;
            await SendGmcpAsync(
                "Core.Hello",
                $"{{\"client\":\"KillerMudClient\",\"version\":\"{ClientVersion}\"}}",
                cancellationToken).ConfigureAwait(false);
            await SendGmcpAsync(
                "Core.Supports.Set",
                "[\"Char 1\",\"Room 1\",\"Comm 1\",\"Group 1\"]",
                cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task HandleRemoteDoAsync(byte option, CancellationToken cancellationToken)
    {
        var supported = option is
            TelnetConstants.Binary or
            TelnetConstants.SuppressGoAhead or
            TelnetConstants.TerminalType or
            TelnetConstants.Naws;

        if (!supported)
        {
            await SendNegotiationAsync(TelnetConstants.Wont, option, cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (_enabledLocalOptions.Add(option))
        {
            await SendNegotiationAsync(TelnetConstants.Will, option, cancellationToken)
                .ConfigureAwait(false);
        }

        if (option == TelnetConstants.Naws)
        {
            await SendWindowSizeAsync(120, 40, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task HandleSubnegotiationAsync(
        TelnetSubnegotiationToken token,
        CancellationToken cancellationToken)
    {
        if (token.Option == TelnetConstants.Gmcp)
        {
            GmcpReceived?.Invoke(GmcpMessage.Parse(token.Data));
            return;
        }

        if (token.Option == TelnetConstants.TerminalType &&
            token.Data.Length > 0 &&
            token.Data[0] == TelnetConstants.TerminalTypeSend)
        {
            var terminalName = Encoding.ASCII.GetBytes("MUDCLIENT");
            var payload = new byte[terminalName.Length + 1];
            payload[0] = TelnetConstants.TerminalTypeIs;
            terminalName.CopyTo(payload.AsSpan(1));

            await SendRawAsync(
                TelnetWriter.Subnegotiation(TelnetConstants.TerminalType, payload),
                cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task SendInitialNegotiationAsync(CancellationToken cancellationToken)
    {
        await SendNegotiationAsync(TelnetConstants.Do, TelnetConstants.Gmcp, cancellationToken)
            .ConfigureAwait(false);
        await SendNegotiationAsync(TelnetConstants.Do, TelnetConstants.SuppressGoAhead, cancellationToken)
            .ConfigureAwait(false);
        await SendNegotiationAsync(TelnetConstants.Do, TelnetConstants.EndOfRecord, cancellationToken)
            .ConfigureAwait(false);
        await SendNegotiationAsync(TelnetConstants.Will, TelnetConstants.Naws, cancellationToken)
            .ConfigureAwait(false);
        await SendNegotiationAsync(TelnetConstants.Will, TelnetConstants.TerminalType, cancellationToken)
            .ConfigureAwait(false);
    }

    private Task SendNegotiationAsync(byte command, byte option, CancellationToken cancellationToken) =>
        SendRawAsync(TelnetWriter.Negotiation(command, option), cancellationToken);

    private async Task SendWindowSizeAsync(
        ushort width,
        ushort height,
        CancellationToken cancellationToken)
    {
        var payload = new byte[4];
        payload[0] = (byte)(width >> 8);
        payload[1] = (byte)(width & 0xFF);
        payload[2] = (byte)(height >> 8);
        payload[3] = (byte)(height & 0xFF);

        await SendRawAsync(
            TelnetWriter.Subnegotiation(TelnetConstants.Naws, payload),
            cancellationToken).ConfigureAwait(false);
    }

    private async Task SendRawAsync(byte[] data, CancellationToken cancellationToken)
    {
        var stream = _stream ?? throw new InvalidOperationException("MUD is not connected.");

        await _sendLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await stream.WriteAsync(data.AsMemory(), cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _sendLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync().ConfigureAwait(false);
        _sendLock.Dispose();
    }
}

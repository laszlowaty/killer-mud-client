using System.Buffers;
using System.Net.Sockets;
using System.Text;
using MudClient.Core.Gmcp;
using MudClient.Core.Telnet;
using MudClient.Core.Text;

namespace MudClient.Core.Networking;

public sealed class MudSession : IAsyncDisposable
{
    private static readonly Encoding MudEncoding = new UTF8Encoding(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: false);

    private readonly TelnetParser _parser = new();
    private readonly Decoder _textDecoder = MudEncoding.GetDecoder();
    private readonly LineAccumulator _lineAccumulator = new();
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private readonly HashSet<byte> _enabledLocalOptions = [];
    private readonly HashSet<byte> _enabledRemoteOptions = [];

    private TcpClient? _client;
    private NetworkStream? _stream;
    private CancellationTokenSource? _sessionCancellation;
    private Task? _receiveTask;
    private bool _gmcpHandshakeSent;

    public event Action<string>? TextReceived;

    public event Action<string>? LineReceived;

    public event Action<GmcpMessage>? GmcpReceived;

    public event Action<GmcpMessage>? GmcpSent;

    public event Action<string>? StatusChanged;

    public event Action<Exception>? ConnectionError;

    public bool IsConnected => _stream is not null;

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
            _sessionCancellation = new CancellationTokenSource();
            _gmcpHandshakeSent = false;
            _enabledLocalOptions.Clear();
            _enabledRemoteOptions.Clear();
            _textDecoder.Reset();

            StatusChanged?.Invoke($"Połączono z {host}:{port}");

            _receiveTask = ReceiveLoopAsync(_sessionCancellation.Token);
            await SendInitialNegotiationAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            client.Dispose();
            _client = null;
            _stream = null;
            throw;
        }
    }

    public async Task DisconnectAsync()
    {
        var cancellation = Interlocked.Exchange(ref _sessionCancellation, null);
        var receiveTask = Interlocked.Exchange(ref _receiveTask, null);
        var stream = Interlocked.Exchange(ref _stream, null);
        var client = Interlocked.Exchange(ref _client, null);

        cancellation?.Cancel();
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
        var bytes = MudEncoding.GetBytes(command + "\r\n");
        await SendRawAsync(TelnetWriter.EscapeData(bytes), cancellationToken).ConfigureAwait(false);
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
                var stream = _stream;
                if (stream is null)
                {
                    break;
                }

                var bytesRead = await stream
                    .ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)
                    .ConfigureAwait(false);

                if (bytesRead == 0)
                {
                    break;
                }

                var tokens = _parser.Feed(buffer.AsSpan(0, bytesRead));
                foreach (var token in tokens)
                {
                    await HandleTokenAsync(token, cancellationToken).ConfigureAwait(false);
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

            Interlocked.Exchange(ref _stream, null)?.Dispose();
            Interlocked.Exchange(ref _client, null)?.Dispose();
            StatusChanged?.Invoke("Połączenie zakończone");
        }
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
        var chars = ArrayPool<char>.Shared.Rent(MudEncoding.GetMaxCharCount(data.Length + 4));

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
                "{\"client\":\"MudClientStarter\",\"version\":\"0.1.0\"}",
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

using System.Text;
using System.Threading.Channels;
using MudClient.App.Controls;

namespace MudClient.App.Services;

/// <summary>
/// Serializes visible MUD output to one UTF-8 text file per application session without
/// blocking the networking or UI thread. ANSI control sequences are omitted from the file.
/// </summary>
public sealed class GameSessionLogger : IAsyncDisposable
{
    private readonly IGameSessionLogStorage _storage;
    private readonly Action<string>? _reportError;
    private readonly Channel<Message> _messages = Channel.CreateUnbounded<Message>(
        new UnboundedChannelOptions { SingleReader = true, AllowSynchronousContinuations = false });
    private readonly CancellationTokenSource _cancellation = new();
    private readonly Task _worker;
    private int _disposed;

    public GameSessionLogger(
        IGameSessionLogStorage storage,
        Action<string>? reportError = null)
    {
        _storage = storage;
        _reportError = reportError;
        _worker = ProcessAsync(_cancellation.Token);
    }

    public void Configure(bool enabled, string folderIdentifier, string? profileName)
    {
        if (Volatile.Read(ref _disposed) == 0)
        {
            _messages.Writer.TryWrite(new ConfigureMessage(
                enabled,
                folderIdentifier?.Trim() ?? string.Empty,
                profileName?.Trim() ?? string.Empty));
        }
    }

    public void Write(string text)
    {
        if (text.Length > 0 && Volatile.Read(ref _disposed) == 0)
        {
            _messages.Writer.TryWrite(new TextMessage(text));
        }
    }

    public void BeginSession(string? profileName)
    {
        if (Volatile.Read(ref _disposed) == 0)
        {
            _messages.Writer.TryWrite(new BeginSessionMessage(profileName?.Trim() ?? string.Empty));
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _messages.Writer.TryComplete();
        try
        {
            await _worker.ConfigureAwait(false);
        }
        finally
        {
            _cancellation.Cancel();
            _cancellation.Dispose();
        }
    }

    private async Task ProcessAsync(CancellationToken cancellationToken)
    {
        StreamWriter? writer = null;
        var parser = new AnsiStreamParser();
        var enabled = false;
        var folderIdentifier = string.Empty;
        var profileName = string.Empty;
        var fileSequence = 0;

        try
        {
            await foreach (var message in _messages.Reader.ReadAllAsync(cancellationToken))
            {
                if (message is ConfigureMessage configuration)
                {
                    var changed = enabled != configuration.Enabled
                        || !string.Equals(folderIdentifier, configuration.FolderIdentifier, StringComparison.Ordinal)
                        || !string.Equals(profileName, configuration.ProfileName, StringComparison.Ordinal);
                    enabled = configuration.Enabled;
                    folderIdentifier = configuration.FolderIdentifier;
                    profileName = configuration.ProfileName;
                    if (changed && writer is not null)
                    {
                        await writer.DisposeAsync().ConfigureAwait(false);
                        writer = null;
                        parser = new AnsiStreamParser();
                    }

                    continue;
                }

                if (message is BeginSessionMessage beginSession)
                {
                    profileName = beginSession.ProfileName;
                    if (writer is not null)
                    {
                        await writer.DisposeAsync().ConfigureAwait(false);
                        writer = null;
                        parser = new AnsiStreamParser();
                    }

                    continue;
                }

                if (!enabled || folderIdentifier.Length == 0 || message is not TextMessage textMessage)
                {
                    continue;
                }

                try
                {
                    if (writer is null)
                    {
                        var stream = await _storage.CreateFileAsync(
                            folderIdentifier,
                            CreateFileName(profileName, ++fileSequence),
                            cancellationToken).ConfigureAwait(false);
                        writer = new StreamWriter(stream, new UTF8Encoding(false), bufferSize: 4096);
                        await writer.WriteLineAsync(
                            $"KillerMudClient — zapis sesji {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}")
                            .ConfigureAwait(false);
                        if (profileName.Length > 0)
                        {
                            await writer.WriteLineAsync($"Profil: {profileName}").ConfigureAwait(false);
                        }

                        await writer.WriteLineAsync().ConfigureAwait(false);
                    }

                    var plainText = ToPlainText(parser.Feed(textMessage.Text));
                    if (plainText.Length > 0)
                    {
                        await writer.WriteAsync(plainText.AsMemory(), cancellationToken).ConfigureAwait(false);
                        await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
                    }
                }
                catch (Exception exception) when (exception is IOException
                    or UnauthorizedAccessException
                    or InvalidOperationException)
                {
                    if (writer is not null)
                    {
                        await writer.DisposeAsync().ConfigureAwait(false);
                        writer = null;
                    }

                    enabled = false;
                    _reportError?.Invoke($"Nie udało się zapisać sesji gry: {exception.Message}");
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Expected only if application teardown cancels the logger after its queue is closed.
        }
        finally
        {
            if (writer is not null)
            {
                await writer.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    private static string CreateFileName(string profileName, int sequence)
    {
        var safeProfile = new string(profileName
            .Select(character => Path.GetInvalidFileNameChars().Contains(character) ? '_' : character)
            .ToArray()).Trim();
        if (safeProfile.Length == 0)
        {
            safeProfile = "sesja";
        }

        return $"KillerMudClient-{safeProfile}-{DateTime.Now:yyyy-MM-dd_HH-mm-ss-fff}-{sequence:000}.txt";
    }

    private static string ToPlainText(IReadOnlyList<AnsiToken> tokens)
    {
        var output = new StringBuilder();
        foreach (var token in tokens)
        {
            switch (token)
            {
                case AnsiTextToken text:
                    output.Append(text.Text);
                    break;
                case AnsiNewLineToken:
                    output.Append('\n');
                    break;
                case AnsiCarriageReturnToken:
                    output.Append('\r');
                    break;
            }
        }

        return output.ToString();
    }

    private abstract record Message;
    private sealed record ConfigureMessage(
        bool Enabled,
        string FolderIdentifier,
        string ProfileName) : Message;
    private sealed record BeginSessionMessage(string ProfileName) : Message;
    private sealed record TextMessage(string Text) : Message;
}

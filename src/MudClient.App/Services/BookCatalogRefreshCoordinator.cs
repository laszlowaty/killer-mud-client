using System.Threading.Channels;
using System.Text;
using System.Text.RegularExpressions;
using MudClient.App.Models;
using MudClient.Core.Killeropedia;

namespace MudClient.App.Services;

public sealed record BookCatalogRefreshProgress(string Stage, int Completed, int Total)
{
    public string DisplayText => Total <= 0 ? Stage : $"{Stage} ({Completed}/{Total})";
}

/// <summary>
/// Coordinates the creator-only booklist conversation. Incoming complete MUD lines are supplied
/// through <see cref="TryCaptureLine"/> while the coordinator serially sends class and detail commands.
/// </summary>
public sealed class BookCatalogRefreshCoordinator
{
    public static readonly IReadOnlyList<string> BookClasses = ["druid", "mag", "paladyn", "nomad", "kleryk"];

    private readonly object _captureLock = new();
    private readonly TimeSpan _classQuietPeriod;
    private readonly TimeSpan _detailQuietPeriod;
    private readonly TimeSpan _responseTimeout;
    private CaptureSession? _activeCapture;

    private static readonly Regex MudPromptRegex = new(
        @"<\d+/\d+hp\b[^\r\n>]*\b\d+/\d+mv\b[^\r\n>]*>",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    public BookCatalogRefreshCoordinator(
        TimeSpan? classQuietPeriod = null,
        TimeSpan? detailQuietPeriod = null,
        TimeSpan? responseTimeout = null)
    {
        _classQuietPeriod = classQuietPeriod ?? TimeSpan.FromSeconds(1);
        _detailQuietPeriod = detailQuietPeriod ?? TimeSpan.FromMilliseconds(500);
        _responseTimeout = responseTimeout ?? TimeSpan.FromSeconds(30);
    }

    public bool IsCapturing
    {
        get
        {
            lock (_captureLock)
            {
                return _activeCapture is not null;
            }
        }
    }

    public bool TryCaptureLine(string line)
    {
        lock (_captureLock)
        {
            if (_activeCapture is not { } capture)
            {
                return false;
            }

            capture.Lines.Writer.TryWrite(line);
            capture.Activity.Writer.TryWrite(true);
            return true;
        }
    }

    /// <summary>Signals response activity even when the MUD returned only a prompt without a newline.</summary>
    public void ObserveText(string text)
    {
        if (text.Length == 0)
        {
            return;
        }

        lock (_captureLock)
        {
            if (_activeCapture is { } capture)
            {
                capture.Text.Writer.TryWrite(text);
                capture.Activity.Writer.TryWrite(true);
            }
        }
    }

    public async Task<BookCatalogDocument> RefreshAsync(
        Func<string, CancellationToken, Task> sendCommandAsync,
        IProgress<BookCatalogRefreshProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sendCommandAsync);
        var books = new Dictionary<int, MutableBook>();

        for (var index = 0; index < BookClasses.Count; index++)
        {
            var bookClass = BookClasses[index];
            progress?.Report(new BookCatalogRefreshProgress(
                $"Pobieranie listy: {bookClass}", index, BookClasses.Count));
            var lines = await CapturePagedClassResponseAsync(
                $"booklist class {bookClass}",
                sendCommandAsync,
                BookListParser.ContainsClassListHeader,
                _classQuietPeriod,
                _responseTimeout,
                cancellationToken).ConfigureAwait(false);

            foreach (var summary in BookListParser.ParseClassList(lines))
            {
                if (!books.TryGetValue(summary.Vnum, out var book))
                {
                    book = new MutableBook(summary.Vnum, summary.Name);
                    books.Add(summary.Vnum, book);
                }

                book.AddClass(bookClass);
                book.AddSpells(summary.Spells);
            }
        }

        var orderedBooks = books.Values.OrderBy(book => book.Vnum).ToArray();
        for (var index = 0; index < orderedBooks.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var book = orderedBooks[index];
            progress?.Report(new BookCatalogRefreshProgress(
                $"Szczegóły księgi {book.Vnum}", index, orderedBooks.Length));
            var lines = await CaptureResponseAsync(
                token => sendCommandAsync($"booklist {book.Vnum}", token),
                BookListParser.ContainsDetailsHeader,
                _detailQuietPeriod,
                _responseTimeout,
                cancellationToken).ConfigureAwait(false);
            book.ApplyDetails(BookListParser.ParseDetails(lines));
        }

        progress?.Report(new BookCatalogRefreshProgress("Zapisywanie katalogu", orderedBooks.Length, orderedBooks.Length));
        return new BookCatalogDocument
        {
            GeneratedAtUtc = DateTimeOffset.UtcNow,
            Classes = BookClasses.ToList(),
            Books = orderedBooks.Select(book => book.ToEntry()).ToList(),
        };
    }

    private async Task<IReadOnlyList<string>> CapturePagedClassResponseAsync(
        string command,
        Func<string, CancellationToken, Task> sendCommandAsync,
        Func<IEnumerable<string>, bool> containsExpectedHeader,
        TimeSpan quietPeriod,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var capture = BeginCapture();
        using var timeoutCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCancellation.CancelAfter(timeout);
        var lines = new List<string>();

        try
        {
            var latestResponse = await SendAndWaitForResponseAsync(
                capture,
                lines,
                token => sendCommandAsync(command, token),
                containsExpectedHeader,
                quietPeriod,
                timeoutCancellation.Token).ConfigureAwait(false);

            for (var page = 0;
                 page < 3 && BookListParser.ContainsPagerPrompt(latestResponse);
                 page++)
            {
                // Continue only when the last page explicitly requested Enter. A short class
                // list has no pager marker and must proceed directly to booklist <vnum>.
                latestResponse = await SendAndWaitForResponseAsync(
                    capture,
                    lines,
                    token => sendCommandAsync(string.Empty, token),
                    containsExpectedHeader,
                    quietPeriod,
                    timeoutCancellation.Token).ConfigureAwait(false);
            }

            return lines;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("MUD nie odpowiedział na komendę lub kontynuację booklist w wyznaczonym czasie.");
        }
        finally
        {
            EndCapture(capture);
        }
    }

    private async Task<IReadOnlyList<string>> CaptureResponseAsync(
        Func<CancellationToken, Task> sendAsync,
        Func<IEnumerable<string>, bool> containsExpectedHeader,
        TimeSpan quietPeriod,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var capture = BeginCapture();
        using var timeoutCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCancellation.CancelAfter(timeout);
        var lines = new List<string>();

        try
        {
            await SendAndWaitForResponseAsync(
                capture,
                lines,
                sendAsync,
                containsExpectedHeader,
                quietPeriod,
                timeoutCancellation.Token).ConfigureAwait(false);
            return lines;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("MUD nie zwrócił kompletnej odpowiedzi booklist w wyznaczonym czasie.");
        }
        finally
        {
            EndCapture(capture);
        }
    }

    private static async Task<IReadOnlyList<string>> SendAndWaitForResponseAsync(
        CaptureSession capture,
        List<string> lines,
        Func<CancellationToken, Task> sendAsync,
        Func<IEnumerable<string>, bool> containsExpectedHeader,
        TimeSpan quietPeriod,
        CancellationToken cancellationToken)
    {
        DrainCapture(capture, lines);
        var responseStart = lines.Count;
        var responseText = new StringBuilder();
        await sendAsync(cancellationToken).ConfigureAwait(false);

        // A response may be a full page, a line, or only a fresh prompt. Always require
        // activity after this specific send before allowing the next pager newline.
        await capture.Activity.Reader.ReadAsync(cancellationToken).ConfigureAwait(false);
        DrainCapture(capture, lines, responseText);

        if (containsExpectedHeader(lines) && ContainsCompletionPrompt(responseText))
        {
            return lines.Skip(responseStart).ToArray();
        }

        while (true)
        {
            await Task.Delay(quietPeriod, cancellationToken).ConfigureAwait(false);
            var drained = DrainCapture(capture, lines, responseText);
            if (containsExpectedHeader(lines)
                && (ContainsCompletionPrompt(responseText) || !drained.HadLines))
            {
                return lines.Skip(responseStart).ToArray();
            }
        }
    }

    private static bool ContainsCompletionPrompt(StringBuilder responseText)
    {
        var text = responseText.ToString();
        var listHeader = text.IndexOf("lista ksiag dla klasy", StringComparison.OrdinalIgnoreCase);
        var detailsHeader = text.IndexOf("Informacje na temat ksiegi", StringComparison.OrdinalIgnoreCase);
        var headerIndex = Math.Max(listHeader, detailsHeader);
        if (headerIndex < 0)
        {
            return false;
        }

        return MudPromptRegex.Matches(text).Any(match => match.Index > headerIndex);
    }

    private CaptureSession BeginCapture()
    {
        var capture = new CaptureSession();
        lock (_captureLock)
        {
            if (_activeCapture is not null)
            {
                throw new InvalidOperationException("Inne pobieranie odpowiedzi booklist jest już aktywne.");
            }

            _activeCapture = capture;
        }

        return capture;
    }

    private void EndCapture(CaptureSession capture)
    {
        lock (_captureLock)
        {
            if (ReferenceEquals(_activeCapture, capture))
            {
                _activeCapture = null;
            }
        }

        capture.Lines.Writer.TryComplete();
        capture.Text.Writer.TryComplete();
        capture.Activity.Writer.TryComplete();
    }

    private static DrainResult DrainCapture(
        CaptureSession capture,
        List<string> lines,
        StringBuilder? responseText = null)
    {
        var hadActivity = false;
        var hadLines = false;
        while (capture.Activity.Reader.TryRead(out _))
        {
            hadActivity = true;
        }

        while (capture.Lines.Reader.TryRead(out var line))
        {
            lines.Add(line);
            hadLines = true;
        }

        while (capture.Text.Reader.TryRead(out var text))
        {
            responseText?.Append(text);
        }

        return new DrainResult(hadActivity, hadLines);
    }

    private readonly record struct DrainResult(bool HadActivity, bool HadLines);

    private sealed class CaptureSession
    {
        public Channel<string> Lines { get; } = Channel.CreateUnbounded<string>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
        });

        public Channel<bool> Activity { get; } = Channel.CreateUnbounded<bool>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
        });

        public Channel<string> Text { get; } = Channel.CreateUnbounded<string>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
        });
    }

    private sealed class MutableBook(int vnum, string name)
    {
        private readonly HashSet<string> _classes = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _spells = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> _locations = [];
        private string _name = name;

        public int Vnum { get; } = vnum;

        public void AddClass(string bookClass) => _classes.Add(bookClass);

        public void AddSpells(IEnumerable<string> spells)
        {
            foreach (var spell in spells)
            {
                _spells.Add(spell);
            }
        }

        public void ApplyDetails(BookListDetails details)
        {
            _name = details.Name;
            AddSpells(details.Spells);
            _locations.Clear();
            _locations.AddRange(details.LoadLocations);
        }

        public BookEntry ToEntry() => new()
        {
            Vnum = Vnum,
            Name = _name,
            Classes = BookClasses.Where(bookClass => _classes.Contains(bookClass)).ToList(),
            Spells = _spells.OrderBy(spell => spell, StringComparer.OrdinalIgnoreCase).ToList(),
            LoadLocations = _locations.ToList(),
        };
    }
}

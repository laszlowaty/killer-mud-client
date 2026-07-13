using System.Text.Json;
using MudClient.App.Models;
using MudClient.App.Services;

namespace MudClient.App.Tests;

public sealed class BookCatalogRefreshTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "KillerMudClient_Books_" + Guid.NewGuid().ToString("N"));

    public BookCatalogRefreshTests()
    {
        Directory.CreateDirectory(_directory);
    }

    public void Dispose()
    {
        Directory.Delete(_directory, recursive: true);
    }

    [Fact]
    public async Task Refresh_WithoutPagerPrompt_ProceedsDirectlyToUniqueBookDetails()
    {
        var coordinator = new BookCatalogRefreshCoordinator(
            TimeSpan.FromMilliseconds(5),
            TimeSpan.FromMilliseconds(5),
            TimeSpan.FromSeconds(2));
        var sent = new List<string>();

        Task Send(string command, CancellationToken cancellationToken)
        {
            sent.Add(command);
            if (command.StartsWith("booklist class ", StringComparison.Ordinal))
            {
                var bookClass = command["booklist class ".Length..];
                coordinator.TryCaptureLine($"<< lista ksiag dla klasy: {bookClass} >>");
                coordinator.TryCaptureLine("[16818] ksiega zaklec: 'force bolt' 'decay'");
                if (bookClass == "mag")
                {
                    coordinator.TryCaptureLine("[28063] starozytna ksiega: 'fireball'");
                }
            }
            else if (command == "booklist 16818")
            {
                FeedDetails(coordinator, "ksiega zaklec", "'force bolt' 'decay'", "na mobie: Zeerith'din (Podmrok)");
            }
            else if (command == "booklist 28063")
            {
                FeedDetails(coordinator, "starozytna ksiega", "'fireball'", "w skrzyni: wieża maga");
            }
            return Task.CompletedTask;
        }

        var catalog = await coordinator.RefreshAsync(Send, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(BookCatalogRefreshCoordinator.BookClasses, catalog.Classes);
        Assert.Equal(2, catalog.Books.Count);
        var sharedBook = Assert.Single(catalog.Books, book => book.Vnum == 16818);
        Assert.Equal(BookCatalogRefreshCoordinator.BookClasses, sharedBook.Classes);
        Assert.Equal(["decay", "force bolt"], sharedBook.Spells);
        Assert.Equal(["na mobie: Zeerith'din (Podmrok)"], sharedBook.LoadLocations);

        Assert.Equal(
            BookCatalogRefreshCoordinator.BookClasses.Select(bookClass => $"booklist class {bookClass}"),
            sent.Take(5));
        Assert.Equal(["booklist 16818", "booklist 28063"], sent.Skip(5));
        Assert.DoesNotContain(string.Empty, sent);
        Assert.False(coordinator.IsCapturing);
    }

    [Fact]
    public async Task Refresh_WaitsForEachPagerResponseBeforeSendingNextBlankLine()
    {
        var coordinator = new BookCatalogRefreshCoordinator(
            TimeSpan.FromMilliseconds(5),
            TimeSpan.FromMilliseconds(5),
            TimeSpan.FromSeconds(2));
        var responsePending = 0;
        var currentClass = string.Empty;
        var pagerContinuation = 0;
        var sent = new List<string>();

        Task Send(string command, CancellationToken cancellationToken)
        {
            Assert.Equal(0, Interlocked.Exchange(ref responsePending, 1));
            sent.Add(command);
            if (command.StartsWith("booklist class ", StringComparison.Ordinal))
            {
                currentClass = command["booklist class ".Length..];
                pagerContinuation = 0;
            }

            _ = Task.Run(async () =>
            {
                await Task.Delay(10, cancellationToken);
                Interlocked.Exchange(ref responsePending, 0);
                if (command.Length == 0)
                {
                    pagerContinuation++;
                    if (pagerContinuation < 3)
                    {
                        coordinator.TryCaptureLine("[Nacisnij Enter aby kontynuowac]");
                    }
                    else
                    {
                        coordinator.ObserveText("> ");
                    }
                }
                else
                {
                    coordinator.TryCaptureLine($"<< lista ksiag dla klasy: {currentClass} >>");
                    coordinator.TryCaptureLine("[Nacisnij Enter aby kontynuowac]");
                }
            }, cancellationToken);
            return Task.CompletedTask;
        }

        var catalog = await coordinator.RefreshAsync(Send, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Empty(catalog.Books);
        Assert.Equal(0, Volatile.Read(ref responsePending));
        Assert.Equal(20, sent.Count);
    }

    [Fact]
    public async Task Refresh_MudPromptCompletesClassResponseWithoutWaitingForQuietPeriod()
    {
        var coordinator = new BookCatalogRefreshCoordinator(
            TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(2),
            TimeSpan.FromMilliseconds(500));

        Task Send(string command, CancellationToken cancellationToken)
        {
            var bookClass = command["booklist class ".Length..];
            coordinator.TryCaptureLine($"<< lista ksiag dla klasy: {bookClass} >>");
            coordinator.ObserveText(
                $"<418/488hp 90/100mv> booklist class {bookClass}\r\n"
                + $"<< lista ksiag dla klasy: {bookClass} >>\r\n"
                + "<418/488hp 164546 94/100mv 1895c 883s 708g 129m 23> [ (W) ]");
            return Task.CompletedTask;
        }

        var catalog = await coordinator.RefreshAsync(Send, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Empty(catalog.Books);
    }

    [Fact]
    public async Task Refresh_UnrelatedTextActivity_DoesNotKeepCompletedClassListOpen()
    {
        var coordinator = new BookCatalogRefreshCoordinator(
            TimeSpan.FromMilliseconds(10),
            TimeSpan.FromMilliseconds(10),
            TimeSpan.FromMilliseconds(80));
        var noiseTasks = new List<Task>();

        Task Send(string command, CancellationToken cancellationToken)
        {
            var bookClass = command["booklist class ".Length..];
            coordinator.TryCaptureLine($"<< lista ksiag dla klasy: {bookClass} >>");
            noiseTasks.Add(Task.Run(async () =>
            {
                for (var index = 0; index < 20; index++)
                {
                    coordinator.ObserveText("odświeżenie prompta bez nowej linii");
                    await Task.Delay(5);
                }
            }));
            return Task.CompletedTask;
        }

        var catalog = await coordinator.RefreshAsync(Send, cancellationToken: TestContext.Current.CancellationToken);
        await Task.WhenAll(noiseTasks);

        Assert.Empty(catalog.Books);
    }

    [Fact]
    public async Task Store_SavesAtomicallyAndLoadsGeneratedJson()
    {
        var path = Path.Combine(_directory, "killeropedia-books.json");
        var store = new BookCatalogStore(path);
        var catalog = new BookCatalogDocument
        {
            GeneratedAtUtc = DateTimeOffset.Parse("2026-07-13T12:00:00Z"),
            Classes = ["mag"],
            Books =
            [
                new BookEntry
                {
                    Vnum = 16818,
                    Name = "ksiega zaklec",
                    Classes = ["mag"],
                    Spells = ["force bolt"],
                    LoadLocations = ["na mobie: Zeerith'din (Podmrok)"],
                },
            ],
        };

        await store.SaveAsync(catalog, TestContext.Current.CancellationToken);
        var loaded = store.Load();

        Assert.Equal(16818, Assert.Single(loaded.Books).Vnum);
        Assert.False(File.Exists(path + ".tmp"));
        using var json = JsonDocument.Parse(await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken));
        Assert.Equal(16818, json.RootElement.GetProperty("books")[0].GetProperty("vnum").GetInt32());
    }

    [Fact]
    public async Task Refresh_TimesOutWithoutHeaderAndReleasesCapture()
    {
        var coordinator = new BookCatalogRefreshCoordinator(
            TimeSpan.FromMilliseconds(5),
            TimeSpan.FromMilliseconds(5),
            TimeSpan.FromMilliseconds(30));

        await Assert.ThrowsAsync<TimeoutException>(() => coordinator.RefreshAsync(
            (_, _) => Task.CompletedTask,
            cancellationToken: TestContext.Current.CancellationToken));

        Assert.False(coordinator.IsCapturing);
    }

    [Fact]
    public async Task Store_CancelledSave_PreservesPreviousCatalog()
    {
        var path = Path.Combine(_directory, "killeropedia-books.json");
        var store = new BookCatalogStore(path);
        await store.SaveAsync(new BookCatalogDocument
        {
            Books = [new BookEntry { Vnum = 1, Name = "poprzednia ksiega" }],
        }, TestContext.Current.CancellationToken);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.SaveAsync(
            new BookCatalogDocument
            {
                Books = [new BookEntry { Vnum = 2, Name = "niepelna ksiega" }],
            },
            cancellation.Token));

        Assert.Equal(1, Assert.Single(store.Load().Books).Vnum);
        Assert.False(File.Exists(path + ".tmp"));
    }

    [Fact]
    public void Store_WithoutUserFile_LoadsBundledBookSnapshot()
    {
        var store = new BookCatalogStore(Path.Combine(_directory, "nie-istnieje.json"));

        var catalog = store.Load();

        Assert.Equal(158, catalog.Books.Count);
        Assert.Equal(BookCatalogRefreshCoordinator.BookClasses, catalog.Classes);
        Assert.Contains(catalog.Books, book => book.Vnum == 16818);
    }

    private static void FeedDetails(
        BookCatalogRefreshCoordinator coordinator,
        string name,
        string spells,
        string location)
    {
        coordinator.TryCaptureLine("<< Informacje na temat ksiegi >>");
        coordinator.TryCaptureLine(name);
        coordinator.TryCaptureLine($"Zaklecia: {spells}");
        coordinator.TryCaptureLine("Laduje sie w(na):");
        coordinator.TryCaptureLine(location);
    }
}

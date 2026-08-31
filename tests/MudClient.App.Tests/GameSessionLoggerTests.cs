using MudClient.App.Services;

namespace MudClient.App.Tests;

public sealed class GameSessionLoggerTests : IAsyncDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "KillerMudClient_LogTest_" + Guid.NewGuid().ToString("N"));

    public GameSessionLoggerTests() => Directory.CreateDirectory(_directory);

    public ValueTask DisposeAsync()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }

        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task EnabledLogger_WritesPlainUtf8OutputToOneProfileFile()
    {
        await using (var logger = new GameSessionLogger(new FileGameSessionLogStorage()))
        {
            logger.Configure(true, _directory, "Łowca/Smoków");
            logger.Write("\u001b[31mCzerwony");
            logger.Write(" tekst\u001b[0m\r\nNastępna linia\n");
        }

        var path = Assert.Single(Directory.EnumerateFiles(_directory, "*.txt"));
        var content = await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken);

        Assert.Contains("Profil: Łowca/Smoków", content);
        Assert.Contains("Czerwony tekst\r\nNastępna linia\n", content);
        Assert.False(content.Contains('\u001b'));
        Assert.Contains("Łowca_Smoków", Path.GetFileName(path));
    }

    [Fact]
    public async Task DisabledLogger_DoesNotCreateAFile()
    {
        await using (var logger = new GameSessionLogger(new FileGameSessionLogStorage()))
        {
            logger.Configure(false, _directory, "Postać");
            logger.Write("Tekst, którego nie zapisujemy.\n");
        }

        Assert.Empty(Directory.EnumerateFiles(_directory));
    }

    [Fact]
    public async Task BeginSession_CreatesASeparateFileForEachConnection()
    {
        await using (var logger = new GameSessionLogger(new FileGameSessionLogStorage()))
        {
            logger.Configure(true, _directory, "Postać");
            logger.BeginSession("Postać");
            logger.Write("Pierwsze połączenie.\n");
            logger.BeginSession("Postać");
            logger.Write("Drugie połączenie.\n");
        }

        var contents = await Task.WhenAll(Directory
            .EnumerateFiles(_directory, "*.txt")
            .Select(path => File.ReadAllTextAsync(path, TestContext.Current.CancellationToken)));
        Assert.Equal(2, contents.Length);
        Assert.Contains(contents, content => content.Contains("Pierwsze połączenie."));
        Assert.Contains(contents, content => content.Contains("Drugie połączenie."));
    }
}

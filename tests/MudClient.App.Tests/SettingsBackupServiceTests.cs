using System.IO.Compression;
using MudClient.App.Services;

namespace MudClient.App.Tests;

public sealed class SettingsBackupServiceTests : IDisposable
{
    private readonly string _rootDirectory = Path.Combine(
        Path.GetTempPath(),
        "KillerMudClient_BackupTest_" + Guid.NewGuid().ToString("N"));
    private readonly string _settingsDirectory;
    private readonly string _pendingDirectory;
    private readonly SettingsBackupService _service;

    public SettingsBackupServiceTests()
    {
        _settingsDirectory = Path.Combine(_rootDirectory, "KillerMudClient");
        _pendingDirectory = Path.Combine(_rootDirectory, ".pending-import");
        Directory.CreateDirectory(_settingsDirectory);
        _service = new SettingsBackupService(_settingsDirectory, _pendingDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootDirectory))
        {
            Directory.Delete(_rootDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task ExportAsync_IncludesEveryFileAndEmptyDirectory()
    {
        File.WriteAllText(Path.Combine(_settingsDirectory, "settings.json"), "ustawienia");
        var profileDirectory = Path.Combine(_settingsDirectory, "Profiles", "konto");
        Directory.CreateDirectory(profileDirectory);
        File.WriteAllText(Path.Combine(profileDirectory, "profile.json"), "profil");
        Directory.CreateDirectory(Path.Combine(_settingsDirectory, "empty"));

        await using var output = new MemoryStream();
        await _service.ExportAsync(output, TestContext.Current.CancellationToken);
        output.Position = 0;

        using var archive = new ZipArchive(output, ZipArchiveMode.Read);
        Assert.NotNull(archive.GetEntry("backup-manifest.json"));
        Assert.Equal("ustawienia", await ReadEntryAsync(archive, "KillerMudClient/settings.json"));
        Assert.Equal("profil", await ReadEntryAsync(archive, "KillerMudClient/Profiles/konto/profile.json"));
        Assert.NotNull(archive.GetEntry("KillerMudClient/empty/"));
    }

    [Fact]
    public async Task ExportAsync_DoesNotIncludeDestinationZipPlacedInSettingsDirectory()
    {
        File.WriteAllText(Path.Combine(_settingsDirectory, "settings.json"), "ustawienia");
        var destinationPath = Path.Combine(_settingsDirectory, "moja-kopia.zip");

        await using (var output = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await _service.ExportAsync(
                output,
                TestContext.Current.CancellationToken,
                destinationPath);
        }

        using var archive = ZipFile.OpenRead(destinationPath);
        Assert.NotNull(archive.GetEntry("KillerMudClient/settings.json"));
        Assert.Null(archive.GetEntry("KillerMudClient/moja-kopia.zip"));
    }

    [Fact]
    public async Task ApplyPendingImport_ReplacesEntireSettingsDirectory()
    {
        File.WriteAllText(Path.Combine(_settingsDirectory, "old.json"), "stare");
        Directory.CreateDirectory(Path.Combine(_settingsDirectory, "OldProfiles"));
        var archive = await CreateBackupAsync(("settings.json", "nowe"), ("Profiles/account.json", "konto"));

        await _service.StageImportAsync(archive, TestContext.Current.CancellationToken);

        Assert.True(_service.ApplyPendingImport());
        Assert.False(File.Exists(Path.Combine(_settingsDirectory, "old.json")));
        Assert.False(Directory.Exists(Path.Combine(_settingsDirectory, "OldProfiles")));
        Assert.Equal("nowe", File.ReadAllText(Path.Combine(_settingsDirectory, "settings.json")));
        Assert.Equal("konto", File.ReadAllText(Path.Combine(_settingsDirectory, "Profiles", "account.json")));
        Assert.False(Directory.Exists(_pendingDirectory));
        Assert.False(_service.ApplyPendingImport());
    }

    [Fact]
    public async Task StageImportAsync_RejectsZipWithoutBackupManifest()
    {
        await using var archiveStream = new MemoryStream();
        using (var archive = new ZipArchive(archiveStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry("settings.json");
            await using var writer = new StreamWriter(entry.Open());
            await writer.WriteAsync("obcy zip");
        }
        archiveStream.Position = 0;

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            _service.StageImportAsync(archiveStream, TestContext.Current.CancellationToken));
        Assert.False(Directory.Exists(_pendingDirectory));
    }

    [Fact]
    public async Task StageImportAsync_RejectsPathOutsideSettingsDirectory()
    {
        var archiveStream = await CreateBackupAsync(("../outside.txt", "atak"));

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            _service.StageImportAsync(archiveStream, TestContext.Current.CancellationToken));
        Assert.False(File.Exists(Path.Combine(_rootDirectory, "outside.txt")));
        Assert.False(Directory.Exists(_pendingDirectory));
    }

    private static async Task<MemoryStream> CreateBackupAsync(params (string Path, string Content)[] files)
    {
        var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var manifest = archive.CreateEntry("backup-manifest.json");
            await using (var writer = new StreamWriter(manifest.Open()))
            {
                await writer.WriteAsync("{\"FormatVersion\":1,\"CreatedAtUtc\":\"2026-07-15T00:00:00Z\"}");
            }

            foreach (var file in files)
            {
                var entry = archive.CreateEntry("KillerMudClient/" + file.Path);
                await using var writer = new StreamWriter(entry.Open());
                await writer.WriteAsync(file.Content);
            }
        }

        stream.Position = 0;
        return stream;
    }

    private static async Task<string> ReadEntryAsync(ZipArchive archive, string path)
    {
        var entry = archive.GetEntry(path) ?? throw new InvalidOperationException($"Brak wpisu {path}.");
        using var reader = new StreamReader(entry.Open());
        return await reader.ReadToEndAsync();
    }
}

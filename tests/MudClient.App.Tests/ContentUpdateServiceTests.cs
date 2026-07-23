using System.IO.Compression;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using MudClient.App.Models;
using MudClient.App.Services;
using MudClient.App.ViewModels;

namespace MudClient.App.Tests;

public sealed class ContentUpdateServiceTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "KillerMudClient-ContentUpdateTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task CheckForUpdate_ReturnsOnlyChangedSupportedComponents()
    {
        var manifest = """
            {
              "schemaVersion": 1,
              "release": "2026.07.18.1",
              "minAppVersion": "0.5.0",
              "components": {
                "map": {
                  "version": "2026.07.18",
                  "url": "https://example.test/map.zip",
                  "size": 123,
                  "sha256": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"
                },
                "unknown": {
                  "version": "1",
                  "url": "https://example.test/unknown.zip",
                  "size": 1,
                  "sha256": "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"
                }
              }
            }
            """;
        var client = new HttpClient(new StaticHandler(_ => JsonResponse(manifest)));
        var service = new ContentUpdateService(
            _directory,
            client,
            new Uri("https://example.test/manifest.json"),
            "0.5.8");

        var update = await service.CheckForUpdateAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(update);
        var component = Assert.Single(update.Components);
        Assert.Equal("map", component.Name);
        Assert.Equal(123, update.DownloadSize);
    }

    [Fact]
    public async Task InstallAsync_ValidatesAndActivatesWholeKilleropediaPackage()
    {
        var package = CreateKilleropediaPackage();
        var sha256 = Convert.ToHexString(SHA256.HashData(package));
        var client = new HttpClient(new StaticHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(package),
        }));
        var service = new ContentUpdateService(_directory, client, appVersion: "0.5.8");
        var update = new ContentUpdateAvailability(
            "2026.07.18.1",
            [new ContentComponentUpdate(
                "killeropedia",
                "2026.07.18.1",
                new Uri("https://example.test/killeropedia.zip"),
                package.Length,
                sha256)]);

        var result = await service.InstallAsync(
            update,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(["killeropedia"], result.InstalledComponents);
        var activeDirectory = new ContentPathResolver(_directory).GetActiveDirectory("killeropedia");
        Assert.NotNull(activeDirectory);
        Assert.True(File.Exists(Path.Combine(activeDirectory, "lore-catalog.json.gz")));
        Assert.True(File.Exists(Path.Combine(activeDirectory, "teachers.json.gz")));
        Assert.True(File.Exists(Path.Combine(activeDirectory, "books.json")));
        Assert.True(File.Exists(Path.Combine(activeDirectory, "quests.json")));

        var secondCheckClient = new HttpClient(new StaticHandler(_ => JsonResponse($$"""
            {
              "schemaVersion": 1,
              "release": "2026.07.18.1",
              "minAppVersion": "0.5.0",
              "components": {
                "killeropedia": {
                  "version": "2026.07.18.1",
                  "url": "https://example.test/killeropedia.zip",
                  "size": {{package.Length}},
                  "sha256": "{{sha256}}"
                }
              }
            }
            """)));
        var secondService = new ContentUpdateService(
            _directory,
            secondCheckClient,
            new Uri("https://example.test/manifest.json"),
            "0.5.8");
        Assert.Null(await secondService.CheckForUpdateAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task InstallAsync_RejectsArchiveEntryOutsideComponentDirectory()
    {
        byte[] package;
        using (var buffer = new MemoryStream())
        {
            using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
            {
                var entry = archive.CreateEntry("../outside.txt");
                using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
                writer.Write("nope");
            }

            package = buffer.ToArray();
        }

        var sha256 = Convert.ToHexString(SHA256.HashData(package));
        var client = new HttpClient(new StaticHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(package),
        }));
        var service = new ContentUpdateService(_directory, client, appVersion: "0.5.8");
        var update = new ContentUpdateAvailability(
            "bad",
            [new ContentComponentUpdate(
                "map",
                "bad",
                new Uri("https://example.test/map.zip"),
                package.Length,
                sha256)]);

        await Assert.ThrowsAsync<InvalidDataException>(() => service.InstallAsync(
            update,
            cancellationToken: TestContext.Current.CancellationToken));
        Assert.Null(new ContentPathResolver(_directory).GetActiveDirectory("map"));
    }

    private static byte[] CreateKilleropediaPackage()
    {
        var assembly = typeof(MainWindowViewModel).Assembly;
        using var buffer = new MemoryStream();
        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            CopyResource(assembly, archive, "MudClient.App.Assets.Data.lore-catalog.json.gz", "lore-catalog.json.gz");
            CopyResource(assembly, archive, "MudClient.App.Assets.Data.teachers.json.gz", "teachers.json.gz");
            CopyResource(assembly, archive, "MudClient.App.Assets.Data.books.json", "books.json");
            CopyResource(assembly, archive, "MudClient.App.Assets.Data.quests.json", "quests.json");
        }

        return buffer.ToArray();
    }

    private static void CopyResource(Assembly assembly, ZipArchive archive, string resourceName, string entryName)
    {
        using var source = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Missing resource {resourceName}.");
        using var destination = archive.CreateEntry(entryName, CompressionLevel.Fastest).Open();
        source.CopyTo(destination);
    }

    private static HttpResponseMessage JsonResponse(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json"),
    };

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private sealed class StaticHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => Task.FromResult(responseFactory(request));
    }
}

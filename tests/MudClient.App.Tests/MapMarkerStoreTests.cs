using System.Text.Json;
using MudClient.App.Models;
using MudClient.App.Services;

namespace MudClient.App.Tests;

public sealed class MapMarkerStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "KillerMudClient_MapMarkers_" + Guid.NewGuid().ToString("N"));

    public MapMarkerStoreTests()
    {
        Directory.CreateDirectory(_directory);
    }

    public void Dispose()
    {
        Directory.Delete(_directory, recursive: true);
    }

    [Fact]
    public void Load_WithoutFile_ReturnsEmptyDocument()
    {
        var store = new MapMarkerStore(Path.Combine(_directory, "nie-istnieje.json"));

        var document = store.Load();

        Assert.Empty(document.Markers);
    }

    [Fact]
    public async Task SavesAtomicallyAndLoadsGeneratedJson()
    {
        var path = Path.Combine(_directory, "map-markers.json");
        var store = new MapMarkerStore(path);
        var document = new MapMarkerDocument { Markers = [new MapMarker("100", "!!")] };

        await store.SaveAsync(document, TestContext.Current.CancellationToken);
        var loaded = store.Load();

        var marker = Assert.Single(loaded.Markers);
        Assert.Equal("100", marker.Vnum);
        Assert.Equal("!!", marker.Symbol);
        Assert.False(File.Exists(path + ".tmp"));
        using var json = JsonDocument.Parse(await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken));
        Assert.Equal("100", json.RootElement.GetProperty("markers")[0].GetProperty("vnum").GetString());
    }

    [Fact]
    public async Task CancelledSave_PreservesPreviousFile()
    {
        var path = Path.Combine(_directory, "map-markers.json");
        var store = new MapMarkerStore(path);
        await store.SaveAsync(
            new MapMarkerDocument { Markers = [new MapMarker("1", "R")] },
            TestContext.Current.CancellationToken);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.SaveAsync(
            new MapMarkerDocument { Markers = [new MapMarker("2", "B")] },
            cancellation.Token));

        Assert.Equal("1", Assert.Single(store.Load().Markers).Vnum);
        Assert.False(File.Exists(path + ".tmp"));
    }

    [Fact]
    public void Load_WithMalformedJson_ThrowsInvalidDataException()
    {
        var path = Path.Combine(_directory, "map-markers.json");
        File.WriteAllText(path, "{ not json");
        var store = new MapMarkerStore(path);

        Assert.Throws<InvalidDataException>(() => store.Load());
    }
}

using MudClient.MapImageCalibrator;
using System.Text.Json.Nodes;
using System.Buffers.Binary;

namespace MudClient.App.Tests;

public sealed class MapImageCalibratorTests
{
    [Fact]
    public void NortantisLayout_LeavesAtLeastFivePercentMarginAroundSelectedRooms()
    {
        var rooms = new[]
        {
            Room(1, x: -100, y: -50),
            Room(2, x: 100, y: 50),
        };

        var layout = new NortantisExportService().CalculateLayout(rooms);

        Assert.True((-100 - layout.MinX) / (layout.MaxX - layout.MinX) >= 0.05);
        Assert.True((layout.MaxX - 100) / (layout.MaxX - layout.MinX) >= 0.05);
        Assert.True((-50 - layout.MinY) / (layout.MaxY - layout.MinY) >= 0.05);
        Assert.True((layout.MaxY - 50) / (layout.MaxY - layout.MinY) >= 0.05);
        Assert.Equal(layout.ProjectWidth * 2, layout.OverlayWidth);
        Assert.Equal(layout.ProjectHeight * 2, layout.OverlayHeight);
    }

    [Fact]
    public void SelectedConnections_ContainsOnlyEdgesBetweenSelectedRooms()
    {
        var rooms = new[]
        {
            Room(1, exits: [2, 3]),
            Room(2, exits: [1]),
        };

        var connection = Assert.Single(NortantisExportService.SelectedConnections(rooms));

        Assert.Equal(1, connection.From.Id);
        Assert.Equal(2, connection.To.Id);
    }

    [Fact]
    public void SaveProject_EnablesOverlayAndClearsManualNortantisArtwork()
    {
        using var directory = new TemporaryDirectory();
        var template = Path.Combine(directory.Path, "template.nort");
        var output = Path.Combine(directory.Path, "output.nort");
        File.WriteAllText(template,
            "{\"generatedWidth\":100,\"generatedHeight\":100,\"resolution\":1.0,\"overlayScale\":1.0," +
            "\"cityProbability\":0.01,\"edits\":{\"centerEdits\":[{\"regionId\":1}]," +
            "\"iconEdits\":[{}],\"textEdits\":[{}],\"roads\":[{}]}}");
        var layout = new NortantisExportLayout(-10, -10, 10, 10, 2048, 1024, 2);

        new NortantisExportService().SaveProject(template, output, Path.Combine(directory.Path, "rooms.png"), layout);
        var project = JsonNode.Parse(File.ReadAllText(output))!.AsObject();

        Assert.True(project["drawOverlayImage"]!.GetValue<bool>());
        Assert.False(project["drawBorder"]!.GetValue<bool>());
        Assert.Equal(2048, project["generatedWidth"]!.GetValue<int>());
        Assert.Empty(project["edits"]!["iconEdits"]!.AsArray());
        Assert.True(project["edits"]!["hasIconEdits"]!.GetValue<bool>());
        Assert.Single(project["edits"]!["centerEdits"]!.AsArray());
        var json = File.ReadAllText(output);
        Assert.Contains("\"resolution\":2.0", json);
        Assert.Contains("\"overlayScale\":1.0", json);
        Assert.Contains("\"cityProbability\":0.0", json);
    }

    [Fact]
    public void SaveOverlay_CreatesReadableTransparentPng()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "overlay.png");
        var layout = new NortantisExportLayout(-10, -10, 10, 10, 64, 32, 1);

        NortantisExportService.SaveOverlay([Room(1), Room(2, x: 5)], layout, path);

        var png = File.ReadAllBytes(path);
        Assert.Equal(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, png[..8]);
        Assert.Equal(64, BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(16, 4)));
        Assert.Equal(32, BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(20, 4)));
        Assert.Equal(6, png[25]); // RGBA, więc niezarysowane piksele pozostają przezroczyste.
    }

    private static RoomPoint Room(int id, double x = 0, double y = 0, int[]? exits = null) =>
        new(id, id.ToString(), $"Room {id}", "las", 1, x, y, 0, exits ?? []);

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "KillerMudClient.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path)) Directory.Delete(Path, recursive: true);
        }
    }
}

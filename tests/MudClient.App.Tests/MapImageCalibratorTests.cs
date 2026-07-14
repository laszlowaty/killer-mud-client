using MudClient.MapImageCalibrator;

namespace MudClient.App.Tests;

public sealed class MapImageCalibratorTests
{
    [Fact]
    public void Workspace_RoundTripsEditableImageElements()
    {
        using var directory = new TemporaryDirectory();
        Directory.CreateDirectory(Path.Combine(directory.Path, "Locations"));
        var repository = new CalibrationRepository(directory.Path);
        var layer = new LocationLayer { FileName = "test.png" };
        var workspace = new CalibrationWorkspace
        {
            ImageFile = layer.FileName,
            ImageElements =
            [
                new MapImageElement
                {
                    Id = "temple-1",
                    AssetFile = "EditorAssets/Budynki/temple.png",
                    ImageX = 120,
                    ImageY = 80,
                    Width = 64,
                    Height = 96,
                    Rotation = 15,
                    Opacity = 0.75,
                    ZIndex = 2,
                },
            ],
        };

        repository.SaveWorkspace(layer, workspace);
        var loaded = repository.LoadWorkspace(layer);

        var element = Assert.Single(loaded.ImageElements);
        Assert.Equal("temple-1", element.Id);
        Assert.Equal("EditorAssets/Budynki/temple.png", element.AssetFile);
        Assert.Equal(120, element.ImageX);
        Assert.Equal(15, element.Rotation);
        Assert.Equal(0.75, element.Opacity);
    }

    [Fact]
    public void ResolveEditorAssetPath_RejectsPathOutsideMapDirectory()
    {
        using var directory = new TemporaryDirectory();
        var repository = new CalibrationRepository(directory.Path);

        Assert.Throws<InvalidDataException>(() => repository.ResolveEditorAssetPath("../outside.png"));
    }

    [Fact]
    public void CompositePathForExport_UsesPackageBaseName()
    {
        var path = CalibrationRepository.CompositePathForExport(
            Path.Combine("CalibrationExports", "arras-20260714-120000.json"));

        Assert.Equal(
            Path.Combine("CalibrationExports", "arras-20260714-120000-composite.png"),
            path);
    }

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

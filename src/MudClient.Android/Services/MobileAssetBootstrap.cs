using Android.Content;
using Android.Content.Res;

namespace MudClient.Android.Services;

internal static class MobileAssetBootstrap
{
    private const string AssetRoot = "Map";
    private const string VersionMarker = ".map-assets-0.6.2";

    public static async Task<string> EnsureMapAssetsAsync(
        Context context,
        CancellationToken cancellationToken)
    {
        var filesDirectory = context.FilesDir?.AbsolutePath
            ?? throw new InvalidOperationException("Android nie udostępnił katalogu danych aplikacji.");
        var mapDirectory = Path.Combine(filesDirectory, "Assets", "Map");
        var markerPath = Path.Combine(mapDirectory, VersionMarker);
        var worldMapPath = Path.Combine(mapDirectory, "world-map.json");

        if (File.Exists(markerPath) && File.Exists(worldMapPath))
        {
            return filesDirectory;
        }

        var assets = context.Assets
            ?? throw new InvalidOperationException("Android nie udostępnił zasobów aplikacji.");

        await CopyDirectoryAsync(assets, AssetRoot, mapDirectory, cancellationToken)
            .ConfigureAwait(false);
        await File.WriteAllTextAsync(markerPath, "ok", cancellationToken).ConfigureAwait(false);
        return filesDirectory;
    }

    private static async Task CopyDirectoryAsync(
        AssetManager assets,
        string assetPath,
        string destinationPath,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var children = assets.List(assetPath) ?? [];
        if (children.Length == 0)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)
                ?? throw new InvalidOperationException("Zasób mapy nie ma katalogu docelowego."));
            await using var source = assets.Open(assetPath, Access.Streaming);
            await using var destination = new FileStream(
                destinationPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                64 * 1024,
                useAsync: true);
            await source.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
            return;
        }

        Directory.CreateDirectory(destinationPath);
        foreach (var child in children)
        {
            await CopyDirectoryAsync(
                    assets,
                    $"{assetPath}/{child}",
                    Path.Combine(destinationPath, child),
                    cancellationToken)
                .ConfigureAwait(false);
        }
    }
}

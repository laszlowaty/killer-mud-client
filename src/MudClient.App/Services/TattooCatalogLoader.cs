using System.Reflection;
using System.Text.Json;
using MudClient.App.Models;

namespace MudClient.App.Services;

internal static class TattooCatalogLoader
{
    private const string ResourceName = "MudClient.App.Assets.Data.tattoos.json";
    private static readonly Lazy<TattooCatalogData> Catalog = new(LoadCore);

    public static TattooCatalogData Load() => Catalog.Value;

    public static TattooCatalogData Load(string? externalPath)
    {
        if (string.IsNullOrWhiteSpace(externalPath) || !File.Exists(externalPath))
        {
            return Load();
        }

        try
        {
            return LoadFile(externalPath);
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or InvalidDataException
            or JsonException)
        {
            return Load();
        }
    }

    internal static TattooCatalogData LoadFile(string path)
    {
        using var file = File.OpenRead(path);
        return Parse(file, path);
    }

    private static TattooCatalogData LoadCore()
    {
        using var resource = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Brak osadzonego katalogu tatuaży: {ResourceName}.");
        return Parse(resource, ResourceName);
    }

    private static TattooCatalogData Parse(Stream stream, string source)
    {
        var catalog = JsonSerializer.Deserialize<TattooCatalogData>(
            stream,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (catalog is null || catalog.Bonuses.Count == 0)
        {
            throw new InvalidDataException($"Katalog tatuaży nie zawiera wpisów: {source}.");
        }

        var sortedBonuses = catalog.Bonuses
            .Where(bonus => !string.IsNullOrWhiteSpace(bonus.Name))
            .OrderBy(bonus => bonus.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();

        return catalog with { Bonuses = sortedBonuses };
    }
}

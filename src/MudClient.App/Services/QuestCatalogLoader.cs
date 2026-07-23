using System.Reflection;
using System.Text.Json;
using MudClient.App.Models;

namespace MudClient.App.Services;

internal static class QuestCatalogLoader
{
    private const string ResourceName = "MudClient.App.Assets.Data.quests.json";
    private static readonly Lazy<IReadOnlyList<QuestEntry>> Catalog = new(LoadCore);

    public static IReadOnlyList<QuestEntry> Load() => Catalog.Value;

    public static IReadOnlyList<QuestEntry> Load(string? externalPath)
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

    internal static IReadOnlyList<QuestEntry> LoadFile(string path)
    {
        using var file = File.OpenRead(path);
        return Parse(file, path);
    }

    private static IReadOnlyList<QuestEntry> LoadCore()
    {
        using var resource = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Brak osadzonego katalogu zadań: {ResourceName}.");
        return Parse(resource, ResourceName);
    }

    private static IReadOnlyList<QuestEntry> Parse(Stream stream, string source)
    {
        var entries = JsonSerializer.Deserialize<List<QuestEntry>>(
                          stream,
                          new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                      ?? throw new InvalidDataException($"Katalog zadań jest pusty: {source}.");

        var result = entries
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Name))
            .OrderBy(entry => entry.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
        return result.Length == 0
            ? throw new InvalidDataException($"Katalog zadań nie zawiera wpisów: {source}.")
            : result;
    }
}

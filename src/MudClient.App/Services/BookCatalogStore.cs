using System.Reflection;
using System.Text.Json;
using MudClient.App.Models;

namespace MudClient.App.Services;

public sealed class BookCatalogStore
{
    private const string EmbeddedResourceName = "MudClient.App.Assets.Data.books.json";
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    private readonly string _path;
    private readonly string? _fallbackPath;

    public BookCatalogStore(string? path = null, string? fallbackPath = null)
    {
        _path = path ?? System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "KillerMudClient",
            "killeropedia-books.json");
        _fallbackPath = fallbackPath;
    }

    public string Path => _path;

    public BookCatalogDocument Load()
    {
        try
        {
            if (File.Exists(_path))
            {
                using var file = File.OpenRead(_path);
                return Deserialize(file);
            }

            if (!string.IsNullOrWhiteSpace(_fallbackPath) && File.Exists(_fallbackPath))
            {
                try
                {
                    using var downloaded = File.OpenRead(_fallbackPath);
                    return Deserialize(downloaded);
                }
                catch (Exception exception) when (exception is IOException
                    or UnauthorizedAccessException
                    or JsonException)
                {
                    // A downloaded base catalog is optional; preserve the embedded fallback.
                }
            }

            using var embedded = Assembly.GetExecutingAssembly().GetManifestResourceStream(EmbeddedResourceName)
                ?? throw new InvalidOperationException($"Brak osadzonego katalogu ksiąg: {EmbeddedResourceName}.");
            return Deserialize(embedded);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("Katalog ksiąg ma nieprawidłowy format JSON.", exception);
        }
    }

    public async Task SaveAsync(BookCatalogDocument catalog, CancellationToken cancellationToken = default)
    {
        var directory = System.IO.Path.GetDirectoryName(_path)
            ?? throw new InvalidOperationException("Ścieżka katalogu ksiąg nie ma katalogu nadrzędnego.");
        Directory.CreateDirectory(directory);
        var temporaryPath = _path + ".tmp";

        try
        {
            await using (var stream = new FileStream(
                temporaryPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                16 * 1024,
                useAsync: true))
            {
                await JsonSerializer.SerializeAsync(
                    stream,
                    catalog,
                    SerializerOptions,
                    cancellationToken).ConfigureAwait(false);
            }

            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporaryPath, _path, overwrite: true);
        }
        finally
        {
            // A cancelled or failed refresh must not leave a partial file that could be loaded later.
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static BookCatalogDocument Deserialize(Stream stream) =>
        JsonSerializer.Deserialize<BookCatalogDocument>(stream, SerializerOptions)
        ?? throw new InvalidDataException("Katalog ksiąg jest pusty.");
}

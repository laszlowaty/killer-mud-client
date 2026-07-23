using System.IO.Compression;
using System.Net.Http.Json;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using MudClient.App.Models;
using MudClient.Core.Map;

namespace MudClient.App.Services;

public interface IContentUpdateService
{
    Task<ContentUpdateAvailability?> CheckForUpdateAsync(CancellationToken cancellationToken = default);

    Task<ContentInstallResult> InstallAsync(
        ContentUpdateAvailability update,
        IProgress<ContentUpdateProgress>? progress = null,
        CancellationToken cancellationToken = default);
}

internal sealed class ContentUpdateService : IContentUpdateService
{
    internal static readonly Uri DefaultManifestUri = new(
        "https://laszlowaty.github.io/killer-mud-client/content/manifest.json");

    private const long MaximumPackageSize = 256L * 1024 * 1024;
    private const long MaximumExtractedSize = 512L * 1024 * 1024;
    private static readonly HttpClient SharedHttpClient = CreateHttpClient();
    private static readonly HashSet<string> SupportedComponents = new(StringComparer.OrdinalIgnoreCase)
    {
        "map",
        "killeropedia",
    };

    private readonly ContentPathResolver _paths;
    private readonly HttpClient _httpClient;
    private readonly Uri _manifestUri;
    private readonly Version _appVersion;
    private readonly SemaphoreSlim _installLock = new(1, 1);

    public ContentUpdateService(
        string dataRoot,
        HttpClient? httpClient = null,
        Uri? manifestUri = null,
        string? appVersion = null)
    {
        _paths = new ContentPathResolver(dataRoot);
        _httpClient = httpClient ?? SharedHttpClient;
        _manifestUri = manifestUri ?? DefaultManifestUri;
        _appVersion = ParseVersion(appVersion ?? GetCurrentVersion()) ?? new Version(0, 0);
    }

    public async Task<ContentUpdateAvailability?> CheckForUpdateAsync(
        CancellationToken cancellationToken = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(8));

        var manifest = await _httpClient.GetFromJsonAsync<ContentManifest>(_manifestUri, timeout.Token)
            .ConfigureAwait(false);
        if (manifest is null || manifest.SchemaVersion != 1)
        {
            throw new InvalidDataException("Manifest aktualizacji danych ma nieobsługiwany format.");
        }

        var minimumVersion = ParseVersion(manifest.MinAppVersion);
        if (minimumVersion is not null && _appVersion < minimumVersion)
        {
            return null;
        }

        var state = _paths.LoadState();
        var updates = new List<ContentComponentUpdate>();
        foreach (var (name, component) in manifest.Components)
        {
            if (!SupportedComponents.Contains(name)
                || string.IsNullOrWhiteSpace(component.Version)
                || component.Version.Length > 64
                || component.Url is null
                || component.Url.Scheme != Uri.UriSchemeHttps
                || component.Size <= 0
                || component.Size > MaximumPackageSize
                || !IsSha256(component.Sha256))
            {
                continue;
            }

            var installedVersion = state.Components!.GetValueOrDefault(name)?.Version;
            if (string.Equals(installedVersion, component.Version, StringComparison.Ordinal))
            {
                continue;
            }

            updates.Add(new ContentComponentUpdate(
                name,
                component.Version,
                component.Url,
                component.Size,
                component.Sha256.ToLowerInvariant()));
        }

        return updates.Count == 0
            ? null
            : new ContentUpdateAvailability(manifest.Release, updates);
    }

    public async Task<ContentInstallResult> InstallAsync(
        ContentUpdateAvailability update,
        IProgress<ContentUpdateProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(update);
        await _installLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        var stagingRoot = Path.Combine(_paths.ContentRoot, ".staging", Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(stagingRoot);
            var prepared = new List<(ContentComponentUpdate Component, string Directory, string StorageKey)>();
            foreach (var component in update.Components)
            {
                if (!SupportedComponents.Contains(component.Name)
                    || string.IsNullOrWhiteSpace(component.Version)
                    || component.Version.Length > 64
                    || component.DownloadUri.Scheme != Uri.UriSchemeHttps
                    || component.Size <= 0
                    || component.Size > MaximumPackageSize
                    || !IsSha256(component.Sha256))
                {
                    throw new InvalidDataException($"Nieprawidłowy opis paczki „{component.Name}”.");
                }

                var componentStaging = Path.Combine(stagingRoot, component.Name);
                Directory.CreateDirectory(componentStaging);
                var archivePath = Path.Combine(stagingRoot, component.Name + ".zip");
                await DownloadAsync(component, archivePath, progress, cancellationToken).ConfigureAwait(false);
                ExtractSafely(archivePath, componentStaging, cancellationToken);
                await ValidateComponentAsync(component.Name, componentStaging, cancellationToken).ConfigureAwait(false);
                prepared.Add((
                    component,
                    componentStaging,
                    ContentPathResolver.CreateStorageKey(component.Version, component.Sha256)));
            }

            var newState = _paths.LoadState();
            foreach (var item in prepared)
            {
                var finalDirectory = _paths.GetComponentDirectory(item.Component.Name, item.StorageKey);
                Directory.CreateDirectory(Path.GetDirectoryName(finalDirectory)!);
                if (!Directory.Exists(finalDirectory))
                {
                    Directory.Move(item.Directory, finalDirectory);
                }

                var previous = newState.Components!.GetValueOrDefault(item.Component.Name);
                newState.Components![item.Component.Name] = new ActiveContentComponent
                {
                    Version = item.Component.Version,
                    StorageKey = item.StorageKey,
                    InstalledAtUtc = DateTimeOffset.UtcNow,
                    PreviousVersion = previous?.Version ?? string.Empty,
                    PreviousStorageKey = previous?.StorageKey ?? string.Empty,
                };
            }

            newState.Release = update.Release;
            await _paths.SaveStateAsync(newState, cancellationToken).ConfigureAwait(false);
            foreach (var item in prepared)
            {
                CleanupOldVersions(item.Component.Name, newState.Components![item.Component.Name]);
            }
            return new ContentInstallResult(update.Release, prepared.Select(item => item.Component.Name).ToArray());
        }
        finally
        {
            try
            {
                if (Directory.Exists(stagingRoot))
                {
                    Directory.Delete(stagingRoot, recursive: true);
                }
            }
            catch (IOException)
            {
                // Staging is never active. A later cleanup can remove a file still held by antivirus software.
            }
            catch (UnauthorizedAccessException)
            {
                // Same as above: leaving inactive staging is safer than failing an already completed activation.
            }

            _installLock.Release();
        }
    }

    private void CleanupOldVersions(string componentName, ActiveContentComponent active)
    {
        var componentRoot = Path.Combine(_paths.ContentRoot, componentName);
        if (!Directory.Exists(componentRoot))
        {
            return;
        }

        var retained = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            active.StorageKey,
            active.PreviousStorageKey,
        };
        foreach (var directory in Directory.EnumerateDirectories(componentRoot))
        {
            if (retained.Contains(Path.GetFileName(directory)))
            {
                continue;
            }

            try
            {
                Directory.Delete(directory, recursive: true);
            }
            catch (IOException)
            {
                // Old inactive data can be cleaned during a later successful update.
            }
            catch (UnauthorizedAccessException)
            {
                // The active state is already safe; cleanup is best-effort only.
            }
        }
    }

    private async Task DownloadAsync(
        ContentComponentUpdate component,
        string destinationPath,
        IProgress<ContentUpdateProgress>? progress,
        CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(
                component.DownloadUri,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength is > MaximumPackageSize)
        {
            throw new InvalidDataException("Paczka aktualizacji przekracza dozwolony rozmiar.");
        }

        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using var destination = new FileStream(
            destinationPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            64 * 1024,
            useAsync: true);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[64 * 1024];
        long received = 0;
        while (true)
        {
            var count = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (count == 0)
            {
                break;
            }

            received += count;
            if (received > MaximumPackageSize || received > component.Size)
            {
                throw new InvalidDataException("Rozmiar pobranej paczki nie zgadza się z manifestem.");
            }

            hash.AppendData(buffer, 0, count);
            await destination.WriteAsync(buffer.AsMemory(0, count), cancellationToken).ConfigureAwait(false);
            progress?.Report(new ContentUpdateProgress(component.Name, received, component.Size));
        }

        if (received != component.Size
            || !string.Equals(Convert.ToHexString(hash.GetHashAndReset()), component.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Pobrana paczka jest niekompletna albo ma nieprawidłową sumę SHA-256.");
        }
    }

    private static void ExtractSafely(string archivePath, string destinationRoot, CancellationToken cancellationToken)
    {
        var normalizedRoot = Path.GetFullPath(destinationRoot) + Path.DirectorySeparatorChar;
        long extractedSize = 0;
        using var archive = ZipFile.OpenRead(archivePath);
        foreach (var entry in archive.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            extractedSize += entry.Length;
            if (extractedSize > MaximumExtractedSize)
            {
                throw new InvalidDataException("Rozpakowana paczka przekracza dozwolony rozmiar.");
            }

            var targetPath = Path.GetFullPath(Path.Combine(destinationRoot, entry.FullName));
            if (!targetPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("Paczka zawiera niedozwoloną ścieżkę pliku.");
            }

            if (string.IsNullOrEmpty(entry.Name))
            {
                Directory.CreateDirectory(targetPath);
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            entry.ExtractToFile(targetPath, overwrite: false);
        }
    }

    private static async Task ValidateComponentAsync(
        string componentName,
        string directory,
        CancellationToken cancellationToken)
    {
        if (componentName.Equals("map", StringComparison.OrdinalIgnoreCase))
        {
            var required = new[]
            {
                "world-map.json",
                "map-settings.json",
                Path.Combine("Sectors", "sectors.json"),
            };
            if (required.Any(path => !File.Exists(Path.Combine(directory, path))))
            {
                throw new InvalidDataException("Paczka mapy nie zawiera wszystkich wymaganych plików.");
            }

            _ = await new MapLoader().LoadAsync(Path.Combine(directory, "world-map.json"), cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        var lorePath = Path.Combine(directory, "lore-catalog.json.gz");
        var teacherPath = Path.Combine(directory, "teachers.json.gz");
        var booksPath = Path.Combine(directory, "books.json");
        var questsPath = Path.Combine(directory, "quests.json");
        if (!File.Exists(lorePath) || !File.Exists(teacherPath) || !File.Exists(booksPath)
            || !File.Exists(questsPath))
        {
            throw new InvalidDataException("Paczka Killeropedii nie zawiera wszystkich wymaganych katalogów.");
        }

        _ = LoreCatalogLoader.LoadFile(lorePath);
        _ = TeacherCatalogLoader.LoadFile(teacherPath);
        _ = new BookCatalogStore(booksPath).Load();
        _ = QuestCatalogLoader.LoadFile(questsPath);
    }

    private static bool IsSha256(string value) =>
        value.Length == 64 && value.All(Uri.IsHexDigit);

    private static string GetCurrentVersion()
    {
        var assembly = Assembly.GetEntryAssembly() ?? typeof(ContentUpdateService).Assembly;
        return assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
               ?? assembly.GetName().Version?.ToString()
               ?? "0.0.0";
    }

    private static Version? ParseVersion(string? value)
    {
        var core = value?.Trim().TrimStart('v', 'V').Split('-', '+')[0];
        return Version.TryParse(core, out var version) ? version : null;
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("KillerMudClient-ContentUpdater");
        return client;
    }

    private sealed class ContentManifest
    {
        public int SchemaVersion { get; init; }
        public string Release { get; init; } = string.Empty;
        public string MinAppVersion { get; init; } = string.Empty;
        public Dictionary<string, ContentManifestComponent> Components { get; init; } =
            new(StringComparer.OrdinalIgnoreCase);
    }

    private sealed class ContentManifestComponent
    {
        public string Version { get; init; } = string.Empty;
        public Uri? Url { get; init; }
        public long Size { get; init; }
        public string Sha256 { get; init; } = string.Empty;
    }
}

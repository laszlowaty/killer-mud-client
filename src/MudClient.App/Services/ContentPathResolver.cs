using System.Text.Json;

namespace MudClient.App.Services;

internal sealed class ContentPathResolver
{
    private const string StateFileName = "active.json";
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    private readonly string _contentRoot;

    public ContentPathResolver(string dataRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataRoot);
        _contentRoot = Path.Combine(dataRoot, "Content");
    }

    public string ContentRoot => _contentRoot;

    public ContentActivationState LoadState()
    {
        var path = Path.Combine(_contentRoot, StateFileName);
        if (!File.Exists(path))
        {
            return new ContentActivationState();
        }

        try
        {
            using var stream = File.OpenRead(path);
            var state = JsonSerializer.Deserialize<ContentActivationState>(stream, SerializerOptions)
                        ?? new ContentActivationState();
            state.Components ??= new Dictionary<string, ActiveContentComponent>(StringComparer.OrdinalIgnoreCase);
            return state;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            // A broken optional state file must never prevent startup; packaged data remains available.
            return new ContentActivationState();
        }
    }

    public string? GetActiveDirectory(string componentName)
    {
        var state = LoadState();
        var component = state.Components!.GetValueOrDefault(componentName);
        if (component is null)
        {
            return null;
        }

        foreach (var storageKey in new[] { component.StorageKey, component.PreviousStorageKey })
        {
            if (!IsSafeStorageKey(storageKey))
            {
                continue;
            }

            var path = Path.Combine(_contentRoot, componentName, storageKey);
            if (Directory.Exists(path))
            {
                return path;
            }
        }

        return null;
    }

    public string GetComponentDirectory(string componentName, string storageKey) =>
        Path.Combine(_contentRoot, componentName, storageKey);

    public async Task SaveStateAsync(ContentActivationState state, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_contentRoot);
        var path = Path.Combine(_contentRoot, StateFileName);
        var temporaryPath = path + ".tmp";
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
                await JsonSerializer.SerializeAsync(stream, state, SerializerOptions, cancellationToken)
                    .ConfigureAwait(false);
            }

            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    public static string CreateStorageKey(string version, string sha256)
    {
        var safeVersion = string.Concat(version.Select(character =>
            char.IsLetterOrDigit(character) || character is '.' or '-' or '_'
                ? character
                : '-'));
        return $"{safeVersion}-{sha256[..Math.Min(12, sha256.Length)].ToLowerInvariant()}";
    }

    private static bool IsSafeStorageKey(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && value.All(character => char.IsLetterOrDigit(character) || character is '.' or '-' or '_')
        && value is not "." and not "..";
}

internal sealed class ContentActivationState
{
    public string Release { get; set; } = string.Empty;

    public Dictionary<string, ActiveContentComponent>? Components { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
}

internal sealed class ActiveContentComponent
{
    public string Version { get; set; } = string.Empty;

    public string StorageKey { get; set; } = string.Empty;

    public DateTimeOffset InstalledAtUtc { get; set; }

    public string PreviousVersion { get; set; } = string.Empty;

    public string PreviousStorageKey { get; set; } = string.Empty;
}

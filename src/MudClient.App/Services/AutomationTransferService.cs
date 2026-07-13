using System.Text.Json;
using MudClient.App.Models;

namespace MudClient.App.Services;

/// <summary>Reads and writes the versioned automation interchange format.</summary>
public sealed class AutomationTransferService
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    public async Task WriteAsync(Stream stream, AutomationTransferPackage package, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(package);
        await JsonSerializer.SerializeAsync(stream, package, Options, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AutomationTransferPackage> ReadAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        var package = await JsonSerializer.DeserializeAsync<AutomationTransferPackage>(stream, Options, cancellationToken)
            .ConfigureAwait(false);

        if (package is null || package.Version != 1)
        {
            throw new JsonException("Nieobsługiwana lub pusta paczka automatyzacji.");
        }

        ValidatePackage(package);
        return package;
    }

    public static void ValidatePackage(AutomationTransferPackage package)
    {
        if (package.Kind is not (FolderKind.Aliases or FolderKind.Triggers or FolderKind.Timers))
        {
            throw new JsonException("Paczka może zawierać wyłącznie aliasy, triggery albo timery.");
        }

        if (package.Folders.Any(folder => folder.Kind != package.Kind))
        {
            throw new JsonException("Paczka zawiera folder innego typu.");
        }

        var folderIds = package.Folders.Select(folder => folder.Id).ToHashSet(StringComparer.Ordinal);
        if (folderIds.Count != package.Folders.Count || folderIds.Contains(string.Empty))
        {
            throw new JsonException("Id folderów muszą być niepuste i unikalne.");
        }

        foreach (var folder in package.Folders)
        {
            if (folder.ParentId is not null && !folderIds.Contains(folder.ParentId))
            {
                throw new JsonException("Folder odwołuje się do nieistniejącego rodzica.");
            }

            var visited = new HashSet<string>(StringComparer.Ordinal) { folder.Id };
            var parentId = folder.ParentId;
            while (parentId is not null)
            {
                if (!visited.Add(parentId))
                {
                    throw new JsonException("Drzewo folderów zawiera cykl.");
                }

                parentId = package.Folders.First(parent => parent.Id == parentId).ParentId;
            }
        }

        if (package.Kind != FolderKind.Aliases && package.Aliases.Count > 0 ||
            package.Kind != FolderKind.Triggers && package.Triggers.Count > 0 ||
            package.Kind != FolderKind.Timers && package.Timers.Count > 0)
        {
            throw new JsonException("Typ zawartości paczki nie zgadza się z jej rodzajem.");
        }
    }
}

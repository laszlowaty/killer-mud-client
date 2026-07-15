using System.IO.Compression;
using System.Text.Json;

namespace MudClient.App.Services;

/// <summary>
/// Exports the complete application data directory and stages a validated restore.
/// A staged restore is applied before application services are created on the next launch,
/// so currently loaded settings cannot overwrite imported files during shutdown.
/// </summary>
public sealed class SettingsBackupService
{
    private const string ArchiveRoot = "KillerMudClient/";
    private const string ManifestEntryName = "backup-manifest.json";
    private const string PendingContentDirectoryName = "content";
    private const string PendingMarkerFileName = "ready.json";
    private const int CurrentFormatVersion = 1;
    private const int MaximumEntryCount = 100_000;
    private const long MaximumExpandedSize = 2L * 1024 * 1024 * 1024;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
    };

    public SettingsBackupService(string settingsDirectory, string? pendingDirectory = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(settingsDirectory);

        SettingsDirectory = Path.GetFullPath(settingsDirectory);
        PendingDirectory = Path.GetFullPath(pendingDirectory ??
            Path.Combine(
                Directory.GetParent(SettingsDirectory)?.FullName
                    ?? throw new ArgumentException("Katalog ustawień musi mieć katalog nadrzędny.", nameof(settingsDirectory)),
                $".{Path.GetFileName(SettingsDirectory)}-pending-import"));

        if (IsSameOrNestedPath(PendingDirectory, SettingsDirectory))
        {
            throw new ArgumentException("Katalog oczekującego importu nie może znajdować się w katalogu ustawień.", nameof(pendingDirectory));
        }
    }

    public string SettingsDirectory { get; }

    internal string PendingDirectory { get; }

    public async Task ExportAsync(
        Stream destination,
        CancellationToken cancellationToken = default,
        string? sourcePathToExclude = null)
    {
        ArgumentNullException.ThrowIfNull(destination);
        if (!destination.CanWrite)
        {
            throw new ArgumentException("Strumień docelowy nie obsługuje zapisu.", nameof(destination));
        }

        Directory.CreateDirectory(SettingsDirectory);
        var temporaryArchivePath = Path.Combine(Path.GetTempPath(), $"KillerMudClient-backup-{Guid.NewGuid():N}.zip");
        try
        {
            await using (var temporaryArchive = new FileStream(
                             temporaryArchivePath,
                             FileMode.CreateNew,
                             FileAccess.ReadWrite,
                             FileShare.None,
                             bufferSize: 81920,
                             useAsync: true))
            {
                await WriteArchiveAsync(temporaryArchive, sourcePathToExclude, cancellationToken);
            }

            await using var completedArchive = new FileStream(
                temporaryArchivePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 81920,
                useAsync: true);
            await completedArchive.CopyToAsync(destination, cancellationToken);
        }
        finally
        {
            if (File.Exists(temporaryArchivePath))
            {
                File.Delete(temporaryArchivePath);
            }
        }
    }

    private async Task WriteArchiveAsync(
        Stream destination,
        string? sourcePathToExclude,
        CancellationToken cancellationToken)
    {
        using var archive = new ZipArchive(destination, ZipArchiveMode.Create, leaveOpen: true);
        var excludedFullPath = string.IsNullOrWhiteSpace(sourcePathToExclude)
            ? null
            : Path.GetFullPath(sourcePathToExclude);
        var manifestEntry = archive.CreateEntry(ManifestEntryName, CompressionLevel.Optimal);
        await using (var manifestStream = manifestEntry.Open())
        {
            await JsonSerializer.SerializeAsync(
                manifestStream,
                new BackupManifest(CurrentFormatVersion, DateTimeOffset.UtcNow),
                SerializerOptions,
                cancellationToken);
        }

        foreach (var directory in Directory.EnumerateDirectories(SettingsDirectory, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var relativePath = Path.GetRelativePath(SettingsDirectory, directory).Replace('\\', '/');
            archive.CreateEntry(ArchiveRoot + relativePath + "/");
        }

        foreach (var file in Directory.EnumerateFiles(SettingsDirectory, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (excludedFullPath is not null && string.Equals(Path.GetFullPath(file), excludedFullPath, PathComparison))
            {
                // The file picker may create the destination inside the settings directory
                // before export starts. Do not include that in-progress ZIP in itself.
                continue;
            }

            var relativePath = Path.GetRelativePath(SettingsDirectory, file).Replace('\\', '/');
            var entry = archive.CreateEntry(ArchiveRoot + relativePath, CompressionLevel.Optimal);
            await using var input = new FileStream(
                file,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete,
                bufferSize: 81920,
                useAsync: true);
            await using var output = entry.Open();
            await input.CopyToAsync(output, cancellationToken);
        }
    }

    public async Task StageImportAsync(Stream source, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!source.CanRead)
        {
            throw new ArgumentException("Strumień źródłowy nie obsługuje odczytu.", nameof(source));
        }

        ResetPendingDirectory();
        var contentDirectory = Path.Combine(PendingDirectory, PendingContentDirectoryName);
        Directory.CreateDirectory(contentDirectory);

        try
        {
            using var archive = new ZipArchive(source, ZipArchiveMode.Read, leaveOpen: true);
            await ValidateManifestAsync(archive, cancellationToken);

            var entries = archive.Entries.Where(entry => entry.FullName.StartsWith(ArchiveRoot, StringComparison.Ordinal)).ToList();
            if (entries.Count > MaximumEntryCount)
            {
                throw new InvalidDataException("Archiwum zawiera zbyt wiele plików.");
            }

            var expandedSize = entries.Sum(entry => entry.Length);
            if (expandedSize > MaximumExpandedSize)
            {
                throw new InvalidDataException("Rozpakowana kopia ustawień jest zbyt duża.");
            }

            var extractedPaths = new HashSet<string>(PathComparer);
            foreach (var entry in entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var relativePath = entry.FullName[ArchiveRoot.Length..].Replace('/', Path.DirectorySeparatorChar);
                if (string.IsNullOrEmpty(relativePath))
                {
                    continue;
                }

                var destinationPath = GetSafeExtractionPath(contentDirectory, relativePath);
                if (!extractedPaths.Add(destinationPath))
                {
                    throw new InvalidDataException($"Archiwum zawiera powieloną ścieżkę: {relativePath}");
                }

                if (entry.FullName.EndsWith("/", StringComparison.Ordinal))
                {
                    Directory.CreateDirectory(destinationPath);
                    continue;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
                await using var input = entry.Open();
                await using var output = new FileStream(
                    destinationPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize: 81920,
                    useAsync: true);
                await input.CopyToAsync(output, cancellationToken);
            }

            await File.WriteAllTextAsync(
                Path.Combine(PendingDirectory, PendingMarkerFileName),
                JsonSerializer.Serialize(new PendingImport(DateTimeOffset.UtcNow), SerializerOptions),
                cancellationToken);
        }
        catch
        {
            if (Directory.Exists(PendingDirectory))
            {
                Directory.Delete(PendingDirectory, recursive: true);
            }

            throw;
        }
    }

    public bool ApplyPendingImport()
    {
        var markerPath = Path.Combine(PendingDirectory, PendingMarkerFileName);
        var contentDirectory = Path.Combine(PendingDirectory, PendingContentDirectoryName);
        if (!File.Exists(markerPath) || !Directory.Exists(contentDirectory))
        {
            return false;
        }

        var parentDirectory = Directory.GetParent(SettingsDirectory)?.FullName
            ?? throw new InvalidOperationException("Katalog ustawień nie ma katalogu nadrzędnego.");
        Directory.CreateDirectory(parentDirectory);

        var rollbackDirectory = Path.Combine(
            parentDirectory,
            $".{Path.GetFileName(SettingsDirectory)}-before-import-{Guid.NewGuid():N}");
        var previousDirectoryMoved = false;
        var importedDirectoryMoved = false;

        try
        {
            if (Directory.Exists(SettingsDirectory))
            {
                Directory.Move(SettingsDirectory, rollbackDirectory);
                previousDirectoryMoved = true;
            }

            Directory.Move(contentDirectory, SettingsDirectory);
            importedDirectoryMoved = true;

            if (previousDirectoryMoved)
            {
                Directory.Delete(rollbackDirectory, recursive: true);
            }

            Directory.Delete(PendingDirectory, recursive: true);
            return true;
        }
        catch
        {
            if (importedDirectoryMoved && Directory.Exists(SettingsDirectory))
            {
                Directory.Delete(SettingsDirectory, recursive: true);
            }

            if (previousDirectoryMoved && Directory.Exists(rollbackDirectory))
            {
                Directory.Move(rollbackDirectory, SettingsDirectory);
            }

            throw;
        }
    }

    private static async Task ValidateManifestAsync(ZipArchive archive, CancellationToken cancellationToken)
    {
        var manifestEntry = archive.GetEntry(ManifestEntryName)
            ?? throw new InvalidDataException("To nie jest kopia ustawień KillerMudClient.");
        await using var manifestStream = manifestEntry.Open();
        var manifest = await JsonSerializer.DeserializeAsync<BackupManifest>(
            manifestStream,
            SerializerOptions,
            cancellationToken);
        if (manifest?.FormatVersion != CurrentFormatVersion)
        {
            throw new InvalidDataException("Nieobsługiwana wersja kopii ustawień.");
        }

        if (archive.Entries.Any(entry =>
                entry.FullName != ManifestEntryName &&
                !entry.FullName.StartsWith(ArchiveRoot, StringComparison.Ordinal)))
        {
            throw new InvalidDataException("Archiwum zawiera pliki poza katalogiem ustawień.");
        }
    }

    private static string GetSafeExtractionPath(string rootDirectory, string relativePath)
    {
        var root = Path.GetFullPath(rootDirectory) + Path.DirectorySeparatorChar;
        var destination = Path.GetFullPath(Path.Combine(rootDirectory, relativePath));
        if (!destination.StartsWith(root, PathComparison))
        {
            throw new InvalidDataException("Archiwum zawiera nieprawidłową ścieżkę pliku.");
        }

        return destination;
    }

    private void ResetPendingDirectory()
    {
        if (Directory.Exists(PendingDirectory))
        {
            Directory.Delete(PendingDirectory, recursive: true);
        }

        Directory.CreateDirectory(PendingDirectory);
    }

    private static bool IsSameOrNestedPath(string candidate, string parent)
    {
        var normalizedParent = Path.GetFullPath(parent).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        var normalizedCandidate = Path.GetFullPath(candidate).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        return normalizedCandidate.StartsWith(normalizedParent, PathComparison);
    }

    private static StringComparison PathComparison =>
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private static StringComparer PathComparer =>
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    private sealed record BackupManifest(int FormatVersion, DateTimeOffset CreatedAtUtc);

    private sealed record PendingImport(DateTimeOffset StagedAtUtc);
}

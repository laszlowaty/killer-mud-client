using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MudClient.App.Models;

namespace MudClient.App.Services;

public sealed class ProfileStorageChangedEventArgs(
    IReadOnlyList<string> relativePaths,
    bool requiresFullReload) : EventArgs
{
    public IReadOnlyList<string> RelativePaths { get; } = relativePaths;
    public bool RequiresFullReload { get; } = requiresFullReload;
}

/// <summary>
/// Stores every profile in its own directory. Automation folders are real file-system
/// directories and every alias, trigger, timer, script, note and autowalk target is a
/// separate JSON file. Legacy one-file profiles are migrated on first access.
/// </summary>
public sealed class ProfileService : IDisposable
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };
    private static readonly JsonSerializerOptions JavaScriptMetadataSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private const string GlobalName = "_global";
    private const string ProfileOrderFileName = "_profiles-order";
    private const string ProfileFileName = "profile.json";
    private const string FolderFileName = ".folder.json";
    private const string JavaScriptHeaderPrefix = "// KillerMudClient: ";
    private static readonly TimeSpan WatcherDebounce = TimeSpan.FromMilliseconds(250);

    private readonly string _directory;
    private readonly object _watcherLock = new();
    private readonly HashSet<string> _pendingWatcherPaths = new(PathComparer);
    private FileSystemWatcher? _watcher;
    private CancellationTokenSource? _watcherDebounceCancellation;
    private bool _watcherRequiresFullReload;
    private string _knownFingerprint = string.Empty;
    private bool _disposed;

    public ProfileService(string? directory = null)
    {
        _directory = directory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "KillerMudClient",
            "Profiles");
    }

    /// <summary>Raised after files changed outside this service and the change settled.</summary>
    public event EventHandler<ProfileStorageChangedEventArgs>? StorageChanged;

    /// <summary>
    /// Starts recursive file monitoring. Kept explicit so short-lived command-line and test
    /// service instances do not unnecessarily hold directory handles.
    /// </summary>
    public void StartWatching()
    {
        lock (_watcherLock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_watcher is not null)
            {
                return;
            }

            Directory.CreateDirectory(_directory);
            MigrateAllLegacyFiles();
            _knownFingerprint = CalculateFingerprint();
            _watcher = new FileSystemWatcher(_directory)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName
                    | NotifyFilters.LastWrite | NotifyFilters.Size,
                EnableRaisingEvents = true,
            };
            _watcher.Changed += OnWatcherChanged;
            _watcher.Created += OnWatcherChanged;
            _watcher.Deleted += OnWatcherChanged;
            _watcher.Renamed += OnWatcherChanged;
            _watcher.Error += OnWatcherError;
        }
    }

    public IReadOnlyList<string> ListProfileNames()
    {
        MigrateAllLegacyFiles();
        if (!Directory.Exists(_directory))
        {
            return [];
        }

        var names = Directory.EnumerateDirectories(_directory)
            .Where(path => File.Exists(Path.Combine(path, ProfileFileName)))
            .Select(path => EffectiveProfileName(path, LoadProfileMetadata(path)))
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var orderPath = Path.Combine(_directory, ProfileOrderFileName + ".json");
        if (!DurableJsonFile.TryRead<ProfileOrderData>(orderPath, SerializerOptions, out var savedOrder)
            || savedOrder is null)
        {
            return names;
        }

        var remaining = new HashSet<string>(names, StringComparer.OrdinalIgnoreCase);
        var ordered = new List<string>(names.Count);
        foreach (var name in savedOrder.Names ?? [])
        {
            if (remaining.Remove(name))
            {
                ordered.Add(names.First(candidate =>
                    string.Equals(candidate, name, StringComparison.OrdinalIgnoreCase)));
            }
        }

        ordered.AddRange(names.Where(remaining.Contains));
        return ordered;
    }

    public bool Exists(string name) => Directory.Exists(GetProfileDirectory(name))
        || File.Exists(GetLegacyPath(name));

    /// <summary>
    /// Returns the physical directory for one automation category of a profile, creating it
    /// when necessary so it can be opened directly from the application UI.
    /// </summary>
    public string EnsureAutomationDirectory(string profileName, FolderKind kind)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileName);
        if (kind is not (FolderKind.Aliases or FolderKind.Triggers or FolderKind.Timers or FolderKind.Scripts))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Nieobsługiwana kategoria automatyzacji.");
        }

        MigrateLegacyProfile(profileName);
        var directory = Path.Combine(GetProfileDirectory(profileName), KindDirectoryName(kind));
        Directory.CreateDirectory(directory);
        return directory;
    }

    public bool StorageChangeAffectsProfile(ProfileStorageChangedEventArgs changes, string profileName)
    {
        ArgumentNullException.ThrowIfNull(changes);
        ArgumentException.ThrowIfNullOrWhiteSpace(profileName);
        var directoryName = Path.GetFileName(GetProfileDirectory(profileName));
        return changes.RequiresFullReload || changes.RelativePaths.Any(path =>
            HasTopLevelDirectory(path, directoryName));
    }

    public bool StorageChangeAffectsGlobal(ProfileStorageChangedEventArgs changes)
    {
        ArgumentNullException.ThrowIfNull(changes);
        return changes.RequiresFullReload || changes.RelativePaths.Any(path =>
            HasTopLevelDirectory(path, GlobalName));
    }

    public void Delete(string name)
    {
        var directory = GetProfileDirectory(name);
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }

        DeleteDurableFile(GetLegacyPath(name));
        RecordOwnChanges();
    }

    public ProfileData? Load(string name)
    {
        MigrateLegacyProfile(name);
        var profileDirectory = GetProfileDirectory(name);
        var metadata = LoadProfileMetadata(profileDirectory);
        if (metadata is null && Directory.Exists(_directory))
        {
            profileDirectory = Directory.EnumerateDirectories(_directory)
                .FirstOrDefault(candidate => string.Equals(
                    LoadProfileMetadata(candidate)?.Name, name, StringComparison.OrdinalIgnoreCase))
                ?? profileDirectory;
            metadata = LoadProfileMetadata(profileDirectory);
        }

        if (metadata is null)
        {
            return null;
        }

        var profile = metadata.ToProfileData();
        profile.Name = EffectiveProfileName(profileDirectory, metadata) ?? name;
        LoadCollections(profileDirectory, profile.Folders, profile.Notes, profile.Rules,
            profile.Timers, profile.Scripts, profile.Locations, isGlobal: false);
        return profile;
    }

    public void Save(ProfileData profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var profileDirectory = GetProfileDirectory(profile.Name);
        SaveProfileMetadata(profile, profileDirectory);
        SaveCollections(profileDirectory, profile.Folders, profile.Notes, profile.Rules,
            profile.Timers, profile.Scripts, profile.Locations, isGlobal: false);
        DeleteDurableFile(GetLegacyPath(profile.Name));
        RecordOwnChanges();
    }

    /// <summary>
    /// Persists account/runtime state (including script variables, deaths and buffs) without
    /// traversing or rewriting automation files. Used by frequent script-variable saves.
    /// </summary>
    public void SaveState(ProfileData profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        SaveProfileMetadata(profile, GetProfileDirectory(profile.Name));
        DeleteDurableFile(GetLegacyPath(profile.Name));
        RecordOwnChanges();
    }

    private static void SaveProfileMetadata(ProfileData profile, string profileDirectory)
    {
        Directory.CreateDirectory(profileDirectory);
        DurableJsonFile.Write(
            Path.Combine(profileDirectory, ProfileFileName),
            ProfileMetadata.From(profile, Path.GetFileName(profileDirectory)),
            SerializerOptions);
    }

    public bool TryRename(string currentName, ProfileData profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (!Exists(currentName))
        {
            return false;
        }

        if (string.Equals(currentName, profile.Name, StringComparison.Ordinal))
        {
            Save(profile);
            return true;
        }

        var currentDirectory = GetProfileDirectory(currentName);
        var newDirectory = GetProfileDirectory(profile.Name);
        var samePathIgnoringCase = string.Equals(
            currentDirectory, newDirectory, StringComparison.OrdinalIgnoreCase);
        if (!samePathIgnoringCase && Exists(profile.Name))
        {
            return false;
        }

        Save(profile);
        if (!samePathIgnoringCase)
        {
            Delete(currentName);
        }
        else if (!string.Equals(currentDirectory, newDirectory, StringComparison.Ordinal))
        {
            var temporaryDirectory = currentDirectory + ".rename-" + Guid.NewGuid().ToString("N");
            Directory.Move(currentDirectory, temporaryDirectory);
            Directory.Move(temporaryDirectory, newDirectory);
            RecordOwnChanges();
        }

        return true;
    }

    public void SaveProfileOrder(IEnumerable<string> names)
    {
        DurableJsonFile.Write(
            Path.Combine(_directory, ProfileOrderFileName + ".json"),
            new ProfileOrderData { Names = names.ToList() },
            SerializerOptions);
        RecordOwnChanges();
    }

    public GlobalData LoadGlobal()
    {
        MigrateLegacyGlobal();
        var data = new GlobalData();
        LoadCollections(GetGlobalDirectory(), data.Folders, data.Notes, data.Rules,
            data.Timers, data.Scripts, data.Locations, isGlobal: true);
        return data;
    }

    public void SaveGlobal(GlobalData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        var globalDirectory = GetGlobalDirectory();
        Directory.CreateDirectory(globalDirectory);
        SaveCollections(globalDirectory, data.Folders, data.Notes, data.Rules,
            data.Timers, data.Scripts, data.Locations, isGlobal: true);
        DeleteDurableFile(Path.Combine(_directory, GlobalName + ".json"));
        RecordOwnChanges();
    }

    private void MigrateAllLegacyFiles()
    {
        if (!Directory.Exists(_directory))
        {
            return;
        }

        MigrateLegacyGlobal();
        foreach (var path in Directory.EnumerateFiles(_directory, "*.json", SearchOption.TopDirectoryOnly)
                     .Where(path => !string.Equals(
                         Path.GetFileNameWithoutExtension(path), ProfileOrderFileName,
                         StringComparison.OrdinalIgnoreCase)
                         && !string.Equals(
                             Path.GetFileNameWithoutExtension(path), GlobalName,
                             StringComparison.OrdinalIgnoreCase)))
        {
            MigrateLegacyProfile(Path.GetFileNameWithoutExtension(path));
        }
    }

    private void MigrateLegacyProfile(string name)
    {
        var legacyPath = GetLegacyPath(name);
        if (Directory.Exists(GetProfileDirectory(name)) || !File.Exists(legacyPath))
        {
            return;
        }

        if (DurableJsonFile.TryRead<ProfileData>(legacyPath, SerializerOptions, out var profile)
            && profile is not null)
        {
            profile.Name = name;
            Save(profile);
        }
    }

    private void MigrateLegacyGlobal()
    {
        var legacyPath = Path.Combine(_directory, GlobalName + ".json");
        if (Directory.Exists(GetGlobalDirectory()) || !File.Exists(legacyPath))
        {
            return;
        }

        if (DurableJsonFile.TryRead<GlobalData>(legacyPath, SerializerOptions, out var data)
            && data is not null)
        {
            SaveGlobal(data);
        }
    }

    private static ProfileMetadata? LoadProfileMetadata(string profileDirectory)
    {
        var path = Path.Combine(profileDirectory, ProfileFileName);
        return DurableJsonFile.TryRead<ProfileMetadata>(path, SerializerOptions, out var metadata)
            ? metadata
            : null;
    }

    private static string? EffectiveProfileName(string profileDirectory, ProfileMetadata? metadata)
    {
        if (metadata is null)
        {
            return null;
        }

        var actualDirectoryName = Path.GetFileName(profileDirectory);
        return string.Equals(metadata.DirectoryName, actualDirectoryName, StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(metadata.Name)
                ? metadata.Name
                : actualDirectoryName;
    }

    private static void SaveCollections(
        string ownerDirectory,
        IReadOnlyCollection<ProfileFolder> folders,
        IReadOnlyCollection<ProfileNote> notes,
        IReadOnlyCollection<ProfileRule> rules,
        IReadOnlyCollection<ProfileTimer> timers,
        IReadOnlyCollection<ProfileScript> scripts,
        IReadOnlyCollection<ProfileLocation> locations,
        bool isGlobal)
    {
        SaveKind(ownerDirectory, FolderKind.Aliases, folders,
            rules.Where(rule => string.Equals(rule.Type, "alias", StringComparison.OrdinalIgnoreCase)),
            rule => rule.Name, isGlobal);
        SaveKind(ownerDirectory, FolderKind.Triggers, folders,
            rules.Where(rule => string.Equals(rule.Type, "trigger", StringComparison.OrdinalIgnoreCase)),
            rule => rule.Name, isGlobal);
        SaveKind(ownerDirectory, FolderKind.Timers, folders, timers, timer => timer.Name, isGlobal);
        SaveKind(ownerDirectory, FolderKind.Scripts, folders, scripts, script => script.Name, isGlobal);
        SaveKind(ownerDirectory, FolderKind.Notes, folders, notes, note => note.Title, isGlobal);
        SaveKind(ownerDirectory, FolderKind.Autowalk, folders, locations, location => location.Name, isGlobal);
    }

    private static void SaveKind<T>(
        string ownerDirectory,
        FolderKind kind,
        IReadOnlyCollection<ProfileFolder> allFolders,
        IEnumerable<T> items,
        Func<T, string> getName,
        bool isGlobal)
        where T : class
    {
        var root = Path.Combine(ownerDirectory, KindDirectoryName(kind));
        Directory.CreateDirectory(root);
        var folders = allFolders.Where(folder =>
            folder.Kind == kind && (isGlobal || !folder.IsGlobal)).ToList();
        var paths = BuildFolderPaths(root, folders);
        RemoveDeletedFolderDirectories(root, paths);

        foreach (var folder in folders)
        {
            if (!paths.TryGetValue(folder.Id, out var path))
            {
                continue;
            }

            Directory.CreateDirectory(path);
            DurableJsonFile.Write(Path.Combine(path, FolderFileName), new FolderMetadata
            {
                Id = folder.Id,
                Name = folder.Name,
                DirectoryName = Path.GetFileName(path),
            }, SerializerOptions);
        }

        var desiredFiles = new HashSet<string>(PathComparer);
        var namesByDirectory = new Dictionary<string, HashSet<string>>(PathComparer);
        foreach (var item in items)
        {
            var folderId = GetFolderId(item);
            var directory = folderId is not null && paths.TryGetValue(folderId, out var folderPath)
                ? folderPath
                : root;
            Directory.CreateDirectory(directory);
            if (!namesByDirectory.TryGetValue(directory, out var names))
            {
                names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                namesByDirectory[directory] = names;
            }

            var fileStem = UniqueFileStem(getName(item), names);
            var isJavaScript = TrySerializeJavaScript(kind, item, out var javaScript);
            var fileName = fileStem + (isJavaScript ? ".js" : ".json");
            var path = Path.Combine(directory, fileName);
            if (isJavaScript)
            {
                DurableJsonFile.WriteText(path, javaScript);
            }
            else
            {
                DurableJsonFile.Write(path, item, SerializerOptions);
            }
            desiredFiles.Add(Path.GetFullPath(path));
        }

        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
                     .Where(path => Path.GetExtension(path) is ".json" or ".js")
                     .Where(path => !string.Equals(Path.GetFileName(path), FolderFileName, StringComparison.OrdinalIgnoreCase)))
        {
            if (!desiredFiles.Contains(Path.GetFullPath(file)))
            {
                DeleteDurableFile(file);
            }
        }
    }

    private static Dictionary<string, string> BuildFolderPaths(
        string root,
        IReadOnlyCollection<ProfileFolder> folders)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var unresolved = new HashSet<ProfileFolder>(folders);
        while (unresolved.Count > 0)
        {
            var progressed = false;
            foreach (var folder in unresolved.ToList())
            {
                if (!string.IsNullOrWhiteSpace(folder.ParentId)
                    && !result.TryGetValue(folder.ParentId, out _))
                {
                    continue;
                }

                var parentPath = string.IsNullOrWhiteSpace(folder.ParentId)
                    ? root
                    : result[folder.ParentId];
                var segment = SanitizeSegment(folder.Name);
                var candidate = Path.Combine(parentPath, segment);
                if (result.Values.Any(existing => string.Equals(existing, candidate, PathComparison)))
                {
                    candidate = Path.Combine(parentPath, segment + " [" + ShortId(folder.Id) + "]");
                }

                result[folder.Id] = candidate;
                unresolved.Remove(folder);
                progressed = true;
            }

            if (!progressed)
            {
                foreach (var folder in unresolved)
                {
                    result[folder.Id] = Path.Combine(root,
                        SanitizeSegment(folder.Name) + " [" + ShortId(folder.Id) + "]");
                }
                break;
            }
        }

        return result;
    }

    private static void RemoveDeletedFolderDirectories(
        string root,
        IReadOnlyDictionary<string, string> desiredPaths)
    {
        foreach (var metadataPath in Directory.EnumerateFiles(root, FolderFileName, SearchOption.AllDirectories)
                     .OrderByDescending(path => path.Length))
        {
            if (DurableJsonFile.TryRead<FolderMetadata>(metadataPath, SerializerOptions, out var metadata)
                && metadata is not null
                && (!desiredPaths.TryGetValue(metadata.Id, out var desiredPath)
                    || !string.Equals(
                        Path.GetDirectoryName(metadataPath), desiredPath, PathComparison)))
            {
                var directory = Path.GetDirectoryName(metadataPath)!;
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, recursive: true);
                }
            }
        }
    }

    private static void LoadCollections(
        string ownerDirectory,
        List<ProfileFolder> folders,
        List<ProfileNote> notes,
        List<ProfileRule> rules,
        List<ProfileTimer> timers,
        List<ProfileScript> scripts,
        List<ProfileLocation> locations,
        bool isGlobal)
    {
        LoadKind(ownerDirectory, FolderKind.Aliases, folders, rules, isGlobal);
        LoadKind(ownerDirectory, FolderKind.Triggers, folders, rules, isGlobal);
        LoadKind(ownerDirectory, FolderKind.Timers, folders, timers, isGlobal);
        LoadKind(ownerDirectory, FolderKind.Scripts, folders, scripts, isGlobal);
        LoadKind(ownerDirectory, FolderKind.Notes, folders, notes, isGlobal);
        LoadKind(ownerDirectory, FolderKind.Autowalk, folders, locations, isGlobal);
    }

    private static void LoadKind<T>(
        string ownerDirectory,
        FolderKind kind,
        List<ProfileFolder> folders,
        List<T> items,
        bool isGlobal)
        where T : class
    {
        var root = Path.Combine(ownerDirectory, KindDirectoryName(kind));
        if (!Directory.Exists(root))
        {
            return;
        }

        var folderByPath = new Dictionary<string, ProfileFolder>(PathComparer);
        foreach (var directory in Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories)
                     .OrderBy(path => path.Count(character => character == Path.DirectorySeparatorChar)))
        {
            var metadataPath = Path.Combine(directory, FolderFileName);
            DurableJsonFile.TryRead<FolderMetadata>(metadataPath, SerializerOptions, out var metadata);
            var actualName = Path.GetFileName(directory);
            var name = metadata is not null
                && string.Equals(metadata.DirectoryName, actualName, StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(metadata.Name)
                    ? metadata.Name
                    : actualName;
            var parentPath = Path.GetDirectoryName(directory);
            var folder = new ProfileFolder
            {
                Id = string.IsNullOrWhiteSpace(metadata?.Id)
                    ? StableId(Path.GetRelativePath(root, directory))
                    : metadata.Id,
                ParentId = parentPath is not null && folderByPath.TryGetValue(parentPath, out var parent)
                    ? parent.Id
                    : null,
                Name = name,
                Kind = kind,
                IsGlobal = isGlobal,
            };
            folders.Add(folder);
            folderByPath[directory] = folder;
        }

        var loadedJavaScriptStems = new HashSet<string>(PathComparer);
        foreach (var path in Directory.EnumerateFiles(root, "*.js", SearchOption.AllDirectories))
        {
            if (!TryDeserializeJavaScript(kind, path, out T? item) || item is null)
            {
                continue;
            }

            loadedJavaScriptStems.Add(Path.ChangeExtension(Path.GetFullPath(path), null));
            var parentPath = Path.GetDirectoryName(path)!;
            SetFolderAndGlobal(item,
                folderByPath.TryGetValue(parentPath, out var folder) ? folder.Id : null,
                isGlobal);
            items.Add(item);
        }

        foreach (var path in Directory.EnumerateFiles(root, "*.json", SearchOption.AllDirectories)
                     .Where(path => !string.Equals(Path.GetFileName(path), FolderFileName, StringComparison.OrdinalIgnoreCase)))
        {
            // During migration an advanced item may briefly exist in both formats.
            // Prefer a successfully parsed JS file without parsing or activating the stale
            // (and often very large) JSON copy; the next normal save removes that old copy.
            if (loadedJavaScriptStems.Contains(Path.ChangeExtension(Path.GetFullPath(path), null)))
            {
                continue;
            }

            if (!DurableJsonFile.TryRead<T>(path, SerializerOptions, out var item) || item is null)
            {
                continue;
            }

            var parentPath = Path.GetDirectoryName(path)!;
            SetFolderAndGlobal(item,
                folderByPath.TryGetValue(parentPath, out var folder) ? folder.Id : null,
                isGlobal);
            if (item is ProfileRule rule)
            {
                rule.Type = kind == FolderKind.Aliases ? "alias" : "trigger";
            }
            items.Add(item);
        }
    }

    private static bool TrySerializeJavaScript<T>(FolderKind kind, T item, out string contents)
        where T : class
    {
        JavaScriptAutomationMetadata? metadata = null;
        string? code = null;
        switch (item)
        {
            case ProfileRule rule when rule.IsAdvanced:
                metadata = new JavaScriptAutomationMetadata
                {
                    Kind = kind == FolderKind.Aliases ? "alias" : "trigger",
                    Name = rule.Name,
                    Pattern = rule.Pattern,
                    Enabled = rule.IsEnabled,
                };
                code = rule.Action;
                break;
            case ProfileTimer timer when timer.IsAdvanced:
                metadata = new JavaScriptAutomationMetadata
                {
                    Kind = "timer",
                    Id = timer.Id,
                    Name = timer.Name,
                    Minutes = timer.Minutes,
                    Seconds = timer.Seconds,
                    Milliseconds = timer.Milliseconds,
                    Enabled = timer.IsEnabled,
                };
                code = !string.IsNullOrEmpty(timer.CommandsText)
                    ? timer.CommandsText
                    : string.Join(Environment.NewLine, timer.Commands);
                break;
            case ProfileScript script:
                metadata = new JavaScriptAutomationMetadata
                {
                    Kind = "script",
                    Id = script.Id,
                    Name = script.Name,
                    GmcpPattern = script.GmcpPattern,
                    Enabled = script.IsEnabled,
                };
                code = script.Code;
                break;
        }

        if (metadata is null)
        {
            contents = string.Empty;
            return false;
        }

        contents = JavaScriptHeaderPrefix
            + JsonSerializer.Serialize(metadata, JavaScriptMetadataSerializerOptions)
            + Environment.NewLine
            + (code ?? string.Empty);
        return true;
    }

    private static bool TryDeserializeJavaScript<T>(FolderKind kind, string path, out T? item)
        where T : class
    {
        item = null;
        foreach (var candidate in JavaScriptRecoveryCandidates(path))
        {
            if (DurableJsonFile.TryReadText(candidate, out var contents)
                && TryDeserializeJavaScriptContents(kind, path, contents, out item))
            {
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<string> JavaScriptRecoveryCandidates(string path)
    {
        yield return path;
        yield return path + DurableJsonFile.BackupSuffix;
        var directory = Path.GetDirectoryName(path);
        if (directory is null || !Directory.Exists(directory))
        {
            yield break;
        }

        IEnumerable<string> temporaryFiles;
        try
        {
            temporaryFiles = Directory.EnumerateFiles(directory, Path.GetFileName(path) + ".tmp-*")
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .ToArray();
        }
        catch (IOException)
        {
            yield break;
        }

        foreach (var temporaryFile in temporaryFiles)
        {
            yield return temporaryFile;
        }
    }

    private static bool TryDeserializeJavaScriptContents<T>(
        FolderKind kind,
        string sourcePath,
        string contents,
        out T? item)
        where T : class
    {
        item = null;

        var lineEnd = contents.IndexOf('\n');
        var firstLine = (lineEnd < 0 ? contents : contents[..lineEnd]).TrimEnd('\r');
        if (!firstLine.StartsWith(JavaScriptHeaderPrefix, StringComparison.Ordinal))
        {
            if (firstLine.StartsWith("// KillerMudClient", StringComparison.Ordinal))
            {
                return false;
            }

            if (kind == FolderKind.Scripts && typeof(T) == typeof(ProfileScript))
            {
                item = (T)(object)new ProfileScript
                {
                    Name = Path.GetFileNameWithoutExtension(sourcePath),
                    Code = contents,
                };
                return true;
            }

            return false;
        }

        JavaScriptAutomationMetadata? metadata;
        try
        {
            metadata = JsonSerializer.Deserialize<JavaScriptAutomationMetadata>(
                firstLine[JavaScriptHeaderPrefix.Length..],
                JavaScriptMetadataSerializerOptions);
        }
        catch (JsonException)
        {
            return false;
        }

        if (metadata?.Version != 1)
        {
            return false;
        }

        var code = lineEnd < 0 ? string.Empty : contents[(lineEnd + 1)..];
        var name = string.IsNullOrWhiteSpace(metadata.Name)
            ? Path.GetFileNameWithoutExtension(sourcePath)
            : metadata.Name;
        object? value = kind switch
        {
            FolderKind.Aliases when metadata.Kind == "alias" => new ProfileRule
            {
                Name = name,
                Type = "alias",
                Pattern = metadata.Pattern,
                Action = code,
                IsEnabled = metadata.Enabled,
                IsAdvanced = true,
            },
            FolderKind.Triggers when metadata.Kind == "trigger" => new ProfileRule
            {
                Name = name,
                Type = "trigger",
                Pattern = metadata.Pattern,
                Action = code,
                IsEnabled = metadata.Enabled,
                IsAdvanced = true,
            },
            FolderKind.Timers when metadata.Kind == "timer" => new ProfileTimer
            {
                Id = string.IsNullOrWhiteSpace(metadata.Id) ? Guid.NewGuid().ToString("N") : metadata.Id,
                Name = name,
                Minutes = metadata.Minutes,
                Seconds = metadata.Seconds,
                Milliseconds = metadata.Milliseconds,
                CommandsText = code,
                IsEnabled = metadata.Enabled,
                IsAdvanced = true,
            },
            FolderKind.Scripts when metadata.Kind == "script" => new ProfileScript
            {
                Id = string.IsNullOrWhiteSpace(metadata.Id) ? Guid.NewGuid().ToString("N") : metadata.Id,
                Name = name,
                Code = code,
                GmcpPattern = metadata.GmcpPattern,
                IsEnabled = metadata.Enabled,
            },
            _ => null,
        };

        item = value as T;
        return item is not null;
    }

    private static string? GetFolderId<T>(T item) => item switch
    {
        ProfileRule value => value.FolderId,
        ProfileTimer value => value.FolderId,
        ProfileScript value => value.FolderId,
        ProfileNote value => value.FolderId,
        ProfileLocation value => value.FolderId,
        _ => null,
    };

    private static void SetFolderAndGlobal<T>(T item, string? folderId, bool isGlobal)
    {
        switch (item)
        {
            case ProfileRule value: value.FolderId = folderId; value.IsGlobal = isGlobal; break;
            case ProfileTimer value: value.FolderId = folderId; value.IsGlobal = isGlobal; break;
            case ProfileScript value: value.FolderId = folderId; value.IsGlobal = isGlobal; break;
            case ProfileNote value: value.FolderId = folderId; value.IsGlobal = isGlobal; break;
            case ProfileLocation value: value.FolderId = folderId; value.IsGlobal = isGlobal; break;
        }
    }

    private void OnWatcherChanged(object sender, FileSystemEventArgs eventArgs)
    {
        ScheduleWatcherNotification(eventArgs.FullPath);
        if (eventArgs is RenamedEventArgs renamedEventArgs)
        {
            ScheduleWatcherNotification(renamedEventArgs.OldFullPath);
        }
    }

    private void OnWatcherError(object sender, ErrorEventArgs eventArgs)
    {
        // A buffer overflow means individual events may have been lost. A full reload after
        // the debounce window is safer than attempting to infer which profile changed.
        ScheduleWatcherNotification(requiresFullReload: true);
    }

    private void ScheduleWatcherNotification(string? path = null, bool requiresFullReload = false)
    {
        lock (_watcherLock)
        {
            if (_disposed)
            {
                return;
            }

            if (path is not null && TryGetRelativeStoragePath(path, out var relativePath))
            {
                _pendingWatcherPaths.Add(relativePath);
            }

            _watcherRequiresFullReload |= requiresFullReload;
            _watcherDebounceCancellation?.Cancel();
            _watcherDebounceCancellation?.Dispose();
            _watcherDebounceCancellation = new CancellationTokenSource();
            _ = NotifyAfterDebounceAsync(_watcherDebounceCancellation.Token);
        }
    }

    private async Task NotifyAfterDebounceAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(WatcherDebounce, cancellationToken).ConfigureAwait(false);
            var fingerprint = CalculateFingerprint();
            ProfileStorageChangedEventArgs changes;
            lock (_watcherLock)
            {
                changes = new ProfileStorageChangedEventArgs(
                    _pendingWatcherPaths.OrderBy(path => path, PathComparer).ToArray(),
                    _watcherRequiresFullReload);
                _pendingWatcherPaths.Clear();
                _watcherRequiresFullReload = false;
                if (_disposed || string.Equals(fingerprint, _knownFingerprint, StringComparison.Ordinal))
                {
                    return;
                }

                _knownFingerprint = fingerprint;
            }

            StorageChanged?.Invoke(this, changes);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // A newer file-system event restarts the debounce window.
        }
        catch (IOException)
        {
            // A transient editor rename/write sequence will produce another watcher event.
        }
    }

    private void RecordOwnChanges()
    {
        lock (_watcherLock)
        {
            if (_watcher is not null)
            {
                _knownFingerprint = CalculateFingerprint();
            }
        }
    }

    private bool TryGetRelativeStoragePath(string path, out string relativePath)
    {
        relativePath = Path.GetRelativePath(_directory, path).Replace('\\', '/');
        return !Path.IsPathRooted(relativePath)
            && !relativePath.Equals("..", StringComparison.Ordinal)
            && !relativePath.StartsWith("../", StringComparison.Ordinal);
    }

    private static bool HasTopLevelDirectory(string relativePath, string directoryName)
    {
        var separatorIndex = relativePath.IndexOf('/');
        var topLevelDirectory = separatorIndex < 0
            ? relativePath
            : relativePath[..separatorIndex];
        return string.Equals(topLevelDirectory, directoryName, PathComparison);
    }

    private string CalculateFingerprint()
    {
        if (!Directory.Exists(_directory))
        {
            return string.Empty;
        }

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var path in Directory.EnumerateFiles(_directory, "*", SearchOption.AllDirectories)
                     .Where(path => !Path.GetFileName(path).Contains(".tmp-", StringComparison.Ordinal))
                     .OrderBy(path => path, PathComparer))
        {
            var relativePath = Path.GetRelativePath(_directory, path).Replace('\\', '/');
            hash.AppendData(Encoding.UTF8.GetBytes(relativePath));
            try
            {
                var info = new FileInfo(path);
                hash.AppendData(BitConverter.GetBytes(info.Length));
                hash.AppendData(BitConverter.GetBytes(info.LastWriteTimeUtc.Ticks));
            }
            catch (IOException)
            {
                // An editor may be between atomic rename steps; the next event retries.
            }
        }

        return Convert.ToHexString(hash.GetHashAndReset());
    }

    public void Dispose()
    {
        lock (_watcherLock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _watcherDebounceCancellation?.Cancel();
            _watcherDebounceCancellation?.Dispose();
            _watcherDebounceCancellation = null;
            if (_watcher is not null)
            {
                _watcher.EnableRaisingEvents = false;
                _watcher.Changed -= OnWatcherChanged;
                _watcher.Created -= OnWatcherChanged;
                _watcher.Deleted -= OnWatcherChanged;
                _watcher.Renamed -= OnWatcherChanged;
                _watcher.Error -= OnWatcherError;
                _watcher.Dispose();
                _watcher = null;
            }
        }
    }

    private string GetProfileDirectory(string name) =>
        Path.Combine(_directory, SanitizeProfileDirectoryName(name));
    private string GetGlobalDirectory() => Path.Combine(_directory, GlobalName);
    private string GetLegacyPath(string name) => Path.Combine(_directory, SanitizeLegacyName(name) + ".json");

    private static string KindDirectoryName(FolderKind kind) => kind switch
    {
        FolderKind.Aliases => "Aliases",
        FolderKind.Triggers => "Triggers",
        FolderKind.Timers => "Timers",
        FolderKind.Scripts => "Scripts",
        FolderKind.Notes => "Notes",
        FolderKind.Autowalk => "Autowalk",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    private static string UniqueFileStem(string name, HashSet<string> usedNames)
    {
        var baseName = SanitizeSegment(name);
        var candidate = baseName;
        var suffix = 2;
        while (!usedNames.Add(candidate))
        {
            candidate = $"{baseName} ({suffix++})";
        }

        return candidate;
    }

    private static string SanitizeSegment(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = (name ?? string.Empty).Trim().Select(character =>
            invalid.Contains(character) ? '_' : character).ToArray();
        var sanitized = new string(chars).TrimEnd('.', ' ');
        return string.IsNullOrWhiteSpace(sanitized) ? "Bez nazwy" : sanitized;
    }

    private static string SanitizeLegacyName(string name)
    {
        var sanitized = SanitizeSegment(name);
        return string.Equals(sanitized, GlobalName, StringComparison.OrdinalIgnoreCase)
            || string.Equals(sanitized, ProfileOrderFileName, StringComparison.OrdinalIgnoreCase)
                ? sanitized + "_profil"
                : sanitized;
    }

    private static string SanitizeProfileDirectoryName(string name)
    {
        var sanitized = SanitizeSegment(name);
        return string.Equals(sanitized, GlobalName, StringComparison.OrdinalIgnoreCase)
            || string.Equals(sanitized, ProfileOrderFileName, StringComparison.OrdinalIgnoreCase)
                ? sanitized + "_profil"
                : sanitized;
    }

    private static string StableId(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant()[..32];

    private static string ShortId(string value) =>
        string.IsNullOrWhiteSpace(value) ? "folder" : value[..Math.Min(8, value.Length)];

    private static void DeleteDurableFile(string path)
    {
        foreach (var candidate in new[] { path, path + DurableJsonFile.BackupSuffix })
        {
            if (File.Exists(candidate))
            {
                File.Delete(candidate);
            }
        }
    }

    private static StringComparison PathComparison =>
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private static StringComparer PathComparer =>
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    private sealed class ProfileOrderData
    {
        public List<string> Names { get; set; } = [];
    }

    private sealed class FolderMetadata
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string DirectoryName { get; set; } = string.Empty;
    }

    private sealed class JavaScriptAutomationMetadata
    {
        public int Version { get; set; } = 1;
        public string Kind { get; set; } = string.Empty;
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Pattern { get; set; } = string.Empty;
        public bool Enabled { get; set; } = true;
        public int Minutes { get; set; }
        public int Seconds { get; set; }
        public int Milliseconds { get; set; }
        public string GmcpPattern { get; set; } = string.Empty;
    }

    private sealed class ProfileMetadata
    {
        public string Name { get; set; } = string.Empty;
        public string DirectoryName { get; set; } = string.Empty;
        public string Login { get; set; } = string.Empty;
        public string Host { get; set; } = "killer-mud.pl";
        public int Port { get; set; } = 4004;
        public string Encoding { get; set; } = Core.Networking.MudTextEncodings.Auto;
        public string EncryptedPassword { get; set; } = string.Empty;
        public bool NeedsRegistration { get; set; }
        public Dictionary<string, JsonElement> ScriptVariables { get; set; } =
            new(StringComparer.OrdinalIgnoreCase);
        public List<ProfileDeath> Deaths { get; set; } = [];
        public List<string> RequiredBuffs { get; set; } = [];
        public List<ProfileBuffSet> BuffSets { get; set; } = [];
        public string ActiveBuffSetId { get; set; } = string.Empty;

        public static ProfileMetadata From(ProfileData profile, string directoryName) => new()
        {
            Name = profile.Name,
            DirectoryName = directoryName,
            Login = profile.Login,
            Host = profile.Host,
            Port = profile.Port,
            Encoding = profile.Encoding,
            EncryptedPassword = profile.EncryptedPassword,
            NeedsRegistration = profile.NeedsRegistration,
            ScriptVariables = profile.ScriptVariables,
            Deaths = profile.Deaths,
            RequiredBuffs = profile.RequiredBuffs,
            BuffSets = profile.BuffSets,
            ActiveBuffSetId = profile.ActiveBuffSetId,
        };

        public ProfileData ToProfileData() => new()
        {
            Name = Name,
            Login = Login,
            Host = Host,
            Port = Port,
            Encoding = Encoding,
            EncryptedPassword = EncryptedPassword,
            NeedsRegistration = NeedsRegistration,
            ScriptVariables = ScriptVariables ?? new(StringComparer.OrdinalIgnoreCase),
            Deaths = Deaths ?? [],
            RequiredBuffs = RequiredBuffs ?? [],
            BuffSets = BuffSets ?? [],
            ActiveBuffSetId = ActiveBuffSetId,
        };
    }
}

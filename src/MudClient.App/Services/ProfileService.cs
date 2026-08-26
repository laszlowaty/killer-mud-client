using System.IO;
using System.Text.Json;
using MudClient.App.Models;

namespace MudClient.App.Services;

/// <summary>
/// Stores user profiles as JSON files, one file per profile.
/// Default location: %AppData%\KillerMudClient\Profiles.
/// </summary>
public sealed class ProfileService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
    };

    /// <summary>File (without extension) holding globally shared rules/timers/locations.</summary>
    private const string GlobalFileName = "_global";
    private const string ProfileOrderFileName = "_profiles-order";

    private readonly string _directory;

    public ProfileService(string? directory = null)
    {
        _directory = directory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "KillerMudClient",
            "Profiles");
    }

    public IReadOnlyList<string> ListProfileNames()
    {
        if (!Directory.Exists(_directory))
        {
            return [];
        }

        var names = Directory.EnumerateFiles(_directory, "*.json")
            .Select(Path.GetFileNameWithoutExtension)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!)
            .Where(name => !string.Equals(name, GlobalFileName, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(name, ProfileOrderFileName, StringComparison.OrdinalIgnoreCase))
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

    public bool Exists(string name) => File.Exists(GetPath(name));

    /// <summary>Removes the account's file from disk. No-op when it doesn't exist.</summary>
    public void Delete(string name)
    {
        var path = GetPath(name);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        var backupPath = path + DurableJsonFile.BackupSuffix;
        if (File.Exists(backupPath))
        {
            File.Delete(backupPath);
        }
    }

    public ProfileData? Load(string name)
    {
        var path = GetPath(name);
        if (DurableJsonFile.TryRead<ProfileData>(path, SerializerOptions, out var profile)
            && profile is not null)
        {
            profile.Name = name;
            return profile;
        }

        return null;
    }

    public void Save(ProfileData profile)
    {
        DurableJsonFile.Write(GetPath(profile.Name), profile, SerializerOptions);
    }

    public bool TryRename(string currentName, ProfileData profile)
    {
        var currentPath = GetPath(currentName);
        var newPath = GetPath(profile.Name);
        if (!File.Exists(currentPath))
        {
            return false;
        }

        if (string.Equals(currentPath, newPath, StringComparison.Ordinal))
        {
            DurableJsonFile.Write(newPath, profile, SerializerOptions);
            return true;
        }

        var samePathIgnoringCase = string.Equals(
            currentPath,
            newPath,
            StringComparison.OrdinalIgnoreCase);
        if (!samePathIgnoringCase && (File.Exists(newPath) || File.Exists(newPath + DurableJsonFile.BackupSuffix)))
        {
            return false;
        }

        Directory.CreateDirectory(_directory);
        if (samePathIgnoringCase)
        {
            File.Move(currentPath, newPath);
            var currentBackupPath = currentPath + DurableJsonFile.BackupSuffix;
            if (File.Exists(currentBackupPath))
            {
                File.Move(currentBackupPath, newPath + DurableJsonFile.BackupSuffix);
            }

            DurableJsonFile.Write(newPath, profile, SerializerOptions);
            return true;
        }

        DurableJsonFile.Write(newPath, profile, SerializerOptions);
        Delete(currentName);
        return true;
    }

    public void SaveProfileOrder(IEnumerable<string> names)
    {
        DurableJsonFile.Write(
            Path.Combine(_directory, ProfileOrderFileName + ".json"),
            new ProfileOrderData { Names = names.ToList() },
            SerializerOptions);
    }

    public GlobalData LoadGlobal()
    {
        var path = Path.Combine(_directory, GlobalFileName + ".json");
        return DurableJsonFile.TryRead<GlobalData>(path, SerializerOptions, out var data)
            ? data ?? new GlobalData()
            : new GlobalData();
    }

    public void SaveGlobal(GlobalData data)
    {
        DurableJsonFile.Write(
            Path.Combine(_directory, GlobalFileName + ".json"),
            data,
            SerializerOptions);
    }

    private string GetPath(string name) => Path.Combine(_directory, Sanitize(name) + ".json");

    /// <summary>
    /// Turns a profile name into a safe file name (profile names come from user input).
    /// </summary>
    private static string Sanitize(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = name.Trim().Select(c => invalid.Contains(c) ? '_' : c).ToArray();
        var sanitized = new string(chars);

        // A profile must never overwrite the shared global file.
        return string.Equals(sanitized, GlobalFileName, StringComparison.OrdinalIgnoreCase)
            || string.Equals(sanitized, ProfileOrderFileName, StringComparison.OrdinalIgnoreCase)
            ? sanitized + "_profil"
            : sanitized;
    }

    private sealed class ProfileOrderData
    {
        public List<string> Names { get; set; } = [];
    }
}

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

        return Directory.EnumerateFiles(_directory, "*.json")
            .Select(Path.GetFileNameWithoutExtension)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public bool Exists(string name) => File.Exists(GetPath(name));

    public ProfileData? Load(string name)
    {
        var path = GetPath(name);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(path);
            var profile = JsonSerializer.Deserialize<ProfileData>(json, SerializerOptions);
            if (profile is not null)
            {
                profile.Name = name;
            }

            return profile;
        }
        catch (Exception exception) when (exception is IOException or JsonException)
        {
            return null;
        }
    }

    public void Save(ProfileData profile)
    {
        Directory.CreateDirectory(_directory);
        var json = JsonSerializer.Serialize(profile, SerializerOptions);
        File.WriteAllText(GetPath(profile.Name), json);
    }

    private string GetPath(string name) => Path.Combine(_directory, Sanitize(name) + ".json");

    /// <summary>
    /// Turns a profile name into a safe file name (profile names come from user input).
    /// </summary>
    private static string Sanitize(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = name.Trim().Select(c => invalid.Contains(c) ? '_' : c).ToArray();
        return new string(chars);
    }
}

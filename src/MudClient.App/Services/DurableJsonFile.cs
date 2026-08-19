using System.Text;
using System.Text.Json;

namespace MudClient.App.Services;

/// <summary>
/// Reads and writes JSON configuration without exposing the destination file to a
/// partially completed write. The previous complete version is kept next to it as
/// <c>.bak</c> and is used automatically when the primary file cannot be read.
/// </summary>
internal static class DurableJsonFile
{
    internal const string BackupSuffix = ".bak";

    public static bool TryRead<T>(string path, JsonSerializerOptions options, out T? value)
    {
        if (TryReadSingle(path, options, out value))
        {
            return true;
        }

        if (TryReadSingle(path + BackupSuffix, options, out value))
        {
            return true;
        }

        // A sudden shutdown can happen after the temporary file was flushed but
        // before its final rename (especially on the first-ever save).
        return TryReadNewestTemporary(path, options, out value);
    }

    public static void Write<T>(string path, T value, JsonSerializerOptions options)
    {
        var json = JsonSerializer.Serialize(value, options);
        WriteText(path, json);
    }

    private static bool TryReadSingle<T>(string path, JsonSerializerOptions options, out T? value)
    {
        value = default;
        try
        {
            if (!File.Exists(path))
            {
                return false;
            }

            value = JsonSerializer.Deserialize<T>(File.ReadAllText(path), options);
            return value is not null;
        }
        catch (Exception exception) when (exception is IOException or JsonException)
        {
            return false;
        }
    }

    private static bool TryReadNewestTemporary<T>(
        string path,
        JsonSerializerOptions options,
        out T? value)
    {
        value = default;
        try
        {
            var directory = Path.GetDirectoryName(path);
            var fileName = Path.GetFileName(path);
            if (directory is null || !Directory.Exists(directory))
            {
                return false;
            }

            foreach (var candidate in Directory
                         .EnumerateFiles(directory, fileName + ".tmp-*")
                         .OrderByDescending(File.GetLastWriteTimeUtc))
            {
                if (TryReadSingle(candidate, options, out value))
                {
                    return true;
                }
            }
        }
        catch (IOException)
        {
            // Recovery candidates are optional; callers retain their normal
            // missing/corrupt-file behavior when the directory cannot be read.
        }

        return false;
    }

    private static void WriteText(string path, string contents)
    {
        var directory = Path.GetDirectoryName(path)
            ?? throw new ArgumentException("Ścieżka pliku konfiguracji nie ma katalogu.", nameof(path));
        Directory.CreateDirectory(directory);

        var temporaryPath = path + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            var bytes = Encoding.UTF8.GetBytes(contents);
            using (var stream = new FileStream(
                       temporaryPath,
                       FileMode.CreateNew,
                       FileAccess.Write,
                       FileShare.None,
                       bufferSize: 4096,
                       FileOptions.WriteThrough))
            {
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }

            ReplaceDestination(temporaryPath, path);
        }
        finally
        {
            try
            {
                File.Delete(temporaryPath);
            }
            catch (IOException)
            {
                // A failed cleanup only leaves an ignored temporary file. The live
                // configuration and its backup are never removed here.
            }
        }
    }

    private static void ReplaceDestination(string temporaryPath, string path)
    {
        if (!File.Exists(path))
        {
            File.Move(temporaryPath, path);
            return;
        }

        try
        {
            File.Replace(temporaryPath, path, path + BackupSuffix, ignoreMetadataErrors: true);
        }
        catch (PlatformNotSupportedException)
        {
            // File.Replace is unavailable on some mobile file systems. The rename
            // still keeps the destination atomic; copy only supplies recovery data.
            File.Copy(path, path + BackupSuffix, overwrite: true);
            File.Move(temporaryPath, path, overwrite: true);
        }
    }
}

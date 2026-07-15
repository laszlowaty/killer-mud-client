using System.Diagnostics;
using System.Reflection;

namespace MudClient.App.Services;

internal static class ApplicationRestartService
{
    internal const string WaitForProcessArgument = "--wait-for-settings-import";
    private const int PreviousProcessExitTimeoutMilliseconds = 60_000;

    public static void StartReplacementProcess()
    {
        var startInfo = CreateStartInfo(Environment.ProcessId, Environment.GetCommandLineArgs().Skip(1));
        if (Process.Start(startInfo) is null)
        {
            throw new InvalidOperationException("Nie udało się uruchomić nowego procesu aplikacji.");
        }
    }

    internal static string[] WaitForPreviousProcessIfRequested(string[] arguments)
    {
        var result = new List<string>(arguments.Length);
        for (var index = 0; index < arguments.Length; index++)
        {
            if (!string.Equals(arguments[index], WaitForProcessArgument, StringComparison.Ordinal))
            {
                result.Add(arguments[index]);
                continue;
            }

            if (++index >= arguments.Length || !int.TryParse(arguments[index], out var processId) || processId <= 0)
            {
                throw new InvalidOperationException("Nieprawidłowe parametry automatycznego restartu.");
            }

            WaitForProcessExit(processId);
        }

        return result.ToArray();
    }

    internal static ProcessStartInfo CreateStartInfo(int currentProcessId, IEnumerable<string> currentArguments)
    {
        var processPath = Environment.ProcessPath
            ?? throw new InvalidOperationException("Nie można ustalić ścieżki uruchomionej aplikacji.");
        var startInfo = new ProcessStartInfo
        {
            FileName = processPath,
            UseShellExecute = false,
            WorkingDirectory = Environment.CurrentDirectory,
        };

        if (string.Equals(Path.GetFileNameWithoutExtension(processPath), "dotnet", StringComparison.OrdinalIgnoreCase))
        {
            var entryAssemblyPath = Assembly.GetEntryAssembly()?.Location;
            if (string.IsNullOrWhiteSpace(entryAssemblyPath))
            {
                throw new InvalidOperationException("Nie można ustalić pliku aplikacji uruchomionej przez dotnet.");
            }

            startInfo.ArgumentList.Add(entryAssemblyPath);
        }

        var arguments = currentArguments.ToArray();
        for (var index = 0; index < arguments.Length; index++)
        {
            if (string.Equals(arguments[index], WaitForProcessArgument, StringComparison.Ordinal))
            {
                index++;
                continue;
            }

            startInfo.ArgumentList.Add(arguments[index]);
        }

        startInfo.ArgumentList.Add(WaitForProcessArgument);
        startInfo.ArgumentList.Add(currentProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture));
        return startInfo;
    }

    private static void WaitForProcessExit(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            if (!process.WaitForExit(PreviousProcessExitTimeoutMilliseconds))
            {
                throw new TimeoutException("Poprzedni proces aplikacji nie zakończył się w wymaganym czasie.");
            }
        }
        catch (ArgumentException)
        {
            // The old process already exited before the replacement obtained its handle.
        }
    }
}

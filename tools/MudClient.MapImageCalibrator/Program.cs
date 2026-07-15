using Avalonia;

namespace MudClient.MapImageCalibrator;

internal static class Program
{
    public static string? RequestedRoot { get; private set; }

    [STAThread]
    public static void Main(string[] args)
    {
        if (args is ["--repair", var requestedProject])
        {
            var projectPath = Path.GetFullPath(requestedProject);
            var projectsDirectory = Path.GetDirectoryName(projectPath)
                ?? throw new InvalidOperationException("Nieprawidłowa ścieżka projektu.");
            var templatePath = Path.Combine(projectsDirectory, "old-continent.nort");
            new NortantisExportService().RepairProject(projectPath, templatePath);
            Console.WriteLine($"Naprawiono: {projectPath}");
            return;
        }

        RequestedRoot = args.FirstOrDefault();
        AppBuilder.Configure<App>().UsePlatformDetect().LogToTrace().StartWithClassicDesktopLifetime(args);
    }
}

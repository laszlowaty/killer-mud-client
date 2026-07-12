using Avalonia;

namespace MudClient.MapImageCalibrator;

internal static class Program
{
    public static string? RequestedRoot { get; private set; }

    [STAThread]
    public static void Main(string[] args)
    {
        RequestedRoot = args.FirstOrDefault();
        AppBuilder.Configure<App>().UsePlatformDetect().LogToTrace().StartWithClassicDesktopLifetime(args);
    }
}

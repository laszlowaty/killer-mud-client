using Avalonia;
using MudClient.App.Services;

namespace MudClient.App;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        var applicationArguments = ApplicationRestartService.WaitForPreviousProcessIfRequested(args);
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(applicationArguments);
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}

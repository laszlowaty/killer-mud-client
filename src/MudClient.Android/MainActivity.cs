using Android.App;
using Android.Content.PM;
using Avalonia.Android;

namespace MudClient.Android;

[Activity(
    Label = "KillerMudClient",
    Theme = "@style/MyTheme.NoActionBar",
    Icon = "@drawable/icon",
    MainLauncher = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public sealed class MainActivity : AvaloniaMainActivity
{
    public MainActivity()
    {
        BackRequested += OnBackRequested;
    }

    private static void OnBackRequested(
        object? sender,
        AndroidBackRequestedEventArgs eventArgs)
    {
        if (Avalonia.Application.Current is MobileApp app
            && app.TryNavigateBackToTerminal())
        {
            eventArgs.Handled = true;
        }
    }
}

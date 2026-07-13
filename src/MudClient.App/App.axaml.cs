using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Dock.Settings;
using MudClient.App.ViewModels;
using MudClient.App.Views;

namespace MudClient.App;

public partial class App : Application
{
    public override void Initialize()
    {
        // Prefer the dock under the pointer while dragging. Edge auto-hide is selected
        // explicitly from the Panels menu; dragging is only for snapping next to another
        // panel, not for creating a tab on the outer edge of the main window.
        DockSettings.GlobalDockingPreset = DockGlobalDockingPreset.LocalFirst;

        AvaloniaXamlLoader.Load(this);

        // Kept in application resources so dock chrome, popups and widget contents all
        // observe the same live values, even though they do not share a visual ancestor.
        Resources["WidgetFontFamilyResource"] = new FontFamily("Inter");
        Resources["WidgetFontSizeResource"] = 13d;
        Resources["WidgetFontWeightResource"] = FontWeight.Normal;
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}

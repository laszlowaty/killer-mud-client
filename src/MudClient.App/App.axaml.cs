using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Dock.Settings;
using MudClient.App.ViewModels;
using MudClient.App.Views;

namespace MudClient.App;

public partial class App : Application
{
    public override void Initialize()
    {
        // Make window-edge drops resolve to the OUTERMOST dock (our "MainLayout"), not the
        // dock under the cursor. MudDockFactory.SplitToDock relies on this to tell an edge
        // drop (→ collapsed side tab) apart from an ordinary inner split.
        DockSettings.GlobalDockingPreset = DockGlobalDockingPreset.GlobalFirst;

        AvaloniaXamlLoader.Load(this);
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

using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using MudClient.Android.Services;
using MudClient.Android.Views;

namespace MudClient.Android;

public sealed partial class MobileApp : Avalonia.Application
{
    private MobileSessionHost? _sessionHost;
    private WeakReference<MobileShellView>? _currentShellView;

    public override void Initialize()
    {
        Dispatcher.UIThread.UnhandledException += OnUnhandledException;
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IActivityApplicationLifetime activityLifetime)
        {
            _sessionHost ??= new MobileSessionHost(global::Android.App.Application.Context);
            activityLifetime.MainViewFactory = () =>
            {
                var shellView = new MobileShellView(_sessionHost);
                _currentShellView = new WeakReference<MobileShellView>(shellView);
                return shellView;
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    public bool TryNavigateBackToTerminal() =>
        _currentShellView?.TryGetTarget(out var shellView) == true
        && shellView.TryNavigateBackToTerminal();

    private static void OnUnhandledException(
        object? sender,
        DispatcherUnhandledExceptionEventArgs eventArgs)
    {
        global::Android.Util.Log.Error(
            "KillerMudClient",
            eventArgs.Exception.ToString());
    }
}

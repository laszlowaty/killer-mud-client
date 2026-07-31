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
    private bool _isImeVisible;
    private int _imeBottomInsetPixels;

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
                shellView.SetImeState(_isImeVisible, _imeBottomInsetPixels);
                return shellView;
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    public bool TryNavigateBackToTerminal() =>
        _currentShellView?.TryGetTarget(out var shellView) == true
        && shellView.TryNavigateBackToTerminal();

    public void SetImeState(bool isVisible, int bottomInsetPixels)
    {
        _isImeVisible = isVisible;
        _imeBottomInsetPixels = Math.Max(0, bottomInsetPixels);

        Dispatcher.UIThread.Post(() =>
        {
            if (_currentShellView?.TryGetTarget(out var shellView) == true)
            {
                shellView.SetImeState(_isImeVisible, _imeBottomInsetPixels);
            }
        });
    }

    private static void OnUnhandledException(
        object? sender,
        DispatcherUnhandledExceptionEventArgs eventArgs)
    {
        global::Android.Util.Log.Error(
            "KillerMudClient",
            eventArgs.Exception.ToString());
    }
}

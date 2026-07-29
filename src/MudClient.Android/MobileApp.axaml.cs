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
            activityLifetime.MainViewFactory = () => new MobileShellView(_sessionHost);
        }

        base.OnFrameworkInitializationCompleted();
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

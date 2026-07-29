using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Android.Util;
using MudClient.Android.Services;

namespace MudClient.Android.Views;

public sealed partial class MobileShellView : UserControl
{
    private readonly MobileSessionHost _sessionHost;
    private readonly CancellationTokenSource _initializationCancellation = new();
    private bool _initializationStarted;

    public MobileShellView()
        : this(new MobileSessionHost(global::Android.App.Application.Context))
    {
    }

    public MobileShellView(MobileSessionHost sessionHost)
    {
        _sessionHost = sessionHost;
        InitializeComponent();
        AttachedToVisualTree += OnAttachedToVisualTree;
        DetachedFromVisualTree += OnDetachedFromVisualTree;
    }

    private async void OnAttachedToVisualTree(
        object? sender,
        Avalonia.VisualTreeAttachmentEventArgs eventArgs)
    {
        if (_initializationStarted)
        {
            return;
        }

        _initializationStarted = true;
        try
        {
            DataContext = await _sessionHost
                .GetViewModelAsync(_initializationCancellation.Token);
            var loadingOverlay = this.FindControl<Grid>("LoadingOverlay");
            if (loadingOverlay is not null)
            {
                loadingOverlay.IsVisible = false;
            }
        }
        catch (OperationCanceledException) when (_initializationCancellation.IsCancellationRequested)
        {
            // Activity recreation detached this view while the shared host kept initializing.
        }
        catch (Exception exception)
        {
            Log.Error("KillerMudClient", exception.ToString());
            var loadingText = this.FindControl<TextBlock>("LoadingText");
            if (loadingText is not null)
            {
                loadingText.Text = $"Nie udało się uruchomić aplikacji:\n{exception.Message}";
            }
        }
    }

    private void OnDetachedFromVisualTree(
        object? sender,
        Avalonia.VisualTreeAttachmentEventArgs eventArgs)
    {
        _initializationCancellation.Cancel();
    }

    private void CloseAutomation_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        var automationToggle =
            this.FindControl<Avalonia.Controls.Primitives.ToggleButton>("AutomationToggle");
        if (automationToggle is not null)
        {
            automationToggle.IsChecked = false;
        }
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}

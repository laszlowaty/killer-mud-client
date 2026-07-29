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

    private void OpenSettings_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        OpenToolOverlay("Ustawienia", "MobileSettingsPanel");
    }

    private void OpenAutomation_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        OpenToolOverlay("Automaty", "MobileAutomationPanel");
    }

    private void OpenAutowalk_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        OpenToolOverlay("Autowalk", "MobileAutowalkPanel");
    }

    private void CloseToolOverlay_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        var overlay = this.FindControl<Border>("ToolOverlay");
        if (overlay is not null)
        {
            overlay.IsVisible = false;
        }
    }

    private void OpenToolOverlay(string title, string activePanelName)
    {
        foreach (var panelName in new[]
                 {
                     "MobileSettingsPanel",
                     "MobileAutomationPanel",
                     "MobileAutowalkPanel",
                 })
        {
            var panel = this.FindControl<Control>(panelName);
            if (panel is not null)
            {
                panel.IsVisible = panelName == activePanelName;
            }
        }

        var titleBlock = this.FindControl<TextBlock>("ToolOverlayTitle");
        if (titleBlock is not null)
        {
            titleBlock.Text = title;
        }

        var overlay = this.FindControl<Border>("ToolOverlay");
        if (overlay is not null)
        {
            overlay.IsVisible = true;
        }

        this.FindControl<Avalonia.Controls.Button>("MobileMenuButton")?.Flyout?.Hide();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}

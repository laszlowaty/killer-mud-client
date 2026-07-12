using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using MudClient.App;
using MudClient.App.Models;
using MudClient.App.ViewModels;
using MudClient.App.Views;
using MudClient.App.Views.Panels;
using Xunit;

[assembly: AvaloniaTestApplication(typeof(MudClient.App.Tests.TestAppBuilder))]

namespace MudClient.App.Tests;

public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder
        .Configure<App>()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions());
}

[Collection(AvaloniaUiCollection.Name)]
public sealed class EditRuleClickTests
{
    [AvaloniaFact]
    public void ClickingEditRule_DoesNotThrow()
    {
        var viewModel = new MainWindowViewModel();
        var window = new MainWindow { DataContext = viewModel };
        window.Show();

        viewModel.AutomationRules.Add(new AutomationRuleEntry(
            "test", "trigger", "^abc$", "def", isEnabled: true));

        // The Automation panel lives in a Dock layout whose ToolDock creates
        // views lazily (only for the active tool).  To make the edit button
        // findable without depending on Dock-internal types at compile time,
        // temporarily add an AutomationPanelView directly to the window tree.
        var automationPanel = new AutomationPanelView { DataContext = viewModel };
        var rootGrid = Assert.IsAssignableFrom<Grid>(window.Content);
        rootGrid.Children.Add(automationPanel);
        Grid.SetRow(automationPanel, 2);

        window.UpdateLayout();
        Avalonia.Headless.AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        window.UpdateLayout();

        var editButton = window
            .GetLogicalDescendants()
            .OfType<Button>()
            .FirstOrDefault(b => b.DataContext is AutomationRuleEntry &&
                                 Equals(b.Content, "✎"));

        Assert.NotNull(editButton);

        editButton!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        Assert.True(viewModel.IsEditingRule);
        Assert.True(viewModel.IsRuleFormExpanded);
        Assert.Null(viewModel.StartupErrorMessage);
    }
}

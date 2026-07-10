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
using Xunit;

[assembly: AvaloniaTestApplication(typeof(MudClient.App.Tests.TestAppBuilder))]

namespace MudClient.App.Tests;

public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder
        .Configure<App>()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions());
}

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

        // Show the automation tab and realize the templated list items.
        viewModel.SelectedRightTab = 1;
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

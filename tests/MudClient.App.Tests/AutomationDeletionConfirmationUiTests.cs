using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using MudClient.App.Models;
using MudClient.App.Services;
using MudClient.App.ViewModels;
using MudClient.App.Views;
using MudClient.App.Views.Panels;
using Xunit;

namespace MudClient.App.Tests;

[Collection(AvaloniaUiCollection.Name)]
public sealed class AutomationDeletionConfirmationUiTests
{
    [AvaloniaFact]
    public async Task DeleteAutowalkTarget_RequiresConfirmation()
    {
        var directory = Path.Combine(Path.GetTempPath(), "KillerMudClient_AutowalkDeleteConfirmation_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var viewModel = new MainWindowViewModel(
            new ProfileService(directory),
            new AppSettingsService(directory),
            new DockLayoutService(directory));
        var location = new AutowalkLocation("Gospoda", "123", "Pod Złotym Smokiem");
        viewModel.Locations.Add(location);
        var confirmations = new Queue<bool>([false, true]);
        var prompts = new List<(string Type, string Name)>();
        var panel = new AutowalkPanelView
        {
            DataContext = viewModel,
            ConfirmDeletionAsync = (_, itemType, itemName) =>
            {
                prompts.Add((itemType, itemName));
                return Task.FromResult(confirmations.Dequeue());
            },
        };
        var window = new Window { Width = 520, Height = 720, Content = panel };

        window.Show();
        window.UpdateLayout();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        window.UpdateLayout();
        var deleteButton = window.GetLogicalDescendants().OfType<Button>().First(button =>
            ReferenceEquals(button.DataContext, location) && Equals(button.Content, "✕"));

        deleteButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.Contains(location, viewModel.Locations);
        deleteButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.DoesNotContain(location, viewModel.Locations);
        Assert.Equal([("cel autowalk", "Gospoda"), ("cel autowalk", "Gospoda")], prompts);

        window.Close();
        await viewModel.DisposeAsync();
        Directory.Delete(directory, recursive: true);
    }

    [AvaloniaFact]
    public void DeleteProfile_RequiresConfirmation()
    {
        var directory = Path.Combine(Path.GetTempPath(), "KillerMudClient_ProfileDeleteConfirmation_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var profileService = new ProfileService(directory);
        profileService.Save(new ProfileData { Name = "Gandalf" });
        var viewModel = new MainWindowViewModel(
            profileService,
            new AppSettingsService(directory),
            new DockLayoutService(directory));
        var confirmations = new Queue<bool>([false, true]);
        var prompts = new List<(string Type, string Name)>();
        var window = new MainWindow
        {
            DataContext = viewModel,
            ConfirmDeletionAsync = (_, itemType, itemName) =>
            {
                prompts.Add((itemType, itemName));
                return Task.FromResult(confirmations.Dequeue());
            },
        };

        window.Show();
        window.UpdateLayout();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        window.UpdateLayout();
        var deleteButton = window.GetLogicalDescendants().OfType<Button>().First(button =>
            Equals(button.DataContext, "Gandalf") && Equals(button.Content, "🗑"));

        deleteButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.Contains("Gandalf", viewModel.AvailableProfiles);
        Assert.True(profileService.Exists("Gandalf"));
        deleteButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.DoesNotContain("Gandalf", viewModel.AvailableProfiles);
        Assert.False(profileService.Exists("Gandalf"));
        Assert.Equal([("profil", "Gandalf"), ("profil", "Gandalf")], prompts);

        window.Close();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Directory.Delete(directory, recursive: true);
    }

    [AvaloniaFact]
    public async Task DeleteButtons_RequireConfirmation_ForTimerAliasAndTrigger()
    {
        var directory = Path.Combine(Path.GetTempPath(), "KillerMudClient_DeleteConfirmation_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var viewModel = new MainWindowViewModel(
            new ProfileService(directory),
            new AppSettingsService(directory),
            new DockLayoutService(directory));
        var timer = new TimerEntry { Name = "Leczenie", Seconds = 10 };
        var alias = new AutomationRuleEntry("Skrót", "alias", "^x$", "look", isEnabled: true);
        var trigger = new AutomationRuleEntry("Obrona", "trigger", "atak", "blokuj", isEnabled: true);
        viewModel.Timers.Add(timer);
        viewModel.AutomationRules.Add(alias);
        viewModel.AutomationRules.Add(trigger);

        var confirmations = new Queue<bool>([false, true, false, true, false, true]);
        var prompts = new List<(string Type, string Name)>();
        var panel = new AutomationPanelView
        {
            DataContext = viewModel,
            ConfirmDeletionAsync = (_, itemType, itemName) =>
            {
                prompts.Add((itemType, itemName));
                return Task.FromResult(confirmations.Dequeue());
            },
        };
        var window = new Window { Width = 520, Height = 720, Content = panel };

        window.Show();
        await VerifyConfirmationAsync(window, viewModel, tabIndex: 0, timer, () => viewModel.Timers.Contains(timer));
        await VerifyConfirmationAsync(window, viewModel, tabIndex: 1, alias, () => viewModel.AutomationRules.Contains(alias));
        await VerifyConfirmationAsync(window, viewModel, tabIndex: 2, trigger, () => viewModel.AutomationRules.Contains(trigger));

        Assert.Equal(
            [("timer", "Leczenie"), ("timer", "Leczenie"),
             ("alias", "Skrót"), ("alias", "Skrót"),
             ("trigger", "Obrona"), ("trigger", "Obrona")],
            prompts);

        window.Close();
        await viewModel.DisposeAsync();
        Directory.Delete(directory, recursive: true);
    }

    private static Task VerifyConfirmationAsync(
        Window window,
        MainWindowViewModel viewModel,
        int tabIndex,
        object item,
        Func<bool> itemExists)
    {
        viewModel.SelectedAutomationTabIndex = tabIndex;
        window.UpdateLayout();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        window.UpdateLayout();

        var deleteButton = window.GetLogicalDescendants().OfType<Button>().First(button =>
            ReferenceEquals(button.DataContext, item) && Equals(button.Content, "✕"));

        deleteButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.True(itemExists());

        deleteButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.False(itemExists());
        return Task.CompletedTask;
    }
}

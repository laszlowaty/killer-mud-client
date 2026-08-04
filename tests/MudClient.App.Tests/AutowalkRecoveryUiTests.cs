using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using MudClient.App.Services;
using MudClient.App.ViewModels;
using MudClient.App.Views.Panels;

namespace MudClient.App.Tests;

[Collection(AvaloniaUiCollection.Name)]
public sealed class AutowalkRecoveryUiTests
{
    [AvaloniaFact]
    public async Task RecoveryOptions_AreShownAndPersisted()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "KillerMudClient-AutowalkRecoveryUiTests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var settingsService = new AppSettingsService(directory);
        var viewModel = new MainWindowViewModel(
            settingsService: settingsService,
            dockLayoutService: new DockLayoutService(directory));
        var panel = new AutowalkPanelView { DataContext = viewModel };
        var window = new Window { Width = 520, Height = 720, Content = panel };

        try
        {
            window.Show();
            window.UpdateLayout();
            var checkBoxes = window.GetLogicalDescendants().OfType<CheckBox>().ToList();
            var refreshes = Assert.Single(checkBoxes, checkBox =>
                Equals(checkBox.Content, "Używaj refreshy"));
            var recuperate = Assert.Single(checkBoxes, checkBox =>
                Equals(checkBox.Content, "Używaj recuperate"));

            refreshes.IsChecked = true;
            recuperate.IsChecked = true;

            Assert.True(viewModel.AutowalkUseRefreshes);
            Assert.True(viewModel.AutowalkUseRecuperate);
            var stored = settingsService.Load();
            Assert.True(stored.AutowalkUseRefreshes);
            Assert.True(stored.AutowalkUseRecuperate);
        }
        finally
        {
            window.Close();
            await viewModel.DisposeAsync();
            Directory.Delete(directory, recursive: true);
        }
    }
}

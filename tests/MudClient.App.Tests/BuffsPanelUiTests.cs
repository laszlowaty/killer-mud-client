using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MudClient.App.Docking;
using MudClient.App.Models;
using MudClient.App.Services;
using MudClient.App.ViewModels;
using MudClient.App.Views;
using MudClient.App.Views.Panels;

namespace MudClient.App.Tests;

[Collection(AvaloniaUiCollection.Name)]
public sealed class BuffsPanelUiTests
{
    [AvaloniaFact]
    public void BuffRows_RenderNamesInsideClickableButtons()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "KillerMudClient-BuffsPanelUiTests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var viewModel = new MainWindowViewModel(
            new ProfileService(directory),
            new AppSettingsService(directory),
            new DockLayoutService(directory),
            layoutPresetService: new LayoutPresetService(directory));
        viewModel.RequiredBuffs.Add(new BuffWatchEntry("armor") { IsActive = true });
        viewModel.RequiredBuffs.Add(new BuffWatchEntry("sanctuary"));
        var window = new MainWindow
        {
            Width = 1400,
            Height = 900,
            DataContext = viewModel,
        };
        window.Show();
        var factory = Assert.IsType<MudDockFactory>(viewModel.Layout.Factory);
        factory.SetActiveDockable(
            factory.AllTools.Single(tool => tool.Id == MudDockFactory.BuffsToolId));
        for (var i = 0; i < 15; i++)
        {
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
        }

        try
        {
            var panel = window.GetVisualDescendants().OfType<BuffsPanelView>()
                .First(control => control.IsEffectivelyVisible);
            var recastButtons = panel.GetVisualDescendants().OfType<Button>()
                .Where(button => button.IsEffectivelyVisible
                    && ToolTip.GetTip(button)?.ToString() == "Rzuć ten spell")
                .ToList();
            var buffList = panel.GetVisualDescendants().OfType<ItemsControl>()
                .Single(control => ReferenceEquals(control.ItemsSource, viewModel.RequiredBuffs));

            Assert.Equal(2, recastButtons.Count);

            foreach (var recastButton in recastButtons)
            {
                var buff = Assert.IsType<BuffWatchEntry>(recastButton.DataContext);
                var nameLabel = Assert.Single(
                    recastButton.GetVisualDescendants().OfType<TextBlock>(),
                    textBlock => textBlock.IsEffectivelyVisible && textBlock.Text == buff.Name);

                Assert.True(
                    recastButton.Bounds.Width > 200,
                    $"Buff button remained collapsed at {recastButton.Bounds.Width}px.");
                Assert.True(
                    recastButton.Bounds.Width >= buffList.Bounds.Width - 2,
                    $"Buff button width {recastButton.Bounds.Width}px did not fill "
                    + $"the {buffList.Bounds.Width}px list.");
                Assert.True(nameLabel.Bounds.Width > 20);
                Assert.Contains(
                    recastButton.GetVisualDescendants().OfType<Button>(),
                    button => button.Content?.ToString() == "✕");
            }

            var clickableBuff = recastButtons[0];
            Assert.True(clickableBuff.IsHitTestVisible);
            Assert.True(clickableBuff.IsEnabled);
            Assert.NotNull(clickableBuff.Command);
            clickableBuff.Command!.Execute(clickableBuff.CommandParameter);
            Dispatcher.UIThread.RunJobs();

            Assert.Contains(
                viewModel.Toasts,
                toast => toast.Text == "Nie połączono — nie można rzucić buffa.");
        }
        finally
        {
            window.Close();
            Dispatcher.UIThread.RunJobs();
            Directory.Delete(directory, recursive: true);
        }
    }
}

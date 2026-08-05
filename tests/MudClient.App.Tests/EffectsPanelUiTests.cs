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
public sealed class EffectsPanelUiTests
{
    [AvaloniaFact]
    public async Task EffectRows_RenderNameWithoutDescription()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "KillerMudClient-EffectsPanelUiTests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var viewModel = new MainWindowViewModel(
            new ProfileService(directory),
            new AppSettingsService(directory),
            new DockLayoutService(directory),
            layoutPresetService: new LayoutPresetService(directory));
        viewModel.Effects.Add(new StatusEffect(
            "Błogosławieństwo",
            "[+]",
            "10m",
            false,
            "Opis, którego widget nie powinien wyświetlać.",
            false,
            false,
            "10m"));
        var window = new MainWindow
        {
            Width = 1400,
            Height = 900,
            DataContext = viewModel,
        };
        window.Show();
        var factory = Assert.IsType<MudDockFactory>(viewModel.Layout.Factory);
        factory.SetActiveDockable(
            factory.AllTools.Single(tool => tool.Id == "Effects"));
        for (var i = 0; i < 15; i++)
        {
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
        }

        try
        {
            var panel = window.GetVisualDescendants().OfType<EffectsPanelView>()
                .First(control => control.IsEffectivelyVisible);
            var visibleTexts = panel.GetVisualDescendants().OfType<TextBlock>()
                .Where(textBlock => textBlock.IsEffectivelyVisible)
                .Select(textBlock => textBlock.Text)
                .ToList();

            Assert.Contains("Błogosławieństwo", visibleTexts);
            Assert.Contains("10m", visibleTexts);
            Assert.DoesNotContain(
                "Opis, którego widget nie powinien wyświetlać.",
                visibleTexts);
        }
        finally
        {
            await window.CloseAndDisposeAsync();
            Directory.Delete(directory, recursive: true);
        }
    }
}

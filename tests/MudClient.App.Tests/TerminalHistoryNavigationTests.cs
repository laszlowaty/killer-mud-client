using System.Reflection;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using MudClient.App.Services;
using MudClient.App.ViewModels;
using MudClient.App.Views.Panels;

namespace MudClient.App.Tests;

[Collection(AvaloniaUiCollection.Name)]
public sealed class TerminalHistoryNavigationTests
{
    [Fact]
    public void DeduplicateConsecutive_CollapsesRunsOfRepeats_KeepingNewestOccurrence()
    {
        // CommandHistory is newest-first, so a run of repeats (e.g. "stun" sent four times in a
        // row) collapses to its most recent instance, not its oldest.
        var history = new[] { "stun", "stun", "stun", "stun", "scan" };

        var deduped = TerminalPanelView.DeduplicateConsecutive(history);

        Assert.Equal(["stun", "scan"], deduped);
    }

    [Fact]
    public void DeduplicateConsecutive_NonConsecutiveRepeats_AreNotCollapsed()
    {
        var history = new[] { "stun", "scan", "stun" };

        var deduped = TerminalPanelView.DeduplicateConsecutive(history);

        Assert.Equal(["stun", "scan", "stun"], deduped);
    }

    [Fact]
    public void DeduplicateConsecutive_EmptyHistory_ReturnsEmpty()
    {
        Assert.Empty(TerminalPanelView.DeduplicateConsecutive([]));
    }

    [AvaloniaFact]
    public async Task NavigateHistory_SkipsConsecutiveRepeats_BothUpAndDown()
    {
        var directory = Path.Combine(
            Path.GetTempPath(), "KillerMudClient_TerminalHistoryUiTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var viewModel = new MainWindowViewModel(settingsService: new AppSettingsService(directory));
        foreach (var command in new[] { "scan", "stun", "stun", "stun", "stun" })
        {
            viewModel.CommandHistory.Insert(0, command);
        }

        var terminal = new TerminalPanelView { DataContext = viewModel };
        var window = new Window { Width = 800, Height = 500, Content = terminal };
        window.Show();

        try
        {
            var commandBox = terminal.FindControl<TextBox>("CommandBox")!;
            var navigateHistory = typeof(TerminalPanelView).GetMethod(
                "NavigateHistory", BindingFlags.NonPublic | BindingFlags.Instance)!;

            // Recalling up should land on "stun" once (collapsing the four repeats), then "scan".
            navigateHistory.Invoke(terminal, [1]);
            Assert.Equal("stun", commandBox.Text);
            navigateHistory.Invoke(terminal, [1]);
            Assert.Equal("scan", commandBox.Text);

            // Going back down retraces the same two stops, then clears to empty.
            navigateHistory.Invoke(terminal, [-1]);
            Assert.Equal("stun", commandBox.Text);
            navigateHistory.Invoke(terminal, [-1]);
            Assert.Equal(string.Empty, commandBox.Text);
        }
        finally
        {
            window.Close();
            await viewModel.DisposeAsync();
            Directory.Delete(directory, recursive: true);
        }
    }
}

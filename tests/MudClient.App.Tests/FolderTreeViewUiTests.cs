using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MudClient.App.Controls;
using MudClient.App.Models;
using MudClient.App.Services;
using MudClient.App.ViewModels;
using MudClient.App.Views.Panels;
using Xunit;

namespace MudClient.App.Tests;

[Collection(AvaloniaUiCollection.Name)]
public sealed class FolderTreeViewUiTests
{
    [Theory]
    [InlineData(0, 300, 100, 1000, 76)]
    [InlineData(24, 300, 100, 1000, 88)]
    [InlineData(48, 300, 100, 1000, 100)]
    [InlineData(252, 300, 100, 1000, 100)]
    [InlineData(276, 300, 100, 1000, 112)]
    [InlineData(300, 300, 100, 1000, 124)]
    [InlineData(0, 300, 10, 1000, 0)]
    [InlineData(300, 300, 690, 1000, 700)]
    public void AutoScrollOffset_RespondsToEdgesAndStaysWithinScrollRange(
        double pointerY,
        double viewportHeight,
        double currentOffset,
        double extentHeight,
        double expectedOffset)
    {
        Assert.Equal(expectedOffset, FolderTreeView.CalculateAutoScrollOffset(
            pointerY,
            viewportHeight,
            currentOffset,
            extentHeight));
    }

    [AvaloniaFact]
    public async Task AutomationPanel_UsesFocusedTabsWithLocalPrimaryActionsAndTeamAutomation()
    {
        var directory = Path.Combine(Path.GetTempPath(), "KillerMudClient_AutomationUi_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var viewModel = new MainWindowViewModel(new ProfileService(directory), new AppSettingsService(directory));
        var window = new Window
        {
            Width = 520,
            Height = 720,
            Content = new AutomationPanelView { DataContext = viewModel },
        };

        window.Show();
        window.UpdateLayout();
        var tabs = Assert.Single(window.GetLogicalDescendants().OfType<TabControl>());
        Assert.Equal(4, tabs.Items.Count);
        Assert.Contains(window.GetLogicalDescendants().OfType<Button>(), button => Equals(button.Content, "＋ Nowy timer"));

        tabs.SelectedIndex = 1;
        window.UpdateLayout();
        Assert.Contains(window.GetLogicalDescendants().OfType<Button>(), button => Equals(button.Content, "＋ Nowy alias"));

        tabs.SelectedIndex = 2;
        window.UpdateLayout();
        Assert.Contains(window.GetLogicalDescendants().OfType<Button>(), button => Equals(button.Content, "＋ Nowy trigger"));

        tabs.SelectedIndex = 3;
        window.UpdateLayout();
        var teamOptions = window.GetLogicalDescendants().OfType<CheckBox>().ToList();
        Assert.Contains(teamOptions, checkBox => Equals(checkBox.Content, "Autoassist — automatyczne wspieranie drużyny"));
        Assert.Contains(teamOptions, checkBox => Equals(checkBox.Content, "Ordery — wykonuj rozkazy członków drużyny"));

        window.Close();
        await viewModel.DisposeAsync();
        Directory.Delete(directory, recursive: true);
    }

    [AvaloniaFact]
    public async Task FolderTreeView_RendersFolderChrome_AndDeleteFolderCommandWorks()
    {
        var directory = Path.Combine(Path.GetTempPath(), "KillerMudClient_FolderUi_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var viewModel = new MainWindowViewModel(new ProfileService(directory), new AppSettingsService(directory));
        viewModel.CreateFolderCommand.Execute(FolderKind.Timers);

        var panel = new AutomationPanelView
        {
            DataContext = viewModel,
            ConfirmDeletionAsync = (_, _, _) => Task.FromResult(true),
        };
        var window = new Window
        {
            Width = 500,
            Height = 700,
            Content = panel,
        };
        window.Show();
        window.UpdateLayout();
        Avalonia.Headless.AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        window.UpdateLayout();

        // The folder's editable name box is rendered for the FolderTreeNode.
        var nameBox = window
            .GetLogicalDescendants()
            .OfType<TextBox>()
            .FirstOrDefault(t => t.DataContext is FolderTreeNode { IsFolder: true });
        Assert.NotNull(nameBox);

        // The folder delete button forwards to the VM command with the FolderNode.
        var deleteButton = window
            .GetLogicalDescendants()
            .OfType<Button>()
            .FirstOrDefault(b => b.DataContext is FolderTreeNode { IsFolder: true } &&
                                 Equals(b.Content, "Usuń"));
        Assert.NotNull(deleteButton);

        // The panel confirmation command and the {Binding Folder} parameter
        // binding must both resolve against the panel / node.
        Assert.Same(panel.ConfirmDeleteFolderCommand, deleteButton!.Command);
        var folder = Assert.IsType<FolderNode>(deleteButton.CommandParameter);
        Assert.True(deleteButton.Command!.CanExecute(folder));

        await panel.ConfirmDeleteFolderCommand.ExecuteAsync(folder);

        Assert.Empty(viewModel.Folders);
        Assert.Null(viewModel.StartupErrorMessage);

        window.Close();
        await viewModel.DisposeAsync();
        Directory.Delete(directory, recursive: true);
    }

    [AvaloniaFact]
    public async Task FolderHeaderAndExpanderGutter_ToggleExpansion()
    {
        var directory = Path.Combine(Path.GetTempPath(), "KillerMudClient_FolderExpand_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var viewModel = new MainWindowViewModel(
            new ProfileService(directory),
            new AppSettingsService(directory),
            new DockLayoutService(directory));
        viewModel.CreateFolderCommand.Execute(FolderKind.Timers);
        var folder = Assert.Single(viewModel.Folders);

        var window = new Window
        {
            Width = 500,
            Height = 700,
            Content = new AutomationPanelView { DataContext = viewModel },
        };

        window.Show();
        window.UpdateLayout();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();

        var folderRow = window.GetLogicalDescendants().OfType<Border>().First(border =>
            border.Classes.Contains("mud-folder-row")
            && border.DataContext is FolderTreeNode { Folder: { } candidate }
            && ReferenceEquals(candidate, folder));
        var itemCount = folderRow.GetLogicalDescendants().OfType<TextBlock>().First(text =>
            Equals(text.Text, "0"));
        var treeItem = window.GetVisualDescendants().OfType<TreeViewItem>().First(item =>
            item.DataContext is FolderTreeNode { Folder: { } candidate }
            && ReferenceEquals(candidate, folder));
        var expanderGutter = treeItem.GetVisualDescendants().OfType<Panel>().First(panel =>
            panel.Name == "PART_ExpandCollapseChevronContainer");

        Assert.NotNull(expanderGutter.Background);

        expanderGutter.RaiseEvent(new TappedEventArgs(InputElement.TappedEvent, null!));
        Assert.False(folder.IsExpanded);

        itemCount.RaiseEvent(new TappedEventArgs(InputElement.TappedEvent, null!));
        Assert.True(folder.IsExpanded);

        var nameBox = folderRow.GetLogicalDescendants().OfType<TextBox>().Single();
        Assert.True(FolderTreeView.IsInteractiveFolderRowSource(nameBox, treeItem));
        Assert.True(folder.IsExpanded);

        window.Close();
        await viewModel.DisposeAsync();
        Directory.Delete(directory, recursive: true);
    }

    [AvaloniaFact]
    public async Task FolderName_Enter_DefersTreeRebuildAndSavesWithoutStartupError()
    {
        var directory = Path.Combine(Path.GetTempPath(), "KillerMudClient_FolderEnter_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var viewModel = new MainWindowViewModel(
            new ProfileService(directory),
            new AppSettingsService(directory),
            new DockLayoutService(directory));
        viewModel.CreateFolderCommand.Execute(FolderKind.Timers);

        var window = new Window
        {
            Width = 500,
            Height = 700,
            Content = new AutomationPanelView { DataContext = viewModel },
        };
        window.Show();
        window.UpdateLayout();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();

        var nameBox = window.GetLogicalDescendants().OfType<TextBox>().First(
            textBox => textBox.IsEffectivelyVisible
                       && textBox.DataContext is FolderTreeNode { IsFolder: true });
        nameBox.Text = "Timery bojowe";
        nameBox.Focus();

        window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        window.UpdateLayout();

        Assert.Equal("Timery bojowe", Assert.Single(viewModel.Folders).Name);
        Assert.Null(viewModel.StartupErrorMessage);
        Assert.Contains(window.GetLogicalDescendants().OfType<TextBox>(),
            textBox => textBox.DataContext is FolderTreeNode { Folder.Name: "Timery bojowe" });

        window.Close();
        await viewModel.DisposeAsync();
        Directory.Delete(directory, recursive: true);
    }
}

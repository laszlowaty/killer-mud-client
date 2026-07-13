using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using MudClient.App.Models;
using MudClient.App.Services;
using MudClient.App.ViewModels;
using MudClient.App.Views.Panels;
using Xunit;

namespace MudClient.App.Tests;

[Collection(AvaloniaUiCollection.Name)]
public sealed class FolderTreeViewUiTests
{
    [AvaloniaFact]
    public async Task AutomationPanel_UsesThreeFocusedTabsWithLocalPrimaryActions()
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
        Assert.Equal(3, tabs.Items.Count);
        Assert.Contains(window.GetLogicalDescendants().OfType<Button>(), button => Equals(button.Content, "＋ Nowy timer"));

        tabs.SelectedIndex = 1;
        window.UpdateLayout();
        Assert.Contains(window.GetLogicalDescendants().OfType<Button>(), button => Equals(button.Content, "＋ Nowy alias"));

        tabs.SelectedIndex = 2;
        window.UpdateLayout();
        Assert.Contains(window.GetLogicalDescendants().OfType<Button>(), button => Equals(button.Content, "＋ Nowy trigger"));

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

        var window = new Window
        {
            Width = 500,
            Height = 700,
            Content = new AutomationPanelView { DataContext = viewModel },
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

        // The RelativeSource command binding and the {Binding Folder} parameter
        // binding must both resolve against the FolderTreeView / node.
        Assert.NotNull(deleteButton!.Command);
        var folder = Assert.IsType<FolderNode>(deleteButton.CommandParameter);
        Assert.True(deleteButton.Command!.CanExecute(folder));

        deleteButton.Command.Execute(folder);

        Assert.Empty(viewModel.Folders);
        Assert.Null(viewModel.StartupErrorMessage);

        window.Close();
        await viewModel.DisposeAsync();
        Directory.Delete(directory, recursive: true);
    }
}

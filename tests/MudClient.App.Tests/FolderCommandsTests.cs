using MudClient.App.Models;
using MudClient.App.Services;
using MudClient.App.ViewModels;

namespace MudClient.App.Tests;

/// <summary>
/// Folder tree building and the generic folder commands (Etap 3).
/// </summary>
public sealed class FolderCommandsTests : IAsyncDisposable
{
    private readonly string _dir =
        Path.Combine(Path.GetTempPath(), "KillerMudClient_FolderTest_" + Guid.NewGuid().ToString("N"));
    private readonly MainWindowViewModel _vm;

    public FolderCommandsTests()
    {
        Directory.CreateDirectory(_dir);
        _vm = new MainWindowViewModel(new ProfileService(_dir), new AppSettingsService(_dir));
    }

    public async ValueTask DisposeAsync()
    {
        await _vm.DisposeAsync();
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    private AutomationRuleEntry AddAliasInFolder(FolderNode folder, bool enabled = true)
    {
        var alias = new AutomationRuleEntry("a", "alias", "^l$", "look", enabled) { FolderId = folder.Id };
        _vm.AutomationRules.Add(alias);
        return alias;
    }

    [Fact]
    public void CreateFolder_AddsFolderOfKindAndShowsInTree()
    {
        _vm.CreateFolderCommand.Execute(FolderKind.Timers);

        var folder = Assert.Single(_vm.Folders);
        Assert.Equal(FolderKind.Timers, folder.Kind);

        var node = Assert.Single(_vm.TimerTree);
        Assert.True(node.IsFolder);
        Assert.Same(folder, node.Folder);
    }

    [Fact]
    public void CreateSubfolder_NestsUnderParentAndInheritsGlobal()
    {
        _vm.CreateFolderCommand.Execute(FolderKind.Notes);
        var parent = _vm.Folders.Single();
        parent.IsGlobal = true;

        _vm.CreateSubfolderCommand.Execute(parent);

        Assert.Equal(2, _vm.Folders.Count);
        var child = _vm.Folders.Single(f => f.ParentId == parent.Id);
        Assert.True(child.IsGlobal);

        var rootNode = _vm.NoteTree.Single(n => n.IsFolder && ReferenceEquals(n.Folder, parent));
        Assert.Contains(rootNode.Children, c => c.IsFolder && ReferenceEquals(c.Folder, child));
    }

    [Fact]
    public void Tree_NestsItemsUnderTheirFolderAndCountsThem()
    {
        _vm.CreateFolderCommand.Execute(FolderKind.Aliases);
        var folder = _vm.Folders.Single();
        var alias = AddAliasInFolder(folder);

        var root = Assert.Single(_vm.AliasTree);
        Assert.True(root.IsFolder);
        Assert.Equal(1, root.ItemCount);
        var leaf = Assert.Single(root.Children);
        Assert.False(leaf.IsFolder);
        Assert.Same(alias, leaf.Content);
    }

    [Fact]
    public void LooseItems_RenderAtRootAlongsideFolders()
    {
        _vm.CreateFolderCommand.Execute(FolderKind.Aliases);
        // A loose alias (no folder).
        _vm.AutomationRules.Add(new AutomationRuleEntry("loose", "alias", "^x$", "y", true));

        Assert.Equal(2, _vm.AliasTree.Count);
        Assert.Contains(_vm.AliasTree, n => n.IsFolder);
        Assert.Contains(_vm.AliasTree, n => !n.IsFolder);
    }

    [Fact]
    public void DeleteFolder_RemovesFolderAndItsItems()
    {
        _vm.CreateFolderCommand.Execute(FolderKind.Timers);
        var folder = _vm.Folders.Single();
        _vm.Timers.Add(new TimerEntry { Name = "t", Seconds = 5, CommandsText = "look", FolderId = folder.Id });

        _vm.DeleteFolderCommand.Execute(folder);

        Assert.Empty(_vm.Folders);
        Assert.Empty(_vm.Timers);
        Assert.Empty(_vm.TimerTree);
    }

    [Fact]
    public void DeleteFolder_RemovesNestedSubfoldersAndTheirItems()
    {
        _vm.CreateFolderCommand.Execute(FolderKind.Aliases);
        var parent = _vm.Folders.Single();
        _vm.CreateSubfolderCommand.Execute(parent);
        var child = _vm.Folders.Single(f => f.ParentId == parent.Id);
        AddAliasInFolder(child);

        _vm.DeleteFolderCommand.Execute(parent);

        Assert.Empty(_vm.Folders);
        Assert.Empty(_vm.AutomationRules);
    }

    [Fact]
    public void ToggleFolderGlobal_CascadesToItemsAndBack()
    {
        _vm.CreateFolderCommand.Execute(FolderKind.Aliases);
        var folder = _vm.Folders.Single();
        var alias = AddAliasInFolder(folder);

        _vm.ToggleFolderGlobalCommand.Execute(folder);
        Assert.True(folder.IsGlobal);
        Assert.True(alias.IsGlobal);

        _vm.ToggleFolderGlobalCommand.Execute(folder);
        Assert.False(folder.IsGlobal);
        Assert.False(alias.IsGlobal);
    }

    [Fact]
    public void ToggleFolderEnabled_DisablesThenEnablesAllItems()
    {
        _vm.CreateFolderCommand.Execute(FolderKind.Triggers);
        var folder = _vm.Folders.Single();
        var t1 = new AutomationRuleEntry("t1", "trigger", "^a$", "x", true) { FolderId = folder.Id };
        var t2 = new AutomationRuleEntry("t2", "trigger", "^b$", "y", true) { FolderId = folder.Id };
        _vm.AutomationRules.Add(t1);
        _vm.AutomationRules.Add(t2);

        _vm.ToggleFolderEnabledCommand.Execute(folder);
        Assert.False(t1.IsEnabled);
        Assert.False(t2.IsEnabled);

        _vm.ToggleFolderEnabledCommand.Execute(folder);
        Assert.True(t1.IsEnabled);
        Assert.True(t2.IsEnabled);
    }
}

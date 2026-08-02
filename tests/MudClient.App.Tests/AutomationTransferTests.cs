using System.Text;
using System.Text.Json;
using MudClient.App.Models;
using MudClient.App.Services;
using MudClient.App.ViewModels;

namespace MudClient.App.Tests;

public sealed class AutomationTransferTests : IAsyncDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "KillerMudClient_Transfer_" + Guid.NewGuid().ToString("N"));
    private readonly MainWindowViewModel _vm;
    private readonly AutomationTransferService _service = new();

    public AutomationTransferTests()
    {
        Directory.CreateDirectory(_dir);
        _vm = new MainWindowViewModel(new ProfileService(_dir), new AppSettingsService(_dir));
    }

    public async ValueTask DisposeAsync()
    {
        await _vm.DisposeAsync();
        Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public async Task FolderPackage_RoundTripsNestedAliasesWithRemappedIds()
    {
        _vm.CreateFolderCommand.Execute(FolderKind.Aliases);
        var root = _vm.Folders.Single();
        _vm.CreateSubfolderCommand.Execute(root);
        var child = _vm.Folders.Single(folder => folder.ParentId == root.Id);
        var alias = new AutomationRuleEntry("l", "alias", "^l$", "look", true) { FolderId = child.Id };
        _vm.AutomationRules.Add(alias);

        var exported = _vm.CreateAutomationTransferPackage(root);
        await using var stream = new MemoryStream();
        await _service.WriteAsync(stream, exported, TestContext.Current.CancellationToken);
        stream.Position = 0;
        var imported = await _service.ReadAsync(stream, TestContext.Current.CancellationToken);
        _vm.ImportAutomationTransferPackage(imported);

        Assert.Equal(4, _vm.Folders.Count);
        Assert.Equal(2, _vm.AliasRules.Count);
        var importedAlias = _vm.AliasRules.Single(entry => !ReferenceEquals(entry, alias));
        Assert.NotEqual(child.Id, importedAlias.FolderId);
        var importedChild = _vm.Folders.Single(folder => folder.Id == importedAlias.FolderId);
        Assert.NotEqual(root.Id, importedChild.ParentId);
    }

    [Fact]
    public void IndividualTimerPackage_DoesNotCarryMissingFolderReference()
    {
        var timer = new TimerEntry
        {
            Name = "regen",
            Seconds = 5,
            CommandsText = "score",
            FolderId = "folder",
        };

        var package = _vm.CreateAutomationTransferPackage(timer);

        Assert.Equal(FolderKind.Timers, package.Kind);
        Assert.Null(Assert.Single(package.Timers).FolderId);
        Assert.Empty(package.Folders);
    }

    [Fact]
    public async Task ReadAsync_RejectsFolderCycle()
    {
        const string json = """
            {
              "Version": 1,
              "Kind": 0,
              "Folders": [
                { "Id": "a", "ParentId": "b", "Name": "A", "Kind": 0 },
                { "Id": "b", "ParentId": "a", "Name": "B", "Kind": 0 }
              ]
            }
            """;
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

        await Assert.ThrowsAsync<JsonException>(() =>
            _service.ReadAsync(stream, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AutowalkPackage_RoundTripsAllLocationsWithRemappedFolders()
    {
        _vm.CreateFolderCommand.Execute(FolderKind.Autowalk);
        var root = _vm.Folders.Single();
        _vm.CreateSubfolderCommand.Execute(root);
        var child = _vm.Folders.Single(folder => folder.ParentId == root.Id);
        root.IsGlobal = true;
        child.IsGlobal = true;
        var location = new AutowalkLocation("Plac Arras", "1234", isGlobal: true)
        {
            FolderId = child.Id,
        };
        _vm.Locations.Add(location);

        var looseLocation = new AutowalkLocation("Gospoda", "4321");
        _vm.Locations.Add(looseLocation);

        var exported = _vm.CreateAutowalkTransferPackage();
        await using var stream = new MemoryStream();
        await _service.WriteAsync(stream, exported, TestContext.Current.CancellationToken);
        stream.Position = 0;
        var imported = await _service.ReadAsync(stream, TestContext.Current.CancellationToken);
        _vm.ImportAutomationTransferPackage(imported);

        Assert.Equal(4, _vm.Folders.Count);
        Assert.Equal(4, _vm.Locations.Count);
        var importedLocation = _vm.Locations
            .Single(entry => entry.Name == location.Name && !ReferenceEquals(entry, location));
        Assert.Equal("Plac Arras", importedLocation.Name);
        Assert.Equal("1234", importedLocation.Vnum);
        Assert.NotEqual(child.Id, importedLocation.FolderId);
        Assert.True(importedLocation.IsGlobal);
        var importedLooseLocation = _vm.Locations
            .Single(entry => entry.Name == looseLocation.Name && !ReferenceEquals(entry, looseLocation));
        Assert.Null(importedLooseLocation.FolderId);
    }
}

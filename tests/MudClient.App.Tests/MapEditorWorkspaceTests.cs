using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using MudClient.App.ViewModels;
using MudClient.Core.Gmcp;
using MudClient.Core.Map;

namespace MudClient.App.Tests;

public sealed class MapEditorWorkspaceTests
{
    [AvaloniaFact]
    public async Task WorkingMap_CanBeComparedExportedAndDiscarded()
    {
        var root = Path.Combine(Path.GetTempPath(), "KillerMudClient_MapWorkspaceTests_" + Guid.NewGuid().ToString("N"));
        var appBase = Path.Combine(root, "app");
        var dataRoot = Path.Combine(root, "data");
        var mapDirectory = Path.Combine(appBase, "Assets", "Map");
        var baseMapPath = Path.Combine(mapDirectory, "world-map.json");
        var exportPath = Path.Combine(root, "export", "world-map.json");
        Directory.CreateDirectory(mapDirectory);

        try
        {
            await new MapWriter().SaveAsync(CreateDocument(), baseMapPath);
            using var viewModel = new MapViewModel(appBase, new GmcpLocationResolver(), dataRoot);
            await viewModel.InitializeAsync();

            Assert.False(viewModel.IsUsingWorkingMap);
            viewModel.HandleRoomSnapshot(new RoomSnapshot("100", "Początek", "droga", []));
            Assert.True(viewModel.SetCurrentMapRoomSymbol("!"));
            await viewModel.SaveMapEditorAsync();

            Assert.True(viewModel.IsUsingWorkingMap);
            Assert.True(File.Exists(Path.Combine(dataRoot, "MapEditor", "recovery.json.gz")));
            Assert.Contains("pokoje +0/-0/~1", await viewModel.GetMapEditorDiffAsync());
            Assert.Contains("Wyeksportowano", await viewModel.ExportMapEditorAsync(exportPath));
            Assert.True(File.Exists(exportPath));

            Assert.Contains("Odrzucono", await viewModel.DiscardWorkingMapAsync());
            Assert.False(viewModel.IsUsingWorkingMap);
            Assert.False(File.Exists(Path.Combine(dataRoot, "MapEditor", "world-map.json")));
            Assert.False(File.Exists(Path.Combine(dataRoot, "MapEditor", "recovery.json.gz")));
            Assert.Null(viewModel.MapIndex!.FindFirstRoomByVnum("100")!.Symbol);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [AvaloniaFact]
    public async Task ImportMap_ValidatesAndActivatesLosslessWorkingCopy()
    {
        var root = Path.Combine(Path.GetTempPath(), "KillerMudClient_MapImportTests_" + Guid.NewGuid().ToString("N"));
        var appBase = Path.Combine(root, "app");
        var dataRoot = Path.Combine(root, "data");
        var mapDirectory = Path.Combine(appBase, "Assets", "Map");
        var importPath = Path.Combine(root, "import.json");
        Directory.CreateDirectory(mapDirectory);

        try
        {
            await new MapWriter().SaveAsync(CreateDocument(), Path.Combine(mapDirectory, "world-map.json"));
            var importedDocument = new MapDocument
            {
                Areas =
                [
                    new MapArea
                    {
                        Id = 7,
                        Name = "Import",
                        Rooms =
                        [
                            new MapRoom
                            {
                                Id = 77,
                                AreaId = 7,
                                Name = "Zaimportowany pokój",
                                Coordinates = new MapCoordinates(9, 8, 1),
                                UserData = new Dictionary<string, System.Text.Json.JsonElement>
                                {
                                    ["vnum"] = System.Text.Json.JsonSerializer.SerializeToElement("777"),
                                },
                            },
                        ],
                    },
                ],
            };
            await new MapWriter().SaveAsync(importedDocument, importPath);
            using var viewModel = new MapViewModel(appBase, new GmcpLocationResolver(), dataRoot);
            await viewModel.InitializeAsync();

            var result = await viewModel.ImportMapEditorAsync(importPath);

            Assert.Contains("Zaimportowano", result);
            Assert.True(viewModel.IsUsingWorkingMap);
            Assert.Equal("Zaimportowany pokój", viewModel.MapIndex!.FindFirstRoomByVnum("777")!.Name);
            var workingPath = Path.Combine(dataRoot, "MapEditor", "world-map.json");
            Assert.True(File.Exists(workingPath));
            var validHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
                await File.ReadAllBytesAsync(workingPath)));

            await File.WriteAllTextAsync(importPath, "to nie jest JSON");
            var invalidResult = await viewModel.ImportMapEditorAsync(importPath);

            Assert.Contains("Nie udało się", invalidResult);
            Assert.Equal(validHash, Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
                await File.ReadAllBytesAsync(workingPath))));
            Assert.Equal("Zaimportowany pokój", viewModel.MapIndex.FindFirstRoomByVnum("777")!.Name);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [AvaloniaFact]
    public async Task UnsavedEdit_IsRecoveredAfterRestartAndCanBeUndone()
    {
        var root = Path.Combine(Path.GetTempPath(), "KillerMudClient_MapRecoveryTests_" + Guid.NewGuid().ToString("N"));
        var appBase = Path.Combine(root, "app");
        var dataRoot = Path.Combine(root, "data");
        var mapDirectory = Path.Combine(appBase, "Assets", "Map");
        Directory.CreateDirectory(mapDirectory);

        try
        {
            await new MapWriter().SaveAsync(CreateDocument(), Path.Combine(mapDirectory, "world-map.json"));
            var first = new MapViewModel(appBase, new GmcpLocationResolver(), dataRoot);
            await first.InitializeAsync();
            first.HandleRoomSnapshot(new RoomSnapshot("100", "Początek", "droga", []));
            Assert.True(first.SetCurrentMapRoomSymbol("!"));
            await first.DisposeAsync();

            Assert.True(File.Exists(Path.Combine(dataRoot, "MapEditor", "recovery.json.gz")));
            var second = new MapViewModel(appBase, new GmcpLocationResolver(), dataRoot);
            await second.InitializeAsync();

            Assert.True(second.IsUsingRecoveryMap);
            Assert.True(second.IsMapEditorDirty);
            Assert.Equal("!", second.MapIndex!.FindFirstRoomByVnum("100")!.Symbol);
            second.UndoMapEditor();
            Assert.Null(second.MapIndex.FindFirstRoomByVnum("100")!.Symbol);
            await second.DisposeAsync();
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [AvaloniaFact]
    public async Task SavedCheckpoint_PreservesUndoHistoryWithoutMarkingMapRecovered()
    {
        var root = Path.Combine(Path.GetTempPath(), "KillerMudClient_MapCheckpointTests_" + Guid.NewGuid().ToString("N"));
        var appBase = Path.Combine(root, "app");
        var dataRoot = Path.Combine(root, "data");
        var mapDirectory = Path.Combine(appBase, "Assets", "Map");
        Directory.CreateDirectory(mapDirectory);

        try
        {
            await new MapWriter().SaveAsync(CreateDocument(), Path.Combine(mapDirectory, "world-map.json"));
            var first = new MapViewModel(appBase, new GmcpLocationResolver(), dataRoot);
            await first.InitializeAsync();
            first.HandleRoomSnapshot(new RoomSnapshot("100", "Początek", "droga", []));
            first.SetCurrentMapRoomSymbol("!");
            await first.SaveMapEditorAsync();
            await first.DisposeAsync();

            var second = new MapViewModel(appBase, new GmcpLocationResolver(), dataRoot);
            await second.InitializeAsync();

            Assert.False(second.IsUsingRecoveryMap);
            Assert.False(second.IsMapEditorDirty);
            Assert.True(second.UndoMapEditorCommand.CanExecute(null));
            second.UndoMapEditor();
            Assert.Null(second.MapIndex!.FindFirstRoomByVnum("100")!.Symbol);
            await second.DisposeAsync();
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [AvaloniaFact]
    public async Task CorruptRecovery_DoesNotHideBaseMap()
    {
        var root = Path.Combine(Path.GetTempPath(), "KillerMudClient_CorruptRecoveryTests_" + Guid.NewGuid().ToString("N"));
        var appBase = Path.Combine(root, "app");
        var dataRoot = Path.Combine(root, "data");
        var mapDirectory = Path.Combine(appBase, "Assets", "Map");
        var editorDirectory = Path.Combine(dataRoot, "MapEditor");
        Directory.CreateDirectory(mapDirectory);
        Directory.CreateDirectory(editorDirectory);

        try
        {
            await new MapWriter().SaveAsync(CreateDocument(), Path.Combine(mapDirectory, "world-map.json"));
            await File.WriteAllTextAsync(Path.Combine(editorDirectory, "recovery.json.gz"), "uszkodzony checkpoint");
            var viewModel = new MapViewModel(appBase, new GmcpLocationResolver(), dataRoot);

            await viewModel.InitializeAsync();

            Assert.False(viewModel.IsUsingRecoveryMap);
            Assert.NotNull(viewModel.MapIndex!.FindFirstRoomByVnum("100"));
            await viewModel.DisposeAsync();
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [AvaloniaFact]
    public async Task CreateAreaCommand_AddsAndSelectsNamedArea()
    {
        var root = Path.Combine(Path.GetTempPath(), "KillerMudClient_CreateMapAreaTests_" + Guid.NewGuid().ToString("N"));
        var appBase = Path.Combine(root, "app");
        var dataRoot = Path.Combine(root, "data");
        var mapDirectory = Path.Combine(appBase, "Assets", "Map");
        Directory.CreateDirectory(mapDirectory);
        MapViewModel? viewModel = null;

        try
        {
            await new MapWriter().SaveAsync(CreateDocument(), Path.Combine(mapDirectory, "world-map.json"));
            viewModel = new MapViewModel(appBase, new GmcpLocationResolver(), dataRoot);
            await viewModel.InitializeAsync();
            viewModel.LordModeEnabled = true;
            viewModel.NewMapAreaName = "Nowa kraina";

            Assert.True(viewModel.CreateMapAreaCommand.CanExecute(null));
            viewModel.CreateMapAreaCommand.Execute(null);

            Assert.Contains(viewModel.Areas, area => area.Name == "Nowa kraina");
            Assert.Equal("Nowa kraina", viewModel.SelectedArea?.Name);
            Assert.Equal(string.Empty, viewModel.NewMapAreaName);
            Assert.True(viewModel.IsMapEditorDirty);
            Assert.Contains("Utworzono obszar", viewModel.MapEditorStatus);
            Assert.True(viewModel.CanMoveExistingRoomsToNewArea);
            Assert.True(viewModel.SetMoveExistingRoomsToNewArea(true));
            Assert.True(viewModel.MoveExistingRoomsToNewArea);
            Assert.Contains("będą przenoszone", viewModel.MapEditorStatus);

            Assert.True(viewModel.SetMoveExistingRoomsToNewArea(false));
            viewModel.SelectedArea = viewModel.Areas.Single(area => area.Name == "Test");
            Assert.True(viewModel.SetMoveExistingRoomsToNewArea(true));
            Assert.Contains("obszaru Test", viewModel.MapEditorStatus);
        }
        finally
        {
            if (viewModel is not null)
            {
                await viewModel.DisposeAsync();
            }
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [AvaloniaFact]
    public async Task NewArea_StartsMappingFromCurrentVnumMissingFromBaseMap()
    {
        var root = Path.Combine(Path.GetTempPath(), "KillerMudClient_NewAreaStartTests_" + Guid.NewGuid().ToString("N"));
        var appBase = Path.Combine(root, "app");
        var dataRoot = Path.Combine(root, "data");
        var mapDirectory = Path.Combine(appBase, "Assets", "Map");
        Directory.CreateDirectory(mapDirectory);
        MapViewModel? viewModel = null;

        try
        {
            await new MapWriter().SaveAsync(CreateDocument(), Path.Combine(mapDirectory, "world-map.json"));
            var resolver = new GmcpLocationResolver();
            viewModel = new MapViewModel(appBase, resolver, dataRoot);
            await viewModel.InitializeAsync();
            resolver.Process(new GmcpMessage("Room.Info", """{"vnum":"3921"}"""));
            Dispatcher.UIThread.RunJobs();
            viewModel.HandleRoomSnapshot(new RoomSnapshot(
                "3921",
                "Nowy początek",
                "droga",
                [new RoomSnapshotExit("E", "east", false, false)]));
            viewModel.LordModeEnabled = true;
            viewModel.NewMapAreaName = "Nowa kraina";
            viewModel.CreateMapAreaCommand.Execute(null);

            Assert.True(viewModel.StartMapEditorCommand.CanExecute(null));
            viewModel.StartMapEditorCommand.Execute(null);

            Assert.True(viewModel.IsMapEditorActive);
            var startingRoom = viewModel.MapIndex!.FindFirstRoomByVnum("3921");
            Assert.NotNull(startingRoom);
            Assert.Equal("Nowa kraina", viewModel.MapIndex.AreasById[startingRoom.AreaId].Name);
            Assert.Contains("Utworzono pokój startowy", viewModel.MapEditorStatus);
        }
        finally
        {
            if (viewModel is not null)
            {
                await viewModel.DisposeAsync();
            }
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [AvaloniaFact]
    public async Task MissingRoomInfo_CancelsPendingMovementButKeepsMappingActive()
    {
        var root = Path.Combine(Path.GetTempPath(), "KillerMudClient_MapTimeoutTests_" + Guid.NewGuid().ToString("N"));
        var appBase = Path.Combine(root, "app");
        var dataRoot = Path.Combine(root, "data");
        var mapDirectory = Path.Combine(appBase, "Assets", "Map");
        Directory.CreateDirectory(mapDirectory);
        MapViewModel? viewModel = null;

        try
        {
            await new MapWriter().SaveAsync(CreateDocument(), Path.Combine(mapDirectory, "world-map.json"));
            var resolver = new GmcpLocationResolver();
            viewModel = new MapViewModel(appBase, resolver, dataRoot, TimeSpan.FromMilliseconds(30));
            await viewModel.InitializeAsync();
            resolver.Process(new GmcpMessage("Room.Info", """{"vnum":"100"}"""));
            Dispatcher.UIThread.RunJobs();
            viewModel.LordModeEnabled = true;
            viewModel.HandleRoomSnapshot(new RoomSnapshot(
                "100",
                "Początek",
                "droga",
                [new RoomSnapshotExit("E", "east", false, false)]));
            viewModel.StartMapEditor();

            Assert.True(viewModel.PrepareMapEditorCommand("east").Allow);
            Assert.True(viewModel.IsMapEditorAwaitingRoomInfo);

            await Task.Delay(100, TestContext.Current.CancellationToken);
            Dispatcher.UIThread.RunJobs();

            Assert.False(viewModel.IsMapEditorAwaitingRoomInfo);
            Assert.True(viewModel.IsMapEditorActive);
            Assert.Contains("Brak Room.Info", viewModel.MapEditorStatus);
        }
        finally
        {
            if (viewModel is not null)
            {
                await viewModel.DisposeAsync();
            }
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [AvaloniaFact]
    public async Task RoomInfoBeforeTimeout_PreventsLateCancellationMessage()
    {
        var root = Path.Combine(Path.GetTempPath(), "KillerMudClient_MapTimeoutRaceTests_" + Guid.NewGuid().ToString("N"));
        var appBase = Path.Combine(root, "app");
        var dataRoot = Path.Combine(root, "data");
        var mapDirectory = Path.Combine(appBase, "Assets", "Map");
        Directory.CreateDirectory(mapDirectory);
        MapViewModel? viewModel = null;

        try
        {
            await new MapWriter().SaveAsync(CreateDocument(), Path.Combine(mapDirectory, "world-map.json"));
            var resolver = new GmcpLocationResolver();
            viewModel = new MapViewModel(appBase, resolver, dataRoot, TimeSpan.FromMilliseconds(50));
            await viewModel.InitializeAsync();
            resolver.Process(new GmcpMessage("Room.Info", """{"vnum":"100"}"""));
            Dispatcher.UIThread.RunJobs();
            viewModel.LordModeEnabled = true;
            viewModel.HandleRoomSnapshot(new RoomSnapshot(
                "100",
                "Początek",
                "droga",
                [new RoomSnapshotExit("E", "east", false, false)]));
            viewModel.StartMapEditor();
            Assert.True(viewModel.PrepareMapEditorCommand("east").Allow);

            viewModel.HandleRoomSnapshot(new RoomSnapshot("200", "Cel", "droga", []));
            var completedStatus = viewModel.MapEditorStatus;
            await Task.Delay(100, TestContext.Current.CancellationToken);
            Dispatcher.UIThread.RunJobs();

            Assert.False(viewModel.IsMapEditorAwaitingRoomInfo);
            Assert.True(viewModel.IsMapEditorActive);
            Assert.Equal(completedStatus, viewModel.MapEditorStatus);
            Assert.DoesNotContain("Brak Room.Info", viewModel.MapEditorStatus);
        }
        finally
        {
            if (viewModel is not null)
            {
                await viewModel.DisposeAsync();
            }
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static MapDocument CreateDocument() => new()
    {
        Areas =
        [
            new MapArea
            {
                Id = 1,
                Name = "Test",
                Rooms =
                [
                    new MapRoom
                    {
                        Id = 1,
                        AreaId = 1,
                        Name = "Początek",
                        Coordinates = new MapCoordinates(0, 0, 0),
                        UserData = new Dictionary<string, System.Text.Json.JsonElement>
                        {
                            ["vnum"] = System.Text.Json.JsonSerializer.SerializeToElement("100"),
                        },
                    },
                ],
            },
        ],
    };
}

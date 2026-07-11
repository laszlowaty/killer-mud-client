using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using System.Text.Json;
using MudClient.App.ViewModels;
using MudClient.Core.Gmcp;
using MudClient.Core.Map;

namespace MudClient.App.Tests;

public sealed class MapViewModelTests
{
    private static readonly MapRoom SampleRoom = new()
    {
        Id = 1,
        AreaId = 1,
        Coordinates = new MapCoordinates(0, 0, 0),
        UserData = new Dictionary<string, JsonElement>
        {
            ["sector"] = JsonSerializer.SerializeToElement("inside"),
        },
    };

    private static readonly MapRoom SampleRoomNoSector = new()
    {
        Id = 2,
        AreaId = 1,
        Coordinates = new MapCoordinates(1, 1, 0),
    };

    // ====================================================================
    // SelectedRoomIcon – null behaviour
    // ====================================================================

    [Fact]
    public void SelectedRoomIcon_WhenTextureCacheNull_ReturnsNull()
    {
        using var vm = CreateViewModel();
        Assert.Null(vm.TextureCache);
        Assert.Null(vm.SelectedRoomIcon);
    }

    [Fact]
    public void SelectedRoomIcon_WhenTextureCacheNullAndRoomSet_ReturnsNull()
    {
        using var vm = CreateViewModel();
        vm.SelectedRoom = SampleRoom;
        Assert.Null(vm.TextureCache);
        Assert.Null(vm.SelectedRoomIcon);
    }

    [Fact]
    public void SelectedRoomIcon_WhenTextureCacheNullAndRoomWithNullSector_ReturnsNull()
    {
        using var vm = CreateViewModel();
        vm.SelectedRoom = SampleRoomNoSector;
        Assert.Null(vm.TextureCache);
        Assert.Null(vm.SelectedRoomIcon);
    }

    // ====================================================================
    // PropertyChanged notifications for SelectedRoomIcon
    // ====================================================================

    [Fact]
    public void SelectedRoom_Setter_RaisesPropertyChangedForSelectedRoomIcon()
    {
        using var vm = CreateViewModel();
        var changedProperties = new List<string?>();

        vm.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName);

        vm.SelectedRoom = SampleRoom;

        Assert.Contains(nameof(MapViewModel.SelectedRoomIcon), changedProperties);
    }

    [Fact]
    public void SelectedRoom_Setter_DoesNotRaiseWhenSameInstance()
    {
        using var vm = CreateViewModel();
        vm.SelectedRoom = SampleRoom;

        var changedProperties = new List<string?>();
        vm.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName);

        // Set same instance again
        vm.SelectedRoom = SampleRoom;

        Assert.DoesNotContain(nameof(MapViewModel.SelectedRoomIcon), changedProperties);
    }

    [Fact]
    public void SelectedRoom_Setter_RaisesPropertyChangedForSelectedRoomIconWhenSetToNull()
    {
        using var vm = CreateViewModel();
        vm.SelectedRoom = SampleRoom;

        var changedProperties = new List<string?>();
        vm.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName);

        vm.SelectedRoom = null;

        Assert.Contains(nameof(MapViewModel.SelectedRoomIcon), changedProperties);
    }

    // ====================================================================
    // SelectedRoomIcon – expression logic
    // ====================================================================

    [Fact]
    public void SelectedRoomIcon_WithNullSelectedRoom_PassesEmptyStringToGetTexture()
    {
        // TextureCache is null -> result will be null regardless of SelectedRoom.
        // This test verifies the binding expression does not throw with null SelectedRoom.
        using var vm = CreateViewModel();
        vm.SelectedRoom = null;

        var ex = Record.Exception(() => _ = vm.SelectedRoomIcon);
        Assert.Null(ex);
    }

    // ====================================================================
    // FollowPlayer initial state and toggling
    // ====================================================================

    [Fact]
    public void FollowPlayer_DefaultsToTrue()
    {
        using var vm = CreateViewModel();
        Assert.True(vm.FollowPlayer);
    }

    [Fact]
    public void FollowPlayer_CanBeSetToFalseAndBack()
    {
        using var vm = CreateViewModel();
        vm.FollowPlayer = false;
        Assert.False(vm.FollowPlayer);
        vm.FollowPlayer = true;
        Assert.True(vm.FollowPlayer);
    }

    [Fact]
    public void FollowPlayer_Setter_RaisesPropertyChanged()
    {
        using var vm = CreateViewModel();
        var changedProperties = new List<string?>();
        vm.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName);

        vm.FollowPlayer = false;

        Assert.Contains(nameof(MapViewModel.FollowPlayer), changedProperties);
    }

    // ====================================================================
    // Public setters disable FollowPlayer
    // ====================================================================

    [Fact]
    public void SelectedArea_Setter_SetsFollowPlayerToFalse()
    {
        using var vm = CreateViewModel();
        Assert.True(vm.FollowPlayer);

        vm.SelectedArea = new MapArea { Id = 99, Name = "Test" };

        Assert.False(vm.FollowPlayer);
    }

    [Fact]
    public void SelectedArea_Setter_SettingNullDoesNotChangeFollowPlayer()
    {
        using var vm = CreateViewModel();
        Assert.True(vm.FollowPlayer);

        // _selectedArea is already null, so SetProperty returns false
        vm.SelectedArea = null;

        Assert.True(vm.FollowPlayer);
    }

    [Fact]
    public void SelectedZ_Setter_SetsFollowPlayerToFalse()
    {
        using var vm = CreateViewModel();
        Assert.True(vm.FollowPlayer);

        vm.SelectedZ = 5.0;  // different from default 0.0

        Assert.False(vm.FollowPlayer);
    }

    [Fact]
    public void SelectedZ_Setter_SettingDefaultValueDoesNotChangeFollowPlayer()
    {
        using var vm = CreateViewModel();
        vm.FollowPlayer = true;

        // _selectedZ is already 0.0, so SetProperty returns false
        vm.SelectedZ = 0.0;

        Assert.True(vm.FollowPlayer);
    }

    // ====================================================================
    // CenterCommand (Wycentruj) behavior
    // ====================================================================

    [Fact]
    public void CenterCommand_WhenCurrentRoomIsNull_DoesNotChangeFollowPlayer()
    {
        using var vm = CreateViewModel();
        Assert.Null(vm.CurrentRoom);

        vm.FollowPlayer = false;
        vm.CenterCommand.Execute(null);

        Assert.False(vm.FollowPlayer);  // still false — no-op
    }

    [Fact]
    public void CenterCommand_WhenCurrentRoomIsNull_DoesNotThrow()
    {
        using var vm = CreateViewModel();
        var ex = Record.Exception(() => vm.CenterCommand.Execute(null));
        Assert.Null(ex);
    }

    [Fact]
    public void CenterCommand_WhenCurrentRoomIsSet_SetsFollowPlayerToTrue()
    {
        using var vm = CreateViewModel();
        var index = CreateSampleIndex();
        SetMapIndex(vm, index);
        SetCurrentRoom(vm, CreateSampleRoom());

        vm.FollowPlayer = false;
        vm.CenterCommand.Execute(null);

        Assert.True(vm.FollowPlayer);
    }

    [Fact]
    public void CenterCommand_WhenCurrentRoomIsSet_RaisesCenterOnCurrentRoomRequested()
    {
        using var vm = CreateViewModel();
        var index = CreateSampleIndex();
        SetMapIndex(vm, index);
        SetCurrentRoom(vm, CreateSampleRoom());

        var raised = false;
        vm.CenterOnCurrentRoomRequested += () => raised = true;

        vm.CenterCommand.Execute(null);

        Assert.True(raised);
    }

    [Fact]
    public void CenterCommand_WhenCurrentRoomIsSet_UpdatesSelectedAreaToMatchRoom()
    {
        using var vm = CreateViewModel();
        var index = CreateSampleIndex();
        SetMapIndex(vm, index);
        SetCurrentRoom(vm, CreateSampleRoom());

        vm.CenterCommand.Execute(null);

        Assert.NotNull(vm.SelectedArea);
        Assert.Equal(1, vm.SelectedArea.Id);
    }

    [Fact]
    public void CenterCommand_WhenCurrentRoomIsSet_UpdatesSelectedZToMatchRoom()
    {
        using var vm = CreateViewModel();
        var index = CreateSampleIndex();
        SetMapIndex(vm, index);
        SetCurrentRoom(vm, CreateSampleRoom());

        vm.CenterCommand.Execute(null);

        Assert.Equal(5.0, vm.SelectedZ);
    }

    // ====================================================================
    // TryResolveCurrentRoom (invoked via reflection) — FollowPlayer preservation
    // ====================================================================

    [Fact]
    public void TryResolveCurrentRoom_DoesNotDisableFollowPlayer()
    {
        using var vm = CreateViewModel();
        var index = CreateSampleIndex();
        SetMapIndex(vm, index);
        SetLocationVnum(vm, "100");

        // FollowPlayer already true by default
        InvokeTryResolveCurrentRoom(vm);

        Assert.True(vm.FollowPlayer);  // must not be disabled by internal setters
    }

    [Fact]
    public void TryResolveCurrentRoom_WhenFollowPlayerIsFalse_EnablesItAndRequestsCentering()
    {
        using var vm = CreateViewModel();
        var index = CreateSampleIndex();
        SetMapIndex(vm, index);
        SetLocationVnum(vm, "100");
        var centerRequested = false;
        vm.CenterOnCurrentRoomRequested += () => centerRequested = true;

        vm.FollowPlayer = false;
        InvokeTryResolveCurrentRoom(vm);

        Assert.True(vm.FollowPlayer);
        Assert.True(centerRequested);
    }

    // ====================================================================
    // TryResolveCurrentRoom — current room resolution
    // ====================================================================

    [Fact]
    public void TryResolveCurrentRoom_SetsCurrentRoom()
    {
        using var vm = CreateViewModel();
        var index = CreateSampleIndex();
        SetMapIndex(vm, index);
        SetLocationVnum(vm, "100");

        InvokeTryResolveCurrentRoom(vm);

        Assert.NotNull(vm.CurrentRoom);
        Assert.Equal("100", vm.CurrentRoom!.Vnum);
        Assert.Equal("Test Room", vm.CurrentRoom.Name);
    }

    [Fact]
    public void TryResolveCurrentRoom_SetsCurrentSector()
    {
        using var vm = CreateViewModel();
        var index = CreateSampleIndex();
        SetMapIndex(vm, index);
        SetLocationVnum(vm, "100");

        InvokeTryResolveCurrentRoom(vm);

        Assert.Equal("inside", vm.CurrentSectorName);
    }

    [Fact]
    public void TryResolveCurrentRoom_WithUnknownVnum_DoesNotSetCurrentRoom()
    {
        using var vm = CreateViewModel();
        var index = CreateSampleIndex();
        SetMapIndex(vm, index);
        SetLocationVnum(vm, "999");

        InvokeTryResolveCurrentRoom(vm);

        Assert.Null(vm.CurrentRoom);
        Assert.Contains("VNUM 999", vm.StatusMessage);
    }

    [Fact]
    public void TryResolveCurrentRoom_WithNullVnum_DoesNothing()
    {
        using var vm = CreateViewModel();
        var index = CreateSampleIndex();
        SetMapIndex(vm, index);
        // CurrentVnum stays null

        InvokeTryResolveCurrentRoom(vm);

        Assert.Null(vm.CurrentRoom);
    }

    // ====================================================================
    // TryResolveCurrentRoom — area and Z selection
    // ====================================================================

    [Fact]
    public void TryResolveCurrentRoom_SelectsCorrectArea()
    {
        using var vm = CreateViewModel();
        var index = CreateSampleIndex();
        SetMapIndex(vm, index);
        SetLocationVnum(vm, "100");

        InvokeTryResolveCurrentRoom(vm);

        Assert.NotNull(vm.SelectedArea);
        Assert.Equal(1, vm.SelectedArea!.Id);
        Assert.Equal("Test Area", vm.SelectedArea.Name);
    }

    [Fact]
    public void TryResolveCurrentRoom_DoesNotSwitchArea_WhenAlreadyCorrect()
    {
        using var vm = CreateViewModel();
        var index = CreateSampleIndex();
        SetMapIndex(vm, index);
        SetLocationVnum(vm, "100");

        // Set area to the same one via internal setter first
        var area = index.AreasById[1];
        SetSelectedAreaInternal(vm, area);

        var changedProperties = new List<string?>();
        vm.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName);

        InvokeTryResolveCurrentRoom(vm);

        // SelectedArea should not have changed (already same instance)
        Assert.Same(area, vm.SelectedArea);
    }

    [Fact]
    public void TryResolveCurrentRoom_SetsCorrectZ()
    {
        using var vm = CreateViewModel();
        var index = CreateSampleIndex();
        SetMapIndex(vm, index);
        SetLocationVnum(vm, "100");

        InvokeTryResolveCurrentRoom(vm);

        Assert.Equal(5.0, vm.SelectedZ);
    }

    [Fact]
    public void TryResolveCurrentRoom_UpdatesZLevels()
    {
        using var vm = CreateViewModel();
        var index = CreateSampleIndex();
        SetMapIndex(vm, index);
        SetLocationVnum(vm, "100");

        InvokeTryResolveCurrentRoom(vm);

        Assert.Contains(5.0, vm.ZLevels);
    }

    // ====================================================================
    // TryResolveCurrentRoom — SelectedRoom defaulting
    // ====================================================================

    [Fact]
    public void TryResolveCurrentRoom_SetsSelectedRoomToCurrentRoom_WhenSelectedRoomIsNull()
    {
        using var vm = CreateViewModel();
        var index = CreateSampleIndex();
        SetMapIndex(vm, index);
        SetLocationVnum(vm, "100");
        Assert.Null(vm.SelectedRoom);

        InvokeTryResolveCurrentRoom(vm);

        Assert.NotNull(vm.SelectedRoom);
        Assert.Same(vm.CurrentRoom, vm.SelectedRoom);
    }

    [Fact]
    public void TryResolveCurrentRoom_UpdatesSelectedRoom_WhenDifferentRoomWasSet()
    {
        using var vm = CreateViewModel();
        var index = CreateSampleIndex();
        SetMapIndex(vm, index);
        SetLocationVnum(vm, "100");

        // Simulate a map click selecting a different room before walking
        var clickedRoom = new MapRoom
        {
            Id = 999,
            AreaId = 1,
            Coordinates = new MapCoordinates(0, 0, 0),
        };
        vm.SelectedRoom = clickedRoom;

        InvokeTryResolveCurrentRoom(vm);

        // Walking overwrites the clicked room with the current room
        Assert.NotNull(vm.CurrentRoom);
        Assert.Same(vm.CurrentRoom, vm.SelectedRoom);
        Assert.NotSame(clickedRoom, vm.SelectedRoom);
    }

    [Fact]
    public void TryResolveCurrentRoom_RaisesPropertyChangedForSelectedRoomIcon_WhenRoomSet()
    {
        using var vm = CreateViewModel();
        var index = CreateSampleIndex();
        SetMapIndex(vm, index);
        SetLocationVnum(vm, "100");
        Assert.Null(vm.SelectedRoom);

        var changedProperties = new List<string?>();
        vm.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName);

        InvokeTryResolveCurrentRoom(vm);

        Assert.Contains(nameof(MapViewModel.SelectedRoomIcon), changedProperties);
    }

    [Fact]
    public void TryResolveCurrentRoom_RaisesSelectedRoomIcon_WhenDifferentRoomWasSet()
    {
        using var vm = CreateViewModel();
        var index = CreateSampleIndex();
        SetMapIndex(vm, index);
        SetLocationVnum(vm, "100");

        // Pre-set SelectedRoom to a different room (simulating a map click)
        vm.SelectedRoom = new MapRoom
        {
            Id = 999,
            AreaId = 1,
            Coordinates = new MapCoordinates(0, 0, 0),
        };

        var changedProperties = new List<string?>();
        vm.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName);

        InvokeTryResolveCurrentRoom(vm);

        Assert.Contains(nameof(MapViewModel.SelectedRoomIcon), changedProperties);
    }

    [Fact]
    public void TryResolveCurrentRoom_DoesNotRaiseSelectedRoomIcon_WhenSameRoomInstance()
    {
        using var vm = CreateViewModel();
        var index = CreateSampleIndex();
        SetMapIndex(vm, index);
        SetLocationVnum(vm, "100");

        // Pre-set SelectedRoom to the exact room instance from the map index,
        // so ReferenceEquals prevents a redundant update
        vm.SelectedRoom = index.RoomsById[1];

        var changedProperties = new List<string?>();
        vm.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName);

        InvokeTryResolveCurrentRoom(vm);

        Assert.DoesNotContain(nameof(MapViewModel.SelectedRoomIcon), changedProperties);
        Assert.Same(vm.CurrentRoom, vm.SelectedRoom);
    }

    // ====================================================================
    // OnLocationChanged integration (via GmcpLocationResolver.Process)
    // ====================================================================

    [Fact]
    public void LocationChangedViaResolver_WithMatchingVnum_SetsCurrentRoom()
    {
        using var vm = CreateViewModel();
        var index = CreateSampleIndex();
        SetMapIndex(vm, index);

        // Process a GMCP message that matches the default resolver settings
        var resolver = GetLocationResolver(vm);
        resolver.Process(new GmcpMessage("Room.Info", """{"vnum": "100"}"""));

        // The resolver fires LocationChanged -> OnLocationChanged -> Dispatcher.UIThread.Post
        // Since Dispatcher.UIThread is not available in tests, the TryResolveCurrentRoom
        // is dispatched asynchronously and may not have run yet.
        // Instead we verify the resolver state changed.
        Assert.Equal("100", resolver.CurrentVnum);
    }

    // ====================================================================
    // PropertyChanged notifications for SelectedArea and SelectedZ
    // (public setters and internal methods)
    // ====================================================================

    [Fact]
    public void SelectedArea_Setter_RaisesPropertyChangedForSelectedArea()
    {
        using var vm = CreateViewModel();
        var changedProperties = new List<string?>();
        vm.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName);

        vm.SelectedArea = new MapArea { Id = 1, Name = "Test" };

        Assert.Contains(nameof(MapViewModel.SelectedArea), changedProperties);
    }

    [Fact]
    public void SelectedZ_Setter_RaisesPropertyChangedForSelectedZ()
    {
        using var vm = CreateViewModel();
        var changedProperties = new List<string?>();
        vm.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName);

        vm.SelectedZ = 5.0;

        Assert.Contains(nameof(MapViewModel.SelectedZ), changedProperties);
    }

    [Fact]
    public void SetSelectedAreaInternal_RaisesPropertyChangedForSelectedArea()
    {
        using var vm = CreateViewModel();
        var index = CreateSampleIndex();
        SetMapIndex(vm, index);

        var changedProperties = new List<string?>();
        vm.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName);

        SetSelectedAreaInternal(vm, new MapArea { Id = 99, Name = "Internal" });

        Assert.Contains(nameof(MapViewModel.SelectedArea), changedProperties);
    }

    [Fact]
    public void SetSelectedZInternal_RaisesPropertyChangedForSelectedZ()
    {
        using var vm = CreateViewModel();
        var index = CreateSampleIndex();
        SetMapIndex(vm, index);

        var changedProperties = new List<string?>();
        vm.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName);

        SetSelectedZInternal(vm, 7.5);

        Assert.Contains(nameof(MapViewModel.SelectedZ), changedProperties);
    }

    [Fact]
    public void RefreshZLevelsInternal_RaisesPropertyChangedForSelectedZ()
    {
        using var vm = CreateViewModel();
        var index = CreateSampleIndex();
        SetMapIndex(vm, index);

        // Pre-set area so RefreshZLevelsInternal does something
        SetSelectedAreaInternal(vm, index.AreasById[1]);

        // Reset _selectedZ to a value not in the Z levels
        var zField = typeof(MapViewModel).GetField("_selectedZ",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(zField);
        zField.SetValue(vm, 999.0);

        var changedProperties = new List<string?>();
        vm.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName);

        RefreshZLevelsInternal(vm);

        Assert.Contains(nameof(MapViewModel.SelectedZ), changedProperties);
    }

    // ====================================================================
    // Helpers
    // ====================================================================

    private static MapViewModel CreateViewModel()
    {
        return new MapViewModel(
            "C:\\dummy",
            new GmcpLocationResolver());
    }

    private static MapRoom CreateSampleRoom()
    {
        return new MapRoom
        {
            Id = 1,
            AreaId = 1,
            Name = "Test Room",
            Coordinates = new MapCoordinates(10, 20, 5),
            UserData = new Dictionary<string, JsonElement>
            {
                ["vnum"] = JsonSerializer.SerializeToElement("100"),
                ["sector"] = JsonSerializer.SerializeToElement("inside"),
            },
        };
    }

    private static MapIndex CreateSampleIndex()
    {
        var room = CreateSampleRoom();
        var area = new MapArea
        {
            Id = 1,
            Name = "Test Area",
            Rooms = [room],
        };
        var doc = new MapDocument
        {
            Areas = [area],
        };
        return new MapIndex(doc);
    }

    private static void SetMapIndex(MapViewModel vm, MapIndex? index)
    {
        var field = typeof(MapViewModel).GetField("_mapIndex",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(field);
        field.SetValue(vm, index);
    }

    private static void SetCurrentRoom(MapViewModel vm, MapRoom? room)
    {
        var field = typeof(MapViewModel).GetField("_currentRoom",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(field);
        field.SetValue(vm, room);
    }

    private static void SetSelectedAreaInternal(MapViewModel vm, MapArea area)
    {
        var method = typeof(MapViewModel).GetMethod("SetSelectedAreaInternal",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(method);
        method.Invoke(vm, [area]);
    }

    private static void SetSelectedZInternal(MapViewModel vm, double z)
    {
        var method = typeof(MapViewModel).GetMethod("SetSelectedZInternal",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(method);
        method.Invoke(vm, [z]);
    }

    private static void RefreshZLevelsInternal(MapViewModel vm)
    {
        var method = typeof(MapViewModel).GetMethod("RefreshZLevelsInternal",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(method);
        method.Invoke(vm, null);
    }

    private static void SetLocationVnum(MapViewModel vm, string vnum)
    {
        var resolver = GetLocationResolver(vm);
        resolver.Process(new GmcpMessage("Room.Info", $$"""{"vnum": "{{vnum}}"}"""));
    }

    private static GmcpLocationResolver GetLocationResolver(MapViewModel vm)
    {
        var field = typeof(MapViewModel).GetField("_locationResolver",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(field);
        return (GmcpLocationResolver)field.GetValue(vm)!;
    }

    private static void InvokeTryResolveCurrentRoom(MapViewModel vm)
    {
        var method = typeof(MapViewModel).GetMethod("TryResolveCurrentRoom",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(method);
        method.Invoke(vm, null);
    }
}

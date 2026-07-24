using System.Collections.ObjectModel;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MudClient.App.Controls;
using MudClient.App.Services;
using MudClient.Core.Gmcp;
using MudClient.Core.Map;

namespace MudClient.App.ViewModels;

public sealed class MapViewModel : ObservableObject, IDisposable, IAsyncDisposable
{
    private readonly string _packagedMapDirectory;
    private readonly ContentPathResolver? _contentPaths;
    private readonly string? _mapEditorPath;
    private readonly MapEditorRecoveryStore? _mapEditorRecoveryStore;
    private readonly TimeSpan _mapMovementTimeout;
    private string _baseWorldMapPath = string.Empty;
    private string _worldMapPath = string.Empty;
    private string _mapSettingsPath = string.Empty;
    private string _sectorDirectory = string.Empty;
    private string _sectorManifestPath = string.Empty;
    private string _roomImageDirectory = string.Empty;
    private readonly GmcpLocationResolver _locationResolver;

    private MapIndex? _mapIndex;
    private SectorTextureCache? _textureCache;
    private RoomImageCache? _roomImages;
    private MapSettings _settings = MapSettings.CreateDefault();
    private CancellationTokenSource? _loadCancellation;
    private CancellationTokenSource? _mapMovementTimeoutCancellation;
    private Task _mapMovementTimeoutTask = Task.CompletedTask;

    private bool _isLoading;
    private string? _errorMessage;
    private string _statusMessage = "Mapa nie została jeszcze załadowana.";

    private MapArea? _selectedArea;
    private double _selectedZ;
    private MapRoom? _currentRoom;
    private MapRoom? _selectedRoom;
    private IReadOnlyList<MapRoom>? _routeRooms;
    private IReadOnlyList<CharacterGroupMember> _groupMembers = [];
    private IReadOnlyList<GroupMapMarker> _groupMarkers = [];
    private string? _currentSectorName;
    private bool _followPlayer = true;
    private bool _lordModeEnabled;
    private bool _showGroupMembersAsNumbers;
    private bool _isUsingWorkingMap;
    private bool _isUsingRecoveryMap;
    private string _newMapAreaName = string.Empty;
    private bool _moveExistingRoomsToNewArea;
    private MapDisplayModeOption _selectedDisplayMode;
    private readonly RelayCommand _lordGotoSelectedRoomCommand;
    private readonly RelayCommand _startMapEditorCommand;
    private readonly RelayCommand _stopMapEditorCommand;
    private readonly RelayCommand _undoMapEditorCommand;
    private readonly RelayCommand _redoMapEditorCommand;
    private readonly RelayCommand _createMapAreaCommand;
    private readonly AsyncRelayCommand _saveMapEditorCommand;
    private MapEditorSession? _mapEditor;

    public MapViewModel(
        string appBaseDirectory,
        GmcpLocationResolver locationResolver,
        string? dataRoot = null,
        TimeSpan? mapMovementTimeout = null)
    {
        _packagedMapDirectory = Path.Combine(appBaseDirectory, "Assets", "Map");
        _contentPaths = string.IsNullOrWhiteSpace(dataRoot) ? null : new ContentPathResolver(dataRoot);
        if (!string.IsNullOrWhiteSpace(dataRoot))
        {
            var editorDirectory = Path.Combine(dataRoot, "MapEditor");
            _mapEditorPath = Path.Combine(editorDirectory, "world-map.json");
            _mapEditorRecoveryStore = new MapEditorRecoveryStore(editorDirectory);
        }
        _mapMovementTimeout = mapMovementTimeout is { } timeout && timeout > TimeSpan.Zero
            ? timeout
            : TimeSpan.FromSeconds(8);
        SetMapPaths(_packagedMapDirectory);

        _selectedDisplayMode = MapDisplayModeOption.All[0];
        _locationResolver = locationResolver;
        _locationResolver.LocationChanged += OnLocationChanged;

        ReloadCommand = new AsyncRelayCommand(InitializeAsync);
        CenterCommand = new RelayCommand(RequestCenterOnCurrentRoom);
        _lordGotoSelectedRoomCommand = new RelayCommand(
            RequestLordGotoSelectedRoom,
            CanLordGotoSelectedRoom);
        _startMapEditorCommand = new RelayCommand(StartMapEditor, CanStartMapEditor);
        _stopMapEditorCommand = new RelayCommand(StopMapEditor, () => IsMapEditorActive);
        _undoMapEditorCommand = new RelayCommand(UndoMapEditor, () => _mapEditor?.CanUndo == true);
        _redoMapEditorCommand = new RelayCommand(RedoMapEditor, () => _mapEditor?.CanRedo == true);
        _createMapAreaCommand = new RelayCommand(CreateMapAreaFromInput, CanCreateMapAreaFromInput);
        _saveMapEditorCommand = new AsyncRelayCommand(SaveMapEditorAsync, () => _mapEditor?.IsDirty == true);
    }

    public event Action? CenterOnCurrentRoomRequested;

    public event Action<MapRoom>? CenterOnRoomRequested;

    /// <summary>Raised by the view when the user double-clicks a room on the map.</summary>
    public event Action<MapRoom>? RoomDoubleClicked;

    public event Action<MapRoom>? LordGotoRequested;

    public event Action<bool>? LordModeChanged;

    public event Action<bool>? GroupMarkerDisplayChanged;

    public event Action<bool>? MapEditorActiveChanged;

    public ObservableCollection<MapArea> Areas { get; } = [];

    public ObservableCollection<double> ZLevels { get; } = [];

    public IAsyncRelayCommand ReloadCommand { get; }

    public IRelayCommand CenterCommand { get; }

    public IRelayCommand LordGotoSelectedRoomCommand => _lordGotoSelectedRoomCommand;

    public IRelayCommand StartMapEditorCommand => _startMapEditorCommand;

    public IRelayCommand StopMapEditorCommand => _stopMapEditorCommand;

    public IRelayCommand UndoMapEditorCommand => _undoMapEditorCommand;

    public IRelayCommand RedoMapEditorCommand => _redoMapEditorCommand;

    public IRelayCommand CreateMapAreaCommand => _createMapAreaCommand;

    public IAsyncRelayCommand SaveMapEditorCommand => _saveMapEditorCommand;

    public string NewMapAreaName
    {
        get => _newMapAreaName;
        set
        {
            if (SetProperty(ref _newMapAreaName, value))
            {
                _createMapAreaCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public bool MoveExistingRoomsToNewArea
    {
        get => _moveExistingRoomsToNewArea;
        set => SetMoveExistingRoomsToNewArea(value);
    }

    public bool CanMoveExistingRoomsToNewArea =>
        _mapEditor is not null && SelectedArea is not null && !IsMapEditorActive;

    public MapIndex? MapIndex
    {
        get => _mapIndex;
        private set
        {
            if (SetProperty(ref _mapIndex, value))
            {
                RefreshGroupMarkers();
            }
        }
    }

    public SectorTextureCache? TextureCache
    {
        get => _textureCache;
        private set
        {
            if (SetProperty(ref _textureCache, value))
            {
                OnPropertyChanged(nameof(SelectedRoomIcon));
            }
        }
    }

    public RoomImageCache? RoomImages
    {
        get => _roomImages;
        private set
        {
            if (SetProperty(ref _roomImages, value))
            {
                OnPropertyChanged(nameof(SelectedRoomIcon));
            }
        }
    }

    public MapSettings Settings
    {
        get => _settings;
        private set => SetProperty(ref _settings, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public MapArea? SelectedArea
    {
        get => _selectedArea;
        set
        {
            if (SetProperty(ref _selectedArea, value) && value is not null)
            {
                if (MoveExistingRoomsToNewArea && !IsMapEditorActive)
                {
                    _mapEditor?.SetMoveKnownRoomsToTargetArea(true, value.Id);
                }
                FollowPlayer = false;
                RefreshZLevels();
                FocusFirstRoom(value);
                OnPropertyChanged(nameof(CanMoveExistingRoomsToNewArea));
            }
        }
    }

    public double SelectedZ
    {
        get => _selectedZ;
        set
        {
            if (SetProperty(ref _selectedZ, value))
            {
                OnPropertyChanged(nameof(SelectedZIndex));
                FollowPlayer = false;
            }
        }
    }

    /// <summary>
    /// Index projection for the Z-level ComboBox. Avalonia temporarily selects -1 while
    /// the level list is rebuilt after an area change; unlike SelectedItem, that transition
    /// does not require converting null to <see cref="double"/>.
    /// </summary>
    public int SelectedZIndex
    {
        get => ZLevels.IndexOf(SelectedZ);
        set
        {
            if (value >= 0 && value < ZLevels.Count)
            {
                SelectedZ = ZLevels[value];
            }
        }
    }

    public MapRoom? CurrentRoom
    {
        get => _currentRoom;
        private set => SetProperty(ref _currentRoom, value);
    }

    public MapRoom? SelectedRoom
    {
        get => _selectedRoom;
        set
        {
            if (SetProperty(ref _selectedRoom, value))
            {
                OnPropertyChanged(nameof(SelectedRoomIcon));
                OnPropertyChanged(nameof(LordGotoMenuHeader));
                _lordGotoSelectedRoomCommand.NotifyCanExecuteChanged();
            }
        }
    }

    /// <summary>Autowalk route to paint on the map, or null when idle.</summary>
    public IReadOnlyList<MapRoom>? RouteRooms
    {
        get => _routeRooms;
        set => SetProperty(ref _routeRooms, value);
    }

    public void NotifyRoomDoubleClicked(MapRoom room) => RoomDoubleClicked?.Invoke(room);

    public bool LordModeEnabled
    {
        get => _lordModeEnabled;
        set
        {
            if (!SetProperty(ref _lordModeEnabled, value))
            {
                return;
            }

            _lordGotoSelectedRoomCommand.NotifyCanExecuteChanged();
            if (!value && IsMapEditorActive)
            {
                StopMapEditor();
            }

            NotifyMapEditorCommands();
            LordModeChanged?.Invoke(value);
        }
    }

    public bool ShowGroupMembersAsNumbers
    {
        get => _showGroupMembersAsNumbers;
        set
        {
            if (SetProperty(ref _showGroupMembersAsNumbers, value))
            {
                GroupMarkerDisplayChanged?.Invoke(value);
            }
        }
    }

    public string LordGotoMenuHeader => SelectedRoom is { } room
        ? $"Goto: {(string.IsNullOrWhiteSpace(room.Name) ? "pokój" : room.Name)} [{room.Vnum ?? "brak vnum"}]"
        : "Goto";

    private bool CanLordGotoSelectedRoom() =>
        LordModeEnabled && IsSafeVnum(SelectedRoom?.Vnum);

    private void RequestLordGotoSelectedRoom()
    {
        if (SelectedRoom is { } room && CanLordGotoSelectedRoom())
        {
            LordGotoRequested?.Invoke(room);
        }
    }

    private static bool IsSafeVnum(string? vnum) =>
        !string.IsNullOrWhiteSpace(vnum) && vnum.All(char.IsAsciiDigit);

    public Bitmap? SelectedRoomIcon =>
        RoomImages?.GetFullImage(SelectedRoom?.Vnum)
        ?? TextureCache?.GetTexture(SelectedRoom?.Sector ?? string.Empty);

    public string? CurrentVnum => _locationResolver.CurrentVnum;

    public string CurrentRoomName => CurrentRoom?.Name ?? "(brak)";

    public string CurrentSectorName => _currentSectorName ?? "(brak)";

    public bool IsMapEditorActive => _mapEditor?.IsMapping == true;

    public bool IsMapEditorDirty => _mapEditor?.IsDirty == true;

    public bool IsMapEditorAwaitingRoomInfo => _mapEditor?.IsAwaitingRoomInfo == true;

    public int MapEditorStep => _mapEditor?.Step ?? 2;

    public string MapEditorStatus => _mapEditor?.Status ?? "Edytor mapy nie jest jeszcze gotowy.";

    public bool IsUsingWorkingMap
    {
        get => _isUsingWorkingMap;
        private set
        {
            if (SetProperty(ref _isUsingWorkingMap, value))
            {
                OnPropertyChanged(nameof(MapEditorSourceDescription));
            }
        }
    }

    public string MapEditorSourceDescription => IsUsingWorkingMap
        ? IsUsingRecoveryMap
            ? "Źródło: odzyskane niezapisane zmiany mapy roboczej."
            : "Źródło: mapa robocza z katalogu MapEditor."
        : IsUsingRecoveryMap
            ? "Źródło: odzyskane niezapisane zmiany mapy bazowej."
            : "Źródło: aktualna mapa bazowa.";

    public bool IsUsingRecoveryMap
    {
        get => _isUsingRecoveryMap;
        private set
        {
            if (SetProperty(ref _isUsingRecoveryMap, value))
            {
                OnPropertyChanged(nameof(MapEditorSourceDescription));
            }
        }
    }

    public bool FollowPlayer
    {
        get => _followPlayer;
        set => SetProperty(ref _followPlayer, value);
    }

    /// <summary>Group members whose GMCP room can be resolved on the loaded map.</summary>
    public IReadOnlyList<GroupMapMarker> GroupMarkers
    {
        get => _groupMarkers;
        private set => SetProperty(ref _groupMarkers, value);
    }

    public void UpdateGroupMembers(IEnumerable<CharacterGroupMember> members, string? selfName)
    {
        ArgumentNullException.ThrowIfNull(members);

        _groupMembers = members
            .Where(member => !string.Equals(member.Name, selfName, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        RefreshGroupMarkers();
    }

    private void RefreshGroupMarkers()
    {
        if (MapIndex is null)
        {
            GroupMarkers = [];
            return;
        }

        GroupMarkers = _groupMembers
            .Select((member, index) => (Member: member, Number: index + 1,
                Room: string.IsNullOrWhiteSpace(member.Room)
                ? null
                : MapIndex.FindFirstRoomByVnum(member.Room)))
            .Where(item => item.Room is not null)
            .Select(item => new GroupMapMarker(
                item.Member.Name,
                item.Member.IsLeader,
                item.Room!,
                item.Number))
            .ToArray();
    }

    public IReadOnlyList<MapDisplayModeOption> DisplayModes { get; } = MapDisplayModeOption.All;

    public MapDisplayModeOption SelectedDisplayMode
    {
        get => _selectedDisplayMode;
        set => SetProperty(ref _selectedDisplayMode, value);
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        _loadCancellation?.Cancel();
        var cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _loadCancellation = cancellation;

        IsLoading = true;
        ErrorMessage = null;
        StatusMessage = "Ładowanie mapy…";

        try
        {
            var downloadedMapDirectory = _contentPaths?.GetActiveDirectory("map");
            if (downloadedMapDirectory is not null)
            {
                try
                {
                    _ = await new MapLoader().LoadAsync(
                            Path.Combine(downloadedMapDirectory, "world-map.json"),
                            cancellation.Token)
                        .ConfigureAwait(false);
                }
                catch (Exception exception) when (exception is MapLoadException
                    or IOException
                    or UnauthorizedAccessException)
                {
                    // A damaged downloaded map must not hide the packaged fallback.
                    System.Diagnostics.Trace.WriteLine(exception);
                    downloadedMapDirectory = null;
                }
            }

            SetMapPaths(downloadedMapDirectory ?? _packagedMapDirectory);
            var settingsLoader = new MapSettingsLoader();
            Settings = await settingsLoader.LoadAsync(_mapSettingsPath, cancellation.Token).ConfigureAwait(false);

            TextureCache?.Dispose();
            TextureCache = new SectorTextureCache(_sectorDirectory, _sectorManifestPath);

            RoomImages?.Dispose();
            RoomImages = new RoomImageCache(_roomImageDirectory);

            var mapPathToLoad = _baseWorldMapPath;
            var useWorkingMap = false;
            if (_mapEditorPath is not null && File.Exists(_mapEditorPath))
            {
                try
                {
                    _ = await new MapLoader().LoadAsync(_mapEditorPath, cancellation.Token).ConfigureAwait(false);
                    mapPathToLoad = _mapEditorPath;
                    useWorkingMap = true;
                }
                catch (Exception exception) when (exception is MapLoadException
                    or IOException
                    or UnauthorizedAccessException)
                {
                    // A damaged optional working map must not hide the current content map.
                    System.Diagnostics.Trace.WriteLine(exception);
                }
            }

            _worldMapPath = mapPathToLoad;

            if (!File.Exists(_worldMapPath))
            {
                ErrorMessage = $"Nie znaleziono pliku mapy: {_worldMapPath}";
                StatusMessage = "Brak pliku mapy.";
                return;
            }

            var loader = new MapLoader();
            var result = await loader.LoadAsync(_worldMapPath, cancellation.Token).ConfigureAwait(false);
            var recovery = _mapEditorRecoveryStore is null
                ? null
                : await _mapEditorRecoveryStore.LoadAsync(cancellation.Token).ConfigureAwait(false);
            var baselineIdentity = GetMapBaselineIdentity();
            var recoveryMatchesBaseline = recovery is not null &&
                                          string.Equals(
                                              recovery.BaselineIdentity,
                                              baselineIdentity,
                                              StringComparison.OrdinalIgnoreCase);
            var recoveredDirtyMap = recovery?.IsDirty == true;
            var editorDocument = recoveredDirtyMap ? recovery!.Current! : result.Document;
            var undoHistory = recovery is not null && (recoveredDirtyMap || recoveryMatchesBaseline)
                ? recovery.UndoHistory
                : [];
            _mapEditor = new MapEditorSession(editorDocument, undoHistory, recoveredDirtyMap);
            _moveExistingRoomsToNewArea = false;

            var index = new MapIndex(editorDocument, Settings.SpatialBucketSize);

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                MapIndex = index;
                Areas.Clear();
                foreach (var area in index.Document.Areas)
                {
                    Areas.Add(area);
                }

                var defaultArea = Areas.FirstOrDefault();
                if (defaultArea is not null)
                {
                    SetSelectedAreaInternal(defaultArea);
                }

                var roomCount = index.RoomsById.Count;
                var warningSuffix = result.Warnings.Count > 0
                    ? $" ({result.Warnings.Count} ostrzeżeń pominiętych pokoi)"
                    : string.Empty;

                StatusMessage = $"Załadowano {index.Document.Areas.Count} obszarów, {roomCount} pokoi{warningSuffix}.";
                IsUsingWorkingMap = useWorkingMap;
                IsUsingRecoveryMap = recoveredDirtyMap;
                OnPropertyChanged(nameof(MoveExistingRoomsToNewArea));
                NotifyMapEditorStateChanged();
            });

            TryResolveCurrentRoom();
        }
        catch (MapLoadException exception)
        {
            ErrorMessage = exception.Message;
            StatusMessage = "Błąd ładowania mapy.";
            System.Diagnostics.Trace.WriteLine(exception);
        }
        catch (OperationCanceledException)
        {
            // Load was superseded by a newer request.
        }
        catch (Exception exception)
        {
            ErrorMessage = "Nieoczekiwany błąd podczas ładowania mapy.";
            StatusMessage = ErrorMessage;
            System.Diagnostics.Trace.WriteLine(exception);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void RefreshZLevels()
    {
        ZLevels.Clear();

        if (SelectedArea is null || MapIndex is null)
        {
            return;
        }

        foreach (var z in MapIndex.GetZLevels(SelectedArea.Id))
        {
            ZLevels.Add(z);
        }

        if (ZLevels.Count > 0 && !ZLevels.Contains(SelectedZ))
        {
            SelectedZ = ZLevels[0];
        }

        OnPropertyChanged(nameof(SelectedZIndex));
    }

    /// <summary>
    /// Refreshes Z levels and auto-selects a valid default Z without using
    /// the public <see cref="SelectedZ"/> setter, so <see cref="FollowPlayer"/>
    /// is not affected. Used during automatic (programmatic) area/Z updates.
    /// </summary>
    private void RefreshZLevelsInternal()
    {
        ZLevels.Clear();

        if (SelectedArea is null || MapIndex is null)
        {
            return;
        }

        foreach (var z in MapIndex.GetZLevels(SelectedArea.Id))
        {
            ZLevels.Add(z);
        }

        if (ZLevels.Count > 0 && !ZLevels.Contains(_selectedZ))
        {
            _selectedZ = ZLevels[0];
            OnPropertyChanged(nameof(SelectedZ));
        }

        OnPropertyChanged(nameof(SelectedZIndex));
    }

    private void SetMapPaths(string mapDirectory)
    {
        _baseWorldMapPath = Path.Combine(mapDirectory, "world-map.json");
        _worldMapPath = _baseWorldMapPath;
        _mapSettingsPath = Path.Combine(mapDirectory, "map-settings.json");
        _sectorDirectory = Path.Combine(mapDirectory, "Sectors");
        _sectorManifestPath = Path.Combine(_sectorDirectory, "sectors.json");
        _roomImageDirectory = Path.Combine(mapDirectory, "Rooms");
    }

    /// <summary>
    /// Sets the selected area without disabling follow-player mode.
    /// Use only for programmatic updates driven by current-room resolution or centering.
    /// </summary>
    private void SetSelectedAreaInternal(MapArea area)
    {
        if (SetProperty(ref _selectedArea, area, nameof(SelectedArea)) && area is not null)
        {
            RefreshZLevelsInternal();
        }
    }

    /// <summary>
    /// Sets the selected Z without disabling follow-player mode.
    /// Use only for programmatic updates driven by current-room resolution or centering.
    /// </summary>
    private void SetSelectedZInternal(double z)
    {
        if (SetProperty(ref _selectedZ, z, nameof(SelectedZ)))
        {
            OnPropertyChanged(nameof(SelectedZIndex));
        }
    }

    private void FocusFirstRoom(MapArea area)
    {
        var room = area.Rooms.FirstOrDefault();
        if (room is null)
        {
            return;
        }

        SetSelectedZInternal(room.Coordinates.Z);
        SelectedRoom = room;
        CenterOnRoomRequested?.Invoke(room);
    }

    private void OnLocationChanged(string vnum)
    {
        Dispatcher.UIThread.Post(() =>
        {
            OnPropertyChanged(nameof(CurrentVnum));
            TryResolveCurrentRoom();
            NotifyMapEditorCommands();
        });
    }

    private void TryResolveCurrentRoom()
    {
        if (MapIndex is null)
        {
            return;
        }

        var vnum = _locationResolver.CurrentVnum;
        if (vnum is null)
        {
            return;
        }

        var room = MapIndex.FindFirstRoomByVnum(vnum);

        if (room is null)
        {
            CurrentRoom = null;
            _currentSectorName = null;
            OnPropertyChanged(nameof(CurrentRoomName));
            OnPropertyChanged(nameof(CurrentSectorName));
            StatusMessage = $"VNUM {vnum} nie istnieje w mapie.";
            return;
        }

        CurrentRoom = room;
        _currentSectorName = room.Sector;
        OnPropertyChanged(nameof(CurrentRoomName));
        OnPropertyChanged(nameof(CurrentSectorName));

        var area = MapIndex.AreasById.GetValueOrDefault(room.AreaId);
        if (area is not null && !ReferenceEquals(SelectedArea, area))
        {
            SetSelectedAreaInternal(area);
        }

        SetSelectedZInternal(room.Coordinates.Z);

        // Keep the details panel and icon in sync with the current room during
        // walking. Clicking a room on the map still sets SelectedRoom through
        // OnRoomClicked, but the next GMCP walking update switches the
        // selection/image back to the current room.
        if (!ReferenceEquals(SelectedRoom, room))
        {
            SelectedRoom = room;
        }

        FollowPlayer = true;
        CenterOnCurrentRoomRequested?.Invoke();
        NotifyMapEditorCommands();
    }

    public void CenterOnPlayer()
    {
        if (CurrentRoom is null)
        {
            return;
        }

        var area = MapIndex?.AreasById.GetValueOrDefault(CurrentRoom.AreaId);
        if (area is not null)
        {
            SetSelectedAreaInternal(area);
        }

        SetSelectedZInternal(CurrentRoom.Coordinates.Z);

        FollowPlayer = true;
        CenterOnCurrentRoomRequested?.Invoke();
    }

    /// <summary>
    /// Selects and centers a mapped room without enabling follow-player mode.
    /// Returns null when the vnum is not present in the loaded map.
    /// </summary>
    public MapRoom? FocusRoomByVnum(string vnum)
    {
        if (MapIndex?.FindFirstRoomByVnum(vnum) is not { } room)
        {
            return null;
        }

        if (MapIndex.AreasById.GetValueOrDefault(room.AreaId) is { } area)
        {
            SetSelectedAreaInternal(area);
        }

        SetSelectedZInternal(room.Coordinates.Z);
        SelectedRoom = room;
        FollowPlayer = false;
        CenterOnRoomRequested?.Invoke(room);
        return room;
    }

    public MapEditorCommandDecision PrepareMapEditorCommand(string command)
    {
        if (_mapEditor is null)
        {
            return new MapEditorCommandDecision(true, command);
        }

        var decision = _mapEditor.PrepareManualCommand(command);
        if (decision.Allow && _mapEditor.IsAwaitingRoomInfo)
        {
            StartMapMovementTimeout();
        }
        NotifyMapEditorStateChanged();
        return decision;
    }

    public bool SetMapEditorStep(int step)
    {
        if (_mapEditor is null)
        {
            return false;
        }

        var result = _mapEditor.SetStep(step);
        NotifyMapEditorStateChanged();
        return result;
    }

    public bool CreateMapArea(string name)
    {
        if (_mapEditor?.CreateArea(name) != true)
        {
            NotifyMapEditorStateChanged();
            return false;
        }

        ApplyMapEditorDocument();
        if (Areas.LastOrDefault(area => string.Equals(area.Name, name.Trim(), StringComparison.OrdinalIgnoreCase)) is { } area)
        {
            SetSelectedAreaInternal(area);
        }

        NotifyMapEditorStateChanged();
        return true;
    }

    public bool SetMoveExistingRoomsToNewArea(bool enabled)
    {
        if (_mapEditor?.SetMoveKnownRoomsToTargetArea(enabled, SelectedArea?.Id) != true)
        {
            NotifyMapEditorStateChanged();
            return false;
        }

        SetProperty(ref _moveExistingRoomsToNewArea, enabled, nameof(MoveExistingRoomsToNewArea));
        NotifyMapEditorStateChanged();
        return true;
    }

    private bool CanCreateMapAreaFromInput() =>
        LordModeEnabled
        && _mapEditor is not null
        && !IsMapEditorActive
        && !string.IsNullOrWhiteSpace(NewMapAreaName);

    private void CreateMapAreaFromInput()
    {
        if (CreateMapArea(NewMapAreaName))
        {
            NewMapAreaName = string.Empty;
        }
    }

    public bool SetCurrentMapRoomSymbol(string symbol) => ApplyMapEditorOperation(
        editor => editor.SetCurrentRoomSymbol(symbol));

    public bool AddCurrentMapLabel(string text) => ApplyMapEditorOperation(
        editor => editor.AddLabel(text));

    public IReadOnlyList<MapLabel> ShowCurrentAreaMapLabels()
    {
        var labels = _mapEditor?.ShowCurrentAreaLabels() ?? [];
        NotifyMapEditorStateChanged();
        return labels;
    }

    public bool SetMapLabelText(int id, string text) => ApplyMapEditorOperation(
        editor => editor.SetLabelText(id, text));

    public bool RemoveMapLabel(int id) => ApplyMapEditorOperation(
        editor => editor.RemoveLabel(id));

    public bool SetCurrentMapRoomName(string name) => ApplyMapEditorOperation(
        editor => editor.SetCurrentRoomName(name));

    public bool SetCurrentMapRoomSector(string sector) => ApplyMapEditorOperation(
        editor => editor.SetCurrentRoomSector(sector));

    public bool SetCurrentMapRoomWeight(double weight) => ApplyMapEditorOperation(
        editor => editor.SetCurrentRoomWeight(weight));

    public bool MoveCurrentMapRoom(MapCoordinates coordinates) => ApplyMapEditorOperation(
        editor => editor.MoveCurrentRoom(coordinates));

    public bool ForgetCurrentMapRoom()
    {
        var wasActive = IsMapEditorActive;
        var changed = ApplyMapEditorOperation(editor => editor.ForgetCurrentRoom());
        if (changed && wasActive)
        {
            MapEditorActiveChanged?.Invoke(false);
        }

        return changed;
    }

    public bool RemoveMapSpecialExit(string direction) => ApplyMapEditorOperation(
        editor => editor.RemoveSpecialExit(direction));

    public MapEditorCommandDecision PrepareMapSpecialMovement(string direction, string command)
    {
        var decision = _mapEditor?.PrepareSpecialMovement(direction, command)
                       ?? new MapEditorCommandDecision(false, command, "Edytor mapy nie jest gotowy.");
        if (decision.Allow && _mapEditor?.IsAwaitingRoomInfo == true)
        {
            StartMapMovementTimeout();
        }
        NotifyMapEditorStateChanged();
        return decision;
    }

    public void CancelPendingMapMovement(string message)
    {
        CancelMapMovementTimeout();
        _mapEditor?.CancelPendingMovement(message);
        NotifyMapEditorStateChanged();
    }

    public bool CancelMapEditorChanges()
    {
        CancelMapMovementTimeout();
        var changed = _mapEditor?.CancelChanges() == true;
        if (changed)
        {
            ApplyMapEditorDocument();
        }

        NotifyMapEditorStateChanged();
        return changed;
    }

    public bool ResolveMapConflictKeepMap() => ApplyMapEditorOperation(
        editor => editor.ResolveConflictKeepMap(),
        applyDocument: false);

    public bool ResolveMapConflictUseGmcp() => ApplyMapEditorOperation(
        editor => editor.ResolveConflictUseGmcp());

    public async Task<string> GetMapEditorDiffAsync(CancellationToken cancellationToken = default)
    {
        if (_mapEditor is null || !File.Exists(_baseWorldMapPath))
        {
            return "Nie można porównać mapy: brak edytora albo mapy bazowej.";
        }

        try
        {
            var baseline = await new MapLoader().LoadAsync(_baseWorldMapPath, cancellationToken)
                .ConfigureAwait(false);
            return MapDocumentDiffer.Compare(baseline.Document, _mapEditor.Document).ToPolishSummary();
        }
        catch (Exception exception) when (exception is MapLoadException or IOException or UnauthorizedAccessException)
        {
            return $"Nie udało się porównać mapy: {exception.Message}";
        }
    }

    public async Task<string> ExportMapEditorAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        if (_mapEditor is null)
        {
            return "Edytor mapy nie jest gotowy.";
        }

        if (string.IsNullOrWhiteSpace(path))
        {
            return "Użycie: /map export <ścieżka-do-world-map.json>.";
        }

        try
        {
            var fullPath = Path.GetFullPath(path.Trim().Trim('"'));
            if (!string.Equals(Path.GetExtension(fullPath), ".json", StringComparison.OrdinalIgnoreCase))
            {
                return "Eksport mapy wymaga ścieżki zakończonej rozszerzeniem .json.";
            }

            await new MapWriter().SaveAsync(
                    _mapEditor.Document,
                    fullPath,
                    cancellationToken,
                    baselinePath: _worldMapPath)
                .ConfigureAwait(false);
            return $"Wyeksportowano mapę do {fullPath}.";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return $"Nie udało się wyeksportować mapy: {exception.Message}";
        }
    }

    public async Task<string> ImportMapEditorAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        if (_mapEditorPath is null)
        {
            return "Brak katalogu danych dla mapy roboczej.";
        }

        try
        {
            var fullPath = Path.GetFullPath(path.Trim().Trim('"'));
            if (!File.Exists(fullPath))
            {
                return $"Nie znaleziono pliku mapy: {fullPath}.";
            }

            var imported = await new MapLoader().LoadAsync(fullPath, cancellationToken);
            if (_mapEditorRecoveryStore is not null)
            {
                await _mapEditorRecoveryStore.DeleteAsync(cancellationToken);
            }

            StopMapEditor();
            await new MapWriter().SaveAsync(
                    imported.Document,
                    _mapEditorPath,
                    cancellationToken,
                    baselinePath: fullPath);
            await InitializeAsync(cancellationToken);
            return $"Zaimportowano mapę roboczą z {fullPath}.";
        }
        catch (Exception exception) when (exception is MapLoadException
            or IOException
            or UnauthorizedAccessException
            or ArgumentException)
        {
            return $"Nie udało się zaimportować mapy: {exception.Message}";
        }
    }

    public async Task<string> DiscardWorkingMapAsync(CancellationToken cancellationToken = default)
    {
        if (_mapEditorPath is null ||
            (!File.Exists(_mapEditorPath) && _mapEditorRecoveryStore?.Exists != true))
        {
            return "Brak zapisanej mapy roboczej do odrzucenia.";
        }

        StopMapEditor();
        try
        {
            if (File.Exists(_mapEditorPath))
            {
                File.Delete(_mapEditorPath);
            }

            if (_mapEditorRecoveryStore is not null)
            {
                await _mapEditorRecoveryStore.DeleteAsync(cancellationToken);
            }

            await InitializeAsync(cancellationToken);
            return "Odrzucono mapę roboczą i załadowano aktualną mapę bazową.";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return $"Nie udało się odrzucić mapy roboczej: {exception.Message}";
        }
    }

    public Task FlushMapEditorRecoveryAsync(CancellationToken cancellationToken = default) =>
        _mapEditorRecoveryStore?.FlushAsync(cancellationToken) ?? Task.CompletedTask;

    public IReadOnlyList<string> ValidateEditedMap()
    {
        var issues = _mapEditor?.Validate() ?? ["Edytor mapy nie jest gotowy."];
        NotifyMapEditorStateChanged();
        return issues;
    }

    public void ShowCurrentMapRoomInfo()
    {
        _mapEditor?.ShowCurrentRoomInfo();
        NotifyMapEditorStateChanged();
    }

    public void HandleRoomSnapshot(RoomSnapshot snapshot)
    {
        if (_mapEditor is null)
        {
            return;
        }

        CancelMapMovementTimeout();
        var wasActive = IsMapEditorActive;
        if (_mapEditor.ProcessSnapshot(snapshot))
        {
            ApplyMapEditorDocument();
        }
        if (wasActive && !IsMapEditorActive)
        {
            MapEditorActiveChanged?.Invoke(false);
        }

        NotifyMapEditorStateChanged();
    }

    public void StartMapEditor()
    {
        if (_mapEditor is null)
        {
            return;
        }

        if (!LordModeEnabled)
        {
            _mapEditor.Stop();
            NotifyMapEditorStateChanged();
            return;
        }

        var wasActive = IsMapEditorActive;
        var documentBeforeStart = _mapEditor.Document;
        _mapEditor.Start(CurrentVnum);
        if (!ReferenceEquals(documentBeforeStart, _mapEditor.Document))
        {
            ApplyMapEditorDocument();
        }
        if (!wasActive && IsMapEditorActive)
        {
            MapEditorActiveChanged?.Invoke(true);
        }

        NotifyMapEditorStateChanged();
    }

    public void StopMapEditor() => StopMapEditor(null);

    public void StopMapEditor(string? reason)
    {
        CancelMapMovementTimeout();
        var wasActive = IsMapEditorActive;
        _mapEditor?.Stop(reason);
        if (wasActive)
        {
            MapEditorActiveChanged?.Invoke(false);
        }

        NotifyMapEditorStateChanged();
    }

    public void UndoMapEditor()
    {
        if (_mapEditor?.Undo() == true)
        {
            ApplyMapEditorDocument();
        }

        NotifyMapEditorStateChanged();
    }

    public void RedoMapEditor()
    {
        if (_mapEditor?.Redo() == true)
        {
            ApplyMapEditorDocument();
        }

        NotifyMapEditorStateChanged();
    }

    public async Task SaveMapEditorAsync()
    {
        if (_mapEditor is null)
        {
            return;
        }

        if (_mapEditorPath is null)
        {
            StatusMessage = "Brak katalogu danych dla roboczej mapy.";
            return;
        }

        try
        {
            await new MapWriter().SaveAsync(
                    _mapEditor.Document,
                    _mapEditorPath,
                    _loadCancellation?.Token ?? default,
                    baselinePath: _worldMapPath)
                .ConfigureAwait(false);
            _mapEditor.MarkSaved();
            _worldMapPath = _mapEditorPath;
            if (_mapEditorRecoveryStore is not null)
            {
                await _mapEditorRecoveryStore.SaveCheckpointAsync(
                    _mapEditor.Document,
                    _mapEditor.GetUndoHistory(),
                    isDirty: false,
                    baselineIdentity: GetMapBaselineIdentity(),
                    cancellationToken: _loadCancellation?.Token ?? default).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            _mapEditor.Stop();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            StatusMessage = $"Nie udało się zapisać mapy: {exception.Message}";
            System.Diagnostics.Trace.WriteLine(exception);
        }

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            IsUsingWorkingMap = File.Exists(_mapEditorPath);
            IsUsingRecoveryMap = false;
            NotifyMapEditorStateChanged();
        });
    }

    private bool CanStartMapEditor() =>
        LordModeEnabled
        && _mapEditor is not null
        && !IsMapEditorActive
        && !string.IsNullOrWhiteSpace(CurrentVnum)
        && (CurrentRoom is not null || _mapEditor.HasTargetArea);

    private bool ApplyMapEditorOperation(
        Func<MapEditorSession, bool> operation,
        bool applyDocument = true)
    {
        if (_mapEditor is null || !operation(_mapEditor))
        {
            NotifyMapEditorStateChanged();
            return false;
        }

        if (applyDocument)
        {
            ApplyMapEditorDocument();
        }
        NotifyMapEditorStateChanged();
        return true;
    }

    private void ApplyMapEditorDocument()
    {
        if (_mapEditor is null)
        {
            return;
        }

        var selectedAreaId = SelectedArea?.Id;
        MapIndex = new MapIndex(_mapEditor.Document, Settings.SpatialBucketSize);
        Areas.Clear();
        foreach (var area in MapIndex.Document.Areas)
        {
            Areas.Add(area);
        }

        if (selectedAreaId is not null && MapIndex.AreasById.GetValueOrDefault(selectedAreaId.Value) is { } selectedArea)
        {
            SetSelectedAreaInternal(selectedArea);
        }

        TryResolveCurrentRoom();
        ScheduleMapEditorRecovery();
    }

    private void NotifyMapEditorStateChanged()
    {
        if (_mapEditor is { } editor &&
            _moveExistingRoomsToNewArea != editor.MoveKnownRoomsToTargetArea)
        {
            _moveExistingRoomsToNewArea = editor.MoveKnownRoomsToTargetArea;
        }

        OnPropertyChanged(nameof(IsMapEditorActive));
        OnPropertyChanged(nameof(IsMapEditorDirty));
        OnPropertyChanged(nameof(IsMapEditorAwaitingRoomInfo));
        OnPropertyChanged(nameof(MapEditorStep));
        OnPropertyChanged(nameof(MapEditorStatus));
        OnPropertyChanged(nameof(MapEditorSourceDescription));
        OnPropertyChanged(nameof(CanMoveExistingRoomsToNewArea));
        OnPropertyChanged(nameof(MoveExistingRoomsToNewArea));
        NotifyMapEditorCommands();
    }

    private void ScheduleMapEditorRecovery()
    {
        if (_mapEditor is null || _mapEditorRecoveryStore is null)
        {
            return;
        }

        _mapEditorRecoveryStore.Schedule(
            _mapEditor.Document,
            _mapEditor.GetUndoHistory(),
            _mapEditor.IsDirty,
            GetMapBaselineIdentity());
    }

    private void StartMapMovementTimeout()
    {
        CancelMapMovementTimeout();
        var cancellation = new CancellationTokenSource();
        _mapMovementTimeoutCancellation = cancellation;
        _mapMovementTimeoutTask = WaitForMapMovementTimeoutAsync(cancellation);
    }

    private async Task WaitForMapMovementTimeoutAsync(CancellationTokenSource cancellation)
    {
        try
        {
            await Task.Delay(_mapMovementTimeout, cancellation.Token).ConfigureAwait(false);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (!ReferenceEquals(_mapMovementTimeoutCancellation, cancellation)
                    || _mapEditor?.IsAwaitingRoomInfo != true)
                {
                    return;
                }

                _mapMovementTimeoutCancellation = null;
                var seconds = _mapMovementTimeout.TotalSeconds.ToString("0.#");
                _mapEditor.CancelPendingMovement(
                    $"Brak Room.Info przez {seconds} s. Anulowano oczekiwanie na ruch; mapowanie pozostaje aktywne.");
                NotifyMapEditorStateChanged();
            });
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            // A snapshot, explicit cancellation, stop, or disposal superseded this timeout.
        }
        catch (Exception exception)
        {
            // Dispatcher shutdown can race application disposal; the mapper timeout must never
            // surface as an unobserved background-task exception.
            System.Diagnostics.Debug.WriteLine($"Map movement timeout failed: {exception}");
        }
        finally
        {
            Interlocked.CompareExchange(ref _mapMovementTimeoutCancellation, null, cancellation);
            cancellation.Dispose();
        }
    }

    private void CancelMapMovementTimeout()
    {
        Interlocked.Exchange(ref _mapMovementTimeoutCancellation, null)?.Cancel();
    }

    private string GetMapBaselineIdentity()
    {
        if (!File.Exists(_worldMapPath))
        {
            return Path.GetFullPath(_worldMapPath);
        }

        var file = new FileInfo(_worldMapPath);
        return $"{file.FullName}|{file.Length}|{file.LastWriteTimeUtc.Ticks}";
    }

    private void NotifyMapEditorCommands()
    {
        _startMapEditorCommand.NotifyCanExecuteChanged();
        _stopMapEditorCommand.NotifyCanExecuteChanged();
        _undoMapEditorCommand.NotifyCanExecuteChanged();
        _redoMapEditorCommand.NotifyCanExecuteChanged();
        _createMapAreaCommand.NotifyCanExecuteChanged();
        _saveMapEditorCommand.NotifyCanExecuteChanged();
    }

    private void RequestCenterOnCurrentRoom() => CenterOnPlayer();

    public void Dispose()
    {
        _locationResolver.LocationChanged -= OnLocationChanged;
        CancelMapMovementTimeout();
        _loadCancellation?.Cancel();
        _loadCancellation?.Dispose();
        TextureCache?.Dispose();
        RoomImages?.Dispose();
        _mapEditorRecoveryStore?.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        _locationResolver.LocationChanged -= OnLocationChanged;
        CancelMapMovementTimeout();
        await _mapMovementTimeoutTask.ConfigureAwait(false);
        _loadCancellation?.Cancel();
        _loadCancellation?.Dispose();
        TextureCache?.Dispose();
        RoomImages?.Dispose();
        if (_mapEditorRecoveryStore is not null)
        {
            await _mapEditorRecoveryStore.DisposeAsync();
        }
    }
}

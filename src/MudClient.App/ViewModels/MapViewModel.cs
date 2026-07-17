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

public sealed class MapViewModel : ObservableObject, IDisposable
{
    private readonly string _worldMapPath;
    private readonly string _mapSettingsPath;
    private readonly string _sectorDirectory;
    private readonly string _sectorManifestPath;
    private readonly string _roomImageDirectory;
    private readonly GmcpLocationResolver _locationResolver;

    private MapIndex? _mapIndex;
    private SectorTextureCache? _textureCache;
    private RoomImageCache? _roomImages;
    private MapSettings _settings = MapSettings.CreateDefault();
    private CancellationTokenSource? _loadCancellation;

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
    private MapDisplayModeOption _selectedDisplayMode;
    private readonly RelayCommand _lordGotoSelectedRoomCommand;

    public MapViewModel(string appBaseDirectory, GmcpLocationResolver locationResolver)
    {
        var mapDirectory = Path.Combine(appBaseDirectory, "Assets", "Map");
        _worldMapPath = Path.Combine(mapDirectory, "world-map.json");
        _mapSettingsPath = Path.Combine(mapDirectory, "map-settings.json");
        _sectorDirectory = Path.Combine(mapDirectory, "Sectors");
        _sectorManifestPath = Path.Combine(_sectorDirectory, "sectors.json");
        _roomImageDirectory = Path.Combine(mapDirectory, "Rooms");

        _selectedDisplayMode = MapDisplayModeOption.All[0];
        _locationResolver = locationResolver;
        _locationResolver.LocationChanged += OnLocationChanged;

        ReloadCommand = new AsyncRelayCommand(InitializeAsync);
        CenterCommand = new RelayCommand(RequestCenterOnCurrentRoom);
        _lordGotoSelectedRoomCommand = new RelayCommand(
            RequestLordGotoSelectedRoom,
            CanLordGotoSelectedRoom);
    }

    public event Action? CenterOnCurrentRoomRequested;

    public event Action<MapRoom>? CenterOnRoomRequested;

    /// <summary>Raised by the view when the user double-clicks a room on the map.</summary>
    public event Action<MapRoom>? RoomDoubleClicked;

    public event Action<MapRoom>? LordGotoRequested;

    public event Action<bool>? LordModeChanged;

    public event Action<bool>? GroupMarkerDisplayChanged;

    public ObservableCollection<MapArea> Areas { get; } = [];

    public ObservableCollection<double> ZLevels { get; } = [];

    public IAsyncRelayCommand ReloadCommand { get; }

    public IRelayCommand CenterCommand { get; }

    public IRelayCommand LordGotoSelectedRoomCommand => _lordGotoSelectedRoomCommand;

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
                FollowPlayer = false;
                RefreshZLevels();
                FocusFirstRoom(value);
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
            var settingsLoader = new MapSettingsLoader();
            Settings = await settingsLoader.LoadAsync(_mapSettingsPath, cancellation.Token).ConfigureAwait(false);

            TextureCache?.Dispose();
            TextureCache = new SectorTextureCache(_sectorDirectory, _sectorManifestPath);

            RoomImages?.Dispose();
            RoomImages = new RoomImageCache(_roomImageDirectory);

            if (!File.Exists(_worldMapPath))
            {
                ErrorMessage = $"Nie znaleziono pliku mapy: {_worldMapPath}";
                StatusMessage = "Brak pliku mapy.";
                return;
            }

            var loader = new MapLoader();
            var result = await loader.LoadAsync(_worldMapPath, cancellation.Token).ConfigureAwait(false);

            var index = new MapIndex(result.Document, Settings.SpatialBucketSize);

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

    private void RequestCenterOnCurrentRoom() => CenterOnPlayer();

    public void Dispose()
    {
        _locationResolver.LocationChanged -= OnLocationChanged;
        _loadCancellation?.Cancel();
        _loadCancellation?.Dispose();
        TextureCache?.Dispose();
        RoomImages?.Dispose();
    }
}

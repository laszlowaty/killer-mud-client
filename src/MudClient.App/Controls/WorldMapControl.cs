using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using MudClient.App.Services;
using MudClient.App.ViewModels;
using MudClient.Core.Map;

namespace MudClient.App.Controls;

public sealed class WorldMapControl : Control
{
    public static readonly StyledProperty<FontFamily> WidgetFontFamilyProperty =
        AvaloniaProperty.Register<WorldMapControl, FontFamily>(
            nameof(WidgetFontFamily), new FontFamily("Inter"));

    public static readonly StyledProperty<double> WidgetFontSizeProperty =
        AvaloniaProperty.Register<WorldMapControl, double>(nameof(WidgetFontSize), 13);

    public static readonly StyledProperty<FontWeight> WidgetFontWeightProperty =
        AvaloniaProperty.Register<WorldMapControl, FontWeight>(
            nameof(WidgetFontWeight), FontWeight.Normal);

    private const double PanKeyStep = 40;
    private const double OverviewZoomThreshold = 0.45;
    private const double SimpleMapSpacingScale = 1.25;
    private const double TerrainCoastOpacity = 0.12;
    private const double TerrainFieldOpacity = 0.20;

    private static readonly Pen ExitPen = new(Brushes.Silver, 2.5);
    private static readonly IBrush SimpleCanvasBrush = new LinearGradientBrush
    {
        StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
        EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
        GradientStops =
        [
            new GradientStop(Color.FromRgb(7, 10, 14), 0),
            new GradientStop(Color.FromRgb(13, 18, 23), 1),
        ],
    };

    private static readonly Pen RoutePen = new(Brushes.Orange, 3.5) { LineCap = PenLineCap.Round };
    private static readonly Pen RouteTargetPen = new(Brushes.Orange, 3);
    private static readonly IBrush LowerLevelRoomBrush =
        new SolidColorBrush(Color.FromArgb(90, 24, 28, 31));
    private static readonly Pen LowerLevelRoomPen =
        new(new SolidColorBrush(Color.FromArgb(105, 104, 110, 114)), 1.4);
    private static readonly Pen LowerLevelExitPen =
        new(new SolidColorBrush(Color.FromArgb(105, 76, 82, 86)), 2)
        {
            LineCap = PenLineCap.Round,
        };
    private static readonly IEffect LowerLevelBlurEffect = new ImmutableBlurEffect(2.4);
    private static readonly Vector LowerLevelShadowOffset = new(3, 3);
    private static readonly Dictionary<int, MapOffset> EmptyOffsets = [];

    private readonly CollisionLayoutService _collisionLayout = new();
    private readonly HashSet<MapCellKey> _expandedGroups = [];
    private readonly Dictionary<int, TerrainStyle> _terrainStyles = [];
    private readonly Dictionary<int, bool> _routeSectors = [];
    private readonly Dictionary<int, FormattedText> _collisionBadges = [];

    private MapIndex? _mapIndex;
    private MapSettings _settings = MapSettings.CreateDefault();
    private SectorTextureCache? _textureCache;
    private RoomImageCache? _roomImages;

    private int _areaId;
    private double _z;
    private MapRoom? _currentRoom;
    private MapRoom? _selectedRoom;
    private IReadOnlyList<MapRoom>? _route;
    private IReadOnlyList<GroupMapMarker> _groupMarkers = [];
    private MapDisplayMode _displayMode;
    private bool _isSimpleMap;

    private double _cameraX;
    private double _cameraY;
    private double _zoom = 1.0;
    private bool _invalidateQueued;

    private Point? _dragStartScreen;
    private double _dragStartCameraX;
    private double _dragStartCameraY;
    private bool _dragMoved;

    public WorldMapControl()
    {
        Focusable = true;
        ClipToBounds = true;
    }

    public event Action<MapRoom?>? RoomClicked;

    public event Action<MapRoom>? RoomDoubleClicked;

    public event Action? ManualNavigationOccurred;

    public FontFamily WidgetFontFamily
    {
        get => GetValue(WidgetFontFamilyProperty);
        set => SetValue(WidgetFontFamilyProperty, value);
    }

    public double WidgetFontSize
    {
        get => GetValue(WidgetFontSizeProperty);
        set => SetValue(WidgetFontSizeProperty, value);
    }

    public FontWeight WidgetFontWeight
    {
        get => GetValue(WidgetFontWeightProperty);
        set => SetValue(WidgetFontWeightProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == WidgetFontFamilyProperty || change.Property == WidgetFontSizeProperty
            || change.Property == WidgetFontWeightProperty)
        {
            _collisionBadges.Clear();
            RequestInvalidateVisual();
        }
    }

    public MapIndex? MapIndex
    {
        get => _mapIndex;
        set
        {
            if (ReferenceEquals(_mapIndex, value))
            {
                return;
            }

            _mapIndex = value;
            _terrainStyles.Clear();
            _routeSectors.Clear();
            _collisionBadges.Clear();
            RequestInvalidateVisual();
        }
    }

    public MapSettings Settings
    {
        get => _settings;
        set
        {
            _settings = value;
            _zoom = Math.Clamp(_zoom, _settings.MinimumZoom, _settings.MaximumZoom);
            RequestInvalidateVisual();
        }
    }

    public SectorTextureCache? TextureCache
    {
        get => _textureCache;
        set
        {
            _textureCache = value;
            RequestInvalidateVisual();
        }
    }

    public RoomImageCache? RoomImages
    {
        get => _roomImages;
        set
        {
            if (ReferenceEquals(_roomImages, value))
            {
                return;
            }

            if (_roomImages is not null)
            {
                _roomImages.MapIconLoaded -= RequestInvalidateVisual;
            }

            _roomImages = value;

            if (_roomImages is not null)
            {
                _roomImages.MapIconLoaded += RequestInvalidateVisual;
            }

            RequestInvalidateVisual();
        }
    }

    public MapDisplayMode DisplayMode
    {
        get => _displayMode;
        set
        {
            if (_displayMode != value)
            {
                _displayMode = value;
                _isSimpleMap = value == MapDisplayMode.Simple;
                RequestInvalidateVisual();
            }
        }
    }

    public int AreaId
    {
        get => _areaId;
        set
        {
            if (_areaId != value)
            {
                _areaId = value;
                _expandedGroups.Clear();
                RequestInvalidateVisual();
            }
        }
    }

    public double Z
    {
        get => _z;
        set
        {
            if (_z != value)
            {
                _z = value;
                _expandedGroups.Clear();
                RequestInvalidateVisual();
            }
        }
    }

    public MapRoom? CurrentRoom
    {
        get => _currentRoom;
        set
        {
            _currentRoom = value;
            RequestInvalidateVisual();
        }
    }

    public MapRoom? SelectedRoom
    {
        get => _selectedRoom;
        set
        {
            _selectedRoom = value;
            RequestInvalidateVisual();
        }
    }

    /// <summary>Autowalk route to draw: consecutive rooms from start to target.</summary>
    public IReadOnlyList<MapRoom>? Route
    {
        get => _route;
        set
        {
            _route = value;
            RequestInvalidateVisual();
        }
    }

    public double Zoom => _zoom;

    public double CameraX => _cameraX;

    public double CameraY => _cameraY;

    public void CenterOnRoom(MapRoom room)
    {
        _cameraX = room.Coordinates.X;
        _cameraY = room.Coordinates.Y;
        RequestInvalidateVisual();
    }

    public void ResetZoom()
    {
        _zoom = 1.0;
        RequestInvalidateVisual();
    }

    public void CollapseExpandedGroups()
    {
        if (_expandedGroups.Count > 0)
        {
            _expandedGroups.Clear();
            RequestInvalidateVisual();
        }
    }

    public IReadOnlyList<GroupMapMarker> GroupMarkers
    {
        get => _groupMarkers;
        set
        {
            _groupMarkers = value ?? [];
            RequestInvalidateVisual();
        }
    }

    private double GetWorldScale() =>
        _settings.PixelsPerCoordinateUnit * _zoom * (_isSimpleMap ? SimpleMapSpacingScale : 1);

    public Point WorldToScreen(double worldX, double worldY)
    {
        var bounds = Bounds;
        var centerX = bounds.Width / 2;
        var centerY = bounds.Height / 2;
        var scale = GetWorldScale();

        var screenX = centerX + (worldX - _cameraX) * scale;
        var screenY = centerY - (worldY - _cameraY) * scale;
        return new Point(screenX, screenY);
    }

    public Point ScreenToWorld(Point screenPoint)
    {
        var bounds = Bounds;
        var centerX = bounds.Width / 2;
        var centerY = bounds.Height / 2;
        var scale = GetWorldScale();

        var worldX = _cameraX + (screenPoint.X - centerX) / scale;
        var worldY = _cameraY - (screenPoint.Y - centerY) / scale;
        return new Point(worldX, worldY);
    }

    public Rect GetVisibleWorldBounds()
    {
        var topLeft = ScreenToWorld(new Point(0, 0));
        var bottomRight = ScreenToWorld(new Point(Bounds.Width, Bounds.Height));

        var minX = Math.Min(topLeft.X, bottomRight.X);
        var maxX = Math.Max(topLeft.X, bottomRight.X);
        var minY = Math.Min(topLeft.Y, bottomRight.Y);
        var maxY = Math.Max(topLeft.Y, bottomRight.Y);

        return new Rect(minX, minY, maxX - minX, maxY - minY);
    }

    public void ZoomAtPointer(double delta, Point pointerPosition)
    {
        var worldBefore = ScreenToWorld(pointerPosition);

        var factor = Math.Pow(1.15, delta);
        _zoom = Math.Clamp(_zoom * factor, _settings.MinimumZoom, _settings.MaximumZoom);

        var worldAfter = ScreenToWorld(pointerPosition);

        _cameraX += worldBefore.X - worldAfter.X;
        _cameraY += worldBefore.Y - worldAfter.Y;

        RequestInvalidateVisual();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Focus();

        var point = e.GetCurrentPoint(this);

        if (point.Properties.IsRightButtonPressed)
        {
            if (HitTestRoom(point.Position) is { } contextRoom)
            {
                SelectedRoom = contextRoom;
                RoomClicked?.Invoke(contextRoom);
            }

            return;
        }

        if (point.Properties.IsLeftButtonPressed && e.ClickCount == 2)
        {
            if (HitTestRoom(point.Position) is { } doubleClicked)
            {
                RoomDoubleClicked?.Invoke(doubleClicked);
                _dragStartScreen = null;
                e.Handled = true;
                return;
            }
        }

        if (point.Properties.IsLeftButtonPressed || point.Properties.IsMiddleButtonPressed)
        {
            _dragStartScreen = point.Position;
            _dragStartCameraX = _cameraX;
            _dragStartCameraY = _cameraY;
            _dragMoved = false;
            e.Pointer.Capture(this);
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        if (_dragStartScreen is not { } start)
        {
            return;
        }

        var position = e.GetCurrentPoint(this).Position;
        var deltaX = position.X - start.X;
        var deltaY = position.Y - start.Y;

        if (!_dragMoved && (Math.Abs(deltaX) > 3 || Math.Abs(deltaY) > 3))
        {
            _dragMoved = true;
        }

        if (!_dragMoved)
        {
            return;
        }

        var scale = GetWorldScale();
        _cameraX = _dragStartCameraX - deltaX / scale;
        _cameraY = _dragStartCameraY + deltaY / scale;

        ManualNavigationOccurred?.Invoke();
        RequestInvalidateVisual();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        e.Pointer.Capture(null);

        if (_dragStartScreen is null)
        {
            return;
        }

        if (!_dragMoved)
        {
            HandleClick(e.GetCurrentPoint(this).Position);
        }

        _dragStartScreen = null;
        _dragMoved = false;
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        ZoomAtPointer(e.Delta.Y, e.GetCurrentPoint(this).Position);
        e.Handled = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        var scale = GetWorldScale();
        var step = PanKeyStep / scale;

        switch (e.Key)
        {
            case Key.Left or Key.A:
                _cameraX -= step;
                ManualNavigationOccurred?.Invoke();
                RequestInvalidateVisual();
                e.Handled = true;
                break;

            case Key.Right or Key.D:
                _cameraX += step;
                ManualNavigationOccurred?.Invoke();
                RequestInvalidateVisual();
                e.Handled = true;
                break;

            case Key.Up or Key.W:
                _cameraY += step;
                ManualNavigationOccurred?.Invoke();
                RequestInvalidateVisual();
                e.Handled = true;
                break;

            case Key.Down or Key.S:
                _cameraY -= step;
                ManualNavigationOccurred?.Invoke();
                RequestInvalidateVisual();
                e.Handled = true;
                break;

            case Key.Home when _currentRoom is not null:
                CenterOnRoom(_currentRoom);
                e.Handled = true;
                break;

            case Key.Escape:
                CollapseExpandedGroups();
                e.Handled = true;
                break;
        }
    }

    private void HandleClick(Point screenPosition)
    {
        var room = HitTestRoom(screenPosition);

        if (room is null)
        {
            SelectedRoom = null;
            CollapseExpandedGroups();
            RoomClicked?.Invoke(null);
            return;
        }

        var group = _mapIndex?.GetCollisionGroup(room);
        if (group is { HasCollision: true } && !_expandedGroups.Contains(group.Cell))
        {
            _expandedGroups.Add(group.Cell);
            RequestInvalidateVisual();
            return;
        }

        SelectedRoom = room;
        RoomClicked?.Invoke(room);
    }

    private MapRoom? HitTestRoom(Point screenPosition)
    {
        if (_mapIndex is null)
        {
            return null;
        }

        var roomSize = _settings.RoomSize * _zoom;
        var candidates = GetVisibleRooms();

        MapRoom? best = null;
        var bestDistance = double.MaxValue;

        foreach (var (room, offset) in candidates)
        {
            var center = WorldToScreen(room.Coordinates.X + offset.X * 0.6, room.Coordinates.Y + offset.Y * 0.6);
            var half = roomSize / 2;

            if (screenPosition.X < center.X - half || screenPosition.X > center.X + half ||
                screenPosition.Y < center.Y - half || screenPosition.Y > center.Y + half)
            {
                continue;
            }

            var distance = Math.Pow(screenPosition.X - center.X, 2) + Math.Pow(screenPosition.Y - center.Y, 2);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = room;
            }
        }

        return best;
    }

    private IEnumerable<(MapRoom Room, MapOffset Offset)> GetVisibleRooms() =>
        GetVisibleRooms(_z, includeExpandedGroups: true);

    private IEnumerable<(MapRoom Room, MapOffset Offset)> GetVisibleRooms(
        double z,
        bool includeExpandedGroups)
    {
        if (_mapIndex is null)
        {
            yield break;
        }

        var bounds = GetVisibleWorldBounds();
        var margin = _settings.RoomSize / Math.Max(GetWorldScale(), 0.001) * 2;

        var rooms = _mapIndex.GetRoomsInBounds(
            _areaId,
            z,
            bounds.X - margin,
            bounds.Y - margin,
            bounds.X + bounds.Width + margin,
            bounds.Y + bounds.Height + margin);

        foreach (var room in rooms)
        {
            var group = _mapIndex.GetCollisionGroup(room);

            if (group is not { HasCollision: true })
            {
                yield return (room, MapOffset.Zero);
                continue;
            }

            var isExpanded = includeExpandedGroups &&
                (_expandedGroups.Contains(group.Cell) ||
                    (_currentRoom is not null && group.Rooms.Any(r => r.Id == _currentRoom.Id)));

            if (!isExpanded)
            {
                if (room.Id == group.Rooms.Min(r => r.Id))
                {
                    yield return (room, MapOffset.Zero);
                }

                continue;
            }

            var layout = _collisionLayout.ComputeLayout(group, _currentRoom?.Id);
            yield return (room, layout.GetValueOrDefault(room.Id, MapOffset.Zero));
        }
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var bounds = Bounds;
        context.FillRectangle(_isSimpleMap ? SimpleCanvasBrush : GetCanvasBrush(), bounds);
        var hasWorldBackdrop = !_isSimpleMap && DrawWorldBackground(context, bounds);

        if (_mapIndex is null)
        {
            DrawCenteredMessage(context, "Ładowanie mapy…");
            return;
        }

        if (_zoom < OverviewZoomThreshold && hasWorldBackdrop)
        {
            DrawRoute(context, EmptyOffsets);
            DrawOverviewSelectionAndCurrent(context);
            DrawGroupMarkers(context, EmptyOffsets);
            DrawCompass(context);
            return;
        }

        var roomsWithOffsets = GetVisibleRooms().ToList();
        var roomLookup = roomsWithOffsets.ToDictionary(r => r.Room.Id, r => r.Offset);

        if (!_isSimpleMap)
        {
            DrawTerrain(context, roomsWithOffsets, roomLookup);
        }

        DrawLowerLevelShadow(context);
        DrawExits(context, roomsWithOffsets, roomLookup);
        DrawRooms(context, roomsWithOffsets);
        DrawRoute(context, roomLookup);
        DrawSelectionAndCurrent(context, roomsWithOffsets);
        DrawGroupMarkers(context, roomLookup);
        if (!_isSimpleMap)
        {
            DrawCompass(context);
        }
    }

    private void DrawOverviewSelectionAndCurrent(DrawingContext context)
    {
        var roomSize = Math.Max(_settings.RoomSize * _zoom, 2);

        if (_selectedRoom is { } selected && selected.AreaId == _areaId && selected.Coordinates.Z == _z)
        {
            var center = WorldToScreen(selected.Coordinates.X, selected.Coordinates.Y);
            var rect = new Rect(center.X - roomSize / 2, center.Y - roomSize / 2, roomSize, roomSize);
            DrawInsideOutline(context, rect, Brushes.Gold, 2);
        }

        if (_currentRoom is { } current && current.AreaId == _areaId && current.Coordinates.Z == _z)
        {
            var center = WorldToScreen(current.Coordinates.X, current.Coordinates.Y);
            var rect = new Rect(center.X - roomSize / 2, center.Y - roomSize / 2, roomSize, roomSize);
            DrawInsideOutline(context, rect, Brushes.LimeGreen, 3);
            DrawInsideOutline(context, rect, Brushes.White, 1, 3);
        }
    }

    private IBrush GetCanvasBrush() => _z < 0 || _areaId is 10 or 15 or 16
        ? new SolidColorBrush(Color.FromRgb(8, 10, 13))
        : new SolidColorBrush(Color.FromRgb(11, 17, 14));

    private bool DrawWorldBackground(DrawingContext context, Rect bounds)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return false;
        }

        if (_textureCache?.GetBackgroundTexture() is { } texture)
        {
            var sourceWidth = texture.PixelSize.Width;
            var sourceHeight = texture.PixelSize.Height;
            var sourceAspect = sourceWidth / (double)sourceHeight;
            var targetAspect = bounds.Width / bounds.Height;
            var source = targetAspect > sourceAspect
                ? new Rect(0, (sourceHeight - sourceWidth / targetAspect) / 2, sourceWidth, sourceWidth / targetAspect)
                : new Rect((sourceWidth - sourceHeight * targetAspect) / 2, 0, sourceHeight * targetAspect, sourceHeight);

            var opacity = _z < 0 || _areaId is 10 or 15 or 16 ? 0.28 : 0.62;
            using (context.PushOpacity(opacity))
            {
                context.DrawImage(texture, source, bounds);
            }
        }

        if (_textureCache?.GetWorldBackdrop(_areaId, _z) is not { } backdrop)
        {
            context.FillRectangle(new SolidColorBrush(Color.FromArgb(54, 5, 9, 7)), bounds);
            return false;
        }

        var visible = GetVisibleWorldBounds();
        var minX = Math.Max(visible.X, backdrop.MinX);
        var maxX = Math.Min(visible.Right, backdrop.MaxX);
        var minY = Math.Max(visible.Y, backdrop.MinY);
        var maxY = Math.Min(visible.Bottom, backdrop.MaxY);
        if (minX >= maxX || minY >= maxY)
        {
            return true;
        }

        var sourceRect = new Rect(
            (minX - backdrop.MinX) * backdrop.PixelsPerUnit,
            (backdrop.MaxY - maxY) * backdrop.PixelsPerUnit,
            (maxX - minX) * backdrop.PixelsPerUnit,
            (maxY - minY) * backdrop.PixelsPerUnit);
        var topLeft = WorldToScreen(minX, maxY);
        var bottomRight = WorldToScreen(maxX, minY);
        var destination = new Rect(topLeft, bottomRight);
        using (context.PushOpacity(_zoom < OverviewZoomThreshold ? 0.74 : 0.5))
        {
            context.DrawImage(backdrop.Terrain, sourceRect, destination);
        }

        if (_zoom < OverviewZoomThreshold)
        {
            context.DrawImage(backdrop.Rooms, sourceRect, destination);
        }

        context.FillRectangle(new SolidColorBrush(Color.FromArgb(34, 5, 9, 7)), bounds);
        return true;
    }

    private void DrawTerrain(
        DrawingContext context,
        List<(MapRoom Room, MapOffset Offset)> rooms,
        Dictionary<int, MapOffset> offsets)
    {
        if (_mapIndex is null || rooms.Count == 0)
        {
            return;
        }

        var fieldWidth = Math.Clamp(_settings.PixelsPerCoordinateUnit * _zoom * 3.2, 7, 58);
        var visibleIds = rooms.Select(item => item.Room.Id).ToHashSet();
        var drawn = new HashSet<(int, int)>();
        var isOverview = _zoom < OverviewZoomThreshold;

        Point Center(MapRoom room)
        {
            var offset = offsets.GetValueOrDefault(room.Id, MapOffset.Zero);
            return WorldToScreen(room.Coordinates.X + offset.X * 0.6, room.Coordinates.Y + offset.Y * 0.6);
        }

        if (!isOverview)
        {
            using (context.PushOpacity(TerrainCoastOpacity))
            {
                var coastBrush = new SolidColorBrush(Color.FromRgb(20, 24, 21));
                var coastPen = new Pen(coastBrush, fieldWidth + 8) { LineCap = PenLineCap.Round };
                foreach (var (room, _) in rooms)
                {
                    foreach (var exit in room.Exits)
                    {
                        if (!visibleIds.Contains(exit.ExitId) || !_mapIndex.RoomsById.TryGetValue(exit.ExitId, out var target))
                        {
                            continue;
                        }

                        var edge = room.Id < target.Id ? (room.Id, target.Id) : (target.Id, room.Id);
                        if (drawn.Add(edge))
                        {
                            context.DrawLine(coastPen, Center(room), Center(target));
                        }
                    }

                    context.DrawEllipse(coastBrush, null, Center(room), (fieldWidth + 8) / 2, (fieldWidth + 8) / 2);
                }
            }
        }

        drawn.Clear();
        var terrainPens = new Dictionary<TerrainStyle, Pen>();
        using (context.PushOpacity(TerrainFieldOpacity))
        {
            foreach (var (room, _) in rooms)
            {
                var style = GetTerrainStyle(room);
                foreach (var exit in room.Exits)
                {
                    if (!visibleIds.Contains(exit.ExitId) || !_mapIndex.RoomsById.TryGetValue(exit.ExitId, out var target))
                    {
                        continue;
                    }

                    var edge = room.Id < target.Id ? (room.Id, target.Id) : (target.Id, room.Id);
                    if (!drawn.Add(edge))
                    {
                        continue;
                    }

                    var targetStyle = GetTerrainStyle(target);
                    var connectionStyle = style.Priority >= targetStyle.Priority ? style : targetStyle;
                    if (!terrainPens.TryGetValue(connectionStyle, out var terrainPen))
                    {
                        terrainPen = new Pen(connectionStyle.Brush, fieldWidth) { LineCap = PenLineCap.Round };
                        terrainPens[connectionStyle] = terrainPen;
                    }

                    context.DrawLine(terrainPen, Center(room), Center(target));
                }

                context.DrawEllipse(style.Brush, null, Center(room), fieldWidth * 0.54, fieldWidth * 0.54);
            }
        }

        if (!isOverview)
        {
            DrawTerrainDetails(context, rooms, offsets, fieldWidth);
        }
    }

    private void DrawTerrainDetails(
        DrawingContext context,
        List<(MapRoom Room, MapOffset Offset)> rooms,
        Dictionary<int, MapOffset> offsets,
        double fieldWidth)
    {
        if (_mapIndex is null)
        {
            return;
        }

        if (_zoom is >= 0.55 and <= 1.35 && rooms.Count <= 700)
        {
            using (context.PushOpacity(0.14))
            {
                foreach (var (room, offset) in rooms)
                {
                    if (room.Sector is null || _textureCache?.GetTexture(room.Sector) is not { } texture)
                    {
                        continue;
                    }

                    var center = WorldToScreen(room.Coordinates.X + offset.X * 0.6, room.Coordinates.Y + offset.Y * 0.6);
                    var size = fieldWidth * 1.05;
                    context.DrawImage(texture, new Rect(0, 0, texture.PixelSize.Width, texture.PixelSize.Height),
                        new Rect(center.X - size / 2, center.Y - size / 2, size, size));
                }
            }
        }

        var roadPen = new Pen(new SolidColorBrush(Color.FromRgb(205, 170, 105)), Math.Clamp(fieldWidth * 0.13, 1.2, 5))
        {
            LineCap = PenLineCap.Round,
        };
        var drawnRoads = new HashSet<(int, int)>();
        foreach (var (room, offset) in rooms.Where(item => IsRouteSector(item.Room)))
        {
            var from = WorldToScreen(room.Coordinates.X + offset.X * 0.6, room.Coordinates.Y + offset.Y * 0.6);
            foreach (var exit in room.Exits)
            {
                if (!_mapIndex.RoomsById.TryGetValue(exit.ExitId, out var target) ||
                    target.AreaId != _areaId || target.Coordinates.Z != _z || !IsRouteSector(target))
                {
                    continue;
                }

                var edge = room.Id < target.Id ? (room.Id, target.Id) : (target.Id, room.Id);
                if (!drawnRoads.Add(edge))
                {
                    continue;
                }

                var targetOffset = offsets.GetValueOrDefault(target.Id, MapOffset.Zero);
                var to = WorldToScreen(target.Coordinates.X + targetOffset.X * 0.6, target.Coordinates.Y + targetOffset.Y * 0.6);
                context.DrawLine(roadPen, from, to);
            }
        }
    }

    private void DrawCompass(DrawingContext context)
    {
        if (Bounds.Width < 180 || Bounds.Height < 180)
        {
            return;
        }

        var center = new Point(Bounds.Right - 39, Bounds.Bottom - 42);
        var brush = new SolidColorBrush(Color.FromArgb(150, 225, 213, 178));
        var pen = new Pen(brush, 1.5);
        context.DrawEllipse(null, pen, center, 19, 19);
        context.DrawLine(pen, new Point(center.X, center.Y - 25), new Point(center.X, center.Y + 19));
        context.DrawLine(pen, new Point(center.X - 19, center.Y), new Point(center.X + 19, center.Y));

        var north = new StreamGeometry();
        using (var geometry = north.Open())
        {
            geometry.BeginFigure(new Point(center.X, center.Y - 26), true);
            geometry.LineTo(new Point(center.X - 5, center.Y - 12));
            geometry.LineTo(new Point(center.X + 5, center.Y - 12));
            geometry.EndFigure(true);
        }
        context.DrawGeometry(brush, null, north);
    }

    private TerrainStyle GetTerrainStyle(MapRoom room)
    {
        if (_terrainStyles.TryGetValue(room.Id, out var style))
        {
            return style;
        }

        style = TerrainStyle.For(room.Sector, room.Name);
        _terrainStyles[room.Id] = style;
        return style;
    }

    private bool IsRouteSector(MapRoom room)
    {
        if (_routeSectors.TryGetValue(room.Id, out var isRoute))
        {
            return isRoute;
        }

        isRoute = TerrainStyle.IsRoute(room.Sector);
        _routeSectors[room.Id] = isRoute;
        return isRoute;
    }

    private sealed record TerrainStyle(IBrush Brush, int Priority)
    {
        private static readonly TerrainStyle Default = Style(72, 84, 67, 1);
        private static readonly TerrainStyle Forest = Style(36, 78, 48, 3);
        private static readonly TerrainStyle DeepForest = Style(24, 61, 42, 4);
        private static readonly TerrainStyle Grass = Style(82, 105, 58, 2);
        private static readonly TerrainStyle Sand = Style(151, 122, 70, 3);
        private static readonly TerrainStyle Mountain = Style(91, 92, 88, 5);
        private static readonly TerrainStyle Snow = Style(159, 177, 178, 6);
        private static readonly TerrainStyle Water = Style(29, 76, 104, 7);
        private static readonly TerrainStyle Swamp = Style(54, 72, 50, 5);
        private static readonly TerrainStyle City = Style(111, 91, 72, 6);
        private static readonly TerrainStyle Underground = Style(49, 45, 52, 6);
        private static readonly TerrainStyle Lava = Style(126, 43, 25, 8);

        public static TerrainStyle For(string? sector, string? roomName)
        {
            var value = (sector ?? string.Empty).Trim().ToLowerInvariant();
            if (value.Contains("lawa")) return Lava;
            if (value.Contains("ocean") || value.Contains("morze") || value.Contains("rzeka") ||
                value.Contains("jezioro") || value.Contains("woda")) return Water;
            if (value.Contains("lodowiec") || value.Contains("arkty") || value.Contains("tundra")) return Snow;
            if (value.Contains("gory") || value.Contains("gorska") || value.Contains("wzgorza")) return Mountain;
            if (value.Contains("pust") || value.Contains("wydmy") || value.Contains("piaski") || value.Contains("plaza")) return Sand;
            if (value.Contains("bagno") || value.Contains("blotna")) return Swamp;
            if (value.Contains("puszcza")) return DeepForest;
            if (value.Contains("las")) return Forest;
            if (value.Contains("miasto") || value.Contains("plac") || value.Contains("arena") || value.Contains("ruiny")) return City;
            if (value.Contains("podzi") || value.Contains("jaskinia") || value.Contains("kopalnia") || value.Contains("wewnatrz")) return Underground;
            if (value.Contains("pole") || value.Contains("laka") || value.Contains("trawa") || value.Contains("step")) return Grass;
            return roomName?.Contains("ulica", StringComparison.OrdinalIgnoreCase) == true ? City : Default;
        }

        public static bool IsRoute(string? sector)
        {
            var value = sector ?? string.Empty;
            return value.Contains("droga", StringComparison.OrdinalIgnoreCase) ||
                value.Contains("sciezka", StringComparison.OrdinalIgnoreCase);
        }

        private static TerrainStyle Style(byte r, byte g, byte b, int priority) =>
            new(new SolidColorBrush(Color.FromRgb(r, g, b)), priority);
    }

    private void RequestInvalidateVisual()
    {
        if (_invalidateQueued)
        {
            return;
        }

        _invalidateQueued = true;
        Dispatcher.UIThread.Post(() =>
        {
            _invalidateQueued = false;
            InvalidateVisual();
        }, DispatcherPriority.Background);
    }

    private void DrawCenteredMessage(DrawingContext context, string message)
    {
        var text = new FormattedText(
            message,
            System.Globalization.CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight,
            new Typeface(WidgetFontFamily, weight: WidgetFontWeight),
            WidgetFontSize,
            Brushes.LightGray);

        var origin = new Point((Bounds.Width - text.Width) / 2, (Bounds.Height - text.Height) / 2);
        context.DrawText(text, origin);
    }

    private void DrawExits(
        DrawingContext context,
        List<(MapRoom Room, MapOffset Offset)> rooms,
        Dictionary<int, MapOffset> offsets)
    {
        if (_mapIndex is null)
        {
            return;
        }

        var drawn = new HashSet<(int, int)>();
        var exitPen = _isSimpleMap
            ? new Pen(Brushes.Silver, Math.Clamp(_zoom, 0.45, 1.15))
            : ExitPen;

        foreach (var (room, offset) in rooms)
        {
            foreach (var exit in room.Exits)
            {
                if (!_mapIndex.RoomsById.TryGetValue(exit.ExitId, out var target))
                {
                    continue;
                }

                if (target.AreaId != room.AreaId || target.Coordinates.Z != room.Coordinates.Z)
                {
                    continue;
                }

                var edgeKey = room.Id < target.Id ? (room.Id, target.Id) : (target.Id, room.Id);
                if (!drawn.Add(edgeKey))
                {
                    continue;
                }

                var targetOffset = offsets.GetValueOrDefault(target.Id, MapOffset.Zero);

                var from = WorldToScreen(room.Coordinates.X + offset.X * 0.6, room.Coordinates.Y + offset.Y * 0.6);
                var to = WorldToScreen(
                    target.Coordinates.X + targetOffset.X * 0.6,
                    target.Coordinates.Y + targetOffset.Y * 0.6);

                context.DrawLine(exitPen, from, to);

                if (exit.HasDoor)
                {
                    var mid = new Point((from.X + to.X) / 2, (from.Y + to.Y) / 2);
                    var doorRect = new Rect(mid.X - 3, mid.Y - 3, 6, 6);
                    context.FillRectangle(Brushes.Peru, doorRect);
                    context.DrawRectangle(new Pen(Brushes.Black, 1), doorRect);
                }
            }
        }
    }

    private void DrawLowerLevelShadow(DrawingContext context)
    {
        var mapIndex = _mapIndex;
        if (mapIndex is null)
        {
            return;
        }

        var rooms = GetLowerLevelShadowRooms();
        if (rooms.Count == 0)
        {
            return;
        }

        var lowerZ = _z - 1;
        var offsets = rooms.ToDictionary(item => item.Room.Id, item => item.Offset);
        var drawnExits = new HashSet<(int, int)>();
        using var shadowEffect = context.PushEffect(LowerLevelBlurEffect, Bounds);

        foreach (var (room, offset) in rooms)
        {
            foreach (var exit in room.Exits)
            {
                if (!mapIndex.RoomsById.TryGetValue(exit.ExitId, out var target) ||
                    target.AreaId != _areaId || target.Coordinates.Z != lowerZ)
                {
                    continue;
                }

                var edgeKey = room.Id < target.Id ? (room.Id, target.Id) : (target.Id, room.Id);
                if (!drawnExits.Add(edgeKey))
                {
                    continue;
                }

                var targetOffset = offsets.GetValueOrDefault(target.Id, MapOffset.Zero);
                var from = WorldToScreen(
                    room.Coordinates.X + offset.X * 0.6,
                    room.Coordinates.Y + offset.Y * 0.6) + LowerLevelShadowOffset;
                var to = WorldToScreen(
                    target.Coordinates.X + targetOffset.X * 0.6,
                    target.Coordinates.Y + targetOffset.Y * 0.6) + LowerLevelShadowOffset;
                context.DrawLine(LowerLevelExitPen, from, to);
            }
        }

        var roomSize = Math.Max(_settings.RoomSize * _zoom, 2);
        foreach (var (room, offset) in rooms)
        {
            var center = WorldToScreen(
                room.Coordinates.X + offset.X * 0.6,
                room.Coordinates.Y + offset.Y * 0.6) + LowerLevelShadowOffset;
            var rect = new Rect(center.X - roomSize / 2, center.Y - roomSize / 2, roomSize, roomSize);
            context.DrawRectangle(LowerLevelRoomBrush, LowerLevelRoomPen, rect);
        }
    }

    internal IReadOnlyList<(MapRoom Room, MapOffset Offset)> GetLowerLevelShadowRooms()
    {
        var lowerZ = _z - 1;
        if (_mapIndex is null || !_mapIndex.RoomsByAreaAndZ.ContainsKey((_areaId, lowerZ)))
        {
            return [];
        }

        return GetVisibleRooms(lowerZ, includeExpandedGroups: false).ToList();
    }

    private void DrawRooms(DrawingContext context, List<(MapRoom Room, MapOffset Offset)> rooms)
    {
        var roomSize = Math.Max(_settings.RoomSize * _zoom, 2);
        foreach (var (room, offset) in rooms)
        {
            var center = WorldToScreen(room.Coordinates.X + offset.X * 0.6, room.Coordinates.Y + offset.Y * 0.6);
            var rect = new Rect(center.X - roomSize / 2, center.Y - roomSize / 2, roomSize, roomSize);

            var texture = _isSimpleMap
                ? null
                : _roomImages?.GetMapIcon(room.Vnum)
                    ?? (room.Sector is not null ? _textureCache?.GetTexture(room.Sector) : null);

            if (texture is not null)
            {
                context.DrawImage(texture, new Rect(0, 0, texture.PixelSize.Width, texture.PixelSize.Height), rect);
            }
            else
            {
                context.FillRectangle(
                    _isSimpleMap ? TerrainStyle.For(room.Sector, roomName: null).Brush : Brushes.SlateGray,
                    rect);
            }

            context.DrawRectangle(null, new Pen(Brushes.Black, 1), rect);

            var group = _mapIndex?.GetCollisionGroup(room);
            if (group is { HasCollision: true } && !_expandedGroups.Contains(group.Cell) &&
                room.Id == group.Rooms.Min(r => r.Id))
            {
                if (!_collisionBadges.TryGetValue(group.Rooms.Count, out var badgeText))
                {
                    badgeText = new FormattedText(
                        group.Rooms.Count.ToString(),
                        System.Globalization.CultureInfo.CurrentUICulture,
                        FlowDirection.LeftToRight,
                        new Typeface(WidgetFontFamily, weight: WidgetFontWeight),
                        WidgetFontSize,
                        Brushes.White);
                    _collisionBadges[group.Rooms.Count] = badgeText;
                }

                var badgeRect = new Rect(rect.Right - 14, rect.Top - 2, 14, 14);
                context.FillRectangle(Brushes.DarkRed, badgeRect);
                context.DrawText(badgeText, new Point(badgeRect.X + 2, badgeRect.Y));
            }

        }
    }

    private void DrawRoute(DrawingContext context, Dictionary<int, MapOffset> offsets)
    {
        if (_route is not { Count: > 0 } route)
        {
            return;
        }

        Point RoomCenter(MapRoom room)
        {
            var offset = offsets.GetValueOrDefault(room.Id, MapOffset.Zero);
            return WorldToScreen(room.Coordinates.X + offset.X * 0.6, room.Coordinates.Y + offset.Y * 0.6);
        }

        bool OnThisLayer(MapRoom room) =>
            room.AreaId == _areaId && room.Coordinates.Z == _z;

        for (var i = 1; i < route.Count; i++)
        {
            var from = route[i - 1];
            var to = route[i];

            // Segments that leave the visible area/level (portals, stairs)
            // are skipped; the route resumes where it re-enters this layer.
            if (!OnThisLayer(from) || !OnThisLayer(to))
            {
                continue;
            }

            context.DrawLine(RoutePen, RoomCenter(from), RoomCenter(to));
        }

        var target = route[^1];
        if (OnThisLayer(target))
        {
            var roomSize = Math.Max(_settings.RoomSize * _zoom, 2);
            var center = RoomCenter(target);
            var radius = roomSize / 2 + 5;
            context.DrawEllipse(null, RouteTargetPen, center, radius, radius);
        }
    }

    private void DrawSelectionAndCurrent(DrawingContext context, List<(MapRoom Room, MapOffset Offset)> rooms)
    {
        var roomSize = Math.Max(_settings.RoomSize * _zoom, 2);

        foreach (var (room, offset) in rooms)
        {
            var center = WorldToScreen(room.Coordinates.X + offset.X * 0.6, room.Coordinates.Y + offset.Y * 0.6);
            var rect = new Rect(center.X - roomSize / 2, center.Y - roomSize / 2, roomSize, roomSize);

            if (_selectedRoom is not null && room.Id == _selectedRoom.Id)
            {
                DrawInsideOutline(context, rect, Brushes.Gold, 2);
            }

            if (_currentRoom is not null && room.Id == _currentRoom.Id)
            {
                DrawInsideOutline(context, rect, Brushes.LimeGreen, 3);
                DrawInsideOutline(context, rect, Brushes.White, 1, 3);
            }
        }
    }

    private void DrawGroupMarkers(DrawingContext context, IReadOnlyDictionary<int, MapOffset> offsets)
    {
        var visibleMarkers = _groupMarkers
            .Where(marker => marker.Room.AreaId == _areaId && marker.Room.Coordinates.Z == _z)
            .GroupBy(marker => marker.Room.Id);

        foreach (var roomMarkers in visibleMarkers)
        {
            var markers = roomMarkers.ToArray();
            var room = markers[0].Room;
            var roomOffset = offsets.GetValueOrDefault(room.Id, MapOffset.Zero);
            var center = WorldToScreen(
                room.Coordinates.X + roomOffset.X * 0.6,
                room.Coordinates.Y + roomOffset.Y * 0.6);

            var labelFontSize = Math.Max(9, WidgetFontSize - 2);
            var labelTypeface = new Typeface(WidgetFontFamily, FontStyle.Normal, FontWeight.Bold);
            var labelBottom = center.Y - Math.Max(_settings.RoomSize * _zoom / 2, 5) - 5;

            for (var index = 0; index < markers.Length; index++)
            {
                var marker = markers[index];
                var brush = marker.IsLeader ? Brushes.Gold : Brushes.DeepSkyBlue;
                var name = new FormattedText(
                    marker.Name,
                    System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    labelTypeface,
                    labelFontSize,
                    brush);

                const double markerRadius = 4;
                const double horizontalPadding = 4;
                const double markerGap = 4;
                var labelHeight = Math.Max(14, name.Height + 4);
                var labelWidth = horizontalPadding + markerRadius * 2 + markerGap + name.Width + horizontalPadding;
                var labelRect = new Rect(
                    center.X - labelWidth / 2,
                    labelBottom - (index + 1) * (labelHeight + 2),
                    labelWidth,
                    labelHeight);

                context.DrawRectangle(
                    new SolidColorBrush(Color.FromArgb(220, 5, 8, 12)),
                    new Pen(brush, 1),
                    labelRect);

                var markerCenter = new Point(
                    labelRect.X + horizontalPadding + markerRadius,
                    labelRect.Center.Y);
                context.DrawEllipse(brush, null, markerCenter, markerRadius, markerRadius);
                context.DrawText(
                    name,
                    new Point(
                        markerCenter.X + markerRadius + markerGap,
                        labelRect.Y + (labelRect.Height - name.Height) / 2));
            }
        }
    }

    private static void DrawInsideOutline(
        DrawingContext context,
        Rect rect,
        IBrush brush,
        double thickness,
        double inset = 0)
    {
        var strokeInset = inset + thickness / 2;
        if (rect.Width <= strokeInset * 2 || rect.Height <= strokeInset * 2)
        {
            return;
        }

        var outline = new Rect(
            rect.X + strokeInset,
            rect.Y + strokeInset,
            rect.Width - strokeInset * 2,
            rect.Height - strokeInset * 2);
        context.DrawRectangle(null, new Pen(brush, thickness), outline);
    }
}

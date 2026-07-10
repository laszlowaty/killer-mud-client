using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using MudClient.App.Services;
using MudClient.Core.Map;

namespace MudClient.App.Controls;

public sealed class WorldMapControl : Control
{
    private const double PanKeyStep = 40;

    private static readonly Pen ExitPen = new(Brushes.Silver, 2.5);

    private readonly CollisionLayoutService _collisionLayout = new();
    private readonly HashSet<MapCellKey> _expandedGroups = [];

    private MapIndex? _mapIndex;
    private MapSettings _settings = MapSettings.CreateDefault();
    private SectorTextureCache? _textureCache;
    private RoomImageCache? _roomImages;

    private int _areaId;
    private double _z;
    private MapRoom? _currentRoom;
    private MapRoom? _selectedRoom;

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

    public event Action? ManualNavigationOccurred;

    public MapIndex? MapIndex
    {
        get => _mapIndex;
        set
        {
            _mapIndex = value;
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

    public Point WorldToScreen(double worldX, double worldY)
    {
        var bounds = Bounds;
        var centerX = bounds.Width / 2;
        var centerY = bounds.Height / 2;
        var scale = _settings.PixelsPerCoordinateUnit * _zoom;

        var screenX = centerX + (worldX - _cameraX) * scale;
        var screenY = centerY - (worldY - _cameraY) * scale;
        return new Point(screenX, screenY);
    }

    public Point ScreenToWorld(Point screenPoint)
    {
        var bounds = Bounds;
        var centerX = bounds.Width / 2;
        var centerY = bounds.Height / 2;
        var scale = _settings.PixelsPerCoordinateUnit * _zoom;

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

        var scale = _settings.PixelsPerCoordinateUnit * _zoom;
        _cameraX = _dragStartCameraX - deltaX / scale;
        _cameraY = _dragStartCameraY + deltaY / scale;

        ManualNavigationOccurred?.Invoke();
        InvalidateVisual();
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

        var scale = _settings.PixelsPerCoordinateUnit * _zoom;
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

    private IEnumerable<(MapRoom Room, MapOffset Offset)> GetVisibleRooms()
    {
        if (_mapIndex is null)
        {
            yield break;
        }

        var bounds = GetVisibleWorldBounds();
        var margin = _settings.RoomSize / Math.Max(_settings.PixelsPerCoordinateUnit * _zoom, 0.001) * 2;

        var rooms = _mapIndex.GetRoomsInBounds(
            _areaId,
            _z,
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

            var isExpanded = _expandedGroups.Contains(group.Cell) ||
                (_currentRoom is not null && group.Rooms.Any(r => r.Id == _currentRoom.Id));

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
        context.FillRectangle(Brushes.Black, bounds);

        if (_mapIndex is null)
        {
            DrawCenteredMessage(context, "Ładowanie mapy…");
            return;
        }

        var roomsWithOffsets = GetVisibleRooms().ToList();
        var roomLookup = roomsWithOffsets.ToDictionary(r => r.Room.Id, r => r.Offset);

        DrawExits(context, roomsWithOffsets, roomLookup);
        DrawRooms(context, roomsWithOffsets);
        DrawSelectionAndCurrent(context, roomsWithOffsets);
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
            Typeface.Default,
            16,
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

                context.DrawLine(ExitPen, from, to);

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

    private void DrawRooms(DrawingContext context, List<(MapRoom Room, MapOffset Offset)> rooms)
    {
        var roomSize = Math.Max(_settings.RoomSize * _zoom, 2);
        var showLabels = _zoom > 1.2;

        foreach (var (room, offset) in rooms)
        {
            var center = WorldToScreen(room.Coordinates.X + offset.X * 0.6, room.Coordinates.Y + offset.Y * 0.6);
            var rect = new Rect(center.X - roomSize / 2, center.Y - roomSize / 2, roomSize, roomSize);

            var texture = _roomImages?.GetMapIcon(room.Vnum)
                ?? (room.Sector is not null ? _textureCache?.GetTexture(room.Sector) : null);

            if (texture is not null)
            {
                context.DrawImage(texture, new Rect(0, 0, texture.PixelSize.Width, texture.PixelSize.Height), rect);
            }
            else
            {
                context.FillRectangle(Brushes.SlateGray, rect);
            }

            context.DrawRectangle(null, new Pen(Brushes.Black, 1), rect);

            var group = _mapIndex?.GetCollisionGroup(room);
            if (group is { HasCollision: true } && !_expandedGroups.Contains(group.Cell) &&
                room.Id == group.Rooms.Min(r => r.Id))
            {
                var badgeText = new FormattedText(
                    group.Rooms.Count.ToString(),
                    System.Globalization.CultureInfo.CurrentUICulture,
                    FlowDirection.LeftToRight,
                    Typeface.Default,
                    11,
                    Brushes.White);

                var badgeRect = new Rect(rect.Right - 14, rect.Top - 2, 14, 14);
                context.FillRectangle(Brushes.DarkRed, badgeRect);
                context.DrawText(badgeText, new Point(badgeRect.X + 2, badgeRect.Y));
            }

            if (showLabels && !string.IsNullOrEmpty(room.Name))
            {
                var label = new FormattedText(
                    room.Name,
                    System.Globalization.CultureInfo.CurrentUICulture,
                    FlowDirection.LeftToRight,
                    Typeface.Default,
                    11,
                    Brushes.White);

                context.DrawText(label, new Point(rect.X, rect.Bottom + 1));
            }
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
                context.DrawRectangle(null, new Pen(Brushes.Gold, 2), rect.Inflate(2));
            }

            if (_currentRoom is not null && room.Id == _currentRoom.Id)
            {
                context.DrawRectangle(null, new Pen(Brushes.LimeGreen, 3), rect.Inflate(4));
                context.DrawRectangle(null, new Pen(Brushes.White, 1), rect.Inflate(1));
            }
        }
    }
}

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using System.Globalization;

namespace MudClient.MapImageCalibrator;

public enum CalibrationMode { MoveImage, SelectRooms, SelectRoomsLasso, MoveSelectedRooms, MoveSingleRoom, AddMarker }

public sealed class CalibrationCanvas : Control
{
    private static readonly Pen ExitPen = new(new SolidColorBrush(Color.FromArgb(150, 220, 220, 210)), 1);
    private static readonly Pen AnchorPen = new(Brushes.OrangeRed, 2);
    private Point? _dragPoint;
    private bool _dragView;
    private Point? _selectionStart;
    private Point? _selectionCurrent;
    private readonly List<Point> _lassoPoints = [];
    private int? _draggedRoomId;
    private double _cameraX;
    private double _cameraY;
    private double _scale = 18;

    public LocationLayer? Layer { get; set; }
    public Bitmap? Image { get; set; }
    public IReadOnlyList<RoomPoint> Rooms { get; set; } = [];
    public IReadOnlyList<CalibrationAnchor> Anchors { get; set; } = [];
    public CalibrationMode Mode { get; set; } = CalibrationMode.MoveImage;
    public Dictionary<int, Point> RoomOffsets { get; set; } = [];
    public HashSet<int> SelectedRoomIds { get; set; } = [];
    public IReadOnlyList<ImageMarker> Markers { get; set; } = [];

    public event Action? LayerChanged;
    public event Action<Rect>? ChunkSelected;
    public event Action<IReadOnlyList<Point>>? LassoSelected;
    public event Action<Point>? MarkerPointPicked;
    public event Action? RoomOffsetsChanged;

    public CalibrationCanvas()
    {
        ClipToBounds = true;
        Focusable = true;
        PointerPressed += OnPointerPressed;
        PointerMoved += OnPointerMoved;
        PointerReleased += OnPointerReleased;
        PointerWheelChanged += OnPointerWheel;
    }

    public void Fit()
    {
        if (Layer is null || Bounds.Width <= 0 || Bounds.Height <= 0) return;
        _cameraX = (Layer.MinX + Layer.MaxX) / 2;
        _cameraY = (Layer.MinY + Layer.MaxY) / 2;
        _scale = Math.Max(0.05, Math.Min(Bounds.Width / (Layer.Width * 1.15), Bounds.Height / (Layer.Height * 1.15)));
        InvalidateVisual();
    }

    public void FitRooms(IReadOnlyList<RoomPoint> rooms)
    {
        if (rooms.Count == 0 || Bounds.Width <= 0 || Bounds.Height <= 0) return;
        var minX = rooms.Min(room => room.X);
        var maxX = rooms.Max(room => room.X);
        var minY = rooms.Min(room => room.Y);
        var maxY = rooms.Max(room => room.Y);
        var width = Math.Max(maxX - minX, 4);
        var height = Math.Max(maxY - minY, 4);
        _cameraX = (minX + maxX) / 2;
        _cameraY = (minY + maxY) / 2;
        _scale = Math.Max(0.05, Math.Min(Bounds.Width / (width * 1.35), Bounds.Height / (height * 1.35)));
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        context.FillRectangle(new SolidColorBrush(Color.FromRgb(15, 18, 17)), Bounds);
        if (Layer is null || Image is null) return;

        var imageRect = new Rect(WorldToScreen(Layer.MinX, Layer.MaxY), WorldToScreen(Layer.MaxX, Layer.MinY));
        var sourceRect = new Rect(0, 0, Image.PixelSize.Width, Image.PixelSize.Height);
        // Opacity 0..1 controls coverage; values above 1 additionally brighten the
        // image toward white (a "rozjaśnienie" wash proportional to Opacity - 1).
        var coverage = Math.Min(1.0, Layer.Opacity);
        var brighten = Math.Clamp(Layer.Opacity - 1.0, 0.0, 1.0);
        using (context.PushOpacity(coverage))
        {
            if (Layer.EdgeFade > 0)
            {
                var masks = CreateEdgeMasks(Layer.EdgeFade);
                using var horizontal = context.PushOpacityMask(masks.Horizontal, imageRect);
                using var vertical = context.PushOpacityMask(masks.Vertical, imageRect);
                context.DrawImage(Image, sourceRect, imageRect);
            }
            else
            {
                context.DrawImage(Image, sourceRect, imageRect);
            }
        }
        if (brighten > 0)
        {
            var wash = new SolidColorBrush(Colors.White, brighten);
            if (Layer.EdgeFade > 0)
            {
                var masks = CreateEdgeMasks(Layer.EdgeFade);
                using var horizontal = context.PushOpacityMask(masks.Horizontal, imageRect);
                using var vertical = context.PushOpacityMask(masks.Vertical, imageRect);
                context.FillRectangle(wash, imageRect);
            }
            else
            {
                context.FillRectangle(wash, imageRect);
            }
        }
        context.DrawRectangle(new Pen(new SolidColorBrush(Color.FromArgb(210, 255, 180, 30)), 2), imageRect);

        var visibleRooms = Rooms.Where(room => room.AreaId == Layer.AreaId && room.Z == Layer.Z).ToList();
        var byId = visibleRooms.ToDictionary(room => room.Id);
        foreach (var room in visibleRooms)
        {
            var from = RoomToScreen(room);
            foreach (var exitId in room.Exits)
            {
                if (room.Id < exitId && byId.TryGetValue(exitId, out var target))
                    context.DrawLine(ExitPen, from, RoomToScreen(target));
            }
        }
        foreach (var room in visibleRooms)
        {
            var point = RoomToScreen(room);
            var brush = SelectedRoomIds.Contains(room.Id) ? Brushes.Orange : room.Vnum is null ? Brushes.Gray : Brushes.White;
            var size = SelectedRoomIds.Contains(room.Id) ? 8 : 5;
            context.FillRectangle(brush, new Rect(point.X - size / 2, point.Y - size / 2, size, size));
        }

        foreach (var anchor in Anchors)
        {
            var imageWorldX = Layer.MinX + anchor.ImageX / Image.PixelSize.Width * Layer.Width;
            var imageWorldY = Layer.MaxY - anchor.ImageY / Image.PixelSize.Height * Layer.Height;
            var imagePoint = WorldToScreen(imageWorldX, imageWorldY);
            var roomPoint = WorldToScreen(anchor.WorldX, anchor.WorldY);
            context.DrawLine(AnchorPen, imagePoint, roomPoint);
            context.DrawEllipse(Brushes.Gold, null, imagePoint, 5, 5);
            context.DrawEllipse(Brushes.LimeGreen, null, roomPoint, 5, 5);
        }

        foreach (var marker in Markers)
        {
            var worldX = Layer.MinX + marker.ImageX / Image.PixelSize.Width * Layer.Width;
            var worldY = Layer.MaxY - marker.ImageY / Image.PixelSize.Height * Layer.Height;
            var point = WorldToScreen(worldX, worldY);
            context.DrawEllipse(Brushes.Magenta, new Pen(Brushes.White, 2), point, 11, 11);
            var text = new FormattedText(marker.Number.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, Typeface.Default, 12, Brushes.White);
            context.DrawText(text, new Point(point.X - text.Width / 2, point.Y - text.Height / 2));
        }

        if (_selectionStart is { } start && _selectionCurrent is { } current)
        {
            var selection = RectFromPoints(start, current);
            context.FillRectangle(new SolidColorBrush(Color.FromArgb(40, 255, 190, 40)), selection);
            context.DrawRectangle(new Pen(Brushes.Orange, 2), selection);
        }
        if (_lassoPoints.Count > 1)
        {
            for (var index = 1; index < _lassoPoints.Count; index++)
                context.DrawLine(new Pen(Brushes.DeepSkyBlue, 2), _lassoPoints[index - 1], _lassoPoints[index]);
        }
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        Focus();
        var point = e.GetPosition(this);
        var properties = e.GetCurrentPoint(this).Properties;
        if (properties.IsRightButtonPressed)
        {
            _dragView = true;
            _dragPoint = point;
            return;
        }
        if (!properties.IsLeftButtonPressed || Layer is null || Image is null) return;
        _dragView = false;

        if (Mode == CalibrationMode.SelectRooms)
        {
            _selectionStart = point;
            _selectionCurrent = point;
            InvalidateVisual();
            return;
        }
        if (Mode == CalibrationMode.SelectRoomsLasso)
        {
            _lassoPoints.Clear();
            _lassoPoints.Add(point);
            InvalidateVisual();
            return;
        }
        if (Mode == CalibrationMode.AddMarker)
        {
            var world = ScreenToWorld(point);
            var x = (world.X - Layer.MinX) / Layer.Width * Image.PixelSize.Width;
            var y = (Layer.MaxY - world.Y) / Layer.Height * Image.PixelSize.Height;
            if (x >= 0 && y >= 0 && x <= Image.PixelSize.Width && y <= Image.PixelSize.Height)
                MarkerPointPicked?.Invoke(new Point(x, y));
            return;
        }
        if (Mode == CalibrationMode.MoveSingleRoom)
        {
            var nearest = FindNearestRoom(point);
            if (nearest is null) return;
            SelectedRoomIds.Clear();
            SelectedRoomIds.Add(nearest.Id);
            _draggedRoomId = nearest.Id;
            _dragPoint = point;
            InvalidateVisual();
            return;
        }
        if (Mode == CalibrationMode.MoveSelectedRooms)
        {
            if (SelectedRoomIds.Count == 0) return;
            _dragPoint = point;
            return;
        }

        _dragView = false;
        _dragPoint = point;
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_selectionStart is not null && Mode == CalibrationMode.SelectRooms)
        {
            _selectionCurrent = e.GetPosition(this);
            InvalidateVisual();
            return;
        }
        if (_lassoPoints.Count > 0 && Mode == CalibrationMode.SelectRoomsLasso)
        {
            var point = e.GetPosition(this);
            if (Distance(_lassoPoints[^1], point) >= 4) _lassoPoints.Add(point);
            InvalidateVisual();
            return;
        }
        if (_dragPoint is not { } previous || Layer is null) return;
        var current = e.GetPosition(this);
        var dx = (current.X - previous.X) / _scale;
        var dy = -(current.Y - previous.Y) / _scale;
        if (_dragView)
        {
            _cameraX -= dx;
            _cameraY -= dy;
        }
        else if (Mode == CalibrationMode.MoveSelectedRooms || Mode == CalibrationMode.MoveSingleRoom)
        {
            var ids = Mode == CalibrationMode.MoveSingleRoom && _draggedRoomId is { } id ? [id] : SelectedRoomIds.ToArray();
            foreach (var roomId in ids)
            {
                var old = RoomOffsets.GetValueOrDefault(roomId, default);
                RoomOffsets[roomId] = new Point(old.X + dx, old.Y + dy);
            }
            RoomOffsetsChanged?.Invoke();
        }
        else if (Mode == CalibrationMode.MoveImage)
        {
            Layer.MinX += dx; Layer.MaxX += dx;
            Layer.MinY += dy; Layer.MaxY += dy;
            LayerChanged?.Invoke();
        }
        _dragPoint = current;
        InvalidateVisual();
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _dragPoint = null;
        _draggedRoomId = null;
        if (_lassoPoints.Count > 2 && Mode == CalibrationMode.SelectRoomsLasso)
        {
            var worldPoints = _lassoPoints.Select(ScreenToWorld).ToList();
            _lassoPoints.Clear();
            InvalidateVisual();
            LassoSelected?.Invoke(worldPoints);
            return;
        }
        _lassoPoints.Clear();
        if (_selectionStart is not { } start || _selectionCurrent is not { } current) return;
        var a = ScreenToWorld(start);
        var b = ScreenToWorld(current);
        _selectionStart = null;
        _selectionCurrent = null;
        InvalidateVisual();
        if (Math.Abs(start.X - current.X) < 8 || Math.Abs(start.Y - current.Y) < 8) return;
        ChunkSelected?.Invoke(new Rect(
            Math.Min(a.X, b.X), Math.Min(a.Y, b.Y),
            Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y)));
    }

    private void OnPointerWheel(object? sender, PointerWheelEventArgs e)
    {
        var cursor = e.GetPosition(this);
        var before = ScreenToWorld(cursor);
        _scale = Math.Clamp(_scale * Math.Pow(1.15, e.Delta.Y), 0.03, 400);
        var after = ScreenToWorld(cursor);
        _cameraX += before.X - after.X;
        _cameraY += before.Y - after.Y;
        InvalidateVisual();
    }

    private Point WorldToScreen(double x, double y) =>
        new(Bounds.Width / 2 + (x - _cameraX) * _scale, Bounds.Height / 2 - (y - _cameraY) * _scale);
    private Point ScreenToWorld(Point p) =>
        new(_cameraX + (p.X - Bounds.Width / 2) / _scale, _cameraY - (p.Y - Bounds.Height / 2) / _scale);
    private static double Distance(Point a, Point b) => Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2));
    private static Rect RectFromPoints(Point a, Point b) =>
        new(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));
    private Point RoomToScreen(RoomPoint room)
    {
        var offset = RoomOffsets.GetValueOrDefault(room.Id, default);
        return WorldToScreen(room.X + offset.X, room.Y + offset.Y);
    }
    private RoomPoint? FindNearestRoom(Point point)
    {
        var nearest = Rooms.Select(room => (Room: room, Distance: Distance(RoomToScreen(room), point)))
            .OrderBy(item => item.Distance).FirstOrDefault();
        return nearest.Room is not null && nearest.Distance <= 24 ? nearest.Room : null;
    }

    private static (IBrush Horizontal, IBrush Vertical) CreateEdgeMasks(double fade)
    {
        static GradientStops Stops(double edge) =>
        [
            new GradientStop(Colors.Transparent, 0),
            new GradientStop(Colors.White, edge),
            new GradientStop(Colors.White, 1 - edge),
            new GradientStop(Colors.Transparent, 1),
        ];
        return (
            new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0.5, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 0.5, RelativeUnit.Relative),
                GradientStops = Stops(fade),
            },
            new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0.5, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(0.5, 1, RelativeUnit.Relative),
                GradientStops = Stops(fade),
            });
    }
}

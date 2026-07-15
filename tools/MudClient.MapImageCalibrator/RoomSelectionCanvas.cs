using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace MudClient.MapImageCalibrator;

public enum RoomSelectionMode
{
    Toggle,
    Rectangle,
    Lasso,
}

public sealed class RoomSelectionCanvas : Control
{
    private Point? _dragPoint;
    private bool _panning;
    private Point? _selectionStart;
    private Point? _selectionCurrent;
    private readonly List<Point> _lasso = [];
    private double _cameraX;
    private double _cameraY;
    private double _scale = 12;

    public IReadOnlyList<RoomPoint> Rooms { get; set; } = [];
    public HashSet<int> SelectedRoomIds { get; } = [];
    public RoomSelectionMode Mode { get; set; } = RoomSelectionMode.Toggle;

    public event Action? SelectionChanged;

    public RoomSelectionCanvas()
    {
        ClipToBounds = true;
        Focusable = true;
        PointerPressed += OnPointerPressed;
        PointerMoved += OnPointerMoved;
        PointerReleased += OnPointerReleased;
        PointerWheelChanged += OnPointerWheel;
    }

    public void SetRooms(IReadOnlyList<RoomPoint> rooms)
    {
        Rooms = rooms;
        SelectedRoomIds.Clear();
        Fit();
        SelectionChanged?.Invoke();
    }

    public void SelectAll()
    {
        foreach (var room in Rooms) SelectedRoomIds.Add(room.Id);
        SelectionChanged?.Invoke();
        InvalidateVisual();
    }

    public void ClearSelection()
    {
        SelectedRoomIds.Clear();
        SelectionChanged?.Invoke();
        InvalidateVisual();
    }

    public IReadOnlyList<RoomPoint> GetSelectedRooms() =>
        Rooms.Where(room => SelectedRoomIds.Contains(room.Id)).ToList();

    public void Fit()
    {
        if (Rooms.Count == 0 || Bounds.Width <= 0 || Bounds.Height <= 0) return;
        var minX = Rooms.Min(room => room.X);
        var maxX = Rooms.Max(room => room.X);
        var minY = Rooms.Min(room => room.Y);
        var maxY = Rooms.Max(room => room.Y);
        var width = Math.Max(maxX - minX, 4);
        var height = Math.Max(maxY - minY, 4);
        _cameraX = (minX + maxX) / 2;
        _cameraY = (minY + maxY) / 2;
        _scale = Math.Max(0.03, Math.Min(Bounds.Width / (width * 1.12), Bounds.Height / (height * 1.12)));
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        context.FillRectangle(new SolidColorBrush(Color.FromRgb(15, 18, 17)), Bounds);
        if (Rooms.Count == 0) return;

        var byId = Rooms.ToDictionary(room => room.Id);
        var exitPen = new Pen(new SolidColorBrush(Color.FromArgb(120, 190, 195, 185)), 1);
        foreach (var room in Rooms)
        {
            foreach (var exitId in room.Exits)
            {
                if (room.Id < exitId && byId.TryGetValue(exitId, out var target))
                    context.DrawLine(exitPen, WorldToScreen(room.X, room.Y), WorldToScreen(target.X, target.Y));
            }
        }

        foreach (var room in Rooms)
        {
            var point = WorldToScreen(room.X, room.Y);
            var selected = SelectedRoomIds.Contains(room.Id);
            var size = selected ? 9 : 6;
            var rect = new Rect(point.X - size / 2d, point.Y - size / 2d, size, size);
            context.FillRectangle(selected ? Brushes.Orange : Brushes.SlateGray, rect);
            context.DrawRectangle(new Pen(Brushes.Black, selected ? 2 : 1), rect);
        }

        if (_selectionStart is { } start && _selectionCurrent is { } current)
        {
            var rect = RectFromPoints(start, current);
            context.FillRectangle(new SolidColorBrush(Color.FromArgb(45, 255, 180, 30)), rect);
            context.DrawRectangle(new Pen(Brushes.Orange, 2), rect);
        }

        for (var index = 1; index < _lasso.Count; index++)
            context.DrawLine(new Pen(Brushes.DeepSkyBlue, 2), _lasso[index - 1], _lasso[index]);
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs args)
    {
        Focus();
        var point = args.GetPosition(this);
        var properties = args.GetCurrentPoint(this).Properties;
        if (properties.IsLeftButtonPressed)
        {
            _panning = true;
            _dragPoint = point;
            return;
        }

        if (!properties.IsRightButtonPressed) return;
        if (Mode == RoomSelectionMode.Toggle)
        {
            var nearest = FindNearest(point);
            if (nearest is null) return;
            if (!SelectedRoomIds.Add(nearest.Id)) SelectedRoomIds.Remove(nearest.Id);
            SelectionChanged?.Invoke();
            InvalidateVisual();
        }
        else if (Mode == RoomSelectionMode.Rectangle)
        {
            _selectionStart = point;
            _selectionCurrent = point;
        }
        else
        {
            _lasso.Clear();
            _lasso.Add(point);
        }
    }

    private void OnPointerMoved(object? sender, PointerEventArgs args)
    {
        var point = args.GetPosition(this);
        if (_selectionStart is not null)
        {
            _selectionCurrent = point;
            InvalidateVisual();
            return;
        }
        if (_lasso.Count > 0)
        {
            if (Distance(_lasso[^1], point) >= 4) _lasso.Add(point);
            InvalidateVisual();
            return;
        }
        if (!_panning || _dragPoint is not { } previous) return;
        _cameraX -= (point.X - previous.X) / _scale;
        _cameraY += (point.Y - previous.Y) / _scale;
        _dragPoint = point;
        InvalidateVisual();
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs args)
    {
        _panning = false;
        _dragPoint = null;
        if (_selectionStart is { } start && _selectionCurrent is { } current)
        {
            var selection = RectFromPoints(start, current);
            SelectWhere(room => selection.Contains(WorldToScreen(room.X, room.Y)));
            _selectionStart = null;
            _selectionCurrent = null;
        }
        else if (_lasso.Count > 2)
        {
            var polygon = _lasso.ToArray();
            SelectWhere(room => IsInsidePolygon(WorldToScreen(room.X, room.Y), polygon));
            _lasso.Clear();
        }
        InvalidateVisual();
    }

    private void OnPointerWheel(object? sender, PointerWheelEventArgs args)
    {
        var cursor = args.GetPosition(this);
        var before = ScreenToWorld(cursor);
        _scale = Math.Clamp(_scale * Math.Pow(1.15, args.Delta.Y), 0.02, 500);
        var after = ScreenToWorld(cursor);
        _cameraX += before.X - after.X;
        _cameraY += before.Y - after.Y;
        InvalidateVisual();
    }

    private void SelectWhere(Func<RoomPoint, bool> predicate)
    {
        foreach (var room in Rooms.Where(predicate)) SelectedRoomIds.Add(room.Id);
        SelectionChanged?.Invoke();
    }

    private RoomPoint? FindNearest(Point point) => Rooms
        .Select(room => (Room: room, Distance: Distance(point, WorldToScreen(room.X, room.Y))))
        .Where(item => item.Distance <= 18)
        .OrderBy(item => item.Distance)
        .Select(item => item.Room)
        .FirstOrDefault();

    private Point WorldToScreen(double x, double y) =>
        new(Bounds.Width / 2 + (x - _cameraX) * _scale, Bounds.Height / 2 - (y - _cameraY) * _scale);

    private Point ScreenToWorld(Point point) =>
        new(_cameraX + (point.X - Bounds.Width / 2) / _scale, _cameraY - (point.Y - Bounds.Height / 2) / _scale);

    private static Rect RectFromPoints(Point a, Point b) =>
        new(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));

    private static double Distance(Point a, Point b) =>
        Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2));

    private static bool IsInsidePolygon(Point point, IReadOnlyList<Point> polygon)
    {
        var inside = false;
        for (var i = 0; i < polygon.Count; i++)
        {
            var j = i == 0 ? polygon.Count - 1 : i - 1;
            if ((polygon[i].Y > point.Y) != (polygon[j].Y > point.Y) &&
                point.X < (polygon[j].X - polygon[i].X) * (point.Y - polygon[i].Y) /
                    (polygon[j].Y - polygon[i].Y) + polygon[i].X)
                inside = !inside;
        }
        return inside;
    }
}

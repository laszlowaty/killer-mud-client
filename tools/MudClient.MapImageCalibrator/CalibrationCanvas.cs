using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using System.Globalization;

namespace MudClient.MapImageCalibrator;

public enum CalibrationMode { MoveImage, EditElements, SelectRooms, SelectRoomsLasso, MoveSelectedRooms, MoveSingleRoom, AddMarker }

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
    private string? _draggedElementId;
    private RoomPoint? _hoverRoom;
    private Point _hoverPoint;
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
    public IReadOnlyList<MapImageElement> ImageElements { get; set; } = [];
    public IReadOnlyDictionary<string, Bitmap> ElementBitmaps { get; set; } = new Dictionary<string, Bitmap>();
    public string? SelectedElementId { get; set; }
    public bool CanAcceptAssetDrop { get; set; }

    public event Action? LayerChanged;
    public event Action<Rect>? ChunkSelected;
    public event Action<IReadOnlyList<Point>>? LassoSelected;
    public event Action<Point>? MarkerPointPicked;
    public event Action? RoomOffsetsChanged;
    public event Action<string?>? SelectedElementChanged;
    public event Action? ElementEditStarted;
    public event Action? ElementEditCompleted;
    public event Action<Point>? AssetDropped;

    public CalibrationCanvas()
    {
        ClipToBounds = true;
        Focusable = true;
        PointerPressed += OnPointerPressed;
        PointerMoved += OnPointerMoved;
        PointerReleased += OnPointerReleased;
        PointerWheelChanged += OnPointerWheel;
        DragDrop.SetAllowDrop(this, true);
        DragDrop.AddDragOverHandler(this, OnDragOver);
        DragDrop.AddDropHandler(this, OnDrop);
        PointerExited += (_, _) =>
        {
            if (_hoverRoom is null) return;
            _hoverRoom = null;
            InvalidateVisual();
        };
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

    public void SaveComposite(string path)
    {
        if (Image is null) throw new InvalidOperationException("Nie wybrano obrazu warstwy.");
        using var result = new RenderTargetBitmap(Image.PixelSize, new Vector(96, 96));
        using (var context = result.CreateDrawingContext())
        {
            var canvasRect = new Rect(0, 0, Image.PixelSize.Width, Image.PixelSize.Height);
            context.DrawImage(Image, canvasRect, canvasRect);
            foreach (var element in ImageElements.OrderBy(item => item.ZIndex))
            {
                if (!ElementBitmaps.TryGetValue(element.AssetFile, out var bitmap)) continue;
                var center = new Point(element.ImageX, element.ImageY);
                var target = new Rect(
                    element.ImageX - element.Width / 2,
                    element.ImageY - element.Height / 2,
                    element.Width,
                    element.Height);
                using (context.PushTransform(Matrix.CreateRotation(element.Rotation * Math.PI / 180, center)))
                using (context.PushOpacity(Math.Clamp(element.Opacity, 0, 1)))
                    context.DrawImage(bitmap, new Rect(0, 0, bitmap.PixelSize.Width, bitmap.PixelSize.Height), target);
            }
        }
        using var stream = File.Create(path);
        result.Save(stream, PngBitmapEncoderOptions.Default);
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
        DrawImageElements(context, imageRect);
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
            var brush = SelectedRoomIds.Contains(room.Id)
                ? Brushes.Orange
                : room.Vnum is null ? Brushes.Gray : SectorBrush(room.Sector);
            var size = SelectedRoomIds.Contains(room.Id) ? 8 : 6;
            var rect = new Rect(point.X - size / 2, point.Y - size / 2, size, size);
            context.FillRectangle(brush, rect);
            context.DrawRectangle(RoomOutlinePen, rect);
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

        if (_hoverRoom is { } hover)
        {
            var lines = hover.Name ?? "(bez nazwy)";
            if (hover.Vnum is not null) lines += $"\nvnum: {hover.Vnum}";
            if (!string.IsNullOrWhiteSpace(hover.Sector)) lines += $"\nsektor: {hover.Sector}";
            var text = new FormattedText(lines, CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, Typeface.Default, 13, Brushes.White);
            var origin = new Point(
                Math.Min(_hoverPoint.X + 14, Bounds.Width - text.Width - 10),
                Math.Min(_hoverPoint.Y + 14, Bounds.Height - text.Height - 10));
            var background = new Rect(origin.X - 6, origin.Y - 4, text.Width + 12, text.Height + 8);
            context.FillRectangle(new SolidColorBrush(Color.FromArgb(220, 20, 24, 22)), background);
            context.DrawRectangle(new Pen(Brushes.Orange, 1), background);
            context.DrawText(text, origin);
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
        if (Mode == CalibrationMode.EditElements && properties.IsLeftButtonPressed && Layer is not null && Image is not null)
        {
            var element = FindElement(point);
            SetSelectedElement(element?.Id);
            if (element is not null)
            {
                _dragView = false;
                _draggedElementId = element.Id;
                _dragPoint = point;
                ElementEditStarted?.Invoke();
                e.Pointer.Capture(this);
                return;
            }
        }
        if (Mode == CalibrationMode.AddMarker && Layer is not null && Image is not null &&
            (properties.IsLeftButtonPressed || properties.IsRightButtonPressed))
        {
            var world = ScreenToWorld(point);
            var x = (world.X - Layer.MinX) / Layer.Width * Image.PixelSize.Width;
            var y = (Layer.MaxY - world.Y) / Layer.Height * Image.PixelSize.Height;
            if (x >= 0 && y >= 0 && x <= Image.PixelSize.Width && y <= Image.PixelSize.Height)
                MarkerPointPicked?.Invoke(new Point(x, y));
            return;
        }
        if (properties.IsLeftButtonPressed)
        {
            _dragView = true;
            _dragPoint = point;
            return;
        }
        if (!properties.IsRightButtonPressed || Layer is null || Image is null) return;
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
        if (_dragPoint is not { } previous || Layer is null)
        {
            UpdateHover(e.GetPosition(this));
            return;
        }
        _hoverRoom = null;
        var current = e.GetPosition(this);
        if (Mode == CalibrationMode.EditElements && _draggedElementId is { } elementId &&
            ImageElements.FirstOrDefault(item => item.Id == elementId) is { } element && Image is not null)
        {
            element.ImageX += (current.X - previous.X) / (_scale * Layer.Width) * Image.PixelSize.Width;
            element.ImageY += (current.Y - previous.Y) / (_scale * Layer.Height) * Image.PixelSize.Height;
            _dragPoint = current;
            InvalidateVisual();
            return;
        }
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
        if (_draggedElementId is not null)
        {
            _draggedElementId = null;
            _dragPoint = null;
            e.Pointer.Capture(null);
            ElementEditCompleted?.Invoke();
            return;
        }
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

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = CanAcceptAssetDrop ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnDrop(object? sender, DragEventArgs e)
    {
        if (!CanAcceptAssetDrop || !TryScreenToImage(e.GetPosition(this), out var imagePoint)) return;
        AssetDropped?.Invoke(imagePoint);
        e.Handled = true;
    }

    private void DrawImageElements(DrawingContext context, Rect imageRect)
    {
        if (Image is null) return;
        foreach (var element in ImageElements.OrderBy(item => item.ZIndex))
        {
            if (!ElementBitmaps.TryGetValue(element.AssetFile, out var bitmap)) continue;
            var center = new Point(
                imageRect.X + element.ImageX / Image.PixelSize.Width * imageRect.Width,
                imageRect.Y + element.ImageY / Image.PixelSize.Height * imageRect.Height);
            var width = element.Width / Image.PixelSize.Width * imageRect.Width;
            var height = element.Height / Image.PixelSize.Height * imageRect.Height;
            var target = new Rect(center.X - width / 2, center.Y - height / 2, width, height);
            using (context.PushTransform(Matrix.CreateRotation(element.Rotation * Math.PI / 180, center)))
            using (context.PushOpacity(Math.Clamp(element.Opacity, 0, 1)))
                context.DrawImage(bitmap, new Rect(0, 0, bitmap.PixelSize.Width, bitmap.PixelSize.Height), target);

            if (element.Id == SelectedElementId)
            {
                using (context.PushTransform(Matrix.CreateRotation(element.Rotation * Math.PI / 180, center)))
                {
                    context.DrawRectangle(new Pen(Brushes.DeepSkyBlue, 2), target);
                    context.FillRectangle(Brushes.White, new Rect(target.Right - 4, target.Bottom - 4, 8, 8));
                }
            }
        }
    }

    private MapImageElement? FindElement(Point screenPoint)
    {
        if (Image is null || !TryScreenToImage(screenPoint, out var imagePoint)) return null;
        foreach (var element in ImageElements.OrderByDescending(item => item.ZIndex))
        {
            var radians = -element.Rotation * Math.PI / 180;
            var dx = imagePoint.X - element.ImageX;
            var dy = imagePoint.Y - element.ImageY;
            var localX = dx * Math.Cos(radians) - dy * Math.Sin(radians);
            var localY = dx * Math.Sin(radians) + dy * Math.Cos(radians);
            if (Math.Abs(localX) <= element.Width / 2 && Math.Abs(localY) <= element.Height / 2)
                return element;
        }
        return null;
    }

    private bool TryScreenToImage(Point screenPoint, out Point imagePoint)
    {
        imagePoint = default;
        if (Layer is null || Image is null || Layer.Width <= 0 || Layer.Height <= 0) return false;
        var world = ScreenToWorld(screenPoint);
        var x = (world.X - Layer.MinX) / Layer.Width * Image.PixelSize.Width;
        var y = (Layer.MaxY - world.Y) / Layer.Height * Image.PixelSize.Height;
        if (x < 0 || y < 0 || x > Image.PixelSize.Width || y > Image.PixelSize.Height) return false;
        imagePoint = new Point(x, y);
        return true;
    }

    private void SetSelectedElement(string? id)
    {
        if (SelectedElementId == id) return;
        SelectedElementId = id;
        SelectedElementChanged?.Invoke(id);
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

    private void UpdateHover(Point point)
    {
        var room = FindNearestRoom(point);
        if (!ReferenceEquals(room, _hoverRoom) || (room is not null && Distance(point, _hoverPoint) > 1))
        {
            _hoverRoom = room;
            _hoverPoint = point;
            InvalidateVisual();
        }
    }

    private static readonly Pen RoomOutlinePen = new(new SolidColorBrush(Color.FromArgb(180, 0, 0, 0)), 1);

    private static IBrush SectorBrush(string? sector)
    {
        var value = (sector ?? string.Empty).Trim().ToLowerInvariant();
        if (value.Contains("lawa")) return Rgb(196, 63, 35);
        if (value.Contains("ocean") || value.Contains("morze") || value.Contains("rzeka") ||
            value.Contains("jezioro") || value.Contains("woda")) return Rgb(52, 128, 176);
        if (value.Contains("lodowiec") || value.Contains("arkty") || value.Contains("tundra")) return Rgb(196, 216, 218);
        if (value.Contains("gory") || value.Contains("gorska") || value.Contains("wzgorza")) return Rgb(140, 141, 136);
        if (value.Contains("pust") || value.Contains("wydmy") || value.Contains("piaski") || value.Contains("plaza")) return Rgb(214, 178, 106);
        if (value.Contains("bagno") || value.Contains("blotna")) return Rgb(94, 122, 84);
        if (value.Contains("puszcza")) return Rgb(34, 96, 62);
        if (value.Contains("las")) return Rgb(58, 132, 78);
        if (value.Contains("droga") || value.Contains("sciezka")) return Rgb(190, 160, 120);
        if (value.Contains("miasto") || value.Contains("plac") || value.Contains("arena") || value.Contains("ruiny")) return Rgb(171, 138, 106);
        if (value.Contains("podzi") || value.Contains("jaskinia") || value.Contains("kopalnia") || value.Contains("wewnatrz")) return Rgb(108, 99, 118);
        if (value.Contains("pole") || value.Contains("laka") || value.Contains("trawa") || value.Contains("step")) return Rgb(132, 165, 92);
        return Brushes.White;
    }

    private static readonly Dictionary<(byte, byte, byte), IBrush> BrushCache = [];
    private static IBrush Rgb(byte r, byte g, byte b)
    {
        if (!BrushCache.TryGetValue((r, g, b), out var brush))
            BrushCache[(r, g, b)] = brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        return brush;
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

using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media.Imaging;

namespace MudClient.MapImageCalibrator;

public sealed class MainWindow : Window
{
    private readonly CalibrationRepository _repository;
    private readonly List<LocationLayer> _layers;
    private readonly Dictionary<string, LocationLayer> _originalLayers;
    private readonly List<RoomPoint> _rooms;
    private readonly Dictionary<string, Bitmap> _bitmaps = new(StringComparer.OrdinalIgnoreCase);
    private readonly IReadOnlyList<MapEditorAsset> _editorAssets;
    private readonly Dictionary<string, Bitmap> _editorBitmaps = new(StringComparer.OrdinalIgnoreCase);
    private readonly CalibrationCanvas _canvas = new();
    private readonly ComboBox _layerPicker = new();
    private readonly TextBox _minX = Field(), _minY = Field(), _maxX = Field(), _maxY = Field();
    private readonly TextBox _label = Field();
    private readonly TextBox _newLayerName = Field();
    private readonly Slider _opacitySlider = new() { Minimum = 0, Maximum = 2, TickFrequency = 0.01 };
    private readonly Slider _edgeFadeSlider = new() { Minimum = 0, Maximum = 0.49, TickFrequency = 0.01 };
    private readonly TextBlock _opacityValue = new(), _edgeFadeValue = new();
    private readonly TextBlock _status = new() { TextWrapping = Avalonia.Media.TextWrapping.Wrap };
    private readonly ObservableCollection<CalibrationAnchor> _anchors = [];
    private readonly ListBox _anchorList = new() { Height = 160 };
    private readonly ObservableCollection<ImageMarker> _markers = [];
    private readonly ListBox _markerList = new() { Height = 160 };
    private readonly ObservableCollection<MapImageElement> _imageElements = [];
    private readonly ListBox _elementList = new() { Height = 130 };
    private readonly TextBox _elementX = Field(), _elementY = Field(), _elementWidth = Field(), _elementHeight = Field();
    private readonly TextBox _elementRotation = Field(), _elementOpacity = Field();
    private readonly Stack<List<MapImageElement>> _undo = new();
    private readonly Stack<List<MapImageElement>> _redo = new();
    private List<MapImageElement>? _dragSnapshot;
    private MapEditorAsset? _dragAsset;
    private PointerPressedEventArgs? _assetDragStartEvent;
    private Point _assetDragStart;
    private bool _assetDragInProgress;
    private bool _syncingElementSelection;
    private Point? _pendingImagePoint;
    private RoomPoint? _pendingRoom;
    private Bitmap? _bitmap;
    private List<int> _includedRoomIds = [];
    private string? _layerName;
    private bool _isBlankCanvas;
    private bool _suppressSliderEvents;

    public MainWindow(string mapDirectory)
    {
        _repository = new CalibrationRepository(mapDirectory);
        _editorAssets = _repository.LoadEditorAssets();
        _layers = _repository.LoadLayers();
        _originalLayers = _layers.ToDictionary(layer => layer.FileName, CloneLayer, StringComparer.OrdinalIgnoreCase);
        _rooms = _repository.LoadRooms();
        foreach (var layer in _layers)
        {
            var path = Path.Combine(_repository.LocationsDirectory, layer.FileName);
            if (File.Exists(path) && !_bitmaps.ContainsKey(layer.FileName))
                _bitmaps[layer.FileName] = new Bitmap(path);
        }
        foreach (var asset in _editorAssets)
        {
            var path = _repository.ResolveEditorAssetPath(asset.File);
            if (File.Exists(path) && !_editorBitmaps.ContainsKey(asset.File))
                _editorBitmaps[asset.File] = new Bitmap(path);
        }
        Title = "Kalibrator obrazów mapy (narzędzie lokalne)";
        Width = 1500; Height = 920; MinWidth = 1050; MinHeight = 650;

        _layerPicker.ItemsSource = _layers;
        _layerPicker.SelectionChanged += (_, _) => SelectLayer(_layerPicker.SelectedItem as LocationLayer);
        _canvas.Rooms = _rooms;
        _canvas.LayerChanged += RefreshFields;
        _canvas.ChunkSelected += ShowSelectedChunk;
        _canvas.LassoSelected += ShowSelectedLasso;
        _canvas.MarkerPointPicked += AddMarkerAt;
        _canvas.AssetDropped += AddAssetAt;
        _canvas.SelectedElementChanged += SelectElement;
        _canvas.ElementEditStarted += () => _dragSnapshot = CaptureElements();
        _canvas.ElementEditCompleted += CompleteElementDrag;
        _canvas.ElementBitmaps = _editorBitmaps;
        _opacitySlider.ValueChanged += (_, _) => ApplySliders();
        _edgeFadeSlider.ValueChanged += (_, _) => ApplySliders();

        _anchorList.ItemsSource = _anchors;
        _markerList.ItemsSource = _markers;
        _elementList.ItemsSource = _imageElements;
        _elementList.SelectionChanged += (_, _) =>
        {
            if (!_syncingElementSelection)
                SelectElement((_elementList.SelectedItem as MapImageElement)?.Id);
        };
        Content = BuildLayout();
        KeyDown += OnKeyDown;
        Opened += (_, _) => { if (_layers.Count > 0) _layerPicker.SelectedIndex = 0; };
        Closed += (_, _) =>
        {
            foreach (var bitmap in _bitmaps.Values) bitmap.Dispose();
            foreach (var bitmap in _editorBitmaps.Values) bitmap.Dispose();
        };
    }

    private Control BuildLayout()
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("230,*,372") };
        var palette = BuildAssetPalette();
        Grid.SetColumn(palette, 0);
        grid.Children.Add(palette);
        Grid.SetColumn(_canvas, 1);
        grid.Children.Add(_canvas);
        var panel = new StackPanel { Margin = new Thickness(10), Spacing = 10 };
        var sidebar = new Border
        {
            Child = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto },
            Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromRgb(24, 27, 26)),
        };
        Grid.SetColumn(sidebar, 2);
        grid.Children.Add(sidebar);

        panel.Children.Add(Section("Warstwa",
            _layerPicker,
            RowButtons(
                Button("Dopasuj widok", (_, _) => _canvas.Fit()),
                Button("Pomoc", (_, _) => ShowHelp()))));

        panel.Children.Add(Section("Siatka roomów",
            RowButtons(
                Button("Zaznacz prostokąt", (_, _) => SetMode(CalibrationMode.SelectRooms)),
                Button("Zaznacz lassem", (_, _) => SetMode(CalibrationMode.SelectRoomsLasso))),
            RowButtons(
                Button("Cała warstwa", (_, _) => ShowWholeLayer()),
                Button("Wszystkie roomy mapy", (_, _) => ShowAllAreaRooms())),
            RowButtons(
                Button("Przesuń zaznaczone", (_, _) => SetMode(CalibrationMode.MoveSelectedRooms)),
                Button("Przesuń jeden room", (_, _) => SetMode(CalibrationMode.MoveSingleRoom))),
            RowButtons(
                Button("Resetuj zaznaczone", (_, _) => ResetSelectedRoomOffsets()),
                Button("Resetuj wszystkie", (_, _) => ResetAllRoomOffsets()))));

        _newLayerName.PlaceholderText = "Nazwa, np. Mroczne Mokradła";
        panel.Children.Add(Section("Nowa warstwa z zaznaczenia",
            _newLayerName,
            Button("Utwórz czarne tło 1200×800", (_, _) => CreateBlankLayer())));

        panel.Children.Add(Section("Obraz i granice świata",
            Row("minX", _minX, "maxX", _maxX),
            Row("minY", _minY, "maxY", _maxY),
            _opacityValue,
            _opacitySlider,
            _edgeFadeValue,
            _edgeFadeSlider,
            RowButtons(
                Button("Zastosuj pola", (_, _) => ApplyFields()),
                Button("Przesuwaj cały obraz", (_, _) => SetMode(CalibrationMode.MoveImage)))));

        panel.Children.Add(Section("Elementy obrazu",
            Button("Edytuj elementy", (_, _) => SetMode(CalibrationMode.EditElements)),
            _elementList,
            Row("X", _elementX, "Y", _elementY),
            Row("Szer.", _elementWidth, "Wys.", _elementHeight),
            Row("Obrót", _elementRotation, "Krycie", _elementOpacity),
            Button("Zastosuj właściwości", (_, _) => ApplyElementFields()),
            RowButtons(
                Button("Duplikuj", (_, _) => DuplicateSelectedElement()),
                Button("Usuń", (_, _) => DeleteSelectedElement())),
            RowButtons(
                Button("Niżej", (_, _) => MoveSelectedElement(-1)),
                Button("Wyżej", (_, _) => MoveSelectedElement(1))),
            RowButtons(
                Button("Cofnij", (_, _) => UndoElements()),
                Button("Ponów", (_, _) => RedoElements()))));

        _label.PlaceholderText = "Opis, np. Usuń tę fontannę";
        panel.Children.Add(Section("Markery do poprawienia grafiki",
            _label,
            Button("Dodaj marker na obrazku", (_, _) => SetMode(CalibrationMode.AddMarker)),
            _markerList,
            Button("Usuń zaznaczony marker", (_, _) => RemoveMarker())));

        panel.Children.Add(Section("Zapis",
            RowButtons(
                Button("Zapisz robocze", (_, _) => SaveWorkspace()),
                Button("Zapisz manifest", (_, _) => SaveManifest())),
            Button("Eksportuj gotowy PNG", (_, _) => ExportComposite()),
            Button("Eksportuj pakiet dla AI", (_, _) => ExportPackage()),
            Button("Wyczyść całą edycję aktywnego miasta", (_, _) => ClearAllEditing())));

        var hint = new TextBlock
        {
            Text = "LPM: widok lub element  ·  PPM: operacje roomów  ·  kółko: zoom",
            FontSize = 11,
            Foreground = Avalonia.Media.Brushes.Gray,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
        };
        panel.Children.Add(hint);
        _status.FontSize = 12;
        panel.Children.Add(new Border
        {
            Child = _status,
            Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromRgb(33, 38, 36)),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10, 8),
        });
        return grid;
    }

    private Control BuildAssetPalette()
    {
        var stack = new StackPanel { Margin = new Thickness(10), Spacing = 8 };
        stack.Children.Add(new TextBlock
        {
            Text = "ELEMENTY",
            FontSize = 13,
            FontWeight = Avalonia.Media.FontWeight.Bold,
            Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromRgb(255, 190, 80)),
        });
        stack.Children.Add(new TextBlock
        {
            Text = "Przeciągnij element na obraz. Własne PNG umieść w Assets/Map/EditorAssets.",
            FontSize = 11,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            Foreground = Avalonia.Media.Brushes.LightGray,
        });

        foreach (var group in _editorAssets.GroupBy(asset => asset.Category))
        {
            stack.Children.Add(new TextBlock
            {
                Text = group.Key.ToUpperInvariant(),
                Margin = new Thickness(0, 8, 0, 0),
                FontSize = 10,
                FontWeight = Avalonia.Media.FontWeight.Bold,
                Foreground = Avalonia.Media.Brushes.Gray,
            });
            foreach (var asset in group)
            {
                var row = new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("44,*"),
                    DataContext = asset,
                    Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromRgb(40, 45, 43)),
                    Margin = new Thickness(0, 2),
                };
                if (_editorBitmaps.TryGetValue(asset.File, out var bitmap))
                {
                    row.Children.Add(new Avalonia.Controls.Image
                    {
                        Source = bitmap,
                        Width = 38,
                        Height = 38,
                        Stretch = Avalonia.Media.Stretch.Uniform,
                        Margin = new Thickness(3),
                    });
                }
                var label = new TextBlock
                {
                    Text = asset.Name,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                    Margin = new Thickness(6, 3),
                    FontSize = 12,
                };
                Grid.SetColumn(label, 1);
                row.Children.Add(label);
                row.PointerPressed += Asset_OnPointerPressed;
                row.PointerMoved += Asset_OnPointerMoved;
                stack.Children.Add(row);
            }
        }

        return new Border
        {
            Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromRgb(24, 27, 26)),
            Child = new ScrollViewer
            {
                Content = stack,
                VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            },
        };
    }

    private static Border Section(string title, params Control[] children)
    {
        var stack = new StackPanel { Spacing = 8 };
        stack.Children.Add(new TextBlock
        {
            Text = title.ToUpperInvariant(),
            FontSize = 11,
            FontWeight = Avalonia.Media.FontWeight.Bold,
            Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromRgb(255, 190, 80)),
        });
        foreach (var child in children) stack.Children.Add(child);
        return new Border
        {
            Child = stack,
            Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromRgb(33, 38, 36)),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12, 10),
        };
    }

    private void SelectLayer(LocationLayer? layer)
    {
        if (layer is null) return;
        if (!_bitmaps.TryGetValue(layer.FileName, out _bitmap))
        {
            SetStatus($"Nie znaleziono obrazu: {layer.FileName}");
            return;
        }
        _anchors.Clear();
        foreach (var anchor in _repository.LoadAnchors(layer)) _anchors.Add(anchor);
        var workspace = _repository.LoadWorkspace(layer);
        _includedRoomIds = workspace.IncludedRoomIds.ToList();
        _layerName = workspace.LayerName;
        _isBlankCanvas = workspace.IsBlankCanvas;
        _markers.Clear();
        foreach (var marker in workspace.Markers) _markers.Add(marker);
        _imageElements.Clear();
        foreach (var element in workspace.ImageElements.OrderBy(item => item.ZIndex))
        {
            if (_editorBitmaps.ContainsKey(element.AssetFile)) _imageElements.Add(element);
        }
        NormalizeElementOrder();
        _undo.Clear();
        _redo.Clear();
        _canvas.Markers = _markers;
        _canvas.ImageElements = _imageElements;
        _canvas.SelectedElementId = null;
        _canvas.RoomOffsets = workspace.RoomOffsets.ToDictionary(
            offset => offset.RoomId, offset => new Point(offset.OffsetX, offset.OffsetY));
        _canvas.SelectedRoomIds.Clear();
        _canvas.Layer = layer; _canvas.Image = _bitmap; _canvas.Anchors = _anchors;
        _canvas.Rooms = GetWorkspaceRooms(layer);
        RefreshFields();
        _canvas.Fit();
        SetStatus($"Wczytano {layer.FileName}: {_canvas.Rooms.Count} pokojów w obszarze warstwy.");
    }

    private void Asset_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control { DataContext: MapEditorAsset asset } ||
            !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        _dragAsset = asset;
        _assetDragStartEvent = e;
        _assetDragStart = e.GetPosition(this);
    }

    private async void Asset_OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_dragAsset is null || _assetDragStartEvent is null || _assetDragInProgress ||
            !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        var current = e.GetPosition(this);
        if (Math.Abs(current.X - _assetDragStart.X) < 4 && Math.Abs(current.Y - _assetDragStart.Y) < 4) return;

        _assetDragInProgress = true;
        _canvas.CanAcceptAssetDrop = true;
        try
        {
            var data = new DataTransfer();
            data.Add(DataTransferItem.CreateText("KillerMudClient/MapEditorAsset"));
            await DragDrop.DoDragDropAsync(_assetDragStartEvent, data, DragDropEffects.Copy);
        }
        finally
        {
            _canvas.CanAcceptAssetDrop = false;
            _assetDragInProgress = false;
            _dragAsset = null;
            _assetDragStartEvent = null;
        }
    }

    private void AddAssetAt(Point imagePoint)
    {
        if (_dragAsset is null || !_editorBitmaps.TryGetValue(_dragAsset.File, out var bitmap)) return;
        PushUndoSnapshot();
        const double defaultMaximumSize = 128;
        var factor = Math.Min(1, defaultMaximumSize / Math.Max(bitmap.PixelSize.Width, bitmap.PixelSize.Height));
        var element = new MapImageElement
        {
            AssetFile = _dragAsset.File,
            ImageX = imagePoint.X,
            ImageY = imagePoint.Y,
            Width = Math.Max(16, bitmap.PixelSize.Width * factor),
            Height = Math.Max(16, bitmap.PixelSize.Height * factor),
            ZIndex = _imageElements.Count,
        };
        _imageElements.Add(element);
        _canvas.Mode = CalibrationMode.EditElements;
        SelectElement(element.Id);
        _canvas.InvalidateVisual();
        SetStatus($"Dodano „{_dragAsset.Name}”. Przeciągnij LPM, aby przesunąć element.");
    }

    private void SetMode(CalibrationMode mode) { _canvas.Mode = mode; SetStatus($"Tryb: {mode}"); }

    private void ShowSelectedChunk(Rect selection)
    {
        if (_canvas.Layer is not { } layer) return;
        var rooms = _canvas.Rooms.Where(room =>
        {
            var offset = _canvas.RoomOffsets.GetValueOrDefault(room.Id, default);
            var x = room.X + offset.X; var y = room.Y + offset.Y;
            return x >= selection.X && x <= selection.Right && y >= selection.Y && y <= selection.Bottom;
        }).ToList();
        if (rooms.Count == 0)
        {
            SetStatus("W zaznaczonym prostokącie nie ma pokojów.");
            return;
        }

        _canvas.SelectedRoomIds.Clear();
        foreach (var room in rooms) _canvas.SelectedRoomIds.Add(room.Id);
        _canvas.Mode = CalibrationMode.MoveSelectedRooms;
        _canvas.InvalidateVisual();
        SetStatus($"Zaznaczono {rooms.Count} pokojów. Możesz je teraz przeciągnąć LPM jako grupę.");
    }

    private void ShowSelectedLasso(IReadOnlyList<Point> polygon)
    {
        var rooms = _canvas.Rooms.Where(room =>
        {
            var offset = _canvas.RoomOffsets.GetValueOrDefault(room.Id, default);
            return IsInsidePolygon(new Point(room.X + offset.X, room.Y + offset.Y), polygon);
        }).ToList();
        SelectRooms(rooms, "lassie");
    }

    private void SelectRooms(IReadOnlyList<RoomPoint> rooms, string shape)
    {
        if (rooms.Count == 0)
        {
            SetStatus($"W zaznaczonym {shape} nie ma pokojów.");
            return;
        }
        _canvas.SelectedRoomIds.Clear();
        foreach (var room in rooms) _canvas.SelectedRoomIds.Add(room.Id);
        _canvas.Mode = CalibrationMode.MoveSelectedRooms;
        _canvas.InvalidateVisual();
        SetStatus($"Zaznaczono {rooms.Count} pokojów {shape}. Możesz utworzyć warstwę albo przesunąć je jako grupę.");
    }

    private void ShowWholeLayer()
    {
        if (_canvas.Layer is not { } layer) return;
        _canvas.Rooms = GetLayerRooms(layer);
        _canvas.SelectedRoomIds.Clear();
        _canvas.Fit();
        SetStatus($"Pokazano cały obszar warstwy: {_canvas.Rooms.Count} pokojów.");
    }

    private void ShowAllAreaRooms()
    {
        if (_canvas.Layer is not { } layer) return;
        _canvas.Rooms = _rooms.Where(room => room.AreaId == layer.AreaId && Math.Abs(room.Z - layer.Z) < 0.0001).ToList();
        _canvas.SelectedRoomIds.Clear();
        _canvas.FitRooms(_canvas.Rooms);
        SetStatus($"Pokazano wszystkie roomy mapy area {layer.AreaId}, z {layer.Z:0.###}: {_canvas.Rooms.Count}.");
    }

    private void CreateBlankLayer()
    {
        var name = _newLayerName.Text?.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            SetStatus("Podaj nazwę nowej warstwy.");
            return;
        }
        var selected = _rooms.Where(room => _canvas.SelectedRoomIds.Contains(room.Id)).ToList();
        if (selected.Count == 0)
        {
            SetStatus("Najpierw zaznacz co najmniej jeden room prostokątem albo lassem.");
            return;
        }
        if (selected.Select(room => (room.AreaId, room.Z)).Distinct().Count() != 1)
        {
            SetStatus("Nowa warstwa może obejmować roomy tylko z jednego area i poziomu Z.");
            return;
        }

        var slug = Slug(name);
        if (string.IsNullOrWhiteSpace(slug))
        {
            SetStatus("Nazwa musi zawierać przynajmniej jedną literę lub cyfrę.");
            return;
        }
        var fileName = UniqueFileName(slug);
        var minX = selected.Min(room => room.X) - 2;
        var maxX = selected.Max(room => room.X) + 2;
        var minY = selected.Min(room => room.Y) - 2;
        var maxY = selected.Max(room => room.Y) + 2;
        if (maxX - minX < 4) { minX -= 2; maxX += 2; }
        if (maxY - minY < 4) { minY -= 2; maxY += 2; }
        var first = selected[0];
        var layer = new LocationLayer
        {
            AreaId = first.AreaId, Z = first.Z,
            MinX = minX, MinY = minY, MaxX = maxX, MaxY = maxY,
            Opacity = 1, EdgeFade = 0.1, FileName = fileName,
        };
        var path = Path.Combine(_repository.LocationsDirectory, fileName);
        CreateBlackPng(path, 1200, 800);
        _bitmaps[fileName] = new Bitmap(path);
        _layers.Add(layer);
        _originalLayers[fileName] = CloneLayer(layer);
        _repository.SaveLayers(_layers);
        _repository.SaveWorkspace(layer, new CalibrationWorkspace
        {
            ImageFile = fileName,
            LayerName = name,
            IsBlankCanvas = true,
            IncludedRoomIds = selected.Select(room => room.Id).ToList(),
            Rooms = ToRoomReferences(selected),
        });
        _layerPicker.ItemsSource = null;
        _layerPicker.ItemsSource = _layers;
        _layerPicker.SelectedItem = layer;
        _newLayerName.Text = string.Empty;
        SetStatus($"Utworzono czarną warstwę „{name}” ({fileName}) dla {selected.Count} roomów. Dodaj markery i wyeksportuj pakiet dla AI.");
    }

    private void AddMarkerAt(Point point)
    {
        var marker = new ImageMarker
        {
            Number = _markers.Count == 0 ? 1 : _markers.Max(item => item.Number) + 1,
            Label = string.IsNullOrWhiteSpace(_label.Text) ? "Marker bez opisu" : _label.Text.Trim(),
            ImageX = point.X,
            ImageY = point.Y,
        };
        _markers.Add(marker);
        _label.Text = string.Empty;
        _canvas.Mode = CalibrationMode.MoveImage;
        _canvas.InvalidateVisual();
        SetStatus($"Dodano marker #{marker.Number}: {marker.Label}. Aby dodać kolejny, kliknij ponownie „Dodaj marker”.");
    }

    private void RemoveMarker()
    {
        if (_markerList.SelectedItem is not ImageMarker marker) return;
        _markers.Remove(marker);
        _canvas.InvalidateVisual();
    }

    private void SelectElement(string? id)
    {
        var element = _imageElements.FirstOrDefault(item => item.Id == id);
        _canvas.SelectedElementId = element?.Id;
        _syncingElementSelection = true;
        _elementList.SelectedItem = element;
        _syncingElementSelection = false;
        if (element is null)
        {
            foreach (var field in ElementFields()) field.Text = string.Empty;
        }
        else
        {
            _elementX.Text = F(element.ImageX);
            _elementY.Text = F(element.ImageY);
            _elementWidth.Text = F(element.Width);
            _elementHeight.Text = F(element.Height);
            _elementRotation.Text = F(element.Rotation);
            _elementOpacity.Text = F(element.Opacity);
        }
        _canvas.InvalidateVisual();
    }

    private void ApplyElementFields()
    {
        if (_elementList.SelectedItem is not MapImageElement element ||
            !Try(_elementX, out var x) || !Try(_elementY, out var y) ||
            !Try(_elementWidth, out var width) || !Try(_elementHeight, out var height) ||
            !Try(_elementRotation, out var rotation) || !Try(_elementOpacity, out var opacity) ||
            width <= 0 || height <= 0)
        {
            SetStatus("Właściwości elementu muszą zawierać poprawne liczby, a rozmiar musi być dodatni.");
            return;
        }
        PushUndoSnapshot();
        element.ImageX = x;
        element.ImageY = y;
        element.Width = width;
        element.Height = height;
        element.Rotation = rotation % 360;
        element.Opacity = Math.Clamp(opacity, 0, 1);
        _canvas.InvalidateVisual();
        SelectElement(element.Id);
    }

    private void DuplicateSelectedElement()
    {
        if (_elementList.SelectedItem is not MapImageElement source) return;
        PushUndoSnapshot();
        var copy = source.Clone();
        copy.Id = Guid.NewGuid().ToString("N");
        copy.ImageX += 16;
        copy.ImageY += 16;
        copy.ZIndex = _imageElements.Count;
        _imageElements.Add(copy);
        SelectElement(copy.Id);
        _canvas.InvalidateVisual();
    }

    private void DeleteSelectedElement()
    {
        if (_elementList.SelectedItem is not MapImageElement element) return;
        PushUndoSnapshot();
        _imageElements.Remove(element);
        NormalizeElementOrder();
        SelectElement(null);
        _canvas.InvalidateVisual();
    }

    private void MoveSelectedElement(int direction)
    {
        if (_elementList.SelectedItem is not MapImageElement element) return;
        var index = _imageElements.IndexOf(element);
        var target = Math.Clamp(index + direction, 0, _imageElements.Count - 1);
        if (target == index) return;
        PushUndoSnapshot();
        _imageElements.Move(index, target);
        NormalizeElementOrder();
        SelectElement(element.Id);
        _canvas.InvalidateVisual();
    }

    private List<TextBox> ElementFields() =>
        [_elementX, _elementY, _elementWidth, _elementHeight, _elementRotation, _elementOpacity];

    private List<MapImageElement> CaptureElements() => _imageElements.Select(element => element.Clone()).ToList();

    private void PushUndoSnapshot()
    {
        _undo.Push(CaptureElements());
        _redo.Clear();
    }

    private void CompleteElementDrag()
    {
        if (_dragSnapshot is null) return;
        _undo.Push(_dragSnapshot);
        _dragSnapshot = null;
        _redo.Clear();
        SelectElement(_canvas.SelectedElementId);
    }

    private void UndoElements()
    {
        if (_undo.Count == 0) return;
        _redo.Push(CaptureElements());
        RestoreElements(_undo.Pop());
    }

    private void RedoElements()
    {
        if (_redo.Count == 0) return;
        _undo.Push(CaptureElements());
        RestoreElements(_redo.Pop());
    }

    private void RestoreElements(IEnumerable<MapImageElement> snapshot)
    {
        var selectedId = _canvas.SelectedElementId;
        _imageElements.Clear();
        foreach (var element in snapshot.Select(item => item.Clone())) _imageElements.Add(element);
        NormalizeElementOrder();
        SelectElement(_imageElements.Any(item => item.Id == selectedId) ? selectedId : null);
        _canvas.InvalidateVisual();
    }

    private void NormalizeElementOrder()
    {
        for (var index = 0; index < _imageElements.Count; index++) _imageElements[index].ZIndex = index;
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.Key == Key.Z) { UndoElements(); e.Handled = true; }
        else if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.Key == Key.Y) { RedoElements(); e.Handled = true; }
        else if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.Key == Key.D) { DuplicateSelectedElement(); e.Handled = true; }
        else if (e.Key == Key.Delete) { DeleteSelectedElement(); e.Handled = true; }
    }

    private void ResetSelectedRoomOffsets()
    {
        if (_canvas.SelectedRoomIds.Count == 0)
        {
            SetStatus("Najpierw zaznacz roomy do zresetowania.");
            return;
        }
        foreach (var roomId in _canvas.SelectedRoomIds) _canvas.RoomOffsets.Remove(roomId);
        _canvas.InvalidateVisual();
        SetStatus($"Zresetowano {_canvas.SelectedRoomIds.Count} zaznaczonych roomów.");
    }

    private void ResetAllRoomOffsets()
    {
        _canvas.RoomOffsets.Clear();
        _canvas.SelectedRoomIds.Clear();
        _canvas.InvalidateVisual();
        SetStatus("Wyzerowano robocze przesunięcia roomów.");
    }

    private CalibrationWorkspace CreateWorkspace()
    {
        var byId = _rooms.ToDictionary(room => room.Id);
        return new CalibrationWorkspace
        {
            ImageFile = _canvas.Layer?.FileName ?? string.Empty,
            LayerName = _layerName,
            IsBlankCanvas = _isBlankCanvas,
            IncludedRoomIds = _includedRoomIds.ToList(),
            Rooms = ToRoomReferences(_includedRoomIds.Count > 0
                ? _rooms.Where(room => _includedRoomIds.Contains(room.Id))
                : _canvas.Rooms),
            Markers = _markers.ToList(),
            ImageElements = _imageElements.Select(element => element.Clone()).ToList(),
            RoomOffsets = _canvas.RoomOffsets
                .Where(pair => Math.Abs(pair.Value.X) > 0.0001 || Math.Abs(pair.Value.Y) > 0.0001)
                .Select(pair => new RoomOffset
                {
                    RoomId = pair.Key,
                    Vnum = byId.GetValueOrDefault(pair.Key)?.Vnum,
                    OffsetX = pair.Value.X,
                    OffsetY = pair.Value.Y,
                }).ToList(),
        };
    }

    private void SaveWorkspace()
    {
        if (_canvas.Layer is not { } layer) return;
        _repository.SaveWorkspace(layer, CreateWorkspace());
        SetStatus("Zapisano elementy obrazu, markery i robocze przesunięcia siatki.");
    }

    private void ExportPackage()
    {
        if (_canvas.Layer is not { } layer || _canvas.Bounds.Width < 1 || _canvas.Bounds.Height < 1) return;
        var jsonPath = _repository.ExportWorkspace(layer, CreateWorkspace());
        var pngPath = Path.ChangeExtension(jsonPath, ".png");
        var compositePath = CalibrationRepository.CompositePathForExport(jsonPath);
        var bitmap = new RenderTargetBitmap(
            new PixelSize((int)_canvas.Bounds.Width, (int)_canvas.Bounds.Height), new Vector(96, 96));
        bitmap.Render(_canvas);
        using (var stream = File.Create(pngPath)) bitmap.Save(stream, PngBitmapEncoderOptions.Default);
        bitmap.Dispose();
        _canvas.SaveComposite(compositePath);
        SetStatus($"Wyeksportowano pakiet AI:\nJSON: {jsonPath}\nPodgląd roomów: {pngPath}\nCzysta kompozycja: {compositePath}");
    }

    private void ExportComposite()
    {
        if (_canvas.Layer is not { } layer || _bitmap is null) return;
        var directory = Path.Combine(_repository.LocationsDirectory, "CalibrationExports");
        Directory.CreateDirectory(directory);
        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var path = Path.Combine(directory,
            $"{Path.GetFileNameWithoutExtension(layer.FileName)}-composite-{stamp}.png");
        _canvas.SaveComposite(path);
        SetStatus($"Wyeksportowano gotowy obraz bez roomów i markerów:\n{path}");
    }

    private void ClearAllEditing()
    {
        if (_canvas.Layer is not { } layer || !_originalLayers.TryGetValue(layer.FileName, out var original)) return;
        layer.MinX = original.MinX;
        layer.MinY = original.MinY;
        layer.MaxX = original.MaxX;
        layer.MaxY = original.MaxY;
        layer.Opacity = original.Opacity;
        layer.EdgeFade = original.EdgeFade;
        _markers.Clear();
        _imageElements.Clear();
        _anchors.Clear();
        _canvas.RoomOffsets.Clear();
        _canvas.SelectedRoomIds.Clear();
        _canvas.Markers = _markers;
        _canvas.SelectedElementId = null;
        _undo.Clear();
        _redo.Clear();
        _repository.ClearWorkspace(layer);
        _canvas.Rooms = GetWorkspaceRooms(layer);
        RefreshFields();
        _canvas.Fit();
        SetStatus("Wyczyszczono całą roboczą edycję aktywnego miasta. Gotowe eksporty pozostawiono bez zmian.");
    }

    private List<RoomPoint> GetLayerRooms(LocationLayer layer)
    {
        const double margin = 2;
        return _rooms.Where(room => room.AreaId == layer.AreaId && room.Z == layer.Z &&
            room.X >= layer.MinX - margin && room.X <= layer.MaxX + margin &&
            room.Y >= layer.MinY - margin && room.Y <= layer.MaxY + margin).ToList();
    }

    private void ShowHelp()
    {
        var help = new Window
        {
            Title = "Pomoc — kalibrator obrazów mapy",
            Width = 680,
            Height = 720,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        };
        var close = Button("Zamknij", (_, _) => help.Close());
        var content = new StackPanel { Margin = new Thickness(20), Spacing = 12 };
        content.Children.Add(new TextBlock
        {
            Text = "Kalibrator obrazów mapy",
            FontSize = 22,
            FontWeight = Avalonia.Media.FontWeight.Bold,
        });
        content.Children.Add(HelpText(
            "1. Wybór warstwy\n" +
            "Wybierz obraz miasta z listy Warstwa. Kalibrator pokazuje tylko tę ilustrację oraz pokoje znajdujące się w jej granicach. Atlas i inne miasta są ukryte. Przycisk Dopasuj widok pokazuje cały obraz.\n\n" +
            "2. Nawigacja\n" +
            "Lewy przycisk myszy przesuwa kamerę. Kółko myszy przybliża względem kursora.\n\n" +
            "3. Zaznaczanie i przesuwanie siatki\n" +
            "Kliknij Zaznacz prostokąt albo Zaznacz lassem i przeciągnij PPM wokół grupy roomów. Zaznaczone roomy zmienią kolor na pomarańczowy. Następnie przeciągaj je w trybie Przesuń zaznaczone. Tryb Przesuń jeden room pozwala złapać pojedynczy kwadrat. Resetuj zaznaczone cofa tylko wybraną grupę, a Resetuj wszystkie usuwa każde robocze przesunięcie. Są to wyłącznie robocze przesunięcia — world-map.json nie jest zmieniany.\n\n" +
            "4. Tworzenie nowej warstwy\n" +
            "Kliknij Wszystkie roomy mapy, wybierz interesujący obszar prostokątem lub lassem, wpisz nazwę i kliknij Utwórz czarne tło 1200×800. Narzędzie utworzy PNG, wpis w manifest.json i plik roboczy z listą wybranych vnumów oraz nazw roomów. Następnie dopasuj siatkę, dodaj opisane markery i wyeksportuj pakiet dla AI.\n\n" +
            "5. Ręczne przesuwanie ilustracji (opcjonalne)\n" +
            "Wybierz Przesuwaj obraz i przeciągaj prawym przyciskiem. Zmienia to minX, minY, maxX i maxY całej warstwy. Pola granic pozwalają wpisać dokładne wartości. Suwaki Krycie obrazu i Zanikanie krawędzi aktualizują podgląd na żywo.\n\n" +
            "6. Ręczna edycja obrazu\n" +
            "Przeciągnij obrazek z lewej palety na mapę. W trybie Edytuj elementy złap go LPM, aby go przesunąć. Dokładne położenie, rozmiar, obrót i krycie ustawisz w panelu Elementy obrazu. Przyciski Wyżej i Niżej zmieniają kolejność nakładania. Ctrl+D duplikuje, Delete usuwa, Ctrl+Z cofa, a Ctrl+Y ponawia. Własne transparentne PNG można dodawać w Assets/Map/EditorAssets; podkatalogi stają się kategoriami.\n\n" +
            "7. Markery na obrazie\n" +
            "Wpisz krótki opis, np. Usuń tę fontannę albo Tutaj powinna być brama. Kliknij Dodaj marker na obrazku, a następnie kliknij właściwe miejsce ilustracji. Marker otrzyma numer widoczny również na eksporcie.\n\n" +
            "8. Zapis i eksport\n" +
            "Zapisz robocze utrwala elementy, markery i przesunięcia roomów w pliku *.calibration.json. Eksportuj gotowy PNG tworzy spłaszczony obraz bez roomów i markerów w CalibrationExports. Eksportuj pakiet dla AI tworzy JSON, screenshot z roomami i markerami oraz czysty composite używany przez skill /mapa jako dokładna baza ręcznej kompozycji.\n\n" +
            "9. Pełny reset\n" +
            "Wyczyść całą edycję aktywnego miasta usuwa markery, przesunięcia roomów, zaznaczenie i plik roboczy oraz przywraca ustawienia obrazu z chwili uruchomienia narzędzia. Nie usuwa wcześniej utworzonych eksportów.\n\n" +
            "Narzędzie nie modyfikuje world-map.json i nie jest częścią wydania dla graczy."));
        content.Children.Add(close);
        help.Content = new ScrollViewer { Content = content };
        _ = help.ShowDialog(this);
    }

    private static TextBlock HelpText(string text) => new()
    {
        Text = text,
        TextWrapping = Avalonia.Media.TextWrapping.Wrap,
        LineHeight = 22,
    };
    private void RefreshFields()
    {
        if (_canvas.Layer is not { } l) return;
        _minX.Text = F(l.MinX); _minY.Text = F(l.MinY); _maxX.Text = F(l.MaxX); _maxY.Text = F(l.MaxY);
        _suppressSliderEvents = true;
        _opacitySlider.Value = l.Opacity;
        _edgeFadeSlider.Value = l.EdgeFade;
        _suppressSliderEvents = false;
        RefreshSliderLabels();
    }

    private void ApplyFields()
    {
        if (_canvas.Layer is not { } l) return;
        if (!Try(_minX, out var minX) || !Try(_minY, out var minY) || !Try(_maxX, out var maxX) ||
            !Try(_maxY, out var maxY) ||
            minX >= maxX || minY >= maxY) { SetStatus("Nieprawidłowe wartości."); return; }
        l.MinX = minX; l.MinY = minY; l.MaxX = maxX; l.MaxY = maxY;
        l.Opacity = _opacitySlider.Value;
        l.EdgeFade = _edgeFadeSlider.Value;
        _canvas.InvalidateVisual(); RefreshFields();
    }

    private void ApplySliders()
    {
        if (_suppressSliderEvents) return;
        if (_canvas.Layer is { } layer)
        {
            layer.Opacity = _opacitySlider.Value;
            layer.EdgeFade = _edgeFadeSlider.Value;
            _canvas.InvalidateVisual();
        }
        RefreshSliderLabels();
    }

    private void RefreshSliderLabels()
    {
        _opacityValue.Text = _opacitySlider.Value <= 1
            ? $"Krycie obrazu: {_opacitySlider.Value:0.00}"
            : $"Krycie obrazu: 1.00 + rozjaśnienie {_opacitySlider.Value - 1:0.00}";
        _edgeFadeValue.Text = $"Zanikanie krawędzi: {_edgeFadeSlider.Value:0.00}";
    }

    private void AddAnchor()
    {
        if (_pendingImagePoint is not { } image || _pendingRoom is not { Vnum: not null } room)
        { SetStatus("Najpierw wskaż punkt obrazka i pokój z vnum."); return; }
        _anchors.Add(new CalibrationAnchor { Label = _label.Text ?? string.Empty, Vnum = room.Vnum,
            ImageX = image.X, ImageY = image.Y, WorldX = room.X, WorldY = room.Y });
        _pendingImagePoint = null; _pendingRoom = null; _label.Text = string.Empty; _canvas.InvalidateVisual();
    }

    private void RemoveAnchor() { if (_anchorList.SelectedItem is CalibrationAnchor a) _anchors.Remove(a); _canvas.InvalidateVisual(); }

    private void FitFromAnchors()
    {
        if (_canvas.Layer is not { } l || _bitmap is null || _anchors.Count == 0) return;
        if (_anchors.Count == 1)
        {
            var a = _anchors[0];
            var currentX = l.MinX + a.ImageX / _bitmap.PixelSize.Width * l.Width;
            var currentY = l.MaxY - a.ImageY / _bitmap.PixelSize.Height * l.Height;
            var dx = a.WorldX - currentX; var dy = a.WorldY - currentY;
            l.MinX += dx; l.MaxX += dx; l.MinY += dy; l.MaxY += dy;
        }
        else
        {
            var (ax, bx) = Regression(_anchors.Select(a => (a.ImageX, a.WorldX)));
            var (ay, by) = Regression(_anchors.Select(a => (a.ImageY, a.WorldY)));
            l.MinX = ax; l.MaxX = ax + bx * _bitmap.PixelSize.Width;
            l.MaxY = ay; l.MinY = ay + by * _bitmap.PixelSize.Height;
            if (l.MinX > l.MaxX) (l.MinX, l.MaxX) = (l.MaxX, l.MinX);
            if (l.MinY > l.MaxY) (l.MinY, l.MaxY) = (l.MaxY, l.MinY);
        }
        RefreshFields(); _canvas.InvalidateVisual(); SetStatus("Dopasowano warstwę do kotwic.");
    }

    private void SaveManifest() { ApplyFields(); _repository.SaveLayers(_layers); SetStatus("Zapisano manifest.json"); }
    private void SaveAnchors() { if (_canvas.Layer is { } l) { _repository.SaveAnchors(l, _anchors); SetStatus("Zapisano plik kotwic."); } }
    private void SetStatus(string text) => _status.Text = text;

    private static (double A, double B) Regression(IEnumerable<(double X, double Y)> values)
    {
        var points = values.ToList(); var meanX = points.Average(p => p.X); var meanY = points.Average(p => p.Y);
        var denominator = points.Sum(p => Math.Pow(p.X - meanX, 2));
        if (denominator < 0.000001) return (meanY, 0);
        var b = points.Sum(p => (p.X - meanX) * (p.Y - meanY)) / denominator;
        return (meanY - b * meanX, b);
    }
    private static string F(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);
    private List<RoomPoint> GetWorkspaceRooms(LocationLayer layer) => _includedRoomIds.Count > 0
        ? _rooms.Where(room => _includedRoomIds.Contains(room.Id)).ToList()
        : GetLayerRooms(layer);
    private static List<RoomReference> ToRoomReferences(IEnumerable<RoomPoint> rooms) => rooms.Select(room => new RoomReference
    {
        RoomId = room.Id, Vnum = room.Vnum, Name = room.Name, X = room.X, Y = room.Y,
    }).ToList();
    private string UniqueFileName(string slug)
    {
        var candidate = slug + ".png";
        var suffix = 2;
        while (File.Exists(Path.Combine(_repository.LocationsDirectory, candidate))) candidate = $"{slug}-{suffix++}.png";
        return candidate;
    }
    private static string Slug(string value)
    {
        var normalized = value.Normalize(System.Text.NormalizationForm.FormD);
        var chars = normalized.Where(character => CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            .Select(character => char.IsLetterOrDigit(character) ? char.ToLowerInvariant(character) : '-')
            .ToArray();
        return string.Join('-', new string(chars).Split('-', StringSplitOptions.RemoveEmptyEntries));
    }
    private static void CreateBlackPng(string path, int width, int height)
    {
        var surface = new Border { Width = width, Height = height, Background = Avalonia.Media.Brushes.Black };
        surface.Measure(new Size(width, height));
        surface.Arrange(new Rect(0, 0, width, height));
        var bitmap = new RenderTargetBitmap(new PixelSize(width, height), new Vector(96, 96));
        bitmap.Render(surface);
        using var stream = File.Create(path);
        bitmap.Save(stream, PngBitmapEncoderOptions.Default);
        bitmap.Dispose();
    }
    private static bool IsInsidePolygon(Point point, IReadOnlyList<Point> polygon)
    {
        var inside = false;
        for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
        {
            var a = polygon[i]; var b = polygon[j];
            if ((a.Y > point.Y) != (b.Y > point.Y) &&
                point.X < (b.X - a.X) * (point.Y - a.Y) / (b.Y - a.Y) + a.X) inside = !inside;
        }
        return inside;
    }
    private static LocationLayer CloneLayer(LocationLayer layer) => new()
    {
        AreaId = layer.AreaId,
        Z = layer.Z,
        MinX = layer.MinX,
        MinY = layer.MinY,
        MaxX = layer.MaxX,
        MaxY = layer.MaxY,
        Opacity = layer.Opacity,
        EdgeFade = layer.EdgeFade,
        FileName = layer.FileName,
    };
    private static bool Try(TextBox box, out double v) => double.TryParse(box.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out v);
    private static TextBox Field() => new() { MinWidth = 70 };
    private static Button Button(string text, EventHandler<Avalonia.Interactivity.RoutedEventArgs> click)
    {
        var b = new Button
        {
            Content = new TextBlock { Text = text, TextWrapping = Avalonia.Media.TextWrapping.Wrap, TextAlignment = Avalonia.Media.TextAlignment.Center },
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Padding = new Thickness(8, 6),
            FontSize = 12,
        };
        b.Click += click;
        return b;
    }
    private static Control Row(string a, Control ac, string b, Control bc) => new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,*"), Children = { Label(a, 0), At(ac, 1), Label(b, 2), At(bc, 3) } };
    private static Control RowButtons(params Control[] controls)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions(string.Join(',', controls.Select(_ => "*"))) };
        for (var index = 0; index < controls.Length; index++)
        {
            controls[index].Margin = index == 0 ? default : new Thickness(6, 0, 0, 0);
            grid.Children.Add(At(controls[index], index));
        }
        return grid;
    }
    private static TextBlock Label(string text, int column) { var c = new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(3) }; Grid.SetColumn(c, column); return c; }
    private static T At<T>(T c, int column) where T : Control { Grid.SetColumn(c, column); return c; }
}

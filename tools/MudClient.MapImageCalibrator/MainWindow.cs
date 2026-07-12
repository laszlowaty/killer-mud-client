using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
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
    private readonly CalibrationCanvas _canvas = new();
    private readonly ComboBox _layerPicker = new();
    private readonly TextBox _minX = Field(), _minY = Field(), _maxX = Field(), _maxY = Field();
    private readonly TextBox _label = Field();
    private readonly TextBox _newLayerName = Field();
    private readonly Slider _opacitySlider = new() { Minimum = 0, Maximum = 1, TickFrequency = 0.01 };
    private readonly Slider _edgeFadeSlider = new() { Minimum = 0, Maximum = 0.49, TickFrequency = 0.01 };
    private readonly TextBlock _opacityValue = new(), _edgeFadeValue = new();
    private readonly TextBlock _status = new() { TextWrapping = Avalonia.Media.TextWrapping.Wrap };
    private readonly ObservableCollection<CalibrationAnchor> _anchors = [];
    private readonly ListBox _anchorList = new() { Height = 160 };
    private readonly ObservableCollection<ImageMarker> _markers = [];
    private readonly ListBox _markerList = new() { Height = 160 };
    private Point? _pendingImagePoint;
    private RoomPoint? _pendingRoom;
    private Bitmap? _bitmap;
    private List<int> _includedRoomIds = [];
    private string? _layerName;
    private bool _isBlankCanvas;

    public MainWindow(string mapDirectory)
    {
        _repository = new CalibrationRepository(mapDirectory);
        _layers = _repository.LoadLayers();
        _originalLayers = _layers.ToDictionary(layer => layer.FileName, CloneLayer, StringComparer.OrdinalIgnoreCase);
        _rooms = _repository.LoadRooms();
        foreach (var layer in _layers)
        {
            var path = Path.Combine(_repository.LocationsDirectory, layer.FileName);
            if (File.Exists(path) && !_bitmaps.ContainsKey(layer.FileName))
                _bitmaps[layer.FileName] = new Bitmap(path);
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
        _opacitySlider.ValueChanged += (_, _) => ApplySliders();
        _edgeFadeSlider.ValueChanged += (_, _) => ApplySliders();

        _anchorList.ItemsSource = _anchors;
        _markerList.ItemsSource = _markers;
        Content = BuildLayout();
        Opened += (_, _) => { if (_layers.Count > 0) _layerPicker.SelectedIndex = 0; };
        Closed += (_, _) =>
        {
            foreach (var bitmap in _bitmaps.Values) bitmap.Dispose();
        };
    }

    private Control BuildLayout()
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,360") };
        grid.Children.Add(_canvas);
        var panel = new StackPanel { Margin = new Thickness(6), Spacing = 8, Width = 326 };
        var sidebar = new Border
        {
            Child = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto },
            BorderThickness = new Thickness(1),
            Padding = new Thickness(6),
        };
        Grid.SetColumn(sidebar, 1);
        grid.Children.Add(sidebar);
        panel.Children.Add(new TextBlock { Text = "Warstwa", FontWeight = Avalonia.Media.FontWeight.Bold });
        panel.Children.Add(_layerPicker);
        panel.Children.Add(RowButtons(
            Button("Dopasuj widok", (_, _) => _canvas.Fit()),
            Button("Pomoc", (_, _) => ShowHelp())));
        panel.Children.Add(new TextBlock { Text = "Siatka roomów", FontWeight = Avalonia.Media.FontWeight.Bold });
        panel.Children.Add(RowButtons(
            Button("Zaznacz prostokąt", (_, _) => SetMode(CalibrationMode.SelectRooms)),
            Button("Zaznacz lassem", (_, _) => SetMode(CalibrationMode.SelectRoomsLasso))));
        panel.Children.Add(RowButtons(
            Button("Cała warstwa", (_, _) => ShowWholeLayer()),
            Button("Wszystkie roomy mapy", (_, _) => ShowAllAreaRooms())));
        panel.Children.Add(RowButtons(
            Button("Przesuń zaznaczone", (_, _) => SetMode(CalibrationMode.MoveSelectedRooms)),
            Button("Przesuń jeden room", (_, _) => SetMode(CalibrationMode.MoveSingleRoom))));
        panel.Children.Add(RowButtons(
            Button("Resetuj zaznaczone", (_, _) => ResetSelectedRoomOffsets()),
            Button("Resetuj wszystkie", (_, _) => ResetAllRoomOffsets())));
        panel.Children.Add(new TextBlock { Text = "Nowa warstwa z zaznaczenia", FontWeight = Avalonia.Media.FontWeight.Bold });
        _newLayerName.PlaceholderText = "Nazwa, np. Mroczne Mokradła";
        panel.Children.Add(_newLayerName);
        panel.Children.Add(Button("Utwórz czarne tło 1200×800", (_, _) => CreateBlankLayer()));
        panel.Children.Add(new TextBlock { Text = "Granice świata", FontWeight = Avalonia.Media.FontWeight.Bold });
        panel.Children.Add(Row("minX", _minX, "maxX", _maxX));
        panel.Children.Add(Row("minY", _minY, "maxY", _maxY));
        panel.Children.Add(_opacityValue);
        panel.Children.Add(_opacitySlider);
        panel.Children.Add(_edgeFadeValue);
        panel.Children.Add(_edgeFadeSlider);
        panel.Children.Add(Button("Zastosuj pola", (_, _) => ApplyFields()));
        panel.Children.Add(new TextBlock { Text = "Tryb obrazu", FontWeight = Avalonia.Media.FontWeight.Bold });
        panel.Children.Add(Button("Przesuwaj cały obraz", (_, _) => SetMode(CalibrationMode.MoveImage)));
        panel.Children.Add(new TextBlock { Text = "LPM: aktywna operacja · PPM: przesuwanie widoku · kółko: zoom", FontSize = 11 });
        panel.Children.Add(new TextBlock { Text = "Markery do poprawienia grafiki", FontWeight = Avalonia.Media.FontWeight.Bold });
        panel.Children.Add(_label);
        _label.PlaceholderText = "Opis, np. Usuń tę fontannę";
        panel.Children.Add(Button("Dodaj marker na obrazku", (_, _) => SetMode(CalibrationMode.AddMarker)));
        panel.Children.Add(_markerList);
        panel.Children.Add(RowButtons(
            Button("Usuń marker", (_, _) => RemoveMarker()),
            Button("Zapisz robocze", (_, _) => SaveWorkspace())));
        panel.Children.Add(RowButtons(
            Button("Zapisz manifest", (_, _) => SaveManifest()),
            Button("Eksportuj pakiet dla AI", (_, _) => ExportPackage())));
        panel.Children.Add(Button("Wyczyść całą edycję aktywnego miasta", (_, _) => ClearAllEditing()));
        panel.Children.Add(_status);
        return grid;
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
        _canvas.Markers = _markers;
        _canvas.RoomOffsets = workspace.RoomOffsets.ToDictionary(
            offset => offset.RoomId, offset => new Point(offset.OffsetX, offset.OffsetY));
        _canvas.SelectedRoomIds.Clear();
        _canvas.Layer = layer; _canvas.Image = _bitmap; _canvas.Anchors = _anchors;
        _canvas.Rooms = GetWorkspaceRooms(layer);
        RefreshFields();
        _canvas.Fit();
        SetStatus($"Wczytano {layer.FileName}: {_canvas.Rooms.Count} pokojów w obszarze warstwy.");
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
        _canvas.InvalidateVisual();
        SetStatus($"Dodano marker #{marker.Number}: {marker.Label}");
    }

    private void RemoveMarker()
    {
        if (_markerList.SelectedItem is not ImageMarker marker) return;
        _markers.Remove(marker);
        _canvas.InvalidateVisual();
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
        SetStatus("Zapisano markery i robocze przesunięcia siatki.");
    }

    private void ExportPackage()
    {
        if (_canvas.Layer is not { } layer || _canvas.Bounds.Width < 1 || _canvas.Bounds.Height < 1) return;
        var jsonPath = _repository.ExportWorkspace(layer, CreateWorkspace());
        var pngPath = Path.ChangeExtension(jsonPath, ".png");
        var bitmap = new RenderTargetBitmap(
            new PixelSize((int)_canvas.Bounds.Width, (int)_canvas.Bounds.Height), new Vector(96, 96));
        bitmap.Render(_canvas);
        using (var stream = File.Create(pngPath)) bitmap.Save(stream, PngBitmapEncoderOptions.Default);
        bitmap.Dispose();
        SetStatus($"Wyeksportowano pakiet:\n{jsonPath}\n{pngPath}");
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
        _anchors.Clear();
        _canvas.RoomOffsets.Clear();
        _canvas.SelectedRoomIds.Clear();
        _canvas.Markers = _markers;
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
            "Prawy przycisk myszy przesuwa kamerę. Kółko myszy przybliża względem kursora.\n\n" +
            "3. Zaznaczanie i przesuwanie siatki\n" +
            "Kliknij Zaznacz prostokąt albo Zaznacz lassem i przeciągnij LPM wokół grupy roomów. Zaznaczone roomy zmienią kolor na pomarańczowy. Następnie przeciągaj je w trybie Przesuń zaznaczone. Tryb Przesuń jeden room pozwala złapać pojedynczy kwadrat. Resetuj zaznaczone cofa tylko wybraną grupę, a Resetuj wszystkie usuwa każde robocze przesunięcie. Są to wyłącznie robocze przesunięcia — world-map.json nie jest zmieniany.\n\n" +
            "4. Tworzenie nowej warstwy\n" +
            "Kliknij Wszystkie roomy mapy, wybierz interesujący obszar prostokątem lub lassem, wpisz nazwę i kliknij Utwórz czarne tło 1200×800. Narzędzie utworzy PNG, wpis w manifest.json i plik roboczy z listą wybranych vnumów oraz nazw roomów. Następnie dopasuj siatkę, dodaj opisane markery i wyeksportuj pakiet dla AI.\n\n" +
            "5. Ręczne przesuwanie ilustracji (opcjonalne)\n" +
            "Wybierz Przesuwaj obraz i przeciągaj lewym przyciskiem. Zmienia to minX, minY, maxX i maxY całej warstwy. Pola granic pozwalają wpisać dokładne wartości. Suwaki Krycie obrazu i Zanikanie krawędzi aktualizują podgląd na żywo.\n\n" +
            "6. Markery na obrazie\n" +
            "Wpisz krótki opis, np. Usuń tę fontannę albo Tutaj powinna być brama. Kliknij Dodaj marker na obrazku, a następnie kliknij właściwe miejsce ilustracji. Marker otrzyma numer widoczny również na eksporcie.\n\n" +
            "7. Zapis i eksport\n" +
            "Zapisz robocze utrwala markery i przesunięcia roomów w pliku *.calibration.json. Zapisz manifest jest potrzebne tylko po zmianie położenia całego obrazu. Eksportuj pakiet dla AI tworzy screenshot z siatką i numerowanymi markerami oraz JSON z ich opisami i przesunięciami. Te dwa pliki można przekazać do poprawienia grafiki.\n\n" +
            "8. Pełny reset\n" +
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
        _opacitySlider.Value = l.Opacity;
        _edgeFadeSlider.Value = l.EdgeFade;
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
        _opacityValue.Text = $"Krycie obrazu: {_opacitySlider.Value:0.00}";
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
    private static Button Button(string text, EventHandler<Avalonia.Interactivity.RoutedEventArgs> click) { var b = new Button { Content = text }; b.Click += click; return b; }
    private static Control Row(string a, Control ac, string b, Control bc) => new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,*"), Children = { Label(a, 0), At(ac, 1), Label(b, 2), At(bc, 3) } };
    private static Control RowButtons(params Control[] controls) { var p = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 }; foreach (var c in controls) p.Children.Add(c); return p; }
    private static TextBlock Label(string text, int column) { var c = new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(3) }; Grid.SetColumn(c, column); return c; }
    private static T At<T>(T c, int column) where T : Control { Grid.SetColumn(c, column); return c; }
}

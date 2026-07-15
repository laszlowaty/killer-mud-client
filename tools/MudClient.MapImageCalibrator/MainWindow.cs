using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace MudClient.MapImageCalibrator;

public sealed class MainWindow : Window
{
    private readonly IReadOnlyList<MapAtlas> _atlases;
    private readonly string _nortantisDirectory;
    private readonly string? _templatePath;
    private readonly NortantisExportService _exportService = new();
    private readonly RoomSelectionCanvas _canvas = new();
    private readonly ComboBox _atlasPicker = new();
    private readonly ComboBox _zPicker = new();
    private readonly TextBox _projectName = new();
    private readonly TextBox _roomSearch = new();
    private readonly TextBlock _selectionSummary = new();
    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap };
    private readonly ObservableCollection<RoomPoint> _selectedRooms = [];

    public MainWindow(string mapDirectory)
    {
        var repository = new CalibrationRepository(mapDirectory);
        _atlases = repository.LoadAtlases();
        var repositoryRoot = new DirectoryInfo(mapDirectory).Parent?.Parent?.Parent?.Parent?.FullName
            ?? throw new DirectoryNotFoundException("Nie udało się ustalić katalogu repozytorium.");
        _nortantisDirectory = Path.Combine(repositoryRoot, "tools", "Nortantis");
        var projectsDirectory = Path.Combine(_nortantisDirectory, "Projects");
        _templatePath = File.Exists(Path.Combine(projectsDirectory, "old-continent.nort"))
            ? Path.Combine(projectsDirectory, "old-continent.nort")
            : Directory.Exists(projectsDirectory)
                ? Directory.EnumerateFiles(projectsDirectory, "*.nort").FirstOrDefault()
                : null;

        Title = "Eksporter roomów do Nortantis";
        Width = 1320;
        Height = 860;
        MinWidth = 940;
        MinHeight = 620;

        _atlasPicker.ItemsSource = _atlases;
        _atlasPicker.SelectionChanged += (_, _) => SelectAtlas(_atlasPicker.SelectedItem as MapAtlas);
        _zPicker.SelectionChanged += (_, _) => SelectZ();
        _canvas.SelectionChanged += RefreshSelection;
        _projectName.PlaceholderText = "np. arras";
        _roomSearch.PlaceholderText = "vnum lub fragment nazwy";
        Content = BuildLayout();

        Opened += (_, _) =>
        {
            if (_atlases.Count > 0) _atlasPicker.SelectedIndex = 0;
            SetStatus(_templatePath is null
                ? "Brak bazowego pliku .nort w tools/Nortantis/Projects."
                : "Wybierz atlas, poziom Z i zaznacz roomy.");
        };
    }

    private Control BuildLayout()
    {
        var root = new Grid { ColumnDefinitions = new ColumnDefinitions("*,340") };
        root.Children.Add(_canvas);

        var panel = new StackPanel { Margin = new Thickness(12), Spacing = 10 };
        var sidebar = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(24, 27, 26)),
            Child = new ScrollViewer { Content = panel },
        };
        Grid.SetColumn(sidebar, 1);
        root.Children.Add(sidebar);

        panel.Children.Add(Section("Atlas i poziom",
            Label("Atlas"),
            _atlasPicker,
            Label("Poziom Z"),
            _zPicker,
            RowButtons(
                Button("Dopasuj widok", (_, _) => _canvas.Fit()),
                Button("Wyczyść", (_, _) => _canvas.ClearSelection()))));

        panel.Children.Add(Section("Wybór roomów",
            RowButtons(
                Button("Pojedynczo", (_, _) => SetMode(RoomSelectionMode.Toggle)),
                Button("Prostokąt", (_, _) => SetMode(RoomSelectionMode.Rectangle)),
                Button("Lasso", (_, _) => SetMode(RoomSelectionMode.Lasso))),
            Button("Zaznacz wszystkie z poziomu", (_, _) => _canvas.SelectAll()),
            _roomSearch,
            Button("Dodaj pasujące vnum/nazwy", (_, _) => AddMatchingRooms()),
            _selectionSummary,
            new ListBox { ItemsSource = _selectedRooms, Height = 180 }));

        panel.Children.Add(Section("Eksport Nortantis",
            Label("Nazwa projektu"),
            _projectName,
            Button("Utwórz pusty .nort i overlay", (_, _) => Export()),
            new TextBlock
            {
                Text = "Projekt trafia do tools/Nortantis/Projects, a overlay do tools/Nortantis/Overlays. " +
                       "Overlay ma przezroczyste tło i zawiera wyłącznie zaznaczone roomy oraz połączenia między nimi.",
                FontSize = 11,
                Foreground = Brushes.LightGray,
                TextWrapping = TextWrapping.Wrap,
            }));

        _status.FontSize = 12;
        panel.Children.Add(new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(33, 38, 36)),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10),
            Child = _status,
        });
        panel.Children.Add(new TextBlock
        {
            Text = "LPM: przesuwanie  ·  PPM: zaznaczanie  ·  kółko: zoom",
            FontSize = 11,
            Foreground = Brushes.Gray,
        });
        return root;
    }

    private void SelectAtlas(MapAtlas? atlas)
    {
        if (atlas is null) return;
        var levels = atlas.Rooms.Select(room => room.Z).Distinct().OrderBy(level => level).ToList();
        _zPicker.ItemsSource = levels;
        _zPicker.SelectedItem = levels.Contains(0) ? 0d : levels.FirstOrDefault();
        _projectName.Text = ToSuggestedName(atlas.Name, Convert.ToDouble(_zPicker.SelectedItem ?? 0));
        SelectZ();
    }

    private void SelectZ()
    {
        if (_atlasPicker.SelectedItem is not MapAtlas atlas || _zPicker.SelectedItem is not double z) return;
        var rooms = atlas.Rooms.Where(room => Math.Abs(room.Z - z) < 0.0001).ToList();
        _canvas.SetRooms(rooms);
        _projectName.Text = ToSuggestedName(atlas.Name, z);
        SetStatus($"{atlas.Name}, z {z:0.###}: {rooms.Count} roomów.");
    }

    private void SetMode(RoomSelectionMode mode)
    {
        _canvas.Mode = mode;
        SetStatus(mode switch
        {
            RoomSelectionMode.Toggle => "Tryb pojedynczy: klikaj roomy PPM.",
            RoomSelectionMode.Rectangle => "Tryb prostokąta: przeciągnij PPM.",
            _ => "Tryb lassa: obrysuj obszar PPM.",
        });
    }

    private void AddMatchingRooms()
    {
        var query = _roomSearch.Text?.Trim();
        if (string.IsNullOrWhiteSpace(query)) return;
        var matches = _canvas.Rooms.Where(room =>
            room.Vnum?.Contains(query, StringComparison.OrdinalIgnoreCase) == true ||
            room.Name?.Contains(query, StringComparison.CurrentCultureIgnoreCase) == true).ToList();
        foreach (var room in matches) _canvas.SelectedRoomIds.Add(room.Id);
        RefreshSelection();
        _canvas.InvalidateVisual();
        SetStatus(matches.Count == 0 ? "Nie znaleziono pasujących roomów." : $"Dodano {matches.Count} pasujących roomów.");
    }

    private void RefreshSelection()
    {
        _selectedRooms.Clear();
        foreach (var room in _canvas.GetSelectedRooms().OrderBy(room => room.Vnum).ThenBy(room => room.Name))
            _selectedRooms.Add(room);
        _selectionSummary.Text = $"Wybrano: {_selectedRooms.Count}";
    }

    private void Export()
    {
        var selected = _canvas.GetSelectedRooms();
        if (selected.Count == 0)
        {
            SetStatus("Najpierw wybierz co najmniej jeden room.");
            return;
        }
        if (_templatePath is null)
        {
            SetStatus("Brak bazowego projektu .nort w tools/Nortantis/Projects.");
            return;
        }

        try
        {
            var result = _exportService.Export(
                selected,
                _projectName.Text ?? string.Empty,
                _templatePath,
                _nortantisDirectory);
            SetStatus(
                $"Gotowe. Wyeksportowano {selected.Count} roomów.\n" +
                $"Nort: {result.ProjectPath}\n" +
                $"Overlay: {result.OverlayPath}\n" +
                $"Rozmiar overlayu: {result.Layout.OverlayWidth}×{result.Layout.OverlayHeight}");
        }
        catch (Exception exception)
        {
            SetStatus($"Eksport nie powiódł się: {exception.Message}");
        }
    }

    private void SetStatus(string text) => _status.Text = text;

    private static string ToSuggestedName(string atlasName, double z)
    {
        var normalized = string.Concat(atlasName.ToLowerInvariant().Select(character =>
            char.IsLetterOrDigit(character) ? character : '-')).Trim('-');
        if (z == 0) return normalized;
        var level = z < 0 ? $"m{Math.Abs(z):0.###}" : z.ToString("0.###");
        return $"{normalized}-z-{level}";
    }

    private static TextBlock Label(string text) => new() { Text = text, Foreground = Brushes.LightGray };

    private static Button Button(string text, EventHandler<Avalonia.Interactivity.RoutedEventArgs> handler)
    {
        var button = new Button { Content = text, Padding = new Thickness(10, 6) };
        button.Click += handler;
        return button;
    }

    private static StackPanel RowButtons(params Control[] controls)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        foreach (var control in controls) row.Children.Add(control);
        return row;
    }

    private static Border Section(string title, params Control[] controls)
    {
        var stack = new StackPanel { Spacing = 8 };
        stack.Children.Add(new TextBlock
        {
            Text = title.ToUpperInvariant(),
            FontSize = 11,
            FontWeight = FontWeight.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(255, 190, 80)),
        });
        foreach (var control in controls) stack.Children.Add(control);
        return new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(33, 38, 36)),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12, 10),
            Child = stack,
        };
    }
}

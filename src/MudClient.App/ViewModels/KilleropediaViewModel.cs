using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MudClient.App.Models;
using MudClient.App.Services;

namespace MudClient.App.ViewModels;

public sealed class KilleropediaViewModel : ObservableObject
{
    private readonly IReadOnlyList<TeacherEntry> _allTeachers;
    private readonly IReadOnlyList<LoreEntry> _allLoreEntries;
    private readonly IReadOnlyDictionary<string, LoreEntry> _loreById;
    private readonly BookCatalogStore _bookCatalogStore;
    private readonly Func<Task>? _refreshBooksAsync;
    private readonly Action<TeacherEntry>? _showTeacherOnMap;
    private readonly AsyncRelayCommand _refreshBooksCommand;
    private readonly List<BookEntry> _allBooks = [];
    private string _teacherSearchText = string.Empty;
    private TeacherEntry? _selectedTeacher;
    private string _bookSearchText = string.Empty;
    private string _selectedBookClass = "Wszystkie";
    private BookEntry? _selectedBook;
    private bool _isConnected;
    private WorldMapRegion? _selectedWorldMapRegion;
    private bool _isBookRefreshRunning;
    private string _bookRefreshStatus = string.Empty;
    private DateTimeOffset? _booksGeneratedAtUtc;
    private string _loreSearchText = string.Empty;
    private string _selectedLoreCategory = "Wszystkie";
    private LoreEntry? _selectedLoreEntry;
    private readonly DateTimeOffset? _loreGeneratedAtUtc;
    private readonly string _loreSourceText;
    private readonly string? _loreWarning;

    public KilleropediaViewModel()
        : this(TeacherCatalogLoader.Load(), new BookCatalogStore(), null, null, null)
    {
    }

    internal KilleropediaViewModel(
        IReadOnlyList<TeacherEntry> teachers,
        BookCatalogStore bookCatalogStore,
        Func<Task>? refreshBooksAsync,
        Action<TeacherEntry>? showTeacherOnMap = null,
        LoreCatalogData? loreCatalog = null)
    {
        _allTeachers = teachers;
        _bookCatalogStore = bookCatalogStore;
        _refreshBooksAsync = refreshBooksAsync;
        _showTeacherOnMap = showTeacherOnMap;
        var resolvedLoreCatalog = loreCatalog ?? LoreCatalogLoader.Load();
        _allLoreEntries = resolvedLoreCatalog.Entries;
        _loreById = _allLoreEntries.ToDictionary(entry => entry.Id, StringComparer.Ordinal);
        _loreGeneratedAtUtc = resolvedLoreCatalog.GeneratedAtUtc;
        _loreSourceText = resolvedLoreCatalog.SourceText;
        _loreWarning = resolvedLoreCatalog.Warning;
        AvailableLoreCategories = [
            "Wszystkie",
            .. _allLoreEntries.Select(entry => entry.Category).Distinct(StringComparer.Ordinal),
        ];
        _refreshBooksCommand = new AsyncRelayCommand(RefreshBooksAsync, CanRefreshBooks);
        ShowTeacherOnMapCommand = new RelayCommand<TeacherEntry>(
            ShowTeacherOnMap,
            teacher => teacher?.HasRoomLocation == true && _showTeacherOnMap is not null);
        NavigateLoreCommand = new RelayCommand<LoreLink>(
            NavigateLore,
            link => link is not null && _loreById.ContainsKey(link.TargetId));
        ApplyTeacherFilter();
        LoadBookCatalog();
        ApplyLoreFilter();
        _selectedWorldMapRegion = WorldMapRegions.FirstOrDefault();
    }

    public IReadOnlyList<WorldMapRegion> WorldMapRegions { get; } =
        [new WorldMapRegion("Stary Kontynent", "old-continent-overview.png")];

    public WorldMapRegion? SelectedWorldMapRegion
    {
        get => _selectedWorldMapRegion;
        set => SetProperty(ref _selectedWorldMapRegion, value);
    }

    public ObservableCollection<TeacherEntry> FilteredTeachers { get; } = [];

    public ObservableCollection<LoreEntry> FilteredLoreEntries { get; } = [];

    public IReadOnlyList<LoreEntry> LoreEntries => _allLoreEntries;

    public IReadOnlyList<string> AvailableLoreCategories { get; }

    public string LoreSearchText
    {
        get => _loreSearchText;
        set
        {
            if (SetProperty(ref _loreSearchText, value))
            {
                ApplyLoreFilter();
            }
        }
    }

    public string SelectedLoreCategory
    {
        get => _selectedLoreCategory;
        set
        {
            if (SetProperty(ref _selectedLoreCategory, value))
            {
                ApplyLoreFilter();
            }
        }
    }

    public LoreEntry? SelectedLoreEntry
    {
        get => _selectedLoreEntry;
        set => SetProperty(ref _selectedLoreEntry, value);
    }

    public IRelayCommand<LoreLink> NavigateLoreCommand { get; }

    public string FilteredLoreCountText => $"Hasła: {FilteredLoreEntries.Count} z {_allLoreEntries.Count}";

    public bool HasLore => _allLoreEntries.Count > 0;

    public bool HasNoLoreResults => FilteredLoreEntries.Count == 0;

    public string LoreCatalogStatusText
    {
        get
        {
            var generated = _loreGeneratedAtUtc is null
                ? "data nieznana"
                : _loreGeneratedAtUtc.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
            return $"Katalog: {generated} · {_loreSourceText}";
        }
    }

    public string LoreCatalogWarning => _loreWarning ?? string.Empty;

    public bool HasLoreCatalogWarning => !string.IsNullOrWhiteSpace(_loreWarning);

    public string TeacherSearchText
    {
        get => _teacherSearchText;
        set
        {
            if (SetProperty(ref _teacherSearchText, value))
            {
                ApplyTeacherFilter();
            }
        }
    }

    public TeacherEntry? SelectedTeacher
    {
        get => _selectedTeacher;
        set => SetProperty(ref _selectedTeacher, value);
    }

    public string FilteredTeacherCountText => $"Nauczyciele: {FilteredTeachers.Count} z {_allTeachers.Count}";

    public IRelayCommand<TeacherEntry> ShowTeacherOnMapCommand { get; }

    public ObservableCollection<BookEntry> FilteredBooks { get; } = [];

    public IReadOnlyList<string> AvailableBookClasses { get; } =
        ["Wszystkie", .. BookCatalogRefreshCoordinator.BookClasses];

    public IAsyncRelayCommand RefreshBooksCommand => _refreshBooksCommand;

    public bool IsBookRefreshButtonVisible => DeveloperFeatures.ShowBookCatalogRefreshButton;

    public bool IsBookRefreshEnabled =>
        DeveloperFeatures.EnableBookCatalogRefreshButton
        && _isConnected
        && !_isBookRefreshRunning;

    public bool IsBookRefreshRunning
    {
        get => _isBookRefreshRunning;
        private set
        {
            if (SetProperty(ref _isBookRefreshRunning, value))
            {
                OnPropertyChanged(nameof(IsBookRefreshEnabled));
                OnPropertyChanged(nameof(BookRefreshButtonText));
                _refreshBooksCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string BookRefreshButtonText => IsBookRefreshRunning ? "Odświeżanie..." : "Odśwież";

    public string BookRefreshStatus
    {
        get => _bookRefreshStatus;
        private set => SetProperty(ref _bookRefreshStatus, value);
    }

    public string BookSearchText
    {
        get => _bookSearchText;
        set
        {
            if (SetProperty(ref _bookSearchText, value))
            {
                ApplyBookFilter();
            }
        }
    }

    public string SelectedBookClass
    {
        get => _selectedBookClass;
        set
        {
            if (SetProperty(ref _selectedBookClass, value))
            {
                ApplyBookFilter();
            }
        }
    }

    public BookEntry? SelectedBook
    {
        get => _selectedBook;
        set => SetProperty(ref _selectedBook, value);
    }

    public string FilteredBookCountText => $"Księgi: {FilteredBooks.Count} z {_allBooks.Count}";

    public bool HasBooks => _allBooks.Count > 0;

    public bool HasNoBooks => !HasBooks;

    public string BooksGeneratedText => _booksGeneratedAtUtc is null
        ? "Brak wygenerowanego katalogu."
        : $"Katalog: {_booksGeneratedAtUtc.Value.ToLocalTime():yyyy-MM-dd HH:mm}";

    public void SetConnectionState(bool isConnected)
    {
        if (_isConnected == isConnected)
        {
            return;
        }

        _isConnected = isConnected;
        OnPropertyChanged(nameof(IsBookRefreshEnabled));
        _refreshBooksCommand.NotifyCanExecuteChanged();
    }

    public void BeginBookRefresh()
    {
        IsBookRefreshRunning = true;
        BookRefreshStatus = "Rozpoczynanie odświeżania...";
    }

    public void ReportBookRefresh(BookCatalogRefreshProgress progress) =>
        BookRefreshStatus = progress.DisplayText;

    public void CompleteBookRefresh(BookCatalogDocument catalog)
    {
        ApplyBookCatalog(catalog);
        BookRefreshStatus = $"Zapisano {_allBooks.Count} ksiąg do {_bookCatalogStore.Path}.";
        IsBookRefreshRunning = false;
    }

    public void FailBookRefresh(string message)
    {
        BookRefreshStatus = message;
        IsBookRefreshRunning = false;
    }

    private void ApplyTeacherFilter()
    {
        var tokens = Normalize(TeacherSearchText)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var previousId = SelectedTeacher?.MobVnum;

        FilteredTeachers.Clear();
        foreach (var teacher in _allTeachers)
        {
            var haystack = Normalize(string.Join(' ',
                teacher.MobVnum,
                teacher.Name,
                teacher.Region,
                teacher.Area,
                teacher.RoomVnum,
                teacher.ClassesText,
                string.Join(' ', teacher.Skills.Select(skill => skill.Name)),
                string.Join(' ', teacher.Tricks.Select(trick => trick.Name))));
            if (tokens.All(haystack.Contains))
            {
                FilteredTeachers.Add(teacher);
            }
        }

        SelectedTeacher = FilteredTeachers.FirstOrDefault(teacher => teacher.MobVnum == previousId)
            ?? FilteredTeachers.FirstOrDefault();
        OnPropertyChanged(nameof(FilteredTeacherCountText));
    }

    private void ApplyLoreFilter()
    {
        var tokens = Normalize(LoreSearchText)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var previousId = SelectedLoreEntry?.Id;

        FilteredLoreEntries.Clear();
        foreach (var entry in _allLoreEntries)
        {
            if (!string.Equals(SelectedLoreCategory, "Wszystkie", StringComparison.Ordinal)
                && !string.Equals(entry.Category, SelectedLoreCategory, StringComparison.Ordinal))
            {
                continue;
            }

            var haystack = Normalize(entry.SearchableText);
            if (tokens.All(haystack.Contains))
            {
                FilteredLoreEntries.Add(entry);
            }
        }

        SelectedLoreEntry = FilteredLoreEntries.FirstOrDefault(entry => entry.Id == previousId)
            ?? FilteredLoreEntries.FirstOrDefault();
        OnPropertyChanged(nameof(FilteredLoreCountText));
        OnPropertyChanged(nameof(HasNoLoreResults));
    }

    private void NavigateLore(LoreLink? link)
    {
        if (link is null || !_loreById.TryGetValue(link.TargetId, out var target))
        {
            return;
        }

        if (!string.Equals(SelectedLoreCategory, "Wszystkie", StringComparison.Ordinal))
        {
            SelectedLoreCategory = "Wszystkie";
        }

        if (!string.IsNullOrWhiteSpace(LoreSearchText))
        {
            LoreSearchText = string.Empty;
        }

        SelectedLoreEntry = target;
    }

    private async Task RefreshBooksAsync()
    {
        if (_refreshBooksAsync is not null)
        {
            await _refreshBooksAsync();
        }
    }

    private void ShowTeacherOnMap(TeacherEntry? teacher)
    {
        if (teacher?.HasRoomLocation == true)
        {
            _showTeacherOnMap?.Invoke(teacher);
        }
    }

    private bool CanRefreshBooks() => IsBookRefreshEnabled && _refreshBooksAsync is not null;

    private void LoadBookCatalog()
    {
        try
        {
            ApplyBookCatalog(_bookCatalogStore.Load());
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or InvalidOperationException)
        {
            ApplyBookCatalog(new BookCatalogDocument());
            BookRefreshStatus = exception.Message;
        }
    }

    private void ApplyBookCatalog(BookCatalogDocument catalog)
    {
        _allBooks.Clear();
        _allBooks.AddRange(catalog.Books.OrderBy(book => book.Name, StringComparer.OrdinalIgnoreCase));
        _booksGeneratedAtUtc = catalog.GeneratedAtUtc;
        ApplyBookFilter();
        OnPropertyChanged(nameof(HasBooks));
        OnPropertyChanged(nameof(HasNoBooks));
        OnPropertyChanged(nameof(BooksGeneratedText));
    }

    private void ApplyBookFilter()
    {
        var tokens = Normalize(BookSearchText)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var previousVnum = SelectedBook?.Vnum;
        FilteredBooks.Clear();

        foreach (var book in _allBooks)
        {
            if (!string.Equals(SelectedBookClass, "Wszystkie", StringComparison.OrdinalIgnoreCase)
                && !book.Classes.Contains(SelectedBookClass, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            var haystack = Normalize(string.Join(' ',
                book.Vnum,
                book.Name,
                string.Join(' ', book.Classes),
                string.Join(' ', book.Spells),
                string.Join(' ', book.LoadLocations)));
            if (tokens.All(haystack.Contains))
            {
                FilteredBooks.Add(book);
            }
        }

        SelectedBook = FilteredBooks.FirstOrDefault(book => book.Vnum == previousVnum)
            ?? FilteredBooks.FirstOrDefault();
        OnPropertyChanged(nameof(FilteredBookCountText));
    }

    private static string Normalize(string? value) => SearchText.Normalize(value);
}

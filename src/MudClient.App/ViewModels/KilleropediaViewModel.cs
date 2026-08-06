using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MudClient.App.Models;
using MudClient.App.Services;
using MudClient.Core.Killeropedia;

namespace MudClient.App.ViewModels;

public sealed class KilleropediaViewModel : ObservableObject
{
    private readonly IReadOnlyList<TeacherEntry> _allTeachers;
    private readonly IReadOnlyList<QuestEntry> _allQuests;
    private readonly TattooCatalogData _tattooCatalog;
    private readonly IReadOnlyList<LoreEntry> _allLoreEntries;
    private readonly IReadOnlyDictionary<string, LoreEntry> _loreById;
    private readonly BookCatalogStore _bookCatalogStore;
    private readonly Func<Task>? _refreshBooksAsync;
    private readonly Action<TeacherEntry>? _showTeacherOnMap;
    private readonly Action<BookLoadLocationEntry>? _showBookLocationOnMap;
    private readonly AsyncRelayCommand _refreshBooksCommand;
    private readonly List<BookEntry> _allBooks = [];
    private readonly RareCatalogStore _rareCatalogStore;
    private readonly Func<Task>? _refreshRaresAsync;
    private readonly AsyncRelayCommand _refreshRaresCommand;
    private readonly List<RareEntry> _allRares = [];
    private string _teacherSearchText = string.Empty;
    private string _questSearchText = string.Empty;
    private QuestEntry? _selectedQuest;
    private string _tattooSearchText = string.Empty;
    private TattooBonusEntry? _selectedTattoo;
    private bool _isTattooInfoExpanded;
    private TeacherEntry? _selectedTeacher;
    private string _bookSearchText = string.Empty;
    private string _selectedBookClass = "Wszystkie";
    private BookEntry? _selectedBook;
    private bool _isConnected;
    private WorldMapRegion? _selectedWorldMapRegion;
    private bool _isBookRefreshRunning;
    private string _bookRefreshStatus = string.Empty;
    private DateTimeOffset? _booksGeneratedAtUtc;
    private string _rareSearchText = string.Empty;
    private string _selectedRareCategory = "Wszystkie";
    private RareEntry? _selectedRare;
    private bool _isRareRefreshRunning;
    private string _rareRefreshStatus = string.Empty;
    private DateTimeOffset? _raresGeneratedAtUtc;
    private string _loreSearchText = string.Empty;
    private string _selectedLoreCategory = "Wszystkie";
    private LoreEntry? _selectedLoreEntry;
    private readonly DateTimeOffset? _loreGeneratedAtUtc;
    private readonly string _loreSourceText;
    private readonly string? _loreWarning;

    public KilleropediaViewModel()
        : this(TeacherCatalogLoader.Load(), new BookCatalogStore(), null, null, null, null, QuestCatalogLoader.Load())
    {
    }

    internal KilleropediaViewModel(
        IReadOnlyList<TeacherEntry> teachers,
        BookCatalogStore bookCatalogStore,
        Func<Task>? refreshBooksAsync,
        Action<TeacherEntry>? showTeacherOnMap = null,
        LoreCatalogData? loreCatalog = null,
        string? mapDirectory = null,
        IReadOnlyList<QuestEntry>? quests = null,
        Action<BookLoadLocationEntry>? showBookLocationOnMap = null,
        TattooCatalogData? tattooCatalog = null,
        RareCatalogStore? rareCatalogStore = null,
        Func<Task>? refreshRaresAsync = null)
    {
        _allTeachers = teachers;
        _allQuests = quests ?? QuestCatalogLoader.Load();
        _tattooCatalog = tattooCatalog ?? TattooCatalogLoader.Load();
        _bookCatalogStore = bookCatalogStore;
        _refreshBooksAsync = refreshBooksAsync;
        _rareCatalogStore = rareCatalogStore ?? new RareCatalogStore();
        _refreshRaresAsync = refreshRaresAsync;
        _showTeacherOnMap = showTeacherOnMap;
        _showBookLocationOnMap = showBookLocationOnMap;
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
        _refreshRaresCommand = new AsyncRelayCommand(RefreshRaresAsync, CanRefreshRares);
        WorldMapRegions = [new WorldMapRegion("Stary Kontynent", "old-continent-overview.png", mapDirectory)];
        ShowTeacherOnMapCommand = new RelayCommand<TeacherEntry>(
            ShowTeacherOnMap,
            teacher => teacher?.HasRoomLocation == true && _showTeacherOnMap is not null);
        ShowBookLocationOnMapCommand = new RelayCommand<BookLoadLocationEntry>(
            ShowBookLocationOnMap,
            location => location?.HasRoomLocation == true && _showBookLocationOnMap is not null);
        NavigateLoreCommand = new RelayCommand<LoreLink>(
            NavigateLore,
            link => link is not null && _loreById.ContainsKey(link.TargetId));
        ToggleTattooInfoCommand = new RelayCommand(() => IsTattooInfoExpanded = !IsTattooInfoExpanded);
        ApplyTeacherFilter();
        ApplyQuestFilter();
        ApplyTattooFilter();
        LoadBookCatalog();
        LoadRareCatalog();
        ApplyLoreFilter();
        _selectedWorldMapRegion = WorldMapRegions.FirstOrDefault();
    }

    public IReadOnlyList<WorldMapRegion> WorldMapRegions { get; }

    public WorldMapRegion? SelectedWorldMapRegion
    {
        get => _selectedWorldMapRegion;
        set => SetProperty(ref _selectedWorldMapRegion, value);
    }

    public ObservableCollection<TeacherEntry> FilteredTeachers { get; } = [];

    public ObservableCollection<QuestEntry> FilteredQuests { get; } = [];

    public string QuestSearchText
    {
        get => _questSearchText;
        set
        {
            if (SetProperty(ref _questSearchText, value))
            {
                ApplyQuestFilter();
            }
        }
    }

    public QuestEntry? SelectedQuest
    {
        get => _selectedQuest;
        set => SetProperty(ref _selectedQuest, value);
    }

    public string FilteredQuestCountText => $"Zadania: {FilteredQuests.Count} z {_allQuests.Count}";

    public bool HasNoQuestResults => FilteredQuests.Count == 0;

    public string TattooIntro => _tattooCatalog.Intro;

    public IReadOnlyList<TattooCommandEntry> TattooCommands => _tattooCatalog.Commands;

    public IReadOnlyList<TattooRuneEntry> TattooRuneTypes => _tattooCatalog.RuneTypes;

    public string TattooStackingNotes => _tattooCatalog.StackingNotes;

    public bool IsTattooInfoExpanded
    {
        get => _isTattooInfoExpanded;
        set => SetProperty(ref _isTattooInfoExpanded, value);
    }

    public IRelayCommand ToggleTattooInfoCommand { get; }

    public ObservableCollection<TattooBonusEntry> FilteredTattoos { get; } = [];

    public string TattooSearchText
    {
        get => _tattooSearchText;
        set
        {
            if (SetProperty(ref _tattooSearchText, value))
            {
                ApplyTattooFilter();
            }
        }
    }

    public TattooBonusEntry? SelectedTattoo
    {
        get => _selectedTattoo;
        set => SetProperty(ref _selectedTattoo, value);
    }

    public string FilteredTattooCountText => $"Bonusy: {FilteredTattoos.Count} z {_tattooCatalog.Bonuses.Count}";

    public bool HasNoTattooResults => FilteredTattoos.Count == 0;

    public IReadOnlyList<string> RandomBookMagWords => RandomBookNaming.MagWords;

    public IReadOnlyList<string> RandomBookKlerykWords => RandomBookNaming.KlerykWords;

    public IReadOnlyList<string> RandomBookPaladynWords => RandomBookNaming.PaladynWords;

    public IReadOnlyList<string> RandomBookDruidWords => RandomBookNaming.DruidWords;

    public IReadOnlyList<string> RandomBookNomadWords => RandomBookNaming.NomadWords;

    public IReadOnlyList<string> RandomBookNazwaWords => RandomBookNaming.NazwaWords;

    public IReadOnlyList<string> RandomBookWartoscDuzaWords => RandomBookNaming.WartoscDuzaWords;

    public IReadOnlyList<string> RandomBookWartoscMalaWords => RandomBookNaming.WartoscMalaWords;

    public IReadOnlyList<string> RandomBookWagaMalaWords => RandomBookNaming.WagaMalaWords;

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

    public IRelayCommand<BookLoadLocationEntry> ShowBookLocationOnMapCommand { get; }

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

    public ObservableCollection<RareEntry> FilteredRares { get; } = [];

    public IReadOnlyList<string> AvailableRareCategories { get; } = ["Wszystkie", "artefakt", "rzadki", "instancyjny"];

    public IAsyncRelayCommand RefreshRaresCommand => _refreshRaresCommand;

    public bool IsRareRefreshButtonVisible => DeveloperFeatures.ShowRareCatalogRefreshButton;

    public bool IsRareRefreshEnabled =>
        DeveloperFeatures.EnableRareCatalogRefreshButton
        && _isConnected
        && !_isRareRefreshRunning;

    public bool IsRareRefreshRunning
    {
        get => _isRareRefreshRunning;
        private set
        {
            if (SetProperty(ref _isRareRefreshRunning, value))
            {
                OnPropertyChanged(nameof(IsRareRefreshEnabled));
                OnPropertyChanged(nameof(RareRefreshButtonText));
                _refreshRaresCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string RareRefreshButtonText => IsRareRefreshRunning ? "Odświeżanie..." : "Odśwież";

    public string RareRefreshStatus
    {
        get => _rareRefreshStatus;
        private set => SetProperty(ref _rareRefreshStatus, value);
    }

    public string RareSearchText
    {
        get => _rareSearchText;
        set
        {
            if (SetProperty(ref _rareSearchText, value))
            {
                ApplyRareFilter();
            }
        }
    }

    public string SelectedRareCategory
    {
        get => _selectedRareCategory;
        set
        {
            if (SetProperty(ref _selectedRareCategory, value))
            {
                ApplyRareFilter();
            }
        }
    }

    public RareEntry? SelectedRare
    {
        get => _selectedRare;
        set => SetProperty(ref _selectedRare, value);
    }

    public string FilteredRareCountText => $"Przedmioty: {FilteredRares.Count} z {_allRares.Count}";

    public bool HasRares => _allRares.Count > 0;

    public bool HasNoRares => !HasRares;

    public string RaresGeneratedText => _raresGeneratedAtUtc is null
        ? "Brak wygenerowanego katalogu."
        : $"Katalog: {_raresGeneratedAtUtc.Value.ToLocalTime():yyyy-MM-dd HH:mm}";

    public void SetConnectionState(bool isConnected)
    {
        if (_isConnected == isConnected)
        {
            return;
        }

        _isConnected = isConnected;
        OnPropertyChanged(nameof(IsBookRefreshEnabled));
        _refreshBooksCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(IsRareRefreshEnabled));
        _refreshRaresCommand.NotifyCanExecuteChanged();
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

    public void BeginRareRefresh()
    {
        IsRareRefreshRunning = true;
        RareRefreshStatus = "Rozpoczynanie odświeżania...";
    }

    public void ReportRareRefresh(RareCatalogRefreshProgress progress) =>
        RareRefreshStatus = progress.DisplayText;

    public void CompleteRareRefresh(RareCatalogDocument catalog)
    {
        ApplyRareCatalog(catalog);
        RareRefreshStatus = $"Zapisano {_allRares.Count} przedmiotów do {_rareCatalogStore.Path}.";
        IsRareRefreshRunning = false;
    }

    public void FailRareRefresh(string message)
    {
        RareRefreshStatus = message;
        IsRareRefreshRunning = false;
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

    private void ApplyQuestFilter()
    {
        var tokens = Normalize(QuestSearchText)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var previousName = SelectedQuest?.Name;

        FilteredQuests.Clear();
        foreach (var quest in _allQuests)
        {
            if (tokens.All(token => Normalize(quest.SearchableText).Contains(token)))
            {
                FilteredQuests.Add(quest);
            }
        }

        SelectedQuest = FilteredQuests.FirstOrDefault(quest => quest.Name == previousName)
            ?? FilteredQuests.FirstOrDefault();
        OnPropertyChanged(nameof(FilteredQuestCountText));
        OnPropertyChanged(nameof(HasNoQuestResults));
    }

    private void ApplyTattooFilter()
    {
        var tokens = Normalize(TattooSearchText)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var previousName = SelectedTattoo?.Name;

        FilteredTattoos.Clear();
        foreach (var bonus in _tattooCatalog.Bonuses)
        {
            if (tokens.All(token => Normalize(bonus.SearchableText).Contains(token)))
            {
                FilteredTattoos.Add(bonus);
            }
        }

        SelectedTattoo = FilteredTattoos.FirstOrDefault(bonus => bonus.Name == previousName)
            ?? FilteredTattoos.FirstOrDefault();
        OnPropertyChanged(nameof(FilteredTattooCountText));
        OnPropertyChanged(nameof(HasNoTattooResults));
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

    private void ShowBookLocationOnMap(BookLoadLocationEntry? location)
    {
        if (location?.HasRoomLocation == true)
        {
            _showBookLocationOnMap?.Invoke(location);
        }
    }

    private bool CanRefreshBooks() => IsBookRefreshEnabled && _refreshBooksAsync is not null;

    private async Task RefreshRaresAsync()
    {
        if (_refreshRaresAsync is not null)
        {
            await _refreshRaresAsync();
        }
    }

    private bool CanRefreshRares() => IsRareRefreshEnabled && _refreshRaresAsync is not null;

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

    private void LoadRareCatalog()
    {
        try
        {
            ApplyRareCatalog(_rareCatalogStore.Load());
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or InvalidOperationException)
        {
            ApplyRareCatalog(new RareCatalogDocument());
            RareRefreshStatus = exception.Message;
        }
    }

    private void ApplyRareCatalog(RareCatalogDocument catalog)
    {
        _allRares.Clear();
        _allRares.AddRange(catalog.Rares.OrderBy(rare => rare.Name, StringComparer.OrdinalIgnoreCase));
        _raresGeneratedAtUtc = catalog.GeneratedAtUtc;
        ApplyRareFilter();
        OnPropertyChanged(nameof(HasRares));
        OnPropertyChanged(nameof(HasNoRares));
        OnPropertyChanged(nameof(RaresGeneratedText));
    }

    private void ApplyRareFilter()
    {
        var tokens = Normalize(RareSearchText)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var previousVnum = SelectedRare?.Vnum;
        FilteredRares.Clear();

        foreach (var rare in _allRares)
        {
            if (!string.Equals(SelectedRareCategory, "Wszystkie", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(rare.Category, SelectedRareCategory, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (tokens.All(token => Normalize(rare.SearchableText).Contains(token)))
            {
                FilteredRares.Add(rare);
            }
        }

        SelectedRare = FilteredRares.FirstOrDefault(rare => rare.Vnum == previousVnum)
            ?? FilteredRares.FirstOrDefault();
        OnPropertyChanged(nameof(FilteredRareCountText));
    }

    private static string Normalize(string? value) => SearchText.Normalize(value);
}

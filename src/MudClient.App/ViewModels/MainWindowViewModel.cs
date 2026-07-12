using System.Collections.ObjectModel;
using System.IO;
using Avalonia.Threading;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Text;
using System.Text.RegularExpressions;
using Dock.Model.Controls;
using MudClient.App.Docking;
using MudClient.App.Controls;
using MudClient.App.Models;
using MudClient.App.Services;
using MudClient.Core.Automation;
using MudClient.Core.Gmcp;
using MudClient.Core.Map;
using MudClient.Core.Networking;

namespace MudClient.App.ViewModels;

public sealed class MainWindowViewModel : ObservableObject, IAsyncDisposable
{
    private readonly MudSession _session = new();
    private readonly AliasEngine _aliases = new();
    private readonly TriggerEngine _triggers = new();
    private readonly MudTimerService _timers = new();
    private readonly GmcpLocationResolver _locationResolver = new();
    private readonly RoomExitsResolver _roomExits = new();
    private readonly CharacterStateResolver _characterState = new();
    private readonly AutoAssistPolicy _autoAssist = new();
    private readonly ProfileService _profiles;

    private readonly SemaphoreSlim _triggerSendLock = new(1, 1);
    private CancellationTokenSource _triggerCts = new();

    // Tracks fire-and-forget trigger-batch tasks so they can be safely
    // drained during DisposeAsync, preventing unobserved exceptions
    // and ensuring no task holds _triggerSendLock when it is disposed.
    private readonly object _triggerTasksLock = new();
    private readonly List<Task> _triggerTasks = new();

    /// <summary>
    /// Tail of the FIFO task chain that guarantees trigger batches are
    /// sent in receive order.  Each new batch created by
    /// <c>OnLineReceived</c> awaits this task (swallowing its faults)
    /// before sending its own commands.  Read and written under
    /// <see cref="_triggerTasksLock"/>.
    /// </summary>
    private Task _triggerQueueTail = Task.CompletedTask;

    /// <summary>
    /// When false, new trigger tasks are rejected.  Set and read under
    /// <see cref="_triggerTasksLock"/> to make task acceptance atomic with
    /// disposal, preventing the shutdown race where <c>DisposeAsync</c>
    /// drains an empty list and disposes the semaphore before
    /// <c>OnLineReceived</c> registers a task that will later touch it.
    /// </summary>
    private bool _acceptingTriggerTasks = true;

    private CharacterGroupUpdate? _latestGroupUpdate;
    private IReadOnlyList<RoomPerson> _latestRoomPeople = [];
    private string? _latestCharacterName;
    private string? _latestCharacterPosition;

    private readonly AsyncRelayCommand _connectCommand;
    private readonly AsyncRelayCommand _disconnectCommand;
    private readonly AsyncRelayCommand _sendCommandCommand;
    private readonly AsyncRelayCommand _retryStartupCommand;

    private readonly MudDockFactory _dockFactory;
    private readonly DockLayoutService _dockLayoutService;
    private readonly LayoutPresetService _layoutPresetService;
    private readonly List<LayoutPreset> _layoutPresets;
    private IRootDock _layout = null!;
    private string _newLayoutName = string.Empty;

    private string _host = "killer-mud.pl";
    private int _port = 4004;
    private string _commandText = string.Empty;
    private string _statusText = "Rozłączono";
    private bool _isConnected;
    private bool _isBusy;
    private string? _startupErrorMessage;
    private string? _startupErrorDetails;

    // --- New UI additions ---
    private string _headerAreaText = "--- Niepołączono ---";
    private int _selectedRightTab;
    private int _selectedLogTab;
    private string _newNoteTitle = string.Empty;
    private string _newNoteContent = string.Empty;
    private bool _newNoteIsGlobal;
    private NoteEntry? _editedNote;
    private bool _isNoteFormExpanded;

    // --- App settings ---
    private readonly AppSettingsService _settingsService;
    private readonly AppSettings _settings;
    private bool _settingsLoaded;

    // --- New alias/trigger form ---
    private string _newRuleName = string.Empty;
    private string _newRuleType = "alias";
    private string _newRulePattern = string.Empty;
    private string _newRuleAction = string.Empty;
    private string? _newRulePatternError;
    private bool _newRuleIsGlobal;
    private AutomationRuleEntry? _editedRule;
    private bool _isRuleFormExpanded;

    // --- Timers ---
    private string _newTimerName = string.Empty;
    private string _newTimerMinutes = "0";
    private string _newTimerSeconds = "0";
    private string _newTimerMilliseconds = "0";
    private string _newTimerCommands = string.Empty;
    private bool _newTimerIsGlobal;
    private TimerEntry? _editedTimer;
    private bool _isTimerFormExpanded;

    // --- Autowalk ---
    private string _newLocationName = string.Empty;
    private string _newLocationVnum = string.Empty;
    private bool _newLocationIsGlobal;
    private MapPathfinder? _pathfinder;
    private MapIndex? _pathfinderIndex;
    private MapPath? _autowalkPath;
    private int _autowalkStep;
    private int _autowalkRecomputes;
    private string? _autowalkTargetName;
    private string _autowalkStatusText = "Bezczynny.";
    private AutowalkLocation? _temporaryTarget;
    private CancellationTokenSource _autowalkCts = new();
    private int? _latestMovement;
    private int? _latestMaximumMovement;
    private IReadOnlyList<MemorizedSpell> _latestMemorizedSpells = [];
    private bool _autowalkRecoveringMovement;
    private int? _autowalkOpeningStep;
    private bool _autowalkWaitingForGate;
    private bool _autowalkGateCommandsSent;
    private bool _autowalkGateIsOpen;

    // --- Required buffs ---
    private string _newBuffName = string.Empty;

    /// <summary>
    /// Normalized names from the latest Char.Affects, used to mark
    /// required buffs as active/missing. Updated on the UI thread.
    /// </summary>
    private readonly HashSet<string> _activeAffectNames = new(StringComparer.OrdinalIgnoreCase);

    // --- Profiles ---
    private string? _activeProfileName;
    private string? _selectedProfileName;
    private string _newProfileName = string.Empty;
    private string _newProfilePassword = string.Empty;
    private string _selectedProfilePassword = string.Empty;

    /// <summary>Decrypted password of the active account, kept only in memory.</summary>
    private string _activeProfilePassword = string.Empty;

    public MainWindowViewModel(ProfileService? profileService = null, AppSettingsService? settingsService = null)
    {
        _profiles = profileService ?? new ProfileService();
        _settingsService = settingsService ?? new AppSettingsService();
        _settings = _settingsService.Load();
        ApplyWidgetFontResources();
        PopulateAvailableFonts();
        _settingsLoaded = true;
        _connectCommand = new AsyncRelayCommand(ConnectAsync, CanConnect);
        _disconnectCommand = new AsyncRelayCommand(DisconnectAsync, CanDisconnect);
        _sendCommandCommand = new AsyncRelayCommand(SendCurrentCommandAsync, CanSendCommand);
        _retryStartupCommand = new AsyncRelayCommand(RetryStartupAsync);
        ExaminePersonCommand = new RelayCommand<string>(ExecuteExaminePerson);
        KillPersonCommand = new RelayCommand<string>(ExecuteKillPerson);
        SelectProfileCommand = new RelayCommand(SelectProfile, () => !string.IsNullOrWhiteSpace(SelectedProfileName));
        CreateProfileCommand = new RelayCommand(CreateProfile, () => !string.IsNullOrWhiteSpace(NewProfileName));
        SwitchProfileCommand = new RelayCommand(SwitchProfile, () => IsProfileSelected && !IsConnected && !IsBusy);
        DeleteProfileCommand = new RelayCommand<string>(DeleteProfile);
        AddTimerCommand = new RelayCommand(AddTimer, () => !string.IsNullOrWhiteSpace(NewTimerName));
        DeleteTimerCommand = new RelayCommand<TimerEntry>(DeleteTimer);
        ToggleTimerCommand = new RelayCommand<TimerEntry>(ToggleTimer);
        EditTimerCommand = new RelayCommand<TimerEntry>(EditTimer);
        CancelTimerEditCommand = new RelayCommand(CancelTimerEdit);
        AddRuleCommand = new RelayCommand(AddRule, CanAddRule);
        DeleteRuleCommand = new RelayCommand<AutomationRuleEntry>(DeleteRule);
        ToggleRuleCommand = new RelayCommand<AutomationRuleEntry>(ToggleRule);
        EditRuleCommand = new RelayCommand<AutomationRuleEntry>(EditRule);
        CancelRuleEditCommand = new RelayCommand(CancelRuleEdit);
        AddCurrentLocationCommand = new RelayCommand(AddCurrentLocation);
        AddLocationCommand = new RelayCommand(AddLocation);
        DeleteLocationCommand = new RelayCommand<AutowalkLocation>(DeleteLocation);
        DeleteDeathCommand = new RelayCommand<DeathMarkEntry>(DeleteDeath);
        GoToDeathCommand = new RelayCommand<DeathMarkEntry>(GoToDeath);
        AddBuffCommand = new RelayCommand(AddBuff, () => !string.IsNullOrWhiteSpace(NewBuffName));
        DeleteBuffCommand = new RelayCommand<BuffWatchEntry>(DeleteBuff);
        RecastBuffsCommand = new AsyncRelayCommand(RecastMissingBuffsAsync);
        GoToLocationCommand = new RelayCommand<AutowalkLocation>(entry =>
        {
            if (entry is not null)
            {
                StartAutowalk(entry);
            }
        });
        StopAutowalkCommand = new RelayCommand(() => StopAutowalk("Autowalk zatrzymany."));
        GoToTemporaryTargetCommand = new RelayCommand(() =>
        {
            if (_temporaryTarget is not null)
            {
                StartAutowalk(_temporaryTarget);
            }
        });
        GoToSelectedTargetCommand = new RelayCommand(HandleGoToSelectedTarget);

        _characterState.VitalsChanged += OnCharacterVitalsChanged;
        _characterState.ConditionChanged += OnCharacterConditionChanged;
        _characterState.PeopleChanged += OnRoomPeopleChanged;
        _characterState.GroupChanged += OnGroupChanged;
        _characterState.AffectsChanged += OnCharacterAffectsChanged;
        _characterState.MemSpellsChanged += OnMemSpellsChanged;

        _session.TextReceived += OnTextReceived;
        _session.LineReceived += OnLineReceived;
        _session.GmcpReceived += OnGmcpReceived;
        _session.GmcpSent += OnGmcpSent;
        _session.StatusChanged += OnStatusChanged;
        _session.ConnectionError += OnConnectionError;
        _session.ConnectionClosed += OnConnectionClosed;

        Map = new MapViewModel(AppContext.BaseDirectory, _locationResolver);
        Map.PropertyChanged += OnMapPropertyChanged;
        _locationResolver.LocationChanged += OnAutowalkLocationChanged;
        _roomExits.ExitsChanged += OnRoomExitsChanged;
        Map.RoomDoubleClicked += OnMapRoomDoubleClicked;

        _dockFactory = new MudDockFactory(Map, this);
        _dockLayoutService = new DockLayoutService();
        Layout = _dockFactory.CreateLayout();
        _dockFactory.InitLayout(Layout);

        var savedLayout = _dockLayoutService.Load();
        if (savedLayout is not null)
        {
            _dockFactory.TryApplySnapshot(Layout, savedLayout);
        }

        _dockFactory.HiddenTools.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HiddenPanels));
        RestorePanelCommand = new RelayCommand<PanelTool>(tool =>
        {
            if (tool is not null)
            {
                _dockFactory.RestoreToTopEdge(tool);
            }
        });

        _layoutPresetService = new LayoutPresetService();
        _layoutPresets = _layoutPresetService.Load();
        RefreshAvailableLayouts();
        ApplyLayoutCommand = new RelayCommand<string>(ApplyLayout);
        SaveLayoutCommand = new RelayCommand(SaveLayout);
        DeleteLayoutCommand = new RelayCommand<string>(DeleteLayout);

        PopulateMockData();

        foreach (var name in _profiles.ListProfileNames())
        {
            AvailableProfiles.Add(name);
        }

        // Global entries are usable even before any profile is selected.
        LoadGlobalEntries();
        ApplyAutomation();
        SyncAllTimers();

        AvailableProfiles.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasProfiles));
    }

    public MapViewModel Map { get; }

    public IRootDock Layout
    {
        get => _layout;
        private set => SetProperty(ref _layout, value);
    }

    public ObservableCollection<PanelTool> HiddenPanels => _dockFactory.HiddenTools;

    public IRelayCommand<PanelTool> RestorePanelCommand { get; }

    /// <summary>Called after every dock drag ends: panels the drag pipeline lost (dropped over
    /// non-dock chrome like the top bar) are moved to <see cref="HiddenPanels"/> for restore.</summary>
    public void ReclaimLostPanels() => _dockFactory.ReclaimLostTools(Layout);

    /// <summary>Layout entries offered in the "Układ" menu: built-in DEFAULT first, then saved presets.</summary>
    public ObservableCollection<LayoutMenuItem> AvailableLayouts { get; } = new();

    public IRelayCommand<string> ApplyLayoutCommand { get; }

    public IRelayCommand SaveLayoutCommand { get; }

    public IRelayCommand<string> DeleteLayoutCommand { get; }

    /// <summary>Name typed into the "zapisz układ" field before saving the current arrangement.</summary>
    public string NewLayoutName
    {
        get => _newLayoutName;
        set => SetProperty(ref _newLayoutName, value);
    }

    private void RefreshAvailableLayouts()
    {
        AvailableLayouts.Clear();
        AvailableLayouts.Add(new LayoutMenuItem { Name = LayoutPresetService.DefaultName, CanDelete = false });
        foreach (var preset in _layoutPresets)
        {
            AvailableLayouts.Add(new LayoutMenuItem { Name = preset.Name, CanDelete = true });
        }
    }

    /// <summary>Restores the built-in default layout or a named preset.</summary>
    private void ApplyLayout(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        // The default is always regenerated fresh so newly-added panels are included.
        var fresh = _dockFactory.ResetToDefault();

        if (!string.Equals(name, LayoutPresetService.DefaultName, StringComparison.Ordinal))
        {
            var preset = _layoutPresets.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.Ordinal));
            if (preset is null)
            {
                return;
            }

            if (!_dockFactory.TryApplySnapshot(fresh, preset.Snapshot))
            {
                // Snapshot no longer matches the current set of panels (e.g. after an update).
                AddToast($"Układ „{name}” jest nieaktualny — wczytano DEFAULT.", "warning");
            }
        }

        Layout = fresh;
        OnPropertyChanged(nameof(HiddenPanels));

        // ResetToDefault/TryApplySnapshot recreate all tools with default titles.
        UpdateBuffsToolTitle();
    }

    private void SaveLayout()
    {
        var name = NewLayoutName.Trim();
        if (name.Length == 0)
        {
            return;
        }

        if (string.Equals(name, LayoutPresetService.DefaultName, StringComparison.OrdinalIgnoreCase))
        {
            AddToast("Nazwa „DEFAULT” jest zarezerwowana.", "warning");
            return;
        }

        var snapshot = _dockFactory.Snapshot(Layout);
        var existing = _layoutPresets.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.Ordinal));
        if (existing is not null)
        {
            existing.Snapshot = snapshot;
        }
        else
        {
            _layoutPresets.Add(new LayoutPreset { Name = name, Snapshot = snapshot });
        }

        _layoutPresetService.Save(_layoutPresets);
        RefreshAvailableLayouts();
        NewLayoutName = string.Empty;
        AddToast($"Zapisano układ „{name}”.", "info");
    }

    private void DeleteLayout(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)
            || string.Equals(name, LayoutPresetService.DefaultName, StringComparison.Ordinal))
        {
            return;
        }

        var removed = _layoutPresets.RemoveAll(p => string.Equals(p.Name, name, StringComparison.Ordinal));
        if (removed > 0)
        {
            _layoutPresetService.Save(_layoutPresets);
            RefreshAvailableLayouts();
            AddToast($"Usunięto układ „{name}”.", "info");
        }
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        ClearStartupError();
        await Map.InitializeAsync(cancellationToken);
    }

    public event Action<string>? OutputReceived;

    /// <summary>Raised when a profile becomes active; the view auto-connects then.</summary>
    public event Action<string>? ProfileActivated;

    // ========================================================================
    // Existing connection / command properties (preserved unchanged)
    // ========================================================================

    public string Host
    {
        get => _host;
        set
        {
            if (SetProperty(ref _host, value))
            {
                RefreshCommands();
            }
        }
    }

    public int Port
    {
        get => _port;
        set
        {
            if (SetProperty(ref _port, value))
            {
                RefreshCommands();
            }
        }
    }

    public string CommandText
    {
        get => _commandText;
        set
        {
            if (SetProperty(ref _commandText, value))
            {
                _sendCommandCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public bool IsConnected
    {
        get => _isConnected;
        private set
        {
            if (SetProperty(ref _isConnected, value))
            {
                RefreshCommands();
                if (value)
                {
                    HeaderAreaText = $"Połączono z {Host}:{Port}";
                }
                else
                {
                    _autoAssist.Reset();
                    HeaderAreaText = "--- Rozłączono ---";
                }
            }
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                RefreshCommands();
            }
        }
    }

    public ObservableCollection<GmcpEntryViewModel> GmcpMessages { get; } = [];

    public ObservableCollection<GmcpEntryViewModel> SentGmcpMessages { get; } = [];

    public IAsyncRelayCommand ConnectCommand => _connectCommand;
    public IAsyncRelayCommand DisconnectCommand => _disconnectCommand;
    public IAsyncRelayCommand SendCommandCommand => _sendCommandCommand;
    public IAsyncRelayCommand RetryStartupCommand => _retryStartupCommand;

    public bool HasStartupError => !string.IsNullOrWhiteSpace(StartupErrorMessage);

    public string? StartupErrorMessage
    {
        get => _startupErrorMessage;
        private set
        {
            if (SetProperty(ref _startupErrorMessage, value))
            {
                OnPropertyChanged(nameof(HasStartupError));
            }
        }
    }

    public string? StartupErrorDetails
    {
        get => _startupErrorDetails;
        private set => SetProperty(ref _startupErrorDetails, value);
    }

    // ========================================================================
    // New UI properties
    // ========================================================================

    public string HeaderAreaText
    {
        get => _headerAreaText;
        private set => SetProperty(ref _headerAreaText, value);
    }

    public int SelectedRightTab
    {
        get => _selectedRightTab;
        set => SetProperty(ref _selectedRightTab, value);
    }

    public int SelectedLogTab
    {
        get => _selectedLogTab;
        set => SetProperty(ref _selectedLogTab, value);
    }

    public string NewNoteTitle
    {
        get => _newNoteTitle;
        set => SetProperty(ref _newNoteTitle, value);
    }

    public string NewNoteContent
    {
        get => _newNoteContent;
        set => SetProperty(ref _newNoteContent, value);
    }

    /// <summary>True = the new note is shared by all profiles.</summary>
    public bool NewNoteIsGlobal
    {
        get => _newNoteIsGlobal;
        set => SetProperty(ref _newNoteIsGlobal, value);
    }

    public bool IsEditingNote => _editedNote is not null;

    /// <summary>Backs the note form Expander (two-way); editing a note opens it.</summary>
    public bool IsNoteFormExpanded
    {
        get => _isNoteFormExpanded;
        set => SetProperty(ref _isNoteFormExpanded, value);
    }

    public string NoteFormButtonText => IsEditingNote ? "Zapisz zmiany" : "Dodaj notatkę";

    public string NoteFormHeader => IsEditingNote ? "✎ Edytuj notatkę" : "＋ Nowa notatka";

    // ========================================================================
    // App settings (system-wide, not per profile)
    // ========================================================================

    public ObservableCollection<string> AvailableFonts { get; } = [];
    public IReadOnlyList<string> AvailableTelnetColorSchemes => AnsiColorPalette.Names;

    public double MinOutputFontSize => AppSettings.MinOutputFontSize;
    public double MaxOutputFontSize => AppSettings.MaxOutputFontSize;
    public double MinWidgetFontSize => AppSettings.MinWidgetFontSize;
    public double MaxWidgetFontSize => AppSettings.MaxWidgetFontSize;

    /// <summary>Font family name for MUD output in the main screen.</summary>
    public string OutputFontFamily
    {
        get => _settings.OutputFontFamily;
        set
        {
            if (string.IsNullOrWhiteSpace(value) || _settings.OutputFontFamily == value)
            {
                return;
            }

            _settings.OutputFontFamily = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(OutputFontFamilyValue));
            SaveSettings();
        }
    }

    public double OutputFontSize
    {
        get => _settings.OutputFontSize;
        set
        {
            var clamped = Math.Clamp(
                Math.Round(value), AppSettings.MinOutputFontSize, AppSettings.MaxOutputFontSize);
            if (Math.Abs(_settings.OutputFontSize - clamped) < 0.1)
            {
                return;
            }

            _settings.OutputFontSize = clamped;
            OnPropertyChanged();
            OnPropertyChanged(nameof(OutputFontSizeText));
            SaveSettings();
        }
    }

    public string OutputFontSizeText => $"{_settings.OutputFontSize:0} px";

    public FontFamily OutputFontFamilyValue => AppFonts.Resolve(_settings.OutputFontFamily);

    public bool OutputFontBold
    {
        get => _settings.OutputFontBold;
        set
        {
            if (_settings.OutputFontBold == value)
            {
                return;
            }

            _settings.OutputFontBold = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(OutputFontWeight));
            SaveSettings();
        }
    }

    public FontWeight OutputFontWeight => OutputFontBold ? FontWeight.Bold : FontWeight.Normal;

    /// <summary>Font family shared by all dockable widgets except the terminal.</summary>
    public string WidgetFontFamily
    {
        get => _settings.WidgetFontFamily;
        set
        {
            if (string.IsNullOrWhiteSpace(value) || _settings.WidgetFontFamily == value)
            {
                return;
            }

            _settings.WidgetFontFamily = value;
            ApplyWidgetFontResources();
            OnPropertyChanged();
            OnPropertyChanged(nameof(WidgetFontFamilyValue));
            SaveSettings();
        }
    }

    public double WidgetFontSize
    {
        get => _settings.WidgetFontSize;
        set
        {
            var clamped = Math.Clamp(
                Math.Round(value), AppSettings.MinWidgetFontSize, AppSettings.MaxWidgetFontSize);
            if (Math.Abs(_settings.WidgetFontSize - clamped) < 0.1)
            {
                return;
            }

            _settings.WidgetFontSize = clamped;
            ApplyWidgetFontResources();
            OnPropertyChanged();
            OnPropertyChanged(nameof(WidgetFontSizeText));
            SaveSettings();
        }
    }

    public string WidgetFontSizeText => $"{_settings.WidgetFontSize:0} px";

    public FontFamily WidgetFontFamilyValue => AppFonts.Resolve(_settings.WidgetFontFamily);

    public bool WidgetFontBold
    {
        get => _settings.WidgetFontBold;
        set
        {
            if (_settings.WidgetFontBold == value)
            {
                return;
            }

            _settings.WidgetFontBold = value;
            ApplyWidgetFontResources();
            OnPropertyChanged();
            OnPropertyChanged(nameof(WidgetFontWeight));
            SaveSettings();
        }
    }

    public FontWeight WidgetFontWeight => WidgetFontBold ? FontWeight.Bold : FontWeight.Normal;

    public bool OutputWordWrap
    {
        get => _settings.OutputWordWrap;
        set
        {
            if (_settings.OutputWordWrap == value)
            {
                return;
            }

            _settings.OutputWordWrap = value;
            OnPropertyChanged();
            SaveSettings();
        }
    }

    public string TelnetColorScheme
    {
        get => _settings.TelnetColorScheme;
        set
        {
            if (!AnsiColorPalette.IsKnown(value) || _settings.TelnetColorScheme == value)
            {
                return;
            }

            _settings.TelnetColorScheme = value;
            OnPropertyChanged();
            SaveSettings();
        }
    }

    /// <summary>
    /// Separator character for command stacking (e.g. ";").  Commands typed
    /// by the user, alias replacements, trigger actions, and timer commands
    /// are split on newlines and on this separator.  Empty disables stacking
    /// (only newlines remain).
    /// </summary>
    public string CommandStackingSeparator
    {
        get => _settings.CommandStackingSeparator;
        set
        {
            var trimmed = value?.Trim() ?? string.Empty;
            if (_settings.CommandStackingSeparator == trimmed)
            {
                return;
            }

            _settings.CommandStackingSeparator = trimmed;
            OnPropertyChanged();
            SaveSettings();

            // Re-sync all running timers so their callback closures pick up the new
            // separator; timer command splitting depends on the current separator.
            SyncAllTimers();
        }
    }

    public bool AutoAssistEnabled
    {
        get => _settings.AutoAssistEnabled;
        set
        {
            if (_settings.AutoAssistEnabled == value)
            {
                return;
            }

            _settings.AutoAssistEnabled = value;
            OnPropertyChanged();
            SaveSettings();
            TryAutoAssist();
        }
    }

    public bool GroupOrdersEnabled
    {
        get => _settings.GroupOrdersEnabled;
        set
        {
            if (_settings.GroupOrdersEnabled == value)
            {
                return;
            }

            _settings.GroupOrdersEnabled = value;
            OnPropertyChanged();
            SaveSettings();
        }
    }

    public RelayCommand ResetOutputFontCommand => new(() =>
    {
        OutputFontFamily = AppSettings.DefaultOutputFontFamily;
        OutputFontSize = AppSettings.DefaultOutputFontSize;
        OutputFontBold = false;
    });

    public RelayCommand ResetWidgetFontCommand => new(() =>
    {
        WidgetFontFamily = AppSettings.DefaultWidgetFontFamily;
        WidgetFontSize = AppSettings.DefaultWidgetFontSize;
        WidgetFontBold = false;
    });

    private void ApplyWidgetFontResources()
    {
        if (Avalonia.Application.Current is not { } application)
        {
            return;
        }

        application.Resources["WidgetFontFamilyResource"] = WidgetFontFamilyValue;
        application.Resources["WidgetFontSizeResource"] = _settings.WidgetFontSize;
        application.Resources["WidgetFontWeightResource"] = WidgetFontWeight;
    }

    private void SaveSettings()
    {
        if (!_settingsLoaded)
        {
            return;
        }

        try
        {
            _settingsService.Save(_settings);
        }
        catch (Exception exception)
        {
            AddToast($"Nie udało się zapisać ustawień: {exception.Message}", "error");
        }
    }

    private void PopulateAvailableFonts()
    {
        var fonts = new List<string>();
        try
        {
            fonts = Avalonia.Media.FontManager.Current.SystemFonts
                .Select(f => f.Name)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Distinct()
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (Exception)
        {
            // Headless environment (e.g. unit tests) — fall back to a curated list.
        }

        if (fonts.Count == 0)
        {
            fonts =
            [
                "Cascadia Mono", "Consolas", "Courier New", "Fira Code",
                "JetBrains Mono", "Lucida Console", "Segoe UI", "Verdana",
            ];
        }

        if (!fonts.Contains(_settings.OutputFontFamily))
        {
            fonts.Insert(0, _settings.OutputFontFamily);
        }

        if (!fonts.Contains(_settings.WidgetFontFamily))
        {
            fonts.Insert(0, _settings.WidgetFontFamily);
        }

        if (!fonts.Contains(AppFonts.OpenDyslexicName, StringComparer.OrdinalIgnoreCase))
        {
            fonts.Add(AppFonts.OpenDyslexicName);
            fonts.Sort(StringComparer.OrdinalIgnoreCase);
        }

        foreach (var font in fonts)
        {
            AvailableFonts.Add(font);
        }
    }

    // ========================================================================
    // Aliases & triggers (regex-based, saved per profile)
    // ========================================================================

    public IReadOnlyList<string> RuleTypes { get; } = ["alias", "trigger"];

    public RelayCommand AddRuleCommand { get; }
    public RelayCommand<AutomationRuleEntry> DeleteRuleCommand { get; }
    public RelayCommand<AutomationRuleEntry> ToggleRuleCommand { get; }
    public RelayCommand<AutomationRuleEntry> EditRuleCommand { get; }
    public RelayCommand CancelRuleEditCommand { get; }

    public bool IsEditingRule => _editedRule is not null;

    /// <summary>Backs the rule form Expander (two-way); editing a rule opens it.</summary>
    public bool IsRuleFormExpanded
    {
        get => _isRuleFormExpanded;
        set => SetProperty(ref _isRuleFormExpanded, value);
    }

    public string RuleFormButtonText => IsEditingRule ? "Zapisz zmiany" : "Dodaj regułę";

    public string RuleFormHeader => IsEditingRule ? "✎ Edytuj alias / trigger" : "＋ Nowy alias / trigger";

    public string NewRuleName
    {
        get => _newRuleName;
        set
        {
            if (SetProperty(ref _newRuleName, value))
            {
                AddRuleCommand.NotifyCanExecuteChanged();
            }
        }
    }

    /// <summary>"alias" or "trigger".</summary>
    public string NewRuleType
    {
        get => _newRuleType;
        set
        {
            if (SetProperty(ref _newRuleType, value))
            {
                OnPropertyChanged(nameof(NewRuleIsAlias));
            }
        }
    }

    public bool NewRuleIsAlias => NewRuleType == "alias";

    /// <summary>.NET regex tested against typed commands (alias) or received lines (trigger).</summary>
    public string NewRulePattern
    {
        get => _newRulePattern;
        set
        {
            if (SetProperty(ref _newRulePattern, value))
            {
                NewRulePatternError = ValidatePattern(value);
                AddRuleCommand.NotifyCanExecuteChanged();
            }
        }
    }

    /// <summary>Command to send; may use capture groups like $1.</summary>
    public string NewRuleAction
    {
        get => _newRuleAction;
        set
        {
            if (SetProperty(ref _newRuleAction, value))
            {
                AddRuleCommand.NotifyCanExecuteChanged();
            }
        }
    }

    /// <summary>True = the new/edited rule is shared by all profiles.</summary>
    public bool NewRuleIsGlobal
    {
        get => _newRuleIsGlobal;
        set => SetProperty(ref _newRuleIsGlobal, value);
    }

    /// <summary>Live regex validation message, or null when the pattern is valid.</summary>
    public string? NewRulePatternError
    {
        get => _newRulePatternError;
        private set
        {
            if (SetProperty(ref _newRulePatternError, value))
            {
                OnPropertyChanged(nameof(HasNewRulePatternError));
            }
        }
    }

    public bool HasNewRulePatternError => NewRulePatternError is not null;

    private static string? ValidatePattern(string pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern))
        {
            return null;
        }

        try
        {
            _ = new Regex(pattern);
            return null;
        }
        catch (ArgumentException exception)
        {
            return $"Nieprawidłowy regex: {exception.Message}";
        }
    }

    private bool CanAddRule() =>
        !string.IsNullOrWhiteSpace(NewRuleName) &&
        !string.IsNullOrWhiteSpace(NewRulePattern) &&
        !string.IsNullOrWhiteSpace(NewRuleAction) &&
        ValidatePattern(NewRulePattern) is null;

    private void AddRule()
    {
        if (!CanAddRule())
        {
            return;
        }

        if (_editedRule is { } edited)
        {
            edited.Name = NewRuleName.Trim();
            edited.Type = NewRuleType;
            edited.Pattern = NewRulePattern;
            edited.Action = NewRuleAction;
            edited.IsGlobal = NewRuleIsGlobal;
        }
        else
        {
            AutomationRules.Add(new AutomationRuleEntry(
                NewRuleName.Trim(), NewRuleType, NewRulePattern, NewRuleAction,
                isEnabled: true, isGlobal: NewRuleIsGlobal));
        }

        ClearRuleForm();
        ApplyAutomation();
        SaveActiveProfile();
    }

    private void EditRule(AutomationRuleEntry? entry)
    {
        if (entry is null)
        {
            return;
        }

        _editedRule = entry;
        NewRuleName = entry.Name;
        NewRuleType = entry.Type;
        NewRulePattern = entry.Pattern;
        NewRuleAction = entry.Action;
        NewRuleIsGlobal = entry.IsGlobal;
        IsRuleFormExpanded = true;
        NotifyRuleEditModeChanged();
    }

    private void CancelRuleEdit() => ClearRuleForm();

    private void ClearRuleForm()
    {
        _editedRule = null;
        NewRuleName = string.Empty;
        NewRulePattern = string.Empty;
        NewRuleAction = string.Empty;
        NewRuleIsGlobal = false;
        NotifyRuleEditModeChanged();
    }

    private void NotifyRuleEditModeChanged()
    {
        OnPropertyChanged(nameof(IsEditingRule));
        OnPropertyChanged(nameof(RuleFormButtonText));
        OnPropertyChanged(nameof(RuleFormHeader));
    }

    private void DeleteRule(AutomationRuleEntry? entry)
    {
        if (entry is null)
        {
            return;
        }

        if (ReferenceEquals(entry, _editedRule))
        {
            ClearRuleForm();
        }

        AutomationRules.Remove(entry);
        ApplyAutomation();
        SaveActiveProfile();
    }

    private void ToggleRule(AutomationRuleEntry? entry)
    {
        if (entry is null)
        {
            return;
        }

        entry.IsEnabled = !entry.IsEnabled;
        ApplyAutomation();
        SaveActiveProfile();
    }

    // ========================================================================
    // Timers (per-character, repeating until disabled)
    // ========================================================================

    public ObservableCollection<TimerEntry> Timers { get; } = [];

    public RelayCommand AddTimerCommand { get; }
    public RelayCommand<TimerEntry> DeleteTimerCommand { get; }
    public RelayCommand<TimerEntry> ToggleTimerCommand { get; }
    public RelayCommand<TimerEntry> EditTimerCommand { get; }
    public RelayCommand CancelTimerEditCommand { get; }

    public bool IsEditingTimer => _editedTimer is not null;

    /// <summary>Backs the timer form Expander (two-way); editing a timer opens it.</summary>
    public bool IsTimerFormExpanded
    {
        get => _isTimerFormExpanded;
        set => SetProperty(ref _isTimerFormExpanded, value);
    }

    public string TimerFormButtonText => IsEditingTimer ? "Zapisz zmiany" : "Dodaj timer";

    public string TimerFormHeader => IsEditingTimer ? "✎ Edytuj timer" : "＋ Nowy timer";

    public string NewTimerName
    {
        get => _newTimerName;
        set
        {
            if (SetProperty(ref _newTimerName, value))
            {
                AddTimerCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string NewTimerMinutes
    {
        get => _newTimerMinutes;
        set => SetProperty(ref _newTimerMinutes, value);
    }

    public string NewTimerSeconds
    {
        get => _newTimerSeconds;
        set => SetProperty(ref _newTimerSeconds, value);
    }

    public string NewTimerMilliseconds
    {
        get => _newTimerMilliseconds;
        set => SetProperty(ref _newTimerMilliseconds, value);
    }

    /// <summary>One command per line; sent in order on every tick.</summary>
    public string NewTimerCommands
    {
        get => _newTimerCommands;
        set => SetProperty(ref _newTimerCommands, value);
    }

    /// <summary>True = the new/edited timer is shared by all profiles.</summary>
    public bool NewTimerIsGlobal
    {
        get => _newTimerIsGlobal;
        set => SetProperty(ref _newTimerIsGlobal, value);
    }

    private void AddTimer()
    {
        var name = NewTimerName.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        var minutes = ParseNonNegative(NewTimerMinutes);
        var seconds = ParseNonNegative(NewTimerSeconds);
        var milliseconds = ParseNonNegative(NewTimerMilliseconds);
        var interval = TimeSpan.FromMinutes(minutes) +
                       TimeSpan.FromSeconds(seconds) +
                       TimeSpan.FromMilliseconds(milliseconds);

        if (interval <= TimeSpan.Zero)
        {
            AddToast("Interwał timera musi być większy od zera.", "error");
            return;
        }

        var hasCommands = CommandStacker.Split(NewTimerCommands, CommandStackingSeparator).Count > 0;
        if (!hasCommands)
        {
            AddToast("Timer musi mieć przynajmniej jedną komendę.", "error");
            return;
        }

        if (_editedTimer is { } edited)
        {
            edited.Name = name;
            edited.Minutes = minutes;
            edited.Seconds = seconds;
            edited.Milliseconds = milliseconds;
            edited.CommandsText = NewTimerCommands;
            edited.IsGlobal = NewTimerIsGlobal;
            SyncTimer(edited);
        }
        else
        {
            Timers.Add(new TimerEntry
            {
                Name = name,
                Minutes = minutes,
                Seconds = seconds,
                Milliseconds = milliseconds,
                CommandsText = NewTimerCommands,
                IsGlobal = NewTimerIsGlobal,
            });
        }

        ClearTimerForm();
        SaveActiveProfile();
    }

    private void EditTimer(TimerEntry? entry)
    {
        if (entry is null)
        {
            return;
        }

        _editedTimer = entry;
        NewTimerName = entry.Name;
        NewTimerMinutes = entry.Minutes.ToString();
        NewTimerSeconds = entry.Seconds.ToString();
        NewTimerMilliseconds = entry.Milliseconds.ToString();
        NewTimerCommands = entry.CommandsText;
        NewTimerIsGlobal = entry.IsGlobal;
        IsTimerFormExpanded = true;
        NotifyTimerEditModeChanged();
    }

    private void CancelTimerEdit() => ClearTimerForm();

    private void ClearTimerForm()
    {
        _editedTimer = null;
        NewTimerName = string.Empty;
        NewTimerMinutes = "0";
        NewTimerSeconds = "0";
        NewTimerMilliseconds = "0";
        NewTimerCommands = string.Empty;
        NewTimerIsGlobal = false;
        NotifyTimerEditModeChanged();
    }

    private void NotifyTimerEditModeChanged()
    {
        OnPropertyChanged(nameof(IsEditingTimer));
        OnPropertyChanged(nameof(TimerFormButtonText));
        OnPropertyChanged(nameof(TimerFormHeader));
    }

    private void DeleteTimer(TimerEntry? entry)
    {
        if (entry is null)
        {
            return;
        }

        if (ReferenceEquals(entry, _editedTimer))
        {
            ClearTimerForm();
        }

        StopTimer(entry);
        Timers.Remove(entry);
        SaveActiveProfile();
    }

    private void ToggleTimer(TimerEntry? entry)
    {
        if (entry is null)
        {
            return;
        }

        entry.IsEnabled = !entry.IsEnabled;
        SyncTimer(entry);
        SaveActiveProfile();

        AddToast(entry.IsEnabled
            ? $"Timer „{entry.Name}” włączony (co {entry.IntervalText})."
            : $"Timer „{entry.Name}” wyłączony.", "info");
    }

    private static string TimerKey(TimerEntry entry) => $"user-timer:{entry.Id}";

    /// <summary>Starts or stops the underlying periodic timer to match IsEnabled.</summary>
    private void SyncTimer(TimerEntry entry)
    {
        if (!entry.IsEnabled)
        {
            StopTimer(entry);
            return;
        }

        var interval = entry.Interval;
        if (interval <= TimeSpan.Zero)
        {
            entry.IsEnabled = false;
            AddToast($"Timer „{entry.Name}” ma nieprawidłowy interwał.", "error");
            return;
        }

        var commands = entry.GetCommands(CommandStackingSeparator);
        _timers.StartPeriodic(TimerKey(entry), interval, async token =>
        {
            if (!IsConnected)
            {
                return;
            }

            foreach (var command in commands)
            {
                token.ThrowIfCancellationRequested();
                await _session.SendCommandAsync(command);
            }
        });
    }

    private void StopTimer(TimerEntry entry) => _timers.Cancel(TimerKey(entry));

    private void SyncAllTimers()
    {
        foreach (var entry in Timers)
        {
            SyncTimer(entry);
        }
    }

    private static int ParseNonNegative(string text) =>
        int.TryParse(text?.Trim(), out var value) && value > 0 ? value : 0;

    // ========================================================================
    // Autowalk (named locations + pathfinding over the world map)
    // ========================================================================

    public ObservableCollection<AutowalkLocation> Locations { get; } = [];

    public RelayCommand AddCurrentLocationCommand { get; }
    public RelayCommand AddLocationCommand { get; }
    public RelayCommand<AutowalkLocation> DeleteLocationCommand { get; }
    public RelayCommand<AutowalkLocation> GoToLocationCommand { get; }
    public RelayCommand StopAutowalkCommand { get; }

    public string NewLocationName
    {
        get => _newLocationName;
        set => SetProperty(ref _newLocationName, value);
    }

    /// <summary>Room vnum typed by the user when defining a remote location.</summary>
    public string NewLocationVnum
    {
        get => _newLocationVnum;
        set => SetProperty(ref _newLocationVnum, value);
    }

    /// <summary>True = the new location is shared by all profiles.</summary>
    public bool NewLocationIsGlobal
    {
        get => _newLocationIsGlobal;
        set => SetProperty(ref _newLocationIsGlobal, value);
    }

    public bool IsAutowalking => _autowalkPath is not null;

    public RelayCommand GoToTemporaryTargetCommand { get; }
    public RelayCommand GoToSelectedTargetCommand { get; }

    /// <summary>Target picked by double-clicking the map; not saved to the profile.</summary>
    public bool HasTemporaryTarget => _temporaryTarget is not null;

    public string TemporaryTargetDisplay => _temporaryTarget is { } target
        ? $"Cel z mapy: {target.Name} (vnum {target.Vnum})"
        : string.Empty;

    private void SetTemporaryTarget(AutowalkLocation? target)
    {
        _temporaryTarget = target;
        OnPropertyChanged(nameof(HasTemporaryTarget));
        OnPropertyChanged(nameof(TemporaryTargetDisplay));
    }

    private void OnMapRoomDoubleClicked(MapRoom room)
    {
        var vnum = room.Vnum;
        if (string.IsNullOrWhiteSpace(vnum))
        {
            AddToast("Ten pokój nie ma vnum — nie można do niego nawigować.", "error");
            return;
        }

        SetTemporaryTarget(new AutowalkLocation(
            string.IsNullOrWhiteSpace(room.Name) ? $"pokój {vnum}" : room.Name!, vnum, room.Name));

        if (IsAutowalking)
        {
            // Stop the active walk so the user can preview the new route,
            // but keep the fresh temporary target (do NOT call StopAutowalk
            // here — it would also clear _temporaryTarget).
            _autowalkCts.Cancel();
            _autowalkPath = null;
            _autowalkStep = 0;
            _autowalkTargetName = null;
            ResetAutowalkTransientState();
            OnPropertyChanged(nameof(IsAutowalking));
            Map.RouteRooms = null;
            AddToast($"Autowalk przerwany — nowy cel „{_temporaryTarget!.Name}”.", "info");
            // Fall through to preview the new route below.
        }

        // Preview the route without walking.
        var currentVnum = Map.CurrentVnum;
        var path = string.IsNullOrWhiteSpace(currentVnum)
            ? null
            : GetPathfinder()?.FindPathByVnum(currentVnum, vnum);

        if (path is null)
        {
            Map.RouteRooms = null;
            AutowalkStatusText = $"Cel: „{_temporaryTarget!.Name}” — brak podglądu trasy (nieznana pozycja lub brak drogi).";
            return;
        }

        PaintRoute(path, 0);
        AutowalkStatusText = $"Cel: „{_temporaryTarget!.Name}” — {path.Steps.Count} kroków. Wpisz /idz albo kliknij IDŹ DO CELU.";
    }

    /// <summary>
    /// Paints the remaining part of a path on the map, starting at the room
    /// the walker currently occupies (fromStep = next step to execute).
    /// </summary>
    private void PaintRoute(MapPath path, int fromStep)
    {
        var rooms = new List<MapRoom>(path.Steps.Count - fromStep + 1)
        {
            fromStep == 0 ? path.From : path.Steps[fromStep - 1].ToRoom,
        };

        for (var i = fromStep; i < path.Steps.Count; i++)
        {
            rooms.Add(path.Steps[i].ToRoom);
        }

        Map.RouteRooms = rooms;
    }

    public string AutowalkStatusText
    {
        get => _autowalkStatusText;
        private set => SetProperty(ref _autowalkStatusText, value);
    }

    /// <summary>
    /// Returns the pathfinder for the currently loaded map, building it once
    /// per MapIndex instance (the CSR graph build is the expensive part).
    /// </summary>
    private MapPathfinder? GetPathfinder()
    {
        var index = Map.MapIndex;
        if (index is null)
        {
            return null;
        }

        if (!ReferenceEquals(index, _pathfinderIndex))
        {
            _pathfinder = new MapPathfinder(index);
            _pathfinderIndex = index;
        }

        return _pathfinder;
    }

    private void AddCurrentLocation()
    {
        var vnum = Map.CurrentVnum;
        if (string.IsNullOrWhiteSpace(vnum))
        {
            AddToast("Nieznana obecna pozycja — brak danych GMCP.", "error");
            return;
        }

        AddLocationCore(NewLocationName, vnum);
    }

    private void AddLocation()
    {
        AddLocationCore(NewLocationName, NewLocationVnum);
    }

    private void AddLocationCore(string rawName, string rawVnum)
    {
        var name = rawName.Trim();
        var vnum = rawVnum.Trim();

        if (name.Length == 0)
        {
            AddToast("Podaj nazwę lokacji.", "error");
            return;
        }

        if (vnum.Length == 0)
        {
            AddToast("Podaj numer pomieszczenia (vnum).", "error");
            return;
        }

        if (Locations.Any(l => string.Equals(l.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            AddToast($"Lokacja „{name}” już istnieje.", "error");
            return;
        }

        var room = Map.MapIndex?.FindFirstRoomByVnum(vnum);
        if (Map.MapIndex is not null && room is null)
        {
            AddToast($"Uwaga: vnum {vnum} nie istnieje w mapie.", "error");
        }

        Locations.Add(new AutowalkLocation(name, vnum, room?.Name, NewLocationIsGlobal));
        NewLocationName = string.Empty;
        NewLocationVnum = string.Empty;
        NewLocationIsGlobal = false;
        SaveActiveProfile();
        AddToast($"Dodano lokację „{name}”.", "info");
    }

    private void DeleteLocation(AutowalkLocation? entry)
    {
        if (entry is null)
        {
            return;
        }

        Locations.Remove(entry);
        SaveActiveProfile();
    }

    private void StartAutowalk(AutowalkLocation entry)
    {
        var pathfinder = GetPathfinder();
        if (pathfinder is null)
        {
            AddToast("Mapa nie jest załadowana.", "error");
            return;
        }

        var currentVnum = Map.CurrentVnum;
        if (string.IsNullOrWhiteSpace(currentVnum))
        {
            AddToast("Nieznana obecna pozycja — brak danych GMCP.", "error");
            return;
        }

        var path = pathfinder.FindPathByVnum(currentVnum, entry.Vnum);
        if (path is null)
        {
            AddToast($"Nie znaleziono trasy do „{entry.Name}”.", "error");
            return;
        }

        if (path.Steps.Count == 0)
        {
            AddToast($"Już jesteś w lokacji „{entry.Name}”.", "info");
            return;
        }

        Map.CenterOnPlayer();
        ReplaceAutowalkCancellation();
        ResetAutowalkTransientState();
        _autowalkPath = path;
        _autowalkStep = 0;
        _autowalkRecomputes = 0;
        _autowalkTargetName = entry.Name;
        OnPropertyChanged(nameof(IsAutowalking));
        AutowalkStatusText = $"Idę do „{entry.Name}” — {path.Steps.Count} kroków.";
        PaintRoute(path, 0);
        _ = SendTriggeredCommandAsync("stand");
        SendAutowalkStep();
    }

    private void StopAutowalk(string message, string toastType = "info")
    {
        var wasWalking = _autowalkPath is not null;
        _autowalkCts.Cancel();
        _autowalkPath = null;
        _autowalkStep = 0;
        _autowalkTargetName = null;
        ResetAutowalkTransientState();
        OnPropertyChanged(nameof(IsAutowalking));
        AutowalkStatusText = "Bezczynny.";
        Map.RouteRooms = null;
        SetTemporaryTarget(null);

        if (wasWalking)
        {
            AddToast(message, toastType);
        }
    }

    private void ReplaceAutowalkCancellation()
    {
        var previous = _autowalkCts;
        _autowalkCts = new CancellationTokenSource();
        previous.Cancel();
        previous.Dispose();
    }

    private void ResetAutowalkTransientState()
    {
        _autowalkRecoveringMovement = false;
        _autowalkOpeningStep = null;
        _autowalkWaitingForGate = false;
        _autowalkGateCommandsSent = false;
        _autowalkGateIsOpen = false;
    }

    private void SendAutowalkStep(bool skipMovementCheck = false)
    {
        if (_autowalkPath is null || _autowalkStep >= _autowalkPath.Steps.Count)
        {
            return;
        }

        if (_autowalkWaitingForGate || _autowalkRecoveringMovement)
        {
            return;
        }

        if (!skipMovementCheck)
        {
            var action = AutowalkRecoveryPolicy.GetLowMovementAction(
                _latestMovement, _latestMaximumMovement, _latestMemorizedSpells);
            if (action != LowMovementAction.None)
            {
                _autowalkRecoveringMovement = true;
                _ = RecoverMovementAndContinueAsync(action, _autowalkCts.Token);
                return;
            }
        }

        var step = _autowalkPath.Steps[_autowalkStep];
        var remaining = _autowalkPath.Steps.Count - _autowalkStep;
        AutowalkStatusText = $"Idę do „{_autowalkTargetName}” — pozostało {remaining} kroków.";

        // A named exit (GMCP "name" or a custom exit name in the map) must be
        // entered by its name — the plain direction command does not work.
        var exit = FindGmcpExit(step.Command);
        var moveCommand = RemoveDiacritics(exit?.Name) ?? step.Command;
        if (!string.Equals(moveCommand, step.Command, StringComparison.OrdinalIgnoreCase))
        {
            EmitSystem($"Autowalk: krok „{step.Command}” wysyłam jako „{moveCommand}”.", 90);
        }

        var openCommand = TryGetOpenCommand(exit);
        _autowalkOpeningStep = openCommand is null ? null : _autowalkStep;
        _ = SendAutowalkCommandsAsync(openCommand, moveCommand, _autowalkCts.Token);
    }

    private async Task RecoverMovementAndContinueAsync(
        LowMovementAction action,
        CancellationToken cancellationToken)
    {
        try
        {
            if (action == LowMovementAction.CastRefresh)
            {
                Dispatcher.UIThread.Post(() =>
                    AutowalkStatusText = "Mało ruchu — rzucam refresh.");
                await SendTriggeredCommandAsync("cast 'refresh' self", cancellationToken);
            }
            else
            {
                Dispatcher.UIThread.Post(() =>
                    AutowalkStatusText = "Mało ruchu — odpoczywam 30 sekund.");
                await SendTriggeredCommandAsync("rest", cancellationToken);
                await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);

                // The character is still resting — stand up before walking on.
                await SendTriggeredCommandAsync("stand", cancellationToken);

                if (AutowalkRecoveryPolicy.HasMemorizedSpell(_latestMemorizedSpells, "float"))
                {
                    await SendTriggeredCommandAsync("cast 'float' self", cancellationToken);
                }
            }

            Dispatcher.UIThread.Post(() =>
            {
                if (cancellationToken.IsCancellationRequested || _autowalkPath is null)
                {
                    return;
                }

                _autowalkRecoveringMovement = false;
                SendAutowalkStep(skipMovementCheck: true);
            });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Stopping autowalk also stops its pending recovery delay and sends.
        }
    }

    private async Task SendAutowalkCommandsAsync(
        string? openCommand,
        string moveCommand,
        CancellationToken cancellationToken)
    {
        try
        {
            if (openCommand is not null)
            {
                await SendTriggeredCommandAsync(openCommand, cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();
            await SendTriggeredCommandAsync(moveCommand, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The user stopped or replaced this autowalk.
        }
    }

    /// <summary>
    /// When GMCP Room.Info reports the step's exit as a closed door, returns
    /// the command that opens it: "open" + the exit name from GMCP, or the
    /// direction when the exit has no name. (The map's "door" field holds the
    /// door state, e.g. "closed" — never a usable name.)
    /// </summary>
    private static string? TryGetOpenCommand(RoomExitInfo? exit)
    {
        if (exit is null || !exit.HasDoor || !exit.IsClosed)
        {
            return null;
        }

        return $"open {RemoveDiacritics(exit.Name) ?? exit.Dir}";
    }

    /// <summary>
    /// Matches a map exit command against the current room's GMCP exits,
    /// either by canonical direction (map "west" ↔ GMCP "W") or, for
    /// custom-named exits, by the exit name itself.
    /// </summary>
    private RoomExitInfo? FindGmcpExit(string stepCommand)
        => FindGmcpExit(stepCommand, _roomExits.CurrentExits);

    private static RoomExitInfo? FindGmcpExit(
        string stepCommand,
        IReadOnlyList<RoomExitInfo> exits)
    {
        var canonical = CanonicalDirection(stepCommand);

        foreach (var exit in exits)
        {
            if (string.Equals(CanonicalDirection(exit.Dir), canonical, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(exit.Name, stepCommand, StringComparison.OrdinalIgnoreCase))
            {
                return exit;
            }
        }

        return null;
    }

    private void OnRoomExitsChanged(IReadOnlyList<RoomExitInfo> exits)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (!_autowalkWaitingForGate || _autowalkPath is null ||
                _autowalkStep >= _autowalkPath.Steps.Count)
            {
                return;
            }

            var exit = FindGmcpExit(_autowalkPath.Steps[_autowalkStep].Command, exits);
            if (exit is null || exit.IsClosed)
            {
                return;
            }

            _autowalkGateIsOpen = true;
            TryContinueThroughOpenedGate();
        });
    }

    private void TryContinueThroughOpenedGate()
    {
        if (!_autowalkWaitingForGate || !_autowalkGateCommandsSent || !_autowalkGateIsOpen)
        {
            return;
        }

        _autowalkWaitingForGate = false;
        _autowalkOpeningStep = null;
        EmitSystem("Autowalk: przejście otwarte w GMCP — idę dalej.", 90);
        SendAutowalkStep();
    }

    /// <summary>Strips diacritics so autowalk commands are plain ASCII (e.g. "wyjście" → "wyjscie").</summary>
    private static string? RemoveDiacritics(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        var normalized = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(text.Length);
        foreach (var ch in normalized)
        {
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch) != System.Globalization.UnicodeCategory.NonSpacingMark)
                sb.Append(ch);
        }
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    /// <summary>Maps full direction names to the short form used by GMCP dirs.</summary>
    private static string CanonicalDirection(string direction) => direction.ToLowerInvariant() switch
    {
        "north" => "N",
        "south" => "S",
        "east" => "E",
        "west" => "W",
        "northeast" => "NE",
        "northwest" => "NW",
        "southeast" => "SE",
        "southwest" => "SW",
        "up" => "U",
        "down" => "D",
        _ => direction.ToUpperInvariant(),
    };

    /// <summary>
    /// Advances the walk when GMCP confirms a room change: if the new room is
    /// one of the upcoming path steps we move past it, otherwise the route is
    /// recomputed from the new position (e.g. after a failed or extra move).
    /// </summary>
    private void OnAutowalkLocationChanged(string vnum)
    {
        TryAutoAssist();

        Dispatcher.UIThread.Post(() =>
        {
            if (_autowalkPath is null)
            {
                return;
            }

            _autowalkOpeningStep = null;
            _autowalkWaitingForGate = false;
            _autowalkGateCommandsSent = false;
            _autowalkGateIsOpen = false;

            var steps = _autowalkPath.Steps;
            for (var i = _autowalkStep; i < steps.Count; i++)
            {
                if (string.Equals(steps[i].ToRoom.Vnum, vnum, StringComparison.Ordinal))
                {
                    _autowalkRecomputes = 0;
                    _autowalkStep = i + 1;
                    if (_autowalkStep >= steps.Count)
                    {
                        _ = SendTriggeredCommandAsync("rest");
                        StopAutowalk($"Dotarłeś do lokacji „{_autowalkTargetName}”.");
                    }
                    else
                    {
                        PaintRoute(_autowalkPath, _autowalkStep);
                        SendAutowalkStep();
                    }

                    return;
                }
            }

            // Off the planned route — recompute from where we actually are.
            // A recompute is expected occasionally (a failed or extra move), but a
            // recompute on every step means the map disagrees with the server
            // (e.g. duplicate vnums or a misdirected named exit) — without this
            // guard the walk degenerates into an endless move/BFS loop that
            // floods the server with commands and starves the UI thread.
            var targetName = _autowalkTargetName;
            _autowalkRecomputes++;
            EmitSystem(
                $"Autowalk: pokój {vnum} poza trasą — przeliczam trasę ({_autowalkRecomputes}/5).", 33);
            if (_autowalkRecomputes >= 5)
            {
                StopAutowalk(
                    $"Autowalk przerwany: trasa do „{targetName}” schodzi z kursu przy każdym kroku (mapa niezgodna z serwerem?).",
                    "error");
                return;
            }

            var path = GetPathfinder()?.FindPathByVnum(vnum, _autowalkPath.To.Vnum ?? string.Empty);
            if (path is null)
            {
                StopAutowalk($"Zgubiłem trasę do „{targetName}” — autowalk przerwany.", "error");
                return;
            }

            if (path.Steps.Count == 0)
            {
                _ = SendTriggeredCommandAsync("rest");
                StopAutowalk($"Dotarłeś do lokacji „{targetName}”.");
                return;
            }

            _autowalkPath = path;
            _autowalkStep = 0;
            PaintRoute(path, 0);
            SendAutowalkStep();
        });
    }

    /// <summary>
    /// Executes the bare /idz action: walks to the temporary map-picked target
    /// or shows usage help when no target has been picked.
    /// </summary>
    private void HandleGoToSelectedTarget()
    {
        if (_temporaryTarget is { } target)
        {
            StartAutowalk(target);
        }
        else
        {
            AddToast("Użycie: /idz <nazwa lokacji> — albo zaznacz cel podwójnym kliknięciem na mapie i wpisz samo /idz.", "info");
        }
    }

    /// <summary>Handles chat-bar commands: /idz &lt;nazwa&gt; and /stop. Returns true when consumed.</summary>
    private bool TryHandleAutowalkCommand(string command)
    {
        if (string.Equals(command, "/stop", StringComparison.OrdinalIgnoreCase))
        {
            StopAutowalk("Autowalk zatrzymany.");
            return true;
        }

        const string prefix = "/idz";
        if (!command.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var argument = command.Length > prefix.Length ? command[prefix.Length..].Trim() : string.Empty;
        if (argument.Length == 0)
        {
            HandleGoToSelectedTarget();
            return true;
        }

        var entry = Locations.FirstOrDefault(
            l => string.Equals(l.Name, argument, StringComparison.OrdinalIgnoreCase));
        if (entry is null)
        {
            AddToast($"Nie znam lokacji „{argument}”.", "error");
            return true;
        }

        StartAutowalk(entry);
        return true;
    }

    // ========================================================================
    // Death marks (last 10 death locations, hard-coded server-line trigger)
    // ========================================================================

    private const int MaxDeathMarks = 10;

    // The server announces death with this exact line; depending on the
    // negotiated charset it arrives with or without Polish diacritics.
    // This trigger is intentionally hard-coded, not a user automation rule.
    private static readonly string[] DeathPhrases =
    [
        "Nie żyjesz, co za pech!!!",
        "Nie zyjesz, co za pech!!!",
    ];

    /// <summary>Last death locations, newest first. Persisted per profile.</summary>
    public ObservableCollection<DeathMarkEntry> Deaths { get; } = [];

    public RelayCommand<DeathMarkEntry> DeleteDeathCommand { get; }
    public RelayCommand<DeathMarkEntry> GoToDeathCommand { get; }

    private static bool IsDeathLine(string line)
    {
        foreach (var phrase in DeathPhrases)
        {
            if (line.Contains(phrase, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Records the current GMCP position as a death mark. Runs on the UI
    /// thread (posted from the network receive loop).
    /// </summary>
    private void RecordDeath()
    {
        var vnum = Map.CurrentVnum;
        if (string.IsNullOrWhiteSpace(vnum))
        {
            AddToast("Zginąłeś, ale pozycja jest nieznana (brak danych GMCP) — miejsce śmierci nie zostało zapisane.", "error");
            return;
        }

        var roomName = Map.MapIndex?.FindFirstRoomByVnum(vnum)?.Name;
        var entry = new DeathMarkEntry(vnum, roomName, DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
        Deaths.Insert(0, entry);
        while (Deaths.Count > MaxDeathMarks)
        {
            Deaths.RemoveAt(Deaths.Count - 1);
        }

        SaveActiveProfile();
        AddToast($"Zapisano miejsce śmierci: {entry.Display}.", "error");
    }

    private void DeleteDeath(DeathMarkEntry? entry)
    {
        if (entry is null)
        {
            return;
        }

        Deaths.Remove(entry);
        SaveActiveProfile();
    }

    private void GoToDeath(DeathMarkEntry? entry)
    {
        if (entry is null)
        {
            return;
        }

        StartAutowalk(new AutowalkLocation(
            string.IsNullOrWhiteSpace(entry.RoomName) ? $"miejsce śmierci (vnum {entry.Vnum})" : entry.RoomName!,
            entry.Vnum,
            entry.RoomName));
    }

    // ========================================================================
    // Required buffs (user-defined, matched against Char.Affects)
    // ========================================================================

    /// <summary>Buffs the user wants to keep active. Persisted per profile.</summary>
    public ObservableCollection<BuffWatchEntry> RequiredBuffs { get; } = [];

    public RelayCommand AddBuffCommand { get; }
    public RelayCommand<BuffWatchEntry> DeleteBuffCommand { get; }
    public AsyncRelayCommand RecastBuffsCommand { get; }

    /// <summary>Header badge for the buffs section, e.g. "2/3" (active/required).</summary>
    public string BuffsBadge => RequiredBuffs.Count == 0
        ? "0"
        : $"{RequiredBuffs.Count(b => b.IsActive)}/{RequiredBuffs.Count}";

    /// <summary>True when at least one required buff is missing.</summary>
    public bool BuffsAlert => RequiredBuffs.Any(b => !b.IsActive);

    private void RefreshBuffIndicators()
    {
        OnPropertyChanged(nameof(BuffsBadge));
        OnPropertyChanged(nameof(BuffsAlert));
        UpdateBuffsToolTitle();
    }

    /// <summary>
    /// Mirrors the buff state onto the dock tab title ("🛡 Buffy 2/3"), so the
    /// missing-buff signal is visible even when another tab covers the panel.
    /// </summary>
    private void UpdateBuffsToolTitle()
    {
        var tool = _dockFactory.AllTools.FirstOrDefault(
            t => string.Equals(t.Id, MudDockFactory.BuffsToolId, StringComparison.Ordinal));
        if (tool is null)
        {
            return;
        }

        tool.Title = RequiredBuffs.Count == 0 ? "🛡 Buffy" : $"🛡 Buffy {BuffsBadge}";
    }

    public string NewBuffName
    {
        get => _newBuffName;
        set
        {
            if (SetProperty(ref _newBuffName, value))
            {
                AddBuffCommand.NotifyCanExecuteChanged();
            }
        }
    }

    private void AddBuff()
    {
        var name = NewBuffName.Trim();
        if (name.Length == 0)
        {
            return;
        }

        var normalized = BuffWatchEntry.NormalizeName(name);
        if (RequiredBuffs.Any(b => string.Equals(
                BuffWatchEntry.NormalizeName(b.Name), normalized, StringComparison.OrdinalIgnoreCase)))
        {
            AddToast($"Buff „{name}” jest już na liście.", "info");
            return;
        }

        RequiredBuffs.Add(new BuffWatchEntry(name)
        {
            IsActive = _activeAffectNames.Contains(normalized),
        });
        NewBuffName = string.Empty;
        RefreshBuffIndicators();
        SaveActiveProfile();
    }

    private void DeleteBuff(BuffWatchEntry? entry)
    {
        if (entry is null)
        {
            return;
        }

        RequiredBuffs.Remove(entry);
        RefreshBuffIndicators();
        SaveActiveProfile();
    }

    /// <summary>
    /// Sends "cast &quot;nazwa&quot; self" for every required buff missing from
    /// the latest Char.Affects. Bound to the RECAST button and the /recast command.
    /// </summary>
    private async Task RecastMissingBuffsAsync()
    {
        if (!IsConnected)
        {
            AddToast("Nie połączono — nie można rzucić buffów.", "error");
            return;
        }

        var missing = RequiredBuffs.Where(b => !b.IsActive).ToList();
        if (missing.Count == 0)
        {
            AddToast("Wszystkie wymagane buffy są aktywne.", "info");
            return;
        }

        foreach (var buff in missing)
        {
            await SendTriggeredCommandAsync($"cast \"{buff.Name}\" self");
        }
    }

    // ========================================================================
    // Profiles
    // ========================================================================

    public ObservableCollection<string> AvailableProfiles { get; } = [];

    public bool HasProfiles => AvailableProfiles.Count > 0;

    public RelayCommand SelectProfileCommand { get; }
    public RelayCommand CreateProfileCommand { get; }
    public RelayCommand SwitchProfileCommand { get; }
    public RelayCommand<string> DeleteProfileCommand { get; }

    /// <summary>Name of the currently active profile, or null before one is chosen.</summary>
    public string? ActiveProfileName
    {
        get => _activeProfileName;
        private set
        {
            if (SetProperty(ref _activeProfileName, value))
            {
                OnPropertyChanged(nameof(IsProfileSelected));
                SwitchProfileCommand.NotifyCanExecuteChanged();
            }
        }
    }

    /// <summary>False shows the profile-picker overlay.</summary>
    public bool IsProfileSelected => _activeProfileName is not null;

    public string? SelectedProfileName
    {
        get => _selectedProfileName;
        set
        {
            if (SetProperty(ref _selectedProfileName, value))
            {
                SelectProfileCommand.NotifyCanExecuteChanged();
            }
        }
    }

    /// <summary>Password for the account being created in the picker.</summary>
    public string NewProfilePassword
    {
        get => _newProfilePassword;
        set => SetProperty(ref _newProfilePassword, value);
    }

    /// <summary>
    /// Optional new password typed when selecting an existing account;
    /// non-empty replaces the stored one, empty keeps it.
    /// </summary>
    public string SelectedProfilePassword
    {
        get => _selectedProfilePassword;
        set => SetProperty(ref _selectedProfilePassword, value);
    }

    public string NewProfileName
    {
        get => _newProfileName;
        set
        {
            if (SetProperty(ref _newProfileName, value))
            {
                CreateProfileCommand.NotifyCanExecuteChanged();
            }
        }
    }

    private void SelectProfile()
    {
        var name = SelectedProfileName?.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        var profile = _profiles.Load(name) ?? new ProfileData { Name = name };

        // A password typed in the picker replaces the stored one.
        var typedPassword = SelectedProfilePassword;
        if (!string.IsNullOrEmpty(typedPassword))
        {
            profile.EncryptedPassword = PasswordProtector.Protect(typedPassword);
            _profiles.Save(profile);
            SelectedProfilePassword = string.Empty;
        }

        ActivateProfile(profile);
    }

    private void CreateProfile()
    {
        var name = NewProfileName.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        if (_profiles.Exists(name))
        {
            // Same name already stored — just activate it instead of overwriting.
            ActivateProfile(_profiles.Load(name) ?? new ProfileData { Name = name });
            NewProfileName = string.Empty;
            return;
        }

        var profile = new ProfileData
        {
            Name = name,
            EncryptedPassword = PasswordProtector.Protect(NewProfilePassword),
            Rules =
            [
                new ProfileRule
                {
                    Name = "Skrót look",
                    Type = "alias",
                    Pattern = "^l$",
                    Action = "look",
                    IsEnabled = true,
                },
            ],
        };

        _profiles.Save(profile);

        if (!AvailableProfiles.Contains(name))
        {
            AvailableProfiles.Add(name);
        }

        NewProfileName = string.Empty;
        NewProfilePassword = string.Empty;
        ActivateProfile(profile);
    }

    private void DeleteProfile(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        try
        {
            _profiles.Delete(name);
        }
        catch (IOException exception)
        {
            AddToast($"Nie udało się usunąć konta: {exception.Message}", "error");
            return;
        }

        AvailableProfiles.Remove(name);
        if (SelectedProfileName == name)
        {
            SelectedProfileName = null;
        }

        AddToast($"Konto „{name}” usunięte.", "info");
    }

    private void SwitchProfile()
    {
        if (!IsProfileSelected || IsConnected)
        {
            return;
        }

        SaveActiveProfile();
        _timers.CancelAll();
        SelectedProfileName = ActiveProfileName;
        ActiveProfileName = null;
        _activeProfilePassword = string.Empty;
    }

    private void ActivateProfile(ProfileData profile)
    {
        StopAutowalk("Autowalk zatrzymany (zmiana konta).");

        Notes.Clear();
        AutomationRules.Clear();
        Timers.Clear();
        Locations.Clear();
        Deaths.Clear();
        RequiredBuffs.Clear();

        // Globals first, then the profile's own entries.
        LoadGlobalEntries();

        foreach (var note in profile.Notes)
        {
            Notes.Add(MakeNoteEntry(note, isGlobal: false));
        }

        foreach (var rule in profile.Rules)
        {
            AutomationRules.Add(new AutomationRuleEntry(
                rule.Name, rule.Type, rule.Pattern, rule.Action, rule.IsEnabled));
        }

        foreach (var timer in profile.Timers)
        {
            Timers.Add(MakeTimerEntry(timer, isGlobal: false));
        }

        foreach (var location in profile.Locations)
        {
            var room = Map.MapIndex?.FindFirstRoomByVnum(location.Vnum);
            Locations.Add(new AutowalkLocation(location.Name, location.Vnum, room?.Name));
        }

        foreach (var death in profile.Deaths.Take(MaxDeathMarks))
        {
            var room = Map.MapIndex?.FindFirstRoomByVnum(death.Vnum);
            Deaths.Add(new DeathMarkEntry(
                death.Vnum,
                string.IsNullOrWhiteSpace(death.RoomName) ? room?.Name : death.RoomName,
                death.When));
        }

        foreach (var buffName in profile.RequiredBuffs)
        {
            RequiredBuffs.Add(new BuffWatchEntry(buffName)
            {
                IsActive = _activeAffectNames.Contains(BuffWatchEntry.NormalizeName(buffName)),
            });
        }

        RefreshBuffIndicators();

        _activeProfilePassword = PasswordProtector.Unprotect(profile.EncryptedPassword);

        ActiveProfileName = profile.Name;
        ApplyAutomation();
        _timers.CancelAll();
        SyncAllTimers();
        AddToast($"Konto „{profile.Name}” aktywne.", "info");
        ProfileActivated?.Invoke(profile.Name);
    }

    /// <summary>Appends entries from the shared global store to the working collections.</summary>
    private void LoadGlobalEntries()
    {
        var global = _profiles.LoadGlobal();

        foreach (var note in global.Notes)
        {
            Notes.Add(MakeNoteEntry(note, isGlobal: true));
        }

        foreach (var rule in global.Rules)
        {
            AutomationRules.Add(new AutomationRuleEntry(
                rule.Name, rule.Type, rule.Pattern, rule.Action, rule.IsEnabled, isGlobal: true));
        }

        foreach (var timer in global.Timers)
        {
            Timers.Add(MakeTimerEntry(timer, isGlobal: true));
        }

        foreach (var location in global.Locations)
        {
            var room = Map.MapIndex?.FindFirstRoomByVnum(location.Vnum);
            Locations.Add(new AutowalkLocation(location.Name, location.Vnum, room?.Name, isGlobal: true));
        }
    }

    private static NoteEntry MakeNoteEntry(ProfileNote note, bool isGlobal) => new()
    {
        Title = note.Title,
        Content = note.Content,
        CreatedAt = note.CreatedAt,
        IsGlobal = isGlobal,
    };

    private static ProfileNote ToProfileNote(NoteEntry n) => new()
    {
        Title = n.Title,
        Content = n.Content,
        CreatedAt = n.CreatedAt,
        IsGlobal = n.IsGlobal,
    };

    private static TimerEntry MakeTimerEntry(ProfileTimer timer, bool isGlobal) => new()
    {
        Id = string.IsNullOrWhiteSpace(timer.Id) ? Guid.NewGuid().ToString("N") : timer.Id,
        Name = timer.Name,
        Minutes = timer.Minutes,
        Seconds = timer.Seconds,
        Milliseconds = timer.Milliseconds,
        CommandsText = !string.IsNullOrEmpty(timer.CommandsText)
            ? timer.CommandsText
            : string.Join(Environment.NewLine, timer.Commands),
        IsEnabled = timer.IsEnabled,
        IsGlobal = isGlobal,
    };

    private static ProfileRule ToProfileRule(AutomationRuleEntry r) => new()
    {
        Name = r.Name,
        Type = r.Type,
        Pattern = r.Pattern,
        Action = r.Action,
        IsEnabled = r.IsEnabled,
        IsGlobal = r.IsGlobal,
    };

    private ProfileTimer ToProfileTimer(TimerEntry t) => new()
    {
        Id = t.Id,
        Name = t.Name,
        Minutes = t.Minutes,
        Seconds = t.Seconds,
        Milliseconds = t.Milliseconds,
        Commands = t.GetCommands(CommandStackingSeparator).ToList(),
        CommandsText = t.CommandsText,
        IsEnabled = t.IsEnabled,
        IsGlobal = t.IsGlobal,
    };

    private static ProfileLocation ToProfileLocation(AutowalkLocation l) => new()
    {
        Name = l.Name,
        Vnum = l.Vnum,
        IsGlobal = l.IsGlobal,
    };

    /// <summary>
    /// Persists the working collections: global entries go to the shared
    /// global file, the rest to the active profile (if any).
    /// </summary>
    private void SaveActiveProfile()
    {
        var global = new GlobalData
        {
            Notes = Notes.Where(n => n.IsGlobal).Select(ToProfileNote).ToList(),
            Rules = AutomationRules.Where(r => r.IsGlobal).Select(ToProfileRule).ToList(),
            Timers = Timers.Where(t => t.IsGlobal).Select(ToProfileTimer).ToList(),
            Locations = Locations.Where(l => l.IsGlobal).Select(ToProfileLocation).ToList(),
        };

        try
        {
            _profiles.SaveGlobal(global);
        }
        catch (Exception exception)
        {
            AddToast($"Nie udało się zapisać globalnych wpisów: {exception.Message}", "error");
        }

        if (ActiveProfileName is null)
        {
            return;
        }

        var profile = new ProfileData
        {
            Name = ActiveProfileName,
            Notes = Notes.Where(n => !n.IsGlobal).Select(ToProfileNote).ToList(),
            Rules = AutomationRules.Where(r => !r.IsGlobal).Select(ToProfileRule).ToList(),
            Timers = Timers.Where(t => !t.IsGlobal).Select(ToProfileTimer).ToList(),
            Locations = Locations.Where(l => !l.IsGlobal).Select(ToProfileLocation).ToList(),
            Deaths = Deaths.Select(d => new ProfileDeath
            {
                Vnum = d.Vnum,
                RoomName = d.RoomName ?? string.Empty,
                When = d.When,
            }).ToList(),
            RequiredBuffs = RequiredBuffs.Select(b => b.Name).ToList(),
            EncryptedPassword = PasswordProtector.Protect(_activeProfilePassword),
        };

        try
        {
            _profiles.Save(profile);
        }
        catch (Exception exception)
        {
            AddToast($"Nie udało się zapisać konta: {exception.Message}", "error");
        }
    }

    /// <summary>
    /// Rebuilds the alias/trigger engines from the active profile's rules.
    /// Timers are managed separately (see SyncTimer).
    /// </summary>
    private void ApplyAutomation()
    {
        _aliases.Clear();
        _triggers.Clear();

        foreach (var rule in AutomationRules)
        {
            if (!rule.IsEnabled)
            {
                continue;
            }

            try
            {
                switch (rule.Type)
                {
                    case "alias":
                        _aliases.Add(new AliasRule(rule.Name, rule.Pattern, rule.Action));
                        break;

                    case "trigger":
                        _triggers.Add(new TriggerRule(rule.Name, rule.Pattern, rule.Action));
                        break;
                }
            }
            catch (ArgumentException)
            {
                // Invalid regex pattern in a stored rule — skip it.
                AddToast($"Pominięto regułę „{rule.Name}”: nieprawidłowy wzorzec.", "error");
            }
        }
    }

    // --- Command history ---
    private const int CommandHistoryMaxSize = 100;
    public ObservableCollection<string> CommandHistory { get; } = [];

    public IRelayCommand<string> ExaminePersonCommand { get; }
    public IRelayCommand<string> KillPersonCommand { get; }

    // --- Log filter tabs ---
    public ObservableCollection<LogFilter> LogFilters { get; } = [];

    // --- Character vitals (mock) ---
    public CharacterVitals Vitals { get; } = new();

    // --- Character conditions (live, from Char.Condition GMCP) ---
    public ObservableCollection<string> Conditions { get; } = [];

    // --- Status effects (live, from Char.Affects GMCP) ---
    public ObservableCollection<StatusEffect> Effects { get; } = [];

    // --- People in room (mock) ---
    public ObservableCollection<PersonEntry> People { get; } = [];

    // --- Group members (mock) ---
    public ObservableCollection<GroupMember> Group { get; } = [];

    public ObservableCollection<MemSpellCircle> MemSpells { get; } = [];

    // --- Automation rules (mock) ---
    public ObservableCollection<AutomationRuleEntry> AutomationRules { get; } = [];

    // --- Notes ---
    public ObservableCollection<NoteEntry> Notes { get; } = [];

    // --- Toast messages ---
    public ObservableCollection<ToastMessage> Toasts { get; } = [];

    // ========================================================================
    // New commands
    // ========================================================================

    public RelayCommand AddNoteCommand => new(AddNote);
    public RelayCommand<NoteEntry> DeleteNoteCommand => new(DeleteNote);
    public RelayCommand<NoteEntry> EditNoteCommand => new(EditNote);
    public RelayCommand CancelNoteEditCommand => new(CancelNoteEdit);
    public RelayCommand<string> CopyToCommandBarCommand => new(CopyToCommandBar);
    public RelayCommand ClearToastsCommand => new(ClearToasts);

    // ========================================================================
    // Existing commands (preserved unchanged)
    // ========================================================================

    private bool CanConnect() =>
        !IsBusy &&
        !IsConnected &&
        !string.IsNullOrWhiteSpace(Host) &&
        Port is >= 1 and <= 65535;

    private bool CanDisconnect() => !IsBusy && IsConnected;

    private bool CanSendCommand() => !IsBusy && IsConnected;

    private async Task ConnectAsync()
    {
        IsBusy = true;
        EmitSystem($"Łączenie z {Host}:{Port}...", 36);

        try
        {
            await _session.ConnectAsync(Host.Trim(), Port);
            IsConnected = true;
            await AutoLoginAsync();
        }
        catch (Exception exception)
        {
            IsConnected = false;
            StatusText = "Błąd połączenia";
            EmitSystem(exception.Message, 31);
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Sends the account name and stored password right after connecting,
    /// so the MUD login prompt is answered automatically.
    /// </summary>
    private async Task AutoLoginAsync()
    {
        var name = ActiveProfileName;
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrEmpty(_activeProfilePassword))
        {
            return;
        }

        // Give the server a moment to show the login prompt before answering it.
        await Task.Delay(500);
        await _session.SendCommandAsync(name);
        await Task.Delay(500);
        await _session.SendCommandAsync(_activeProfilePassword);
        EmitSystem($"Zalogowano automatycznie jako {name}.", 36);
    }

    private async Task DisconnectAsync()
    {
        IsBusy = true;

        try
        {
            await _session.DisconnectAsync();
        }
        finally
        {
            IsConnected = false;
            IsBusy = false;
        }
    }

    private async Task SendCurrentCommandAsync()
    {
        var sourceCommand = CommandText.Trim();

        // Split on the stacking separator first (also handles newlines).
        // Alias processing runs per segment; autowalk commands are consumed
        // per segment, and non-slash segments are forwarded normally.
        // An empty command is meaningful to a MUD: it sends a bare line ending.
        // CommandStacker intentionally discards empty items for aliases and timers,
        // so preserve only the explicitly empty command entered by the user here.
        IReadOnlyList<string> segments = sourceCommand.Length == 0
            ? [string.Empty]
            : CommandStacker.Split(sourceCommand, CommandStackingSeparator);

        // Track history – record the original typed command as one entry.
        CommandHistory.Insert(0, sourceCommand);
        while (CommandHistory.Count > CommandHistoryMaxSize)
        {
            CommandHistory.RemoveAt(CommandHistory.Count - 1);
        }

        foreach (var segment in segments)
        {
            if (TryHandleAutowalkCommand(segment))
            {
                continue;
            }

            if (string.Equals(segment, "/recast", StringComparison.OrdinalIgnoreCase))
            {
                await RecastMissingBuffsAsync();
                continue;
            }

            // Alias processing happens per stacked segment so that an alias
            // that replaces one segment can still produce multiple commands
            // (via newlines in its replacement).
            var commands = _aliases.ProcessCommands(segment, CommandStackingSeparator);

            foreach (var command in commands)
            {
                EmitSystem($"> {command}", 90);

                try
                {
                    await _session.SendCommandAsync(command);
                }
                catch (Exception exception)
                {
                    EmitSystem(exception.Message, 31);
                }
            }
        }
    }

    // ========================================================================
    // New command implementations
    // ========================================================================

    private void ExecuteExaminePerson(string? name)
    {
        if (!string.IsNullOrWhiteSpace(name) && IsConnected)
        {
            _ = _session.SendCommandAsync($"exa {name}");
        }
    }

    private void ExecuteKillPerson(string? name)
    {
        if (!string.IsNullOrWhiteSpace(name) && IsConnected)
        {
            _ = _session.SendCommandAsync($"kill {name}");
        }
    }

    private void AddNote()
    {
        if (string.IsNullOrWhiteSpace(NewNoteTitle))
        {
            return;
        }

        if (_editedNote is { } edited)
        {
            edited.Title = NewNoteTitle;
            edited.Content = NewNoteContent;
            edited.IsGlobal = NewNoteIsGlobal;
        }
        else
        {
            Notes.Insert(0, new NoteEntry
            {
                Title = NewNoteTitle,
                Content = NewNoteContent,
                CreatedAt = DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm"),
                IsGlobal = NewNoteIsGlobal,
            });
        }

        ClearNoteForm();
        SaveActiveProfile();
    }

    private void EditNote(NoteEntry? note)
    {
        if (note is null)
        {
            return;
        }

        _editedNote = note;
        NewNoteTitle = note.Title;
        NewNoteContent = note.Content;
        NewNoteIsGlobal = note.IsGlobal;
        IsNoteFormExpanded = true;
        NotifyNoteEditModeChanged();
    }

    private void CancelNoteEdit() => ClearNoteForm();

    private void ClearNoteForm()
    {
        _editedNote = null;
        NewNoteTitle = string.Empty;
        NewNoteContent = string.Empty;
        NewNoteIsGlobal = false;
        NotifyNoteEditModeChanged();
    }

    private void NotifyNoteEditModeChanged()
    {
        OnPropertyChanged(nameof(IsEditingNote));
        OnPropertyChanged(nameof(NoteFormButtonText));
        OnPropertyChanged(nameof(NoteFormHeader));
    }

    private void DeleteNote(NoteEntry? note)
    {
        if (note is null)
        {
            return;
        }

        if (ReferenceEquals(note, _editedNote))
        {
            ClearNoteForm();
        }

        Notes.Remove(note);
        SaveActiveProfile();
    }

    private void CopyToCommandBar(string? text)
    {
        if (!string.IsNullOrWhiteSpace(text))
        {
            CommandText = text;
        }
    }

    private void ClearToasts()
    {
        Toasts.Clear();
    }

    public void ReportStartupError(Exception exception)
    {
        // Unwrap TargetInvocationException etc. so the dialog shows the real cause.
        var rootCause = exception.GetBaseException();
        StartupErrorMessage = "Nie udało się uruchomić interfejsu.";
        StartupErrorDetails = rootCause.Message;
        AddToast("Wystąpił błąd uruchamiania interfejsu.", "error");
        EmitSystem(rootCause.Message, 31);
    }

    private void ClearStartupError()
    {
        StartupErrorMessage = null;
        StartupErrorDetails = null;
    }

    private async Task RetryStartupAsync()
    {
        try
        {
            await InitializeAsync();
        }
        catch (Exception exception)
        {
            ReportStartupError(exception);
        }
    }

    private void AddToast(string text, string type = "info")
    {
        // Newest goes last: the top-bar strip is right-aligned, so the latest
        // toast hugs the right edge and older ones get clipped on the left.
        var toast = new ToastMessage { Text = text, Type = type };
        Toasts.Add(toast);
        while (Toasts.Count > 10)
        {
            Toasts.RemoveAt(0);
        }
    }

    // ========================================================================
    // Session event handlers (preserved)
    // ========================================================================

    private void OnTextReceived(string text)
    {
        Dispatcher.UIThread.Post(() => OutputReceived?.Invoke(text));
    }

    private void OnLineReceived(string line)
    {
        if (IsDeathLine(line))
        {
            // Capture the position on the UI thread — Map state is UI-bound.
            Dispatcher.UIThread.Post(RecordDeath);
        }

        if (AutowalkRecoveryPolicy.IsLockedGateMessage(line))
        {
            Dispatcher.UIThread.Post(HandleLockedAutowalkGate);
        }

        if (GroupOrdersEnabled
            && GroupOrderPolicy.TryGetCommand(
                line, _latestCharacterName, _latestGroupUpdate, out var orderedCommand))
        {
            QueueTriggeredCommands([orderedCommand]);
        }

        var commands = _triggers.Evaluate(line, CommandStackingSeparator);
        if (commands.Count == 0)
        {
            return;
        }

        QueueTriggeredCommands(commands);
    }

    private void TryAutoAssist()
    {
        if (_autoAssist.ShouldAssist(
                AutoAssistEnabled && IsConnected,
                Map.CurrentVnum,
                _latestCharacterName,
                string.Equals(_latestCharacterPosition, "fighting", StringComparison.OrdinalIgnoreCase),
                _latestGroupUpdate,
                _latestRoomPeople))
        {
            QueueTriggeredCommands(["as"]);
        }
    }

    private void QueueTriggeredCommands(IReadOnlyList<string> commands)
    {
        Task task;
        lock (_triggerTasksLock)
        {
            // Reject new work if the view-model is shutting down.
            // This check + task creation + registration are all inside
            // the same critical section that DisposeAsync uses to flip
            // _acceptingTriggerTasks, so no task can be started after
            // DisposeAsync has already drained and disposed the semaphore.
            if (!_acceptingTriggerTasks)
            {
                return;
            }

            // Capture the current tail of the FIFO chain.  The new task
            // will await this previous batch (swallowing its faults) so
            // that batches are sent strictly in receive order.
            var previous = _triggerQueueTail;

            // Create the new batch task and register it as the new tail.
            // EnqueueBatchAsync yields immediately so the lock is held
            // only for the duration of the synchronous preamble.
            task = EnqueueBatchAsync(previous, commands);
            _triggerQueueTail = task;
            _triggerTasks.Add(task);
        }

        // Fire-and-forget continuation that removes the task from the
        // tracking list once it completes, preventing unbounded growth
        // of _triggerTasks during normal operation.
        _ = RemoveWhenCompleted(task);
    }

    private void HandleLockedAutowalkGate()
    {
        if (_autowalkPath is null || _autowalkWaitingForGate ||
            _autowalkOpeningStep != _autowalkStep)
        {
            return;
        }

        _autowalkWaitingForGate = true;
        _autowalkOpeningStep = null;
        _autowalkGateCommandsSent = false;
        _autowalkGateIsOpen = false;
        AutowalkStatusText = "Brama zamknięta — próbuję ją uruchomić i czekam na GMCP.";
        _ = SendGateCommandsAsync(_autowalkCts.Token);
    }

    private async Task SendGateCommandsAsync(CancellationToken cancellationToken)
    {
        try
        {
            foreach (var command in new[] { "zapukaj", "pull", "uderz" })
            {
                cancellationToken.ThrowIfCancellationRequested();
                await SendTriggeredCommandAsync(command, cancellationToken);
            }

            Dispatcher.UIThread.Post(() =>
            {
                if (cancellationToken.IsCancellationRequested || !_autowalkWaitingForGate)
                {
                    return;
                }

                _autowalkGateCommandsSent = true;
                TryContinueThroughOpenedGate();
            });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The autowalk was stopped while the gate sequence was being sent.
        }
    }

    /// <summary>
    /// Awaits <paramref name="task"/> and removes it from
    /// <see cref="_triggerTasks"/> under lock when it completes (or faults,
    /// or is cancelled).  All exceptions are swallowed — trigger-command
    /// errors are already logged inside <see cref="SendTriggeredCommandAsync"/>,
    /// and <see cref="OperationCanceledException"/> is expected during
    /// disposal shutdown.
    /// </summary>
    private async Task RemoveWhenCompleted(Task task)
    {
        try
        {
            await task.ConfigureAwait(false);
        }
        catch
        {
            // Swallow all exceptions (see xmldoc above).
        }

        lock (_triggerTasksLock)
        {
            _triggerTasks.Remove(task);
        }
    }

    /// <summary>
    /// Awaits <paramref name="previous"/> (the prior batch in the FIFO
    /// chain) and then sends <paramref name="commands"/>.  Exceptions
    /// from the previous task are swallowed so a faulted batch never
    /// stalls later batches.  The semaphore inside
    /// <see cref="SendTriggeredCommandsAsync"/> provides an additional
    /// layer of non-interleaving protection (belt-and-suspenders).
    /// </summary>
    private async Task EnqueueBatchAsync(Task previous, IReadOnlyList<string> commands)
    {
        // Yield immediately so the caller's lock is released and this
        // method returns a Task to the caller.  The continuation runs
        // on a thread-pool thread (the caller fires from the network
        // receive loop, which has no SynchronizationContext).
        await Task.Yield();

        try
        {
            await previous.ConfigureAwait(false);
        }
        catch
        {
            // Swallow all exceptions from the prior batch so the FIFO
            // chain continues.  Individual command errors are already
            // logged inside SendTriggeredCommandAsync, and cancellation
            // of the current batch will be observed in its own
            // SendTriggeredCommandsAsync call below.
        }

        await SendTriggeredCommandsAsync(commands);
    }

    private async Task SendTriggeredCommandsAsync(IReadOnlyList<string> commands)
    {
        await _triggerSendLock.WaitAsync(_triggerCts.Token);
        try
        {
            foreach (var command in commands)
            {
                await SendTriggeredCommandAsync(command);
            }
        }
        finally
        {
            _triggerSendLock.Release();
        }
    }

    private async Task SendTriggeredCommandAsync(
        string command,
        CancellationToken cancellationToken = default)
    {
        Dispatcher.UIThread.Post(() => EmitSystem($"> {command}", 90));

        try
        {
            await _session.SendCommandAsync(command, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            Dispatcher.UIThread.Post(() => EmitSystem(exception.Message, 31));
        }
    }

    private void OnGmcpReceived(GmcpMessage message)
    {
        // Exits must be parsed before the location resolver fires
        // LocationChanged, so autowalk sees the new room's doors.
        _roomExits.Process(message);
        _locationResolver.Process(message);
        _characterState.Process(message);

        Dispatcher.UIThread.Post(() =>
        {
            GmcpMessages.Insert(0, new GmcpEntryViewModel(
                message.Package,
                string.IsNullOrWhiteSpace(message.Json) ? "(bez danych)" : message.Json,
                DateTimeOffset.Now.ToString("HH:mm:ss")));

            while (GmcpMessages.Count > 100)
            {
                GmcpMessages.RemoveAt(GmcpMessages.Count - 1);
            }
        });
    }

    private void OnCharacterVitalsChanged(CharacterVitalsUpdate update)
    {
        if (update.Mv is { } movement) _latestMovement = movement;
        if (update.MaxMv is { } maximumMovement) _latestMaximumMovement = maximumMovement;
        if (update.Name is { } name) _latestCharacterName = name;
        if (update.Position is { } position) _latestCharacterPosition = position;
        TryAutoAssist();

        Dispatcher.UIThread.Post(() =>
        {
            if (update.Hp is { } hp) Vitals.HitPoints = hp;
            if (update.MaxHp is { } maxHp) Vitals.MaxHitPoints = maxHp;
            if (update.Mv is { } mv) Vitals.EndurancePoints = mv;
            if (update.MaxMv is { } maxMv) Vitals.MaxEndurancePoints = maxMv;
            if (update.Level is { } level) Vitals.Level = level;
            if (update.Name is { } name) Vitals.Name = name;
            if (update.Sex is { } sex) Vitals.SexDisplay = TranslateSex(sex);
            if (update.Position is { } position) Vitals.PositionDisplay = TranslatePosition(position);

            if (update.Mem is { } mem)
            {
                Vitals.SpellPoints = mem;
                if (mem > Vitals.MaxSpellPoints)
                {
                    Vitals.MaxSpellPoints = mem;
                }
            }
        });
    }

    private void OnCharacterConditionChanged(CharacterConditionUpdate update)
    {
        if (update.Position is { } position)
        {
            _latestCharacterPosition = position;
        }

        TryAutoAssist();

        Dispatcher.UIThread.Post(() =>
        {
            if (update.Position is { } position)
            {
                Vitals.PositionDisplay = TranslatePosition(position);
            }

            Conditions.Clear();
            foreach (var (flag, active) in update.Flags)
            {
                if (active)
                {
                    Conditions.Add(TranslateCondition(flag));
                }
            }
        });
    }

    private void OnCharacterAffectsChanged(IReadOnlyList<CharacterAffect> affects)
    {
        Dispatcher.UIThread.Post(() =>
        {
            Effects.Clear();
            _activeAffectNames.Clear();
            foreach (var affect in affects)
            {
                Effects.Add(StatusEffect.FromCore(affect));
                _activeAffectNames.Add(BuffWatchEntry.NormalizeName(affect.Name));
            }

            foreach (var buff in RequiredBuffs)
            {
                buff.IsActive = _activeAffectNames.Contains(BuffWatchEntry.NormalizeName(buff.Name));
            }

            RefreshBuffIndicators();
        });
    }

    private void OnRoomPeopleChanged(IReadOnlyList<RoomPerson> people)
    {
        _latestRoomPeople = people.ToArray();
        TryAutoAssist();

        Dispatcher.UIThread.Post(() =>
        {
            People.Clear();
            foreach (var person in people)
            {
                var isSelf = string.Equals(person.Name, _latestCharacterName, StringComparison.OrdinalIgnoreCase);
                People.Add(new PersonEntry(person.Name, person.IsFighting, person.Enemy, isSelf));
            }
        });
    }

    private void OnGroupChanged(CharacterGroupUpdate update)
    {
        _latestGroupUpdate = update;
        TryAutoAssist();
        Dispatcher.UIThread.Post(() =>
        {
            Map.UpdateGroupMembers(update.Members, _latestCharacterName);
            Group.Clear();
            foreach (var member in update.Members)
            {
                if (string.Equals(member.Name, _latestCharacterName, StringComparison.OrdinalIgnoreCase))
                    continue;
                var roomDisplay = ResolveRoomDisplay(member.Room);
                Group.Add(GroupMember.FromCore(member, roomDisplay));
            }
        });
    }

    private void OnMemSpellsChanged(IReadOnlyList<MemorizedSpell> spells)
    {
        _latestMemorizedSpells = spells.ToArray();

        Dispatcher.UIThread.Post(() =>
        {
            MemSpells.Clear();
            foreach (var circle in MemSpellCircle.FromCore(spells))
            {
                MemSpells.Add(circle);
            }

        });
    }

    /// <summary>
    /// Resolves a raw room vnum to a display string.
    /// Uses the loaded map room name when available, falls back to "pokój {vnum}",
    /// or "?" when there is no room value at all.
    /// </summary>
    private string ResolveRoomDisplay(string? room)
    {
        if (room is null)
        {
            return "?";
        }

        var mapRoom = Map.MapIndex?.FindFirstRoomByVnum(room);
        var mapName = mapRoom?.Name?.Trim();
        if (!string.IsNullOrEmpty(mapName))
        {
            return mapName;
        }

        return $"pokój {room}";
    }

    /// <summary>
    /// Rebuilds the Group collection when MapIndex becomes available after map loading,
    /// so that entries that previously showed "pokój xxx" switch to resolved room names.
    /// </summary>
    private void OnMapPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MapViewModel.MapIndex) && _latestGroupUpdate is not null)
        {
            var update = _latestGroupUpdate;
            Dispatcher.UIThread.Post(() =>
            {
                Map.UpdateGroupMembers(update.Members, _latestCharacterName);
                Group.Clear();
                foreach (var member in update.Members)
                {
                    var roomDisplay = ResolveRoomDisplay(member.Room);
                    Group.Add(GroupMember.FromCore(member, roomDisplay));
                }
            });
        }
    }

    private static string TranslateSex(string sex) => sex.ToUpperInvariant() switch
    {
        "M" => "Mężczyzna",
        "F" or "K" => "Kobieta",
        _ => sex,
    };

    private static string TranslatePosition(string position) => position switch
    {
        "standing" => "Stoi",
        "sitting" => "Siedzi",
        "resting" => "Odpoczywa",
        "sleeping" => "Śpi",
        "fighting" => "Walczy",
        "stunned" => "Oszołomiony",
        "incap" or "incapacitated" => "Obezwładniony",
        "mortal" or "mortally" => "Umierający",
        "dead" => "Martwy",
        "lying" => "Leży",
        _ => position,
    };

    private static string TranslateCondition(string flag) => flag.ToLowerInvariant() switch
    {
        "overweight" => "Przeciążenie",
        "drunk" => "Upojenie",
        "thirsty" => "Pragnienie",
        "hungry" => "Głód",
        "sleepy" => "Senność",
        "smoking" => "Pali",
        "thighjab" => "Rana uda",
        "bleedingwound" => "Krwawiąca rana",
        "bleed" => "Krwawienie",
        "halucinations" => "Halucynacje",
        _ => flag,
    };

    private void OnGmcpSent(GmcpMessage message)
    {
        Dispatcher.UIThread.Post(() =>
        {
            SentGmcpMessages.Insert(0, new GmcpEntryViewModel(
                message.Package,
                string.IsNullOrWhiteSpace(message.Json) ? "(bez danych)" : message.Json,
                DateTimeOffset.Now.ToString("HH:mm:ss")));

            while (SentGmcpMessages.Count > 100)
            {
                SentGmcpMessages.RemoveAt(SentGmcpMessages.Count - 1);
            }
        });
    }

    private void OnStatusChanged(string status)
    {
        Dispatcher.UIThread.Post(() =>
        {
            StatusText = status;
        });
    }

    private void OnConnectionClosed()
    {
        Dispatcher.UIThread.Post(() => IsConnected = false);
    }

    private void OnConnectionError(Exception exception)
    {
        Dispatcher.UIThread.Post(() =>
        {
            IsConnected = false;
            EmitSystem(exception.Message, 31);
        });
    }

    private void EmitSystem(string text, int ansiColor)
    {
        OutputReceived?.Invoke($"\u001b[{ansiColor}m{text}\u001b[0m\n");
    }

    private void RefreshCommands()
    {
        _connectCommand.NotifyCanExecuteChanged();
        _disconnectCommand.NotifyCanExecuteChanged();
        _sendCommandCommand.NotifyCanExecuteChanged();
        SwitchProfileCommand.NotifyCanExecuteChanged();
    }

    // ========================================================================
    // Mock data
    // ========================================================================

    private void PopulateMockData()
    {
        // Log filter tabs
        foreach (var filter in Models.LogFilters.Defaults)
        {
            LogFilters.Add(filter);
        }

        // Status effects are populated live from Char.Affects GMCP.

        // Group members are populated live from Char.Group GMCP.

        // Notes (mock)
        Notes.Add(new NoteEntry
        {
            Title = "Lista zakupów",
            Content = "- Mikstura leczenia x5\n- Zwój teleportacji\n- Nowy miecz",
            CreatedAt = "2026-01-15 14:22",
        });
        Notes.Add(new NoteEntry
        {
            Title = "Kluczowe lokacje",
            Content = "Gildia magów: 3n, 2w od rynku\nKowal: 1e, 4s od rynku",
            CreatedAt = "2026-01-14 09:10",
        });

        // Welcome toast
        AddToast("Witaj w MudClient! Łączenie automatyczne — możesz zmienić host/port i połączyć się ręcznie.", "info");
    }

    // ========================================================================
    // Dispose
    // ========================================================================

    public async ValueTask DisposeAsync()
    {
        SaveActiveProfile();

        try
        {
            _dockLayoutService.Save(_dockFactory.Snapshot(Layout));
        }
        catch (IOException)
        {
            // Best-effort; the previous layout file (if any) remains on disk.
        }

        _characterState.VitalsChanged -= OnCharacterVitalsChanged;
        _characterState.ConditionChanged -= OnCharacterConditionChanged;
        _characterState.PeopleChanged -= OnRoomPeopleChanged;
        _characterState.GroupChanged -= OnGroupChanged;
        _characterState.MemSpellsChanged -= OnMemSpellsChanged;
        _characterState.AffectsChanged -= OnCharacterAffectsChanged;

        _session.TextReceived -= OnTextReceived;
        _session.LineReceived -= OnLineReceived;
        _session.GmcpReceived -= OnGmcpReceived;
        _session.GmcpSent -= OnGmcpSent;
        _session.StatusChanged -= OnStatusChanged;
        _session.ConnectionError -= OnConnectionError;
        _session.ConnectionClosed -= OnConnectionClosed;

        Map.PropertyChanged -= OnMapPropertyChanged;
        _locationResolver.LocationChanged -= OnAutowalkLocationChanged;
        _roomExits.ExitsChanged -= OnRoomExitsChanged;
        Map.RoomDoubleClicked -= OnMapRoomDoubleClicked;

        _autowalkCts.Cancel();

        // Phase 1 — stop accepting new trigger tasks atomically.
        // OnLineReceived holds the same lock when it checks the flag,
        // creates a task, and registers it, so after this block no new
        // task will be added to _triggerTasks.
        List<Task> pending;
        lock (_triggerTasksLock)
        {
            _acceptingTriggerTasks = false;
            pending = new List<Task>(_triggerTasks);
            _triggerTasks.Clear();
        }

        // Phase 2 — cancel the CTS so any in-flight WaitAsync calls
        // on the semaphore observe cancellation and exit without
        // acquiring the lock.
        _triggerCts.Cancel();

        // Phase 3 — drain the tasks we snapshotted above.
        foreach (var task in pending)
        {
            try
            {
                await task;
            }
            catch (OperationCanceledException)
            {
                // Expected — the batch was cancelled by our CTS.
            }
            catch (Exception)
            {
                // Swallow any other exceptions during shutdown so that
                // they do not become unobserved and tear down the process.
            }
        }

        // Phase 4 — belt-and-suspenders re-check.  The flag gate above
        // prevents new additions, and RemoveWhenCompleted only removes
        // from the list, so this loop should be empty.  We keep it as a
        // defense-in-depth measure against any unanticipated path.
        while (true)
        {
            lock (_triggerTasksLock)
            {
                if (_triggerTasks.Count == 0)
                {
                    break;
                }

                pending = new List<Task>(_triggerTasks);
                _triggerTasks.Clear();
            }

            foreach (var task in pending)
            {
                try
                {
                    await task;
                }
                catch (OperationCanceledException)
                {
                    // Expected.
                }
                catch (Exception)
                {
                    // Swallow.
                }
            }
        }

        // Final gate: acquire the semaphore and release it immediately.
        // This protects against the edge case where a trigger task managed
        // to acquire the semaphore before the CTS was cancelled but had
        // not yet released it.  Waiting ensures the release happened.
        await _triggerSendLock.WaitAsync();
        _triggerSendLock.Release();

        await _timers.DisposeAsync();
        await _session.DisposeAsync();
        Map.Dispose();
        _triggerSendLock.Dispose();
        _triggerCts.Dispose();
        _autowalkCts.Dispose();
    }
}

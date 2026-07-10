using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Text.RegularExpressions;
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
    private readonly ProfileService _profiles;

    private readonly AsyncRelayCommand _connectCommand;
    private readonly AsyncRelayCommand _disconnectCommand;
    private readonly AsyncRelayCommand _sendCommandCommand;
    private readonly AsyncRelayCommand _retryStartupCommand;

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

    // --- Timers ---
    private string _newTimerName = string.Empty;
    private string _newTimerMinutes = "0";
    private string _newTimerSeconds = "0";
    private string _newTimerMilliseconds = "0";
    private string _newTimerCommands = string.Empty;

    // --- Profiles ---
    private string? _activeProfileName;
    private string? _selectedProfileName;
    private string _newProfileName = string.Empty;

    public MainWindowViewModel(ProfileService? profileService = null, AppSettingsService? settingsService = null)
    {
        _profiles = profileService ?? new ProfileService();
        _settingsService = settingsService ?? new AppSettingsService();
        _settings = _settingsService.Load();
        PopulateAvailableFonts();
        _settingsLoaded = true;
        _connectCommand = new AsyncRelayCommand(ConnectAsync, CanConnect);
        _disconnectCommand = new AsyncRelayCommand(DisconnectAsync, CanDisconnect);
        _sendCommandCommand = new AsyncRelayCommand(SendCurrentCommandAsync, CanSendCommand);
        _retryStartupCommand = new AsyncRelayCommand(RetryStartupAsync);
        QuickCommandExecuteCommand = new RelayCommand<string>(ExecuteQuickCommand);
        SelectProfileCommand = new RelayCommand(SelectProfile, () => !string.IsNullOrWhiteSpace(SelectedProfileName));
        CreateProfileCommand = new RelayCommand(CreateProfile, () => !string.IsNullOrWhiteSpace(NewProfileName));
        SwitchProfileCommand = new RelayCommand(SwitchProfile, () => IsProfileSelected && !IsConnected && !IsBusy);
        AddTimerCommand = new RelayCommand(AddTimer, () => !string.IsNullOrWhiteSpace(NewTimerName));
        DeleteTimerCommand = new RelayCommand<TimerEntry>(DeleteTimer);
        ToggleTimerCommand = new RelayCommand<TimerEntry>(ToggleTimer);
        AddRuleCommand = new RelayCommand(AddRule, CanAddRule);
        DeleteRuleCommand = new RelayCommand<AutomationRuleEntry>(DeleteRule);
        ToggleRuleCommand = new RelayCommand<AutomationRuleEntry>(ToggleRule);

        _session.TextReceived += OnTextReceived;
        _session.LineReceived += OnLineReceived;
        _session.GmcpReceived += OnGmcpReceived;
        _session.GmcpSent += OnGmcpSent;
        _session.StatusChanged += OnStatusChanged;
        _session.ConnectionError += OnConnectionError;

        Map = new MapViewModel(AppContext.BaseDirectory, _locationResolver);

        PopulateMockData();

        foreach (var name in _profiles.ListProfileNames())
        {
            AvailableProfiles.Add(name);
        }

        AvailableProfiles.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasProfiles));
    }

    public MapViewModel Map { get; }

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

    // ========================================================================
    // App settings (system-wide, not per profile)
    // ========================================================================

    public ObservableCollection<string> AvailableFonts { get; } = [];

    public double MinOutputFontSize => AppSettings.MinOutputFontSize;
    public double MaxOutputFontSize => AppSettings.MaxOutputFontSize;

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

    public RelayCommand ResetOutputFontCommand => new(() =>
    {
        OutputFontFamily = AppSettings.DefaultOutputFontFamily;
        OutputFontSize = AppSettings.DefaultOutputFontSize;
    });

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

        AutomationRules.Add(new AutomationRuleEntry(
            NewRuleName.Trim(), NewRuleType, NewRulePattern, NewRuleAction, isEnabled: true));

        NewRuleName = string.Empty;
        NewRulePattern = string.Empty;
        NewRuleAction = string.Empty;

        ApplyAutomation();
        SaveActiveProfile();
    }

    private void DeleteRule(AutomationRuleEntry? entry)
    {
        if (entry is null)
        {
            return;
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

    private void AddTimer()
    {
        var name = NewTimerName.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        var entry = new TimerEntry
        {
            Name = name,
            Minutes = ParseNonNegative(NewTimerMinutes),
            Seconds = ParseNonNegative(NewTimerSeconds),
            Milliseconds = ParseNonNegative(NewTimerMilliseconds),
            CommandsText = NewTimerCommands,
        };

        if (entry.Interval <= TimeSpan.Zero)
        {
            AddToast("Interwał timera musi być większy od zera.", "error");
            return;
        }

        if (entry.GetCommands().Count == 0)
        {
            AddToast("Timer musi mieć przynajmniej jedną komendę.", "error");
            return;
        }

        Timers.Add(entry);

        NewTimerName = string.Empty;
        NewTimerMinutes = "0";
        NewTimerSeconds = "0";
        NewTimerMilliseconds = "0";
        NewTimerCommands = string.Empty;

        SaveActiveProfile();
    }

    private void DeleteTimer(TimerEntry? entry)
    {
        if (entry is null)
        {
            return;
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

        var commands = entry.GetCommands();
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
    // Profiles
    // ========================================================================

    public ObservableCollection<string> AvailableProfiles { get; } = [];

    public bool HasProfiles => AvailableProfiles.Count > 0;

    public RelayCommand SelectProfileCommand { get; }
    public RelayCommand CreateProfileCommand { get; }
    public RelayCommand SwitchProfileCommand { get; }

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
        ActivateProfile(profile);
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
    }

    private void ActivateProfile(ProfileData profile)
    {
        Notes.Clear();
        foreach (var note in profile.Notes)
        {
            Notes.Add(new NoteEntry
            {
                Title = note.Title,
                Content = note.Content,
                CreatedAt = note.CreatedAt,
            });
        }

        AutomationRules.Clear();
        foreach (var rule in profile.Rules)
        {
            AutomationRules.Add(new AutomationRuleEntry(
                rule.Name, rule.Type, rule.Pattern, rule.Action, rule.IsEnabled));
        }

        Timers.Clear();
        foreach (var timer in profile.Timers)
        {
            Timers.Add(new TimerEntry
            {
                Id = string.IsNullOrWhiteSpace(timer.Id) ? Guid.NewGuid().ToString("N") : timer.Id,
                Name = timer.Name,
                Minutes = timer.Minutes,
                Seconds = timer.Seconds,
                Milliseconds = timer.Milliseconds,
                CommandsText = string.Join(Environment.NewLine, timer.Commands),
                IsEnabled = timer.IsEnabled,
            });
        }

        ActiveProfileName = profile.Name;
        ApplyAutomation();
        _timers.CancelAll();
        SyncAllTimers();
        AddToast($"Profil „{profile.Name}” aktywny.", "info");
        ProfileActivated?.Invoke(profile.Name);
    }

    private void SaveActiveProfile()
    {
        if (ActiveProfileName is null)
        {
            return;
        }

        var profile = new ProfileData
        {
            Name = ActiveProfileName,
            Notes = Notes
                .Select(n => new ProfileNote { Title = n.Title, Content = n.Content, CreatedAt = n.CreatedAt })
                .ToList(),
            Rules = AutomationRules
                .Select(r => new ProfileRule
                {
                    Name = r.Name,
                    Type = r.Type,
                    Pattern = r.Pattern,
                    Action = r.Action,
                    IsEnabled = r.IsEnabled,
                })
                .ToList(),
            Timers = Timers
                .Select(t => new ProfileTimer
                {
                    Id = t.Id,
                    Name = t.Name,
                    Minutes = t.Minutes,
                    Seconds = t.Seconds,
                    Milliseconds = t.Milliseconds,
                    Commands = t.GetCommands().ToList(),
                    IsEnabled = t.IsEnabled,
                })
                .ToList(),
        };

        try
        {
            _profiles.Save(profile);
        }
        catch (Exception exception)
        {
            AddToast($"Nie udało się zapisać profilu: {exception.Message}", "error");
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

    // --- Quick commands (chips) ---
    public ObservableCollection<QuickCommand> QuickCommands { get; } = [];
    public IRelayCommand<string> QuickCommandExecuteCommand { get; }

    // --- Log filter tabs ---
    public ObservableCollection<LogFilter> LogFilters { get; } = [];

    // --- Character vitals (mock) ---
    public CharacterVitals Vitals { get; } = new();

    // --- Status effects (mock) ---
    public ObservableCollection<StatusEffect> Effects { get; } = [];

    // --- People in room (mock) ---
    public ObservableCollection<PersonEntry> People { get; } = [];

    // --- Group members (mock) ---
    public ObservableCollection<GroupMember> Group { get; } = [];

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
        var command = _aliases.Process(sourceCommand);

        // Track history
        CommandHistory.Insert(0, command);
        while (CommandHistory.Count > CommandHistoryMaxSize)
        {
            CommandHistory.RemoveAt(CommandHistory.Count - 1);
        }

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

    // ========================================================================
    // New command implementations
    // ========================================================================

    private void ExecuteQuickCommand(string? command)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            return;
        }

        if (IsConnected)
        {
            CommandText = command;
            if (CanSendCommand())
            {
                _ = SendCurrentCommandAsync();
            }
        }
        else
        {
            // When not connected, just set the text for convenience.
            CommandText = command;
        }
    }

    private void AddNote()
    {
        if (string.IsNullOrWhiteSpace(NewNoteTitle))
        {
            return;
        }

        Notes.Insert(0, new NoteEntry
        {
            Title = NewNoteTitle,
            Content = NewNoteContent,
            CreatedAt = DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm"),
        });

        NewNoteTitle = string.Empty;
        NewNoteContent = string.Empty;
        SaveActiveProfile();
    }

    private void DeleteNote(NoteEntry? note)
    {
        if (note is not null)
        {
            Notes.Remove(note);
            SaveActiveProfile();
        }
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
        StartupErrorMessage = "Nie udało się uruchomić interfejsu.";
        StartupErrorDetails = exception.Message;
        AddToast("Wystąpił błąd uruchamiania interfejsu.", "error");
        EmitSystem(exception.Message, 31);
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
        foreach (var command in _triggers.Evaluate(line))
        {
            _ = SendTriggeredCommandAsync(command);
        }
    }

    private async Task SendTriggeredCommandAsync(string command)
    {
        try
        {
            await _session.SendCommandAsync(command);
        }
        catch (Exception exception)
        {
            Dispatcher.UIThread.Post(() => EmitSystem(exception.Message, 31));
        }
    }

    private void OnGmcpReceived(GmcpMessage message)
    {
        _locationResolver.Process(message);

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
            IsConnected = _session.IsConnected;
        });
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
        // Quick commands
        foreach (var cmd in new[]
        {
            new QuickCommand("spojrzyj", "look"),
            new QuickCommand("ekwipunek", "inventory"),
            new QuickCommand("statystyki", "score"),
            new QuickCommand("sprzęt", "equipment"),
            new QuickCommand("kto", "who"),
            new QuickCommand("północ", "north"),
            new QuickCommand("południe", "south"),
            new QuickCommand("wschód", "east"),
            new QuickCommand("zachód", "west"),
            new QuickCommand("góra", "up"),
            new QuickCommand("dół", "down"),
            new QuickCommand("pomoc", "help"),
        })
        {
            QuickCommands.Add(cmd);
        }

        // Log filter tabs
        foreach (var filter in Models.LogFilters.Defaults)
        {
            LogFilters.Add(filter);
        }

        // Status effects (mock)
        Effects.Add(new StatusEffect("Błogosławieństwo", "[+]", "12 min", false));
        Effects.Add(new StatusEffect("Kamienna skóra", "[+]", "8 min", false));
        Effects.Add(new StatusEffect("Zatrucie", "[-]", "3 min", true));

        // People in room (mock)
        People.Add(new PersonEntry("Strażnik miasta", "(NPC)", false));
        People.Add(new PersonEntry("Stary mag", "(NPC)", false));

        // Group members (mock)
        Group.Add(new GroupMember("Ty", "*", 100, 80, 100));
        Group.Add(new GroupMember("Aelindra", " ", 85, 60, 92));

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

        _session.TextReceived -= OnTextReceived;
        _session.LineReceived -= OnLineReceived;
        _session.GmcpReceived -= OnGmcpReceived;
        _session.GmcpSent -= OnGmcpSent;
        _session.StatusChanged -= OnStatusChanged;
        _session.ConnectionError -= OnConnectionError;

        await _timers.DisposeAsync();
        await _session.DisposeAsync();
        Map.Dispose();
    }
}

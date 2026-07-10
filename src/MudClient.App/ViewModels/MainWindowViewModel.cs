using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MudClient.App.Models;
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

    public MainWindowViewModel()
    {
        _connectCommand = new AsyncRelayCommand(ConnectAsync, CanConnect);
        _disconnectCommand = new AsyncRelayCommand(DisconnectAsync, CanDisconnect);
        _sendCommandCommand = new AsyncRelayCommand(SendCurrentCommandAsync, CanSendCommand);
        _retryStartupCommand = new AsyncRelayCommand(RetryStartupAsync);
        QuickCommandExecuteCommand = new RelayCommand<string>(ExecuteQuickCommand);

        _session.TextReceived += OnTextReceived;
        _session.LineReceived += OnLineReceived;
        _session.GmcpReceived += OnGmcpReceived;
        _session.StatusChanged += OnStatusChanged;
        _session.ConnectionError += OnConnectionError;

        // Demonstracyjny alias. Usuń go, gdy powstanie edytor aliasów.
        _aliases.Add(new AliasRule("krótkie-look", "^l$", "look"));

        Map = new MapViewModel(AppContext.BaseDirectory, _locationResolver);

        PopulateMockData();
    }

    public MapViewModel Map { get; }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        ClearStartupError();
        await Map.InitializeAsync(cancellationToken);
    }

    public event Action<string>? OutputReceived;

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

    // --- Command history ---
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
        while (CommandHistory.Count > 100)
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
    }

    private void DeleteNote(NoteEntry? note)
    {
        if (note is not null)
        {
            Notes.Remove(note);
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
        var toast = new ToastMessage { Text = text, Type = type };
        Toasts.Insert(0, toast);
        while (Toasts.Count > 10)
        {
            Toasts.RemoveAt(Toasts.Count - 1);
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

        // Automation rules (mock)
        AutomationRules.Add(new AutomationRuleEntry("Skrót look", "alias", "^l$", "look", true));
        AutomationRules.Add(new AutomationRuleEntry("Automatyczne podnoszenie", "trigger", "leży na ziemi", "wez wszystko", true));
        AutomationRules.Add(new AutomationRuleEntry("Leczenie co 30s", "timer", "co 30s", "rzuc 'leczenie'", false));

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
        AddToast("Witaj w MudClient! Wpisz host i port, a następnie kliknij Połącz.", "info");
    }

    // ========================================================================
    // Dispose
    // ========================================================================

    public async ValueTask DisposeAsync()
    {
        _session.TextReceived -= OnTextReceived;
        _session.LineReceived -= OnLineReceived;
        _session.GmcpReceived -= OnGmcpReceived;
        _session.StatusChanged -= OnStatusChanged;
        _session.ConnectionError -= OnConnectionError;

        await _timers.DisposeAsync();
        await _session.DisposeAsync();
        Map.Dispose();
    }
}

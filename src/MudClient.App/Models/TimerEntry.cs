using CommunityToolkit.Mvvm.ComponentModel;

namespace MudClient.App.Models;

/// <summary>
/// Editable timer shown in the UI. Interval = Minutes + Seconds + Milliseconds;
/// while enabled it fires repeatedly and sends its commands in order.
/// </summary>
public sealed class TimerEntry : ObservableObject, IFolderItem
{
    private string _name = string.Empty;
    private int _minutes;
    private int _seconds;
    private int _milliseconds;
    private string _commandsText = string.Empty;
    private bool _isEnabled;
    private bool _isGlobal;
    private string? _folderId;

    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public int Minutes
    {
        get => _minutes;
        set => SetProperty(ref _minutes, value);
    }

    public int Seconds
    {
        get => _seconds;
        set => SetProperty(ref _seconds, value);
    }

    public int Milliseconds
    {
        get => _milliseconds;
        set => SetProperty(ref _milliseconds, value);
    }

    /// <summary>One command per line; sent top-to-bottom on every tick.</summary>
    public string CommandsText
    {
        get => _commandsText;
        set => SetProperty(ref _commandsText, value);
    }

    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (SetProperty(ref _isEnabled, value))
            {
                OnPropertyChanged(nameof(StatusText));
            }
        }
    }

    public string StatusText => IsEnabled ? "WŁĄCZONY" : "WYŁĄCZONY";

    /// <summary>True = shared by all profiles (stored in the global file).</summary>
    public bool IsGlobal
    {
        get => _isGlobal;
        set => SetProperty(ref _isGlobal, value);
    }

    /// <summary>Id of the containing folder, or null when loose.</summary>
    public string? FolderId
    {
        get => _folderId;
        set => SetProperty(ref _folderId, value);
    }

    public TimeSpan Interval =>
        TimeSpan.FromMinutes(Minutes) +
        TimeSpan.FromSeconds(Seconds) +
        TimeSpan.FromMilliseconds(Milliseconds);

    public string IntervalText
    {
        get
        {
            var parts = new List<string>();
            if (Minutes > 0) parts.Add($"{Minutes} min");
            if (Seconds > 0) parts.Add($"{Seconds} s");
            if (Milliseconds > 0) parts.Add($"{Milliseconds} ms");
            return parts.Count > 0 ? string.Join(" ", parts) : "brak interwału";
        }
    }

    /// <summary>
    /// Returns the timer's commands split on newlines only (no stacking separator).
    /// </summary>
    public IReadOnlyList<string> GetCommands() => GetCommands(separator: null);

    /// <summary>
    /// Returns the timer's commands split on newlines and, when
    /// <paramref name="separator"/> is non-empty, also on that separator.
    /// </summary>
    public IReadOnlyList<string> GetCommands(string? separator) =>
        MudClient.Core.Automation.CommandStacker.Split(CommandsText, separator);
}

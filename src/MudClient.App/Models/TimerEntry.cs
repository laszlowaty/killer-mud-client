using CommunityToolkit.Mvvm.ComponentModel;

namespace MudClient.App.Models;

/// <summary>
/// Editable timer shown in the UI. Interval = Minutes + Seconds + Milliseconds;
/// while enabled it fires repeatedly and sends its commands in order.
/// </summary>
public sealed class TimerEntry : ObservableObject, IActivatableFolderItem
{
    private string _name = string.Empty;
    private int _minutes;
    private int _seconds;
    private int _milliseconds;
    private string _commandsText = string.Empty;
    private bool _isEnabled;
    private bool _isGlobal;
    private string? _folderId;
    private DateTimeOffset? _nextActivationAt;
    private string _remainingText = string.Empty;

    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    public string Name
    {
        get => _name;
        set
        {
            if (SetProperty(ref _name, value))
            {
                OnPropertyChanged(nameof(ShortName));
            }
        }
    }

    /// <summary>Compact timer name used by the terminal countdown overlay.</summary>
    public string ShortName
    {
        get
        {
            const int maximumLength = 12;
            var trimmed = Name.Trim();
            return trimmed.Length <= maximumLength
                ? trimmed
                : $"{trimmed[..(maximumLength - 1)]}…";
        }
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

    /// <summary>Formatted time remaining until the next activation.</summary>
    public string RemainingText
    {
        get => _remainingText;
        private set => SetProperty(ref _remainingText, value);
    }

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

    internal void ScheduleNextActivation(DateTimeOffset activationAt, DateTimeOffset now)
    {
        _nextActivationAt = activationAt;
        RefreshCountdown(now);
    }

    internal void ClearNextActivation()
    {
        _nextActivationAt = null;
        RemainingText = string.Empty;
    }

    internal void RefreshCountdown(DateTimeOffset now)
    {
        if (!IsEnabled || _nextActivationAt is not { } activationAt)
        {
            RemainingText = string.Empty;
            return;
        }

        RemainingText = FormatRemaining(activationAt - now);
    }

    internal static string FormatRemaining(TimeSpan remaining)
    {
        var totalMilliseconds = Math.Max(0, remaining.TotalMilliseconds);
        if (totalMilliseconds < 10_000)
        {
            var tenths = Math.Ceiling(totalMilliseconds / 100);
            return $"{tenths / 10:0.0} s";
        }

        var totalSeconds = (long)Math.Ceiling(totalMilliseconds / 1000);
        if (totalSeconds < 3600)
        {
            return $"{totalSeconds / 60:00}:{totalSeconds % 60:00}";
        }

        return $"{totalSeconds / 3600}:{totalSeconds / 60 % 60:00}:{totalSeconds % 60:00}";
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

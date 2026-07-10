namespace MudClient.App.Models;

/// <summary>
/// A log tab filter. Only the label and key are needed for the mock UI.
/// Real filtering logic can be added later.
/// </summary>
public sealed record LogFilter(string Label, string Key);

/// <summary>
/// Provides the default set of log filter tabs.
/// </summary>
public static class LogFilters
{
    public static IReadOnlyList<LogFilter> Defaults { get; } =
    [
        new("Wszystko", "all"),
        new("Walka", "combat"),
        new("Czaty", "chat"),
        new("System", "system"),
    ];
}

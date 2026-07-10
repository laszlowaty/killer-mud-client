namespace MudClient.App.Models;

/// <summary>
/// Mock automation rule for the UI. Represents an alias, trigger, or timer.
/// </summary>
public sealed record AutomationRuleEntry(
    string Name,
    string Type,     // "alias", "trigger", "timer"
    string Pattern,
    string Action,
    bool IsEnabled);

using MudClient.Core.Gmcp;

namespace MudClient.App.Models;

/// <summary>
/// Status effect (buff/debuff) displayed on the character.
/// Populated live from Char.Affects GMCP.
/// </summary>
public sealed record StatusEffect(
    string Name,
    string Icon,
    string Duration,
    bool IsDebuff,
    string Description,
    bool Negative,
    bool Ending,
    string? ExtraValue)
{
    /// <summary>True when Description is non-empty, for UI visibility bindings.</summary>
    public bool HasDescription => !string.IsNullOrWhiteSpace(Description);

    /// <summary>True when Duration is non-empty, for UI visibility bindings.</summary>
    public bool HasDuration => !string.IsNullOrWhiteSpace(Duration);

    /// <summary>Creates an app model from a core affect.</summary>
    public static StatusEffect FromCore(CharacterAffect affect) => new(
        Name: affect.Name,
        Icon: affect.Ending ? "[!]" : (affect.Negative ? "[-]" : "[+]"),
        Duration: affect.ExtraValue ?? string.Empty,
        IsDebuff: affect.Negative,
        Description: affect.Description,
        Negative: affect.Negative,
        Ending: affect.Ending,
        ExtraValue: affect.ExtraValue);
}

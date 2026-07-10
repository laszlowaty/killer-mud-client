namespace MudClient.App.Models;

/// <summary>
/// Mock status effect (buff/debuff) displayed on the character.
/// </summary>
public sealed record StatusEffect(string Name, string Icon, string Duration, bool IsDebuff);

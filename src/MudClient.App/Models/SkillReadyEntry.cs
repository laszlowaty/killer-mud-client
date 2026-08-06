namespace MudClient.App.Models;

/// <summary>A transient notice that a skill has come off cooldown and can be used again.</summary>
public sealed record SkillReadyEntry(string Name);

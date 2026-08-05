namespace MudClient.Core.Scripting;

public enum ScriptEffectKind
{
    Execute,
    Send,
    Echo,
    Log,
}

/// <summary>
/// A side effect requested by JavaScript. The interpreter never performs
/// network or UI work directly; the application dispatches these effects.
/// </summary>
public sealed record ScriptEffect(
    ScriptEffectKind Kind,
    string Text,
    string? Color = null);

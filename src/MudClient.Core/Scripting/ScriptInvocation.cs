namespace MudClient.Core.Scripting;

public sealed record ScriptMatchContext(
    string Value,
    IReadOnlyList<string> Captures,
    IReadOnlyDictionary<string, string> Groups);

public sealed record ScriptGmcpContext(
    string Package,
    string Json);

/// <summary>Immutable input exposed to one JavaScript execution.</summary>
public sealed record ScriptInvocation(
    string Name,
    string Source,
    string Code,
    string? Input = null,
    ScriptMatchContext? Match = null,
    ScriptGmcpContext? Gmcp = null,
    IReadOnlyList<string>? CommandHistory = null);

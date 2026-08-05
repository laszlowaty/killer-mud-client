namespace MudClient.Core.Scripting;

public sealed record ScriptExecutionResult(
    IReadOnlyList<ScriptEffect> Effects,
    string? Error = null)
{
    public bool Success => Error is null;
}

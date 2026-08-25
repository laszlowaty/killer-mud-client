namespace MudClient.Core.Scripting;

/// <summary>
/// Executes the deliberately bounded HTTP requests exposed to user JavaScript.
/// The interpreter receives only this narrow capability and never CLR access.
/// </summary>
public interface IScriptHttpClient
{
    Task<ScriptHttpResponse> SendAsync(
        ScriptHttpRequest request,
        CancellationToken cancellationToken);
}

public sealed record ScriptHttpRequest(
    string Method,
    string Url,
    IReadOnlyDictionary<string, string> Headers,
    string? Body,
    int TimeoutMilliseconds);

public sealed record ScriptHttpResponse(
    int Status,
    string Reason,
    string Url,
    IReadOnlyDictionary<string, string> Headers,
    string Text);

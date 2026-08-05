namespace MudClient.Core.Scripting;

/// <summary>
/// Profile-scoped JSON variable storage. Values cross the script boundary as
/// JSON so scripts cannot receive arbitrary CLR objects.
/// </summary>
public interface IScriptVariableStore
{
    string? GetJson(string name);

    void SetJson(string name, string json);

    bool Contains(string name);

    bool Remove(string name);

    double Increment(string name, double amount);
}

namespace MudClient.App.Models;

public interface IScriptErrorSource
{
    string LastError { get; set; }

    bool HasLastError { get; }
}

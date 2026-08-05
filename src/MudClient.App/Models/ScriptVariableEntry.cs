using CommunityToolkit.Mvvm.ComponentModel;

namespace MudClient.App.Models;

public sealed class ScriptVariableEntry : ObservableObject
{
    private string _valueJson = "null";

    public required string Name { get; init; }

    public string ValueJson
    {
        get => _valueJson;
        set => SetProperty(ref _valueJson, value);
    }
}

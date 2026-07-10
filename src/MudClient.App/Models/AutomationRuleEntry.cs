using CommunityToolkit.Mvvm.ComponentModel;

namespace MudClient.App.Models;

/// <summary>
/// Alias or trigger shown in the UI. Pattern is a .NET regex; Action may use
/// capture-group substitutions like $1.
/// </summary>
public sealed class AutomationRuleEntry : ObservableObject
{
    private string _name;
    private string _type;
    private string _pattern;
    private string _action;
    private bool _isEnabled;

    public AutomationRuleEntry(string name, string type, string pattern, string action, bool isEnabled)
    {
        _name = name;
        _type = type;
        _pattern = pattern;
        _action = action;
        _isEnabled = isEnabled;
    }

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    /// <summary>"alias" or "trigger" ("timer" kept for legacy profiles).</summary>
    public string Type
    {
        get => _type;
        set => SetProperty(ref _type, value);
    }

    public string Pattern
    {
        get => _pattern;
        set => SetProperty(ref _pattern, value);
    }

    public string Action
    {
        get => _action;
        set => SetProperty(ref _action, value);
    }

    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (SetProperty(ref _isEnabled, value))
            {
                OnPropertyChanged(nameof(StatusText));
            }
        }
    }

    public string StatusText => IsEnabled ? "WŁĄCZONY" : "WYŁĄCZONY";
}

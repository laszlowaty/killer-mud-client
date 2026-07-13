using CommunityToolkit.Mvvm.ComponentModel;

namespace MudClient.App.Models;

/// <summary>
/// Alias or trigger shown in the UI. Pattern is a .NET regex; Action may use
/// capture-group substitutions like $1.
/// </summary>
public sealed class AutomationRuleEntry : ObservableObject, IFolderItem
{
    private string _name;
    private string _type;
    private string _pattern;
    private string _action;
    private bool _isEnabled;
    private bool _isGlobal;
    private string? _folderId;

    public AutomationRuleEntry(string name, string type, string pattern, string action, bool isEnabled, bool isGlobal = false)
    {
        _name = name;
        _type = type;
        _pattern = pattern;
        _action = action;
        _isEnabled = isEnabled;
        _isGlobal = isGlobal;
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

    /// <summary>True = shared by all profiles (stored in the global file).</summary>
    public bool IsGlobal
    {
        get => _isGlobal;
        set => SetProperty(ref _isGlobal, value);
    }

    /// <summary>Id of the containing folder, or null when loose.</summary>
    public string? FolderId
    {
        get => _folderId;
        set => SetProperty(ref _folderId, value);
    }
}

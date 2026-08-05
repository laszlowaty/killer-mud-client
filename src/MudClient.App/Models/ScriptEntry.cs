using CommunityToolkit.Mvvm.ComponentModel;

namespace MudClient.App.Models;

public sealed class ScriptEntry : ObservableObject, IActivatableFolderItem, IScriptErrorSource
{
    private string _name = string.Empty;
    private string _code = string.Empty;
    private string _gmcpPattern = string.Empty;
    private bool _isEnabled = true;
    private bool _isGlobal;
    private string? _folderId;
    private string _lastError = string.Empty;

    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public string Code
    {
        get => _code;
        set => SetProperty(ref _code, value);
    }

    public string GmcpPattern
    {
        get => _gmcpPattern;
        set => SetProperty(ref _gmcpPattern, value);
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

    public bool IsGlobal
    {
        get => _isGlobal;
        set => SetProperty(ref _isGlobal, value);
    }

    public string? FolderId
    {
        get => _folderId;
        set => SetProperty(ref _folderId, value);
    }

    public string LastError
    {
        get => _lastError;
        set
        {
            if (SetProperty(ref _lastError, value))
            {
                OnPropertyChanged(nameof(HasLastError));
            }
        }
    }

    public bool HasLastError => !string.IsNullOrWhiteSpace(LastError);
}

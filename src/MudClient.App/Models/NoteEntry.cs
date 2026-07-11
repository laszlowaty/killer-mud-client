using CommunityToolkit.Mvvm.ComponentModel;

namespace MudClient.App.Models;

/// <summary>
/// A user note.
/// </summary>
public sealed partial class NoteEntry : ObservableObject
{
    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _content = string.Empty;

    [ObservableProperty]
    private string _createdAt = string.Empty;

    /// <summary>True = shared by all profiles (stored in the global file).</summary>
    [ObservableProperty]
    private bool _isGlobal;
}

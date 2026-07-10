using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MudClient.App.Models;

/// <summary>
/// A transient toast notification message.
/// </summary>
public sealed partial class ToastMessage : ObservableObject
{
    [ObservableProperty]
    private string _text = string.Empty;

    [ObservableProperty]
    private string _type = "info"; // "info", "warning", "error"

    [ObservableProperty]
    private bool _isVisible = true;

    public string IconGlyph => Type switch
    {
        "warning" => "[!]",
        "error" => "[X]",
        _ => "[i]",
    };

    public IBrush ForegroundBrush => Type switch
    {
        "warning" => Brushes.Orange,
        "error" => Brushes.OrangeRed,
        _ => Brushes.CornflowerBlue,
    };
}

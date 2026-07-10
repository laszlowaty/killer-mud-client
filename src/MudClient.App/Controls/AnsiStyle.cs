using Avalonia.Media;

namespace MudClient.App.Controls;

internal readonly record struct AnsiStyle(
    Color? Foreground,
    Color? Background,
    bool Bold,
    bool Underline);

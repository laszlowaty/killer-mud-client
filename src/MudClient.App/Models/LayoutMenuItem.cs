namespace MudClient.App.Models;

/// <summary>One entry in the "Układ" menu: a layout name plus whether it can be deleted
/// (the built-in DEFAULT cannot).</summary>
public sealed class LayoutMenuItem
{
    public string Name { get; init; } = string.Empty;

    public bool CanDelete { get; init; }
}

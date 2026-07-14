using MudClient.App.Controls;

namespace MudClient.App.ViewModels;

public sealed record MapDisplayModeOption(MapDisplayMode Mode, string Label)
{
    public static readonly IReadOnlyList<MapDisplayModeOption> All =
    [
        new(MapDisplayMode.Standard, "Domyślna"),
        new(MapDisplayMode.Terrain, "Teren (bez tła)"),
        new(MapDisplayMode.Simple, "Prosta"),
    ];

    public override string ToString() => Label;
}

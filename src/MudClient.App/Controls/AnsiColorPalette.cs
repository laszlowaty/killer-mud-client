using Avalonia.Media;

namespace MudClient.App.Controls;

internal sealed record AnsiColorPalette(Color[] Normal, Color[] Bright)
{
    public const string Warm = "Ciepłe";
    public const string Colorblind = "Colorblind";
    public const string Vivid = "Jaskrawe";

    public static IReadOnlyList<string> Names { get; } =
        [Warm, Colorblind, Vivid];

    public static bool IsKnown(string? name) => Names.Contains(name, StringComparer.Ordinal);

    public static AnsiColorPalette FromName(string? name) => name switch
    {
        Colorblind => Create(
            ["#787878", "#808080", "#888888", "#909090", "#989898", "#A0A0A0", "#A8A8A8", "#B0B0B0"],
            ["#B8B8B8", "#C0C0C0", "#C8C8C8", "#D0D0D0", "#D8D8D8", "#E0E0E0", "#E8E8E8", "#FFFFFF"]),
        Vivid => Create(
            ["#777777", "#FF0033", "#00FF44", "#FFE600", "#0066FF", "#D900FF", "#00E5FF", "#FFFFFF"],
            ["#999999", "#FF0000", "#00FF00", "#FFFF00", "#0080FF", "#FF00FF", "#00FFFF", "#FFFFFF"]),
        _ => Create(
            ["#505050", "#CD3131", "#0DBC79", "#E5E510", "#2472C8", "#BC3FBC", "#11A8CD", "#E5E5E5"],
            ["#666666", "#F14C4C", "#23D18B", "#F5F543", "#3B8EEA", "#D670D6", "#29B8DB", "#FFFFFF"]),
    };

    private static AnsiColorPalette Create(string[] normal, string[] bright) =>
        new(normal.Select(Color.Parse).ToArray(), bright.Select(Color.Parse).ToArray());
}

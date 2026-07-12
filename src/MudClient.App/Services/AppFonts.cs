using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace MudClient.App.Services;

/// <summary>Application-bundled fonts and conversion from persisted display names.</summary>
public static class AppFonts
{
    public const string OpenDyslexicName = "OpenDyslexic";

    private const string OpenDyslexicUri =
        "avares://MudClient.App/Assets/Fonts/OpenDyslexic#OpenDyslexic";

    public static FontFamily Resolve(string? name) =>
        string.Equals(name, OpenDyslexicName, StringComparison.OrdinalIgnoreCase)
            ? new FontFamily(OpenDyslexicUri)
            : new FontFamily(string.IsNullOrWhiteSpace(name) ? "Inter" : name);
}

public sealed class AppFontFamilyConverter : IValueConverter
{
    public static AppFontFamilyConverter Instance { get; } = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        AppFonts.Resolve(value as string);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

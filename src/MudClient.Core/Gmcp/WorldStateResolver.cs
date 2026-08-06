using System.Text.Json;

namespace MudClient.Core.Gmcp;

/// <summary>Fields from Mud.TimeInfo; null when the message did not include them.</summary>
public sealed record WorldTimeUpdate(
    int? Day,
    string? DayName,
    string? Era,
    string? Month,
    int? Time,
    string? TimeName,
    int? Year);

/// <summary>Fields from Mud.Weather; null when the message did not include them.</summary>
public sealed record WorldWeatherUpdate(
    string? Sky,
    string? Wind);

/// <summary>
/// Translates Mud.TimeInfo / Mud.Weather GMCP messages into typed updates.
/// Malformed or unknown messages are ignored.
/// </summary>
public sealed class WorldStateResolver
{
    public event Action<WorldTimeUpdate>? TimeChanged;

    public event Action<WorldWeatherUpdate>? WeatherChanged;

    public void Process(GmcpMessage message)
    {
        if (string.IsNullOrWhiteSpace(message.Json))
        {
            return;
        }

        var isTime = string.Equals(message.Package, "Mud.TimeInfo", StringComparison.OrdinalIgnoreCase);
        var isWeather = string.Equals(message.Package, "Mud.Weather", StringComparison.OrdinalIgnoreCase);
        if (!isTime && !isWeather)
        {
            return;
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(message.Json);
        }
        catch (JsonException)
        {
            return;
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return;
            }

            if (isTime)
            {
                TimeChanged?.Invoke(new WorldTimeUpdate(
                    Day: GetInt(root, "day"),
                    DayName: GetString(root, "dayname"),
                    Era: GetString(root, "era"),
                    Month: GetString(root, "month"),
                    Time: GetInt(root, "time"),
                    TimeName: GetString(root, "timename"),
                    Year: GetInt(root, "year")));
            }
            else
            {
                WeatherChanged?.Invoke(new WorldWeatherUpdate(
                    Sky: GetString(root, "sky"),
                    Wind: GetString(root, "wind")));
            }
        }
    }

    private static int? GetInt(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.Number
        && value.TryGetInt32(out var intValue)
            ? intValue
            : null;

    private static string? GetString(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}

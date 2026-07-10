using System.Text.Json;
using System.Text.Json.Serialization;

namespace MudClient.Core.Map;

internal sealed class RawMapDocument
{
    public string? AnonymousAreaName { get; set; }

    public int AreaCount { get; set; }

    public List<RawMapArea>? Areas { get; set; }
}

internal sealed class RawMapArea
{
    public int Id { get; set; }

    public string? Name { get; set; }

    public int RoomCount { get; set; }

    public List<RawMapRoom>? Rooms { get; set; }
}

internal sealed class RawMapRoom
{
    public int Id { get; set; }

    [JsonConverter(typeof(JsonStringOrNullConverter))]
    public string? Name { get; set; }

    public List<double>? Coordinates { get; set; }

    [JsonConverter(typeof(JsonFlexibleIntConverter))]
    public int? Environment { get; set; }

    public List<RawMapExit>? Exits { get; set; }

    public Dictionary<string, JsonElement>? UserData { get; set; }

    [JsonConverter(typeof(JsonStringOrNullConverter))]
    public string? Symbol { get; set; }

    public double? Weight { get; set; }
}

internal sealed class RawMapExit
{
    public int ExitId { get; set; }

    public string? Name { get; set; }

    [JsonConverter(typeof(JsonStringOrNullConverter))]
    public string? Door { get; set; }
}

internal sealed class JsonStringOrNullConverter : JsonConverter<string?>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var element = document.RootElement;

        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Null => null,
            JsonValueKind.Undefined => null,
            _ => element.GetRawText(),
        };
    }

    public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
        }
        else
        {
            writer.WriteStringValue(value);
        }
    }
}

internal sealed class JsonFlexibleIntConverter : JsonConverter<int?>
{
    public override int? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Number:
                return reader.TryGetInt32(out var intValue) ? intValue : (int)reader.GetDouble();

            case JsonTokenType.String:
                var text = reader.GetString();
                return int.TryParse(text, out var parsed) ? parsed : null;

            default:
                return null;
        }
    }

    public override void Write(Utf8JsonWriter writer, int? value, JsonSerializerOptions options)
    {
        if (value.HasValue)
        {
            writer.WriteNumberValue(value.Value);
        }
        else
        {
            writer.WriteNullValue();
        }
    }
}

namespace MudClient.Core.Map;

public sealed class MapLabel
{
    public required int Id { get; init; }

    public required int AreaId { get; init; }

    public required string Text { get; init; }

    public required MapCoordinates Coordinates { get; init; }

    public double? FontSize { get; init; }

    public bool ShowOnTop { get; init; }
}

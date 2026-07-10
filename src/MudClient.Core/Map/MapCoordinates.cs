namespace MudClient.Core.Map;

public readonly record struct MapCoordinates(double X, double Y, double Z)
{
    public static bool TryCreate(IReadOnlyList<double>? values, out MapCoordinates coordinates)
    {
        if (values is { Count: >= 3 })
        {
            coordinates = new MapCoordinates(values[0], values[1], values[2]);
            return true;
        }

        coordinates = default;
        return false;
    }
}

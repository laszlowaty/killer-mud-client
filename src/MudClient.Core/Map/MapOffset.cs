namespace MudClient.Core.Map;

public readonly record struct MapOffset(double X, double Y)
{
    public static readonly MapOffset Zero = new(0, 0);
}

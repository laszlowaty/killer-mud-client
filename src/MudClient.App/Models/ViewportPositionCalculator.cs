namespace MudClient.App.Models;

public static class ViewportPositionCalculator
{
    public static double ClampOrCenter(
        double requestedPosition,
        double minimumPosition,
        double maximumPosition)
    {
        if (minimumPosition <= maximumPosition)
        {
            return Math.Clamp(
                requestedPosition,
                minimumPosition,
                maximumPosition);
        }

        return (minimumPosition + maximumPosition) / 2;
    }
}

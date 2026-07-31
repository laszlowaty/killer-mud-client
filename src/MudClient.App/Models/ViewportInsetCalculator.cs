namespace MudClient.App.Models;

public static class ViewportInsetCalculator
{
    public static double CalculateMissingBottomInset(
        double viewportHeightWithoutInset,
        double currentViewportHeight,
        double requestedInset)
    {
        if (viewportHeightWithoutInset <= 0
            || currentViewportHeight <= 0
            || requestedInset <= 0)
        {
            return 0;
        }

        var nativeReduction = Math.Max(
            0,
            viewportHeightWithoutInset - currentViewportHeight);
        return Math.Max(0, requestedInset - nativeReduction);
    }
}

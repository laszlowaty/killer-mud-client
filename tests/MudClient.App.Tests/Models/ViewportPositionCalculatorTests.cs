using MudClient.App.Models;

namespace MudClient.App.Tests.Models;

public sealed class ViewportPositionCalculatorTests
{
    [Theory]
    [InlineData(-20, -10, 10, -10)]
    [InlineData(5, -10, 10, 5)]
    [InlineData(20, -10, 10, 10)]
    public void AvailableRange_ClampsRequestedPosition(
        double requested,
        double minimum,
        double maximum,
        double expected)
    {
        var result = ViewportPositionCalculator.ClampOrCenter(
            requested,
            minimum,
            maximum);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void ViewportSmallerThanControl_CentersOverflow()
    {
        var result = ViewportPositionCalculator.ClampOrCenter(
            requestedPosition: 0,
            minimumPosition: -196,
            maximumPosition: -409);

        Assert.Equal(-302.5, result);
    }
}

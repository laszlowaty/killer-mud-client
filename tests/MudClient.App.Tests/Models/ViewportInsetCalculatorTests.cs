using MudClient.App.Models;

namespace MudClient.App.Tests.Models;

public sealed class ViewportInsetCalculatorTests
{
    [Fact]
    public void NativeResizeCoveringIme_DoesNotAddFallback()
    {
        var result = ViewportInsetCalculator.CalculateMissingBottomInset(
            viewportHeightWithoutInset: 800,
            currentViewportHeight: 500,
            requestedInset: 300);

        Assert.Equal(0, result);
    }

    [Fact]
    public void MissingNativeResize_AddsImeInset()
    {
        var result = ViewportInsetCalculator.CalculateMissingBottomInset(
            viewportHeightWithoutInset: 800,
            currentViewportHeight: 800,
            requestedInset: 300);

        Assert.Equal(300, result);
    }

    [Fact]
    public void Recalculation_ReturnsTheSameMissingInset()
    {
        var first = ViewportInsetCalculator.CalculateMissingBottomInset(
            viewportHeightWithoutInset: 800,
            currentViewportHeight: 800,
            requestedInset: 300);
        var second = ViewportInsetCalculator.CalculateMissingBottomInset(
            viewportHeightWithoutInset: 800,
            currentViewportHeight: 800,
            requestedInset: 300);

        Assert.Equal(first, second);
    }

    [Fact]
    public void PartialNativeResize_AddsOnlyMissingInset()
    {
        var result = ViewportInsetCalculator.CalculateMissingBottomInset(
            viewportHeightWithoutInset: 800,
            currentViewportHeight: 650,
            requestedInset: 300);

        Assert.Equal(150, result);
    }
}

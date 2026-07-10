using Avalonia.Media;
using MudClient.App.Models;

namespace MudClient.App.Tests.Models;

public sealed class ToastMessageTests
{
    [Fact]
    public void DefaultTypeIsInfo()
    {
        var toast = new ToastMessage();
        Assert.Equal("info", toast.Type);
    }

    [Fact]
    public void InfoType_ReturnsCorrectIconAndBrush()
    {
        var toast = new ToastMessage { Type = "info" };
        Assert.Equal("[i]", toast.IconGlyph);
        Assert.Equal(Brushes.CornflowerBlue, toast.ForegroundBrush);
    }

    [Fact]
    public void WarningType_ReturnsCorrectIconAndBrush()
    {
        var toast = new ToastMessage { Type = "warning" };
        Assert.Equal("[!]", toast.IconGlyph);
        Assert.Equal(Brushes.Orange, toast.ForegroundBrush);
    }

    [Fact]
    public void ErrorType_ReturnsCorrectIconAndBrush()
    {
        var toast = new ToastMessage { Type = "error" };
        Assert.Equal("[X]", toast.IconGlyph);
        Assert.Equal(Brushes.OrangeRed, toast.ForegroundBrush);
    }

    [Fact]
    public void UnknownType_FallsBackToInfoBehavior()
    {
        var toast = new ToastMessage { Type = "unknown" };
        Assert.Equal("[i]", toast.IconGlyph);
    }

    [Fact]
    public void IsVisibleDefaultsToTrue()
    {
        var toast = new ToastMessage();
        Assert.True(toast.IsVisible);
    }

    [Fact]
    public void TextAndTypeAreSettable()
    {
        var toast = new ToastMessage
        {
            Text = "Hello",
            Type = "warning",
        };
        Assert.Equal("Hello", toast.Text);
        Assert.Equal("warning", toast.Type);
    }
}

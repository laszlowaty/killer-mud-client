using System.Reflection;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using MudClient.App.Controls;
using Xunit;

namespace MudClient.App.Tests;

public sealed class MudOutputViewTests
{
    [AvaloniaFact]
    public void CloseSplitButton_ReturnsOutputToSinglePane()
    {
        var output = new MudOutputView();
        var splitBar = output.FindControl<Grid>("SplitBar");
        var liveTail = output.FindControl<ScrollViewer>("LiveTailScroller");
        var closeButton = output.FindControl<Button>("CloseSplitButton");

        Assert.NotNull(splitBar);
        Assert.NotNull(liveTail);
        Assert.NotNull(closeButton);

        SetSplitMode(output, true);
        Assert.True(splitBar!.IsVisible);
        Assert.True(liveTail!.IsVisible);

        closeButton!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        Assert.False(splitBar.IsVisible);
        Assert.False(liveTail.IsVisible);
    }

    private static void SetSplitMode(MudOutputView output, bool enabled)
    {
        var method = typeof(MudOutputView).GetMethod(
            "SetSplitMode",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(method);
        method!.Invoke(output, [enabled]);
    }
}

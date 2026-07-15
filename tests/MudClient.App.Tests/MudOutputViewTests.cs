using System.Reflection;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Layout;
using MudClient.App.Controls;
using Xunit;

namespace MudClient.App.Tests;

[Collection(AvaloniaUiCollection.Name)]
public sealed class MudOutputViewTests
{
    [AvaloniaFact]
    public void SearchBox_IsAvailableInBottomRightCorner()
    {
        var output = new MudOutputView();
        var searchBox = output.FindControl<TextBox>("SearchBox");

        Assert.NotNull(searchBox);
        Assert.Equal("Search...", searchBox!.PlaceholderText);
        var searchBorder = Assert.IsType<Border>(searchBox.Parent);
        Assert.Equal(HorizontalAlignment.Right, searchBorder.HorizontalAlignment);
        Assert.Equal(VerticalAlignment.Bottom, searchBorder.VerticalAlignment);
    }

    [AvaloniaFact]
    public void WordWrap_DisablesHorizontalScrollbars()
    {
        var output = new MudOutputView { WordWrap = true };

        Assert.Equal(
            ScrollBarVisibility.Disabled,
            output.FindControl<ScrollViewer>("ScrollbackScroller")!.HorizontalScrollBarVisibility);

        output.WordWrap = false;

        Assert.Equal(
            ScrollBarVisibility.Auto,
            output.FindControl<ScrollViewer>("ScrollbackScroller")!.HorizontalScrollBarVisibility);
    }

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

    [AvaloniaFact]
    public void Search_SelectsNewestMatch_AndMovesThroughOlderMatches()
    {
        var output = new MudOutputView();
        output.AppendText("pierwszy smok\nbez trafienia\nostatni smok\n");

        Assert.True(output.Search("SMOK"));
        Assert.Equal("smok", output.SelectedSearchText);
        Assert.Equal(2, output.SelectedSearchGlobalLine);

        Assert.True(output.Search("smok"));
        Assert.Equal("smok", output.SelectedSearchText);
        Assert.Equal(0, output.SelectedSearchGlobalLine);

        var splitBar = output.FindControl<Grid>("SplitBar");
        Assert.True(splitBar!.IsVisible);
    }

    [AvaloniaFact]
    public void TerminalWheelScroll_UsesFourLineLogicalStep()
    {
        var pane = new OutputPaneControl();
        var logicalScrollable = (ILogicalScrollable)pane;

        Assert.True(logicalScrollable.ScrollSize.Height > 40);
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

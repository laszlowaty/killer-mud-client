using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using MudClient.App.Controls;
using Xunit;

namespace MudClient.App.Tests;

[Collection(AvaloniaUiCollection.Name)]
public sealed class OutputPanePerformanceTests
{
    [AvaloniaFact]
    public void AppendingAtMaximumScrollback_DoesNotRescanExistingLines()
    {
        var buffer = new OutputBuffer(10_000);
        for (var i = 0; i < buffer.Capacity - 1; i++)
        {
            buffer.Append($"line {i:D5} with enough text to wrap across the terminal viewport", default);
            buffer.CompleteLine();
        }

        var pane = new OutputPaneControl { Buffer = buffer, WordWrap = true };
        pane.Arrange(new Rect(0, 0, 320, 240));
        _ = pane.Extent;
        var fullScans = pane.HeightIndexFullScanCount;

        for (var i = 0; i < 100; i++)
        {
            buffer.Append($"new line {i}", default);
            buffer.CompleteLine();
            pane.NotifyContentChanged();
            _ = pane.Extent;
        }

        Assert.Equal(fullScans, pane.HeightIndexFullScanCount);
    }

    [AvaloniaFact]
    public void NoWrap_LongLinesKeepSingleRowHeight()
    {
        var longLines = new OutputBuffer(100);
        var emptyLines = new OutputBuffer(100);
        for (var i = 0; i < 50; i++)
        {
            longLines.Append(new string('x', 500), default);
            longLines.CompleteLine();
            emptyLines.CompleteLine();
        }

        var longLinesPane = new OutputPaneControl { Buffer = longLines, WordWrap = false };
        var emptyLinesPane = new OutputPaneControl { Buffer = emptyLines, WordWrap = false };
        longLinesPane.Arrange(new Rect(0, 0, 160, 240));
        emptyLinesPane.Arrange(new Rect(0, 0, 160, 240));

        Assert.Equal(emptyLinesPane.Extent.Height, longLinesPane.Extent.Height);
    }

    [AvaloniaFact]
    public void WrappedWideText_RemainsPinnedToActualBottom()
    {
        var buffer = new OutputBuffer(100);
        buffer.Append(new string('W', 200), default);
        buffer.CompleteLine();

        var pane = new OutputPaneControl { Buffer = buffer, WordWrap = true };
        pane.Arrange(new Rect(0, 0, 120, 80));

        var bitmap = new RenderTargetBitmap(new PixelSize(120, 80));
        bitmap.Render(pane);

        var expectedBottom = Math.Max(0, pane.Extent.Height - pane.Viewport.Height);
        Assert.Equal(expectedBottom, pane.Offset.Y, precision: 5);
    }

    [AvaloniaFact]
    public void HiddenLiveTail_DoesNotRecalculateUntilSplitIsShown()
    {
        var output = new MudOutputView();
        var window = new Window { Content = output, Width = 800, Height = 500 };
        window.Show();
        window.UpdateLayout();

        try
        {
            var hiddenTail = Assert.IsType<OutputPaneControl>(typeof(MudOutputView)
                .GetField("_liveTailPane",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                ?.GetValue(output));
            var scans = hiddenTail.HeightIndexFullScanCount;

            for (var i = 0; i < 100; i++)
            {
                output.AppendText($"message {i}\n");
            }

            Assert.Equal(scans, hiddenTail.HeightIndexFullScanCount);
        }
        finally
        {
            window.Close();
        }
    }
}

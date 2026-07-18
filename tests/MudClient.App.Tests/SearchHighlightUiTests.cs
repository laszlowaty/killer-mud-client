using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using MudClient.App.Behaviors;

namespace MudClient.App.Tests;

[Collection(AvaloniaUiCollection.Name)]
public sealed class SearchHighlightUiTests
{
    [AvaloniaFact]
    public void Highlight_UsesDarkInkOnGoldAndBoldText()
    {
        var ink = new SolidColorBrush(Color.Parse("#2C2110"));
        var textBlock = new TextBlock { Foreground = ink };

        SearchHighlight.SetText(textBlock, "Magia ognia");
        SearchHighlight.SetTerms(textBlock, "ognia");

        Assert.True(SearchHighlight.GetIsMatch(textBlock));
        var highlighted = Assert.Single(textBlock.Inlines!.OfType<Run>(), run => run.Text == "ognia");
        Assert.Equal(FontWeight.Bold, highlighted.FontWeight);
        Assert.Equal(
            Color.Parse("#B0D5A34A"),
            Assert.IsAssignableFrom<ISolidColorBrush>(highlighted.Background).Color);
        Assert.Equal(
            Color.Parse("#FF2C2110"),
            Assert.IsAssignableFrom<ISolidColorBrush>(highlighted.Foreground).Color);
        Assert.All(
            textBlock.Inlines!.OfType<Run>(),
            run => Assert.Equal(ink.Color, Assert.IsAssignableFrom<ISolidColorBrush>(run.Foreground).Color));
    }

    [AvaloniaFact]
    public void AutoScroll_BringsFirstHighlightedDetailIntoView()
    {
        var match = new TextBlock { Margin = new Avalonia.Thickness(0, 420, 0, 0) };
        SearchHighlight.SetText(match, "odległe trafienie");
        SearchHighlight.SetTerms(match, "trafienie");

        var scrollViewer = new ScrollViewer
        {
            Width = 320,
            Height = 120,
            Content = new StackPanel
            {
                Children = { match, new Border { Height = 300 } },
            },
        };
        var window = new Window { Width = 360, Height = 180, Content = scrollViewer };
        window.Show();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();

        SearchAutoScroll.SetTerms(scrollViewer, "trafienie");
        SearchAutoScroll.SetContext(scrollViewer, new object());
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();

        Assert.True(scrollViewer.Offset.Y > 0);
        window.Close();
    }
}

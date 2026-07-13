using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using MudClient.App.Controls;

namespace MudClient.App.Tests;

[Collection(AvaloniaUiCollection.Name)]
public sealed class LargeTabbedWidgetTests
{
    [AvaloniaFact]
    public void Widget_UsesNinetyPercentFrame_ClosesAndInheritsWidgetFont()
    {
        var tabs = new TabControl();
        tabs.Items.Add(new TabItem { Header = "Nauczyciele", Content = new TextBlock { Text = "Treść" } });
        tabs.Items.Add(new TabItem { Header = "Księgi Magiczne", Content = new TextBlock { Text = "Treść" } });
        var widget = new LargeTabbedWidget
        {
            IsOpen = true,
            Title = "KILLEROPEDIA",
            TabContent = tabs,
        };
        var window = new Window
        {
            Width = 1000,
            Height = 800,
            Content = widget,
        };

        window.Show();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();

        var frame = widget.FindControl<Border>("WidgetFrame");
        Assert.NotNull(frame);
        Assert.Equal(widget.Bounds.Width * 0.9, frame.Bounds.Width, 1);
        Assert.Equal(widget.Bounds.Height * 0.9, frame.Bounds.Height, 1);
        Assert.Equal(2, tabs.ItemCount);

        var title = widget.GetVisualDescendants()
            .OfType<TextBlock>()
            .Single(text => text.Text == "KILLEROPEDIA");
        Assert.Equal(
            Avalonia.Application.Current!.Resources["WidgetFontFamilyResource"]!.ToString(),
            title.FontFamily.Name);

        var closeButton = widget.GetVisualDescendants()
            .OfType<Button>()
            .Single(button => button.Content?.ToString() == "✕");
        closeButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Assert.False(widget.IsOpen);

        window.Close();
    }
}

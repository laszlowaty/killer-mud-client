using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace MudClient.App.Behaviors;

/// <summary>
/// Keeps a Killeropedia detail scroller aligned with the first highlighted search result.
/// The context property retriggers the scroll when filtering selects a different record.
/// </summary>
public static class SearchAutoScroll
{
    public static readonly AttachedProperty<string?> TermsProperty =
        AvaloniaProperty.RegisterAttached<ScrollViewer, string?>("Terms", typeof(SearchAutoScroll));

    public static readonly AttachedProperty<object?> ContextProperty =
        AvaloniaProperty.RegisterAttached<ScrollViewer, object?>("Context", typeof(SearchAutoScroll));

    private static readonly AttachedProperty<int> RequestVersionProperty =
        AvaloniaProperty.RegisterAttached<ScrollViewer, int>("RequestVersion", typeof(SearchAutoScroll));

    static SearchAutoScroll()
    {
        TermsProperty.Changed.AddClassHandler<ScrollViewer>((scrollViewer, _) => Schedule(scrollViewer));
        ContextProperty.Changed.AddClassHandler<ScrollViewer>((scrollViewer, _) => Schedule(scrollViewer));
    }

    public static string? GetTerms(ScrollViewer element) => element.GetValue(TermsProperty);

    public static void SetTerms(ScrollViewer element, string? value) => element.SetValue(TermsProperty, value);

    public static object? GetContext(ScrollViewer element) => element.GetValue(ContextProperty);

    public static void SetContext(ScrollViewer element, object? value) => element.SetValue(ContextProperty, value);

    private static void Schedule(ScrollViewer scrollViewer)
    {
        var version = scrollViewer.GetValue(RequestVersionProperty) + 1;
        scrollViewer.SetValue(RequestVersionProperty, version);

        Dispatcher.UIThread.Post(() =>
        {
            if (scrollViewer.GetValue(RequestVersionProperty) != version)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(GetTerms(scrollViewer)))
            {
                scrollViewer.ScrollToHome();
                return;
            }

            var match = scrollViewer.GetVisualDescendants()
                .OfType<TextBlock>()
                .FirstOrDefault(SearchHighlight.GetIsMatch);
            match?.BringIntoView();
        }, DispatcherPriority.Loaded);
    }
}

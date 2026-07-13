using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace MudClient.App.Controls;

/// <summary>
/// Reusable, fixed-layout overlay for large widget screens. The frame always occupies
/// 90% of its host in both dimensions; callers provide the tab control through
/// <see cref="TabContent"/> and toggle <see cref="IsOpen"/>.
/// </summary>
public partial class LargeTabbedWidget : UserControl
{
    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<LargeTabbedWidget, bool>(
            nameof(IsOpen), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<LargeTabbedWidget, string>(nameof(Title), string.Empty);

    public static readonly StyledProperty<TabControl?> TabContentProperty =
        AvaloniaProperty.Register<LargeTabbedWidget, TabControl?>(nameof(TabContent));

    public LargeTabbedWidget()
    {
        InitializeComponent();
    }

    public bool IsOpen
    {
        get => GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public TabControl? TabContent
    {
        get => GetValue(TabContentProperty);
        set => SetValue(TabContentProperty, value);
    }

    private void Close_OnClick(object? sender, RoutedEventArgs eventArgs) => Close();

    private void OnKeyDown(object? sender, KeyEventArgs eventArgs)
    {
        if (eventArgs.Key != Key.Escape)
        {
            return;
        }

        Close();
        eventArgs.Handled = true;
    }

    private void Close()
    {
        IsOpen = false;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}

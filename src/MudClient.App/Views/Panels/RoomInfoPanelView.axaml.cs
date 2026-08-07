using Avalonia;
using Avalonia.Controls;

namespace MudClient.App.Views.Panels;

public sealed partial class RoomInfoPanelView : UserControl
{
    public static readonly StyledProperty<bool> ShowDetailsProperty =
        AvaloniaProperty.Register<RoomInfoPanelView, bool>(nameof(ShowDetails), defaultValue: true);

    public bool ShowDetails
    {
        get => GetValue(ShowDetailsProperty);
        set => SetValue(ShowDetailsProperty, value);
    }
    public RoomInfoPanelView()
    {
        InitializeComponent();
    }
}

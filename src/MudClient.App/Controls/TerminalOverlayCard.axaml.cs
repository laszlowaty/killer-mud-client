using Avalonia.Controls;

namespace MudClient.App.Controls;

/// <summary>
/// One translucent card rendering a panel pinned as a Terminal overlay in TRANSPARENCY mode. Purely
/// presentational — see the .axaml file for the fixed right-aligned stacking that positions these,
/// and <see cref="MudClient.App.Docking.MudDockFactory.IsTransparencyLayout"/> for why there is no
/// drag/resize here (deliberately not combined with free-form docking).
/// </summary>
public sealed partial class TerminalOverlayCard : UserControl
{
    public TerminalOverlayCard()
    {
        InitializeComponent();
    }
}

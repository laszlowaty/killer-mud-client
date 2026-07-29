using CommunityToolkit.Mvvm.ComponentModel;
using MudClient.App.Docking;
using MudClient.App.Models;

namespace MudClient.App.ViewModels;

/// <summary>
/// View-model for one panel pinned as a floating overlay on the Terminal. Wraps the docked
/// <see cref="PanelTool"/> together with its persisted <see cref="TerminalOverlayEntry"/> height
/// weight. Several of these can be active at once — see
/// <see cref="MainWindowViewModel.TerminalOverlays"/>.
/// </summary>
public sealed class TerminalOverlayViewModel : ObservableObject
{
    private readonly TerminalOverlayEntry _entry;
    private readonly Action _onChanged;

    public TerminalOverlayViewModel(PanelTool panel, TerminalOverlayEntry entry, Action onChanged)
    {
        Panel = panel;
        _entry = entry;
        _onChanged = onChanged;
    }

    public PanelTool Panel { get; }

    /// <summary>This overlay's height relative to its siblings in the stack (a Grid star weight).
    /// Set by <c>TerminalOverlayHost</c> when the user drags the splitter between two cards.</summary>
    public double HeightWeight
    {
        get => _entry.HeightWeight;
        set
        {
            var clamped = Math.Clamp(
                value, AppSettings.MinTerminalOverlayHeightWeight, AppSettings.MaxTerminalOverlayHeightWeight);
            if (Math.Abs(_entry.HeightWeight - clamped) < 0.001)
            {
                return;
            }

            _entry.HeightWeight = clamped;
            OnPropertyChanged();
            _onChanged();
        }
    }
}

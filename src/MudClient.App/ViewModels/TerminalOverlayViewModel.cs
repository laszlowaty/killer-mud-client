using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MudClient.App.Docking;
using MudClient.App.Models;

namespace MudClient.App.ViewModels;

/// <summary>Direction requested by one of a <see cref="TerminalOverlayViewModel"/>'s move
/// buttons — see <see cref="MainWindowViewModel"/>'s handling for what each one does.</summary>
public enum OverlayMoveDirection
{
    Up,
    Down,
    Left,
    Right,
}

/// <summary>
/// View-model for one panel pinned as a floating overlay on the Terminal. Wraps the docked
/// <see cref="PanelTool"/> together with its persisted <see cref="TerminalOverlayEntry"/> height
/// weight and side. Several of these can be active at once — see
/// <see cref="MainWindowViewModel.TerminalOverlays"/>.
/// </summary>
public sealed class TerminalOverlayViewModel : ObservableObject
{
    private readonly TerminalOverlayEntry _entry;
    private readonly Action _onChanged;
    private readonly Action<TerminalOverlayViewModel, OverlayMoveDirection> _onMove;

    public TerminalOverlayViewModel(
        PanelTool panel,
        TerminalOverlayEntry entry,
        Action onChanged,
        Action<TerminalOverlayViewModel, OverlayMoveDirection> onMove)
    {
        Panel = panel;
        _entry = entry;
        _onChanged = onChanged;
        _onMove = onMove;
        MoveUpCommand = new RelayCommand(() => _onMove(this, OverlayMoveDirection.Up));
        MoveDownCommand = new RelayCommand(() => _onMove(this, OverlayMoveDirection.Down));
        MoveLeftCommand = new RelayCommand(() => _onMove(this, OverlayMoveDirection.Left));
        MoveRightCommand = new RelayCommand(() => _onMove(this, OverlayMoveDirection.Right));
    }

    public PanelTool Panel { get; }

    /// <summary>Which side of the Terminal this overlay currently renders on. Changed only via
    /// <see cref="MoveLeftCommand"/>/<see cref="MoveRightCommand"/>, handled by
    /// <see cref="MainWindowViewModel"/> (a side change moves the card between two different
    /// visual stacks, which needs a structural rebuild — unlike <see cref="HeightWeight"/>, it's
    /// not something this view-model can apply by itself).</summary>
    public OverlaySide Side => _entry.Side;

    /// <summary>Set by <see cref="MainWindowViewModel"/> after deciding a side change should
    /// happen. Deliberately not a public setter: changing <see cref="Side"/> requires rebuilding
    /// which physical stack the card lives in, which only the owning view-model can orchestrate.</summary>
    internal void SetSide(OverlaySide side)
    {
        if (_entry.Side == side)
        {
            return;
        }

        _entry.Side = side;
        OnPropertyChanged(nameof(Side));
    }

    /// <summary>This overlay's height relative to its siblings in its side's stack (a Grid star
    /// weight). Set by <c>TerminalOverlayHost</c> when the user drags the splitter between two
    /// cards.</summary>
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

    public IRelayCommand MoveUpCommand { get; }

    public IRelayCommand MoveDownCommand { get; }

    public IRelayCommand MoveLeftCommand { get; }

    public IRelayCommand MoveRightCommand { get; }
}

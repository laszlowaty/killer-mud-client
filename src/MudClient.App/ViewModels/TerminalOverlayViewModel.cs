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
/// weight, column index, and column size. Several of these can be active at once — see
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

    /// <summary>Which column this overlay currently renders in — 0 hugs the right edge, higher
    /// indices sit further left. Changed only via <see cref="MoveLeftCommand"/>/
    /// <see cref="MoveRightCommand"/>, handled by <see cref="MainWindowViewModel"/> (a column
    /// change needs a structural rebuild of the floating columns — unlike <see cref="HeightWeight"/>,
    /// it's not something this view-model can apply by itself).</summary>
    public int ColumnIndex => _entry.ColumnIndex;

    /// <summary>Set by <see cref="MainWindowViewModel"/> after deciding a column change should
    /// happen. Deliberately not a public setter: changing <see cref="ColumnIndex"/> requires
    /// rebuilding which physical column the card lives in, which only the owning view-model can
    /// orchestrate.</summary>
    internal void SetColumnIndex(int columnIndex)
    {
        if (_entry.ColumnIndex == columnIndex)
        {
            return;
        }

        _entry.ColumnIndex = columnIndex;
        OnPropertyChanged(nameof(ColumnIndex));
    }

    /// <summary>This overlay's height relative to its siblings in its column (a Grid star
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

    /// <summary>This overlay's column's width in pixels — logically shared by every overlay in
    /// the same column. Set by <c>TerminalOverlayHost</c> when the user drags that column's
    /// left-edge handle, applied to every overlay sharing <see cref="ColumnIndex"/> so they stay
    /// in sync.</summary>
    public double ColumnWidth
    {
        get => _entry.ColumnWidth;
        set
        {
            var clamped = Math.Clamp(
                value, AppSettings.MinTerminalOverlayColumnWidth, AppSettings.MaxTerminalOverlayColumnWidth);
            if (Math.Abs(_entry.ColumnWidth - clamped) < 0.5)
            {
                return;
            }

            _entry.ColumnWidth = clamped;
            OnPropertyChanged();
            _onChanged();
        }
    }

    /// <summary>This overlay's column's overall height as a fraction (0..1) of the Terminal's own
    /// height — logically shared by every overlay in the same column. Set by
    /// <c>TerminalOverlayHost</c> when the user drags that column's bottom-edge handle.</summary>
    public double ColumnHeightFraction
    {
        get => _entry.ColumnHeightFraction;
        set
        {
            var clamped = Math.Clamp(
                value,
                AppSettings.MinTerminalOverlayColumnHeightFraction,
                AppSettings.MaxTerminalOverlayColumnHeightFraction);
            if (Math.Abs(_entry.ColumnHeightFraction - clamped) < 0.001)
            {
                return;
            }

            _entry.ColumnHeightFraction = clamped;
            OnPropertyChanged();
            _onChanged();
        }
    }

    public IRelayCommand MoveUpCommand { get; }

    public IRelayCommand MoveDownCommand { get; }

    public IRelayCommand MoveLeftCommand { get; }

    public IRelayCommand MoveRightCommand { get; }
}

using System.Collections.Specialized;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using MudClient.App.Models;
using MudClient.App.ViewModels;

namespace MudClient.App.Controls;

/// <summary>
/// Builds and maintains the two independently resizable stacks of <see cref="TerminalOverlayCard"/>s
/// flanking the Terminal (which always stays centered) — see the .axaml file for the static shell.
/// Every resize gesture (either side's column width, either side's stack height, and the split
/// between two adjacent cards within one side) is a hand-rolled pointer-drag computing a
/// fraction/weight directly from the pixel delta, rather than Avalonia's <see cref="GridSplitter"/>:
/// GridSplitter's automatic column/row-pair detection did not reliably resize two star-sized
/// siblings here. Each drag has exactly one writer (this class writes the view-model; the
/// view-model's PropertyChanged reapplies it to the Grid), so there is no feedback loop to guard
/// against — unlike an earlier GridSplitter-based version of this file, which recursed into a
/// stack overflow because setting two adjacent GridLengths as separate property writes let a
/// reactive listener observe inconsistent in-between state.
/// </summary>
public sealed partial class TerminalOverlayHost : UserControl
{
    private readonly Grid _rootGrid;

    private readonly Grid _stackArea;
    private readonly Grid _overlayColumn;
    private readonly Border _widthHandle;
    private readonly Border _heightHandle;

    private readonly Grid _leftStackArea;
    private readonly Grid _leftOverlayColumn;
    private readonly Border _leftWidthHandle;
    private readonly Border _leftHeightHandle;

    private MainWindowViewModel? _viewModel;

    private Point? _widthDragStart;
    private double _widthDragStartFraction;
    private Point? _heightDragStart;
    private double _heightDragStartFraction;

    private Point? _leftWidthDragStart;
    private double _leftWidthDragStartFraction;
    private Point? _leftHeightDragStart;
    private double _leftHeightDragStartFraction;

    public TerminalOverlayHost()
    {
        InitializeComponent();
        _rootGrid = FindRequired<Grid>("RootGrid");
        _stackArea = FindRequired<Grid>("StackArea");
        _overlayColumn = FindRequired<Grid>("OverlayColumn");
        _widthHandle = FindRequired<Border>("WidthHandle");
        _heightHandle = FindRequired<Border>("HeightHandle");
        _leftStackArea = FindRequired<Grid>("LeftStackArea");
        _leftOverlayColumn = FindRequired<Grid>("LeftOverlayColumn");
        _leftWidthHandle = FindRequired<Border>("LeftWidthHandle");
        _leftHeightHandle = FindRequired<Border>("LeftHeightHandle");

        _widthHandle.PointerPressed += OnWidthHandlePointerPressed;
        _widthHandle.PointerMoved += OnWidthHandlePointerMoved;
        _widthHandle.PointerReleased += OnWidthHandlePointerReleased;
        _heightHandle.PointerPressed += OnHeightHandlePointerPressed;
        _heightHandle.PointerMoved += OnHeightHandlePointerMoved;
        _heightHandle.PointerReleased += OnHeightHandlePointerReleased;

        _leftWidthHandle.PointerPressed += OnLeftWidthHandlePointerPressed;
        _leftWidthHandle.PointerMoved += OnLeftWidthHandlePointerMoved;
        _leftWidthHandle.PointerReleased += OnLeftWidthHandlePointerReleased;
        _leftHeightHandle.PointerPressed += OnLeftHeightHandlePointerPressed;
        _leftHeightHandle.PointerMoved += OnLeftHeightHandlePointerMoved;
        _leftHeightHandle.PointerReleased += OnLeftHeightHandlePointerReleased;

        DataContextChanged += OnDataContextChanged;
    }

    private T FindRequired<T>(string name) where T : Control =>
        this.FindControl<T>(name) ?? throw new InvalidOperationException($"{name} not found.");

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.TerminalOverlays.CollectionChanged -= OnTerminalOverlaysChanged;
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _viewModel = DataContext as MainWindowViewModel;

        if (_viewModel is not null)
        {
            _viewModel.TerminalOverlays.CollectionChanged += OnTerminalOverlaysChanged;
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            ApplyColumnWidths();
            ApplyColumnHeights();
        }

        RebuildColumns();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(MainWindowViewModel.TerminalOverlayColumnWidthFraction):
            case nameof(MainWindowViewModel.TerminalOverlayLeftColumnWidthFraction):
                ApplyColumnWidths();
                break;
            case nameof(MainWindowViewModel.TerminalOverlayColumnHeightFraction):
                ApplyColumnHeights();
                break;
            case nameof(MainWindowViewModel.TerminalOverlayLeftColumnHeightFraction):
                ApplyLeftColumnHeight();
                break;
        }
    }

    private void OnTerminalOverlaysChanged(object? sender, NotifyCollectionChangedEventArgs e) => RebuildColumns();

    /// <summary>Applies both sides' width fractions to the shared 3-star-column layout (left
    /// stack, center passthrough, right stack). The center always keeps at least a thin sliver
    /// (never a negative or zero Star weight) even if both sides are dragged toward their max.</summary>
    private void ApplyColumnWidths()
    {
        if (_viewModel is null)
        {
            return;
        }

        var left = _viewModel.TerminalOverlayLeftColumnWidthFraction;
        var right = _viewModel.TerminalOverlayColumnWidthFraction;
        var center = Math.Max(0.05, 1 - left - right);
        _rootGrid.ColumnDefinitions[0].Width = new GridLength(left, GridUnitType.Star);
        _rootGrid.ColumnDefinitions[2].Width = new GridLength(center, GridUnitType.Star);
        _rootGrid.ColumnDefinitions[4].Width = new GridLength(right, GridUnitType.Star);
    }

    private void ApplyColumnHeights()
    {
        ApplyRightColumnHeight();
        ApplyLeftColumnHeight();
    }

    private void ApplyRightColumnHeight()
    {
        if (_viewModel is null)
        {
            return;
        }

        var fraction = _viewModel.TerminalOverlayColumnHeightFraction;
        _stackArea.RowDefinitions[0].Height = new GridLength(fraction, GridUnitType.Star);
        _stackArea.RowDefinitions[2].Height = new GridLength(1 - fraction, GridUnitType.Star);
    }

    private void ApplyLeftColumnHeight()
    {
        if (_viewModel is null)
        {
            return;
        }

        var fraction = _viewModel.TerminalOverlayLeftColumnHeightFraction;
        _leftStackArea.RowDefinitions[0].Height = new GridLength(fraction, GridUnitType.Star);
        _leftStackArea.RowDefinitions[2].Height = new GridLength(1 - fraction, GridUnitType.Star);
    }

    private void OnWidthHandlePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_viewModel is null || !e.GetCurrentPoint(_widthHandle).Properties.IsLeftButtonPressed)
        {
            return;
        }

        _widthDragStart = e.GetPosition(_rootGrid);
        _widthDragStartFraction = _viewModel.TerminalOverlayColumnWidthFraction;
        e.Pointer.Capture(_widthHandle);
        e.Handled = true;
    }

    private void OnWidthHandlePointerMoved(object? sender, PointerEventArgs e)
    {
        if (_widthDragStart is not { } start || _viewModel is null)
        {
            return;
        }

        var totalWidth = _rootGrid.Bounds.Width;
        if (totalWidth <= 0)
        {
            return;
        }

        var deltaX = e.GetPosition(_rootGrid).X - start.X;
        // Dragging left shrinks the center passthrough and grows the right overlay column.
        _viewModel.TerminalOverlayColumnWidthFraction = _widthDragStartFraction - deltaX / totalWidth;
    }

    private void OnWidthHandlePointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _widthDragStart = null;
        e.Pointer.Capture(null);
    }

    private void OnLeftWidthHandlePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_viewModel is null || !e.GetCurrentPoint(_leftWidthHandle).Properties.IsLeftButtonPressed)
        {
            return;
        }

        _leftWidthDragStart = e.GetPosition(_rootGrid);
        _leftWidthDragStartFraction = _viewModel.TerminalOverlayLeftColumnWidthFraction;
        e.Pointer.Capture(_leftWidthHandle);
        e.Handled = true;
    }

    private void OnLeftWidthHandlePointerMoved(object? sender, PointerEventArgs e)
    {
        if (_leftWidthDragStart is not { } start || _viewModel is null)
        {
            return;
        }

        var totalWidth = _rootGrid.Bounds.Width;
        if (totalWidth <= 0)
        {
            return;
        }

        var deltaX = e.GetPosition(_rootGrid).X - start.X;
        // Dragging right shrinks the center passthrough and grows the left overlay column.
        _viewModel.TerminalOverlayLeftColumnWidthFraction = _leftWidthDragStartFraction + deltaX / totalWidth;
    }

    private void OnLeftWidthHandlePointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _leftWidthDragStart = null;
        e.Pointer.Capture(null);
    }

    private void OnHeightHandlePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_viewModel is null || !e.GetCurrentPoint(_heightHandle).Properties.IsLeftButtonPressed)
        {
            return;
        }

        _heightDragStart = e.GetPosition(_stackArea);
        _heightDragStartFraction = _viewModel.TerminalOverlayColumnHeightFraction;
        e.Pointer.Capture(_heightHandle);
        e.Handled = true;
    }

    private void OnHeightHandlePointerMoved(object? sender, PointerEventArgs e)
    {
        if (_heightDragStart is not { } start || _viewModel is null)
        {
            return;
        }

        var totalHeight = _stackArea.Bounds.Height;
        if (totalHeight <= 0)
        {
            return;
        }

        var deltaY = e.GetPosition(_stackArea).Y - start.Y;
        // The stack is anchored to the top: dragging its bottom-edge handle down grows it back
        // toward full height; dragging up shrinks it, revealing terminal beneath the last card.
        _viewModel.TerminalOverlayColumnHeightFraction = _heightDragStartFraction + deltaY / totalHeight;
    }

    private void OnHeightHandlePointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _heightDragStart = null;
        e.Pointer.Capture(null);
    }

    private void OnLeftHeightHandlePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_viewModel is null || !e.GetCurrentPoint(_leftHeightHandle).Properties.IsLeftButtonPressed)
        {
            return;
        }

        _leftHeightDragStart = e.GetPosition(_leftStackArea);
        _leftHeightDragStartFraction = _viewModel.TerminalOverlayLeftColumnHeightFraction;
        e.Pointer.Capture(_leftHeightHandle);
        e.Handled = true;
    }

    private void OnLeftHeightHandlePointerMoved(object? sender, PointerEventArgs e)
    {
        if (_leftHeightDragStart is not { } start || _viewModel is null)
        {
            return;
        }

        var totalHeight = _leftStackArea.Bounds.Height;
        if (totalHeight <= 0)
        {
            return;
        }

        var deltaY = e.GetPosition(_leftStackArea).Y - start.Y;
        _viewModel.TerminalOverlayLeftColumnHeightFraction = _leftHeightDragStartFraction + deltaY / totalHeight;
    }

    private void OnLeftHeightHandlePointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _leftHeightDragStart = null;
        e.Pointer.Capture(null);
    }

    /// <summary>Rebuilds every row, card, and splitter in both side columns from the current
    /// <see cref="MainWindowViewModel.TerminalOverlays"/>, filtered by
    /// <see cref="TerminalOverlayViewModel.Side"/>. Simpler than diffing the collection or a side
    /// change item-by-item, and cheap: pinning/unpinning/moving an overlay is a rare, deliberate
    /// action, not a hot path.</summary>
    private void RebuildColumns()
    {
        if (_viewModel is null)
        {
            _leftOverlayColumn.Children.Clear();
            _leftOverlayColumn.RowDefinitions.Clear();
            _overlayColumn.Children.Clear();
            _overlayColumn.RowDefinitions.Clear();
            _leftStackArea.IsVisible = false;
            _widthHandle.IsVisible = false;
            _leftWidthHandle.IsVisible = false;
            _stackArea.IsVisible = false;
            return;
        }

        var leftOverlays = _viewModel.TerminalOverlays.Where(o => o.Side == OverlaySide.Left).ToList();
        var rightOverlays = _viewModel.TerminalOverlays.Where(o => o.Side == OverlaySide.Right).ToList();

        RebuildColumn(_leftOverlayColumn, leftOverlays);
        RebuildColumn(_overlayColumn, rightOverlays);

        _leftStackArea.IsVisible = leftOverlays.Count > 0;
        _leftWidthHandle.IsVisible = leftOverlays.Count > 0;
        _stackArea.IsVisible = rightOverlays.Count > 0;
        _widthHandle.IsVisible = rightOverlays.Count > 0;
    }

    private void RebuildColumn(Grid column, IReadOnlyList<TerminalOverlayViewModel> overlays)
    {
        column.Children.Clear();
        column.RowDefinitions.Clear();

        for (var i = 0; i < overlays.Count; i++)
        {
            var overlay = overlays[i];
            var rowDefinition = new RowDefinition(new GridLength(overlay.HeightWeight, GridUnitType.Star))
            {
                MinHeight = 60,
            };
            var rowIndex = column.RowDefinitions.Count;
            column.RowDefinitions.Add(rowDefinition);

            var card = new TerminalOverlayCard { DataContext = overlay.Panel, Overlay = overlay };
            Grid.SetRow(card, rowIndex);
            column.Children.Add(card);

            if (i == overlays.Count - 1)
            {
                continue;
            }

            var below = overlays[i + 1];
            var splitterRowIndex = column.RowDefinitions.Count;
            column.RowDefinitions.Add(new RowDefinition(new GridLength(6, GridUnitType.Pixel)));
            var splitter = CreateRowSplitter(column, overlays, overlay, below);
            Grid.SetRow(splitter, splitterRowIndex);
            column.Children.Add(splitter);
        }
    }

    private static Border CreateRowSplitter(
        Grid column,
        IReadOnlyList<TerminalOverlayViewModel> sideOverlays,
        TerminalOverlayViewModel above,
        TerminalOverlayViewModel below)
    {
        var line = new Border
        {
            Height = 1,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Center,
        };
        line.Classes.Add("mud-overlay-handle-line");

        var handle = new Border
        {
            Background = Brushes.Transparent,
            Cursor = new Cursor(StandardCursorType.SizeNorthSouth),
            Child = line,
        };
        handle.Classes.Add("mud-overlay-handle");

        Point? dragStart = null;
        var startAboveWeight = 0.0;
        var startBelowWeight = 0.0;
        var totalWeightAtDragStart = 0.0;

        handle.PointerPressed += (_, e) =>
        {
            if (!e.GetCurrentPoint(handle).Properties.IsLeftButtonPressed)
            {
                return;
            }

            dragStart = e.GetPosition(column);
            startAboveWeight = above.HeightWeight;
            startBelowWeight = below.HeightWeight;
            totalWeightAtDragStart = sideOverlays.Sum(o => o.HeightWeight);
            e.Pointer.Capture(handle);
            e.Handled = true;
        };

        handle.PointerMoved += (_, e) =>
        {
            if (dragStart is not { } start || totalWeightAtDragStart <= 0)
            {
                return;
            }

            var totalHeightPx = column.Bounds.Height;
            if (totalHeightPx <= 0)
            {
                return;
            }

            var deltaY = e.GetPosition(column).Y - start.Y;
            var weightDelta = deltaY * totalWeightAtDragStart / totalHeightPx;

            above.HeightWeight = startAboveWeight + weightDelta;
            below.HeightWeight = startBelowWeight - weightDelta;
            ReapplyRowWeights(column, sideOverlays);
        };

        handle.PointerReleased += (_, e) =>
        {
            dragStart = null;
            e.Pointer.Capture(null);
        };

        return handle;
    }

    /// <summary>Pushes each overlay's current <see cref="TerminalOverlayViewModel.HeightWeight"/>
    /// back onto its own row — mirrors <see cref="RebuildColumn"/>'s row order (card, splitter,
    /// card, ...), so overlay i always lives at row 2*i.</summary>
    private static void ReapplyRowWeights(Grid column, IReadOnlyList<TerminalOverlayViewModel> overlays)
    {
        for (var i = 0; i < overlays.Count; i++)
        {
            column.RowDefinitions[i * 2].Height = new GridLength(overlays[i].HeightWeight, GridUnitType.Star);
        }
    }
}

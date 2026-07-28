using System.Collections.Specialized;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using MudClient.App.ViewModels;

namespace MudClient.App.Controls;

/// <summary>
/// Builds and maintains the right-aligned, resizable stack of <see cref="TerminalOverlayCard"/>s —
/// see the .axaml file for the static shell. Every resize gesture (column width, stack height,
/// and the split between two adjacent cards) is a hand-rolled pointer-drag computing a
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

    private MainWindowViewModel? _viewModel;

    private Point? _widthDragStart;
    private double _widthDragStartFraction;
    private Point? _heightDragStart;
    private double _heightDragStartFraction;

    public TerminalOverlayHost()
    {
        InitializeComponent();
        _rootGrid = this.FindControl<Grid>("RootGrid")
            ?? throw new InvalidOperationException("RootGrid not found.");
        _stackArea = this.FindControl<Grid>("StackArea")
            ?? throw new InvalidOperationException("StackArea not found.");
        _overlayColumn = this.FindControl<Grid>("OverlayColumn")
            ?? throw new InvalidOperationException("OverlayColumn not found.");
        _widthHandle = this.FindControl<Border>("WidthHandle")
            ?? throw new InvalidOperationException("WidthHandle not found.");
        _heightHandle = this.FindControl<Border>("HeightHandle")
            ?? throw new InvalidOperationException("HeightHandle not found.");

        _widthHandle.PointerPressed += OnWidthHandlePointerPressed;
        _widthHandle.PointerMoved += OnWidthHandlePointerMoved;
        _widthHandle.PointerReleased += OnWidthHandlePointerReleased;
        _heightHandle.PointerPressed += OnHeightHandlePointerPressed;
        _heightHandle.PointerMoved += OnHeightHandlePointerMoved;
        _heightHandle.PointerReleased += OnHeightHandlePointerReleased;

        DataContextChanged += OnDataContextChanged;
    }

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
            ApplyColumnWidth();
            ApplyColumnHeight();
        }

        RebuildColumn();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(MainWindowViewModel.TerminalOverlayColumnWidthFraction):
                ApplyColumnWidth();
                break;
            case nameof(MainWindowViewModel.TerminalOverlayColumnHeightFraction):
                ApplyColumnHeight();
                break;
        }
    }

    private void OnTerminalOverlaysChanged(object? sender, NotifyCollectionChangedEventArgs e) => RebuildColumn();

    private void ApplyColumnWidth()
    {
        if (_viewModel is null)
        {
            return;
        }

        var fraction = _viewModel.TerminalOverlayColumnWidthFraction;
        _rootGrid.ColumnDefinitions[0].Width = new GridLength(1 - fraction, GridUnitType.Star);
        _rootGrid.ColumnDefinitions[2].Width = new GridLength(fraction, GridUnitType.Star);
    }

    private void ApplyColumnHeight()
    {
        if (_viewModel is null)
        {
            return;
        }

        var fraction = _viewModel.TerminalOverlayColumnHeightFraction;
        _stackArea.RowDefinitions[0].Height = new GridLength(fraction, GridUnitType.Star);
        _stackArea.RowDefinitions[2].Height = new GridLength(1 - fraction, GridUnitType.Star);
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
        // Dragging left shrinks the empty passthrough column and grows the overlay column.
        _viewModel.TerminalOverlayColumnWidthFraction = _widthDragStartFraction - deltaX / totalWidth;
    }

    private void OnWidthHandlePointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _widthDragStart = null;
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

    /// <summary>Rebuilds every row, card, and splitter in <see cref="OverlayColumn"/> from the
    /// current <see cref="MainWindowViewModel.TerminalOverlays"/>. Simpler than diffing the
    /// collection change-by-change, and cheap: pinning/unpinning an overlay is a rare, deliberate
    /// action, not a hot path.</summary>
    private void RebuildColumn()
    {
        _overlayColumn.Children.Clear();
        _overlayColumn.RowDefinitions.Clear();

        if (_viewModel is null)
        {
            return;
        }

        var overlays = _viewModel.TerminalOverlays;
        for (var i = 0; i < overlays.Count; i++)
        {
            var overlay = overlays[i];
            var rowDefinition = new RowDefinition(new GridLength(overlay.HeightWeight, GridUnitType.Star))
            {
                MinHeight = 60,
            };
            var rowIndex = _overlayColumn.RowDefinitions.Count;
            _overlayColumn.RowDefinitions.Add(rowDefinition);

            var card = new TerminalOverlayCard { DataContext = overlay.Panel };
            Grid.SetRow(card, rowIndex);
            _overlayColumn.Children.Add(card);

            if (i == overlays.Count - 1)
            {
                continue;
            }

            var below = overlays[i + 1];
            var splitterRowIndex = _overlayColumn.RowDefinitions.Count;
            _overlayColumn.RowDefinitions.Add(new RowDefinition(new GridLength(6, GridUnitType.Pixel)));
            var splitter = CreateRowSplitter(overlay, below);
            Grid.SetRow(splitter, splitterRowIndex);
            _overlayColumn.Children.Add(splitter);
        }
    }

    private Border CreateRowSplitter(TerminalOverlayViewModel above, TerminalOverlayViewModel below)
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

            dragStart = e.GetPosition(_overlayColumn);
            startAboveWeight = above.HeightWeight;
            startBelowWeight = below.HeightWeight;
            totalWeightAtDragStart = _viewModel?.TerminalOverlays.Sum(o => o.HeightWeight) ?? 0;
            e.Pointer.Capture(handle);
            e.Handled = true;
        };

        handle.PointerMoved += (_, e) =>
        {
            if (dragStart is not { } start || totalWeightAtDragStart <= 0)
            {
                return;
            }

            var totalHeightPx = _overlayColumn.Bounds.Height;
            if (totalHeightPx <= 0)
            {
                return;
            }

            var deltaY = e.GetPosition(_overlayColumn).Y - start.Y;
            var weightDelta = deltaY * totalWeightAtDragStart / totalHeightPx;

            above.HeightWeight = startAboveWeight + weightDelta;
            below.HeightWeight = startBelowWeight - weightDelta;
            ReapplyRowWeights();
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
    private void ReapplyRowWeights()
    {
        if (_viewModel is null)
        {
            return;
        }

        var overlays = _viewModel.TerminalOverlays;
        for (var i = 0; i < overlays.Count; i++)
        {
            _overlayColumn.RowDefinitions[i * 2].Height = new GridLength(overlays[i].HeightWeight, GridUnitType.Star);
        }
    }
}

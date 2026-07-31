using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using MudClient.App.ViewModels;

namespace MudClient.App.Controls;

/// <summary>
/// Builds and maintains the floating, right-anchored columns of <see cref="TerminalOverlayCard"/>s
/// on top of the Terminal — see the .axaml file for why this is built in code-behind rather than
/// static XAML. Every resize gesture (a column's own width, a column's overall height, and the
/// split between two adjacent cards within one column) is a hand-rolled pointer-drag computing a
/// value directly from the pixel delta, rather than Avalonia's <see cref="GridSplitter"/>:
/// GridSplitter's automatic column/row-pair detection did not reliably resize two star-sized
/// siblings here. Each drag has exactly one writer (this class writes the view-model directly),
/// so there is no feedback loop to guard against.
/// </summary>
public sealed partial class TerminalOverlayHost : UserControl
{
    private readonly StackPanel _columnsPanel;

    private MainWindowViewModel? _viewModel;

    public TerminalOverlayHost()
    {
        InitializeComponent();
        _columnsPanel = FindRequired<StackPanel>("ColumnsPanel");
        DataContextChanged += OnDataContextChanged;
    }

    private T FindRequired<T>(string name) where T : Control =>
        this.FindControl<T>(name) ?? throw new InvalidOperationException($"{name} not found.");

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.TerminalOverlays.CollectionChanged -= OnTerminalOverlaysChanged;
        }

        _viewModel = DataContext as MainWindowViewModel;

        if (_viewModel is not null)
        {
            _viewModel.TerminalOverlays.CollectionChanged += OnTerminalOverlaysChanged;
        }

        RebuildColumns();
    }

    private void OnTerminalOverlaysChanged(object? sender, NotifyCollectionChangedEventArgs e) => RebuildColumns();

    /// <summary>Rebuilds every column, row, card, and splitter from the current
    /// <see cref="MainWindowViewModel.TerminalOverlays"/>, grouped by
    /// <see cref="TerminalOverlayViewModel.ColumnIndex"/> and ordered so column 0 ends up
    /// rightmost. Simpler than diffing the collection item-by-item, and cheap: pinning/unpinning/
    /// moving an overlay is a rare, deliberate action, not a hot path.</summary>
    private void RebuildColumns()
    {
        _columnsPanel.Children.Clear();

        if (_viewModel is null)
        {
            return;
        }

        var columns = _viewModel.TerminalOverlays
            .GroupBy(o => o.ColumnIndex)
            .OrderByDescending(group => group.Key)
            .Select(group => (IReadOnlyList<TerminalOverlayViewModel>)group.ToList());

        foreach (var column in columns)
        {
            _columnsPanel.Children.Add(BuildColumn(column));
        }
    }

    /// <summary>Builds one floating column: a resize handle on its left edge, then a fixed-width
    /// stack of cards (top) over a passthrough area (bottom) revealed by shrinking the column's
    /// height fraction, with its own resize handle in between.</summary>
    private Grid BuildColumn(IReadOnlyList<TerminalOverlayViewModel> overlays)
    {
        var stackArea = new Grid
        {
            Width = overlays[0].ColumnWidth,
            MinWidth = 160,
            RowDefinitions = new RowDefinitions("*,6,*"),
        };
        ApplyColumnHeight(stackArea, overlays[0].ColumnHeightFraction);

        var overlayColumn = new Grid();
        Grid.SetRow(overlayColumn, 0);
        stackArea.Children.Add(overlayColumn);
        RebuildColumnRows(overlayColumn, overlays);

        var heightHandle = CreateHandle(new Cursor(StandardCursorType.SizeNorthSouth), vertical: true);
        Grid.SetRow(heightHandle, 1);
        stackArea.Children.Add(heightHandle);
        AttachHeightHandleDrag(heightHandle, overlays, stackArea);

        var widthHandle = CreateHandle(new Cursor(StandardCursorType.SizeWestEast), vertical: false);
        widthHandle.Width = 6;
        AttachWidthHandleDrag(widthHandle, overlays, stackArea);

        var root = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,Auto") };
        Grid.SetColumn(widthHandle, 0);
        Grid.SetColumn(stackArea, 1);
        root.Children.Add(widthHandle);
        root.Children.Add(stackArea);

        return root;
    }

    private static Border CreateHandle(Cursor cursor, bool vertical)
    {
        var line = vertical
            ? new Border
            {
                Height = 1,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Center,
            }
            : new Border
            {
                Width = 1,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Stretch,
            };
        line.Classes.Add("mud-overlay-handle-line");

        var handle = new Border
        {
            Background = Brushes.Transparent,
            Cursor = cursor,
            Child = line,
        };
        handle.Classes.Add("mud-overlay-handle");
        return handle;
    }

    private static void ApplyColumnHeight(Grid stackArea, double fraction)
    {
        stackArea.RowDefinitions[0].Height = new GridLength(fraction, GridUnitType.Star);
        stackArea.RowDefinitions[2].Height = new GridLength(1 - fraction, GridUnitType.Star);
    }

    /// <summary>Dragging this column's left-edge handle resizes only that column, applied to every
    /// overlay sharing its <see cref="TerminalOverlayViewModel.ColumnIndex"/> so they stay in
    /// sync. Position is measured against this whole host (which never itself resizes from an
    /// internal drag) rather than the column being resized.</summary>
    private void AttachWidthHandleDrag(Border handle, IReadOnlyList<TerminalOverlayViewModel> overlays, Grid stackArea)
    {
        Point? dragStart = null;
        var startWidth = 0.0;

        handle.PointerPressed += (_, e) =>
        {
            if (!e.GetCurrentPoint(handle).Properties.IsLeftButtonPressed)
            {
                return;
            }

            dragStart = e.GetPosition(this);
            startWidth = overlays[0].ColumnWidth;
            e.Pointer.Capture(handle);
            e.Handled = true;
        };

        handle.PointerMoved += (_, e) =>
        {
            if (dragStart is not { } start)
            {
                return;
            }

            var deltaX = e.GetPosition(this).X - start.X;
            // Dragging left grows the column (its content sits to the handle's right); dragging
            // right shrinks it.
            var newWidth = startWidth - deltaX;
            foreach (var overlay in overlays)
            {
                overlay.ColumnWidth = newWidth;
            }

            stackArea.Width = overlays[0].ColumnWidth;
        };

        handle.PointerReleased += (_, e) =>
        {
            dragStart = null;
            e.Pointer.Capture(null);
        };
    }

    /// <summary>Dragging this column's height handle shrinks/grows how much of the Terminal's
    /// height it covers, applied to every overlay sharing its column so they stay in sync.</summary>
    private static void AttachHeightHandleDrag(
        Border handle, IReadOnlyList<TerminalOverlayViewModel> overlays, Grid stackArea)
    {
        Point? dragStart = null;
        var startFraction = 0.0;

        handle.PointerPressed += (_, e) =>
        {
            if (!e.GetCurrentPoint(handle).Properties.IsLeftButtonPressed)
            {
                return;
            }

            dragStart = e.GetPosition(stackArea);
            startFraction = overlays[0].ColumnHeightFraction;
            e.Pointer.Capture(handle);
            e.Handled = true;
        };

        handle.PointerMoved += (_, e) =>
        {
            if (dragStart is not { } start)
            {
                return;
            }

            var totalHeight = stackArea.Bounds.Height;
            if (totalHeight <= 0)
            {
                return;
            }

            var deltaY = e.GetPosition(stackArea).Y - start.Y;
            var newFraction = startFraction + deltaY / totalHeight;
            foreach (var overlay in overlays)
            {
                overlay.ColumnHeightFraction = newFraction;
            }

            ApplyColumnHeight(stackArea, overlays[0].ColumnHeightFraction);
        };

        handle.PointerReleased += (_, e) =>
        {
            dragStart = null;
            e.Pointer.Capture(null);
        };
    }

    private static void RebuildColumnRows(Grid column, IReadOnlyList<TerminalOverlayViewModel> overlays)
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
        IReadOnlyList<TerminalOverlayViewModel> columnOverlays,
        TerminalOverlayViewModel above,
        TerminalOverlayViewModel below)
    {
        var handle = CreateHandle(new Cursor(StandardCursorType.SizeNorthSouth), vertical: true);

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
            totalWeightAtDragStart = columnOverlays.Sum(o => o.HeightWeight);
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
            ReapplyRowWeights(column, columnOverlays);
        };

        handle.PointerReleased += (_, e) =>
        {
            dragStart = null;
            e.Pointer.Capture(null);
        };

        return handle;
    }

    /// <summary>Pushes each overlay's current <see cref="TerminalOverlayViewModel.HeightWeight"/>
    /// back onto its own row — mirrors <see cref="RebuildColumnRows"/>'s row order (card, splitter,
    /// card, ...), so overlay i always lives at row 2*i.</summary>
    private static void ReapplyRowWeights(Grid column, IReadOnlyList<TerminalOverlayViewModel> overlays)
    {
        for (var i = 0; i < overlays.Count; i++)
        {
            column.RowDefinitions[i * 2].Height = new GridLength(overlays[i].HeightWeight, GridUnitType.Star);
        }
    }
}

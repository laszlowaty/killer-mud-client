using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using MudClient.App.Controls;
using MudClient.App.ViewModels;

namespace MudClient.App.Views;

public partial class KilleropediaWorldMapView : UserControl
{
    private const double MinScale = 0.1;
    private const double MaxScale = 8.0;
    private const double WheelZoomFactor = 1.15;

    private double _scale = 1.0;
    private Vector _offset;
    private Point? _panPointerStart;
    private Vector _panOffsetStart;
    private KilleropediaViewModel? _viewModel;
    private string? _loadedImagePath;
    private bool _userAdjustedView;
    private bool _transformUpdatePending;

    public KilleropediaWorldMapView()
    {
        InitializeComponent();
        RenderOptions.SetBitmapInterpolationMode(MapCanvas, BitmapInterpolationMode.MediumQuality);
        MapViewport.PointerWheelChanged += OnViewportPointerWheelChanged;
        MapViewport.PointerPressed += OnViewportPointerPressed;
        MapViewport.PointerMoved += OnViewportPointerMoved;
        MapViewport.PointerReleased += OnViewportPointerReleased;
        MapViewport.SizeChanged += OnViewportSizeChanged;
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _viewModel = DataContext as KilleropediaViewModel;
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        LoadRegionImage();
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(KilleropediaViewModel.SelectedWorldMapRegion))
        {
            LoadRegionImage();
        }
    }

    private void LoadRegionImage()
    {
        var path = _viewModel?.SelectedWorldMapRegion?.ImagePath;
        if (path == _loadedImagePath)
        {
            return;
        }

        (MapCanvas.Source as Bitmap)?.Dispose();
        MapCanvas.Source = null;
        _loadedImagePath = path;

        if (path is not null && File.Exists(path))
        {
            try
            {
                MapCanvas.Source = new Bitmap(path);
            }
            catch (Exception exception) when (exception is IOException or ArgumentException)
            {
                MapCanvas.Source = null;
            }
        }

        ResetView();
    }

    private void OnResetViewClick(object? sender, RoutedEventArgs e) => ResetView();

    private void OnViewportSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (!_userAdjustedView)
        {
            ResetView();
        }
    }

    /// <summary>Fits the whole map inside the viewport and centers it.</summary>
    private void ResetView()
    {
        _userAdjustedView = false;
        var viewport = MapViewport.Bounds.Size;
        if (MapCanvas.Source is not { } source || viewport.Width <= 0 || viewport.Height <= 0)
        {
            _scale = 1.0;
            _offset = default;
            ApplyTransform();
            return;
        }

        var imageSize = source.Size;
        _scale = Math.Min(FitScale(), MaxScale);
        _offset = new Vector(
            (viewport.Width - imageSize.Width * _scale) / 2,
            (viewport.Height - imageSize.Height * _scale) / 2);
        ApplyTransform();
    }

    /// <summary>Scale at which the whole map exactly fits the viewport — the zoom-out floor.</summary>
    private double FitScale()
    {
        var viewport = MapViewport.Bounds.Size;
        if (MapCanvas.Source is not { } source || viewport.Width <= 0 || viewport.Height <= 0)
        {
            return MinScale;
        }

        return Math.Min(viewport.Width / source.Size.Width, viewport.Height / source.Size.Height);
    }

    private void OnViewportPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (MapCanvas.Source is null)
        {
            return;
        }

        var factor = e.Delta.Y > 0 ? WheelZoomFactor : 1.0 / WheelZoomFactor;
        var newScale = Math.Clamp(_scale * factor, Math.Min(FitScale(), MaxScale), MaxScale);
        if (Math.Abs(newScale - _scale) < double.Epsilon)
        {
            e.Handled = true;
            return;
        }

        if (Math.Abs(newScale - FitScale()) < 0.001)
        {
            // Reached max zoom-out: snap back to the fitted, centered view.
            ResetView();
            e.Handled = true;
            return;
        }

        // Zoom around the pointer: keep the image point under the cursor fixed.
        var cursor = e.GetPosition(MapViewport);
        var ratio = newScale / _scale;
        _offset = new Vector(
            cursor.X - (cursor.X - _offset.X) * ratio,
            cursor.Y - (cursor.Y - _offset.Y) * ratio);
        _scale = newScale;
        _userAdjustedView = true;
        ApplyTransform();
        e.Handled = true;
    }

    private void OnViewportPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (MapCanvas.Source is null || !e.GetCurrentPoint(MapViewport).Properties.IsLeftButtonPressed)
        {
            return;
        }

        _panPointerStart = e.GetPosition(MapViewport);
        _panOffsetStart = _offset;
        RenderOptions.SetBitmapInterpolationMode(MapCanvas, BitmapInterpolationMode.LowQuality);
        e.Pointer.Capture(MapViewport);
    }

    private void OnViewportPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_panPointerStart is not { } start)
        {
            return;
        }

        var position = e.GetPosition(MapViewport);
        _offset = new Vector(
            _panOffsetStart.X + position.X - start.X,
            _panOffsetStart.Y + position.Y - start.Y);
        _userAdjustedView = true;
        ScheduleTransform();
    }

    private void OnViewportPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _panPointerStart = null;
        RenderOptions.SetBitmapInterpolationMode(MapCanvas, BitmapInterpolationMode.MediumQuality);
        ScheduleTransform();
        e.Pointer.Capture(null);
    }

    /// <summary>
    /// Coalesces high-frequency pointer events so the large map bitmap receives at
    /// most one compositor transform update per rendered frame.
    /// </summary>
    internal void ScheduleTransform()
    {
        if (_transformUpdatePending)
        {
            return;
        }

        _transformUpdatePending = true;
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is not null)
        {
            topLevel.RequestAnimationFrame(_ => FlushScheduledTransform());
            return;
        }

        Dispatcher.UIThread.Post(FlushScheduledTransform, DispatcherPriority.Render);
    }

    private void FlushScheduledTransform()
    {
        _transformUpdatePending = false;
        ApplyTransform();
    }

    internal int TransformApplyCount { get; private set; }

    private void ApplyTransform()
    {
        MapCanvas.SetView(_scale, _offset);
        TransformApplyCount++;
    }
}

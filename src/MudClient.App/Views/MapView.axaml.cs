using Avalonia.Controls;
using Avalonia.Input;
using MudClient.App.ViewModels;

namespace MudClient.App.Views;

public sealed partial class MapView : UserControl
{
    private MapViewModel? _viewModel;

    public MapView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        MapControl.RoomClicked += OnRoomClicked;
        MapControl.ManualNavigationOccurred += OnManualNavigation;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _viewModel.CenterOnCurrentRoomRequested -= OnCenterRequested;
            _viewModel.ResetZoomRequested -= OnResetZoomRequested;
        }

        _viewModel = DataContext as MapViewModel;

        if (_viewModel is null)
        {
            return;
        }

        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        _viewModel.CenterOnCurrentRoomRequested += OnCenterRequested;
        _viewModel.ResetZoomRequested += OnResetZoomRequested;

        SyncControlFromViewModel();
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        SyncControlFromViewModel();
    }

    private void SyncControlFromViewModel()
    {
        if (_viewModel is null)
        {
            return;
        }

        MapControl.MapIndex = _viewModel.MapIndex;
        MapControl.Settings = _viewModel.Settings;
        MapControl.TextureCache = _viewModel.TextureCache;
        MapControl.RoomImages = _viewModel.RoomImages;
        MapControl.AreaId = _viewModel.SelectedArea?.Id ?? 0;
        MapControl.Z = _viewModel.SelectedZ;
        MapControl.CurrentRoom = _viewModel.CurrentRoom;
        MapControl.SelectedRoom = _viewModel.SelectedRoom;
    }

    private void OnRoomClicked(MudClient.Core.Map.MapRoom? room)
    {
        if (_viewModel is not null)
        {
            _viewModel.SelectedRoom = room;
        }
    }

    private void OnManualNavigation()
    {
        if (_viewModel is not null)
        {
            _viewModel.FollowPlayer = false;
        }
    }

    private void OnCenterRequested()
    {
        if (_viewModel?.CurrentRoom is { } room)
        {
            MapControl.CenterOnRoom(room);
        }
    }

    private void OnResetZoomRequested()
    {
        MapControl.ResetZoom();
    }
}

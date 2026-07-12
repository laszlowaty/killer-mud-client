using Avalonia.Controls;
using MudClient.App.ViewModels;

namespace MudClient.App.Views.Panels;

public sealed partial class MapPanelView : UserControl
{
    private MapViewModel? _viewModel;

    public MapPanelView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        MapControl.RoomClicked += OnRoomClicked;
        MapControl.RoomDoubleClicked += OnRoomDoubleClicked;
        MapControl.ManualNavigationOccurred += OnManualNavigation;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _viewModel.CenterOnCurrentRoomRequested -= OnCenterRequested;
            _viewModel.CenterOnRoomRequested -= OnCenterOnRoomRequested;
        }

        _viewModel = DataContext as MapViewModel;

        if (_viewModel is null)
        {
            return;
        }

        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        _viewModel.CenterOnCurrentRoomRequested += OnCenterRequested;
        _viewModel.CenterOnRoomRequested += OnCenterOnRoomRequested;

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
        MapControl.Route = _viewModel.RouteRooms;
        MapControl.IsSimpleMap = _viewModel.IsSimpleMap;
    }

    private void OnRoomDoubleClicked(MudClient.Core.Map.MapRoom room)
    {
        _viewModel?.NotifyRoomDoubleClicked(room);
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

    private void OnCenterOnRoomRequested(MudClient.Core.Map.MapRoom room)
    {
        MapControl.CenterOnRoom(room);
    }

}

using System.Text.Json;
using System.Reflection;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using MudClient.App.Controls;
using MudClient.App.ViewModels;
using MudClient.App.Views.Panels;
using MudClient.Core.Map;
using Xunit;

namespace MudClient.App.Tests;

[Collection(AvaloniaUiCollection.Name)]
public sealed class LordModeMapUiTests
{
    [AvaloniaFact]
    public void AreaChange_WithoutConnection_KeepsZSelectionTyped()
    {
        using var viewModel = new MapViewModel(AppContext.BaseDirectory, new GmcpLocationResolver());
        var firstArea = CreateArea(1, "Pierwszy obszar", 0);
        var secondArea = CreateArea(2, "Drugi obszar", 5);
        var index = new MapIndex(new MapDocument { Areas = [firstArea, secondArea] });
        typeof(MapViewModel).GetProperty(nameof(MapViewModel.MapIndex))!
            .SetValue(viewModel, index);
        viewModel.Areas.Add(firstArea);
        viewModel.Areas.Add(secondArea);
        viewModel.SelectedArea = firstArea;

        var panel = new MapPanelView { DataContext = viewModel };
        var window = new Window { Width = 800, Height = 600, Content = panel };

        try
        {
            window.Show();
            window.UpdateLayout();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();

            var mapMenuButton = panel.FindControl<Button>("MapMenuButton");
            Assert.NotNull(mapMenuButton);
            Assert.NotNull(mapMenuButton.Flyout);
            mapMenuButton.Flyout.ShowAt(mapMenuButton);
            Dispatcher.UIThread.RunJobs();

            var zSelector = panel.FindControl<ComboBox>("ZSelector");
            Assert.NotNull(zSelector);

            viewModel.SelectedArea = secondArea;
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();

            Assert.Same(secondArea, viewModel.SelectedArea);
            Assert.Equal(5, viewModel.SelectedZ);
            Assert.Same(viewModel, zSelector.DataContext);
            Assert.Equal(0, viewModel.SelectedZIndex);
            Assert.Equal(0, zSelector.SelectedIndex);
            Assert.Equal(5d, Assert.IsType<double>(zSelector.SelectedItem));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ContextMenu_InLordMode_ExposesGotoForSelectedRoom()
    {
        using var viewModel = new MapViewModel(AppContext.BaseDirectory, new GmcpLocationResolver());
        var room = new MapRoom
        {
            Id = 1,
            AreaId = 1,
            Name = "Sala prób",
            Coordinates = new MapCoordinates(0, 0, 0),
            UserData = new Dictionary<string, JsonElement>
            {
                ["vnum"] = JsonSerializer.SerializeToElement("6017"),
            },
        };
        var requests = new List<MapRoom>();
        viewModel.LordGotoRequested += requests.Add;
        viewModel.SelectedRoom = room;
        viewModel.LordModeEnabled = true;

        var panel = new MapPanelView { DataContext = viewModel };
        var window = new Window { Width = 800, Height = 600, Content = panel };

        try
        {
            window.Show();
            window.UpdateLayout();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();

            var map = panel.FindControl<WorldMapControl>("MapControl");
            Assert.NotNull(map);
            var contextMenu = Assert.IsType<ContextMenu>(map.ContextMenu);
            contextMenu.Open(map);
            Dispatcher.UIThread.RunJobs();

            var menuItem = Assert.Single(contextMenu.Items.OfType<MenuItem>());
            Assert.True(menuItem.IsVisible);
            Assert.Equal("Goto: Sala prób [6017]", menuItem.Header);
            Assert.NotNull(menuItem.Command);

            menuItem.Command.Execute(menuItem.CommandParameter);

            Assert.Equal([room], requests);
        }
        finally
        {
            window.Close();
        }
    }

    private static MapArea CreateArea(int id, string name, double z) => new()
    {
        Id = id,
        Name = name,
        Rooms =
        [
            new MapRoom
            {
                Id = id,
                AreaId = id,
                Name = $"Pokój {id}",
                Coordinates = new MapCoordinates(0, 0, z),
            },
        ],
    };
}

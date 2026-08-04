using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using MudClient.App.ViewModels;
using MudClient.App.Views.Panels;
using MudClient.Core.Gmcp;
using MudClient.Core.Map;

namespace MudClient.App.Tests;

/// <summary>Covers the compact room-info strip folded into the Map panel from the former
/// standalone "Pokój" panel — shows the room's details and image, but never its occupants.</summary>
[Collection(AvaloniaUiCollection.Name)]
public sealed class MapRoomInfoStripUiTests
{
    [AvaloniaFact]
    public void NoSelectedRoom_StripIsHidden()
    {
        using var viewModel = new MapViewModel(AppContext.BaseDirectory, new GmcpLocationResolver());
        var panel = new MapPanelView { DataContext = viewModel };
        var window = new Window { Width = 800, Height = 600, Content = panel };

        try
        {
            window.Show();
            window.UpdateLayout();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();

            var nameLabel = window.GetVisualDescendants().OfType<TextBlock>()
                .SingleOrDefault(text => text.Classes.Contains("mud-heading") && text.Text == "Sala prób");
            Assert.Null(nameLabel);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void SelectedRoom_ShowsDetailsAndImage_ButNeverOccupants()
    {
        using var viewModel = new MapViewModel(AppContext.BaseDirectory, new GmcpLocationResolver());
        var room = new MapRoom
        {
            Id = 1,
            AreaId = 1,
            Name = "Sala prób",
            Coordinates = new MapCoordinates(0, 0, 0),
            Environment = 2,
            Weight = 1.5,
            UserData = new Dictionary<string, JsonElement>
            {
                ["vnum"] = JsonSerializer.SerializeToElement("6017"),
                ["sector"] = JsonSerializer.SerializeToElement("miasto"),
            },
        };
        viewModel.SelectedRoom = room;

        var panel = new MapPanelView { DataContext = viewModel };
        var window = new Window { Width = 800, Height = 600, Content = panel };

        try
        {
            window.Show();
            window.UpdateLayout();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();

            var nameLabel = window.GetVisualDescendants().OfType<TextBlock>()
                .Single(text => text.Classes.Contains("mud-heading") && text.Text == "Sala prób");
            Assert.True(nameLabel.IsEffectivelyVisible);

            var vnumLabel = window.GetVisualDescendants().OfType<TextBlock>()
                .Single(text => text.Text == "Vnum: 6017");
            Assert.True(vnumLabel.IsEffectivelyVisible);

            var image = window.GetVisualDescendants().OfType<Image>().Single();
            Assert.True(image.IsEffectivelyVisible);

            // The former standalone panel listed room occupants ("Osoby w pokoju") — the strip
            // folded into Map must not carry that over.
            Assert.DoesNotContain(
                window.GetVisualDescendants().OfType<TextBlock>(),
                text => text.Text == "Osoby w pokoju");
        }
        finally
        {
            window.Close();
        }
    }
}

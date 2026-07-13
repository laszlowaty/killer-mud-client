using MudClient.Core.Map;

namespace MudClient.Core.Tests;

public sealed class MapLoaderTests
{
    private static async Task<MapLoadResult> LoadSampleAsync()
    {
        var path = Path.GetTempFileName();
        await File.WriteAllTextAsync(path, MapTestFixture.SampleJson);

        try
        {
            var loader = new MapLoader();
            return await loader.LoadAsync(path);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task DeserializesAreasAndRooms()
    {
        var result = await LoadSampleAsync();

        Assert.Equal(2, result.Document.Areas.Count);
        Assert.Equal(5, result.Document.Areas[0].Rooms.Count);
    }

    [Fact]
    public async Task IgnoresLabelsAndUnknownFields()
    {
        var result = await LoadSampleAsync();

        var room3 = result.Document.Areas[0].Rooms.Single(r => r.Id == 3);
        Assert.Null(room3.Name);
    }

    [Fact]
    public async Task SkipsRoomsWithInvalidCoordinatesAndReportsWarning()
    {
        var result = await LoadSampleAsync();

        Assert.DoesNotContain(result.Document.Areas[0].Rooms, r => r.Id == 6);
        Assert.NotEmpty(result.Warnings);
    }

    [Fact]
    public async Task ParsesVnumAsStringOrNumber()
    {
        var result = await LoadSampleAsync();

        var room1 = result.Document.Areas[0].Rooms.Single(r => r.Id == 1);
        var room2 = result.Document.Areas[0].Rooms.Single(r => r.Id == 2);

        Assert.Equal("1001", room1.Vnum);
        Assert.Equal("1002", room2.Vnum);
    }

    [Fact]
    public async Task ThrowsMapLoadExceptionWhenFileMissing()
    {
        var loader = new MapLoader();

        await Assert.ThrowsAsync<MapLoadException>(
            () => loader.LoadAsync(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json")));
    }

    [Fact]
    public async Task ThrowsMapLoadExceptionForInvalidJson()
    {
        var path = Path.GetTempFileName();
        await File.WriteAllTextAsync(path, "{ not valid json");

        try
        {
            var loader = new MapLoader();
            await Assert.ThrowsAsync<MapLoadException>(() => loader.LoadAsync(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task UnknownExitDirectionDoesNotThrow()
    {
        var result = await LoadSampleAsync();

        var room = result.Document.Areas[1].Rooms.Single(r => r.Id == 101);
        Assert.Single(room.Exits);
        Assert.Equal("unknown-direction", room.Exits[0].Name);
        Assert.True(room.Exits[0].HasDoor);
    }

    [Fact]
    public async Task ParsesNonStringSymbolValuesWithoutThrowing()
    {
        var path = Path.GetTempFileName();
        var json = MapTestFixture.SampleJson.Replace(
            "\"coordinates\": [0, 0, 1],",
            "\"coordinates\": [0, 0, 1],\n                \"symbol\": { \"kind\": \"special\" },",
            StringComparison.Ordinal);

        await File.WriteAllTextAsync(path, json);

        try
        {
            var loader = new MapLoader();
            var result = await loader.LoadAsync(path);

            var room = result.Document.Areas[1].Rooms.Single(r => r.Id == 101);
            Assert.Equal("{ \"kind\": \"special\" }", room.Symbol);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task CrossLevelExit_UsesLegacyCommand_WhenEmbeddedTargetIdWasRenumbered()
    {
        const string json = """
        {
          "areas": [
            {
              "id": 1,
              "name": "Stary Kontynent",
              "rooms": [
                {
                  "id": 358,
                  "coordinates": [0, 0, 0],
                  "exits": [ { "exitId": 7837, "name": "southwest" } ],
                  "userData": {
                    "sw": "{\"id\":\"16125\",\"command\":\"down\"}",
                    "vnum": "24951"
                  }
                },
                {
                  "id": 7837,
                  "coordinates": [0, 0, -1],
                  "exits": [],
                  "userData": { "vnum": "45820" }
                }
              ]
            }
          ]
        }
        """;
        var path = Path.GetTempFileName();
        await File.WriteAllTextAsync(path, json);

        try
        {
            var result = await new MapLoader().LoadAsync(path);
            var route = new MapPathfinder(new MapIndex(result.Document)).FindPath(358, 7837);

            Assert.NotNull(route);
            Assert.Equal("down", Assert.Single(route.Steps).Command);
            Assert.Equal(-1, route.To.Coordinates.Z);
        }
        finally
        {
            File.Delete(path);
        }
    }
}

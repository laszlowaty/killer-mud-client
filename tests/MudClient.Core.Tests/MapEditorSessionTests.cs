using System.Text.Json;
using MudClient.Core.Map;

namespace MudClient.Core.Tests;

public sealed class MapEditorSessionTests
{
    [Fact]
    public void SuccessfulMoveToUnknownVnum_CreatesRoomsAndTwoWayConnection()
    {
        var editor = CreateEditor();
        editor.ProcessSnapshot(Snapshot("100", ("E", null)));
        Assert.True(editor.Start("100"));

        var decision = editor.PrepareManualCommand("e");
        var changed = editor.ProcessSnapshot(Snapshot("200", ("W", null)));

        Assert.True(decision.Allow);
        Assert.True(changed);
        Assert.True(editor.IsDirty);
        var rooms = editor.Document.Areas.Single().Rooms;
        var origin = Assert.Single(rooms, room => room.Vnum == "100");
        var target = Assert.Single(rooms, room => room.Vnum == "200");
        Assert.Equal(new MapCoordinates(2, 0, 0), target.Coordinates);
        Assert.Contains(origin.Exits, exit => exit.ExitId == target.Id);
        Assert.Contains(target.Exits, exit => exit.ExitId == origin.Id);
    }

    [Fact]
    public void RepeatedOriginVnum_TreatsMovementAsFailed()
    {
        var editor = CreateEditor();
        var origin = Snapshot("100", ("E", null));
        editor.ProcessSnapshot(origin);
        editor.Start("100");
        editor.PrepareManualCommand("e");

        var changed = editor.ProcessSnapshot(origin);

        Assert.True(changed);
        Assert.Single(editor.Document.Areas.Single().Rooms);
        Assert.Contains("nie zmienił pokoju", editor.Status);
    }

    [Fact]
    public void ActiveMapping_BlocksUnknownAndSecondOutstandingCommand()
    {
        var editor = CreateEditor();
        editor.ProcessSnapshot(Snapshot("100", ("E", null)));
        editor.Start("100");

        Assert.False(editor.PrepareManualCommand("kill szczur").Allow);
        Assert.True(editor.PrepareManualCommand("east").Allow);
        Assert.False(editor.PrepareManualCommand("e").Allow);
        Assert.True(editor.IsAwaitingRoomInfo);
    }

    [Fact]
    public void UnsolicitedKnownVnum_ResynchronizesMappingAfterTeleport()
    {
        var editor = CreateEditor();
        editor.ProcessSnapshot(Snapshot("100", ("E", null)));
        editor.Start("100");
        editor.PrepareManualCommand("e");
        editor.ProcessSnapshot(Snapshot("200", ("W", null)));

        var changed = editor.ProcessSnapshot(Snapshot("100", ("E", null)));

        Assert.True(changed);
        Assert.True(editor.IsMapping);
        Assert.False(editor.IsAwaitingRoomInfo);
        Assert.Equal(2, editor.Document.Areas.Single().Rooms.Count);
        Assert.Contains("Wykryto teleport z vnum 200 do 100", editor.Status);
    }

    [Fact]
    public void UnsolicitedUnknownVnum_StopsMappingWithoutCreatingRoom()
    {
        var editor = CreateEditor();
        editor.ProcessSnapshot(Snapshot("100"));
        editor.Start("100");

        var changed = editor.ProcessSnapshot(Snapshot("999"));

        Assert.False(changed);
        Assert.False(editor.IsMapping);
        Assert.Single(editor.Document.Areas.Single().Rooms);
        Assert.Contains("teleport", editor.Status);
        Assert.Contains("Mapowanie zatrzymano", editor.Status);
    }

    [Fact]
    public void Undo_RestoresDocumentBeforeLastMovement()
    {
        var editor = CreateEditor();
        editor.ProcessSnapshot(Snapshot("100", ("E", null)));
        editor.Start("100");
        editor.PrepareManualCommand("e");
        editor.ProcessSnapshot(Snapshot("200", ("W", null)));

        Assert.True(editor.Undo());

        Assert.Single(editor.Document.Areas.Single().Rooms);
        Assert.Null(editor.Document.Areas.Single().Rooms.Single().Exits.SingleOrDefault());
    }

    [Fact]
    public void NewAreaAndSpecialMovement_CreateFirstRoomInSelectedArea()
    {
        var editor = CreateEditor();
        editor.ProcessSnapshot(Snapshot("100"));
        Assert.True(editor.CreateArea("Nowa kraina"));
        Assert.True(editor.Start("100"));

        var decision = editor.PrepareSpecialMovement("e", "przecisnij");
        var changed = editor.ProcessSnapshot(Snapshot("200"));

        Assert.True(decision.Allow);
        Assert.True(changed);
        var area = Assert.Single(editor.Document.Areas, item => item.Name == "Nowa kraina");
        var target = Assert.Single(area.Rooms);
        Assert.Equal("200", target.Vnum);
        Assert.Equal(new MapCoordinates(0, 0, 0), target.Coordinates);
        var origin = Assert.Single(editor.Document.Areas.Single(item => item.Id == 1).Rooms);
        Assert.Contains(origin.Exits, exit => exit.ExitId == target.Id && exit.Name == "przecisnij");
    }

    [Fact]
    public void ReassignMode_MovesKnownRoomsButKeepsMappingStartInOriginalArea()
    {
        var editor = CreateEditor();
        editor.ProcessSnapshot(Snapshot("100", ("E", null)));
        editor.Start("100");
        editor.PrepareManualCommand("e");
        editor.ProcessSnapshot(Snapshot("200", ("W", null), ("N", null)));
        editor.PrepareManualCommand("n");
        editor.ProcessSnapshot(Snapshot("300", ("S", null)));
        editor.Stop();
        editor.ProcessSnapshot(Snapshot("100", ("E", null)));
        Assert.True(editor.CreateArea("Nowa kraina"));
        Assert.True(editor.SetMoveKnownRoomsToTargetArea(true));
        Assert.True(editor.Start("100"));

        editor.PrepareManualCommand("e");
        Assert.True(editor.ProcessSnapshot(Snapshot("200", ("W", null), ("N", null))));
        editor.PrepareManualCommand("n");
        Assert.True(editor.ProcessSnapshot(Snapshot("300", ("S", null))));

        var targetArea = Assert.Single(editor.Document.Areas, area => area.Name == "Nowa kraina");
        Assert.Equal(2, targetArea.Rooms.Count);
        Assert.Equal(new MapCoordinates(0, 0, 0), targetArea.Rooms.Single(room => room.Vnum == "200").Coordinates);
        Assert.Equal(new MapCoordinates(0, 2, 0), targetArea.Rooms.Single(room => room.Vnum == "300").Coordinates);
        Assert.Single(editor.Document.Areas.Single(area => area.Name == "Test").Rooms);
        Assert.Contains("Przeniesiono vnum 300", editor.Status);

        editor.PrepareManualCommand("s");
        editor.ProcessSnapshot(Snapshot("200", ("W", null), ("N", null)));
        editor.PrepareManualCommand("w");
        editor.ProcessSnapshot(Snapshot("100", ("E", null)));

        Assert.Single(editor.Document.Areas.Single(area => area.Name == "Test").Rooms);
        for (var i = 0; i < 4; i++)
        {
            Assert.True(editor.Undo());
        }
        Assert.Equal(3, editor.Document.Areas.Single(area => area.Name == "Test").Rooms.Count);
        Assert.Empty(editor.Document.Areas.Single(area => area.Name == "Nowa kraina").Rooms);
    }

    [Fact]
    public void StartFromUnknownCurrentVnum_CreatesStartingRoomInTargetArea()
    {
        var editor = CreateEditor();
        editor.ProcessSnapshot(Snapshot("3921", ("E", null)));
        Assert.True(editor.CreateArea("Nowa kraina"));

        Assert.True(editor.Start("3921"));

        var area = Assert.Single(editor.Document.Areas, item => item.Name == "Nowa kraina");
        var startingRoom = Assert.Single(area.Rooms);
        Assert.Equal("3921", startingRoom.Vnum);
        Assert.Equal(new MapCoordinates(0, 0, 0), startingRoom.Coordinates);
        Assert.True(editor.IsMapping);
        Assert.Contains("Utworzono pokój startowy", editor.Status);

        Assert.True(editor.PrepareManualCommand("e").Allow);
        Assert.True(editor.ProcessSnapshot(Snapshot("3922", ("W", null))));
        Assert.Equal(2, editor.Document.Areas.Single(item => item.Name == "Nowa kraina").Rooms.Count);
    }

    [Fact]
    public void SymbolLabelForgetAndUndo_UpdateCurrentRoomReversibly()
    {
        var editor = CreateEditor();
        editor.ProcessSnapshot(Snapshot("100"));

        Assert.True(editor.SetCurrentRoomSymbol("!!"));
        Assert.True(editor.AddLabel("## Niebezpieczne miejsce!"));
        Assert.True(editor.ForgetCurrentRoom());

        var forgotten = editor.Document.Areas.Single().Rooms.Single();
        Assert.Null(forgotten.Vnum);
        Assert.Null(forgotten.Symbol);
        var label = Assert.Single(editor.Document.Areas.Single().Labels);
        Assert.Equal("☠ Niebezpieczne miejsce", label.Text);
        Assert.Equal(30, label.FontSize);

        Assert.True(editor.Undo());
        var restored = editor.Document.Areas.Single().Rooms.Single();
        Assert.Equal("100", restored.Vnum);
        Assert.Equal("‼", restored.Symbol);
    }

    [Fact]
    public void RemoveSpecialExit_RemovesGraphEdgeAndMetadata()
    {
        var editor = CreateEditor();
        editor.ProcessSnapshot(Snapshot("100"));
        editor.Start("100");
        editor.PrepareSpecialMovement("e", "przecisnij");
        editor.ProcessSnapshot(Snapshot("200"));
        editor.ProcessSnapshot(Snapshot("100"));

        Assert.True(editor.RemoveSpecialExit("e"));

        var origin = editor.Document.Areas.Single().Rooms.Single(room => room.Vnum == "100");
        Assert.Empty(origin.Exits);
        Assert.False(origin.UserData!.ContainsKey("e"));
    }

    [Fact]
    public void Validate_ReportsDuplicateVnumAndBrokenExit()
    {
        var vnum = JsonSerializer.SerializeToElement("100");
        var editor = new MapEditorSession(new MapDocument
        {
            Areas =
            [
                new MapArea
                {
                    Id = 1,
                    Name = "Test",
                    Rooms =
                    [
                        new MapRoom
                        {
                            Id = 1,
                            AreaId = 1,
                            Coordinates = new MapCoordinates(0, 0, 0),
                            UserData = new Dictionary<string, JsonElement> { ["vnum"] = vnum },
                            Exits = [new MapExit { ExitId = 999, Name = "north" }],
                        },
                        new MapRoom
                        {
                            Id = 2,
                            AreaId = 1,
                            Coordinates = new MapCoordinates(1, 0, 0),
                            UserData = new Dictionary<string, JsonElement> { ["vnum"] = vnum },
                        },
                    ],
                },
            ],
        });

        var issues = editor.Validate();

        Assert.Equal(2, issues.Count);
        Assert.Contains(issues, issue => issue.Contains("występuje 2 razy"));
        Assert.Contains(issues, issue => issue.Contains("brakującego pokoju 999"));
    }

    [Fact]
    public void ConflictResolutionUseGmcp_ReplacesOldTwoWayConnection()
    {
        var editor = CreateEditor();
        editor.ProcessSnapshot(Snapshot("100", ("E", null)));
        editor.Start("100");
        editor.PrepareManualCommand("e");
        editor.ProcessSnapshot(Snapshot("200", ("W", null)));
        editor.ProcessSnapshot(Snapshot("100", ("E", null)));
        editor.PrepareManualCommand("e");

        Assert.False(editor.ProcessSnapshot(Snapshot("300", ("W", null))));
        Assert.True(editor.HasConflict);
        Assert.Contains("Konflikt", editor.Status);

        Assert.True(editor.ResolveConflictUseGmcp());

        Assert.False(editor.HasConflict);
        var rooms = editor.Document.Areas.Single().Rooms;
        var origin = Assert.Single(rooms, room => room.Vnum == "100");
        var oldTarget = Assert.Single(rooms, room => room.Vnum == "200");
        var newTarget = Assert.Single(rooms, room => room.Vnum == "300");
        Assert.Contains(origin.Exits, exit => exit.ExitId == newTarget.Id);
        Assert.DoesNotContain(origin.Exits, exit => exit.ExitId == oldTarget.Id);
        Assert.DoesNotContain(oldTarget.Exits, exit => exit.ExitId == origin.Id);
    }

    [Fact]
    public void ConflictResolutionKeepMap_DoesNotMutateDocument()
    {
        var editor = CreateEditor();
        editor.ProcessSnapshot(Snapshot("100", ("E", null)));
        editor.Start("100");
        editor.PrepareManualCommand("e");
        editor.ProcessSnapshot(Snapshot("200", ("W", null)));
        editor.ProcessSnapshot(Snapshot("100", ("E", null)));
        editor.PrepareManualCommand("e");
        var before = editor.Document;
        editor.ProcessSnapshot(Snapshot("300"));

        Assert.True(editor.ResolveConflictKeepMap());

        Assert.Same(before, editor.Document);
        Assert.False(editor.HasConflict);
        Assert.DoesNotContain(editor.Document.Areas.Single().Rooms, room => room.Vnum == "300");
    }

    [Fact]
    public void DocumentDiffer_ReportsSemanticChangesByKind()
    {
        var baseline = CreateEditor().Document;
        var editor = CreateEditor();
        editor.ProcessSnapshot(Snapshot("100"));
        editor.SetCurrentRoomSymbol("!");
        editor.AddLabel("Nowa etykieta");
        editor.CreateArea("Nowy obszar");

        var diff = MapDocumentDiffer.Compare(baseline, editor.Document);

        Assert.Equal(1, diff.AddedAreas);
        Assert.Equal(1, diff.ChangedRooms);
        Assert.Equal(1, diff.AddedLabels);
        Assert.True(diff.HasChanges);
    }

    [Fact]
    public void RoomAndLabelEditing_AreUndoableAndRedoable()
    {
        var editor = CreateEditor();
        editor.ProcessSnapshot(Snapshot("100"));

        Assert.True(editor.SetCurrentRoomName("Nowa nazwa"));
        Assert.True(editor.SetCurrentRoomSector("gory"));
        Assert.True(editor.SetCurrentRoomWeight(2.5));
        Assert.True(editor.MoveCurrentRoom(new MapCoordinates(4, 6, 1)));
        Assert.True(editor.AddLabel("Pierwsza"));
        var label = Assert.Single(editor.ShowCurrentAreaLabels());
        Assert.True(editor.SetLabelText(label.Id, "Zmieniona"));
        Assert.True(editor.RemoveLabel(label.Id));

        var room = editor.Document.Areas.Single().Rooms.Single();
        Assert.Equal("Nowa nazwa", room.Name);
        Assert.Equal("gory", room.Sector);
        Assert.Equal(2.5, room.Weight);
        Assert.Equal(new MapCoordinates(4, 6, 1), room.Coordinates);
        Assert.Empty(editor.Document.Areas.Single().Labels);

        Assert.True(editor.Undo());
        Assert.Single(editor.Document.Areas.Single().Labels);
        Assert.True(editor.CanRedo);
        Assert.True(editor.Redo());
        Assert.Empty(editor.Document.Areas.Single().Labels);
    }

    [Fact]
    public void MoveCurrentRoom_RejectsOccupiedCoordinates()
    {
        var editor = CreateEditor();
        editor.ProcessSnapshot(Snapshot("100", ("E", null)));
        editor.Start("100");
        editor.PrepareManualCommand("e");
        editor.ProcessSnapshot(Snapshot("200"));
        editor.ProcessSnapshot(Snapshot("100"));

        Assert.False(editor.MoveCurrentRoom(new MapCoordinates(2, 0, 0)));
        Assert.Contains("już zajęte", editor.Status);
    }

    [Fact]
    public async Task Writer_RoundTripsEditedDocumentThroughMapLoader()
    {
        var editor = CreateEditor();
        editor.ProcessSnapshot(Snapshot("100", ("E", null)));
        editor.Start("100");
        editor.PrepareManualCommand("e");
        editor.ProcessSnapshot(Snapshot("200", ("W", null)));
        var directory = Path.Combine(Path.GetTempPath(), "KillerMudClient_MapWriterTests_" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "world-map.json");

        try
        {
            await new MapWriter().SaveAsync(editor.Document, path);
            var loaded = await new MapLoader().LoadAsync(path);

            Assert.Equal(2, loaded.Document.Areas.Single().Rooms.Count);
            Assert.NotNull(new MapIndex(loaded.Document).FindFirstRoomByVnum("200"));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Writer_WithMudletBaseline_PreservesUnknownMapFields()
    {
        var editor = CreateEditor();
        var originalRoom = editor.Document.Areas.Single().Rooms.Single();
        var document = new MapDocument
        {
            Areas =
            [
                new MapArea
                {
                    Id = 1,
                    Name = "Test",
                    Labels =
                    [
                        new MapLabel
                        {
                            Id = 7,
                            AreaId = 1,
                            Text = "etykieta",
                            Coordinates = new MapCoordinates(0, 1, 0),
                        },
                    ],
                    Rooms =
                    [
                        new MapRoom
                        {
                            Id = originalRoom.Id,
                            AreaId = originalRoom.AreaId,
                            Name = originalRoom.Name,
                            Coordinates = originalRoom.Coordinates,
                            UserData = originalRoom.UserData,
                            Exits = [new MapExit { ExitId = 9, Name = "north" }],
                        },
                    ],
                },
            ],
        };
        var directory = Path.Combine(Path.GetTempPath(), "KillerMudClient_MapWriterTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var baseline = Path.Combine(directory, "baseline.json");
        var output = Path.Combine(directory, "world-map.json");
        await File.WriteAllTextAsync(
            baseline,
            """
            {
              "formatVersion": 1,
              "customEnvColors": [{"id":100,"color24RGB":[1,2,3]}],
              "areas": [{
                "id": 1,
                "name": "Test",
                "labels": [{"id":7,"text":"etykieta","coordinates":[0,1,0],"image":["png-data"]}],
                "userData": {"zoom":"20"},
                "rooms": [{
                  "id": 1,
                  "coordinates": [0,0,0],
                  "locked": true,
                  "stubExits": ["n"],
                  "exits": [{"exitId":9,"name":"north","customLine":{"color":"red"}}],
                  "userData": {"vnum":"100"}
                }]
              }]
            }
            """);

        try
        {
            await new MapWriter().SaveAsync(document, output, baselinePath: baseline);
            using var saved = JsonDocument.Parse(await File.ReadAllTextAsync(output));
            var root = saved.RootElement;
            var area = root.GetProperty("areas")[0];
            var room = area.GetProperty("rooms")[0];

            Assert.Equal(1, root.GetProperty("formatVersion").GetInt32());
            Assert.Equal(7, area.GetProperty("labels")[0].GetProperty("id").GetInt32());
            Assert.Equal("png-data", area.GetProperty("labels")[0].GetProperty("image")[0].GetString());
            Assert.Equal("20", area.GetProperty("userData").GetProperty("zoom").GetString());
            Assert.True(room.GetProperty("locked").GetBoolean());
            Assert.Equal("n", room.GetProperty("stubExits")[0].GetString());
            Assert.Equal("red", room.GetProperty("exits")[0].GetProperty("customLine").GetProperty("color").GetString());

            var loaded = await new MapLoader().LoadAsync(output);
            var loadedLabel = Assert.Single(loaded.Document.Areas.Single().Labels);
            Assert.Equal("etykieta", loadedLabel.Text);
            Assert.Equal(new MapCoordinates(0, 1, 0), loadedLabel.Coordinates);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static MapEditorSession CreateEditor()
    {
        var vnum = JsonSerializer.SerializeToElement("100");
        return new MapEditorSession(new MapDocument
        {
            Areas =
            [
                new MapArea
                {
                    Id = 1,
                    Name = "Test",
                    Rooms =
                    [
                        new MapRoom
                        {
                            Id = 1,
                            AreaId = 1,
                            Name = "Początek",
                            Coordinates = new MapCoordinates(0, 0, 0),
                            UserData = new Dictionary<string, JsonElement> { ["vnum"] = vnum },
                        },
                    ],
                },
            ],
        });
    }

    private static RoomSnapshot Snapshot(string vnum, params (string Direction, string? Name)[] exits) =>
        new(
            vnum,
            "Pokój " + vnum,
            "droga",
            exits.Select(exit => new RoomSnapshotExit(exit.Direction, exit.Name, false, false)).ToArray());
}

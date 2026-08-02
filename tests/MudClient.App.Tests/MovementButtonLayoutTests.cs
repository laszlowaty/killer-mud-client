using MudClient.App.ViewModels;
using MudClient.Core.Map;

namespace MudClient.App.Tests;

public sealed class MovementButtonLayoutTests
{
    [Fact]
    public void Create_WithoutNamedExits_ReturnsDefaultCrossCommands()
    {
        var layout = MovementButtonLayout.Create(
        [
            new RoomExitInfo("n", null, false, false),
            new RoomExitInfo("e", "east", false, false),
        ]);

        Assert.Equal(new MovementButtonState("n", "n"), layout.North);
        Assert.Equal(new MovementButtonState("s", "s"), layout.South);
        Assert.Equal(new MovementButtonState("w", "w"), layout.West);
        Assert.Equal(new MovementButtonState("e", "e"), layout.East);
        Assert.Equal(new MovementButtonState("up", "up"), layout.Up);
        Assert.Equal(new MovementButtonState("down", "down"), layout.Down);
    }

    [Fact]
    public void Create_NamedExit_ReplacesLabelAndCommandInItsDirectionSlot()
    {
        var layout = MovementButtonLayout.Create(
        [
            new RoomExitInfo("e", "  karczma  ", false, false),
            new RoomExitInfo("up", "żółta góra", false, false),
        ]);

        Assert.Equal(new MovementButtonState("karczma", "karczma"), layout.East);
        Assert.Equal(new MovementButtonState("żółta góra", "zolta gora"), layout.Up);
        Assert.Equal(new MovementButtonState("n", "n"), layout.North);
    }

    [Fact]
    public void Create_ClosedExits_OpenBeforeMoving()
    {
        var layout = MovementButtonLayout.Create(
        [
            new RoomExitInfo("w", null, true, true),
            new RoomExitInfo("e", "żółta brama", true, true),
        ]);

        Assert.Equal(new MovementButtonState("w", "open w", "w"), layout.West);
        Assert.Equal(
            new MovementButtonState(
                "żółta brama",
                "open \"zolta brama\"",
                "zolta brama"),
            layout.East);
    }

    [Fact]
    public void MarkOpened_ChangesOnlyMatchingButtonToMovementCommand()
    {
        var layout = MovementButtonLayout.Create(
        [
            new RoomExitInfo("w", null, true, true),
            new RoomExitInfo("e", "karczma", true, true),
        ]);

        var opened = layout.MarkOpened("open w");
        var namedOpened = layout.MarkOpened("open \"karczma\"");

        Assert.Equal(new MovementButtonState("w", "w"), opened.West);
        Assert.Equal(
            new MovementButtonState(
                "karczma",
                "open \"karczma\"",
                "karczma"),
            opened.East);
        Assert.Equal(
            new MovementButtonState("karczma", "karczma"),
            namedOpened.East);
    }
}

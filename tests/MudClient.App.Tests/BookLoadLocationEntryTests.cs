using MudClient.App.Models;

namespace MudClient.App.Tests;

public sealed class BookLoadLocationEntryTests
{
    [Theory]
    [InlineData("na mobie: czarnoksieznik Zeerith'din (Podmrok gobliny)")]
    [InlineData("na mobie: Feezin (+# Krypta Kamienia)")]
    public void Parse_MobLocationWithNoVnum_HasNoRoomLocation(string text)
    {
        var entry = BookLoadLocationEntry.Parse(text);

        Assert.Equal(text, entry.Text);
        Assert.Null(entry.RoomVnum);
        Assert.False(entry.HasRoomLocation);
    }

    [Theory]
    [InlineData("w pokoju: Biblioteka (vnum 1234)", "1234")]
    [InlineData("w pokoju: Biblioteka (vnum: 1234)", "1234")]
    [InlineData("VNUM 5678", "5678")]
    public void Parse_LocationWithVnum_ExtractsIt(string text, string expectedVnum)
    {
        var entry = BookLoadLocationEntry.Parse(text);

        Assert.Equal(expectedVnum, entry.RoomVnum);
        Assert.True(entry.HasRoomLocation);
    }
}

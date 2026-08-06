using MudClient.Core.Gmcp;

namespace MudClient.Core.Tests;

public sealed class WorldStateResolverTests
{
    private readonly WorldStateResolver _resolver = new();

    [Fact]
    public void Process_MudTimeInfo_RaisesTimeChangedWithAllFields()
    {
        WorldTimeUpdate? update = null;
        _resolver.TimeChanged += u => update = u;

        _resolver.Process(new GmcpMessage(
            "Mud.TimeInfo",
            """{ "day": 14, "dayname": "Łowów", "era": "Pierwsza Era Magicznych Portali", "month": "Wiosennego Brzasku", "time": 14, "timename": "godzina czternasta", "year": 59 }"""));

        Assert.NotNull(update);
        Assert.Equal(14, update!.Day);
        Assert.Equal("Łowów", update.DayName);
        Assert.Equal("Pierwsza Era Magicznych Portali", update.Era);
        Assert.Equal("Wiosennego Brzasku", update.Month);
        Assert.Equal(14, update.Time);
        Assert.Equal("godzina czternasta", update.TimeName);
        Assert.Equal(59, update.Year);
    }

    [Fact]
    public void Process_MudWeather_RaisesWeatherChangedWithAllFields()
    {
        WorldWeatherUpdate? update = null;
        _resolver.WeatherChanged += u => update = u;

        _resolver.Process(new GmcpMessage(
            "Mud.Weather",
            """{ "sky": "pochmurne", "wind": "wieje mocno zimny, północny wiatr" }"""));

        Assert.NotNull(update);
        Assert.Equal("pochmurne", update!.Sky);
        Assert.Equal("wieje mocno zimny, północny wiatr", update.Wind);
    }

    [Fact]
    public void Process_UnrelatedPackage_DoesNotRaiseEvents()
    {
        var raised = false;
        _resolver.TimeChanged += _ => raised = true;
        _resolver.WeatherChanged += _ => raised = true;

        _resolver.Process(new GmcpMessage("Char.Vitals", """{ "hp": 1 }"""));

        Assert.False(raised);
    }

    [Fact]
    public void Process_MalformedJson_IsIgnored()
    {
        var raised = false;
        _resolver.TimeChanged += _ => raised = true;

        _resolver.Process(new GmcpMessage("Mud.TimeInfo", "{ not json"));

        Assert.False(raised);
    }
}

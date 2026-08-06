using CommunityToolkit.Mvvm.ComponentModel;

namespace MudClient.App.Models;

/// <summary>
/// In-game date/time and weather, live from GMCP (Mud.TimeInfo / Mud.Weather).
/// </summary>
public sealed partial class WorldTimeWeather : ObservableObject
{
    [ObservableProperty]
    private int _day;

    [ObservableProperty]
    private string _dayName = "—";

    [ObservableProperty]
    private string _era = "—";

    [ObservableProperty]
    private string _month = "—";

    [ObservableProperty]
    private int _time;

    [ObservableProperty]
    private string _timeName = "—";

    [ObservableProperty]
    private int _year;

    [ObservableProperty]
    private string _sky = "—";

    [ObservableProperty]
    private string _wind = "—";
}

using CommunityToolkit.Mvvm.ComponentModel;

namespace MudClient.App.Models;

/// <summary>
/// Character vitals and stats. HP/MV/level/name/sex/position come live from
/// GMCP (Char.Vitals); the remaining stats are placeholders until the server
/// exposes them.
/// </summary>
public sealed partial class CharacterVitals : ObservableObject
{
    [ObservableProperty]
    private string _name = "—";

    [ObservableProperty]
    private string _sexDisplay = "—";

    [ObservableProperty]
    private string _positionDisplay = "—";

    [ObservableProperty]
    private int _hitPoints = 120;

    [ObservableProperty]
    private int _maxHitPoints = 120;

    [ObservableProperty]
    private int _spellPoints = 80;

    [ObservableProperty]
    private int _maxSpellPoints = 80;

    [ObservableProperty]
    private int _endurancePoints = 100;

    [ObservableProperty]
    private int _maxEndurancePoints = 100;

    [ObservableProperty]
    private int _experience = 4500;

    [ObservableProperty]
    private int _experienceToLevel = 8000;

    [ObservableProperty]
    private int _level = 5;

    [ObservableProperty]
    private string _className = "Wojownik";

    [ObservableProperty]
    private string _race = "Człowiek";
}

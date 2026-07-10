using CommunityToolkit.Mvvm.ComponentModel;

namespace MudClient.App.Models;

/// <summary>
/// Mock character vitals and stats. Replace with real data from GMCP/parsing later.
/// </summary>
public sealed partial class CharacterVitals : ObservableObject
{
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

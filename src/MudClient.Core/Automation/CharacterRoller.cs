using System.Text.RegularExpressions;

namespace MudClient.Core.Automation;

public sealed record CharacterRoll(
    int Strength,
    int Intelligence,
    int Wisdom,
    int Dexterity,
    int Constitution,
    int Charisma)
{
    public int Sum => Strength + Intelligence + Wisdom + Dexterity + Constitution + Charisma;
}

public sealed record CharacterRollerConfiguration(
    int? Sum,
    int? Strength,
    int? Intelligence,
    int? Wisdom,
    int? Dexterity,
    int? Constitution,
    int? Charisma,
    bool FinishCharacterCreation)
{
    public static CharacterRollerConfiguration Default { get; } = new(
        Sum: 450,
        Strength: 79,
        Intelligence: null,
        Wisdom: 83,
        Dexterity: null,
        Constitution: 87,
        Charisma: null,
        FinishCharacterCreation: true);
}

public enum CharacterRollerAction
{
    None,
    RequestConfiguration,
    RollAgain,
    Accepted,
    FinishCharacterCreation,
}

/// <summary>
/// Stateful parser and policy for KillerMUD's three-line character-stat roll.
/// It receives complete, plain-text MUD lines and never interacts with the UI
/// or network directly.
/// </summary>
public sealed partial class CharacterRoller
{
    private const int MaximumLinesBetweenStatRows = 4;

    private int? _strength;
    private int? _intelligence;
    private int? _wisdom;
    private int? _dexterity;
    private int _expectedRow;
    private int _linesUntilReset;
    private bool _configured;
    private bool _paused;

    public CharacterRollerConfiguration Configuration { get; private set; } =
        CharacterRollerConfiguration.Default;

    public CharacterRoll? LastRoll { get; private set; }

    public CharacterRollerAction ObserveLine(string line)
    {
        ArgumentNullException.ThrowIfNull(line);

        var firstRow = FirstRowRegex().Match(line);
        if (firstRow.Success)
        {
            _strength = Parse(firstRow, 1);
            _intelligence = Parse(firstRow, 2);
            _wisdom = null;
            _dexterity = null;
            _expectedRow = 2;
            _linesUntilReset = MaximumLinesBetweenStatRows;
            return CharacterRollerAction.None;
        }

        if (_expectedRow == 2)
        {
            var secondRow = SecondRowRegex().Match(line);
            if (secondRow.Success)
            {
                _wisdom = Parse(secondRow, 1);
                _dexterity = Parse(secondRow, 2);
                _expectedRow = 3;
                _linesUntilReset = MaximumLinesBetweenStatRows;
                return CharacterRollerAction.None;
            }
        }
        else if (_expectedRow == 3)
        {
            var thirdRow = ThirdRowRegex().Match(line);
            if (thirdRow.Success)
            {
                var roll = new CharacterRoll(
                    _strength!.Value,
                    _intelligence!.Value,
                    _wisdom!.Value,
                    _dexterity!.Value,
                    Parse(thirdRow, 1),
                    Parse(thirdRow, 2));

                ResetPartialRoll();
                LastRoll = roll;

                if (!_configured)
                {
                    _paused = true;
                    return CharacterRollerAction.RequestConfiguration;
                }

                return _paused ? CharacterRollerAction.None : Evaluate(roll);
            }
        }

        if (_expectedRow != 0 && --_linesUntilReset < 0)
        {
            ResetPartialRoll();
        }

        return CharacterRollerAction.None;
    }

    public CharacterRollerAction Configure(CharacterRollerConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        Configuration = configuration;
        _configured = true;
        _paused = false;
        return LastRoll is null ? CharacterRollerAction.None : Evaluate(LastRoll);
    }

    public void PauseForConfiguration() => _paused = true;

    public void ResetForNewSession()
    {
        ResetPartialRoll();
        LastRoll = null;
        _configured = false;
        _paused = false;
    }

    private CharacterRollerAction Evaluate(CharacterRoll roll)
    {
        var accepted =
            MeetsTarget(roll.Sum, Configuration.Sum) &&
            MeetsTarget(roll.Strength, Configuration.Strength) &&
            MeetsTarget(roll.Intelligence, Configuration.Intelligence) &&
            MeetsTarget(roll.Wisdom, Configuration.Wisdom) &&
            MeetsTarget(roll.Dexterity, Configuration.Dexterity) &&
            MeetsTarget(roll.Constitution, Configuration.Constitution) &&
            MeetsTarget(roll.Charisma, Configuration.Charisma);

        if (!accepted)
        {
            return CharacterRollerAction.RollAgain;
        }

        _paused = true;
        return Configuration.FinishCharacterCreation
            ? CharacterRollerAction.FinishCharacterCreation
            : CharacterRollerAction.Accepted;
    }

    private static bool MeetsTarget(int value, int? target) => target is null || value >= target;

    private static int Parse(Match match, int group) =>
        int.Parse(match.Groups[group].Value, System.Globalization.CultureInfo.InvariantCulture);

    private void ResetPartialRoll()
    {
        _strength = null;
        _intelligence = null;
        _wisdom = null;
        _dexterity = null;
        _expectedRow = 0;
        _linesUntilReset = 0;
    }

    [GeneratedRegex(@"STR:\s*(\d+).*INT:\s*(\d+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex FirstRowRegex();

    [GeneratedRegex(@"WIS:\s*(\d+).*DEX:\s*(\d+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SecondRowRegex();

    [GeneratedRegex(@"CON:\s*(\d+).*CHA:\s*(\d+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ThirdRowRegex();
}

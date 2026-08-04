using System.Text;

namespace MudClient.Core.Text;

/// <summary>
/// Adds KillerMUD's numeric damage tier after a matching combat phrase.
/// Matching is case-sensitive because uppercase combat messages represent
/// different tiers, but Polish diacritics are optional.
/// </summary>
public sealed class CombatDamageRangeTextTransformer
{
    private static readonly DamagePhrase[] DamagePhrases =
    [
        new("Chybiasz", null),
        new("chybiasz", null),
        new("chybiajac", null),
        new("chybia", null),
        new("Siniaczysz", "2"),
        new("siniaczysz", "2"),
        new("siniaczy", "2"),
        new("Muskasz", "6"),
        new("muskasz", "6"),
        new("muska", "6"),
        new("Ledwie ranisz", "10"),
        new("ledwie ranisz", "10"),
        new("ledwie rani", "10"),
        new("Lekko ranisz", "14"),
        new("lekko ranisz", "14"),
        new("lekko rani", "14"),
        new("Ranisz", "18"),
        new("Eanisz", "18"),
        new("ranisz", "18"),
        new("rani", "18"),
        new("Mocno ranisz", "22"),
        new("mocno ranisz", "22"),
        new("mocno rani", "22"),
        new("Dotkliwie ranisz", "26"),
        new("dotkliwie ranisz", "26"),
        new("dotkliwie rani", "26"),
        new("Powaznie ranisz", "30"),
        new("powaznie ranisz", "30"),
        new("powaznie rani", "30"),
        new("Masakrujesz", "34"),
        new("masakrujesz", "34"),
        new("masakruje", "34"),
        new("Rozpruwasz", "38"),
        new("rozpruwasz", "38"),
        new("rozpruwa", "38"),
        new("Dewastujesz", "44"),
        new("dewastujesz", "44"),
        new("dewastuje", "44"),
        new("Grzmocisz", "50"),
        new("grzmocisz", "50"),
        new("grzmoci", "50"),
        new("Niszczysz", "55"),
        new("niszczysz", "55"),
        new("niszczy", "55"),
        new("NISZCZYSZ", "60"),
        new("NISZCZY", "60"),
        new("DRUZGOCZESZ", "67"),
        new("DRUZGOCZE", "67"),
        new("ROZPRUWASZ", "75"),
        new("ROZPRUWA", "75"),
        new("ROZRYWASZ", "84"),
        new("ROZRYWA", "84"),
        new("ROZBEBESZASZ", "100"),
        new("ROZBEBESZA", "100"),
        new("DEKAPITUJESZ", "115"),
        new("DEKAPITUJE", "115"),
        new("EKSTYRPUJESZ", "130"),
        new("EKSTYRPUJE", "130"),
        new("ANIHILUJESZ", "145"),
        new("ANIHILUJE", "145"),
        new("USMIERCASZ", "200"),
        new("USMIERCA", "200"),
        new("UNICESTWIASZ", "200++"),
        new("UNICESTWIA", "200++"),
    ];

    private readonly object _gate = new();
    private readonly StringBuilder _pendingRaw = new();
    private readonly StringBuilder _pendingComparable = new();
    private int _annotationInsertionIndex;
    private bool _atWordBoundary = true;
    private AnsiState _ansiState;
    private bool _ansiBelongsToPending;

    public string Transform(string text, bool enabled)
    {
        ArgumentNullException.ThrowIfNull(text);

        lock (_gate)
        {
            if (!enabled)
            {
                var unmodified = _pendingRaw.Length == 0
                    ? text
                    : _pendingRaw.Append(text).ToString();
                Reset();
                return unmodified;
            }

            var output = new StringBuilder(text.Length + 16);
            foreach (var character in text)
            {
                TransformCharacter(character, output);
            }

            return output.ToString();
        }
    }

    private void TransformCharacter(char character, StringBuilder output)
    {
        if (_ansiState != AnsiState.None)
        {
            AppendAnsiCharacter(character, output);
            if (_ansiState == AnsiState.Escape)
            {
                _ansiState = character == '[' ? AnsiState.ControlSequence : AnsiState.None;
            }
            else if (character is >= '@' and <= '~')
            {
                _ansiState = AnsiState.None;
            }

            return;
        }

        if (character == '\u001b')
        {
            _ansiBelongsToPending = _pendingRaw.Length > 0;
            AppendAnsiCharacter(character, output);
            _ansiState = AnsiState.Escape;
            return;
        }

        TransformVisibleCharacter(character, output);
    }

    private void TransformVisibleCharacter(char character, StringBuilder output)
    {
        if (_pendingComparable.Length > 0)
        {
            var exactMatch = FindExactMatch(_pendingComparable.ToString());
            if (exactMatch is not null && !IsWordCharacter(character))
            {
                CommitPending(exactMatch, output);
                TransformVisibleCharacter(character, output);
                return;
            }

            var extended = _pendingComparable.ToString() + FoldPolishCharacter(character);
            if (DamagePhrases.Any(
                    phrase => phrase.Comparable.StartsWith(extended, StringComparison.Ordinal)))
            {
                AppendPending(character);
                return;
            }

            FlushPending(output);
            TransformVisibleCharacter(character, output);
            return;
        }

        var comparableCharacter = FoldPolishCharacter(character);
        if (_atWordBoundary && DamagePhrases.Any(
                phrase => phrase.Comparable.StartsWith(
                    comparableCharacter.ToString(),
                    StringComparison.Ordinal)))
        {
            AppendPending(character);
            return;
        }

        output.Append(character);
        _atWordBoundary = !IsWordCharacter(character);
    }

    private void AppendPending(char character)
    {
        _pendingRaw.Append(character);
        _pendingComparable.Append(FoldPolishCharacter(character));
        _annotationInsertionIndex = _pendingRaw.Length;
    }

    private void AppendAnsiCharacter(char character, StringBuilder output)
    {
        if (_ansiBelongsToPending)
        {
            _pendingRaw.Append(character);
        }
        else
        {
            output.Append(character);
        }
    }

    private void CommitPending(DamagePhrase phrase, StringBuilder output)
    {
        if (phrase.DisplayDamage is null)
        {
            output.Append(_pendingRaw);
        }
        else
        {
            output.Append(_pendingRaw.ToString(0, _annotationInsertionIndex));
            output.Append(" (").Append(phrase.DisplayDamage).Append(')');
            output.Append(_pendingRaw.ToString(_annotationInsertionIndex, _pendingRaw.Length - _annotationInsertionIndex));
        }

        ClearPending();
        _atWordBoundary = false;
    }

    private void FlushPending(StringBuilder output)
    {
        output.Append(_pendingRaw);
        _atWordBoundary = _pendingComparable.Length == 0
            || !IsWordCharacter(_pendingComparable[^1]);
        ClearPending();
    }

    private void ClearPending()
    {
        _pendingRaw.Clear();
        _pendingComparable.Clear();
        _annotationInsertionIndex = 0;
    }

    private void Reset()
    {
        ClearPending();
        _atWordBoundary = true;
        _ansiState = AnsiState.None;
        _ansiBelongsToPending = false;
    }

    private static DamagePhrase? FindExactMatch(string comparable) =>
        DamagePhrases.FirstOrDefault(
            phrase => string.Equals(phrase.Comparable, comparable, StringComparison.Ordinal));

    private static bool IsWordCharacter(char character) =>
        char.IsLetterOrDigit(character) || character == '_';

    private static char FoldPolishCharacter(char character) =>
        character switch
        {
            'ą' => 'a',
            'Ą' => 'A',
            'ć' => 'c',
            'Ć' => 'C',
            'ę' => 'e',
            'Ę' => 'E',
            'ł' => 'l',
            'Ł' => 'L',
            'ń' => 'n',
            'Ń' => 'N',
            'ó' => 'o',
            'Ó' => 'O',
            'ś' => 's',
            'Ś' => 'S',
            'ź' or 'ż' => 'z',
            'Ź' or 'Ż' => 'Z',
            _ => character,
        };

    private sealed record DamagePhrase(string Comparable, string? DisplayDamage);

    private enum AnsiState
    {
        None,
        Escape,
        ControlSequence,
    }
}

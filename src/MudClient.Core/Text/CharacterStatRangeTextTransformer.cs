using System.Text;
using System.Text.RegularExpressions;

namespace MudClient.Core.Text;

/// <summary>
/// Adds numeric ranges to KillerMUD's descriptive character-stat lines.
/// The transformer keeps only possible "Twoja ..." lines until their newline,
/// so ordinary output and prompts continue to stream without line buffering.
/// </summary>
public sealed partial class CharacterStatRangeTextTransformer
{
    private static readonly string[] StatLinePrefixes =
    [
        "Twoja siła jest ",
        "Twoja zręczność jest ",
        "Twoja kondycja jest ",
        "Twoja inteligencja jest ",
        "Twoja wiedza jest ",
        "Twoja charyzma jest ",
    ];

    private static readonly (string Description, string Range)[] DescriptiveRanges =
    [
        ("półboska", "214+"),
        ("legendarna", "200-213"),
        ("niespotykana", "186-199"),
        ("niezmiernie wysoka", "172-185"),
        ("wysoka", "158-171"),
        ("niezła", "144-157"),
        ("nieprzeciętna", "130-143"),
        ("średnia", "116-129"),
        ("poniżej przeciętnej", "102-115"),
        ("bardzo niska", "88-101"),
        ("godna pożałowania", "74-87"),
    ];

    private readonly object _gate = new();
    private readonly StringBuilder _pending = new();
    private readonly StringBuilder _visiblePrefix = new();
    private LineState _lineState;
    private AnsiState _ansiState;

    public string Transform(string text, bool enabled)
    {
        ArgumentNullException.ThrowIfNull(text);

        lock (_gate)
        {
            if (!enabled)
            {
                var unmodified = _pending.Length == 0
                    ? text
                    : _pending.Append(text).ToString();
                ResetLine();
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

    public static string AnnotateLine(string line)
    {
        ArgumentNullException.ThrowIfNull(line);

        var cleanLine = AnsiSequenceRegex().Replace(line, string.Empty).TrimStart();
        var comparableLine = FoldPolishCharacters(cleanLine);
        if (!StatLinePrefixes.Any(
                prefix => comparableLine.StartsWith(
                    FoldPolishCharacters(prefix),
                    StringComparison.OrdinalIgnoreCase)))
        {
            return line;
        }

        var range = DescriptiveRanges
            .FirstOrDefault(entry =>
                comparableLine.Contains(
                    FoldPolishCharacters(entry.Description),
                    StringComparison.OrdinalIgnoreCase))
            .Range;
        range ??= "<73";

        if (cleanLine.Contains($"({range})", StringComparison.Ordinal))
        {
            return line;
        }

        return $"{line} ({range})";
    }

    private void TransformCharacter(char character, StringBuilder output)
    {
        if (_lineState == LineState.PassThrough)
        {
            output.Append(character);
            if (character == '\n')
            {
                ResetLine();
            }

            return;
        }

        _pending.Append(character);
        if (character == '\n')
        {
            var lineEndingLength = _pending.Length >= 2 && _pending[^2] == '\r' ? 2 : 1;
            var line = _pending.ToString(0, _pending.Length - lineEndingLength);
            output.Append(AnnotateLine(line));
            output.Append(lineEndingLength == 2 ? "\r\n" : "\n");
            ResetLine();
            return;
        }

        if (_lineState == LineState.StatCandidate)
        {
            return;
        }

        ObserveVisibleCharacter(character);
        var visible = FoldPolishCharacters(_visiblePrefix.ToString().TrimStart());
        if (StatLinePrefixes.Any(
                prefix => visible.StartsWith(
                    FoldPolishCharacters(prefix),
                    StringComparison.OrdinalIgnoreCase)))
        {
            _lineState = LineState.StatCandidate;
            return;
        }

        if (StatLinePrefixes.Any(
                prefix => FoldPolishCharacters(prefix)
                    .StartsWith(visible, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        output.Append(_pending);
        _pending.Clear();
        _lineState = LineState.PassThrough;
    }

    private static string FoldPolishCharacters(string value)
    {
        var result = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            result.Append(character switch
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
            });
        }

        return result.ToString();
    }

    private void ObserveVisibleCharacter(char character)
    {
        if (_ansiState == AnsiState.Escape)
        {
            _ansiState = character == '[' ? AnsiState.ControlSequence : AnsiState.None;
            return;
        }

        if (_ansiState == AnsiState.ControlSequence)
        {
            if (character is >= '@' and <= '~')
            {
                _ansiState = AnsiState.None;
            }

            return;
        }

        if (character == '\u001b')
        {
            _ansiState = AnsiState.Escape;
            return;
        }

        if (character != '\r')
        {
            _visiblePrefix.Append(character);
        }
    }

    private void ResetLine()
    {
        _pending.Clear();
        _visiblePrefix.Clear();
        _lineState = LineState.Undecided;
        _ansiState = AnsiState.None;
    }

    [GeneratedRegex(@"\x1B\[[0-?]*[ -/]*[@-~]", RegexOptions.CultureInvariant)]
    private static partial Regex AnsiSequenceRegex();

    private enum LineState
    {
        Undecided,
        StatCandidate,
        PassThrough,
    }

    private enum AnsiState
    {
        None,
        Escape,
        ControlSequence,
    }
}

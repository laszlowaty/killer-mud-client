using System.Globalization;
using System.Text;
using Avalonia.Media;

namespace MudClient.App.Controls;

/// <summary>
/// Streaming parser for ANSI/VT100 escape sequences. It intentionally ignores cursor movement
/// because MUD output is rendered as append-only logical lines, but every recognised escape
/// form is still fully consumed so its bytes never leak into visible text.
/// </summary>
internal sealed class AnsiStreamParser
{
    private const char Esc = '';
    private const char Del = '';

    // A malformed/never-terminated CSI or string sequence must not be allowed to swallow the
    // rest of the stream forever. Real terminals cap this too; MUD sequences are always short.
    private const int MaxSequenceLength = 128;

    private AnsiColorPalette _palette;

    private enum EscapeState
    {
        None,
        JustSawEscape,
        Csi,
        CsiIntermediate,
        StringSequence,
        StringSequenceEscape,
        ChangeCharsetIntermediate,
    }

    private readonly StringBuilder _sequence = new();
    private AnsiStyle _style;
    private int? _standardForegroundIndex;
    private bool _standardForegroundIsExplicitlyBright;
    private EscapeState _escapeState = EscapeState.None;

    public AnsiStreamParser(string? colorScheme = null)
    {
        _palette = AnsiColorPalette.FromName(colorScheme);
    }

    public void SetColorScheme(string? colorScheme)
    {
        _palette = AnsiColorPalette.FromName(colorScheme);
        UpdateStandardForeground();
    }

    public IReadOnlyList<AnsiToken> Feed(string text)
    {
        var tokens = new List<AnsiToken>();
        var plainText = new StringBuilder();

        void FlushPlainText()
        {
            if (plainText.Length == 0)
            {
                return;
            }

            tokens.Add(new AnsiTextToken(plainText.ToString(), _style));
            plainText.Clear();
        }

        void Backspace()
        {
            if (plainText.Length > 0)
            {
                plainText.Length--;
                return;
            }

            for (var i = tokens.Count - 1; i >= 0; i--)
            {
                if (tokens[i] is not AnsiTextToken previous || previous.Text.Length == 0)
                {
                    continue;
                }

                tokens[i] = previous with { Text = previous.Text[..^1] };
                return;
            }
        }

        foreach (var character in text)
        {
            if (_escapeState != EscapeState.None)
            {
                HandleEscapeCharacter(character);
                continue;
            }

            switch (character)
            {
                case Esc:
                    FlushPlainText();
                    _escapeState = EscapeState.JustSawEscape;
                    _sequence.Clear();
                    break;

                case '\n':
                    FlushPlainText();
                    tokens.Add(new AnsiNewLineToken());
                    break;

                case '\r':
                    FlushPlainText();
                    tokens.Add(new AnsiCarriageReturnToken());
                    break;

                case '\b':
                    Backspace();
                    break;

                // NUL and DEL are Telnet/terminal padding bytes with no visible glyph.
                case '\0':
                case Del:
                    break;

                default:
                    plainText.Append(character);
                    break;
            }
        }

        FlushPlainText();
        return tokens;

        void HandleEscapeCharacter(char character)
        {
            switch (_escapeState)
            {
                case EscapeState.JustSawEscape:
                    switch (character)
                    {
                        case '[':
                            _escapeState = EscapeState.Csi;
                            _sequence.Clear();
                            break;

                        case ']':
                        case 'P': // DCS
                        case '^': // PM
                        case '_': // APC
                            _escapeState = EscapeState.StringSequence;
                            break;

                        case '(':
                        case ')':
                        case '*':
                        case '+':
                            // Charset designation, e.g. ESC ( B — exactly one more byte follows.
                            _escapeState = EscapeState.ChangeCharsetIntermediate;
                            break;

                        default:
                            // Any other two-byte escape (ESC c, ESC 7, ESC =, ESC M, ...) is
                            // complete as soon as this single byte arrives. Never keep scanning
                            // forward for a terminator — that is what used to eat real text
                            // (e.g. parentheses) whenever a MUD sent one of these sequences.
                            _escapeState = EscapeState.None;
                            break;
                    }

                    break;

                case EscapeState.ChangeCharsetIntermediate:
                    _escapeState = EscapeState.None;
                    break;

                case EscapeState.Csi:
                    if (character is >= '\x30' and <= '\x3F')
                    {
                        // Parameter byte (digits, ';', ':', '<', '=', '>', '?').
                        _sequence.Append(character);
                        AbortIfTooLong();
                    }
                    else if (character is >= '\x20' and <= '\x2F')
                    {
                        // Intermediate byte.
                        _sequence.Append(character);
                        _escapeState = EscapeState.CsiIntermediate;
                        AbortIfTooLong();
                    }
                    else if (character is >= '\x40' and <= '\x7E')
                    {
                        CompleteCsi(character);
                    }
                    else
                    {
                        // Not a legal CSI byte at all (e.g. a stray control char) — bail out
                        // cleanly instead of holding the sequence open indefinitely.
                        _escapeState = EscapeState.None;
                    }

                    break;

                case EscapeState.CsiIntermediate:
                    if (character is >= '\x20' and <= '\x2F')
                    {
                        _sequence.Append(character);
                        AbortIfTooLong();
                    }
                    else if (character is >= '\x40' and <= '\x7E')
                    {
                        CompleteCsi(character);
                    }
                    else
                    {
                        _escapeState = EscapeState.None;
                    }

                    break;

                case EscapeState.StringSequence:
                    if (character == Esc)
                    {
                        _escapeState = EscapeState.StringSequenceEscape;
                    }
                    else if (character == '\a')
                    {
                        // BEL terminates an OSC/DCS/PM/APC string sequence.
                        _escapeState = EscapeState.None;
                    }
                    else
                    {
                        AbortIfTooLong();
                    }

                    break;

                case EscapeState.StringSequenceEscape:
                    // String Terminator is ESC \. Anything else defensively ends the
                    // (malformed) string sequence too, rather than swallowing the stream.
                    _escapeState = EscapeState.None;
                    break;
            }
        }

        void AbortIfTooLong()
        {
            if (_sequence.Length >= MaxSequenceLength)
            {
                _escapeState = EscapeState.None;
            }
        }

        void CompleteCsi(char finalByte)
        {
            if (finalByte == 'm')
            {
                HandleSgr(_sequence.ToString());
            }

            _sequence.Clear();
            _escapeState = EscapeState.None;
        }
    }

    private void HandleSgr(string parameterText)
    {
        int[] parameters = parameterText.Length == 0
            ? [0]
            : parameterText.Split(';')
                .Select(value => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
                    ? parsed
                    : 0)
                .ToArray();

        for (var index = 0; index < parameters.Length; index++)
        {
            var code = parameters[index];

            switch (code)
            {
                case 0:
                    _style = default;
                    _standardForegroundIndex = null;
                    _standardForegroundIsExplicitlyBright = false;
                    break;
                case 1:
                    _style = _style with { Bold = true };
                    UpdateStandardForeground();
                    break;
                case 22:
                    _style = _style with { Bold = false };
                    UpdateStandardForeground();
                    break;
                case 4:
                    _style = _style with { Underline = true };
                    break;
                case 24:
                    _style = _style with { Underline = false };
                    break;
                case 39:
                    _style = _style with { Foreground = null };
                    _standardForegroundIndex = null;
                    _standardForegroundIsExplicitlyBright = false;
                    break;
                case 49:
                    _style = _style with { Background = null };
                    break;
                case >= 30 and <= 37:
                    _standardForegroundIndex = code - 30;
                    _standardForegroundIsExplicitlyBright = false;
                    UpdateStandardForeground();
                    break;
                case >= 40 and <= 47:
                    _style = _style with { Background = _palette.Normal[code - 40] };
                    break;
                case >= 90 and <= 97:
                    _standardForegroundIndex = code - 90;
                    _standardForegroundIsExplicitlyBright = true;
                    UpdateStandardForeground();
                    break;
                case >= 100 and <= 107:
                    _style = _style with { Background = _palette.Bright[code - 100] };
                    break;
                case 38:
                case 48:
                    var isForeground = code == 38;
                    if (TryReadExtendedColor(parameters, ref index, out var color))
                    {
                        _style = isForeground
                            ? _style with { Foreground = color }
                            : _style with { Background = color };

                        if (isForeground)
                        {
                            _standardForegroundIndex = null;
                            _standardForegroundIsExplicitlyBright = false;
                        }
                    }

                    break;
            }
        }
    }

    private void UpdateStandardForeground()
    {
        if (_standardForegroundIndex is not { } index)
        {
            return;
        }

        var useBright = _standardForegroundIsExplicitlyBright || _style.Bold;
        _style = _style with
        {
            Foreground = useBright ? _palette.Bright[index] : _palette.Normal[index],
        };
    }

    private bool TryReadExtendedColor(
        IReadOnlyList<int> parameters,
        ref int index,
        out Color color)
    {
        color = default;

        if (index + 1 >= parameters.Count)
        {
            return false;
        }

        var mode = parameters[++index];

        if (mode == 5 && index + 1 < parameters.Count)
        {
            color = ColorFrom256Palette(Math.Clamp(parameters[++index], 0, 255));
            return true;
        }

        if (mode == 2 && index + 3 < parameters.Count)
        {
            var red = (byte)Math.Clamp(parameters[++index], 0, 255);
            var green = (byte)Math.Clamp(parameters[++index], 0, 255);
            var blue = (byte)Math.Clamp(parameters[++index], 0, 255);
            color = Color.FromRgb(red, green, blue);
            return true;
        }

        return false;
    }

    private Color ColorFrom256Palette(int index)
    {
        if (index < 8)
        {
            return _palette.Normal[index];
        }

        if (index < 16)
        {
            return _palette.Bright[index - 8];
        }

        if (index < 232)
        {
            var cubeIndex = index - 16;
            var red = cubeIndex / 36;
            var green = cubeIndex / 6 % 6;
            var blue = cubeIndex % 6;

            static byte Component(int value) => value == 0 ? (byte)0 : (byte)(55 + value * 40);

            return Color.FromRgb(Component(red), Component(green), Component(blue));
        }

        var gray = (byte)(8 + (index - 232) * 10);
        return Color.FromRgb(gray, gray, gray);
    }
}

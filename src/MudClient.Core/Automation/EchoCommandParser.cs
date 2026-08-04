using System.Text;

namespace MudClient.Core.Automation;

public sealed record EchoCommand(int AnsiColorCode, string Text);

public enum EchoCommandParseStatus
{
    NotEcho,
    Success,
    Invalid,
}

/// <summary>
/// Parses local terminal output commands in the form
/// <c>echo("red", "message")</c>. Echo commands are handled by the client
/// and must never be forwarded to the MUD server.
/// </summary>
public static class EchoCommandParser
{
    private static readonly IReadOnlyDictionary<string, int> Colors =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["black"] = 30,
            ["red"] = 31,
            ["green"] = 32,
            ["yellow"] = 33,
            ["blue"] = 34,
            ["magenta"] = 35,
            ["cyan"] = 36,
            ["white"] = 37,
            ["gray"] = 90,
            ["grey"] = 90,
        };

    public static IReadOnlyCollection<string> ColorNames { get; } =
        ["black", "red", "green", "yellow", "blue", "magenta", "cyan", "white", "gray"];

    public static bool TryCreate(string? color, string text, out EchoCommand? command)
    {
        command = null;
        if (string.IsNullOrWhiteSpace(color)
            || !Colors.TryGetValue(color, out var ansiColorCode))
        {
            return false;
        }

        command = new EchoCommand(ansiColorCode, text);
        return true;
    }

    public static EchoCommandParseStatus Parse(string? input, out EchoCommand? command)
    {
        command = null;
        if (string.IsNullOrWhiteSpace(input))
        {
            return EchoCommandParseStatus.NotEcho;
        }

        var parser = new Parser(input);
        parser.SkipWhitespace();
        if (!parser.TryReadWord("echo"))
        {
            return EchoCommandParseStatus.NotEcho;
        }

        parser.SkipWhitespace();
        if (!parser.TryRead('('))
        {
            return EchoCommandParseStatus.NotEcho;
        }

        parser.SkipWhitespace();
        if (!parser.TryReadQuotedString(out var color))
        {
            return EchoCommandParseStatus.Invalid;
        }

        parser.SkipWhitespace();
        if (!parser.TryRead(','))
        {
            return EchoCommandParseStatus.Invalid;
        }

        parser.SkipWhitespace();
        if (!parser.TryReadQuotedString(out var text))
        {
            return EchoCommandParseStatus.Invalid;
        }

        parser.SkipWhitespace();
        if (!parser.TryRead(')'))
        {
            return EchoCommandParseStatus.Invalid;
        }

        parser.SkipWhitespace();
        if (!parser.AtEnd || !TryCreate(color, text, out command))
        {
            return EchoCommandParseStatus.Invalid;
        }

        return EchoCommandParseStatus.Success;
    }

    private sealed class Parser(string input)
    {
        private int _position;

        public bool AtEnd => _position == input.Length;

        public void SkipWhitespace()
        {
            while (_position < input.Length && char.IsWhiteSpace(input[_position]))
            {
                _position++;
            }
        }

        public bool TryRead(char expected)
        {
            if (_position >= input.Length || input[_position] != expected)
            {
                return false;
            }

            _position++;
            return true;
        }

        public bool TryReadWord(string expected)
        {
            if (_position + expected.Length > input.Length
                || !input.AsSpan(_position, expected.Length).Equals(
                    expected,
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var end = _position + expected.Length;
            if (end < input.Length
                && (char.IsLetterOrDigit(input[end]) || input[end] == '_'))
            {
                return false;
            }

            _position = end;
            return true;
        }

        public bool TryReadQuotedString(out string value)
        {
            value = string.Empty;
            if (_position >= input.Length || input[_position] is not ('"' or '\''))
            {
                return false;
            }

            var quote = input[_position++];
            var builder = new StringBuilder();
            while (_position < input.Length)
            {
                var current = input[_position++];
                if (current == quote)
                {
                    value = builder.ToString();
                    return true;
                }

                if (current != '\\')
                {
                    builder.Append(current);
                    continue;
                }

                if (_position >= input.Length)
                {
                    return false;
                }

                var escaped = input[_position++];
                builder.Append(escaped switch
                {
                    '\\' => '\\',
                    '"' => '"',
                    '\'' => '\'',
                    'n' => '\n',
                    'r' => '\r',
                    't' => '\t',
                    _ => escaped,
                });
            }

            return false;
        }
    }
}

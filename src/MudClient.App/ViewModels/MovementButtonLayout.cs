using MudClient.Core.Map;
using System.Globalization;
using System.Text;

namespace MudClient.App.ViewModels;

public sealed record MovementButtonState(string Label, string Command);

public sealed record MovementButtonLayout(
    MovementButtonState North,
    MovementButtonState South,
    MovementButtonState West,
    MovementButtonState East,
    MovementButtonState Up,
    MovementButtonState Down)
{
    public static MovementButtonLayout Create(IReadOnlyList<RoomExitInfo>? exits = null)
    {
        var buttons = new Dictionary<string, MovementButtonState>(StringComparer.OrdinalIgnoreCase)
        {
            ["N"] = new("n", "n"),
            ["S"] = new("s", "s"),
            ["W"] = new("w", "w"),
            ["E"] = new("e", "e"),
            ["U"] = new("up", "up"),
            ["D"] = new("down", "down"),
        };

        foreach (var exit in exits ?? [])
        {
            var direction = CanonicalDirection(exit.Dir);
            if (!buttons.TryGetValue(direction, out var defaultButton) ||
                string.IsNullOrWhiteSpace(exit.Name) ||
                CanonicalDirection(exit.Name) == direction)
            {
                continue;
            }

            var label = exit.Name.Trim();
            buttons[direction] = new MovementButtonState(
                label,
                ToMudCommand(label));
        }

        return new MovementButtonLayout(
            buttons["N"],
            buttons["S"],
            buttons["W"],
            buttons["E"],
            buttons["U"],
            buttons["D"]);
    }

    private static string CanonicalDirection(string direction) =>
        direction.Trim().ToLowerInvariant() switch
        {
            "n" or "north" => "N",
            "s" or "south" => "S",
            "w" or "west" => "W",
            "e" or "east" => "E",
            "u" or "up" => "U",
            "d" or "down" => "D",
            _ => direction.Trim().ToUpperInvariant(),
        };

    private static string ToMudCommand(string label)
    {
        var decomposed = label.Normalize(NormalizationForm.FormD);
        var command = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) ==
                UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            command.Append(character switch
            {
                'ł' => 'l',
                'Ł' => 'L',
                _ => character,
            });
        }

        return command.ToString().Normalize(NormalizationForm.FormC);
    }
}

using System.Globalization;
using System.Text;
using MudClient.Core.Map;

namespace MudClient.App.ViewModels;

public sealed record MovementButtonState(
    string Label,
    string Command,
    string? MoveCommandAfterOpening = null);

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
            if (!buttons.TryGetValue(direction, out var defaultButton))
            {
                continue;
            }

            var label = defaultButton.Label;
            var moveCommand = defaultButton.Command;
            if (!string.IsNullOrWhiteSpace(exit.Name) &&
                CanonicalDirection(exit.Name) != direction)
            {
                label = exit.Name.Trim();
                moveCommand = ToMudCommand(label);
            }

            buttons[direction] = exit.IsClosed
                ? new MovementButtonState(
                    label,
                    CreateOpeningCommand(exit, defaultButton.Command),
                    moveCommand)
                : new MovementButtonState(label, moveCommand);
        }

        return new MovementButtonLayout(
            buttons["N"],
            buttons["S"],
            buttons["W"],
            buttons["E"],
            buttons["U"],
            buttons["D"]);
    }

    public MovementButtonLayout MarkOpened(string openingCommand) =>
        new(
            MarkOpened(North, openingCommand),
            MarkOpened(South, openingCommand),
            MarkOpened(West, openingCommand),
            MarkOpened(East, openingCommand),
            MarkOpened(Up, openingCommand),
            MarkOpened(Down, openingCommand));

    private static MovementButtonState MarkOpened(
        MovementButtonState button,
        string openingCommand) =>
        button.MoveCommandAfterOpening is { } moveCommand &&
        string.Equals(button.Command, openingCommand, StringComparison.OrdinalIgnoreCase)
            ? new MovementButtonState(button.Label, moveCommand)
            : button;

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

    private static string CreateOpeningCommand(
        RoomExitInfo exit,
        string fallbackDirection) =>
        string.IsNullOrWhiteSpace(exit.Name)
            ? $"open {fallbackDirection}"
            : $"open \"{ToMudCommand(exit.Name.Trim())}\"";
}

using System.Globalization;
using System.Text;

namespace MudClient.Core.Text;

/// <summary>Normalizes user-visible Polish names for commands accepted by the MUD.</summary>
public static class MudCommandText
{
    public static string ToAsciiLowerInvariant(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var decomposed = text.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var command = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            command.Append(character == 'ł' ? 'l' : character);
        }

        return command.ToString().Normalize(NormalizationForm.FormC);
    }
}

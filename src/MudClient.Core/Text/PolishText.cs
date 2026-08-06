using System.Globalization;
using System.Text;

namespace MudClient.Core.Text;

/// <summary>
/// Folds Polish diacritics to their base ASCII letters (e.g. "księga" → "ksiega"). This MUD's
/// server never sends diacritics in its own output, so any word list sourced from a modern
/// (UTF-8, properly-accented) reference like a wiki page needs folding before it can match real
/// game text.
/// </summary>
public static class PolishText
{
    public static string Fold(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var decomposed = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                // Unlike the remaining Polish diacritics, ł/Ł does not decompose in FormD.
                builder.Append(character switch
                {
                    'ł' => 'l',
                    'Ł' => 'L',
                    _ => character,
                });
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}

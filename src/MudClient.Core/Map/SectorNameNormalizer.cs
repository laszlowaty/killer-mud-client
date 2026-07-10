using System.Globalization;
using System.Text;

namespace MudClient.Core.Map;

public static class SectorNameNormalizer
{
    public static string ToFileName(string sectorName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sectorName);

        var trimmed = sectorName.Trim().ToLowerInvariant().Replace('ł', 'l');
        var formD = trimmed.Normalize(NormalizationForm.FormD);

        var builder = new StringBuilder(formD.Length);
        foreach (var ch in formD)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            builder.Append(ch is ' ' or '-' or '\t' ? '_' : ch);
        }

        var normalized = builder.ToString().Normalize(NormalizationForm.FormC);

        while (normalized.Contains("__", StringComparison.Ordinal))
        {
            normalized = normalized.Replace("__", "_", StringComparison.Ordinal);
        }

        normalized = normalized.Trim('_');

        return normalized + ".png";
    }
}

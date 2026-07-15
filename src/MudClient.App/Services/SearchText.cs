using System.Globalization;
using System.Text;

namespace MudClient.App.Services;

/// <summary>
/// Diacritics-insensitive text matching shared by Killeropedia filters and highlighting.
/// </summary>
public static class SearchText
{
    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var decomposed = value.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var result = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                // Unlike the remaining Polish diacritics, ł does not decompose in FormD.
                result.Append(character == 'ł' ? 'l' : character);
            }
        }

        return result.ToString().Normalize(NormalizationForm.FormC);
    }

    /// <summary>
    /// Finds the ranges (in <paramref name="text"/> coordinates) matched by any
    /// whitespace-separated token of <paramref name="searchText"/>, using the same
    /// normalization as <see cref="Normalize"/>. Ranges are sorted and merged.
    /// </summary>
    public static IReadOnlyList<(int Start, int Length)> FindMatchRanges(string? text, string? searchText)
    {
        if (string.IsNullOrEmpty(text))
        {
            return [];
        }

        var tokens = Normalize(searchText)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length == 0)
        {
            return [];
        }

        var (normalized, sourceIndices) = NormalizeWithMap(text);
        var ranges = new List<(int Start, int End)>();
        foreach (var token in tokens)
        {
            var searchFrom = 0;
            while (searchFrom <= normalized.Length - token.Length)
            {
                var index = normalized.IndexOf(token, searchFrom, StringComparison.Ordinal);
                if (index < 0)
                {
                    break;
                }

                var start = sourceIndices[index];
                var end = sourceIndices[index + token.Length - 1] + 1;
                ranges.Add((start, end));
                searchFrom = index + 1;
            }
        }

        if (ranges.Count == 0)
        {
            return [];
        }

        ranges.Sort();
        var merged = new List<(int Start, int Length)>();
        var (currentStart, currentEnd) = ranges[0];
        foreach (var (start, end) in ranges.Skip(1))
        {
            if (start <= currentEnd)
            {
                currentEnd = Math.Max(currentEnd, end);
            }
            else
            {
                merged.Add((currentStart, currentEnd - currentStart));
                (currentStart, currentEnd) = (start, end);
            }
        }

        merged.Add((currentStart, currentEnd - currentStart));
        return merged;
    }

    private static (string Normalized, List<int> SourceIndices) NormalizeWithMap(string text)
    {
        var builder = new StringBuilder(text.Length);
        var sourceIndices = new List<int>(text.Length);
        for (var sourceIndex = 0; sourceIndex < text.Length; sourceIndex++)
        {
            var decomposed = char.ToLowerInvariant(text[sourceIndex]).ToString().Normalize(NormalizationForm.FormD);
            foreach (var character in decomposed)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
                {
                    builder.Append(character == 'ł' ? 'l' : character);
                    sourceIndices.Add(sourceIndex);
                }
            }
        }

        return (builder.ToString(), sourceIndices);
    }
}

using MudClient.Core.Map;

namespace MudClient.Core.Tests;

public sealed class SectorNameNormalizerTests
{
    [Theory]
    [InlineData("las", "las.png")]
    [InlineData("arktyczny lad", "arktyczny_lad.png")]
    [InlineData("podziemna droga", "podziemna_droga.png")]
    [InlineData("Łąka", "laka.png")]
    [InlineData("  Trawa  ", "trawa.png")]
    public void NormalizesSectorNameToFileName(string input, string expected)
    {
        Assert.Equal(expected, SectorNameNormalizer.ToFileName(input));
    }
}

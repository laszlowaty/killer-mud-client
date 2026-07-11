using MudClient.App.Models;
using MudClient.App.Services;

namespace MudClient.App.Tests;

public sealed class BuffWatchTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), "MudClientTests", Guid.NewGuid().ToString("N"));

    private ProfileService CreateService() => new(_directory);

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    // ====================================================================
    // Name normalization (parenthesized counters must be ignored)
    // ====================================================================

    [Theory]
    [InlineData("mirror image (7)", "mirror image")]
    [InlineData("mirror image(7)", "mirror image")]
    [InlineData("blur", "blur")]
    [InlineData("  armor  ", "armor")]
    [InlineData("stone skin (2) ", "stone skin")]
    public void NormalizeName_StripsParenthesizedSuffixAndTrims(string input, string expected)
    {
        Assert.Equal(expected, BuffWatchEntry.NormalizeName(input));
    }

    // ====================================================================
    // Persistence
    // ====================================================================

    [Fact]
    public void SaveAndLoad_RoundTripsRequiredBuffs()
    {
        var service = CreateService();
        service.Save(new ProfileData
        {
            Name = "Gandalf",
            RequiredBuffs = ["armor", "mirror image"],
        });

        var loaded = service.Load("Gandalf");

        Assert.NotNull(loaded);
        Assert.Equal(["armor", "mirror image"], loaded!.RequiredBuffs);
    }

    [Fact]
    public void Load_OldProfileWithoutBuffs_ReturnsEmptyList()
    {
        var service = CreateService();
        service.Save(new ProfileData { Name = "Stary" });

        var loaded = service.Load("Stary");

        Assert.NotNull(loaded);
        Assert.Empty(loaded!.RequiredBuffs);
    }
}

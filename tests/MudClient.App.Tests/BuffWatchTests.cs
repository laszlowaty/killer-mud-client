using MudClient.App.Models;
using MudClient.App.Services;
using MudClient.App.ViewModels;

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
    public void SaveAndLoad_RoundTripsNamedSetsAndSelection()
    {
        var service = CreateService();
        service.Save(new ProfileData
        {
            Name = "Gandalf",
            ActiveBuffSetId = "combat",
            BuffSets =
            [
                new ProfileBuffSet { Id = "travel", Name = "Podróż", Buffs = ["fly"] },
                new ProfileBuffSet
                {
                    Id = "combat",
                    Name = "Walka",
                    Buffs = ["armor", "sanctuary"],
                    LossNotifications = ["sanctuary"],
                },
            ],
        });

        var loaded = Assert.IsType<ProfileData>(service.Load("Gandalf"));

        Assert.Equal("combat", loaded.ActiveBuffSetId);
        Assert.Collection(
            loaded.BuffSets,
            set =>
            {
                Assert.Equal("Podróż", set.Name);
                Assert.Equal(["fly"], set.Buffs);
            },
            set =>
            {
                Assert.Equal("Walka", set.Name);
                Assert.Equal(["armor", "sanctuary"], set.Buffs);
                Assert.Equal(["sanctuary"], set.LossNotifications);
            });
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

    [Fact]
    public async Task ViewModel_MigratesLegacyListAndPersistsNewSets()
    {
        var service = CreateService();
        service.Save(new ProfileData
        {
            Name = "Mag",
            RequiredBuffs = ["armor", "mirror image"],
        });

        await using var viewModel = new MainWindowViewModel(
            service,
            new AppSettingsService(_directory));
        viewModel.SelectedProfileName = "Mag";
        viewModel.SelectProfileCommand.Execute(null);

        Assert.Equal("Domyślny", viewModel.SelectedBuffSet?.Name);
        Assert.Equal(["armor", "mirror image"], viewModel.RequiredBuffs.Select(buff => buff.Name));

        viewModel.NewBuffSetName = "Walka";
        viewModel.CreateBuffSetCommand.Execute(null);
        viewModel.NewBuffName = "sanctuary";
        viewModel.AddBuffCommand.Execute(null);
        Assert.Single(viewModel.RequiredBuffs).IsLossNotificationEnabled = true;

        var loaded = Assert.IsType<ProfileData>(service.Load("Mag"));
        Assert.Equal("Walka", viewModel.SelectedBuffSet?.Name);
        Assert.Equal(viewModel.SelectedBuffSet?.Id, loaded.ActiveBuffSetId);
        Assert.Collection(
            loaded.BuffSets,
            set => Assert.Equal(["armor", "mirror image"], set.Buffs),
            set =>
            {
                Assert.Equal(["sanctuary"], set.Buffs);
                Assert.Equal(["sanctuary"], set.LossNotifications);
            });
    }

    [Fact]
    public async Task ViewModel_SwitchesVisibleBuffsAndPreventsDuplicateSetNames()
    {
        await using var viewModel = new MainWindowViewModel(
            CreateService(),
            new AppSettingsService(_directory));
        viewModel.NewBuffName = "armor";
        viewModel.AddBuffCommand.Execute(null);
        var defaultSet = Assert.IsType<BuffSetEntry>(viewModel.SelectedBuffSet);

        viewModel.NewBuffSetName = "Walka";
        viewModel.CreateBuffSetCommand.Execute(null);
        viewModel.NewBuffName = "sanctuary";
        viewModel.AddBuffCommand.Execute(null);

        Assert.Equal(["sanctuary"], viewModel.RequiredBuffs.Select(buff => buff.Name));
        viewModel.SelectedBuffSet = defaultSet;
        Assert.Equal(["armor"], viewModel.RequiredBuffs.Select(buff => buff.Name));

        viewModel.NewBuffSetName = "walka";
        viewModel.CreateBuffSetCommand.Execute(null);

        Assert.Equal(2, viewModel.BuffSets.Count);
        Assert.Contains(viewModel.Toasts, toast => toast.Text == "Zestaw „walka” już istnieje.");
    }
}

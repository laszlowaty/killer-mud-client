using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using MudClient.App.Models;
using MudClient.App.Behaviors;
using MudClient.App.Services;
using MudClient.App.ViewModels;
using MudClient.App.Views;

namespace MudClient.App.Tests;

[Collection(AvaloniaUiCollection.Name)]
public sealed class KilleropediaTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "KillerMudClient_Killeropedia_" + Guid.NewGuid().ToString("N"));

    public KilleropediaTests()
    {
        Directory.CreateDirectory(_directory);
    }

    public void Dispose()
    {
        Directory.Delete(_directory, recursive: true);
    }

    [Fact]
    public void Catalog_MergesSupplementaryTeachersAndSkillsWithoutDuplicates()
    {
        var teachers = TeacherCatalogLoader.Load();

        Assert.Equal(151, teachers.Count);
        Assert.Equal(1892, teachers.Sum(teacher => teacher.Skills.Count));

        var renegade = Assert.Single(teachers, teacher => teacher.MobVnum == "19216");
        Assert.Contains(renegade.Skills, skill => skill.Name == "whirlwind");
        Assert.Contains(renegade.Skills, skill => skill.Name == "cyclone");

        var haghburg = Assert.Single(teachers, teacher => teacher.MobVnum == "6611");
        Assert.Single(haghburg.Skills, skill => skill.Name == "whirlwind");
        Assert.Contains(haghburg.Skills, skill => skill.Name == "cyclone");
        Assert.Equal("Koszmary Pustyni Kaan-ar", haghburg.Area);

        Assert.Equal(8, teachers.Sum(teacher => teacher.Skills.Count(skill => skill.Name == "twohanded weapon mastery")));
        Assert.Equal(9, teachers.Sum(teacher => teacher.Skills.Count(skill => skill.Name == "unity with familiar")));
        Assert.Equal(5, teachers.Sum(teacher => teacher.Skills.Count(skill => skill.Name == "bladedance")));
        Assert.Equal(5, teachers.Sum(teacher => teacher.Skills.Count(skill => skill.Name == "bladefury")));
        Assert.Equal(5, teachers.Sum(teacher => teacher.Skills.Count(skill => skill.Name == "desert bond")));
        Assert.Equal(4, teachers.Sum(teacher => teacher.Skills.Count(skill => skill.Name == "loth prayer")));

        var yergiz = Assert.Single(teachers, teacher => teacher.MobVnum == "52");
        Assert.Contains(yergiz.Skills, skill => skill == new TeacherSkillEntry(
            "twohanded weapon mastery", 0, 45, 35, 40));

        var sareech = Assert.Single(teachers, teacher => teacher.MobVnum == "34626");
        Assert.Contains(sareech.Skills, skill => skill == new TeacherSkillEntry(
            "unity with familiar", 0, 29, 0, 0));

        var lothTeacher = Assert.Single(teachers, teacher => teacher.MobVnum == "66989");
        Assert.Contains(lothTeacher.Skills, skill => skill == new TeacherSkillEntry(
            "loth prayer", 70, 95, 55, 65));
    }

    [Fact]
    public void Catalog_ImportsTeacherTricksWithLearnChanceAndPrice()
    {
        var teachers = TeacherCatalogLoader.Load();
        (string MobVnum, string Name, int LearnChance, int Price)[] expected =
        [
            ("1354", "vertical kick", 25, 5000),
            ("1354", "staff swirl", 23, 5250),
            ("27577", "entwine", 20, 8000),
            ("27577", "weapon wrench", 23, 9000),
            ("27662", "riposte", 19, 11000),
            ("6611", "cyclone", 18, 7500),
            ("6199", "flabbergast", 12, 3700),
            ("6460", "dragon strike", 16, 10000),
            ("6460", "glorious impale", 16, 8188),
            ("28598", "decapitation", 11, 6000),
            ("10952", "thundering whack", 18, 7450),
            ("17938", "strucking wallop", 19, 7111),
            ("16601", "shove", 35, 5900),
            ("16601", "thigh jab", 25, 6666),
            ("4507", "bleed", 21, 7878),
            ("43911", "ravaging orb", 23, 8000),
            ("40342", "crushing mace", 25, 6543),
            ("33013", "thousandslayer", 21, 8765),
            ("33013", "divine impact", 15, 7240),
            ("923", "divine impact", 15, 7240),
            ("14961", "lethal blow", 5, 25000),
            ("14961", "thigh jab", 10, 5000),
        ];

        Assert.Equal(expected.Length, teachers.Sum(teacher => teacher.Tricks.Count));
        foreach (var item in expected)
        {
            var teacher = Assert.Single(teachers, teacher => teacher.MobVnum == item.MobVnum);
            Assert.Contains(
                new TeacherTrickEntry(item.Name, item.LearnChance, item.Price),
                teacher.Tricks);
        }

        var keredel = Assert.Single(teachers, teacher => teacher.MobVnum == "1354");
        Assert.Equal("Jedzący mnich Keredel", keredel.Name);
        Assert.False(keredel.HasRoomLocation);
    }

    [Fact]
    public void TeacherSearch_MatchesDiacriticsSkillsAndVnum()
    {
        var viewModel = CreateViewModel();

        viewModel.TeacherSearchText = "zlodziej bladesplash";
        Assert.Contains(viewModel.FilteredTeachers, teacher => teacher.MobVnum == "1960");

        viewModel.TeacherSearchText = "42832 panther";
        var della = Assert.Single(viewModel.FilteredTeachers);
        Assert.Equal("Druidka Della", della.Name);
        Assert.Same(della, viewModel.SelectedTeacher);

        viewModel.TeacherSearchText = "33013 thousandslayer";
        var trickTeacher = Assert.Single(viewModel.FilteredTeachers);
        Assert.Equal("Władca mroku", trickTeacher.Name);
    }

    [Fact]
    public void ShowTeacherOnMapCommand_OnlyInvokesCallbackForKnownRoom()
    {
        TeacherEntry? requestedTeacher = null;
        var teachers = TeacherCatalogLoader.Load();
        var mappedTeacher = teachers.First(teacher => teacher.HasRoomLocation);
        var viewModel = new KilleropediaViewModel(
            teachers,
            CreateBookStore(),
            null,
            teacher => requestedTeacher = teacher,
            CreateLoreCatalog());

        Assert.True(viewModel.ShowTeacherOnMapCommand.CanExecute(mappedTeacher));
        viewModel.ShowTeacherOnMapCommand.Execute(mappedTeacher);

        Assert.Same(mappedTeacher, requestedTeacher);
        Assert.False(viewModel.ShowTeacherOnMapCommand.CanExecute(mappedTeacher with { RoomVnum = null }));
    }

    [AvaloniaFact]
    public void TeachersView_RendersCatalogAndSelectedTeacherDetails()
    {
        var viewModel = CreateViewModel();
        var view = new KilleropediaTeachersView { DataContext = viewModel };
        var window = new Window { Width = 1100, Height = 720, Content = view };

        window.Show();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();

        Assert.Equal(17, view.FontSize);
        Assert.Contains("Almendra", view.FontFamily.ToString(), StringComparison.Ordinal);
        Assert.Equal(Avalonia.Media.FontStyle.Normal, view.FontStyle);
        Assert.Equal(Avalonia.Media.FontWeight.Bold, view.FontWeight);

        var list = view.GetVisualDescendants().OfType<ListBox>().Single();
        Assert.Equal(151, list.ItemCount);
        Assert.NotNull(viewModel.SelectedTeacher);
        Assert.Contains(
            view.GetVisualDescendants().OfType<TextBlock>(),
            text => text.Text == viewModel.SelectedTeacher.Name);

        var offeringTabs = view.FindControl<TabControl>("TeacherOfferingTabs");
        Assert.NotNull(offeringTabs);
        Assert.Equal(2, offeringTabs!.ItemCount);
        Assert.Equal("Umiejętności", Assert.IsType<TabItem>(offeringTabs.Items[0]).Header);
        Assert.Equal("Triki", Assert.IsType<TabItem>(offeringTabs.Items[1]).Header);

        var detailsScroller = Assert.Single(
            view.GetVisualDescendants().OfType<ScrollViewer>(),
            scroller => scroller.Classes.Contains("killeropedia-content-scroll"));
        Assert.Equal(14, detailsScroller.Padding.Right);

        var bookPages = view.GetVisualDescendants()
            .OfType<Border>()
            .Where(border => border.Classes.Contains("killeropedia-left-page") ||
                             border.Classes.Contains("killeropedia-right-page"))
            .ToArray();
        Assert.Equal(2, bookPages.Length);
        Assert.All(bookPages, page => Assert.IsType<Avalonia.Media.ImageBrush>(page.Background));

        window.Close();
    }

    [AvaloniaFact]
    public async Task BooksView_LoadsJsonFiltersSpellsAndKeepsDeveloperRefreshDisabled()
    {
        var store = CreateBookStore();
        await store.SaveAsync(new BookCatalogDocument
        {
            GeneratedAtUtc = DateTimeOffset.Parse("2026-07-13T12:00:00Z"),
            Classes = ["mag", "druid"],
            Books =
            [
                new BookEntry
                {
                    Vnum = 16818,
                    Name = "ksiega zaklec",
                    Classes = ["mag"],
                    Spells = ["force bolt", "decay"],
                    LoadLocations = ["na mobie: Zeerith'din (Podmrok)"],
                },
                new BookEntry
                {
                    Vnum = 25000,
                    Name = "druidzki notatnik",
                    Classes = ["druid"],
                    Spells = ["bear form"],
                },
            ],
        }, TestContext.Current.CancellationToken);
        var viewModel = new KilleropediaViewModel(
            TeacherCatalogLoader.Load(),
            store,
            null,
            loreCatalog: CreateLoreCatalog());
        viewModel.BookSearchText = "zeerith force";
        Assert.Equal(16818, Assert.Single(viewModel.FilteredBooks).Vnum);

        viewModel.BookSearchText = string.Empty;
        viewModel.SelectedBookClass = "druid";
        Assert.Equal(25000, Assert.Single(viewModel.FilteredBooks).Vnum);
        Assert.False(viewModel.IsBookRefreshEnabled);

        var view = new KilleropediaBooksView { DataContext = viewModel };
        var window = new Window { Width = 1100, Height = 720, Content = view };
        window.Show();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();

        var refresh = view.GetVisualDescendants()
            .OfType<Button>()
            .Single(button => button.Content?.ToString() == "Odśwież");
        Assert.False(refresh.IsEnabled);
        Assert.Contains(
            view.GetVisualDescendants().OfType<TextBlock>(),
            text => text.Text == "druidzki notatnik");

        var detailsScroller = Assert.Single(
            view.GetVisualDescendants().OfType<ScrollViewer>(),
            scroller => scroller.Classes.Contains("killeropedia-content-scroll"));
        Assert.Equal(14, detailsScroller.Padding.Right);

        window.Close();
    }

    [Fact]
    public void LoreCatalog_LoadsArticlesRecordsAndClickableRelations()
    {
        var catalog = CreateLoreCatalog();

        Assert.Equal(179, catalog.Entries.Count);
        var arras = Assert.Single(catalog.Entries, entry => entry.Id == "place:arras");
        Assert.Equal("Arras", arras.Name);
        Assert.Contains(arras.Sections, section => section.Title == "Władza i porządek");
        Assert.Contains(arras.Links, link => link.TargetId == "character:khabar");
        Assert.Contains(arras.Sources, source => source.DisplayText.Contains("arras.are", StringComparison.Ordinal));

        var easterial = Assert.Single(catalog.Entries, entry => entry.Id == "place:easterial");
        Assert.Contains(easterial.Links, link => link.TargetId == "place:dinneshere");
        Assert.Contains(easterial.Links, link => link.TargetId == "character:eltar-odwazny");
        var easterialCharacter = Assert.Single(easterial.Facts, fact => fact.Label == "Charakter osady");
        Assert.DoesNotContain("settlement", easterialCharacter.Label, StringComparison.OrdinalIgnoreCase);
        var silea = Assert.Single(catalog.Entries, entry => entry.Id == "place:silea");
        Assert.Contains(silea.Links, link => link.TargetId == "deity:silea");
        Assert.Contains(silea.Links, link => link.TargetId == "character:morhin-atyer");
        Assert.Contains(silea.Sections, section => section.Title == "Miasto i port");
        Assert.DoesNotContain(
            catalog.Entries.SelectMany(entry => entry.Facts).Select(fact => fact.Label),
            label => label.Contains('_'));
        Assert.DoesNotContain(
            catalog.Entries.SelectMany(entry => entry.Links).Select(link => link.RelationText),
            label => label.Contains('_'));
        Assert.Single(catalog.Entries, entry => entry.Id == "place:karakris");
    }

    [Fact]
    public void LoreSearch_MatchesWithoutDiacriticsAndNavigatesRelations()
    {
        var viewModel = CreateViewModel();

        viewModel.LoreSearchText = "eltar odwazny";
        Assert.Contains(viewModel.FilteredLoreEntries, entry => entry.Id == "character:eltar-odwazny");

        viewModel.LoreSearchText = string.Empty;
        viewModel.SelectedLoreCategory = "Miejsca";
        var easterial = Assert.Single(viewModel.FilteredLoreEntries, entry => entry.Id == "place:easterial");
        var eltarLink = Assert.Single(easterial.Links, link => link.TargetId == "character:eltar-odwazny");

        viewModel.NavigateLoreCommand.Execute(eltarLink);

        Assert.Equal("Wszystkie", viewModel.SelectedLoreCategory);
        Assert.Equal("character:eltar-odwazny", viewModel.SelectedLoreEntry?.Id);
    }

    [AvaloniaFact]
    public void LoreText_CreatesClickableLinksForKnownEntries()
    {
        var viewModel = CreateViewModel();
        var arras = Assert.Single(viewModel.LoreEntries, entry => entry.Id == "place:arras");
        var textBlock = new TextBlock { FontSize = 18 };

        LoreTextLinks.SetText(textBlock, "Z Arras trakt prowadzi do Carrallak.");
        LoreTextLinks.SetEntries(textBlock, viewModel.LoreEntries);
        LoreTextLinks.SetCurrentEntryId(textBlock, arras.Id);
        LoreTextLinks.SetCommand(textBlock, viewModel.NavigateLoreCommand);

        var inline = Assert.Single(textBlock.Inlines!.OfType<InlineUIContainer>());
        Assert.Equal(Avalonia.Media.BaselineAlignment.TextBottom, inline.BaselineAlignment);
        var button = Assert.IsType<Button>(inline.Child);
        var label = Assert.IsType<TextBlock>(button.Content);
        Assert.Equal(textBlock.FontSize, label.FontSize);
        var link = Assert.IsType<LoreLink>(button.CommandParameter);
        Assert.Equal("place:carrallak", link.TargetId);
        Assert.Contains("killeropedia-inline-link", button.Classes);

        button.Command!.Execute(button.CommandParameter);

        Assert.Equal("place:carrallak", viewModel.SelectedLoreEntry?.Id);
    }

    [Fact]
    public void LoreCatalog_InvalidExternalOverrideFallsBackToEmbeddedCatalog()
    {
        var dataDirectory = Path.Combine(_directory, "Data");
        Directory.CreateDirectory(dataDirectory);
        File.WriteAllText(Path.Combine(dataDirectory, "lore-catalog.json.gz"), "uszkodzony katalog");

        var catalog = LoreCatalogLoader.Load(_directory);

        Assert.Equal(179, catalog.Entries.Count);
        Assert.Equal("katalog wbudowany", catalog.SourceText);
        Assert.Contains("Nie udało się wczytać", catalog.Warning);
    }

    [AvaloniaFact]
    public void LoreView_RendersCatalogDetailsAndClickableRelations()
    {
        var viewModel = CreateViewModel();
        viewModel.LoreSearchText = "Arras";
        var view = new KilleropediaLoreView { DataContext = viewModel };
        var window = new Window { Width = 1100, Height = 720, Content = view };

        window.Show();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();

        var searchBox = Assert.Single(view.GetVisualDescendants().OfType<TextBox>());
        Assert.True(searchBox.Focus());
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        var searchBorder = Assert.Single(
            searchBox.GetVisualDescendants().OfType<Border>(),
            border => border.Name == "PART_BorderElement");
        var searchBackground = Assert.IsAssignableFrom<Avalonia.Media.ISolidColorBrush>(searchBorder.Background);
        Assert.Equal(Avalonia.Media.Color.Parse("#D8EBD6A8"), searchBackground.Color);

        var categoryTabs = Assert.Single(
            view.GetVisualDescendants().OfType<ListBox>(),
            list => list.Classes.Contains("killeropedia-category-tabs"));
        Assert.Equal(viewModel.AvailableLoreCategories.Count, categoryTabs.ItemCount);
        Assert.DoesNotContain(view.GetVisualDescendants(), visual => visual is ComboBox);

        var list = Assert.Single(
            view.GetVisualDescendants().OfType<ListBox>(),
            candidate => !candidate.Classes.Contains("killeropedia-category-tabs"));
        Assert.True(list.ItemCount > 0);
        Assert.Contains(
            view.GetVisualDescendants().OfType<TextBlock>(),
            text => text.Text == "Arras" || MudClient.App.Behaviors.SearchHighlight.GetText(text) == "Arras");
        Assert.Contains(
            view.GetVisualDescendants().OfType<Button>(),
            button => button.Command == viewModel.NavigateLoreCommand);
        var inlineLinks = view.GetVisualDescendants().OfType<Button>()
            .Where(button => button.Classes.Contains("killeropedia-inline-link"))
            .ToArray();
        Assert.NotEmpty(inlineLinks);
        var inlineLink = inlineLinks[0];
        Assert.Equal(new Avalonia.Thickness(0), inlineLink.BorderThickness);
        Assert.Equal(
            Avalonia.Media.Colors.Transparent,
            Assert.IsAssignableFrom<Avalonia.Media.ISolidColorBrush>(inlineLink.Background).Color);
        var inlineLabel = Assert.IsType<TextBlock>(inlineLink.Content);
        Assert.Equal(
            Avalonia.Media.Color.Parse("#2C2110"),
            Assert.IsAssignableFrom<Avalonia.Media.ISolidColorBrush>(inlineLabel.Foreground).Color);
        Assert.NotNull(inlineLabel.TextDecorations);

        var detailsScroller = Assert.Single(
            view.GetVisualDescendants().OfType<ScrollViewer>(),
            scroller => scroller.Classes.Contains("killeropedia-content-scroll"));
        Assert.Equal(14, detailsScroller.Padding.Right);

        window.Close();
    }

    private KilleropediaViewModel CreateViewModel() =>
        new(
            TeacherCatalogLoader.Load(),
            CreateBookStore(),
            null,
            loreCatalog: CreateLoreCatalog());

    private static LoreCatalogData CreateLoreCatalog() => LoreCatalogLoader.LoadEmbedded();

    private BookCatalogStore CreateBookStore() =>
        new(Path.Combine(_directory, "killeropedia-books.json"));
}

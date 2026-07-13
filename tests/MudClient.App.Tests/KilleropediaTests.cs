using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using MudClient.App.Models;
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

        Assert.Equal(150, teachers.Count);
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
    public void TeacherSearch_MatchesDiacriticsSkillsAndVnum()
    {
        var viewModel = CreateViewModel();

        viewModel.TeacherSearchText = "zlodziej bladesplash";
        Assert.Contains(viewModel.FilteredTeachers, teacher => teacher.MobVnum == "1960");

        viewModel.TeacherSearchText = "42832 panther";
        var della = Assert.Single(viewModel.FilteredTeachers);
        Assert.Equal("Druidka Della", della.Name);
        Assert.Same(della, viewModel.SelectedTeacher);
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
            teacher => requestedTeacher = teacher);

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

        var list = view.GetVisualDescendants().OfType<ListBox>().Single();
        Assert.Equal(150, list.ItemCount);
        Assert.NotNull(viewModel.SelectedTeacher);
        Assert.Contains(
            view.GetVisualDescendants().OfType<TextBlock>(),
            text => text.Text == viewModel.SelectedTeacher.Name);

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
        var viewModel = new KilleropediaViewModel(TeacherCatalogLoader.Load(), store, null);
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

        window.Close();
    }

    private KilleropediaViewModel CreateViewModel() =>
        new(TeacherCatalogLoader.Load(), CreateBookStore(), null);

    private BookCatalogStore CreateBookStore() =>
        new(Path.Combine(_directory, "killeropedia-books.json"));
}

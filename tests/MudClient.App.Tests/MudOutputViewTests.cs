using System.Reflection;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Layout;
using MudClient.App.Controls;
using MudClient.App.Services;
using MudClient.App.ViewModels;
using MudClient.App.Views.Panels;
using Xunit;

namespace MudClient.App.Tests;

[Collection(AvaloniaUiCollection.Name)]
public sealed class MudOutputViewTests
{
    [AvaloniaFact]
    public void SearchBox_IsOnRightSideOfTerminalInput()
    {
        var terminal = new TerminalPanelView();
        var commandBox = terminal.FindControl<TextBox>("CommandBox");
        var searchBox = terminal.FindControl<TextBox>("SearchBox");

        Assert.NotNull(commandBox);
        Assert.NotNull(searchBox);
        Assert.Equal("Search...", searchBox!.PlaceholderText);
        Assert.Same(commandBox!.Parent, searchBox.Parent);
        Assert.Equal(0, Grid.GetColumn(commandBox));
        Assert.Equal(1, Grid.GetColumn(searchBox));
    }

    [AvaloniaFact]
    public async Task VitalsBars_HideAndReleaseTheirTerminalColumns()
    {
        var settingsDirectory = Path.Combine(
            Path.GetTempPath(),
            "KillerMudClient_VitalsUiTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(settingsDirectory);
        var viewModel = new MainWindowViewModel(
            settingsService: new AppSettingsService(settingsDirectory));
        var terminal = new TerminalPanelView { DataContext = viewModel };
        var window = new Window { Width = 800, Height = 500, Content = terminal };
        window.Show();

        try
        {
            var hpBar = terminal.FindControl<Border>("HitPointsBar")!;
            var mvBar = terminal.FindControl<Border>("MovementPointsBar")!;
            var output = terminal.FindControl<MudOutputView>("MudOutput")!;
            var widthWithBars = output.Bounds.Width;

            Assert.True(hpBar.IsVisible);
            Assert.True(mvBar.IsVisible);

            viewModel.ShowTerminalVitalsBars = false;
            window.UpdateLayout();

            Assert.False(hpBar.IsVisible);
            Assert.False(mvBar.IsVisible);
            Assert.True(output.Bounds.Width > widthWithBars + 80);
        }
        finally
        {
            window.Close();
            await viewModel.DisposeAsync();
            Directory.Delete(settingsDirectory, recursive: true);
        }
    }

    [AvaloniaFact]
    public void WordWrap_DisablesHorizontalScrollbars()
    {
        var output = new MudOutputView { WordWrap = true };

        Assert.Equal(
            ScrollBarVisibility.Disabled,
            output.FindControl<ScrollViewer>("ScrollbackScroller")!.HorizontalScrollBarVisibility);

        output.WordWrap = false;

        Assert.Equal(
            ScrollBarVisibility.Auto,
            output.FindControl<ScrollViewer>("ScrollbackScroller")!.HorizontalScrollBarVisibility);
    }

    [AvaloniaFact]
    public void CloseSplitButton_ReturnsOutputToSinglePane()
    {
        var output = new MudOutputView();
        var splitBar = output.FindControl<Grid>("SplitBar");
        var liveTail = output.FindControl<ScrollViewer>("LiveTailScroller");
        var closeButton = output.FindControl<Button>("CloseSplitButton");

        Assert.NotNull(splitBar);
        Assert.NotNull(liveTail);
        Assert.NotNull(closeButton);

        SetSplitMode(output, true);
        Assert.True(splitBar!.IsVisible);
        Assert.True(liveTail!.IsVisible);

        closeButton!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        Assert.False(splitBar.IsVisible);
        Assert.False(liveTail.IsVisible);
    }

    [AvaloniaFact]
    public void Search_SelectsNewestMatch_AndMovesThroughOlderMatches()
    {
        var output = new MudOutputView();
        output.AppendText("pierwszy smok\nbez trafienia\nostatni smok\n");

        Assert.True(output.Search("SMOK"));
        Assert.Equal("smok", output.SelectedSearchText);
        Assert.Equal(2, output.SelectedSearchGlobalLine);

        Assert.True(output.Search("smok"));
        Assert.Equal("smok", output.SelectedSearchText);
        Assert.Equal(0, output.SelectedSearchGlobalLine);

        var splitBar = output.FindControl<Grid>("SplitBar");
        Assert.True(splitBar!.IsVisible);
    }

    [AvaloniaFact]
    public void SearchNavigationButtons_StepThroughMatchesLikeEnterAndShiftEnter()
    {
        var terminal = new TerminalPanelView();
        var output = terminal.FindControl<MudOutputView>("MudOutput")!;
        var searchBox = terminal.FindControl<TextBox>("SearchBox")!;
        var prevButton = terminal.FindControl<Button>("SearchPrevButton")!;
        var nextButton = terminal.FindControl<Button>("SearchNextButton")!;

        // TerminalPanelView seeds the output with a greeting on construction, so match
        // positions are asserted relative to each other rather than as absolute line numbers.
        output.AppendText("pierwszy smok\nbez trafienia\nostatni smok\n");
        searchBox.Text = "smok";
        Assert.True(output.UpdateSearch("smok"));
        var newestMatchLine = output.SelectedSearchGlobalLine;
        Assert.NotNull(newestMatchLine);

        prevButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        var olderMatchLine = output.SelectedSearchGlobalLine;
        Assert.NotNull(olderMatchLine);
        Assert.True(olderMatchLine < newestMatchLine);

        nextButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.Equal(newestMatchLine, output.SelectedSearchGlobalLine);
    }

    [AvaloniaFact]
    public void TerminalWheelScroll_UsesFourLineLogicalStep()
    {
        var pane = new OutputPaneControl();
        var logicalScrollable = (ILogicalScrollable)pane;

        Assert.True(logicalScrollable.ScrollSize.Height > 40);
    }

    [AvaloniaFact]
    public async Task TerminalCtrlC_CopiesOutputSelection_AndFallsBackToCommandInput()
    {
        var terminal = new TerminalPanelView();
        var commandBox = terminal.FindControl<TextBox>("CommandBox")!;
        var output = terminal.FindControl<MudOutputView>("MudOutput")!;
        var window = new Window { Content = terminal };
        window.Show();
        var clipboard = Assert.IsAssignableFrom<IClipboard>(window.Clipboard);

        try
        {
            commandBox.Text = "tekst inputu";
            commandBox.SelectAll();
            commandBox.Focus();

            output.AppendText("tekst terminala\n");
            Assert.True(output.UpdateSearch("tekst terminala"));

            window.KeyPress(Key.C, RawInputModifiers.Control, PhysicalKey.C, null);

            Assert.Equal("tekst terminala", await clipboard.TryGetTextAsync());

            output.Clear();
            commandBox.SelectAll();

            window.KeyPress(Key.C, RawInputModifiers.Control, PhysicalKey.C, null);

            Assert.Equal("tekst inputu", await clipboard.TryGetTextAsync());
        }
        finally
        {
            window.Close();
        }
    }

    private static void SetSplitMode(MudOutputView output, bool enabled)
    {
        var method = typeof(MudOutputView).GetMethod(
            "SetSplitMode",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(method);
        method!.Invoke(output, [enabled]);
    }
}

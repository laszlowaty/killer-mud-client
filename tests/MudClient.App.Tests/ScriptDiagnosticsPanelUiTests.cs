using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MudClient.App.Models;
using MudClient.App.Services;
using MudClient.App.ViewModels;
using MudClient.App.Views.Panels;

namespace MudClient.App.Tests;

[Collection(AvaloniaUiCollection.Name)]
public sealed class ScriptDiagnosticsPanelUiTests
{
    [AvaloniaFact]
    public async Task Panels_ShowLiveJavaScriptLogsAndProfileVariables()
    {
        var directory = Directory.CreateTempSubdirectory("script-diagnostics-").FullName;
        var viewModel = new MainWindowViewModel(
            profileService: new ProfileService(directory),
            settingsService: new AppSettingsService(directory),
            dockLayoutService: new DockLayoutService(directory));

        try
        {
            await viewModel.RunScriptCommand.ExecuteAsync(new ScriptEntry
            {
                Name = "diagnostyka",
                Code =
                    """
                    variables.set("combat.target", { name: "ork", hp: 42 });
                    log("Wybrano cel", variables.get("combat.target"));
                    console.warn("Niski zapas many");
                    """,
            });
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(2, viewModel.ScriptLogs.Count);
            Assert.Equal("INFO", viewModel.ScriptLogs[0].Level);
            Assert.Equal("WARN", viewModel.ScriptLogs[1].Level);
            var variable = Assert.Single(viewModel.ScriptVariables);
            Assert.Equal("combat.target", variable.Name);
            Assert.Equal("""{"name":"ork","hp":42}""", variable.ValueJson);

            var content = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*,*"),
                Children =
                {
                    new JavaScriptConsolePanelView { DataContext = viewModel },
                    new ScriptVariablesPanelView
                    {
                        DataContext = viewModel,
                        [Grid.ColumnProperty] = 1,
                    },
                },
            };
            var window = new Window { Width = 900, Height = 500, Content = content };
            window.Show();
            try
            {
                window.UpdateLayout();
                var texts = window.GetVisualDescendants()
                    .OfType<TextBlock>()
                    .Select(text => text.Text)
                    .ToList();
                var selectableTexts = window.GetVisualDescendants()
                    .OfType<SelectableTextBlock>()
                    .Select(text => text.Text)
                    .ToList();

                Assert.Contains("WARN", texts);
                Assert.Contains("diagnostyka", texts);
                Assert.Contains(
                    selectableTexts,
                    text => text?.Contains("Wybrano cel", StringComparison.Ordinal) == true);
                Assert.Contains("combat.target", selectableTexts);
                Assert.Contains("""{"name":"ork","hp":42}""", selectableTexts);
            }
            finally
            {
                window.Close();
            }

            viewModel.ClearScriptLogsCommand.Execute(null);
            Assert.Empty(viewModel.ScriptLogs);
        }
        finally
        {
            await viewModel.DisposeAsync();
            Directory.Delete(directory, recursive: true);
        }
    }
}

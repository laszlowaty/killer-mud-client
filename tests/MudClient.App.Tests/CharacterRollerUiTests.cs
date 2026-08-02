using Avalonia.Headless.XUnit;
using MudClient.App.Services;
using MudClient.App.ViewModels;
using MudClient.App.Views;
using MudClient.Core.Automation;
using Xunit;

namespace MudClient.App.Tests;

[Collection(AvaloniaUiCollection.Name)]
public sealed class CharacterRollerUiTests
{
    [Fact]
    public void RollerUsesMudletCharacterCreationCommands()
    {
        Assert.Equal("n", MainWindowViewModel.CharacterRollAgainCommand);
        Assert.Equal(
            ["t", " ", "12", "t"],
            MainWindowViewModel.CharacterCreationFinishCommands);
    }

    [AvaloniaFact]
    public async Task DetectedStatBlock_OpensConfigurationPopupThroughMainWindow()
    {
        var directory = CreateDirectory();
        var viewModel = CreateViewModel(directory);
        var request = new TaskCompletionSource<(CharacterRollerConfiguration Configuration, CharacterRoll? Roll)>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var window = new MainWindow
        {
            DataContext = viewModel,
            ConfigureCharacterRollerAsync = (_, configuration, roll) =>
            {
                request.TrySetResult((configuration, roll));
                return Task.FromResult<CharacterRollerConfiguration?>(null);
            },
        };

        try
        {
            window.Show();
            viewModel.ObserveCharacterRollLine("STR: 79 INT: 78");
            viewModel.ObserveCharacterRollLine("WIS: 83 DEX: 70");
            viewModel.ObserveCharacterRollLine("CON: 87 CHA: 68");
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            var shown = await request.Task.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.Equal(CharacterRollerConfiguration.Default, shown.Configuration);
            Assert.Equal(465, shown.Roll?.Sum);
        }
        finally
        {
            window.DataContext = null;
            window.Close();
            await viewModel.DisposeAsync();
            Directory.Delete(directory, recursive: true);
        }
    }

    [AvaloniaFact]
    public async Task RerollCommand_IsConsumedAndReopensConfigurationPopup()
    {
        var directory = CreateDirectory();
        var viewModel = CreateViewModel(directory);
        var requestCount = 0;
        var window = new MainWindow
        {
            DataContext = viewModel,
            ConfigureCharacterRollerAsync = (_, _, _) =>
            {
                requestCount++;
                return Task.FromResult<CharacterRollerConfiguration?>(null);
            },
        };

        try
        {
            window.Show();

            Assert.True(viewModel.TryHandleCharacterRollerCommand("/ReRoLl"));
            Assert.False(viewModel.TryHandleCharacterRollerCommand("reroll"));
            Assert.Equal(1, requestCount);
        }
        finally
        {
            window.DataContext = null;
            window.Close();
            await viewModel.DisposeAsync();
            Directory.Delete(directory, recursive: true);
        }
    }

    private static MainWindowViewModel CreateViewModel(string directory) =>
        new(
            new ProfileService(directory),
            new AppSettingsService(directory),
            new DockLayoutService(directory));

    private static string CreateDirectory()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "KillerMudClient_CharacterRoller_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}

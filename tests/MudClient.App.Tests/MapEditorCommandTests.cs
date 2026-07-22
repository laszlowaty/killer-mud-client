using System.Reflection;
using MudClient.App.Services;
using MudClient.App.ViewModels;

namespace MudClient.App.Tests;

public sealed class MapEditorCommandTests
{
    [Theory]
    [InlineData("/map status")]
    [InlineData("/mapa status")]
    [InlineData("+map status")]
    [InlineData("/map area Nowa kraina")]
    [InlineData("/map symbol !!")]
    [InlineData("/map label ## Niebezpieczne miejsce!")]
    [InlineData("/map forget")]
    [InlineData("/map special e przecisnij")]
    [InlineData("/map check")]
    [InlineData("/map cancel")]
    [InlineData("/map diff")]
    [InlineData("/map export C:\\temp\\world-map.json")]
    [InlineData("/map discard")]
    [InlineData("/map resolve keep")]
    [InlineData("/map redo")]
    [InlineData("/map import C:\\temp\\world-map.json confirm")]
    [InlineData("/map room name Nowa nazwa")]
    [InlineData("/map room sector gory")]
    [InlineData("/map room weight 2.5")]
    [InlineData("/map room move 1 2 0")]
    [InlineData("/map label list")]
    [InlineData("/map label set 1 Nowa etykieta")]
    [InlineData("/map label delete 1")]
    public async Task TypedMapCommands_AreConsumedLocallyInLordMode(string command)
    {
        var directory = Path.Combine(Path.GetTempPath(), "KillerMudClient_MapCommandTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            await using var viewModel = new MainWindowViewModel(settingsService: new AppSettingsService(directory));
            viewModel.LordModeEnabled = true;

            var consumed = await InvokeMapCommandAsync(viewModel, command);

            Assert.True(consumed);
            Assert.NotEmpty(viewModel.Toasts);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task TypedMapCommand_OutsideLordMode_IsConsumedWithExplanation()
    {
        var directory = Path.Combine(Path.GetTempPath(), "KillerMudClient_MapCommandTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            await using var viewModel = new MainWindowViewModel(settingsService: new AppSettingsService(directory));

            var consumed = await InvokeMapCommandAsync(viewModel, "/map start");

            Assert.True(consumed);
            Assert.Contains("tylko w trybie lorda", viewModel.Toasts[^1].Text, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static Task<bool> InvokeMapCommandAsync(MainWindowViewModel viewModel, string command)
    {
        var method = typeof(MainWindowViewModel).GetMethod(
            "TryHandleMapEditorCommandAsync",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (Task<bool>)method.Invoke(viewModel, [command])!;
    }
}

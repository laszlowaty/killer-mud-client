using System.Reflection;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using MudClient.App.Models;
using MudClient.App.Services;
using MudClient.App.ViewModels;

namespace MudClient.App.Tests;

[Collection(AvaloniaUiCollection.Name)]
public sealed class AutomationCommandEchoUiTests
{
    [AvaloniaFact]
    public async Task TriggerCommand_IsEchoedToTerminal()
    {
        var (viewModel, directory) = CreateViewModel();
        var output = new List<string>();
        viewModel.OutputReceived += output.Add;

        try
        {
            var method = typeof(MainWindowViewModel).GetMethod(
                "SendTriggeredCommandAsync",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(method);

            await Assert.IsAssignableFrom<Task>(method!.Invoke(viewModel, ["wstan", CancellationToken.None]));
            Dispatcher.UIThread.RunJobs();

            Assert.Contains(output, line => line.Contains("> wstan", StringComparison.Ordinal));
        }
        finally
        {
            await DisposeAsync(viewModel, directory);
        }
    }

    [AvaloniaFact]
    public async Task TimerCommand_IsEchoedToTerminal()
    {
        var (viewModel, directory) = CreateViewModel();
        var echoReceived = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        viewModel.OutputReceived += text =>
        {
            if (text.Contains("> spojrz", StringComparison.Ordinal))
            {
                echoReceived.TrySetResult(text);
            }
        };

        try
        {
            SetConnected(viewModel);
            var timer = new TimerEntry
            {
                Name = "Obserwacja",
                Milliseconds = 10,
                CommandsText = "spojrz",
                IsEnabled = true,
            };

            var method = typeof(MainWindowViewModel).GetMethod(
                "SyncTimer",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(method);
            method!.Invoke(viewModel, [timer]);

            var echo = await echoReceived.Task.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.Contains("> spojrz", echo, StringComparison.Ordinal);
        }
        finally
        {
            await DisposeAsync(viewModel, directory);
        }
    }

    private static (MainWindowViewModel ViewModel, string Directory) CreateViewModel()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "KillerMudClient_AutomationEcho_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return (new MainWindowViewModel(settingsService: new AppSettingsService(directory)), directory);
    }

    private static void SetConnected(MainWindowViewModel viewModel)
    {
        var field = typeof(MainWindowViewModel).GetField(
            "_isConnected",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(field);
        field!.SetValue(viewModel, true);
    }

    private static async Task DisposeAsync(MainWindowViewModel viewModel, string directory)
    {
        await viewModel.DisposeAsync();
        Directory.Delete(directory, recursive: true);
    }
}

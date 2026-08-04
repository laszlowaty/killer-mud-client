using System.Reflection;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using MudClient.App.Models;
using MudClient.App.Services;
using MudClient.App.ViewModels;
using MudClient.Core.Automation;
using MudClient.Core.Gmcp;

namespace MudClient.App.Tests;

[Collection(AvaloniaUiCollection.Name)]
public sealed class AutomationCommandEchoUiTests
{
    private const string EchoInvocation = "echo(\"red\", \"Straciłeś ochronę!\")";
    private const string ExpectedEcho = "\u001b[31mStraciłeś ochronę!\u001b[0m\n";

    [AvaloniaFact]
    public async Task EffectsEcho_IsDisplayedOnlyWhenEffectIsLost()
    {
        var (viewModel, directory) = CreateViewModel();
        var output = new List<string>();
        viewModel.OutputReceived += output.Add;

        try
        {
            InvokeAffectsChanged(
                viewModel,
                [new CharacterAffect("Ochrona", string.Empty, false, false, null)]);
            Dispatcher.UIThread.RunJobs();
            Assert.Empty(output);

            InvokeAffectsChanged(
                viewModel,
                [
                    new CharacterAffect("Ochrona", string.Empty, false, false, null),
                    new CharacterAffect("Błogosławieństwo", string.Empty, false, false, null),
                ]);
            Dispatcher.UIThread.RunJobs();
            Assert.Empty(output);

            InvokeAffectsChanged(
                viewModel,
                [new CharacterAffect("Błogosławieństwo", string.Empty, false, false, null)]);
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(
                ["\n\u001b[31mUtracono efekt: Ochrona.\u001b[0m\n"],
                output);
        }
        finally
        {
            await DisposeAsync(viewModel, directory);
        }
    }

    [AvaloniaFact]
    public async Task AliasEcho_IsDisplayedLocally()
    {
        var (viewModel, directory) = CreateViewModel();
        var output = new List<string>();
        viewModel.OutputReceived += output.Add;

        try
        {
            SetConnected(viewModel);
            GetAliases(viewModel).Add(new AliasRule("ostrzeżenie", "^ostrzez$", EchoInvocation));
            viewModel.CommandText = "ostrzez";

            await viewModel.SendCommandCommand.ExecuteAsync(null);

            Assert.Contains(ExpectedEcho, output);
            Assert.DoesNotContain(output, line => line.Contains("> echo(", StringComparison.Ordinal));
        }
        finally
        {
            await DisposeAsync(viewModel, directory);
        }
    }

    [AvaloniaFact]
    public async Task TriggerEcho_IsDisplayedLocally()
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

            await Assert.IsAssignableFrom<Task>(
                method!.Invoke(viewModel, [EchoInvocation, CancellationToken.None]));
            Dispatcher.UIThread.RunJobs();

            Assert.Contains(ExpectedEcho, output);
            Assert.DoesNotContain(output, line => line.Contains("> echo(", StringComparison.Ordinal));
        }
        finally
        {
            await DisposeAsync(viewModel, directory);
        }
    }

    [AvaloniaFact]
    public async Task FloatingButtonEcho_IsDisplayedLocally()
    {
        var (viewModel, directory) = CreateViewModel();
        var output = new List<string>();
        viewModel.OutputReceived += output.Add;

        try
        {
            SetConnected(viewModel);

            await viewModel.SendFloatingCommand.ExecuteAsync(EchoInvocation);

            Assert.Contains(ExpectedEcho, output);
            Assert.DoesNotContain(output, line => line.Contains("> echo(", StringComparison.Ordinal));
        }
        finally
        {
            await DisposeAsync(viewModel, directory);
        }
    }

    [AvaloniaFact]
    public async Task TimerEcho_IsDisplayedLocally()
    {
        var (viewModel, directory) = CreateViewModel();
        var echoReceived = new TaskCompletionSource<string>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        viewModel.OutputReceived += text =>
        {
            if (text == ExpectedEcho)
            {
                echoReceived.TrySetResult(text);
            }
        };

        try
        {
            SetConnected(viewModel);
            var timer = new TimerEntry
            {
                Name = "Ostrzeżenie",
                Milliseconds = 10,
                CommandsText = EchoInvocation,
                IsEnabled = true,
            };

            InvokeSyncTimer(viewModel, timer);

            Assert.Equal(
                ExpectedEcho,
                await echoReceived.Task.WaitAsync(TimeSpan.FromSeconds(2)));
        }
        finally
        {
            await DisposeAsync(viewModel, directory);
        }
    }

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

            InvokeSyncTimer(viewModel, timer);

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

    private static AliasEngine GetAliases(MainWindowViewModel viewModel)
    {
        var field = typeof(MainWindowViewModel).GetField(
            "_aliases",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(field);
        return Assert.IsType<AliasEngine>(field!.GetValue(viewModel));
    }

    private static void InvokeSyncTimer(MainWindowViewModel viewModel, TimerEntry timer)
    {
        var method = typeof(MainWindowViewModel).GetMethod(
            "SyncTimer",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(method);
        method!.Invoke(viewModel, [timer]);
    }

    private static void InvokeAffectsChanged(
        MainWindowViewModel viewModel,
        IReadOnlyList<CharacterAffect> affects)
    {
        var method = typeof(MainWindowViewModel).GetMethod(
            "OnCharacterAffectsChanged",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(method);
        method!.Invoke(viewModel, [affects]);
    }

    private static async Task DisposeAsync(MainWindowViewModel viewModel, string directory)
    {
        await viewModel.DisposeAsync();
        Directory.Delete(directory, recursive: true);
    }
}

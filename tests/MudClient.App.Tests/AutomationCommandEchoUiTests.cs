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
            viewModel.NewBuffName = "Ochrona";
            viewModel.AddBuffCommand.Execute(null);
            Assert.Single(viewModel.RequiredBuffs).IsLossNotificationEnabled = true;

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

            Assert.Equal(
                ["\n\u001b[31mUtracono efekt: Ochrona.\u001b[0m\n"],
                output);
            Dispatcher.UIThread.RunJobs();
        }
        finally
        {
            await DisposeAsync(viewModel, directory);
        }
    }

    [AvaloniaFact]
    public async Task EffectsEcho_IgnoresEffectsWithoutLossTracking()
    {
        var (viewModel, directory) = CreateViewModel();
        var output = new List<string>();
        viewModel.OutputReceived += output.Add;

        try
        {
            viewModel.NewBuffName = "Ochrona";
            viewModel.AddBuffCommand.Execute(null);

            InvokeAffectsChanged(
                viewModel,
                [new CharacterAffect("Ochrona", string.Empty, false, false, null)]);
            Dispatcher.UIThread.RunJobs();
            InvokeAffectsChanged(viewModel, []);

            Assert.Empty(output);
            Dispatcher.UIThread.RunJobs();
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
                BindingFlags.NonPublic | BindingFlags.Instance,
                new[] { typeof(string), typeof(bool), typeof(CancellationToken) });
            Assert.NotNull(method);

            await Assert.IsAssignableFrom<Task>(
                method!.Invoke(viewModel, [EchoInvocation, true, CancellationToken.None]));
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
                BindingFlags.NonPublic | BindingFlags.Instance,
                new[] { typeof(string), typeof(bool), typeof(CancellationToken) });
            Assert.NotNull(method);

            await Assert.IsAssignableFrom<Task>(method!.Invoke(viewModel, ["wstan", true, CancellationToken.None]));
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

    [AvaloniaFact]
    public async Task AdvancedAlias_CanUseVariablesAndEcho()
    {
        var (viewModel, directory) = CreateViewModel();
        var output = new List<string>();
        viewModel.OutputReceived += output.Add;

        try
        {
            SetConnected(viewModel);
            viewModel.AutomationRules.Add(new AutomationRuleEntry(
                "js",
                "alias",
                "^js (?<name>.+)$",
                """
                variables.set("target", match.groups.name);
                echo("Cel: " + variables.get("target"), "green");
                """,
                isEnabled: true,
                isAdvanced: true));
            InvokeApplyAutomation(viewModel);
            viewModel.CommandText = "js ork";

            await viewModel.SendCommandCommand.ExecuteAsync(null);
            Dispatcher.UIThread.RunJobs();

            Assert.Contains("\u001b[32mCel: ork\u001b[0m\n", output);
        }
        finally
        {
            await DisposeAsync(viewModel, directory);
        }
    }

    [AvaloniaFact]
    public async Task Alias_CanRunNamedScriptThroughBuiltInCommand()
    {
        var (viewModel, directory) = CreateViewModel();
        var output = new List<string>();
        viewModel.OutputReceived += output.Add;

        try
        {
            SetConnected(viewModel);
            viewModel.Scripts.Add(new ScriptEntry
            {
                Name = "pomocnik",
                Code = """echo("Skrypt uruchomiony", "cyan");""",
            });
            viewModel.AutomationRules.Add(new AutomationRuleEntry(
                "pomocnik",
                "alias",
                "^pomocnik$",
                "/script pomocnik",
                isEnabled: true));
            InvokeApplyAutomation(viewModel);
            viewModel.CommandText = "pomocnik";

            await viewModel.SendCommandCommand.ExecuteAsync(null);
            Dispatcher.UIThread.RunJobs();

            Assert.Contains("\u001b[36mSkrypt uruchomiony\u001b[0m\n", output);
            Assert.DoesNotContain(output, line => line.Contains("> /script", StringComparison.Ordinal));
        }
        finally
        {
            await DisposeAsync(viewModel, directory);
        }
    }

    [AvaloniaFact]
    public async Task ScriptExecute_IdzIsHandledByClientInsteadOfSentToMud()
    {
        var (viewModel, directory) = CreateViewModel();
        var output = new List<string>();
        viewModel.OutputReceived += output.Add;

        try
        {
            SetConnected(viewModel);
            var script = new ScriptEntry
            {
                Name = "droga",
                Code = """execute("/idz nieznane");""",
            };

            await viewModel.RunScriptCommand.ExecuteAsync(script);
            Dispatcher.UIThread.RunJobs();

            Assert.Contains(
                viewModel.Toasts,
                toast => toast.Text.Contains("Nie znam lokacji", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(output, line => line.Contains("> /idz", StringComparison.Ordinal));
        }
        finally
        {
            await DisposeAsync(viewModel, directory);
        }
    }

    [AvaloniaFact]
    public async Task AdvancedTrigger_ExecutesJavaScriptOffTheUiPath()
    {
        var (viewModel, directory) = CreateViewModel();
        var output = new List<string>();
        viewModel.OutputReceived += output.Add;

        try
        {
            viewModel.AutomationRules.Add(new AutomationRuleEntry(
                "obrażenia",
                "trigger",
                "^cios (?<damage>\\d+)$",
                """
                if (Number(match.groups.damage) > 50) {
                    echo("Duży cios: " + match.groups.damage, "red");
                }
                """,
                isEnabled: true,
                isAdvanced: true));
            InvokeApplyAutomation(viewModel);

            InvokeQueueMatchingTriggers(viewModel, "cios 75");
            await GetAutomationQueueTail(viewModel).WaitAsync(TimeSpan.FromSeconds(2));
            Dispatcher.UIThread.RunJobs();

            Assert.Contains("\u001b[31mDuży cios: 75\u001b[0m\n", output);
        }
        finally
        {
            await DisposeAsync(viewModel, directory);
        }
    }

    [AvaloniaFact]
    public async Task AdvancedTimer_ExecutesJavaScript()
    {
        var (viewModel, directory) = CreateViewModel();
        var echoReceived = new TaskCompletionSource<string>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        viewModel.OutputReceived += text =>
        {
            if (text.Contains("Timer JS", StringComparison.Ordinal))
            {
                echoReceived.TrySetResult(text);
            }
        };

        try
        {
            SetConnected(viewModel);
            var timer = new TimerEntry
            {
                Name = "js",
                Milliseconds = 10,
                CommandsText = """echo("Timer JS", "yellow");""",
                IsEnabled = true,
                IsAdvanced = true,
            };

            InvokeSyncTimer(viewModel, timer);

            Assert.Contains(
                "Timer JS",
                await echoReceived.Task.WaitAsync(TimeSpan.FromSeconds(2)),
                StringComparison.Ordinal);
        }
        finally
        {
            await DisposeAsync(viewModel, directory);
        }
    }

    [AvaloniaFact]
    public async Task ProfileScript_HandlesOnlyMatchingGmcpAndSharesVariables()
    {
        var (viewModel, directory) = CreateViewModel();
        var output = new List<string>();
        viewModel.OutputReceived += output.Add;

        try
        {
            viewModel.Scripts.Add(new ScriptEntry
            {
                Name = "hp",
                GmcpPattern = "Char.Vitals",
                Code =
                    """
                    onGmcp("Char.Vitals", event => {
                        variables.set("hp", event.data.hp);
                        echo("HP=" + variables.get("hp"));
                    });
                    """,
            });

            InvokeQueueGmcpScripts(
                viewModel,
                new GmcpMessage("Room.Info", """{"num":"1"}"""));
            InvokeQueueGmcpScripts(
                viewModel,
                new GmcpMessage("Char.Vitals", """{"hp":42}"""));
            await GetAutomationQueueTail(viewModel).WaitAsync(TimeSpan.FromSeconds(2));
            Dispatcher.UIThread.RunJobs();

            Assert.Single(output, line => line.Contains("HP=42", StringComparison.Ordinal));
            var variable = Assert.Single(viewModel.ScriptVariables);
            Assert.Equal("hp", variable.Name);
            Assert.Equal("42", variable.ValueJson);
        }
        finally
        {
            await DisposeAsync(viewModel, directory);
        }
    }

    [AvaloniaFact]
    public async Task ScriptVariableBurst_QueuesSingleUiRefresh()
    {
        var (viewModel, directory) = CreateViewModel();

        try
        {
            Dispatcher.UIThread.RunJobs();
            var collectionChanges = 0;
            viewModel.ScriptVariables.CollectionChanged += (_, _) => collectionChanges++;
            var variables = GetScriptVariables(viewModel);

            for (var index = 0; index < 100; index++)
            {
                variables.SetJson("counter", index.ToString());
            }

            Dispatcher.UIThread.RunJobs();

            var variable = Assert.Single(viewModel.ScriptVariables);
            Assert.Equal("counter", variable.Name);
            Assert.Equal("99", variable.ValueJson);
            Assert.Equal(2, collectionChanges);
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

    private static ProfileScriptVariableStore GetScriptVariables(MainWindowViewModel viewModel)
    {
        var field = typeof(MainWindowViewModel).GetField(
            "_scriptVariables",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(field);
        return Assert.IsType<ProfileScriptVariableStore>(field!.GetValue(viewModel));
    }

    private static void InvokeSyncTimer(MainWindowViewModel viewModel, TimerEntry timer)
    {
        var method = typeof(MainWindowViewModel).GetMethod(
            "SyncTimer",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(method);
        method!.Invoke(viewModel, [timer]);
    }

    private static void InvokeApplyAutomation(MainWindowViewModel viewModel)
    {
        var method = typeof(MainWindowViewModel).GetMethod(
            "ApplyAutomation",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(method);
        method!.Invoke(viewModel, null);
    }

    private static void InvokeQueueMatchingTriggers(MainWindowViewModel viewModel, string line)
    {
        var method = typeof(MainWindowViewModel).GetMethod(
            "QueueMatchingTriggers",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(method);
        method!.Invoke(viewModel, [line]);
    }

    private static Task GetAutomationQueueTail(MainWindowViewModel viewModel)
    {
        var field = typeof(MainWindowViewModel).GetField(
            "_triggerQueueTail",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(field);
        return Assert.IsAssignableFrom<Task>(field!.GetValue(viewModel));
    }

    private static void InvokeQueueGmcpScripts(MainWindowViewModel viewModel, GmcpMessage message)
    {
        var method = typeof(MainWindowViewModel).GetMethod(
            "QueueGmcpScripts",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(method);
        method!.Invoke(viewModel, [message]);
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

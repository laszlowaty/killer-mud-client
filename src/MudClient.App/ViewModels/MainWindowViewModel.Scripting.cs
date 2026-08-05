using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.RegularExpressions;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using MudClient.App.Models;
using MudClient.App.Services;
using MudClient.Core.Automation;
using MudClient.Core.Gmcp;
using MudClient.Core.Scripting;

namespace MudClient.App.ViewModels;

public sealed partial class MainWindowViewModel
{
    private const int MaximumAutomationDepth = 12;
    private const int MaximumScriptLogEntries = 500;
    private static readonly Regex VariableInterpolationRegex = new(
        @"\$\{(?<name>[A-Za-z_][A-Za-z0-9_.-]{0,127})\}",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex ExplicitAliasCallRegex = new(
        @"^\s*alias\((.*)\)\s*$",
        RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.CultureInvariant);

    private readonly JavaScriptRunner _javaScriptRunner = new();
    private ProfileScriptVariableStore _scriptVariables = null!;
    private IReadOnlyList<AutomationRuleEntry> _activeAliasRules = [];
    private IReadOnlyList<AutomationRuleEntry> _activeTriggerRules = [];
    private CancellationTokenSource? _scriptVariableSaveCts;
    private readonly object _scriptVariableSaveLock = new();
    private int _scriptVariableRefreshScheduled;
    private readonly AsyncLocal<int> _automationExecutionDepth = new();

    private ScriptEntry? _editedScript;
    private bool _isScriptFormExpanded;
    private string _newScriptName = string.Empty;
    private string _newScriptCode = string.Empty;
    private string _newScriptGmcpPattern = string.Empty;
    private bool _newScriptIsGlobal;
    private string _newScriptVariableName = string.Empty;
    private string _newScriptVariableJson = "null";

    public ObservableCollection<ScriptVariableEntry> ScriptVariables { get; } = [];
    public ObservableCollection<ScriptLogEntryViewModel> ScriptLogs { get; } = [];

    public RelayCommand AddScriptCommand { get; private set; } = null!;
    public RelayCommand StartAddScriptCommand { get; private set; } = null!;
    public RelayCommand<ScriptEntry> EditScriptCommand { get; private set; } = null!;
    public RelayCommand<ScriptEntry> ToggleScriptCommand { get; private set; } = null!;
    public RelayCommand<ScriptEntry> DeleteScriptCommand { get; private set; } = null!;
    public AsyncRelayCommand<ScriptEntry> RunScriptCommand { get; private set; } = null!;
    public RelayCommand CancelScriptEditCommand { get; private set; } = null!;
    public RelayCommand AddScriptVariableCommand { get; private set; } = null!;
    public RelayCommand<ScriptVariableEntry> DeleteScriptVariableCommand { get; private set; } = null!;
    public RelayCommand ClearScriptLogsCommand { get; private set; } = null!;

    public bool IsScriptFormExpanded
    {
        get => _isScriptFormExpanded;
        set => SetProperty(ref _isScriptFormExpanded, value);
    }

    public bool IsEditingScript => _editedScript is not null;

    public string ScriptFormHeader => IsEditingScript ? "✎ Edytuj skrypt" : "＋ Nowy skrypt";

    public string ScriptFormButtonText => IsEditingScript ? "Zapisz zmiany" : "Dodaj skrypt";

    public string NewScriptName
    {
        get => _newScriptName;
        set
        {
            if (SetProperty(ref _newScriptName, value))
            {
                AddScriptCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string NewScriptCode
    {
        get => _newScriptCode;
        set
        {
            if (SetProperty(ref _newScriptCode, value))
            {
                AddScriptCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string NewScriptGmcpPattern
    {
        get => _newScriptGmcpPattern;
        set => SetProperty(ref _newScriptGmcpPattern, value);
    }

    public bool NewScriptIsGlobal
    {
        get => _newScriptIsGlobal;
        set => SetProperty(ref _newScriptIsGlobal, value);
    }

    public string NewScriptVariableName
    {
        get => _newScriptVariableName;
        set
        {
            if (SetProperty(ref _newScriptVariableName, value))
            {
                AddScriptVariableCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string NewScriptVariableJson
    {
        get => _newScriptVariableJson;
        set
        {
            if (SetProperty(ref _newScriptVariableJson, value))
            {
                AddScriptVariableCommand.NotifyCanExecuteChanged();
            }
        }
    }

    private void InitializeScripting()
    {
        _scriptVariables = new ProfileScriptVariableStore(OnScriptVariablesChanged);
        AddScriptCommand = new RelayCommand(AddScript, CanAddScript);
        StartAddScriptCommand = new RelayCommand(StartAddScript);
        EditScriptCommand = new RelayCommand<ScriptEntry>(EditScript);
        ToggleScriptCommand = new RelayCommand<ScriptEntry>(ToggleScript);
        DeleteScriptCommand = new RelayCommand<ScriptEntry>(DeleteScript);
        RunScriptCommand = new AsyncRelayCommand<ScriptEntry>(RunScriptAsync);
        CancelScriptEditCommand = new RelayCommand(ClearScriptForm);
        AddScriptVariableCommand = new RelayCommand(AddScriptVariable, CanAddScriptVariable);
        DeleteScriptVariableCommand = new RelayCommand<ScriptVariableEntry>(DeleteScriptVariable);
        ClearScriptLogsCommand = new RelayCommand(ScriptLogs.Clear);
    }

    private bool CanAddScript() =>
        !string.IsNullOrWhiteSpace(NewScriptName)
        && !string.IsNullOrWhiteSpace(NewScriptCode);

    private void StartAddScript()
    {
        ClearScriptForm();
        IsScriptFormExpanded = true;
        SelectedAutomationTabIndex = 3;
    }

    private void AddScript()
    {
        if (!CanAddScript())
        {
            return;
        }

        if (_javaScriptRunner.Validate(NewScriptName.Trim(), NewScriptCode) is { } scriptError)
        {
            AddToast(scriptError, "error");
            return;
        }

        if (_editedScript is { } edited)
        {
            edited.Name = NewScriptName.Trim();
            edited.Code = NewScriptCode;
            edited.GmcpPattern = NewScriptGmcpPattern.Trim();
            edited.IsGlobal = NewScriptIsGlobal;
            edited.LastError = string.Empty;
        }
        else
        {
            Scripts.Add(new ScriptEntry
            {
                Name = NewScriptName.Trim(),
                Code = NewScriptCode,
                GmcpPattern = NewScriptGmcpPattern.Trim(),
                IsGlobal = NewScriptIsGlobal,
            });
        }

        ClearScriptForm();
        RebuildFolderTrees();
        SaveActiveProfile();
    }

    private void EditScript(ScriptEntry? script)
    {
        if (script is null)
        {
            return;
        }

        _editedScript = script;
        NewScriptName = script.Name;
        NewScriptCode = script.Code;
        NewScriptGmcpPattern = script.GmcpPattern;
        NewScriptIsGlobal = script.IsGlobal;
        IsScriptFormExpanded = true;
        SelectedAutomationTabIndex = 3;
        NotifyScriptEditModeChanged();
    }

    private void ToggleScript(ScriptEntry? script)
    {
        if (script is null)
        {
            return;
        }

        script.IsEnabled = !script.IsEnabled;
        RebuildFolderTrees();
        SaveActiveProfile();
    }

    private void DeleteScript(ScriptEntry? script)
    {
        if (script is null)
        {
            return;
        }

        if (ReferenceEquals(script, _editedScript))
        {
            ClearScriptForm();
        }

        Scripts.Remove(script);
        SaveActiveProfile();
    }

    private async Task RunScriptAsync(ScriptEntry? script, CancellationToken cancellationToken)
    {
        if (script is null)
        {
            return;
        }

        await ExecuteScriptAsync(
            new ScriptInvocation(script.Name, "script", script.Code),
            script,
            depth: 0,
            cancellationToken);
    }

    private void ClearScriptForm()
    {
        _editedScript = null;
        IsScriptFormExpanded = false;
        NewScriptName = string.Empty;
        NewScriptCode = string.Empty;
        NewScriptGmcpPattern = string.Empty;
        NewScriptIsGlobal = false;
        NotifyScriptEditModeChanged();
    }

    private void NotifyScriptEditModeChanged()
    {
        OnPropertyChanged(nameof(IsEditingScript));
        OnPropertyChanged(nameof(ScriptFormHeader));
        OnPropertyChanged(nameof(ScriptFormButtonText));
    }

    private bool CanAddScriptVariable() =>
        !string.IsNullOrWhiteSpace(NewScriptVariableName)
        && IsValidJson(NewScriptVariableJson);

    private static bool IsValidJson(string json)
    {
        try
        {
            using var _ = JsonDocument.Parse(json);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private void AddScriptVariable()
    {
        if (!CanAddScriptVariable())
        {
            AddToast("Zmienna wymaga nazwy i prawidłowej wartości JSON.", "error");
            return;
        }

        try
        {
            _scriptVariables.SetJson(NewScriptVariableName.Trim(), NewScriptVariableJson);
            NewScriptVariableName = string.Empty;
            NewScriptVariableJson = "null";
        }
        catch (Exception exception)
        {
            AddToast($"Nie udało się zapisać zmiennej: {exception.Message}", "error");
        }
    }

    private void DeleteScriptVariable(ScriptVariableEntry? variable)
    {
        if (variable is not null)
        {
            _scriptVariables.Remove(variable.Name);
        }
    }

    private void OnScriptVariablesChanged()
    {
        ScheduleScriptVariableRefresh();

        CancellationTokenSource cancellation;
        lock (_scriptVariableSaveLock)
        {
            _scriptVariableSaveCts?.Cancel();
            cancellation = new CancellationTokenSource();
            _scriptVariableSaveCts = cancellation;
        }

        _ = SaveScriptVariablesAfterDelayAsync(cancellation);
    }

    private void ScheduleScriptVariableRefresh()
    {
        if (Interlocked.Exchange(ref _scriptVariableRefreshScheduled, 1) != 0)
        {
            return;
        }

        Dispatcher.UIThread.Post(
            () =>
            {
                Interlocked.Exchange(ref _scriptVariableRefreshScheduled, 0);
                RefreshScriptVariableEntries();
            },
            DispatcherPriority.Background);
    }

    private async Task SaveScriptVariablesAfterDelayAsync(CancellationTokenSource cancellation)
    {
        try
        {
            await Task.Delay(TimeSpan.FromMilliseconds(300), cancellation.Token);
            await Dispatcher.UIThread.InvokeAsync(SaveActiveProfile);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            // A newer variable change superseded this delayed save.
        }
        finally
        {
            lock (_scriptVariableSaveLock)
            {
                if (ReferenceEquals(_scriptVariableSaveCts, cancellation))
                {
                    _scriptVariableSaveCts = null;
                }
            }

            cancellation.Dispose();
        }
    }

    private void RefreshScriptVariableEntries()
    {
        ScriptVariables.Clear();
        foreach (var pair in _scriptVariables.Snapshot().OrderBy(pair => pair.Key))
        {
            ScriptVariables.Add(new ScriptVariableEntry
            {
                Name = pair.Key,
                ValueJson = pair.Value.GetRawText(),
            });
        }
    }

    private void RefreshScriptingAutomation()
    {
        _activeAliasRules = AutomationRules
            .Where(rule => rule.IsEnabled && rule.Type == "alias")
            .ToArray();
        _activeTriggerRules = AutomationRules
            .Where(rule => rule.IsEnabled && rule.Type == "trigger")
            .ToArray();
    }

    private void QueueMatchingTriggers(string line)
    {
        var matches = new List<(AutomationRuleEntry Rule, ScriptMatchContext Match, string? Commands)>();
        foreach (var rule in _activeTriggerRules)
        {
            try
            {
                var regex = new Regex(rule.Pattern);
                var match = regex.Match(line);
                if (!match.Success)
                {
                    continue;
                }

                matches.Add((
                    rule,
                    CreateMatchContext(regex, match),
                    rule.IsAdvanced ? null : InterpolateVariables(match.Result(rule.Action))));
            }
            catch (ArgumentException)
            {
                // Invalid persisted regexes are already reported by ApplyAutomation.
            }
        }

        if (matches.Count == 0)
        {
            // Keep the standalone Core trigger engine usable by focused tests
            // and integrations that populate it directly. Profile-backed rules
            // are handled above so they still share the scripting queue.
            var coreCommands = _triggers.Evaluate(line);
            if (coreCommands.Count > 0)
            {
                QueueTriggeredCommands(coreCommands);
            }

            return;
        }

        QueueAutomationWork(async cancellationToken =>
        {
            foreach (var item in matches)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (item.Rule.IsAdvanced)
                {
                    await ExecuteScriptAsync(
                        new ScriptInvocation(
                            item.Rule.Name,
                            "trigger",
                            item.Rule.Action,
                            Input: line,
                            Match: item.Match),
                        owner: item.Rule,
                        depth: 0,
                        cancellationToken);
                }
                else
                {
                    await ExecuteClientCommandTextAsync(
                        item.Commands!,
                        expandAliases: true,
                        depth: 0,
                        cancellationToken);
                }
            }
        });
    }

    private void QueueGmcpScripts(GmcpMessage message)
    {
        var scripts = Scripts.Where(script =>
                script.IsEnabled
                && JavaScriptRunner.MatchesGmcpPackage(script.GmcpPattern, message.Package))
            .ToArray();
        if (scripts.Length == 0)
        {
            return;
        }

        QueueAutomationWork(async cancellationToken =>
        {
            foreach (var script in scripts)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await ExecuteScriptAsync(
                    new ScriptInvocation(
                        script.Name,
                        "script",
                        script.Code,
                        Gmcp: new ScriptGmcpContext(message.Package, message.Json)),
                    script,
                    depth: 0,
                    cancellationToken);
            }
        });
    }

    private Task QueueAutomationWork(Func<CancellationToken, Task> work)
    {
        Task task;
        lock (_triggerTasksLock)
        {
            if (!_acceptingTriggerTasks)
            {
                return Task.CompletedTask;
            }

            var previous = _triggerQueueTail;
            task = EnqueueAutomationWorkAsync(previous, work);
            _triggerQueueTail = task;
            _triggerTasks.Add(task);
        }

        _ = RemoveWhenCompleted(task);
        return task;
    }

    private async Task EnqueueAutomationWorkAsync(
        Task previous,
        Func<CancellationToken, Task> work)
    {
        await Task.Yield();
        try
        {
            await previous.ConfigureAwait(false);
        }
        catch
        {
            // A failed earlier automation must not stall the FIFO chain.
        }

        await _triggerSendLock.WaitAsync(_triggerCts.Token);
        try
        {
            _automationExecutionDepth.Value++;
            try
            {
                await work(_triggerCts.Token);
            }
            finally
            {
                _automationExecutionDepth.Value--;
            }
        }
        finally
        {
            _triggerSendLock.Release();
        }
    }

    private async Task ExecuteScriptAsync(
        ScriptInvocation invocation,
        IScriptErrorSource? owner,
        int depth,
        CancellationToken cancellationToken)
    {
        if (depth > MaximumAutomationDepth)
        {
            ReportScriptError(
                owner,
                "Przekroczono limit zagnieżdżenia automatyzacji.",
                invocation.Name);
            return;
        }

        var result = await Task.Run(
            () => _javaScriptRunner.Execute(invocation, _scriptVariables, cancellationToken),
            cancellationToken);

        if (!result.Success)
        {
            ReportScriptError(owner, result.Error!, invocation.Name);
            return;
        }

        if (owner is not null && owner.HasLastError)
        {
            Dispatcher.UIThread.Post(() => owner.LastError = string.Empty);
        }

        foreach (var effect in result.Effects)
        {
            cancellationToken.ThrowIfCancellationRequested();
            switch (effect.Kind)
            {
                case ScriptEffectKind.Execute:
                    await ExecuteClientCommandTextAsync(
                        effect.Text,
                        expandAliases: true,
                        depth + 1,
                        cancellationToken);
                    break;
                case ScriptEffectKind.Send:
                    foreach (var command in CommandStacker.Split(effect.Text, CommandStackingSeparator))
                    {
                        await SendMudCommandRawAsync(command, cancellationToken);
                    }
                    break;
                case ScriptEffectKind.Echo:
                    Dispatcher.UIThread.Post(() =>
                        EmitEcho(effect.Color ?? "cyan", effect.Text));
                    break;
                case ScriptEffectKind.Log:
                    AddScriptLog(
                        invocation.Name,
                        effect.Color ?? "info",
                        effect.Text);
                    break;
            }
        }
    }

    private void ReportScriptError(
        IScriptErrorSource? owner,
        string error,
        string source)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (owner is not null)
            {
                owner.LastError = error;
            }

            EmitSystem(error, 31);
            AppendScriptLog(source, "error", error);
        });
    }

    private void AddScriptLog(string source, string level, string message)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            AppendScriptLog(source, level, message);
        }
        else
        {
            Dispatcher.UIThread.Post(() => AppendScriptLog(source, level, message));
        }
    }

    private void AppendScriptLog(string source, string level, string message)
    {
        ScriptLogs.Add(new ScriptLogEntryViewModel(
            DateTimeOffset.Now.ToString("HH:mm:ss"),
            source,
            level switch
            {
                "warning" => "WARN",
                "error" => "BŁĄD",
                _ => "INFO",
            },
            message));

        while (ScriptLogs.Count > MaximumScriptLogEntries)
        {
            ScriptLogs.RemoveAt(0);
        }
    }

    private async Task ExecuteClientCommandTextAsync(
        string text,
        bool expandAliases,
        int depth,
        CancellationToken cancellationToken)
    {
        if (depth > MaximumAutomationDepth)
        {
            Dispatcher.UIThread.Post(() =>
                EmitSystem("Przekroczono limit zagnieżdżenia automatyzacji.", 31));
            return;
        }

        var commands = CommandStacker.Split(text, CommandStackingSeparator);
        foreach (var command in commands)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await ExecuteClientCommandSegmentAsync(
                command,
                expandAliases,
                depth,
                cancellationToken);
        }
    }

    private async Task ExecuteClientCommandSegmentAsync(
        string command,
        bool expandAliases,
        int depth,
        CancellationToken cancellationToken)
    {
        var explicitAlias = ExplicitAliasCallRegex.Match(command);
        if (explicitAlias.Success)
        {
            await ExecuteClientCommandSegmentAsync(
                explicitAlias.Groups[1].Value,
                expandAliases: true,
                depth + 1,
                cancellationToken);
            return;
        }

        // Every built-in client command currently has a slash prefix, with
        // +map retained as a mapper compatibility spelling. Avoid a UI-thread
        // round trip for ordinary MUD commands.
        var couldBeBuiltIn = command.Length > 0 && command[0] == '/'
            || command.StartsWith("+map", StringComparison.OrdinalIgnoreCase);
        if (couldBeBuiltIn
            && await TryHandleBuiltInCommandOnUiThreadAsync(command, depth, cancellationToken))
        {
            return;
        }

        if (TryHandleEchoCommandOnUiThread(command))
        {
            return;
        }

        if (expandAliases)
        {
            foreach (var rule in _activeAliasRules)
            {
                Match match;
                Regex regex;
                try
                {
                    regex = new Regex(rule.Pattern);
                    match = regex.Match(command);
                }
                catch (ArgumentException)
                {
                    continue;
                }

                if (!match.Success)
                {
                    continue;
                }

                if (rule.IsAdvanced)
                {
                    await ExecuteScriptAsync(
                        new ScriptInvocation(
                            rule.Name,
                            "alias",
                            rule.Action,
                            Input: command,
                            Match: CreateMatchContext(regex, match)),
                        owner: rule,
                        depth + 1,
                        cancellationToken);
                }
                else
                {
                    var replacement = InterpolateVariables(match.Result(rule.Action));
                    await ExecuteClientCommandTextAsync(
                        replacement,
                        expandAliases: false,
                        depth + 1,
                        cancellationToken);
                }

                return;
            }

            // Keep the standalone Core alias engine usable by focused tests
            // and any integrations that populate it directly.
            var coreExpansion = _aliases.ProcessCommands(command, CommandStackingSeparator);
            if (coreExpansion.Count != 1
                || !string.Equals(coreExpansion[0], command, StringComparison.Ordinal))
            {
                foreach (var expanded in coreExpansion)
                {
                    await ExecuteClientCommandSegmentAsync(
                        expanded,
                        expandAliases: false,
                        depth + 1,
                        cancellationToken);
                }

                return;
            }
        }

        await SendMudCommandRawAsync(command, cancellationToken);
    }

    private async Task<bool> TryHandleBuiltInCommandOnUiThreadAsync(
        string command,
        int depth,
        CancellationToken cancellationToken)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            return await TryHandleBuiltInCommandAsync(command, depth, cancellationToken);
        }

        var completion = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        Dispatcher.UIThread.Post(async () =>
        {
            try
            {
                completion.TrySetResult(
                    await TryHandleBuiltInCommandAsync(command, depth, cancellationToken));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                completion.TrySetCanceled(cancellationToken);
            }
            catch (Exception exception)
            {
                completion.TrySetException(exception);
            }
        });

        return await completion.Task;
    }

    private async Task<bool> TryHandleBuiltInCommandAsync(
        string command,
        int depth,
        CancellationToken cancellationToken)
    {
        if (TryHandleCharacterRollerCommand(command))
        {
            return true;
        }

        if (await TryHandleMapEditorCommandAsync(command))
        {
            return true;
        }

        if (TryHandleAutowalkCommand(command))
        {
            return true;
        }

        if (string.Equals(command, "/recast", StringComparison.OrdinalIgnoreCase))
        {
            await RecastMissingBuffsAsync();
            return true;
        }

        const string scriptPrefix = "/script ";
        if (command.StartsWith(scriptPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var name = command[scriptPrefix.Length..].Trim();
            var script = Scripts.FirstOrDefault(item =>
                item.IsEnabled
                && string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase));
            if (script is null)
            {
                EmitSystem($"Nie znaleziono aktywnego skryptu „{name}”.", 31);
            }
            else
            {
                await ExecuteScriptAsync(
                    new ScriptInvocation(script.Name, "command", script.Code, Input: command),
                    script,
                    depth + 1,
                    cancellationToken);
            }

            return true;
        }

        return false;
    }

    private bool TryHandleEchoCommandOnUiThread(string command)
    {
        if (EchoCommandParser.Parse(command, out _) == EchoCommandParseStatus.NotEcho)
        {
            return false;
        }

        if (Dispatcher.UIThread.CheckAccess())
        {
            TryHandleEchoCommand(command);
        }
        else
        {
            Dispatcher.UIThread.Post(() => TryHandleEchoCommand(command));
        }

        return true;
    }

    private async Task SendMudCommandRawAsync(
        string command,
        CancellationToken cancellationToken)
    {
        if (Map.IsMapEditorActive)
        {
            return;
        }

        var mapperDecision = Map.PrepareMapEditorCommand(command);
        if (!mapperDecision.Allow)
        {
            Dispatcher.UIThread.Post(() =>
                EmitSystem($"Mapper: {mapperDecision.Message}", 33));
            return;
        }

        if (_automationExecutionDepth.Value > 0)
        {
            Dispatcher.UIThread.Post(() => EmitCommandEcho(command));
        }
        else
        {
            EmitCommandEcho(command);
        }
        try
        {
            await _session.SendCommandAsync(command, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (Map.IsMapEditorAwaitingRoomInfo)
                {
                    Map.CancelPendingMapMovement(
                        $"Nie udało się wysłać ruchu mappera: {exception.Message}");
                }

                EmitSystem(exception.Message, 31);
            });
        }
    }

    private string InterpolateVariables(string text) =>
        VariableInterpolationRegex.Replace(text, match =>
        {
            var json = _scriptVariables.GetJson(match.Groups["name"].Value);
            if (json is null)
            {
                return string.Empty;
            }

            using var document = JsonDocument.Parse(json);
            return document.RootElement.ValueKind == JsonValueKind.String
                ? document.RootElement.GetString() ?? string.Empty
                : document.RootElement.GetRawText();
        });

    private static ScriptMatchContext CreateMatchContext(Regex regex, Match match)
    {
        var groups = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var groupName in regex.GetGroupNames())
        {
            if (!int.TryParse(groupName, out _))
            {
                groups[groupName] = match.Groups[groupName].Value;
            }
        }

        return new ScriptMatchContext(
            match.Value,
            match.Groups.Cast<Group>().Select(group => group.Value).ToArray(),
            groups);
    }

    private void StopScriptingPersistence()
    {
        lock (_scriptVariableSaveLock)
        {
            _scriptVariableSaveCts?.Cancel();
            _scriptVariableSaveCts = null;
        }
    }

    private async Task ResetAutomationQueueAsync()
    {
        List<Task> pending;
        CancellationTokenSource previousCancellation;
        lock (_triggerTasksLock)
        {
            _acceptingTriggerTasks = false;
            pending = [.. _triggerTasks];
            previousCancellation = _triggerCts;
        }

        previousCancellation.Cancel();
        foreach (var task in pending)
        {
            try
            {
                await task;
            }
            catch (OperationCanceledException)
            {
                // Expected when disconnecting or changing the active session.
            }
            catch (Exception exception)
            {
                Dispatcher.UIThread.Post(() => EmitSystem(exception.Message, 31));
            }
        }

        lock (_triggerTasksLock)
        {
            _triggerTasks.Clear();
            _triggerQueueTail = Task.CompletedTask;
            _triggerCts = new CancellationTokenSource();
            _acceptingTriggerTasks = true;
        }

        previousCancellation.Dispose();
    }

    private void CancelAutomationQueue()
    {
        lock (_triggerTasksLock)
        {
            _acceptingTriggerTasks = false;
            _triggerCts.Cancel();
        }
    }
}

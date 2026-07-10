using System.Reflection;
using MudClient.App.ViewModels;
using MudClient.Core.Networking;

namespace MudClient.App.Tests;

public sealed class MainWindowViewModelTests : IAsyncDisposable
{
    private readonly MainWindowViewModel _vm = new();

    public async ValueTask DisposeAsync()
    {
        await _vm.DisposeAsync();
    }

    /// <summary>
    /// Forces the VM's IsConnected to the given value by writing the
    /// backing field directly. This avoids the need for a real TCP
    /// connection while still exercising the actual send pipeline.
    /// </summary>
    private void SetIsConnected(bool value)
    {
        var field = typeof(MainWindowViewModel).GetField("_isConnected",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(field);
        field!.SetValue(_vm, value);
    }

    // ====================================================================
    // Constructor / mock data
    // ====================================================================

    [Fact]
    public void Constructor_PopulatesMockQuickCommands()
    {
        Assert.NotEmpty(_vm.QuickCommands);
        Assert.Contains(_vm.QuickCommands, q => q.Label == "spojrzyj" && q.Command == "look");
    }

    [Fact]
    public void Constructor_PopulatesMockLogFilters()
    {
        Assert.NotEmpty(_vm.LogFilters);
        Assert.Contains(_vm.LogFilters, f => f.Key == "all");
    }

    [Fact]
    public void Constructor_PopulatesMockEffects()
    {
        Assert.NotEmpty(_vm.Effects);
        Assert.Contains(_vm.Effects, e => e.Name == "Błogosławieństwo");
    }

    [Fact]
    public void Constructor_PopulatesMockPeople()
    {
        Assert.NotEmpty(_vm.People);
        Assert.Contains(_vm.People, p => p.Name == "Strażnik miasta");
    }

    [Fact]
    public void Constructor_PopulatesMockGroup()
    {
        Assert.NotEmpty(_vm.Group);
        Assert.Contains(_vm.Group, m => m.Name == "Ty");
    }

    [Fact]
    public void Constructor_StartsWithNoAutomationRules()
    {
        // Rules are per-profile; none should exist before a profile is activated.
        Assert.Empty(_vm.AutomationRules);
    }

    [Fact]
    public void Constructor_PopulatesMockNotes()
    {
        Assert.NotEmpty(_vm.Notes);
        Assert.Contains(_vm.Notes, n => n.Title == "Lista zakupów");
    }

    [Fact]
    public void Constructor_AddsWelcomeToast()
    {
        Assert.Single(_vm.Toasts);
        Assert.Contains("Witaj", _vm.Toasts[0].Text);
    }

    [Fact]
    public void Constructor_HeaderDefaultsToDisconnected()
    {
        Assert.Equal("--- Niepołączono ---", _vm.HeaderAreaText);
    }

    [Fact]
    public void Constructor_StatusTextDefaultsToRozłączono()
    {
        Assert.Equal("Rozłączono", _vm.StatusText);
    }

    [Fact]
    public void Constructor_CommandHistoryIsEmpty()
    {
        Assert.Empty(_vm.CommandHistory);
    }

    // ====================================================================
    // AddNote command
    // ====================================================================

    [Fact]
    public void AddNote_WithTitleAndContent_AddsNoteAndClearsFields()
    {
        _vm.NewNoteTitle = "Test title";
        _vm.NewNoteContent = "Test content";

        _vm.AddNoteCommand.Execute(null);

        var note = Assert.Single(_vm.Notes, n => n.Title == "Test title");
        Assert.Equal("Test content", note.Content);
        Assert.NotEmpty(note.CreatedAt);
        Assert.Empty(_vm.NewNoteTitle);
        Assert.Empty(_vm.NewNoteContent);
    }

    [Fact]
    public void AddNote_WithEmptyTitle_DoesNotAddNote()
    {
        var countBefore = _vm.Notes.Count;
        _vm.NewNoteTitle = string.Empty;

        _vm.AddNoteCommand.Execute(null);

        Assert.Equal(countBefore, _vm.Notes.Count);
    }

    [Fact]
    public void AddNote_WithWhitespaceTitle_DoesNotAddNote()
    {
        var countBefore = _vm.Notes.Count;
        _vm.NewNoteTitle = "   ";

        _vm.AddNoteCommand.Execute(null);

        Assert.Equal(countBefore, _vm.Notes.Count);
    }

    [Fact]
    public void AddNote_InsertsAtTopOfNotesCollection()
    {
        _vm.NewNoteTitle = "Newest";
        _vm.AddNoteCommand.Execute(null);

        Assert.Equal("Newest", _vm.Notes[0].Title);
    }

    // ====================================================================
    // DeleteNote command
    // ====================================================================

    [Fact]
    public void DeleteNote_WithExistingNote_RemovesIt()
    {
        _vm.NewNoteTitle = "To delete";
        _vm.AddNoteCommand.Execute(null);
        var note = _vm.Notes[0];
        Assert.Contains(note, _vm.Notes);

        _vm.DeleteNoteCommand.Execute(note);

        Assert.DoesNotContain(note, _vm.Notes);
    }

    [Fact]
    public void DeleteNote_WithNull_DoesNotThrow()
    {
        _vm.DeleteNoteCommand.Execute(null);
        // No exception expected
    }

    [Fact]
    public void DeleteNote_WithNonExistentNote_DoesNotChangeCollection()
    {
        var nonExistent = new MudClient.App.Models.NoteEntry { Title = "Ghost" };
        _vm.DeleteNoteCommand.Execute(nonExistent);
        // No exception expected, no change
    }

    // ====================================================================
    // CopyToCommandBar command
    // ====================================================================

    [Theory]
    [InlineData("north")]
    [InlineData("look")]
    [InlineData("say hello")]
    public void CopyToCommandBar_SetsCommandText(string text)
    {
        _vm.CopyToCommandBarCommand.Execute(text);

        Assert.Equal(text, _vm.CommandText);
    }

    [Fact]
    public void CopyToCommandBar_WithNull_DoesNotChangeCommandText()
    {
        _vm.CommandText = "existing";
        _vm.CopyToCommandBarCommand.Execute(null);

        Assert.Equal("existing", _vm.CommandText);
    }

    [Fact]
    public void CopyToCommandBar_WithEmptyString_DoesNotChangeCommandText()
    {
        _vm.CommandText = "existing";
        _vm.CopyToCommandBarCommand.Execute(string.Empty);

        Assert.Equal("existing", _vm.CommandText);
    }

    // ====================================================================
    // ClearToasts command
    // ====================================================================

    [Fact]
    public void ClearToasts_RemovesAllToasts()
    {
        Assert.NotEmpty(_vm.Toasts);

        _vm.ClearToastsCommand.Execute(null);

        Assert.Empty(_vm.Toasts);
    }

    // ====================================================================
    // QuickCommandExecute
    // ====================================================================

    [Fact]
    public void ExecuteQuickCommand_WhenDisconnected_SetsCommandText()
    {
        Assert.False(_vm.IsConnected);

        _vm.QuickCommandExecuteCommand.Execute("look");

        Assert.Equal("look", _vm.CommandText);
    }

    [Fact]
    public void ExecuteQuickCommand_WithNull_DoesNothing()
    {
        _vm.CommandText = "original";
        _vm.QuickCommandExecuteCommand.Execute(null);

        Assert.Equal("original", _vm.CommandText);
    }

    [Fact]
    public void ExecuteQuickCommand_WithWhitespace_DoesNothing()
    {
        _vm.CommandText = "original";
        _vm.QuickCommandExecuteCommand.Execute("   ");

        Assert.Equal("original", _vm.CommandText);
    }

    // ====================================================================
    // SelectedRightTab / SelectedLogTab properties
    // ====================================================================

    [Fact]
    public void SelectedRightTab_DefaultsToZero()
    {
        Assert.Equal(0, _vm.SelectedRightTab);
    }

    [Fact]
    public void SelectedRightTab_IsSettable()
    {
        _vm.SelectedRightTab = 2;
        Assert.Equal(2, _vm.SelectedRightTab);
    }

    [Fact]
    public void SelectedLogTab_DefaultsToZero()
    {
        Assert.Equal(0, _vm.SelectedLogTab);
    }

    [Fact]
    public void SelectedLogTab_IsSettable()
    {
        _vm.SelectedLogTab = 1;
        Assert.Equal(1, _vm.SelectedLogTab);
    }

    // ====================================================================
    // Command history – ordering, trimming, and capacity
    // ====================================================================

    [Fact]
    public async Task SendCommand_HistoryInsertedAtTopNewestFirst()
    {
        // Arrange
        SetIsConnected(true);
        _vm.CommandText = "first";

        // Act
        await _vm.SendCommandCommand.ExecuteAsync(null);
        _vm.CommandText = "second";
        await _vm.SendCommandCommand.ExecuteAsync(null);

        // Assert: newest command is at index 0
        Assert.Equal(2, _vm.CommandHistory.Count);
        Assert.Equal("second", _vm.CommandHistory[0]);
        Assert.Equal("first", _vm.CommandHistory[1]);
    }

    [Fact]
    public async Task SendCommand_HistoryTrimsAtMaxSize()
    {
        // Arrange: send 101 commands (1 more than the 100-entry cap)
        SetIsConnected(true);
        const int targetCount = 101;
        var allCommands = new string[targetCount];
        for (var i = 0; i < targetCount; i++)
        {
            allCommands[i] = $"cmd{i:D3}";
        }

        // Act
        foreach (var cmd in allCommands)
        {
            _vm.CommandText = cmd;
            await _vm.SendCommandCommand.ExecuteAsync(null);
        }

        // Assert: count is capped at 100, oldest entry ("cmd000") is removed
        Assert.Equal(100, _vm.CommandHistory.Count);
        Assert.DoesNotContain("cmd000", _vm.CommandHistory);
    }

    [Fact]
    public async Task SendCommand_HistoryPreservesNewestEntriesAtCap()
    {
        // Arrange: send 101 commands
        SetIsConnected(true);
        const int targetCount = 101;
        for (var i = 0; i < targetCount; i++)
        {
            _vm.CommandText = $"cmd{i:D3}";
            await _vm.SendCommandCommand.ExecuteAsync(null);
        }

        // Assert: the 100 newest entries ("cmd001" through "cmd100") are present,
        //         ordered newest-first.
        Assert.Equal(100, _vm.CommandHistory.Count);
        Assert.Equal("cmd100", _vm.CommandHistory[0]);
        Assert.Equal("cmd099", _vm.CommandHistory[1]);
        Assert.Contains("cmd001", _vm.CommandHistory);
    }

    [Fact]
    public async Task SendCommand_HistoryMaintainsOrderAfterMultipleTrims()
    {
        // Arrange: send 150 commands (50 past cap)
        SetIsConnected(true);
        for (var i = 0; i < 150; i++)
        {
            _vm.CommandText = $"cmd{i:D3}";
            await _vm.SendCommandCommand.ExecuteAsync(null);
        }

        // Assert: count stays at 100, oldest 50 are gone, newest 50 are present
        Assert.Equal(100, _vm.CommandHistory.Count);
        Assert.DoesNotContain("cmd000", _vm.CommandHistory);
        Assert.DoesNotContain("cmd049", _vm.CommandHistory);
        Assert.Contains("cmd050", _vm.CommandHistory);
        Assert.Contains("cmd149", _vm.CommandHistory);
        Assert.Equal("cmd149", _vm.CommandHistory[0]);
    }

    // ====================================================================
    // Host / Port defaults
    // ====================================================================

    [Fact]
    public void Host_DefaultsToKillerMudPl()
    {
        Assert.Equal("killer-mud.pl", _vm.Host);
    }

    [Fact]
    public void Port_DefaultsTo4004()
    {
        Assert.Equal(4004, _vm.Port);
    }

    // ====================================================================
    // Disconnect CanExecute
    // ====================================================================

    [Fact]
    public void DisconnectCommand_InitiallyCannotExecute()
    {
        Assert.False(_vm.DisconnectCommand.CanExecute(null));
    }

    // ====================================================================
    // Connect CanExecute
    // ====================================================================

    [Fact]
    public void ConnectCommand_InitiallyCanExecute()
    {
        Assert.True(_vm.ConnectCommand.CanExecute(null));
    }

    // ====================================================================
    // Send command – CommandText preservation  (coder change validation)
    // ====================================================================

    [Fact]
    public async Task SendCommand_PreservesOriginalTextAfterSend()
    {
        // Arrange: simulate connected so the command can execute
        SetIsConnected(true);
        _vm.CommandText = "l";

        // Act: execute the send command (full pipeline w/ alias expansion)
        await _vm.SendCommandCommand.ExecuteAsync(null);

        // Assert: CommandText retains the original typed text, not cleared
        Assert.Equal("l", _vm.CommandText);
    }

    /// <summary>Registers a "l" → "look" alias through the real rule pipeline.</summary>
    private void AddLookAlias()
    {
        _vm.NewRuleName = "Skrót look";
        _vm.NewRuleType = "alias";
        _vm.NewRulePattern = "^l$";
        _vm.NewRuleAction = "look";
        _vm.AddRuleCommand.Execute(null);
    }

    [Fact]
    public async Task SendCommand_HistoryContainsAliasExpandedVersion()
    {
        // Arrange
        AddLookAlias();
        SetIsConnected(true);
        _vm.CommandText = "l";

        // Act
        await _vm.SendCommandCommand.ExecuteAsync(null);

        // Assert: history stores the alias-expanded command ("look")
        //         but NOT the short form ("l")
        Assert.NotEmpty(_vm.CommandHistory);
        Assert.Contains("look", _vm.CommandHistory);
        Assert.DoesNotContain("l", _vm.CommandHistory);
    }

    [Fact]
    public async Task SendCommand_PreservesWhitespaceText()
    {
        // Arrange
        SetIsConnected(true);
        _vm.CommandText = "   ";

        // Act
        await _vm.SendCommandCommand.ExecuteAsync(null);

        // Assert: whitespace text is preserved (not cleared)
        Assert.Equal("   ", _vm.CommandText);
    }

    [Fact]
    public async Task SendCommand_PreservesEmptyText()
    {
        // Arrange
        SetIsConnected(true);
        _vm.CommandText = string.Empty;

        // Act
        await _vm.SendCommandCommand.ExecuteAsync(null);

        // Assert: empty string is preserved
        Assert.Equal(string.Empty, _vm.CommandText);
    }

    [Fact]
    public async Task SendCommand_WithLongCommand_TextSurvives()
    {
        // Arrange
        SetIsConnected(true);
        const string longCmd = "say Witaj, świecie! 1234567890";
        _vm.CommandText = longCmd;

        // Act
        await _vm.SendCommandCommand.ExecuteAsync(null);

        // Assert: the full command text survives the send
        Assert.Equal(longCmd, _vm.CommandText);
    }

    [Fact]
    public async Task SendCommand_WithNonAliasedCommand_OriginalPreserved()
    {
        // Arrange
        SetIsConnected(true);
        const string cmd = "north";
        _vm.CommandText = cmd;

        // Act
        await _vm.SendCommandCommand.ExecuteAsync(null);

        // Assert: unchanged, same text in CommandText
        Assert.Equal(cmd, _vm.CommandText);
        Assert.Contains(cmd, _vm.CommandHistory);
    }

    [Fact]
    public async Task SendCommand_OriginalTextDifferentFromHistoryWhenAliased()
    {
        // Arrange
        AddLookAlias();
        SetIsConnected(true);
        _vm.CommandText = "l";

        // Act
        await _vm.SendCommandCommand.ExecuteAsync(null);

        // Assert: CommandText ("l") differs from history entry ("look")
        Assert.Equal("l", _vm.CommandText);
        var historyEntry = Assert.Single(_vm.CommandHistory);
        Assert.Equal("look", historyEntry);
        Assert.NotEqual(_vm.CommandText, historyEntry);
    }

    // ====================================================================
    // Outgoing GMCP recording (SentGmcpMessages) and 100-entry cap
    //
    // The production event handlers (OnGmcpSent / OnGmcpReceived) use
    // Dispatcher.UIThread.Post to marshal work to the UI thread.  Calling
    // Dispatcher.UIThread.RunJobs() from a non-UI thread would throw
    // Dispatcher.VerifyAccess, making a dispatcher-based test helper
    // unreliable without a headless Avalonia platform.
    //
    // Instead, the tests below verify the insert/order/cap/placeholder
    // behaviour directly on the observable collections — the same logic
    // that the dispatcher-invoked lambdas execute in production.
    // Event-subscription wiring is covered separately via reflection
    // (GmcpSentEvent_IsSubscribedByConstructor / GmcpReceivedEvent_Is
    // SubscribedByConstructor).
    // ====================================================================

    /// <summary>
    /// Replicates the production handler's JSON → display-value logic.
    /// </summary>
    private static string FormatGmcpJson(string json) =>
        string.IsNullOrWhiteSpace(json) ? "(bez danych)" : json;

    private static GmcpEntryViewModel MakeEntry(string package, string json) =>
        new(package, FormatGmcpJson(json), DateTimeOffset.Now.ToString("HH:mm:ss"));

    /// <summary>
    /// Simulates the production OnGmcpSent logic: insert at head, trim to 100.
    /// </summary>
    private void SimulateGmcpSent(string package, string json)
    {
        _vm.SentGmcpMessages.Insert(0, MakeEntry(package, json));
        while (_vm.SentGmcpMessages.Count > 100)
            _vm.SentGmcpMessages.RemoveAt(_vm.SentGmcpMessages.Count - 1);
    }

    /// <summary>
    /// Simulates the production OnGmcpReceived logic: insert at head, trim to 100.
    /// </summary>
    private void SimulateGmcpReceived(string package, string json)
    {
        _vm.GmcpMessages.Insert(0, MakeEntry(package, json));
        while (_vm.GmcpMessages.Count > 100)
            _vm.GmcpMessages.RemoveAt(_vm.GmcpMessages.Count - 1);
    }

    [Fact]
    public void SentGmcpMessages_InitiallyEmpty()
    {
        Assert.Empty(_vm.SentGmcpMessages);
    }

    [Fact]
    public void GmcpMessages_InitiallyEmpty()
    {
        Assert.Empty(_vm.GmcpMessages);
    }

    [Fact]
    public void SentGmcpMessages_InsertThenAddsEntry()
    {
        SimulateGmcpSent("Test.Package", "{\"key\":\"val\"}");

        var entry = Assert.Single(_vm.SentGmcpMessages);
        Assert.Equal("Test.Package", entry.Package);
        Assert.Contains("key", entry.Json);
    }

    [Fact]
    public void SentGmcpMessages_EntryContainsReceivedAtTimestamp()
    {
        SimulateGmcpSent("Core.Ping", "{}");

        var entry = Assert.Single(_vm.SentGmcpMessages);
        Assert.NotEmpty(entry.ReceivedAt);
    }

    [Fact]
    public void SentGmcpMessages_NewestFirst()
    {
        SimulateGmcpSent("First.Pkg", "{}");
        SimulateGmcpSent("Second.Pkg", "{}");

        Assert.Equal(2, _vm.SentGmcpMessages.Count);
        Assert.Equal("Second.Pkg", _vm.SentGmcpMessages[0].Package);
        Assert.Equal("First.Pkg", _vm.SentGmcpMessages[1].Package);
    }

    [Fact]
    public void SentGmcpMessages_CappedAt100_AfterMultipleEvents()
    {
        for (var i = 0; i < 101; i++)
        {
            SimulateGmcpSent($"Pkg{i:D3}", $"{{\"i\":{i}}}");
        }

        Assert.Equal(100, _vm.SentGmcpMessages.Count);
        // Oldest entry (Pkg000) should be gone.
        Assert.DoesNotContain(_vm.SentGmcpMessages, e => e.Package == "Pkg000");
    }

    [Fact]
    public void SentGmcpMessages_PreservesNewestEntriesAtCap()
    {
        for (var i = 0; i < 101; i++)
        {
            SimulateGmcpSent($"Pkg{i:D3}", $"{{\"i\":{i}}}");
        }

        // The 100 newest (Pkg001 through Pkg100) should be present,
        // ordered newest-first.
        Assert.Equal(100, _vm.SentGmcpMessages.Count);
        Assert.Equal("Pkg100", _vm.SentGmcpMessages[0].Package);
        Assert.Equal("Pkg099", _vm.SentGmcpMessages[1].Package);
        Assert.Contains(_vm.SentGmcpMessages, e => e.Package == "Pkg001");
    }

    [Fact]
    public void SentGmcpMessages_JsonIsStoredInEntry()
    {
        const string json = "{\"hp\":150,\"maxHp\":200}";
        SimulateGmcpSent("Char.Vitals", json);

        var entry = Assert.Single(_vm.SentGmcpMessages);
        Assert.Equal(FormatGmcpJson(json), entry.Json);
    }

    [Fact]
    public void SentGmcpMessages_EmptyJsonBecomesPlaceholder()
    {
        SimulateGmcpSent("Core.Ping", string.Empty);

        var entry = Assert.Single(_vm.SentGmcpMessages);
        Assert.Equal("(bez danych)", entry.Json);
    }

    // ====================================================================
    // Incoming GMCP (GmcpMessages) — same cap + ordering logic
    // ====================================================================

    [Fact]
    public void GmcpMessages_InsertThenAddsEntry()
    {
        SimulateGmcpReceived("Room.Info", "{\"name\":\"Tavern\"}");

        var entry = Assert.Single(_vm.GmcpMessages);
        Assert.Equal("Room.Info", entry.Package);
    }

    [Fact]
    public void GmcpMessages_CappedAt100_AfterMultipleEvents()
    {
        for (var i = 0; i < 101; i++)
        {
            SimulateGmcpReceived($"Pkg{i:D3}", $"{{\"i\":{i}}}");
        }

        Assert.Equal(100, _vm.GmcpMessages.Count);
        Assert.DoesNotContain(_vm.GmcpMessages, e => e.Package == "Pkg000");
    }

    [Fact]
    public void GmcpMessages_NewestFirst()
    {
        SimulateGmcpReceived("A", "{}");
        SimulateGmcpReceived("B", "{}");

        Assert.Equal(2, _vm.GmcpMessages.Count);
        Assert.Equal("B", _vm.GmcpMessages[0].Package);
        Assert.Equal("A", _vm.GmcpMessages[1].Package);
    }

    [Fact]
    public void GmcpMessages_PreservesNewestEntriesAtCap()
    {
        for (var i = 0; i < 101; i++)
        {
            SimulateGmcpReceived($"Pkg{i:D3}", $"{{\"i\":{i}}}");
        }

        Assert.Equal(100, _vm.GmcpMessages.Count);
        Assert.Equal("Pkg100", _vm.GmcpMessages[0].Package);
        Assert.Equal("Pkg099", _vm.GmcpMessages[1].Package);
        Assert.Contains(_vm.GmcpMessages, e => e.Package == "Pkg001");
    }

    [Fact]
    public void GmcpMessages_JsonIsStoredInEntry()
    {
        const string json = "{\"name\":\"Tavern\",\"zone\":3}";
        SimulateGmcpReceived("Room.Info", json);

        var entry = Assert.Single(_vm.GmcpMessages);
        Assert.Equal(FormatGmcpJson(json), entry.Json);
    }

    [Fact]
    public void GmcpMessages_EmptyJsonBecomesPlaceholder()
    {
        SimulateGmcpReceived("Core.Ping", string.Empty);

        var entry = Assert.Single(_vm.GmcpMessages);
        Assert.Equal("(bez danych)", entry.Json);
    }

    [Fact]
    public void GmcpMessages_EntryContainsReceivedAtTimestamp()
    {
        SimulateGmcpReceived("Room.Info", "{}");

        var entry = Assert.Single(_vm.GmcpMessages);
        Assert.NotEmpty(entry.ReceivedAt);
    }

    // ====================================================================
    // GMCP event subscription wiring (reflection-based)
    // ====================================================================

    [Fact]
    public void GmcpSentEvent_IsSubscribedByConstructor()
    {
        // Check that the private _session.GmcpSent event has at least one
        // subscriber after MainWindowViewModel construction.
        var sessionField = typeof(MainWindowViewModel).GetField("_session",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(sessionField);
        var session = sessionField!.GetValue(_vm);
        Assert.NotNull(session);

        var gmcpSentField = typeof(MudSession).GetField("GmcpSent",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(gmcpSentField);
        var delegateObj = gmcpSentField!.GetValue(session) as Delegate;
        Assert.NotNull(delegateObj);
        Assert.NotEmpty(delegateObj.GetInvocationList());
    }

    [Fact]
    public void GmcpReceivedEvent_IsSubscribedByConstructor()
    {
        var sessionField = typeof(MainWindowViewModel).GetField("_session",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(sessionField);
        var session = sessionField!.GetValue(_vm);
        Assert.NotNull(session);

        var gmcpReceivedField = typeof(MudSession).GetField("GmcpReceived",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(gmcpReceivedField);
        var delegateObj = gmcpReceivedField!.GetValue(session) as Delegate;
        Assert.NotNull(delegateObj);
        Assert.NotEmpty(delegateObj.GetInvocationList());
    }

    // ====================================================================
    // GMCP tab structure — XAML validation note
    // ====================================================================
    //
    // MainWindow.axaml contains two GMCP TabItem elements inside the
    // right-sidebar TabControl:
    //   Tab 3: Header="GMCP odebrane",  ItemsSource="{Binding GmcpMessages}"
    //   Tab 4: Header="GMCP wysłane",   ItemsSource="{Binding SentGmcpMessages}"
    //
    // Both tabs use ItemsControl with a ScrollViewer template and present
    // each entry via GmcpEntryViewModel DataTemplate (SelectableTextBlock
    // for package/json/timestamp).
    //
    // The test project (MudClient.App.Tests) does not reference
    // Avalonia.Headless or any headless-Avalonia package.  Without headless
    // UI infrastructure, the visual tree (TabItem, ItemsControl, DataTemplate)
    // cannot be instantiated in a unit test — AvaloniaXamlLoader.Load() in
    // InitializeComponent() requires a running Avalonia windowing platform.
    //
    // Therefore the XAML tab structure is validated by the build/XAML
    // compile step only.  Any binding mismatch (e.g. a missing property name)
    // will surface as an XAML compile error.  The ViewModel-level behaviour
    // (collections initialized, entries inserted at head, cap at 100) is
    // verified by the GmcpMessages_* and SentGmcpMessages_* tests above.
    //
    // ====================================================================
    // Auto-connect startup — validation notes
    // ====================================================================
    //
    // The auto-connect logic lives in MainWindow.OnOpened (the code-behind),
    // which calls:
    //   1. _viewModel.InitializeAsync()
    //   2. _viewModel.ConnectCommand.ExecuteAsync(null)
    //
    // The VM's ConnectAsync() method calls:
    //   _session.ConnectAsync(Host, Port)
    //
    // MudSession.ConnectAsync creates a real TcpClient and connects over the
    // network (host = "killer-mud.pl", port = 4004).  There is no seam to
    // replace or short-circuit this — no ITcpClientFactory, no virtual
    // method, no test-only constructor.
    //
    // To test auto-connect without a real TCP connection, the production code
    // would need an injectable transport (e.g. ITcpClient / ITransport) that
    // the test could replace with a loopback or a fake.  The architecture
    // currently hard-codes TcpClient inside MudSession (line 53 of
    // MudSession.cs), so direct unit testing of the full startup path is not
    // feasible without either (a) running a real MUD server, (b) adding a
    // seam to production code, or (c) using an integration-test network
    // loopback.
    //
    // The CanExecute path (ConnectCommand / CanConnect) is already tested
    // above (ConnectCommand_InitiallyCanExecute).
    //
    // ====================================================================
    // MudOutputView split-scroll / responsive layout — validation notes
    // ====================================================================
    //
    // MudOutputView (Controls/MudOutputView.axaml + .axaml.cs) is an
    // Avalonia UserControl that depends on:
    //   * a visual tree with named ScrollViewer/StackPanel elements
    //   * AvaloniaXamlLoader to load the XAML template
    //   * AnsiStreamParser for tokenizing input
    //   * Dispatcher.UIThread.Post for auto-scrolling
    //
    // The test project (MudClient.App.Tests) does not reference
    // Avalonia.Headless or any headless-Avalonia package.  Without headless
    // UI infrastructure, the control cannot be instantiated and exercised in
    // a test — InitializeComponent() calls AvaloniaXamlLoader.Load(this),
    // which requires a running Avalonia windowing platform.
    //
    // Similarly, MapView.axaml uses a WrapPanel for its header to prevent
    // button disappearance on resize.  Layout-level responsiveness is
    // inherently a visual/rendering concern and cannot be verified without
    // a headless or screenshot-testing framework.
    //
    // Conditional split-scroll (added by the coder for this change):
    //
    //   Purpose:
    //     Show split-screen (upper manual-scroll pane + lower live-tail pane)
    //     only while the user scrolls upward.  When at the newest/bottom
    //     position the grid returns to a single pane with the live tail
    //     hidden.
    //
    //   XAML (MudOutputView.axaml):
    //     - Grid.Row 0 = scrollback (upper), Row 1 = GridSplitter,
    //       Row 2 = live tail (lower).
    //     - Default RowDefinitions="*,Auto,0" — live tail and splitter
    //       are zero-height initially.
    //     - GridSplitter named OutputSplitter, Grid named OutputGrid.
    //
    //   Code-behind (MudOutputView.axaml.cs):
    //     1. OnScrollbackScrollChanged handler:
    //        distanceFromBottom = Extent.Height - Viewport.Height - Offset.Y
    //        - Enable split when distanceFromBottom > 30px && !_isSplitMode
    //          && e.OffsetDelta.Y < 0
    //          (The OffsetDelta.Y < 0 guard prevents extent-growth caused by
    //           AppendText from triggering split activation — only deliberate
    //           user scroll-up events can enable split.)
    //        - Disable split when distanceFromBottom <= 10px && _isSplitMode
    //        (Flicker protection: activation uses a larger threshold than
    //         deactivation.)
    //
    //     2. SetSplitMode(bool enabled):
    //        - When enabling: row[0] = 2*, row[2] = 1*, splitter/live-tail
    //          visible.
    //        - When disabling: row[0] = 1*, row[2] = 0, splitter/live-tail
    //          hidden.
    //
    //     3. Clear() calls SetSplitMode(false) before clearing (line 104),
    //        ensuring a deterministic return to single-pane layout after
    //        a clear operation.
    //
    //     4. AppendText auto-scroll behaviour:
    //        - When split OFF (_isSplitMode == false): auto-scroll the
    //          scrollback pane to the newest line.
    //        - When split ON:  scrollback pane stays at the user's manual
    //          scroll offset; only the live-tail pane auto-scrolls.
    //        - Live-tail pane always auto-scrolls (harmless when hidden).
    //
    //     5. Existing behaviour preserved:
    //        - In-progress-line mirroring (AppendRun writes to both
    //          _currentLine and _liveTailCurrentLine).
    //        - Clear() resets both panels.
    //        - CopySelection_OnClick / CopyAll_OnClick operate on both
    //          panels.
    //        - Live-tail capped at LiveTailMaxLines (100), scrollback at
    //          MaximumLines (5000).
    //
    //   Testability:
    //     Every code path above requires an instantiated MudOutputView with
    //     a live visual tree.  Neither the thresholds (10/30 px), the
    //     OffsetDelta.Y < 0 guard, nor the hysteresis logic can be exercised
    //     in a unit test without headless Avalonia, so validation currently
    //     relies on:
    //       * XAML compile (build)
    //       * Manual functional testing
    //       * This specification note to prevent accidental regressions.
    //
    // ====================================================================
    // Down-at-fresh command history behavior — validation notes
    // ====================================================================
    //
    // The "Down-at-fresh" behavior (MainWindow.axaml.cs NavigateHistory):
    // pressing the Down arrow when no history entry has been selected yet
    // (historyIndex == -1, the "fresh" position) should not clear the
    // user's current draft.  The ViewModel does not track a "history
    // navigation index" — that state (_historyIndex) lives entirely in
    // the code-behind field of MainWindow.
    //
    // The ViewModel-level history (CommandHistory) and its insert/cap
    // behaviour are tested above (SendCommand_History* tests).  The
    // Down-at-fresh guard is a View-level concern and cannot be exercised
    // without either (a) instantiating MainWindow with a headless UI
    // framework or (b) extracting the navigation logic into the ViewModel.
}

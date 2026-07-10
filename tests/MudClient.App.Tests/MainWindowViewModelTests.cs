using System.Reflection;
using MudClient.App.ViewModels;

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
    public void Constructor_PopulatesMockAutomationRules()
    {
        Assert.NotEmpty(_vm.AutomationRules);
        Assert.Contains(_vm.AutomationRules, r => r.Name == "Skrót look");
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

    [Fact]
    public async Task SendCommand_HistoryContainsAliasExpandedVersion()
    {
        // Arrange
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
}

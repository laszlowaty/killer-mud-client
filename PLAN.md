# Plan: keep sent MUD command selected in input

## Context
- The command input is `CommandBox` in `src/MudClient.App/Views/MainWindow.axaml`.
- Enter handling lives in `src/MudClient.App/Views/MainWindow.axaml.cs` in `CommandBox_OnKeyDown()`.
- Sending currently calls `MainWindowViewModel.SendCurrentCommandAsync()`, which trims `CommandText`, clears it immediately, processes aliases, adds history, and sends the processed command.
- User wants the old typed text to remain in the input after sending and be selected so pressing Enter resends it, while typing a different command replaces it immediately.

## Implementation
1. Change command sending so a sent command is not cleared from the command input.
   - In `MainWindowViewModel.SendCurrentCommandAsync()`, remove the `CommandText = string.Empty;` clearing behavior.
   - Preserve the existing send semantics: trim the source text, process aliases, record/send the processed command, and keep error handling.
   - Prefer keeping the user's original typed text in `CommandText`, not the alias-expanded command, so the visible text matches what the user entered.
2. Select the full command text after sending from the command box.
   - In `CommandBox_OnKeyDown()` after `SendCommandCommand.Execute(null)` succeeds/starts, focus `CommandBox` and select all current text.
   - Use Avalonia `TextBox.SelectAll()` if available, otherwise set selection start/end explicitly.
   - Keep `eventArgs.Handled = true` and `_historyIndex = -1` behavior.
   - Ensure the selected text remains in place so pressing Enter again sends the same command; normal typing should replace selected text using default TextBox behavior.
3. Keep scope limited to app UI/view-model command-input behavior.
   - Do not modify `MudClient.Core`, Telnet/networking, alias processing, or unrelated UI.
   - No README update is required because this is a small interaction change.

## Verification
- Add/update app tests only if existing test structure can directly cover the view-model change (for example, asserting `CommandText` remains after send where practical). Do not ask the production-code coder to write tests.
- Build with `dotnet build MudClientStarter.sln`.
- Run relevant tests, preferably `dotnet test MudClientStarter.sln` or at least `dotnet test tests/MudClient.App.Tests/MudClient.App.Tests.csproj`.

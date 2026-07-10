# Plan

## Goal
When the user clicks the MUD output/text area and the app redirects focus to the command input, the command input should select all currently entered text instead of placing the caret at the end.

## Context
- UI is Avalonia.
- `MainWindow.axaml` contains `MudOutput` and the command `TextBox` named `CommandBox`.
- `MainWindow.axaml.cs` already redirects generic window clicks to `_commandBox.Focus()` in `Window_OnPointerPressed`, but it currently sets `CaretIndex` to the end.
- `Window_OnPointerPressed` currently treats `SelectableTextBlock` as an interactive control and returns early, which can prevent clicks directly on output text from using the command-box focus behavior.
- Existing `HandlePostSend()` focuses and selects all after sending; the new behavior should align with that select-all command-input pattern.

## Implementation steps for coder
1. Modify production UI code only, primarily `src/MudClient.App/Views/MainWindow.axaml.cs`.
2. Add a small helper method if useful, e.g. `FocusCommandBoxAndSelectAll()`, that focuses `_commandBox` and calls `_commandBox.SelectAll()`.
3. In `Window_OnPointerPressed`, when a non-interactive click is redirected to the command box, select all text in the command input instead of moving the caret to the end.
4. Ensure clicks on the MUD output text itself are included in this behavior. Do not exclude `SelectableTextBlock` descendants from the redirect when they are inside `MudOutput`; preserve exclusions for genuinely interactive controls such as buttons, text boxes, list boxes, tab controls, grid splitters, scrollbars, combo boxes, toggle buttons, map control, and progress bars. Avoid breaking text selection/copying in other selectable text areas if possible.
5. Do not change `OnPreviewTextInput` behavior unless necessary: typing while focus is outside a text-editing control should continue appending/entering text normally, not unexpectedly replacing existing command text due to select-all.
6. Reuse the helper in `HandlePostSend()` if appropriate to keep behavior consistent.
7. Do not modify core networking/protocol code.

## Verification expectations
- Project builds.
- Existing tests pass.
- New tests may be added by tester to cover clicking output focuses command box and selects all existing command text.

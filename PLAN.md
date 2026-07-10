Plan: make MUD output split-screen conditional on scrollback

Context:
- `MudOutputView` currently always shows two panes: upper full scrollback and lower live-tail.
- User wants split-screen to enable only while they are scrolled up in the main output.
- When the user scrolls back down to the newest/active messages, the split should turn off and the output should return to a single-pane live view.

Implementation:
1. Update `src/MudClient.App/Controls/MudOutputView.axaml` so the live-tail pane and splitter can be hidden when split mode is off.
   - The normal/default mode should be a single full-height scrollback output pane.
   - The split mode should show upper scrollback + splitter + lower live-tail.
2. Update `MudOutputView.axaml.cs` to track whether the scrollback pane is at the bottom.
   - Define a small bottom tolerance to account for floating-point/layout differences.
   - Subscribe to the scrollback `ScrollChanged` event or equivalent Avalonia event.
   - If the user/main scrollback offset moves away from the bottom, enable split mode.
   - If the scrollback reaches the bottom again, disable split mode.
3. Preserve auto-scroll behavior:
   - When split mode is off, appended text should auto-scroll the main scrollback pane to the newest output.
   - When split mode is on, appended text should not move the user's upper scrollback position, but the lower live-tail pane should continue auto-scrolling to newest output.
4. Keep existing functionality intact:
   - The live-tail pane must still mirror in-progress lines that do not end with newline.
   - Copy selection, copy all, clear, line caps, ANSI styling, and selectable text must continue to work.
5. Avoid `Thread.Sleep`; use Avalonia dispatcher posts as already done.

Validation to be handled by tester:
1. Add/update tests only if feasible with current test infrastructure. The project currently lacks Avalonia Headless, so direct visual/scroll tests may not be possible.
2. At minimum, run `dotnet test MudClientStarter.sln` and document any UI testing limitations.

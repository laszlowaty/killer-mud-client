Plan: make aliases and triggers support multi-line commands

Context:
- Timers already support multiple commands using `TimerEntry.CommandsText`: one command per line, blank lines skipped, commands sent top-to-bottom.
- Alias/trigger UI currently has a single-line action `TextBox` bound to `NewRuleAction` and stores `AutomationRuleEntry.Action` as one string.
- `AliasEngine.Process(command)` currently returns one replacement string; `MainWindowViewModel.SendCurrentCommandAsync` sends it as one command.
- `TriggerEngine.Evaluate(line)` currently returns one command string per matching trigger rule; `MainWindowViewModel.OnLineReceived` sends each returned string.
- User wants triggers and aliases to be multi-line like timers: one command per line.

Production implementation:
1. Add a shared command-splitting behavior for alias/trigger actions.
   - Split on `\n`, trim whitespace and trailing `\r`, skip empty/whitespace-only lines.
   - Preserve command order.
   - Keep capture group substitution (`$1`, `$2`, etc.) working for each generated line. It is acceptable to apply `match.Result(...)` to the full multi-line action and then split the resulting text.
2. Update core automation behavior.
   - For triggers, update `TriggerEngine.Evaluate` so a single matched trigger action can produce multiple command strings.
   - For aliases, either add a new API (preferred, e.g. `ProcessCommands(string command): IReadOnlyList<string>`) or adjust app usage so a multi-line alias replacement results in multiple sent commands.
   - Preserve backward compatibility: a single-line alias/trigger must behave exactly as before.
   - If no alias matches, the original typed command should still be sent as one command.
3. Update `MainWindowViewModel.SendCurrentCommandAsync`.
   - Process the user's typed command through aliases and send every resulting command line in order.
   - Emit/system history behavior should remain sensible: keep recording the original typed command in history as today; if system echo currently shows the sent command, echo each sent command or otherwise avoid hiding commands.
   - Do not use `.Wait()`/`.Result`; send sequentially with `await` so command order is preserved.
4. Update the alias/trigger editor UI in `MainWindow.axaml`.
   - Change the action field to a multi-line TextBox like timers: `AcceptsReturn="True"`, `TextWrapping="Wrap"`, monospace, reasonable `MinHeight`.
   - Update label/placeholder to indicate `jedna komenda w linii` and capture groups are still allowed.
   - Update rule display in the list so multi-line actions are readable (wrap/preserve newlines) rather than character-ellipsis truncation only.
5. Keep persistence compatible.
   - `AutomationRuleEntry.Action` and profile data can remain a string containing newlines; no schema migration should be required.
   - Existing one-line rules should continue to load and work.
6. Respect architecture rules: no blocking waits; triggers should not execute directly on the UI thread; no unrelated behavior changes.

Validation to be handled by tester:
1. Add/update core tests for `TriggerEngine.Evaluate`: one matched rule with multi-line action returns multiple commands in order, blank lines skipped, capture groups substituted.
2. Add/update core tests for alias multi-line behavior via the chosen API or app pipeline: matched alias produces multiple commands in order, blank lines skipped, capture groups substituted, unmatched input returns the original command.
3. Add/update app VM tests for sending multi-line aliases sequentially if existing infrastructure can fake/session-inspect sends; otherwise test helper/model behavior and document limitation.
4. Add UI/model tests where feasible to ensure multiline action text is stored unchanged in `AutomationRuleEntry`.
5. Run `dotnet test MudClientStarter.sln` and report results.

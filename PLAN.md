# Plan: command stacking

## Goal
Add command stacking: one text value may contain multiple commands separated by a configurable separator. The separator is stored in application settings and applies to commands typed by the user, alias replacements, trigger actions, and timer commands.

## Constraints
- Keep `MudClient.Core` UI-independent.
- Do not use regex for Telnet parsing; this change should not touch Telnet parsing.
- Preserve existing newline-based multi-command behavior for aliases/triggers/timers.
- Avoid blocking async work; keep cancellation/finally patterns already present.

## Implementation steps
1. Add a reusable core splitter in `MudClient.Core.Automation` (for example `CommandStacker` or similar) that:
   - Uses a default separator constant (suggested: `;`).
   - Splits on `\n` and, when the configured separator is non-empty, on the separator too.
   - Trims whitespace and trailing `\r` from each command and skips empty items.
   - Treats null/empty separator as "stacking disabled except newlines".
2. Update `AliasEngine` and `TriggerEngine` to accept/use a configurable separator for `ProcessCommands`/`Evaluate` while keeping old overloads or defaults so current callers/tests still compile.
3. Update `TimerEntry.GetCommands` so timer text is split with the same configurable separator; keep a default/no-arg path for existing callers.
4. Add `CommandStackingSeparator` to `AppSettings` with default `;`; normalize loaded settings in `AppSettingsService.Load` so null/whitespace becomes the default.
5. Add `CommandStackingSeparator` property on `MainWindowViewModel` that persists via `SaveSettings()`, raises change notification, and supplies the value to:
   - typed command sending in `SendCurrentCommandAsync` (split typed text before alias processing; run aliases per stacked command; keep one history entry for original input),
   - trigger evaluation in `OnLineReceived`,
   - timer validation, serialization, and timer execution (`GetCommands(separator)`).
6. Update Settings UI (`SettingsPanelView.axaml`) with a small field for the separator and explanatory text that it is global and saved automatically.
7. Keep autowalk slash commands working for single commands; stacked input may be handled by splitting first, with `/idz` or `/stop` consumed per segment and non-slash segments sent normally.
8. Leave production behavior documented via XML comments or UI help text; update README only if this changes documented user behavior there.

## Verification expected from tester
- Add/update unit tests for the core splitter, alias, trigger, timer, settings persistence/defaults, and VM settings property where practical.
- Run `dotnet test` from repo root.

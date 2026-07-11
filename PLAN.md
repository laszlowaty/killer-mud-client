# Plan

## Goal
Make the HP and MV side bars stretch across the full height of the terminal panel/window area, not only the MUD output row. They should stay pinned to the left and right sides while the terminal content remains in the center.

## Context
- Avalonia UI lives under `src/MudClient.App`.
- `src/MudClient.App/Views/Panels/TerminalPanelView.axaml` currently has a root grid with rows `Auto,*,Auto,Auto`.
- The HP/MV bars are currently inside `Grid.Row="1"` beside `MudOutputView`, so they only span the output area and do not cover the filter tabs, quick command chips, and command bar height.
- The previous change removed the top HP/SP/EP strip and added left HP / right MV indicators. SP should remain hidden from visible UI.

## Implementation steps for coder
1. Modify production UI code only. Do not write or update tests.
2. Restructure `TerminalPanelView.axaml` so the root layout has side columns for the vitals bars and a center column for all terminal content.
3. Put the HP bar in the left column and the MV bar in the right column so each spans the full root grid height of the terminal panel.
4. Move/preserve the existing terminal content (filter tabs, `MudOutputView`, quick command chips, command bar) in the center column with its existing row structure and behavior.
5. Ensure each bar visually stretches vertically: use appropriate `VerticalAlignment="Stretch"`, star-sized inner progress area, and avoid margins/padding that make the bar look detached from the panel height. Small internal padding is OK.
6. Keep labels/bindings unchanged: HP uses `Vitals.HitPoints` / `Vitals.MaxHitPoints`; MV uses `Vitals.EndurancePoints` / `Vitals.MaxEndurancePoints`; no visible SP.
7. Do not modify `MudClient.Core` or any networking/protocol code.

## Verification expectations
- `dotnet build` succeeds.
- Existing tests are run or known unrelated failures are documented.
- Visual intent: HP and MV side bars occupy the full height of the terminal panel/window area, with terminal controls and output between them.

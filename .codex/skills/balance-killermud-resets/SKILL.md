---
name: balance-killermud-resets
description: Create ordered KillerMUD resets that equip required tactical gear in correct slots, then review encounters, spellcasting, population, equipment, rewards, and traversal balance for an accepted isolated area within a user-supplied inclusive vnum range. Use after rooms, descriptions, mobs, and items are complete and before final review.
---

# Balance KillerMUD Resets

Read the shared workflow contract, all completed project stages, `tools/KillerMUD/area-lore/olc.hlp` reset guidance, loader/reset implementation, and several active areas from `tools/KillerMUD/area-lore` in the same level band. Confirm the supplied inclusive vnum range matches the project.

Build a room-by-room population and reward budget. Keep safe arrival space readable, distribute ambient and hostile populations according to room roles, limit elites and bosses, avoid unavoidable stacked encounters, and ensure rewards match risk and intended level. Verify keys can be obtained before their locks, containers before contents, and equipment/give resets immediately after the correct mob. Use explicit chances and maxima consistent with current syntax.

Materialize every `equipmentAssignments` entry immediately after the correct `M` reset. Use `E` for equipment needed at combat start and copy the numeric wear location from the validated `equipVerification.resetSlot`; confirm it matches both the semantic slot and the object's actual `Wear` flag. Do not merely give a backstabber a dagger in inventory, and never force an object into a slot the mob could not equip normally. Required tactical equipment uses 100% on its conditional `E` reset, so every successfully loaded mob receives it. Optional cosmetic or reward gear may use a lower chance. Simulate the mob with and without every optional item, and include spell repertoires when comparing encounter strength.

Add normalized entries to `resets` in `area-project.json` and matching ordered lines to `#RESETS` in `draft.are.utf8`. Every room and mob reference must be internal. Object references must be internal or an explicitly justified `externalDependencies` entry. Do not create cross-area travel through resets or programs.

Compare the complete package—statistics, class act flags, offensive skills, `Spells`, equipped weapon/armor, and group density—against multiple active examples; do not infer balance from level alone. Record assumptions, outliers, expected density, boss cadence, and reward risks in `reviewNotes`. Run the shared validator with `--stage resets`, then set `stages.resets` to `complete` and validate again.

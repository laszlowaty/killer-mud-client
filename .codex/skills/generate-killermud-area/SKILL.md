---
name: generate-killermud-area
description: Orchestrate creation of a new isolated KillerMUD area from an explicitly approved lore-backed concept and a user-supplied vnum range. Use when Codex should turn a queried and accepted area idea into a staged draft covering rooms, descriptions, mobs, items, resets, balance, and final review without connecting it to existing areas or adding it to area.lst.
---

# Generate KillerMUD Area

Coordinate the stage skills; do not replace their specialist work.

## Establish the gate

1. Use `$query-killermud-lore` to research the idea. Use relevant `analyze-killermud-*` skills when the concept depends on history, politics, religion, or legends.
2. Use `$prepare-killermud-area-context` when an anchor place exists. Treat its `WorldContextPack` as evidence, not approval.
3. Present a bounded concept and wait for explicit user acceptance. Do not interpret a query or context pack as acceptance.
4. Require: slug, area name, inclusive vnum range, exact room count, intended level range, entry-room premise, and acceptance note. Keep the entrance internal: it represents where a future connection could arrive, but has no exit outside the range.
5. Read [references/workflow-contract.md](references/workflow-contract.md), then initialize the workspace:

```powershell
python .codex/skills/generate-killermud-area/scripts/area_project.py init `
  --slug sine-rozlewiska --name "Sine Rozlewiska" `
  --vnum-min 32000 --vnum-max 32099 --room-count 60 `
  --level-min 35 --level-max 45 `
  --approval-note "Zaakceptowane przez użytkownika" `
  --brief "Bagienna kraina pogranicza" `
  --context lore/dist/context/easterial-nearby.world-context.json
```

Never overwrite an existing workspace. Never reserve a colliding range.

## Run the stages

Invoke in order, passing the same inclusive range and workspace path each time:

1. `$create-killermud-room-grid`
2. `$describe-killermud-rooms`
3. `$create-killermud-mobs`
4. `$create-killermud-items`
5. `$balance-killermud-resets`
6. `$review-killermud-area`

After every stage, run:

```powershell
python .codex/skills/generate-killermud-area/scripts/area_project.py validate `
  --project tools/KillerMUD/area-lore/drafts/sine-rozlewiska/area-project.json `
  --stage grid
```

Use the corresponding stage name: `grid`, `descriptions`, `mobs`, `items`, `resets`, or `review`.

Preserve the handoffs across stages: the grid explicitly decides each room's lighting mode; descriptions must agree with it; mob text uses no feminatives; the mob stage declares flags, skills, spells, and equipment needs; the item stage proves each object is genuinely equippable by that mob; the reset stage uses the verified numeric slot; final review rejects any broken link. If a later stage cannot fulfill an earlier decision, mark that earlier stage and all successors `pending` instead of weakening the requirement silently.

## Finish safely

Only the review skill may publish `draft.are.utf8` to `<slug>.are`. Publishing converts UTF-8 to ISO-8859-2 (Latin-2), matching the active area sources. Use only `tools/KillerMUD/area-lore` for source inspection, workspaces, validation, and publication. Do not edit `tools/KillerMUD/area-lore/area.lst`, create cross-area exits, declare new lore canonical, or copy the result over an active area.

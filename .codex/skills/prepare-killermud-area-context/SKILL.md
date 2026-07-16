---
name: prepare-killermud-area-context
description: Build a bounded, self-contained WorldContextPack JSON file from canonical KillerMUD lore for use by a future AI area-generation skill. Use when an anchor place and optional placement direction are known and the user needs relevant geography, politics, religion, history, legends, peoples, constraints, styles, conflicts, and source provenance without generating any area content.
---

# Prepare KillerMUD Area Context

Read `lore/` and write only a `WorldContextPack`. Do not generate rooms, NPCs, objects, programs, vnums, exits, or `.are` files.

Run from the KillerMudClient root:

```powershell
python .codex/skills/prepare-killermud-area-context/scripts/build_world_context.py `
  --lore-root lore `
  --anchor place:arras `
  --placement "na południowy wschód od Arras" `
  --radius 2 `
  --max-records 250 `
  --output lore/dist/context/arras-south-east.world-context.json
```

Use `lore/schema/world-context-pack.schema.json` as the output contract. Require an existing anchor ID. Include related entities only within the requested relation radius, then attach relevant claims, events, narratives, constraints, styles, conflicts, gaps, and unique source references. If the record limit is exceeded, narrow the radius or anchor instead of silently dropping constraints.

Review the pack for missing placement evidence and unresolved hard constraints. Report those gaps; do not fill them by invention. Treat the resulting JSON as input for a future area builder, not as approval or a design for an area.

---
name: create-killermud-room-grid
description: "Create the first stage of an isolated KillerMUD area for a user-supplied inclusive vnum range: an internally connected room graph with one entry premise, room names, roles, zones, coordinates, reciprocal exits, and valid sector numbers. Use after an area concept has been explicitly accepted and its generate-killermud-area workspace exists."
---

# Create KillerMUD Room Grid

Read `.codex/skills/generate-killermud-area/references/workflow-contract.md`, the workspace `area-project.json`, its lore context, `tools/KillerMUD/src/const.c` sector table, and representative active rooms. Confirm the supplied inclusive vnum range exactly matches the project.

Design zones and progression from the internal entry room before assigning vnums. Make every transition narratively plausible: arrival space, threshold, public circulation, increasingly restricted or dangerous branches, focal locations, and purposeful service spaces. Never use a stable, sewer, refuse area, or backstage room as the unexplained approach to a temple, court, archive, or other focal place.

Create exactly `roomCount` rooms in a connected graph reachable from the entry. Use reciprocal cardinal exits, paired vertical exits, at least one loop for a nontrivial area, and intentional dead ends only. Do not create an exit, portal, or program target outside the range. Choose sectors only from the current `sector_table` and consider movement requirements.

Decide lighting explicitly for every room from its architecture, openings, stable fixtures, sector and intended time/weather behavior. Record `lightMode` as `natural`, `lit`, or `dark`; never leave it implicit. `lit` means the renderer will set `ROOM_LIGHT`, `dark` means `ROOM_DARK`, and `natural` leaves both flags clear.

Fill `entryRoomVnum` and `rooms` in `area-project.json` with `vnum`, `name`, numeric `sector`, narrative `role`, `zone`, integer `x/y/z`, `lightMode`, and `exits` containing `direction` and `to`. Descriptions may remain short explicit placeholders for the next stage. Materialize matching room records without touching other sections:

```powershell
python .codex/skills/generate-killermud-area/scripts/area_project.py render-grid `
  --project <workspace>/area-project.json
```

Do not add mobs, objects, or resets.

Run the shared validator with `--stage grid`. Fix every error, then set `stages.grid` to `complete` and validate again. Report the room count, unused room vnums, entry vnum, zones, loops, and deliberate dead ends.

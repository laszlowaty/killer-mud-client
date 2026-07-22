---
name: describe-killermud-rooms
description: Add substantial Polish room descriptions and verify sensory, functional, navigational, and reset continuity for a completed KillerMUD room grid within a user-supplied inclusive vnum range. Use for the second stage of an accepted isolated area workspace when names, sectors, topology, roles, and zones already exist and must remain structurally unchanged.
---

# Describe KillerMUD Rooms

Read the shared workflow contract, approved brief, lore context, completed room ledger, and several stylistically relevant active areas. Confirm the supplied range matches the project and `stages.grid` is complete.

Write distinct Polish descriptions grounded in what can be perceived from each room. Normally use 3-5 sentences and roughly 300-650 characters: establish the space, add two or more concrete sensory or functional details, and clarify at least one meaningful transition or visible landmark. Use shorter text only for an intentionally stark connector; record the reason in both that room's `descriptionLengthException` and `reviewNotes`. Preserve lore uncertainty; do not turn rumors or legends into objective scenery. Maintain continuity across exits, environmental transitions, vertical movement, weather exposure, architecture, and the entry premise. Avoid repetitive openings, encyclopedia exposition, player actions, assumed emotions, filler, and details contradicted by adjacent rooms.

Do not use feminatives in player-facing prose. Recheck every room's `lightMode` against the finished description: permanent lamps, magical glow or other reliable illumination normally require `lit`; intentional darkness requires `dark`; daylight, weather, time of day, carried lights, and reset-driven light sources require `natural`. If the prose exposes a wrong lighting decision, return to the grid stage instead of leaving the flag inconsistent.

Update each room's `description` in `area-project.json` and the matching `Descr ... ~` field in `draft.are.utf8`. Do not change topology, names, sectors, mobs, items, or resets. If a structural defect blocks good descriptions, mark `grid` and all later stages `pending`, record the reason, and return to `$create-killermud-room-grid`.

After drafting, compare every description with its room name, role, sector, exits, adjacent descriptions, later mob placements, and intended reset use. Reject prose that describes a permanently present guard, weapon, corpse, light source, or furnishing unless the corresponding mob/object/reset or stable room feature will actually support it. Run the shared validator with `--stage descriptions`; set `stages.descriptions` to `complete` only after it passes. Then proofread Polish text and confirm lossless ISO-8859-2 encoding.

---
name: review-killermud-area
description: Perform final structural, narrative, lore, language, encoding, reference, combat-coherence, equipment, spell, reset, and balance review of a staged isolated KillerMUD area in a user-supplied inclusive vnum range, then publish an ISO-8859-2 .are only when all checks pass. Use as the mandatory final stage; never activate the area or connect it to other areas.
---

# Review KillerMUD Area

Read the shared workflow contract, approved brief, context pack with source provenance, entire `area-project.json`, entire `draft.are.utf8`, current loader syntax, and relevant active exemplars. Confirm the supplied range matches and all prior stages are complete.

Review independently from the stage summaries:

1. Check every record and reference against the inclusive range, declared external object dependencies, exact section order, required terminators, current flags/tables, and ISO-8859-2 encodability.
2. Walk every route from the entry. Check reciprocity, reachability, loops, vertical pairs, doors/keys, dead ends, and narrative thresholds. Reject implausible access such as entering a sacred or formal focal place through an unrelated stable or service room.
3. Cross-check names, room descriptions, mobs, objects, and programs against the accepted brief and lore evidence. Separate canon, in-world belief, and invented local content.
4. Proofread Polish spelling, diacritics, inflection, capitalization, sensory continuity, repeated prose, and absence of feminatives. Verify that room descriptions are normally 3-5 substantive sentences and that any shorter connector has a documented reason.
5. Audit every room's `lightMode` against its rendered `FlagsExt`, sector and description. Reject permanently illuminated interiors without `ROOM_LIGHT`, unjustified forced light, and deliberate darkness without `ROOM_DARK`.
6. Audit each mob as a complete combat package. Match semantic `actFlags` and `offSkills` to actual rendered flags; verify `backstab` has an equipped dagger, `shield_block` has an equipped shield, and every weapon-dependent tactic has compatible gear. For every assignment, independently verify actual `Type`, `Wear`, `Wear2Ext`, `Value`, reset slot, race, class, anatomy, level, hands, size and weight against `can_equip_obj` behavior. For `ACT_MAGE`/`ACT_CLERIC`, require non-empty, valid, level-appropriate rendered `Spells` and review the repertoire's tactical coherence. Compare descriptions with the equipment that resets actually place on the mob.
7. Simulate resets in order. Check mob caps, chance, equipment ownership and numeric wear slot, required-equipment chance, containers, keys, encounter density, boss/reward farming, dead content, and impossible dependencies.
7. Use the server's loader/build or closest repository-supported checker. Do not call a textual inspection equivalent to a successful load.

Fix only clear local defects. For a design-level change, mark the affected stage and all later stages `pending` and route back to its skill. List every unresolved blocker in `reviewNotes`; do not publish with blockers.

When review is clean, set `stages.review` to `complete`, run:

```powershell
python .codex/skills/generate-killermud-area/scripts/area_project.py validate `
  --project <workspace>/area-project.json --stage review
python .codex/skills/generate-killermud-area/scripts/area_project.py publish `
  --project <workspace>/area-project.json
```

Confirm the produced `<slug>.are` decodes as ISO-8859-2 and matches the UTF-8 draft text. Do not add it to `area.lst`, link it to the world, or overwrite an active file.

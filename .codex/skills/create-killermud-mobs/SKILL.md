---
name: create-killermud-mobs
description: Create KillerMUD mobs with Polish forms and descriptions plus coherent class acts, offensive skills, explicit spell repertoires, equipment needs, placements, and conservative combat parameters within a user-supplied inclusive vnum range. Use after an accepted isolated area's room grid and descriptions are complete, without creating items or resets yet.
---

# Create KillerMUD Mobs

Read the shared workflow contract, project, lore context, room progression, current race/attack/flag tables, and active mobs of comparable level and role. Confirm the supplied vnum and intended level ranges match the project.

Design an ecology and social cast before individual records: ambient inhabitants, functional NPCs, common threats, elites, and at most a justified boss. Tie every mob to one or more room roles. Do not make every room hostile or densely populated. Preserve canonical peoples, religions, factions, and historical uncertainty from the context pack; mark newly invented local names and individuals as area content, not canonical lore.

For each mob, write keywords, Polish short/long/full descriptions, all required inflection forms, role, level, placement rooms, and complete current-format mechanics. Do not use feminatives in names, titles, keywords, inflection or descriptions; express a female mob's sex separately and retain the established non-feminative role noun. Add a structured `combatPlan` containing `archetype`, semantic `actFlags`, semantic `offSkills`, numeric `spells`, and `equipmentNeeds`. Treat every combat flag as a promise that later stages must fulfill, not decoration.

Derive the plan from current `tables.c`, `handler.c`, `update.c`, `skynet.c`, `load_fun.c`, and comparable active mobs. In particular:

- `backstab` requires a wielded weapon whose verified weapon class is `dagger`; add a `wield` equipment need.
- `shield_block` requires an equipment need with `slot: shield` and exact `itemType: shield`; weapon-dependent skills require a compatible weapon.
- `ACT_MAGE` or `ACT_CLERIC` requires a non-empty, level-appropriate `Spells` repertoire selected from actual active spell IDs. Include a coherent mix of offense, defense, control, or healing appropriate to role; do not rely on the class flag alone.
- Keep caster weapon and armor choices compatible with the intended tactics. Do not give flags, spells, or gear merely to inflate difficulty.

Cross-check that the short, long, and full descriptions agree with race, body parts, tactics, visible armor, and weapons. Do not claim that a mob holds or wears an item unless `equipmentNeeds` requires it and later item/reset stages can supply an object the mob can actually equip. Record relevant anatomy, class, level, hand-use, size or weight constraints in the need when they narrow valid choices. Copy no unexplained flags or statistics. Calibrate against several active examples in the same level band and document exceptional defenses, immunities, wealth, spells, or aggression in `reviewNotes`. Prefer no mob programs in the first pass.

Update `mobs` in `area-project.json` and `#MOBILES` in `draft.are.utf8`, including rendered `Spells` lines. Leave `equipmentAssignments` unresolved for the item stage. Do not add items or resets. Run the shared validator with `--stage mobs`, then set `stages.mobs` to `complete` and validate again.

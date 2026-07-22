---
name: create-killermud-items
description: Resolve mob combat-equipment needs, create KillerMUD objects, or reuse verified objects from active standard reservoirs, with Polish forms, mechanics, wear slots, and intended sources within a user-supplied inclusive vnum range. Use after rooms and mobs of an accepted isolated area are complete and before resets are authored.
---

# Create KillerMUD Items

Read the shared workflow contract, project, lore context, completed mobs, current item/material/wear tables, and comparable active objects. Confirm the supplied inclusive range matches the project.

Start from every mob's `combatPlan.equipmentNeeds`, then plan remaining objects by purpose: environmental props, keys/containers, consumables, quest or lore objects, ordinary equipment, and rare rewards. Every item needs a plausible source such as room, mob, container, shop, or scripted event; record it even though resets come later. Avoid reward inflation, redundant gear, unexplained powerful effects, and artifacts that contradict canonical lore.

Read [references/standard-reservoir.md](references/standard-reservoir.md) before creating ordinary equipment, clothing, food, drink, jewelry, furniture, or generic containers. Prefer a suitable reservoir object when it already expresses the intended generic item. Create a local object when its text, mechanics, uniqueness, key role, lore, or reward identity must belong specifically to this area.

Write keywords, Polish short/long/full descriptions and required inflection without feminatives, plus type, material, level, flags, wear location, `Wear2Ext`, all seven `Value` integers, weight, cost, condition, and intended source using current-format active exemplars. Never invent numeric value semantics. Do not copy reservoir records into `#OBJECTS`. Declare each reused reservoir object under `externalDependencies` with `kind: object`, `sourceType: standard-reservoir`, exact `sourceFile`, vnum, and a concrete reason tied to its planned reset.

Resolve every combat equipment need into one `equipmentAssignments` entry with `mobVnum`, `slot`, `objectVnum`, exact `itemType`, optional `weaponClass`, `reason`, and `equipVerification`. Inspect the actual object record rather than its name. Confirm the required letter in `Wear`, the exact numeric reset slot, `Type`, weapon class in `Value`, `Wear2Ext` race/class restrictions, mob anatomy/body parts, class weapon availability at its level, free hands and two-handed conflicts, size, and weight. Set `equipVerification.wearFlag`, `resetSlot`, `raceAllowed`, `classAllowed`, `anatomyAllowed`, `levelAllowed`, `handsAllowed`, `sizeAllowed`, and `weightAllowed`; every boolean must be true. A backstab assignment must be a wieldable dagger, and a shield assignment must be `Type shield` with the shield wear flag, not merely an object named or typed as a shield. Ensure the mob description, item description, level, material, and faction style agree. If no compatible reservoir item exists, create a conservative local object.

Update local `items`, `equipmentAssignments`, verified reservoir references in `externalDependencies`, and only local records in `#OBJECTS` of `draft.are.utf8`. Do not add resets. An area may use only reservoir objects when no local object is justified. Run the shared validator with `--stage items`, then set `stages.items` to `complete` and validate again.

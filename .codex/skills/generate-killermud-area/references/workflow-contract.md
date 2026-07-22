# Area workflow contract

## Workspace

Use `tools/KillerMUD/area-lore` exclusively for every area source, exemplar, help file, workspace, validation input, and published draft. Store each generated area under `tools/KillerMUD/area-lore/drafts/<slug>/`:

- `area-project.json`: UTF-8 design ledger and stage state.
- `draft.are.utf8`: UTF-8 area source following the current KillerMUD format.
- `<slug>.are`: ISO-8859-2 (Latin-2) output created only after final review.

Treat `area-project.json` as the semantic handoff between skills and keep it synchronized with the area source. Read local active exemplars before authoring records; the current custom format, not generic Merc documentation, is authoritative. Use `tools/KillerMUD/doc/area.txt`, `tools/KillerMUD/area-lore/olc.hlp`, the loader in `tools/KillerMUD/src/load_fun.c`, and nearby active `.are` records from `tools/KillerMUD/area-lore`.

## Vnums and isolation

- Accept an inclusive `vnumMin..vnumMax` from the user. Mob, object, and room namespaces may reuse numbers, but every record must stay within the range.
- Record the accepted exact room count as `roomCount`; it must fit within the room vnum range, and the completed grid must contain exactly that many rooms.
- Reject overlap with an active area's `VNUMs` interval even if unused holes appear available.
- Choose one internal entry room. Give it a narrative arrival premise, but no exit to a vnum outside the range.
- Do not add the draft to `area.lst`, modify another area, or create portals/programs that target external vnums.
- External stock objects used intentionally by resets must be listed in `externalDependencies` with a reason. For standard-reservoir objects, also record `sourceType: standard-reservoir` and the exact `sourceFile`. External rooms and mobs are forbidden.

## Stage ledger

Each stage updates its array and then sets `stages.<name>` to `complete` only after validation:

- `rooms`: `vnum`, `name`, `sector`, `role`, `zone`, `x`, `y`, `z`, `exits`, explicit `lightMode` (`natural`, `lit`, or `dark`), later `description`.
- `mobs`: `vnum`, `name`, `role`, `level`, `rooms`, `description`, plus `combatPlan` containing `archetype`, semantic `actFlags`, semantic `offSkills`, numeric `spells`, and structured `equipmentNeeds`.
- `items`: `vnum`, `name`, `type`, `level`, `source`, `description`.
- `equipmentAssignments`: resolved links from each mob equipment need to an internal or declared reservoir object, including `mobVnum`, `slot`, `objectVnum`, exact `itemType`, optional `weaponClass`, `reason`, and `equipVerification` with the verified wear flag, numeric reset slot, and confirmed race, class, anatomy, level, hand, size, and weight compatibility.
- `resets`: normalized commands containing `command` and referenced `mobVnum`, `objectVnum`, `roomVnum`, or `containerVnum` as applicable.

Do not silently change a completed earlier stage. If revision is necessary, mark the affected stage and every later stage `pending`, explain the change in `reviewNotes`, and revalidate.

## Standard object reservoir

Allow reuse of existing objects from the active standard reservoirs instead of duplicating their records: `rezerwua.are` (common equipment and miscellaneous objects), `rezerwuc.are` (clothing), `rezerwuf.are` (food and drink), `rezerwuj.are` (jewelry), and `rezerwum.are` (furniture). Inspect the actual `#OBJECTS` record before choosing a vnum; an area's declared interval is not proof that every number contains an object. Do not copy a reservoir record into the generated `#OBJECTS` section. Reference it through resets and declare it in `externalDependencies` with `kind: object`, `sourceType: standard-reservoir`, `sourceFile`, `vnum`, and a concrete use-specific `reason`.

## Text and source format

- Write player-facing Polish with correct inflection and diacritics. Do not use feminatives; for a female mob use a non-feminative role name and express sex separately when needed.
- Edit `draft.are.utf8`, never an ISO-8859-2 active source.
- Preserve exact current section order: `#AREADATA`, `#MOBILES`, `#OBJECTS`, `#ROOMS`, `#SHOPS`, `#BANKS`, `#SPECIALS`, `#RESETS`, program sections, traps/descriptions/repairs/bonus sets, `#$`.
- Do not invent flags, races, item types, wear locations, attacks, materials, sectors, reset forms, or program syntax. Derive them from current source tables and representative active areas.
- Decide lighting for every room. Use `lightMode: lit` only for a location that should always carry `ROOM_LIGHT` (`FlagsExt 0|H`), `dark` only for deliberate `ROOM_DARK` (`0|A`), and `natural` when sector, weather, time, carried lights, or reset objects should control visibility.
- Prefer no programs in the first pass. Programs require separate evidence-based review of every referenced vnum and command.

## Logical continuity

Build progression from the entry premise. Each transition must answer why the next place is reachable from the current one. Public arrival spaces lead to access routes or thresholds before private, sacred, military, industrial, or dangerous interiors. A stable, refuse heap, sewer, or backstage service room cannot be the unexplained route into a temple, throne room, archive, or similar focal place.

Require reciprocal ordinary exits, a connected graph reachable from the entry, at least one loop for a nontrivial area, purposeful dead ends, and no accidental one-way traps. Vertical exits must pair up/down unless explicitly designed and reviewed.

## Combat continuity

Treat combat mechanics as a staged dependency chain: role and description -> class act/offensive flags and explicit spell IDs -> compatible verified objects -> ordered equip resets -> final simulation. `backstab` requires a dagger equipped in the wield slot. `shield_block` requires an equipped shield. `ACT_MAGE` and `ACT_CLERIC` require a non-empty, level-appropriate `Spells` repertoire; the class flag alone is incomplete. Do not describe visible equipment that is absent from this chain.

An assigned object must be genuinely equippable by that mob. Verify the object's actual `Type`, `Wear`, `Wear2Ext`, weapon class and level against the semantic slot, reset wear location, mob race, class, anatomy/body parts, level, available hands, size, and weight limits. A matching name or item type alone is insufficient.

## Completion

The workflow is complete only when the final validator passes, UTF-8 converts losslessly to ISO-8859-2, the server-side area loader/build or closest available checker accepts the file, and final review reports no unresolved blocking issue. Publishing does not activate the area.

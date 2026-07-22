# KillerMUD source map

This map was prepared against local KillerMUD commit `60af6d4dd21bb41164812607790ddbafd9b6f751` from `https://github.com/iworks/KillerMUD.git`. Recheck loaders and field names after updating that checkout.

Lore parsing uses `tools/KillerMUD/area-lore` exclusively for all area/help data. Record these sources with `area-lore/...` paths in `sourceRefs.file` and `mapReferences.areaFiles`. Non-area sources (`src/`, `doc/`, `system/`, `boards/`, `player/`, `log/`) are unaffected and still live under `tools/KillerMUD/` as usual.

## Contents

- [Source priority](#source-priority)
- [Encoding and record syntax](#encoding-and-record-syntax)
- [Loaded world and area metadata](#loaded-world-and-area-metadata)
- [NPC records](#npc-records)
- [Object records](#object-records)
- [Room and exit records](#room-and-exit-records)
- [Help records](#help-records)
- [Other possible lore sources](#other-possible-lore-sources)
- [Files that are not primary lore](#files-that-are-not-primary-lore)
- [Useful searches](#useful-searches)

## Source priority

Use these sources in descending order when locating existing lore:

1. Active player-facing entries in files named by `area-lore/area.lst`.
2. Other active structured game data, for example clan or language definitions.
3. Hard-coded player-facing text and tables in `src/`.
4. Inactive `.are` files, technical documentation, reports, logs, boards, and player data.

The same fact may appear in several places. Keep every path and record identifier, and flag contradictions. A mention in a room description is evidence about what characters can observe; it is not necessarily an omniscient historical fact.

## Encoding and record syntax

The checked area content uses ISO-8859-2 (Latin-2), not UTF-8. PowerShell example:

```powershell
$bytes = [IO.File]::ReadAllBytes($path)
$text = [Text.Encoding]::GetEncoding(28592).GetString($bytes)
```

Most narrative values are `~`-terminated strings. They may begin after a field name and continue across lines until `~`. Do not split a record merely at a newline. Color/style tokens use a left brace plus a code character, for example `{Y`, `{c`, `{S`, and reset `{x`; preserve them when quoting raw data and optionally strip them for analysis.

The authoritative loaders are:

- `src/db.c`: top-level area sections, `#AREADATA`, and `#HELPS`.
- `src/load_fun.c`: current keyed NPC, object, room, trap, random-description, and description records.
- `src/olc_save.c`: the current serialized order and field names.
- `doc/area.txt`: older Merc format documentation; useful background, but the current files use extended OLC fields.

## Loaded world and area metadata

- `area-lore/area.lst`: startup list of loaded area/help files. This is the first authority for active versus inactive content.
- `area-lore/*.are`: world data. Most current files contain `#AREADATA`, `#MOBILES`, `#OBJECTS`, `#ROOMS`, and supporting mechanical sections.
- `#AREADATA` fields:
  - `Name`: displayed area/land name; useful for geography and provenance.
  - `Builders`, `Credits`: authorship, not in-world lore unless the text itself clearly says otherwise.
  - `VNUMs`: allocated vnum range.
  - `Region`: numeric grouping; resolve its meaning from source or other data before assigning an in-world region name.
  - `Locked`, `Security`, `ResetAge`: builder/runtime metadata, not lore.

Sections such as `#RESETS`, `#SHOPS`, `#BANKS`, `#SPECIALS`, `#MOBPROGSNEW`, `#OBJPROGSNEW`, `#ROOMPROGSNEW`, `#TRAPS`, `#RANDDESC`, `#DESC`, `#REPAIRS`, and `#BONUS_SET` mainly describe behavior or mechanics. Scripts and random descriptions may still contain player-facing narrative, so inspect them when researching dialogue, rituals, events, or dynamic scenery.

## NPC records

Find records under `#MOBILES`. Each begins with `#Vnum <number>`, ends with `Mobend`, and the section ends with `#Vnum 0`.

| Field | Meaning |
| --- | --- |
| `Name` | Command keywords used to target/find the NPC; not a polished display name. |
| `Odmiana` | Five Polish declined forms stored after the base form. |
| `Short` | Short referential/display phrase used in messages. Usually the best compact NPC label. |
| `Long` | Sentence shown when the NPC is visible in a room. |
| `Descr` | Full description shown when a player explicitly looks at the NPC. |
| `Race`, `Sex`, `Align` | Structured traits; useful evidence but partly mechanical. |
| `Mobprog` / `MobprogNew` | Trigger links; follow them into program sections for dialogue and behavior. |
| `Comments` | Builder comments, not player-facing canon by default. |

Other combat, dice, flags, wealth, form, parts, and position fields are mechanics. They may support a factual claim but are not prose descriptions.

## Object records

Find records under `#OBJECTS`. Each begins with `#Vnum <number>`, ends with `Objend`, and the section ends with `#Vnum 0`.

| Field | Meaning |
| --- | --- |
| `Name` | Command keywords used to target/find the object. |
| `Odmiana` | Five Polish declined forms. |
| `Short` | Short object name used in messages, inventories, and shops. |
| `Descr` | Description used when the object is displayed in the room/list context. |
| `Itemdesc` | Main detailed text shown when the player looks at the object; falls back to `Descr` when absent. |
| `Extradesc` | Keyword string followed by detailed text for a specific object detail. |
| `Identdesc` | Identification-specific description, not ordinary visual lore. |
| `Hiddendesc` | Hidden/reveal-dependent description. |
| `Material`, `Type` | Structured physical/mechanical properties. |
| `Comments` | Builder comments, not player-facing canon by default. |

Object programs can contain inscriptions, dialogue, transformations, and scripted reveals; follow them only when the topic requires dynamic behavior.

## Room and exit records

Find records under `#ROOMS`. Each begins with `#Vnum <number>`, ends with `Roomend`, and the section ends with `#Vnum 0`.

| Field | Meaning |
| --- | --- |
| `Name` | Room title. |
| `Descr` | Main room description shown during normal viewing. |
| `Nightdescr` | Night-specific room description; if empty, normal `Descr` is used. |
| `Extra` | Keyword string followed by text seen when examining that room detail. Multiple entries are allowed. |
| `Exit` / `ExitExtDescription` | Direction-specific view/exit description. |
| `ExitExtNightDescription` | Night-specific exit description. |
| `ExitExtToVnum` | Destination room vnum; use it to connect geography, not as narrative prose. |
| `Owner`, `Sector`, flags | Ownership or mechanics; verify player-visible meaning before treating as lore. |
| `Roomprog` | Trigger link to scripted room behavior and messages. |

Room `Extra` entries often hold plaques, signs, monuments, books, maps, murals, and local exposition that is absent from the main `Descr`. Search them for history, politics, and religion.

## Help records

Active help containers listed in `area-lore/area.lst` include `help.are`, `helpblk.are`, `helpcle.are`, `helpdru.are`, `helpmag.are`, `skills.are`, and `olc.hlp`.

Each `#HELPS` record is:

1. numeric minimum level,
2. a `~`-terminated keyword/alias string,
3. a `~`-terminated multiline help body.

The section ends with `-1 $~`; the file ends with `#$`.

`help.are` contains general player help and is the strongest help source for world-wide concepts. Class help files and `skills.are` mix setting details with game rules. `olc.hlp` is builder documentation, not lore. A negative help level suppresses keyword display in-game; it does not mean the entry is inactive.

## Other possible lore sources

- `system/clans.txt`: current structured clan definitions (`Name`, `Motto`, `Descr`, ranks, members). Treat membership and counters as mutable runtime state. Clan-area `.are` files may contain richer player-facing lore.
- `system/lang.txt`: language transformation tables and names. Primarily implementation data, but useful for identifying language vocabulary and labels.
- `src/`: hard-coded races, classes, languages, regions, commands, messages, and tables. Search it when no data-file equivalent exists, then classify the result as structured or implementation evidence.
- Program sections in area files: dynamic dialogue, quests, ceremonies, environmental messages, and conditional exposition.
- `boards/`: player-authored or staff-authored posts. Use only as attributed, dated secondary evidence unless the user explicitly wants community history.

## Files that are not primary lore

- `system/help.txt`: log of failed or requested help lookups, not help article content.
- `system/raport.txt` and `system/default.dta`: generated reports/indexes; useful for discovery, not canonical prose.
- `system/odk.txt`, `system/regent_log.txt`, `log/`: administrative and runtime logs.
- `player/`: mutable player saves, not world canon.
- `doc/` and `docs/`: technical/design notes unless a specific document is proven to contain intended setting material.
- `src/docs/`: upstream license/credit documentation.
- `#RESETS`, shops, banks, repairs, flags, dice, costs, and combat stats: mechanics unless the research question specifically depends on them.

## Useful searches

Run searches from `tools/KillerMUD`:

```powershell
rg -n '^#(AREADATA|MOBILES|OBJECTS|ROOMS|HELPS)$' area-lore
rg -n '^#Vnum 6000$' area-lore -g '*.are'
rg -n '^(Name|Short|Long|Descr|Itemdesc|Nightdescr|Extra) ' area-lore -g '*.are'
rg -n -i 'histori|król|bóg|bogini|wiar|świąty|klan|wojn|traktat' area-lore -g '*.are'
```

For Polish terms containing non-ASCII characters, decode candidate files as ISO-8859-2 before text matching. Use ASCII stems with `rg` for initial discovery when possible.

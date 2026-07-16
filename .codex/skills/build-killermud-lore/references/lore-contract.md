# KillerMUD lore contract

## Canonical store

Use `lore/manifest.json` to discover every JSONL shard. Treat each nonblank line as one independent UTF-8 JSON record conforming to `lore/schema/lore-record.schema.json`.

Use stable lowercase IDs with a type prefix, for example:

- `place:arras`
- `organization:arras-city-guard`
- `deity:example`
- `artifact:example-sword`
- `event:example-war`
- `claim:arras:trade-center`
- `relation:arras:carrallak:road`
- `constraint:arras:no-open-necromancy`
- `style:arras`
- `article:arras`

Never reuse an ID for a different concept. Add aliases instead of changing an established identity.

When the source world changes, update the existing record with the same ID if the concept still has the same identity. Use `deprecated` for removed or superseded concepts, update every affected `sourceRef`, and rebuild dependent articles. Do not delete history merely to make the generated views look current.

## Record roles

- `entity`: place, organization, character, deity, culture, people, language, or artifact.
- `event`: historical occurrence with exact, relative, era-based, or unknown time.
- `narrative`: legend, myth, prophecy, belief, rumor, or folktale. Its existence can be certain even when its content is not factual.
- `claim`: one atomic assertion about a subject.
- `relation`: one typed edge between two records, including spatial and political relations.
- `constraint`: hard, soft, thematic, or unknown boundary relevant to future world building.
- `style-profile`: evidence-backed architecture, naming, themes, factions, religions, and elements to avoid.
- `source`: reusable source-file metadata.
- `article`: curated presentation record compiled for KillerMudClient.

## Presentation language

Keep schema identifiers and predicates stable, lowercase, and machine-facing. Write authored player-facing text in Polish and never derive a visible label by expanding an English identifier. Follow [presentation-language.md](presentation-language.md) for the complete field boundary and approved-predicate workflow.

## Evidence model

Keep these axes independent:

- `evidenceStatus`: `explicit`, `inferred`, or `disputed`.
- `truthStatus`: `accepted`, `rumor`, `belief`, `legend`, or `unknown`.
- `confidence`: `high`, `medium`, or `low` confidence that the extraction correctly represents the source.
- `status`: workflow state: `draft`, `extracted`, `reviewed`, `canonical`, or `deprecated`.

Do not interpret `confidence: high` as proof that a legend happened. It can mean the source clearly presents that legend.

## Source references

Point to the narrowest available location:

```json
{
  "file": "area/arras.are",
  "sourceTier": "active-player-facing",
  "section": "ROOMS",
  "vnum": 6000,
  "field": "Extra",
  "keyword": "tablica",
  "perspective": "inscription",
  "excerpt": "..."
}
```

Use excerpts only when they materially help review. Keep the path, section, vnum, field, and perspective whenever available.

## Geography and politics

Express topology and power as relations instead of prose-only summaries. Prefer predicates such as:

- `inside`, `north_of`, `south_of`, `east_of`, `west_of`, `adjacent_to`
- `connected_by_road`, `connected_by_river`
- `controls`, `claims`, `governs`, `trades_with`, `allied_with`, `at_war_with`
- `worships`, `opposes`, `founded_by`, `destroyed_by`

Use `mapReferences.areaFiles` and string `roomVnums` for connections to the implemented world. Do not invent coordinates when only relative placement is known.

## Future area-generation input

Record only evidence-backed constraints and styles in canonical lore. The read-only `$prepare-killermud-area-context` skill projects them into a self-contained file conforming to `lore/schema/world-context-pack.schema.json`.

The pack must contain an anchor, nearby entities, spatial relations, hard and soft constraints, themes, styles, relevant domain records, conflicts, open gaps, and provenance. It is input for a future area-building skill; it is not an area design or generated area.

## Application view

Write curated player-facing content as `article` records. Use:

- `visibility`: `public`, `discovered`, `secret`, or `builder-only`.
- `spoilerLevel`: `0` through `3`.
- domain-typed `sections`.
- `relatedIds` and `mapReferences` for navigation and map integration.

Compile only `reviewed` or `canonical` articles. Also include navigable `entity`, `event`, and `narrative` records in `extracted`, `reviewed`, or `canonical` state, their atomic facts, and relations whose endpoints are both navigable. This lets Killeropedia resolve references into clickable detail cards without loading canonical JSONL at runtime. The compiled `lore-catalog.json.gz` is replaceable build output.

Generate `build-manifest.json` alongside the catalog. It records the source commit, canonical and catalog hashes, counts, and output paths. KillerMudClient consumes the gzip catalog; it never parses Markdown or canonical JSONL at runtime.

## Human-readable view

Generate `lore/dist/markdown/` from the same reviewed/canonical `article` records. Use the article ID suffix as the stable filename, include resolved relationships, map references, and source provenance, and rebuild `index.md` on every export. The exporter tracks its own files in `.generated-files.json` so removed or renamed articles do not leave stale documentation.

Markdown is a replaceable review and browsing view, not a second authoring format. Update JSONL records first, then regenerate Markdown. Never merge manual Markdown edits back into canon.

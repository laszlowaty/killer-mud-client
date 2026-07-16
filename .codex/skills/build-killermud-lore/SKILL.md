---
name: build-killermud-lore
description: Extract, normalize, review, and compile the canonical KillerMUD world-lore dataset from the local server repository. Use when Codex must add or update source-backed lore records, relationships, constraints, style profiles, or application articles; distinguish facts from beliefs and legends; compile the application catalog; or generate and refresh the human-readable Markdown lore. This is the only KillerMUD lore skill allowed to modify the canonical lore dataset.
---

# Build KillerMUD Lore

Build the canonical dataset in `lore/` from sources in `tools/KillerMUD`. Do not generate MUD areas or `.are` content.

## Load the contracts

1. Read [references/source-map.md](references/source-map.md) before parsing KillerMUD files.
2. Read [references/lore-contract.md](references/lore-contract.md) before writing records.
3. Use `lore/schema/lore-record.schema.json` as the canonical record contract.
4. Preserve the source checkout commit in `lore/manifest.json`.

## Build in bounded batches

1. Choose one bounded subject: one area file, deity, city, organization, artifact family, or help topic.
2. Confirm active source files through `area/area.lst`.
3. Extract source-backed records with stable IDs and exact `sourceRefs`.
4. Store atomic assertions as `claim` or `relation` records. Do not bury important assertions only in prose.
5. Separate observable facts, beliefs, rumors, legends, and inference through `evidenceStatus` and `truthStatus`.
6. Add `constraint` and `style-profile` records only when supported by evidence. Mark uncertain gaps as `unknown`; do not invent missing canon.
7. Create or update an `article` record only after its underlying entities and claims exist.
8. Leave new records as `draft`, `extracted`, or `reviewed`; use `canonical` only after resolving conflicts or explicitly accepting them as competing accounts.
9. Check for duplicate IDs and malformed JSONL before finishing.
10. Regenerate all derived views after every accepted canonical change so existing application and Markdown content stays synchronized.

Validate the canonical store:

```powershell
python .codex/skills/build-killermud-lore/scripts/validate_lore.py --lore-root lore
```

## Refresh lore after world changes

1. Update the source checkout deliberately and record its new commit in `lore/manifest.json`.
2. Find affected canonical records through `sourceRefs.file`, `vnum`, `field`, and `keyword` instead of rebuilding unrelated lore.
3. Keep stable IDs for unchanged concepts. Update their assertions and provenance; mark removed or replaced concepts `deprecated` rather than silently reusing their IDs.
4. Re-evaluate dependent claims, relations, constraints, style profiles, and articles. Preserve competing accounts when the new source does not resolve them.
5. Validate JSONL, then regenerate the application catalog, build manifest, and Markdown. Existing Markdown filenames remain stable because they derive from article IDs.

## Refresh generated views

Build and validate every output with one command:

```powershell
python .codex/skills/build-killermud-lore/scripts/build_lore_outputs.py --lore-root lore
```

Use `--install-dir <path>` only when the user explicitly wants to copy `lore-catalog.json.gz` and `build-manifest.json` into a client data directory.

The unified build runs the following lower-level steps. Compile the application catalog alone only for focused diagnostics:

```powershell
python .codex/skills/build-killermud-lore/scripts/compile_lore_catalog.py --lore-root lore --output lore/dist/lore-catalog.json.gz
```

Generate or update the human-readable Markdown catalog:

```powershell
python .codex/skills/build-killermud-lore/scripts/export_lore_markdown.py --lore-root lore --output-dir lore/dist/markdown
```

The Markdown exporter updates existing article files, creates new ones, rebuilds `index.md`, and removes only stale files listed in its own `.generated-files.json`. Treat `lore/dist/lore-catalog.json.gz`, `lore/dist/build-manifest.json`, and `lore/dist/markdown/` as generated views. Never edit them directly or use them as the source of truth.

## Protect scope

- Do not modify `tools/KillerMUD` while extracting lore.
- Do not make builder comments, logs, or inactive areas canonical without an explicit reason.
- Do not turn a legend into history merely because the source states it confidently.
- Do not preserve manual edits in generated Markdown; put every lasting change into canonical records and regenerate the view.
- Do not generate a `WorldContextPack`; use `$prepare-killermud-area-context` for that read-only projection.
- Do not generate rooms, NPCs, objects, programs, vnum ranges, or area files.

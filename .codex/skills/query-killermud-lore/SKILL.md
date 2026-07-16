---
name: query-killermud-lore
description: Search and retrieve a bounded subset of the canonical KillerMUD lore dataset by ID, text, domain, record type, or relationship. Use when Codex needs precise lore facts, provenance, related entities, or source references without loading or modifying the full lore dataset.
---

# Query KillerMUD Lore

Query `lore/` read-only. Never edit JSONL records or infer new canon in this skill.

Run the bundled selector from the KillerMudClient root:

```powershell
python .codex/skills/query-killermud-lore/scripts/query_lore.py --lore-root lore --id place:arras --include-related --max-records 50
```

Useful filters:

```powershell
python .codex/skills/query-killermud-lore/scripts/query_lore.py --lore-root lore --domain religion --domain legends --text Lanseril --max-records 40
python .codex/skills/query-killermud-lore/scripts/query_lore.py --lore-root lore --record-type narrative --related-id artifact:example --max-records 40
```

Start with exact IDs or narrow domains. Increase `--max-records` only when the returned set is insufficient. Include record IDs and `sourceRefs` in the answer, and label unresolved conflicts instead of choosing a version silently.

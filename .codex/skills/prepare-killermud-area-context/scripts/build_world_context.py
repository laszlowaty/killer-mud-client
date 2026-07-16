#!/usr/bin/env python3
"""Build a self-contained WorldContextPack without generating a MUD area."""

from __future__ import annotations

import argparse
import json
import sys
from collections import deque
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Iterable


DEFAULT_DOMAINS = ("geography", "politics", "religion", "history", "legends", "artifacts", "peoples")


def load_records(lore_root: Path) -> list[dict[str, Any]]:
    manifest = json.loads((lore_root / "manifest.json").read_text(encoding="utf-8"))
    records: list[dict[str, Any]] = []
    seen: set[str] = set()
    for relative in manifest.get("recordFiles", []):
        path = lore_root / relative
        if not path.is_file():
            raise ValueError(f"Missing record file listed by manifest: {path}")
        for line_number, raw_line in enumerate(path.read_text(encoding="utf-8").splitlines(), 1):
            if not raw_line.strip():
                continue
            try:
                record = json.loads(raw_line)
            except json.JSONDecodeError as exc:
                raise ValueError(f"Invalid JSONL at {path}:{line_number}: {exc}") from exc
            record_id = record.get("id")
            if not isinstance(record_id, str) or not record_id:
                raise ValueError(f"Missing record id at {path}:{line_number}")
            if record_id in seen:
                raise ValueError(f"Duplicate lore record id: {record_id}")
            seen.add(record_id)
            records.append(record)
    return records


def relation_neighborhood(records: list[dict[str, Any]], anchor_id: str, radius: int) -> tuple[set[str], list[dict[str, Any]]]:
    relations = [record for record in records if record.get("recordType") == "relation"]
    adjacency: dict[str, list[tuple[str, dict[str, Any]]]] = {}
    for relation in relations:
        subject = relation.get("subjectId")
        target = relation.get("targetId")
        if isinstance(subject, str) and isinstance(target, str):
            adjacency.setdefault(subject, []).append((target, relation))
            adjacency.setdefault(target, []).append((subject, relation))

    found_ids = {anchor_id}
    found_relations: dict[str, dict[str, Any]] = {}
    queue: deque[tuple[str, int]] = deque([(anchor_id, 0)])
    while queue:
        current, depth = queue.popleft()
        if depth >= radius:
            continue
        for neighbor, relation in adjacency.get(current, []):
            found_relations[relation["id"]] = relation
            if neighbor not in found_ids:
                found_ids.add(neighbor)
                queue.append((neighbor, depth + 1))
    return found_ids, list(found_relations.values())


def intersects(record: dict[str, Any], entity_ids: set[str]) -> bool:
    if record.get("id") in entity_ids:
        return True
    for key in ("subjectId", "targetId", "entityId"):
        if record.get(key) in entity_ids:
            return True
    for key in ("participantIds", "locationIds", "relatedEntityIds", "relatedIds", "scopeIds", "commonFactionIds", "religiousInfluenceIds"):
        value = record.get(key, [])
        if isinstance(value, list) and entity_ids.intersection(value):
            return True
    return False


def unique_source_refs(records: Iterable[dict[str, Any]]) -> list[dict[str, Any]]:
    refs: list[dict[str, Any]] = []
    seen: set[str] = set()
    for record in records:
        for ref in record.get("sourceRefs", []):
            key = json.dumps(ref, ensure_ascii=False, sort_keys=True)
            if key not in seen:
                seen.add(key)
                refs.append(ref)
    return refs


def in_domain(record: dict[str, Any], domain: str) -> bool:
    return domain in record.get("domains", [])


def build_pack(records: list[dict[str, Any]], args: argparse.Namespace) -> dict[str, Any]:
    by_id = {record["id"]: record for record in records}
    anchor = by_id.get(args.anchor)
    if anchor is None:
        raise ValueError(f"Unknown anchor id: {args.anchor}")
    if anchor.get("recordType") != "entity":
        raise ValueError(f"Anchor must be an entity: {args.anchor}")

    entity_ids, relations = relation_neighborhood(records, args.anchor, args.radius)
    selected = [record for record in records if intersects(record, entity_ids)]
    selected_ids = {record["id"] for record in selected}
    for relation in relations:
        if relation["id"] not in selected_ids:
            selected.append(relation)
    if len(selected) > args.max_records:
        raise ValueError(
            f"Context would contain {len(selected)} records, above --max-records {args.max_records}; "
            "reduce --radius or narrow the anchor"
        )

    constraints = [record for record in selected if record.get("recordType") == "constraint"]
    styles = [record for record in selected if record.get("recordType") == "style-profile"]
    conflicts = [
        record for record in selected
        if record.get("recordType") == "claim" and record.get("evidenceStatus") == "disputed"
    ]
    gaps = [record for record in constraints if record.get("severity") == "unknown"]
    nearby = sorted(entity_id for entity_id in entity_ids if entity_id != args.anchor)
    region_ids = sorted(
        record["id"] for record in selected
        if record.get("recordType") == "entity" and record.get("entityType") in ("continent", "region")
    )
    map_refs = anchor.get("mapReferences", {})
    open_questions: list[str] = []
    if not relations:
        open_questions.append("Brak relacji przestrzennych łączących kotwicę z otoczeniem.")
    if not map_refs.get("roomVnums"):
        open_questions.append("Kotwica nie ma wskazanego room vnum do połączenia przyszłej krainy.")
    if not region_ids and not map_refs.get("regionId"):
        open_questions.append("Nie ustalono regionu nadrzędnego kotwicy.")
    if map_refs.get("regionId") and map_refs["regionId"] not in region_ids:
        region_ids.append(map_refs["regionId"])

    domains = tuple(args.domains or DEFAULT_DOMAINS)
    requested_domains = set(domains)

    def domain_records(domain: str) -> list[dict[str, Any]]:
        if domain not in requested_domains:
            return []
        return [record for record in selected if in_domain(record, domain)]

    pack = {
        "schemaVersion": 1,
        "generatedAt": args.generated_at or datetime.now(timezone.utc).isoformat(),
        "purpose": "generate-area",
        "anchor": {
            "entityId": args.anchor,
            "requestedPlacement": args.placement,
            "connectionRoomVnums": list(dict.fromkeys(args.connection_room_vnum or map_refs.get("roomVnums", []))),
        },
        "selection": {
            "domains": list(domains),
            "relationRadius": args.radius,
            "maxRecords": args.max_records,
            "recordCount": len(selected),
        },
        "placement": {
            "regionIds": region_ids,
            "nearEntityIds": nearby,
            "relations": relations,
            "openQuestions": open_questions,
        },
        "hardConstraints": [record for record in constraints if record.get("severity") == "hard"],
        "softConstraints": [record for record in constraints if record.get("severity") == "soft"],
        "themes": [record for record in constraints if record.get("severity") == "theme"],
        "styleProfiles": styles,
        "claims": [record for record in selected if record.get("recordType") == "claim"],
        "geography": domain_records("geography"),
        "politics": domain_records("politics"),
        "religion": domain_records("religion"),
        "history": domain_records("history"),
        "legends": domain_records("legends"),
        "artifacts": [
            record for record in selected
            if "artifacts" in requested_domains and (in_domain(record, "artifacts") or record.get("entityType") == "artifact")
        ],
        "peoples": domain_records("peoples"),
        "unresolvedConflicts": conflicts,
        "openLoreGaps": gaps,
        "sourceRefs": unique_source_refs(selected),
    }
    return pack


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--lore-root", type=Path, default=Path("lore"))
    parser.add_argument("--anchor", required=True)
    parser.add_argument("--placement")
    parser.add_argument("--connection-room-vnum", action="append")
    parser.add_argument("--radius", type=int, default=2)
    parser.add_argument("--max-records", type=int, default=250)
    parser.add_argument("--domains", nargs="+")
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--generated-at", help="Override ISO-8601 timestamp for reproducible tests")
    args = parser.parse_args()
    if args.radius < 0:
        parser.error("--radius must be non-negative")
    if args.max_records < 1:
        parser.error("--max-records must be positive")
    return args


def main() -> int:
    try:
        args = parse_args()
        pack = build_pack(load_records(args.lore_root), args)
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(json.dumps(pack, ensure_ascii=False, indent=2, sort_keys=True) + "\n", encoding="utf-8")
        return 0
    except (KeyError, OSError, ValueError, json.JSONDecodeError) as exc:
        print(f"build_world_context: {exc}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())

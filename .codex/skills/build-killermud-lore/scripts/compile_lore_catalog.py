#!/usr/bin/env python3
"""Compile KillerMUD lore articles and navigable records into an application catalog."""

from __future__ import annotations

import argparse
import gzip
import json
import sys
from datetime import datetime, timezone
from pathlib import Path
from typing import Any


VISIBLE_RECORD_STATUSES = {"extracted", "reviewed", "canonical"}
NAVIGABLE_RECORD_TYPES = {"entity", "event", "narrative"}


def load_records(lore_root: Path) -> list[dict[str, Any]]:
    manifest = json.loads((lore_root / "manifest.json").read_text(encoding="utf-8"))
    records: list[dict[str, Any]] = []
    for relative in manifest.get("recordFiles", []):
        path = lore_root / relative
        for line_number, raw_line in enumerate(path.read_text(encoding="utf-8").splitlines(), 1):
            if not raw_line.strip():
                continue
            try:
                records.append(json.loads(raw_line))
            except json.JSONDecodeError as exc:
                raise ValueError(f"Invalid JSONL at {path}:{line_number}: {exc}") from exc
    return records


def compile_articles(records: list[dict[str, Any]]) -> list[dict[str, Any]]:
    articles = []
    for record in records:
        if record.get("recordType") != "article":
            continue
        if record.get("status") not in ("reviewed", "canonical"):
            continue
        if record.get("visibility") == "builder-only":
            continue
        articles.append(
            {
                "id": record["id"],
                "entityId": record["entityId"],
                "category": record["category"],
                "title": record["title"],
                "summary": record["summary"],
                "sections": record["sections"],
                "relatedIds": record.get("relatedIds", []),
                "mapReferences": record["mapReferences"],
                "visibility": record["visibility"],
                "spoilerLevel": record["spoilerLevel"],
                "tags": record.get("tags", []),
            }
        )
    return sorted(articles, key=lambda item: (item["category"], item["title"].casefold(), item["id"]))


def compile_navigable_records(records: list[dict[str, Any]]) -> list[dict[str, Any]]:
    facts_by_subject: dict[str, list[dict[str, Any]]] = {}
    for record in records:
        if record.get("recordType") != "claim" or record.get("status") not in VISIBLE_RECORD_STATUSES:
            continue
        facts_by_subject.setdefault(record["subjectId"], []).append(
            {
                "id": record["id"],
                "predicate": record["predicate"],
                "value": record["value"],
                "evidenceStatus": record["evidenceStatus"],
                "truthStatus": record["truthStatus"],
                "confidence": record["confidence"],
            }
        )

    compiled = []
    for record in records:
        if record.get("recordType") not in NAVIGABLE_RECORD_TYPES:
            continue
        if record.get("status") not in VISIBLE_RECORD_STATUSES:
            continue
        if record.get("visibility") == "builder-only":
            continue
        related_ids = []
        for field in ("relatedEntityIds", "participantIds", "locationIds"):
            related_ids.extend(record.get(field, []))
        compiled.append(
            {
                "id": record["id"],
                "recordType": record["recordType"],
                "kind": record.get("entityType") or record.get("eventType") or record.get("narrativeType") or "other",
                "name": record["name"],
                "aliases": record.get("aliases", []),
                "summary": record["summary"],
                "description": record.get("description", ""),
                "domains": record.get("domains", []),
                "tags": record.get("tags", []),
                "status": record["status"],
                "truthStatus": record.get("truthStatus"),
                "time": record.get("time"),
                "mapReferences": record.get("mapReferences", {"areaFiles": [], "roomVnums": []}),
                "sourceRefs": record.get("sourceRefs", []),
                "relatedIds": list(dict.fromkeys(related_ids)),
                "facts": sorted(facts_by_subject.get(record["id"], []), key=lambda item: item["id"]),
            }
        )
    return sorted(compiled, key=lambda item: (item["recordType"], item["kind"], item["name"].casefold(), item["id"]))


def compile_relations(records: list[dict[str, Any]], navigable_ids: set[str]) -> list[dict[str, Any]]:
    relations = []
    for record in records:
        if record.get("recordType") != "relation" or record.get("status") not in VISIBLE_RECORD_STATUSES:
            continue
        if record["subjectId"] not in navigable_ids or record["targetId"] not in navigable_ids:
            continue
        relations.append(
            {
                "id": record["id"],
                "subjectId": record["subjectId"],
                "predicate": record["predicate"],
                "targetId": record["targetId"],
                "direction": record.get("direction"),
                "evidenceStatus": record.get("evidenceStatus"),
                "truthStatus": record.get("truthStatus"),
                "confidence": record["confidence"],
            }
        )
    return sorted(relations, key=lambda item: item["id"])


def build_catalog(lore_root: Path, generated_at: str) -> dict[str, Any]:
    records = load_records(lore_root)
    entries = compile_articles(records)
    navigable_records = compile_navigable_records(records)
    navigable_ids = {record["id"] for record in navigable_records}
    return {
        "schemaVersion": 1,
        "generatedAt": generated_at,
        "entries": entries,
        "records": navigable_records,
        "relations": compile_relations(records, navigable_ids),
    }


def write_catalog(catalog: dict[str, Any], output: Path) -> None:
    output.parent.mkdir(parents=True, exist_ok=True)
    payload = (json.dumps(catalog, ensure_ascii=False, separators=(",", ":"), sort_keys=True) + "\n").encode("utf-8")
    if output.suffix == ".gz":
        with output.open("wb") as raw:
            with gzip.GzipFile(filename="", mode="wb", fileobj=raw, mtime=0) as compressed:
                compressed.write(payload)
    else:
        output.write_bytes(payload)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--lore-root", type=Path, default=Path("lore"))
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--generated-at", help="Override ISO-8601 timestamp for reproducible tests")
    return parser.parse_args()


def main() -> int:
    try:
        args = parse_args()
        generated_at = args.generated_at or datetime.now(timezone.utc).isoformat()
        write_catalog(build_catalog(args.lore_root, generated_at), args.output)
        return 0
    except (KeyError, OSError, ValueError, json.JSONDecodeError) as exc:
        print(f"compile_lore_catalog: {exc}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())

#!/usr/bin/env python3
"""Select a bounded subset of the canonical KillerMUD lore JSONL dataset."""

from __future__ import annotations

import argparse
import json
import sys
import unicodedata
from pathlib import Path
from typing import Any, Iterable


def load_records(lore_root: Path) -> list[dict[str, Any]]:
    manifest_path = lore_root / "manifest.json"
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    records: list[dict[str, Any]] = []
    seen_ids: set[str] = set()
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
            if record_id in seen_ids:
                raise ValueError(f"Duplicate lore record id: {record_id}")
            seen_ids.add(record_id)
            records.append(record)
    return records


def normalize(value: str) -> str:
    decomposed = unicodedata.normalize("NFKD", value.casefold())
    return "".join(char for char in decomposed if not unicodedata.combining(char))


def searchable_text(record: dict[str, Any]) -> str:
    return normalize(json.dumps(record, ensure_ascii=False, sort_keys=True))


def references_id(record: dict[str, Any], record_id: str) -> bool:
    for key in (
        "subjectId", "targetId", "entityId", "participantIds", "locationIds",
        "relatedEntityIds", "relatedIds", "scopeIds", "commonFactionIds",
        "religiousInfluenceIds", "claimIds",
    ):
        value = record.get(key)
        if value == record_id or isinstance(value, list) and record_id in value:
            return True
    return False


def select_records(records: list[dict[str, Any]], args: argparse.Namespace) -> list[dict[str, Any]]:
    text = normalize(args.text) if args.text else None
    selected: list[dict[str, Any]] = []
    for record in records:
        if args.id and record.get("id") != args.id:
            continue
        if args.record_type and record.get("recordType") != args.record_type:
            continue
        if args.domain and not set(args.domain).intersection(record.get("domains", [])):
            continue
        if args.related_id and not references_id(record, args.related_id) and record.get("id") != args.related_id:
            continue
        if text and text not in searchable_text(record):
            continue
        selected.append(record)

    if not args.include_related or not selected:
        return selected[: args.max_records]

    selected_ids = {record["id"] for record in selected}
    related_ids = set(selected_ids)
    for record in records:
        if record.get("recordType") == "relation":
            subject = record.get("subjectId")
            target = record.get("targetId")
            if subject in selected_ids or target in selected_ids:
                related_ids.update(value for value in (subject, target) if isinstance(value, str))

    expanded = list(selected)
    expanded_ids = set(selected_ids)
    for record in records:
        record_id = record["id"]
        if record_id in expanded_ids:
            continue
        if record_id in related_ids or any(references_id(record, related_id) for related_id in related_ids):
            expanded.append(record)
            expanded_ids.add(record_id)
        if len(expanded) >= args.max_records:
            break
    return expanded[: args.max_records]


def write_result(records: Iterable[dict[str, Any]], args: argparse.Namespace) -> None:
    records = list(records)
    if args.format == "jsonl":
        content = "".join(json.dumps(record, ensure_ascii=False, sort_keys=True) + "\n" for record in records)
    else:
        content = json.dumps(
            {
                "query": {
                    "id": args.id,
                    "recordType": args.record_type,
                    "domain": args.domain,
                    "text": args.text,
                    "relatedId": args.related_id,
                    "includeRelated": args.include_related,
                    "maxRecords": args.max_records,
                },
                "count": len(records),
                "records": records,
            },
            ensure_ascii=False,
            indent=2,
            sort_keys=True,
        ) + "\n"
    if args.output:
        output = Path(args.output)
        output.parent.mkdir(parents=True, exist_ok=True)
        output.write_text(content, encoding="utf-8")
    else:
        sys.stdout.write(content)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--lore-root", type=Path, default=Path("lore"))
    parser.add_argument("--id")
    parser.add_argument("--record-type")
    parser.add_argument("--domain", action="append", help="Repeat to match any selected domain")
    parser.add_argument("--text")
    parser.add_argument("--related-id")
    parser.add_argument("--include-related", action="store_true")
    parser.add_argument("--max-records", type=int, default=50)
    parser.add_argument("--format", choices=("json", "jsonl"), default="json")
    parser.add_argument("--output")
    args = parser.parse_args()
    if args.max_records < 1:
        parser.error("--max-records must be positive")
    if not any((args.id, args.record_type, args.domain, args.text, args.related_id)):
        parser.error("provide at least one bounded filter")
    return args


def main() -> int:
    try:
        args = parse_args()
        records = load_records(args.lore_root)
        write_result(select_records(records, args), args)
        return 0
    except (OSError, ValueError, json.JSONDecodeError) as exc:
        print(f"query_lore: {exc}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())

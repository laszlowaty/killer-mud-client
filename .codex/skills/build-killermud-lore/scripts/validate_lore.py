#!/usr/bin/env python3
"""Validate the structural contract of canonical KillerMUD lore JSONL."""

from __future__ import annotations

import argparse
import json
import re
import sys
from pathlib import Path
from typing import Any


ALLOWED_TYPES = {"entity", "event", "narrative", "claim", "relation", "constraint", "style-profile", "source", "article"}
ALLOWED_STATUSES = {"draft", "extracted", "reviewed", "canonical", "deprecated"}
ID_PATTERN = re.compile(r"^[a-z0-9][a-z0-9:-]*$")
REQUIRED_BY_TYPE = {
    "entity": {"name", "entityType", "domains", "summary", "sourceRefs"},
    "event": {"name", "eventType", "domains", "summary", "time", "sourceRefs"},
    "narrative": {"name", "narrativeType", "truthStatus", "summary", "relatedEntityIds", "sourceRefs"},
    "claim": {"subjectId", "predicate", "value", "evidenceStatus", "truthStatus", "confidence", "sourceRefs"},
    "relation": {"subjectId", "predicate", "targetId", "confidence", "sourceRefs"},
    "constraint": {"scopeIds", "severity", "rule", "sourceRefs"},
    "style-profile": {"name", "scopeIds", "sourceRefs"},
    "source": {"path", "sourceTier", "active", "encoding"},
    "article": {"entityId", "category", "title", "summary", "sections", "relatedIds", "visibility", "spoilerLevel", "mapReferences"},
}


def require(record: dict[str, Any], fields: set[str], location: str) -> list[str]:
    return [f"{location}: missing required field '{field}'" for field in sorted(fields) if field not in record]


def validate_record(record: Any, location: str) -> list[str]:
    if not isinstance(record, dict):
        return [f"{location}: record must be an object"]
    errors = require(record, {"schemaVersion", "recordType", "id", "status"}, location)
    if errors:
        return errors
    if record["schemaVersion"] != 1:
        errors.append(f"{location}: schemaVersion must be 1")
    record_type = record["recordType"]
    if record_type not in ALLOWED_TYPES:
        errors.append(f"{location}: unsupported recordType '{record_type}'")
        return errors
    record_id = record["id"]
    if not isinstance(record_id, str) or not ID_PATTERN.fullmatch(record_id):
        errors.append(f"{location}: invalid id '{record_id}'")
    if record["status"] not in ALLOWED_STATUSES:
        errors.append(f"{location}: invalid status '{record['status']}'")
    errors.extend(require(record, REQUIRED_BY_TYPE[record_type], location))
    for index, source_ref in enumerate(record.get("sourceRefs", [])):
        if not isinstance(source_ref, dict):
            errors.append(f"{location}: sourceRefs[{index}] must be an object")
        else:
            errors.extend(require(source_ref, {"file", "sourceTier"}, f"{location}:sourceRefs[{index}]"))
    return errors


def validate_lore(lore_root: Path) -> tuple[int, list[str]]:
    errors: list[str] = []
    manifest_path = lore_root / "manifest.json"
    try:
        manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as exc:
        return 0, [f"{manifest_path}: {exc}"]
    if manifest.get("schemaVersion") != 1:
        errors.append(f"{manifest_path}: schemaVersion must be 1")
    seen_ids: dict[str, str] = {}
    count = 0
    for relative in manifest.get("recordFiles", []):
        path = lore_root / relative
        if not path.is_file():
            errors.append(f"{path}: missing record file listed by manifest")
            continue
        for line_number, raw_line in enumerate(path.read_text(encoding="utf-8").splitlines(), 1):
            if not raw_line.strip():
                continue
            location = f"{path}:{line_number}"
            try:
                record = json.loads(raw_line)
            except json.JSONDecodeError as exc:
                errors.append(f"{location}: invalid JSON: {exc}")
                continue
            count += 1
            errors.extend(validate_record(record, location))
            if isinstance(record, dict) and isinstance(record.get("id"), str):
                record_id = record["id"]
                if record_id in seen_ids:
                    errors.append(f"{location}: duplicate id '{record_id}', first seen at {seen_ids[record_id]}")
                else:
                    seen_ids[record_id] = location
    for relative in ("schema/lore-record.schema.json", "schema/lore-catalog.schema.json", "schema/world-context-pack.schema.json"):
        path = lore_root / relative
        try:
            json.loads(path.read_text(encoding="utf-8"))
        except (OSError, json.JSONDecodeError) as exc:
            errors.append(f"{path}: {exc}")
    return count, errors


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--lore-root", type=Path, default=Path("lore"))
    args = parser.parse_args()
    count, errors = validate_lore(args.lore_root)
    if errors:
        for error in errors:
            print(error, file=sys.stderr)
        print(f"Lore validation failed with {len(errors)} error(s).", file=sys.stderr)
        return 1
    print(f"Lore is valid: {count} record(s).")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

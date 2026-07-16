#!/usr/bin/env python3
"""Validate lore and build every human- and application-facing output."""

from __future__ import annotations

import argparse
import hashlib
import json
import shutil
import sys
from datetime import datetime, timezone
from pathlib import Path

from compile_lore_catalog import build_catalog, write_catalog
from export_lore_markdown import export_markdown
from validate_lore import validate_lore


def canonical_hash(lore_root: Path) -> str:
    manifest_path = lore_root / "manifest.json"
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    digest = hashlib.sha256()
    for path in [manifest_path, *(lore_root / relative for relative in manifest.get("recordFiles", []))]:
        digest.update(path.relative_to(lore_root).as_posix().encode("utf-8"))
        digest.update(b"\0")
        digest.update(path.read_bytes())
        digest.update(b"\0")
    return digest.hexdigest()


def write_manifest(path: Path, payload: dict[str, object]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_name(f".{path.name}.tmp")
    temporary.write_text(json.dumps(payload, ensure_ascii=False, indent=2, sort_keys=True) + "\n", encoding="utf-8", newline="\n")
    temporary.replace(path)


def build_outputs(
    lore_root: Path,
    output_dir: Path,
    generated_at: str,
    install_dir: Path | None = None,
) -> dict[str, object]:
    record_count, errors = validate_lore(lore_root)
    if errors:
        raise ValueError("Lore validation failed:\n" + "\n".join(errors))

    output_dir.mkdir(parents=True, exist_ok=True)
    catalog_path = output_dir / "lore-catalog.json.gz"
    catalog = build_catalog(lore_root, generated_at)
    write_catalog(catalog, catalog_path)
    article_count, stale_count = export_markdown(lore_root, output_dir / "markdown")

    source_manifest = json.loads((lore_root / "manifest.json").read_text(encoding="utf-8"))
    build_manifest = {
        "schemaVersion": 1,
        "generatedAt": generated_at,
        "datasetId": source_manifest.get("datasetId"),
        "sourceCommit": source_manifest.get("sourceRepository", {}).get("commit"),
        "canonicalSha256": canonical_hash(lore_root),
        "catalogSha256": hashlib.sha256(catalog_path.read_bytes()).hexdigest(),
        "counts": {
            "canonicalRecords": record_count,
            "articles": len(catalog["entries"]),
            "navigableRecords": len(catalog["records"]),
            "relations": len(catalog["relations"]),
            "markdownArticles": article_count,
            "staleMarkdownRemoved": stale_count,
        },
        "files": {
            "applicationCatalog": "lore-catalog.json.gz",
            "humanReadableIndex": "markdown/index.md",
        },
    }
    manifest_path = output_dir / "build-manifest.json"
    write_manifest(manifest_path, build_manifest)

    if install_dir is not None:
        install_dir.mkdir(parents=True, exist_ok=True)
        shutil.copy2(catalog_path, install_dir / catalog_path.name)
        shutil.copy2(manifest_path, install_dir / manifest_path.name)

    return build_manifest


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--lore-root", type=Path, default=Path("lore"))
    parser.add_argument("--output-dir", type=Path, default=Path("lore/dist"))
    parser.add_argument("--install-dir", type=Path, help="Optionally copy runtime files to a KillerMudClient Data directory")
    parser.add_argument("--generated-at", help="Override ISO-8601 timestamp for reproducible tests")
    return parser.parse_args()


def main() -> int:
    try:
        args = parse_args()
        generated_at = args.generated_at or datetime.now(timezone.utc).isoformat()
        manifest = build_outputs(args.lore_root, args.output_dir, generated_at, args.install_dir)
        counts = manifest["counts"]
        print(
            "Lore outputs built: "
            f"{counts['canonicalRecords']} canonical record(s), "
            f"{counts['navigableRecords']} navigable record(s), "
            f"{counts['articles']} article(s), "
            f"{counts['relations']} relation(s)."
        )
        return 0
    except (KeyError, OSError, ValueError, json.JSONDecodeError) as exc:
        print(f"build_lore_outputs: {exc}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())

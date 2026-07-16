from __future__ import annotations

import importlib.util
import json
import tempfile
import unittest
from pathlib import Path


SCRIPT_PATH = Path(__file__).parents[1] / "scripts" / "export_lore_markdown.py"
SPEC = importlib.util.spec_from_file_location("export_lore_markdown", SCRIPT_PATH)
assert SPEC is not None and SPEC.loader is not None
EXPORTER = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(EXPORTER)


def entity(record_id: str, name: str) -> dict[str, object]:
    return {
        "schemaVersion": 1,
        "recordType": "entity",
        "id": record_id,
        "status": "reviewed",
        "name": name,
        "entityType": "city",
        "domains": ["geography"],
        "summary": f"Opis {name}",
        "sourceRefs": [],
    }


def article(article_id: str, entity_id: str, title: str, related_ids: list[str]) -> dict[str, object]:
    return {
        "schemaVersion": 1,
        "recordType": "article",
        "id": article_id,
        "status": "reviewed",
        "entityId": entity_id,
        "category": "city",
        "title": title,
        "summary": f"Podsumowanie {title}",
        "sections": [{"type": "overview", "title": "Opis", "content": f"Treść {title}", "sourceRefs": []}],
        "relatedIds": related_ids,
        "visibility": "public",
        "spoilerLevel": 0,
        "mapReferences": {"areaFiles": [], "roomVnums": []},
    }


class ExportLoreMarkdownTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temporary = tempfile.TemporaryDirectory()
        self.root = Path(self.temporary.name)
        self.lore_root = self.root / "lore"
        self.output_dir = self.lore_root / "dist" / "markdown"
        self.lore_root.mkdir(parents=True)
        (self.lore_root / "manifest.json").write_text(
            json.dumps({"schemaVersion": 1, "recordFiles": ["records.jsonl"]}),
            encoding="utf-8",
        )

    def tearDown(self) -> None:
        self.temporary.cleanup()

    def write_records(self, records: list[dict[str, object]]) -> None:
        payload = "".join(json.dumps(record, ensure_ascii=False) + "\n" for record in records)
        (self.lore_root / "records.jsonl").write_text(payload, encoding="utf-8", newline="\n")

    def test_second_export_is_byte_for_byte_identical(self) -> None:
        self.write_records([entity("place:a", "Miasto A"), article("article:a", "place:a", "Miasto A", [])])

        EXPORTER.export_markdown(self.lore_root, self.output_dir)
        first = {path.name: path.read_bytes() for path in self.output_dir.iterdir()}
        EXPORTER.export_markdown(self.lore_root, self.output_dir)
        second = {path.name: path.read_bytes() for path in self.output_dir.iterdir()}

        self.assertEqual(first, second)

    def test_update_removes_only_tracked_stale_files(self) -> None:
        records = [
            entity("place:a", "Miasto A"),
            entity("place:b", "Miasto B"),
            article("article:a", "place:a", "Miasto A", ["place:b"]),
            article("article:b", "place:b", "Miasto B", []),
        ]
        self.write_records(records)
        EXPORTER.export_markdown(self.lore_root, self.output_dir)
        (self.output_dir / "manual.md").write_text("Ręczna notatka\n", encoding="utf-8")

        self.write_records(records[:-1])
        article_count, stale_count = EXPORTER.export_markdown(self.lore_root, self.output_dir)

        self.assertEqual(1, article_count)
        self.assertEqual(1, stale_count)
        self.assertFalse((self.output_dir / "b.md").exists())
        self.assertEqual("Ręczna notatka\n", (self.output_dir / "manual.md").read_text(encoding="utf-8"))


if __name__ == "__main__":
    unittest.main()

from __future__ import annotations

import importlib.util
import unittest
from pathlib import Path


SCRIPT_PATH = Path(__file__).parents[1] / "scripts" / "validate_lore.py"
SPEC = importlib.util.spec_from_file_location("validate_lore", SCRIPT_PATH)
assert SPEC is not None and SPEC.loader is not None
VALIDATOR = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(VALIDATOR)


class ValidateLoreTests(unittest.TestCase):
    def test_rejects_claim_predicate_without_polish_presentation_label(self) -> None:
        record = {
            "schemaVersion": 1,
            "recordType": "claim",
            "id": "claim:test:new-fact",
            "status": "reviewed",
            "subjectId": "place:test",
            "predicate": "new_english_fact",
            "value": "Polska treść",
            "evidenceStatus": "explicit",
            "truthStatus": "accepted",
            "confidence": "high",
            "sourceRefs": [],
        }

        errors = VALIDATOR.validate_record(record, "record.jsonl:1")

        self.assertTrue(any("explicit Polish Killeropedia label" in error for error in errors))

    def test_rejects_relation_predicate_without_two_polish_labels(self) -> None:
        record = {
            "schemaVersion": 1,
            "recordType": "relation",
            "id": "relation:test:new-edge",
            "status": "reviewed",
            "subjectId": "place:test",
            "predicate": "new_english_relation",
            "targetId": "place:target",
            "confidence": "high",
            "sourceRefs": [],
        }

        errors = VALIDATOR.validate_record(record, "record.jsonl:1")

        self.assertTrue(any("forward and inverse Killeropedia labels" in error for error in errors))

    def test_rejects_article_category_without_polish_labels(self) -> None:
        record = {
            "schemaVersion": 1,
            "recordType": "article",
            "id": "article:test",
            "status": "reviewed",
            "entityId": "place:test",
            "category": "english_category",
            "title": "Test",
            "summary": "Polskie podsumowanie.",
            "sections": [],
            "relatedIds": [],
            "visibility": "public",
            "spoilerLevel": 0,
            "mapReferences": {"areaFiles": [], "roomVnums": []},
        }

        errors = VALIDATOR.validate_record(record, "record.jsonl:1")

        self.assertTrue(any("Polish Markdown and Killeropedia labels" in error for error in errors))


if __name__ == "__main__":
    unittest.main()

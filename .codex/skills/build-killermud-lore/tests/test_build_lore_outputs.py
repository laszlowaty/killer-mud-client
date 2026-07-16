from __future__ import annotations

import gzip
import json
import sys
import tempfile
import unittest
from pathlib import Path


SKILL_ROOT = Path(__file__).parents[1]
REPOSITORY_ROOT = SKILL_ROOT.parents[2]
sys.path.insert(0, str(SKILL_ROOT / "scripts"))

from build_lore_outputs import build_outputs  # noqa: E402


class BuildLoreOutputsTests(unittest.TestCase):
    def test_builds_catalog_markdown_and_manifest_from_canonical_store(self) -> None:
        generated_at = "2026-07-16T00:00:00Z"
        with tempfile.TemporaryDirectory() as temporary:
            output_dir = Path(temporary) / "dist"

            manifest = build_outputs(
                REPOSITORY_ROOT / "lore",
                output_dir,
                generated_at,
            )

            catalog_path = output_dir / "lore-catalog.json.gz"
            with gzip.open(catalog_path, "rt", encoding="utf-8") as stream:
                catalog = json.load(stream)

            self.assertEqual(1, catalog["schemaVersion"])
            self.assertEqual(generated_at, catalog["generatedAt"])
            self.assertGreater(len(catalog["records"]), 0)
            self.assertGreater(len(catalog["entries"]), 0)
            self.assertEqual(len(catalog["records"]), manifest["counts"]["navigableRecords"])
            self.assertTrue((output_dir / "markdown" / "index.md").is_file())
            self.assertTrue((output_dir / "build-manifest.json").is_file())


if __name__ == "__main__":
    unittest.main()

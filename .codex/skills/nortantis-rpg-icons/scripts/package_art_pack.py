#!/usr/bin/env python3
"""Validate a prepared icon tree and package it as a Nortantis art pack."""

from __future__ import annotations

import argparse
import re
import sys
import zipfile
from pathlib import Path, PurePosixPath

from PIL import Image


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Package transparent PNG icons into a Nortantis-compatible ZIP."
    )
    parser.add_argument("source", type=Path, help="Directory containing category folders and PNG files")
    parser.add_argument("output", type=Path, help="Destination ZIP file")
    parser.add_argument("--pack-name", required=True, help="Single top-level folder name inside the ZIP")
    parser.add_argument("--required-version", default="3.16", help="Nortantis requiredVersion value")
    return parser.parse_args()


def validate_name(pack_name: str) -> None:
    if not pack_name.strip() or pack_name in {".", ".."}:
        raise ValueError("Pack name must not be empty.")
    if "/" in pack_name or "\\" in pack_name:
        raise ValueError("Pack name must be a single folder name without path separators.")


def validate_png(path: Path) -> None:
    with Image.open(path) as image:
        image.load()
        if image.format != "PNG":
            raise ValueError(f"Not a PNG file: {path}")
        if "A" not in image.getbands():
            raise ValueError(f"Icon has no alpha channel: {path}")
        alpha = image.getchannel("A")
        if alpha.getbbox() is None:
            raise ValueError(f"Icon is fully transparent: {path}")

        rgba = image.convert("RGBA")
        visible_colors = {
            (r, g, b)
            for r, g, b, a in rgba.getdata()
            if a > 0
        }
        if any(color != (0, 0, 0) for color in visible_colors):
            raise ValueError(f"Visible pixels must use pure black RGB: {path}")


def collect_files(source: Path) -> list[Path]:
    if not source.is_dir():
        raise ValueError(f"Source directory does not exist: {source}")

    all_files = sorted(path for path in source.rglob("*") if path.is_file())
    files = [path for path in all_files if path.name.lower() != "settings.txt"]
    if not files:
        raise ValueError("Source directory is empty.")

    for path in all_files:
        if path.name.lower() == "settings.txt":
            continue
        if path.suffix.lower() != ".png":
            raise ValueError(f"Only PNG icons are allowed in the source tree: {path}")
        relative = path.relative_to(source)
        if len(relative.parts) != 3:
            raise ValueError(
                "Nortantis icons must use exactly type/group/icon.png; "
                f"unsupported nested path: {relative}"
            )
        if not re.fullmatch(r"[a-z0-9_]+\.png", path.name):
            raise ValueError(f"Use snake_case ASCII PNG filenames: {path.name}")
        validate_png(path)
    return files


def directory_entries(source: Path, files: list[Path], root: str) -> list[str]:
    directories = {PurePosixPath(root)}
    for path in files:
        relative = PurePosixPath(path.relative_to(source).as_posix())
        parent = PurePosixPath(root) / relative.parent
        while str(parent) not in {".", root}:
            directories.add(parent)
            parent = parent.parent
    return sorted(f"{directory.as_posix().rstrip('/')}/" for directory in directories)


def package(source: Path, output: Path, pack_name: str, required_version: str) -> None:
    validate_name(pack_name)
    files = collect_files(source)
    output.parent.mkdir(parents=True, exist_ok=True)

    with zipfile.ZipFile(output, "w", compression=zipfile.ZIP_DEFLATED, compresslevel=9) as archive:
        for entry in directory_entries(source, files, pack_name):
            archive.writestr(entry, b"")
        for path in files:
            relative = PurePosixPath(path.relative_to(source).as_posix())
            archive.write(path, (PurePosixPath(pack_name) / relative).as_posix())
        archive.writestr(
            (PurePosixPath(pack_name) / "settings.txt").as_posix(),
            f"requiredVersion={required_version}\r\n".encode("ascii"),
        )

    with zipfile.ZipFile(output) as archive:
        roots = {PurePosixPath(name).parts[0] for name in archive.namelist() if name}
        if roots != {pack_name}:
            raise RuntimeError(f"Generated archive has invalid roots: {sorted(roots)}")
        if f"{pack_name}/" not in archive.namelist():
            raise RuntimeError("Generated archive has no explicit top-level directory entry.")


def main() -> int:
    args = parse_args()
    try:
        package(args.source.resolve(), args.output.resolve(), args.pack_name.strip(), args.required_version)
    except (OSError, ValueError, RuntimeError, zipfile.BadZipFile) as error:
        print(f"error: {error}", file=sys.stderr)
        return 1

    print(f"Created Nortantis art pack: {args.output.resolve()}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

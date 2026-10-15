#!/usr/bin/env python3
"""OGL .NET 10: manifest, verify and stage an OFFLINE backup without touching originals.

This is an integrity/byte-for-byte restore rehearsal, NOT a database
recovery, legacy BinaryFormatter converter, or live simulator restart.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path, PurePosixPath
import shutil
import sys

VERSION = 1
MAX_FILES = 200000

def digest(path):
    h = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            h.update(block)
    return h.hexdigest()

def inside(path, parent):
    return path == parent or parent in path.parents

def check_root(root):
    root = root.absolute()
    if not root.is_dir() or root.is_symlink():
        raise ValueError("An existing, non-symlink snapshot directory is required")
    return root

def list_files(root):
    paths = []
    for current, dirs, files in os.walk(root, followlinks=False):
        for name in dirs + files:
            path = Path(current, name)
            if path.is_symlink():
                raise ValueError("Symlink in backup snapshot: " + str(path))
        for name in files:
            path = Path(current, name)
            if not path.is_file():
                raise ValueError("Nonregular backup entry: " + str(path))
            paths.append(path)
            if len(paths) > MAX_FILES:
                raise ValueError("Too many files in backup snapshot")
    return sorted(paths)

def manifest(root):
    files = []
    for p in list_files(root):
        relative = p.relative_to(root).as_posix()
        files.append({"path": relative, "bytes": p.stat().st_size, "sha256": digest(p)})
    if not files:
        raise ValueError("Backup snapshot is empty")
    return {"schema": VERSION, "file_count": len(files), "files": files}

def validated_entries(doc):
    if not isinstance(doc, dict) or doc.get("schema") != VERSION:
        raise ValueError("Unsupported backup manifest schema")
    entries = doc.get("files")
    if not isinstance(entries, list) or not entries or len(entries) > MAX_FILES:
        raise ValueError("Invalid backup manifest file count")
    if doc.get("file_count") != len(entries):
        raise ValueError("Manifest count mismatch")
    visited = set()
    for entry in entries:
        if not isinstance(entry, dict):
            raise ValueError("Invalid manifest record")
        raw = entry.get("path")
        if not isinstance(raw, str) or not raw or "\\" in raw or raw.startswith("/"):
            raise ValueError("Invalid manifest path")
        parts = PurePosixPath(raw).parts
        if any(p in ("", ".", "..") for p in parts) or PurePosixPath(raw).as_posix() != raw:
            raise ValueError("Manifest path traversal")
        if raw in visited:
            raise ValueError("Duplicate manifest entry")
        visited.add(raw)
        if not isinstance(entry.get("bytes"), int) or isinstance(entry["bytes"], bool) or entry["bytes"] < 0:
            raise ValueError("Invalid manifest file size")
        checksum = entry.get("sha256")
        if not isinstance(checksum, str) or len(checksum) != 64 or any(c not in "0123456789abcdef" for c in checksum):
            raise ValueError("Invalid SHA-256 in manifest")
    return entries

def verify(root, doc):
    entries = validated_entries(doc)
    actual = list_files(root)
    listed = {e["path"] for e in entries}
    found = {p.relative_to(root).as_posix() for p in actual}
    if listed != found:
        raise ValueError("Backup file set changed: missing=" + str(sorted(listed-found)[:5]) +
                         " extra=" + str(sorted(found-listed)[:5]))
    for entry in entries:
        path = root.joinpath(*PurePosixPath(entry["path"]).parts)
        if path.stat().st_size != entry["bytes"] or digest(path) != entry["sha256"]:
            raise ValueError("Backup checksum mismatch: " + entry["path"])
    return len(entries)

def load(path):
    if path.is_symlink():
        raise ValueError("Manifest symlink is not permitted")
    return json.loads(path.read_text(encoding="utf-8"))

def write_manifest(root, output):
    if inside(output.absolute(), root):
        raise ValueError("Manifest must be written outside the backup snapshot")
    if output.exists() or output.is_symlink():
        raise ValueError("Refusing to overwrite an existing manifest")
    doc = manifest(root)
    with output.open("x", encoding="utf-8") as stream:
        json.dump(doc, stream, indent=2, sort_keys=True)
        stream.write("\n")
    print(f"Created immutable-source manifest: {len(doc['files'])} files")
    return 0

def stage(root, doc, destination):
    dest = destination.absolute()
    if inside(dest, root) or inside(root, dest):
        raise ValueError("Staging destination must be separate from snapshot source")
    if dest.exists() or dest.is_symlink():
        raise ValueError("Staging destination must not exist (no overwrites)")
    count = verify(root, doc)  # verify the source *before* touching the destination
    if dest.parent.is_symlink() or not dest.parent.is_dir():
        raise ValueError("Staging parent must exist and must not be a symlink")
    dest.mkdir(mode=0o700)
    try:
        for entry in validated_entries(doc):
            parts = PurePosixPath(entry["path"]).parts
            source = root.joinpath(*parts)
            target = dest.joinpath(*parts)
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(source, target, follow_symlinks=False)
        verify(dest, doc)
    except Exception:
        # Keep the partial stage for investigation; never touch the source.
        print("Stage incomplete; remove the NEW destination only after inspection", file=sys.stderr)
        raise
    print(f"Byte-identical isolated restore staging: PASS ({count} files)")
    return 0

def main(args=None):
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest="command", required=True)
    for name in ("manifest", "verify", "stage"):
        cmd = commands.add_parser(name)
        cmd.add_argument("--snapshot", type=Path, required=True)
        cmd.add_argument("--manifest", type=Path, required=True)
        if name == "stage":
            cmd.add_argument("--destination", type=Path, required=True)
    options = parser.parse_args(args)
    root = check_root(options.snapshot)
    if options.command == "manifest":
        return write_manifest(root, options.manifest)
    doc = load(options.manifest)
    if options.command == "verify":
        count = verify(root, doc)
        print(f"Backup integrity and complete file inventory: PASS ({count} files)")
        return 0
    return stage(root, doc, options.destination)

if __name__ == "__main__":
    try:
        sys.exit(main())
    except (OSError, ValueError, json.JSONDecodeError) as error:
        print("Backup gate FAIL: " + str(error), file=sys.stderr)
        sys.exit(2)

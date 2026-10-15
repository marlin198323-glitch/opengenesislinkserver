#!/usr/bin/env python3
"""Read-only inventory of KeyframeMotion encoding before an OpenGenesisLINK .NET 10 upgrade.

Accepts SQLite region-store snapshots and OAR files. Never deserializes
BinaryFormatter and never writes to the supplied data. This is an *audit*
tool; legacy data still needs a separate, tested offline migration.
"""
from __future__ import annotations

import argparse
import base64
import binascii
from collections import Counter
import json
import math
import struct
from pathlib import Path
import sqlite3
import sys
import tarfile
import xml.etree.ElementTree as ET

MAGIC = b"OGKM"
MAX_BLOB_BYTES = 4 * 1024 * 1024
MAX_XML_BYTES = 8 * 1024 * 1024
MAX_OAR_MEMBERS = 200_000


def validate_typed_v1(blob: bytes) -> bool:
    """Structurally verify OGKM v1 without constructing CLR/Python objects.

    Mirrors KeyframeMotion.SafeSerialization.cs (fixed layout, bounded frames,
    finite vectors/quaternions). A valid structure is NOT a guarantee of
    identical game behavior; real runtime migration tests are still required.
    """
    if len(blob) < 8 or len(blob) > MAX_BLOB_BYTES or blob[:8] != MAGIC + b"\x01\x00\x00\x00":
        return False
    offset = 8

    def read(fmt: str):
        nonlocal offset
        size = struct.calcsize(fmt)
        if offset + size > len(blob):
            raise ValueError("Truncated keyframe data")
        result = struct.unpack_from(fmt, blob, offset)
        offset += size
        return result

    def vector():
        if not all(math.isfinite(v) for v in read("<3f")):
            raise ValueError("Nonfinite keyframe vector")

    def rotation():
        if not all(math.isfinite(v) for v in read("<4f")):
            raise ValueError("Nonfinite keyframe quaternion")

    def frame():
        (has_position,) = read("<B")
        if has_position:
            vector()
        (has_rotation,) = read("<B")
        if has_rotation:
            rotation()
        rotation()
        read("<ii")  # TimeMS, TimeTotal (signed .NET Int32)
        vector()  # AngularVelocity
        vector()  # StartPosition

    try:
        (mode, data_flags) = read("<ii")
        if not 0 <= mode <= 3 or (data_flags & ~3) != 0:
            return False
        read("<B")   # Running
        read("<ii")  # Iterations, SkipLoops
        vector()     # SerializedPosition
        vector()     # BasePosition
        rotation()   # BaseRotation
        frame()      # CurrentFrame

        (frame_count,) = read("<i")
        if frame_count < 0 or frame_count > 4096:
            return False
        for _ in range(frame_count):
            frame()

        (template_count,) = read("<i")
        if template_count < -1 or template_count > 4096:
            return False
        for _ in range(max(0, template_count)):
            frame()
    except (ValueError, struct.error):
        return False
    return offset == len(blob)


def classify(blob: bytes) -> str:
    """Classify an asset header and validate the entire typed v1 structure."""
    if not blob:
        return "empty"
    if len(blob) > MAX_BLOB_BYTES:
        return "oversized"
    if blob.startswith(MAGIC):
        if len(blob) < 8:
            return "truncated-v1-header"
        version = int.from_bytes(blob[4:8], "little")
        if version != 1:
            return "unknown-typed-version"
        return "typed-v1-valid" if validate_typed_v1(blob) else "typed-v1-invalid"
    # Never deserialize an unknown/legacy .NET object graph.
    return "non-v1-legacy-or-unknown"


def audit_sqlite(path: Path) -> dict:
    """Query the region prims table without modifying the SQLite file."""
    if not path.is_file():
        raise FileNotFoundError(path)
    # mode=ro avoids accidental DB creation; only pass paths, never SQL from CLI.
    uri = path.resolve().as_uri() + "?mode=ro"
    result = Counter()
    with sqlite3.connect(uri, uri=True, timeout=3) as db:
        tables = {row[0].lower(): row[0] for row in db.execute(
            "SELECT name FROM sqlite_master WHERE type='table'")}
        if "prims" not in tables:
            raise ValueError("SQLite region store has no prims table")
        columns = {row[1].lower(): row[1] for row in db.execute(
            'PRAGMA table_info("prims")')}
        if "keyframemotion" not in columns:
            raise ValueError("SQLite prims table has no KeyframeMotion column")
        result["examined_nonnull"] = 0
        for (blob,) in db.execute('SELECT "KeyframeMotion" FROM "prims" '
                                  'WHERE "KeyframeMotion" IS NOT NULL'):
            result["examined_nonnull"] += 1
            result[classify(bytes(blob))] += 1
    return {"kind": "sqlite", "source": str(path), "counts": dict(result)}


def audit_hex_lines(path: Path) -> dict:
    """Read a MariaDB/MySQL SELECT HEX(KeyframeMotion) export, never connect or write."""
    if not path.is_file():
        raise FileNotFoundError(path)
    result = Counter()
    with path.open("r", encoding="ascii") as stream:
        for line in stream:
            result["examined_nonnull"] += 1
            value = line.strip()
            if len(value) > MAX_BLOB_BYTES * 2:
                result["oversized"] += 1
                continue
            try:
                blob = bytes.fromhex(value)
            except ValueError:
                result["invalid-hex"] += 1
                continue
            result[classify(blob)] += 1
    return {"kind": "mariadb-hex-export", "source": str(path),
            "counts": dict(result)}


def audit_oar(path: Path) -> dict:
    """Scan only OAR objects/*.xml streams, never extract archive members."""
    if not path.is_file():
        raise FileNotFoundError(path)
    result = Counter()
    with tarfile.open(path, mode="r:*") as archive:
        for index, entry in enumerate(archive):
            if index >= MAX_OAR_MEMBERS:
                raise ValueError("OAR member count exceeds safety limit")
            if not (entry.isfile() and entry.name.startswith("objects/")
                    and entry.name.lower().endswith(".xml")):
                continue
            result["object_xml_files"] += 1
            if entry.size > MAX_XML_BYTES:
                result["oversized-object-xml"] += 1
                continue
            stream = archive.extractfile(entry)
            if stream is None:
                result["unreadable-object-xml"] += 1
                continue
            raw = stream.read(MAX_XML_BYTES + 1)
            if len(raw) > MAX_XML_BYTES or b"<!DOCTYPE" in raw.upper():
                result["unsafe-object-xml"] += 1
                continue
            try:
                root = ET.fromstring(raw)
            except ET.ParseError:
                result["invalid-object-xml"] += 1
                continue
            for elem in root.iter():
                if elem.tag.rsplit("}", 1)[-1] != "KeyframeMotion":
                    continue
                result["keyframe_fields"] += 1
                txt = "".join((elem.text or "").split())
                # Max encoded size before decoding to prevent huge allocations.
                if len(txt) > (MAX_BLOB_BYTES + 1) * 4 // 3 + 16:
                    result["oversized"] += 1
                    continue
                try:
                    data = base64.b64decode(txt, validate=True)
                except (ValueError, binascii.Error):
                    result["invalid-base64"] += 1
                    continue
                result[classify(data)] += 1
    return {"kind": "oar", "source": str(path), "counts": dict(result)}


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--sqlite", action="append", default=[], type=Path,
                        help="Read-only SQLite region-store snapshot (repeatable)")
    parser.add_argument("--oar", action="append", default=[], type=Path,
                        help="OAR tar/tar.gz archive (repeatable)")
    parser.add_argument("--hex-lines", action="append", default=[], type=Path,
                        help="Offline HEX(KeyframeMotion) export from MariaDB (repeatable)")
    parser.add_argument("--json", action="store_true",
                        help="Emit machine-readable JSON report")
    parser.add_argument("--fail-on-legacy", action="store_true",
                        help="Return code 2 for any non-v1, invalid or oversized data")
    args = parser.parse_args(argv)
    if not args.sqlite and not args.oar and not args.hex_lines:
        parser.error("Pass at least one --sqlite, --oar or --hex-lines")
    reports = []
    for path in args.sqlite:
        reports.append(audit_sqlite(path))
    for path in args.oar:
        reports.append(audit_oar(path))
    for path in args.hex_lines:
        reports.append(audit_hex_lines(path))
    invalid = {"non-v1-legacy-or-unknown", "unknown-typed-version",
               "truncated-v1-header", "typed-v1-invalid", "oversized", "invalid-base64",
               "oversized-object-xml", "unsafe-object-xml",
               "unreadable-object-xml", "invalid-object-xml", "invalid-hex"}
    legacy_found = any(any(r["counts"].get(k, 0) for k in invalid)
                       for r in reports)
    report = {"warning": "Read-only bounded typed-v1 structural audit; validated "
                         "structure does NOT prove behavioral compatibility. "
                         "Never use legacy BinaryFormatter on untrusted data.",
              "needs_migration_review": legacy_found,
              "sources": reports}
    if args.json:
        print(json.dumps(report, indent=2, ensure_ascii=False))
    else:
        print(report["warning"])
        for source in reports:
            print(f"{source['kind']} {source['source']}")
            for name, count in sorted(source["counts"].items()):
                print(f"  {name}: {count}")
        print("REVIEW REQUIRED" if legacy_found else "No malformed/legacy headers detected")
    return 2 if args.fail_on_legacy and legacy_found else 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except (OSError, ValueError, sqlite3.DatabaseError, tarfile.TarError) as exc:
        print(f"Audit failed without changing source files: {exc}", file=sys.stderr)
        sys.exit(3)

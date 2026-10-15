#!/usr/bin/env python3
"""Read-only, conservative OGL YEngine .state/OAR inventory (NO deserialization).

Reports YEngine migration snapshot headers; even matching v11 headers are
NOT proof of compatibility with the .NET10 allowlisted graph codec.
"""
import argparse
import base64
import binascii
from collections import Counter
import json
from pathlib import Path
import sys
import tarfile
import xml.etree.ElementTree as ET

MAX_XML = 8 * 1024 * 1024
MAX_SNAPSHOT = 4 * 1024 * 1024
MAX_MEMBERS = 200000
EXPECTED_VERSION = 11

def classify(xml_bytes, count):
    count["xml_examined"] += 1
    if len(xml_bytes) > MAX_XML or b"<!DOCTYPE" in xml_bytes.upper():
        count["xml_unsafe_or_oversized"] += 1
        return
    try:
        root = ET.fromstring(xml_bytes)
    except ET.ParseError:
        count["xml_invalid"] += 1
        return
    for node in root.iter():
        if node.tag.rsplit("}", 1)[-1] != "ScriptState":
            continue
        if node.attrib.get("Engine") != "YEngine":
            continue
        count["yengine_states"] += 1
        snapshot = next((n for n in node if n.tag.rsplit("}", 1)[-1] == "Snapshot"), None)
        if snapshot is None or snapshot.text is None:
            count["missing_snapshot"] += 1
            continue
        encoded = "".join(snapshot.text.split())
        if len(encoded) > (MAX_SNAPSHOT + 1) * 4 // 3 + 16:
            count["oversized_snapshot"] += 1
            continue
        try:
            raw = base64.b64decode(encoded, validate=True)
        except (ValueError, binascii.Error):
            count["invalid_base64"] += 1
            continue
        if len(raw) > MAX_SNAPSHOT:
            count["oversized_snapshot"] += 1
        elif len(raw) < 3:
            count["truncated_snapshot"] += 1
        elif raw[0] != EXPECTED_VERSION:
            count["unknown_migration_version"] += 1
        elif raw[1] != 16:
            count["unexpected_snapshot_marker"] += 1
        else:
            # A legacy BinaryFormatter payload can be nested in the graph.
            # It cannot be detected reliably with a simple header check.
            count["v11_requires_runtime_validation"] += 1

def states_dir(path, counts):
    if not path.is_dir() or path.is_symlink():
        raise ValueError("Existing, non-symlink state directory required")
    for file in sorted(path.rglob("*.state")):
        if file.is_symlink() or not file.is_file():
            raise ValueError("Invalid script state file: " + str(file))
        if file.stat().st_size > MAX_XML:
            counts["xml_unsafe_or_oversized"] += 1
        else:
            classify(file.read_bytes(), counts)

def oar(path, counts):
    if not path.is_file() or path.is_symlink():
        raise ValueError("Existing non-symlink OAR required")
    with tarfile.open(path, "r:*") as archive:
        for index, member in enumerate(archive):
            if index >= MAX_MEMBERS:
                raise ValueError("OAR member limit reached")
            if not (member.isfile() and member.name.startswith("objects/") and
                    member.name.lower().endswith(".xml")):
                continue
            if member.size > MAX_XML:
                counts["xml_unsafe_or_oversized"] += 1
                continue
            handle = archive.extractfile(member)
            if handle is None:
                counts["xml_invalid"] += 1
                continue
            classify(handle.read(MAX_XML + 1), counts)

def main(argv=None):
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument("--state-dir", action="append", default=[], type=Path)
    p.add_argument("--oar", action="append", default=[], type=Path)
    p.add_argument("--json", action="store_true")
    p.add_argument("--fail-on-review", action="store_true",
                   help="Exit 2 when YEngine state or malformed XML requires operator review")
    args = p.parse_args(argv)
    if not args.state_dir and not args.oar:
        p.error("Specify --state-dir or --oar")
    counts = Counter()
    for path in args.state_dir:
        states_dir(path, counts)
    for path in args.oar:
        oar(path, counts)
    blockers = (counts["yengine_states"] or counts["xml_invalid"] or
                counts["xml_unsafe_or_oversized"])
    report = {
        "safe_to_upgrade": False if blockers else True,
        "warning": "YEngine header inspection only; matching v11 snapshots may "
                   "contain legacy BinaryFormatter graph tokens. Validate real "
                   "script restart/crossing in isolated .NET10 test grid.",
        "counts": dict(counts)
    }
    if args.json:
        print(json.dumps(report, indent=2, ensure_ascii=False))
    else:
        print(report["warning"])
        for key in sorted(counts):
            print(f"  {key}: {counts[key]}")
        print("MANUAL MIGRATION REVIEW REQUIRED" if blockers else "No YEngine states found")
    return 2 if args.fail_on_review and blockers else 0

if __name__ == "__main__":
    try:
        sys.exit(main())
    except (OSError, ValueError, tarfile.TarError) as e:
        print("YEngine audit failed without changing sources: " + str(e), file=sys.stderr)
        sys.exit(3)

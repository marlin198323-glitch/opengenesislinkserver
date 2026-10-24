#!/usr/bin/env python3
"""Review versioned OpenSim INI defaults without reading or copying secret values."""
import argparse
import json
from pathlib import Path
import re
import sys

KEY = re.compile(r"^\s*([^#;\s][^=]*?)\s*=\s*(.*)$")
SECTION = re.compile(r"^\s*\[([^\]]+)\]\s*(?:[;#].*)?$")


def keys(path):
    """Return names only; never include live INI values in generated output."""
    values = set()
    section = ""
    if not path.is_file():
        return values
    for raw in path.read_text(encoding="utf-8-sig").splitlines():
        match = SECTION.match(raw)
        if match:
            section = match.group(1).strip()
            continue
        match = KEY.match(raw)
        if match and section:
            values.add((section, match.group(1).strip()))
    return values


def safe_relative(base, path):
    relative = path.relative_to(base)
    if any(part.startswith(".") for part in relative.parts):
        raise ValueError("Hidden configuration path not accepted")
    return relative


def sync(active_bin, downloaded_bin, output):
    if not active_bin.is_dir() or not downloaded_bin.is_dir():
        raise ValueError("Both binary config directories must exist")
    active_root = active_bin.resolve()
    downloaded_root = downloaded_bin.resolve()
    output_root = output.resolve()
    if (output_root == active_root or active_root in output_root.parents
            or output_root == downloaded_root or downloaded_root in output_root.parents):
        raise ValueError("Review output must not be inside a live or downloaded bin tree")
    if output.is_symlink():
        raise ValueError("Review output may not be a symlink")
    output.mkdir(parents=True, exist_ok=True)
    report = []
    # Scan the shipped complete defaults, plus active config files. Keep all
    # actual live config values private: report contains setting names only.
    file_set = set()
    for folder in (downloaded_bin, active_bin):
        for pattern in ("*.ini", "*.ini.example"):
            for path in folder.glob(pattern):
                if path.is_file():
                    canonical = path.name.removesuffix(".example")
                    file_set.add(canonical)
        inc = folder / "config-include"
        if inc.is_dir():
            for pattern in ("*.ini", "*.ini.example"):
                for path in inc.rglob(pattern):
                    if path.is_file():
                        canonical = str(safe_relative(folder, path))
                        file_set.add(canonical.removesuffix(".example"))
    for name in sorted(file_set):
        relative = Path(name)
        shipped = downloaded_bin / relative
        example = shipped.with_name(shipped.name + ".example")
        active = active_bin / relative
        baseline = shipped if shipped.is_file() else example
        if not baseline.is_file():
            continue
        if baseline.is_symlink() or active.is_symlink():
            raise ValueError("Configuration symlink rejected: " + name)
        new_keys = keys(baseline)
        old_keys = keys(active)
        missing = sorted(new_keys - old_keys)
        # Produce only derived .example.ini files in a separate review dir.
        # No attempt to merge live values or modify existing active configs.
        if missing or not active.exists():
            destination = output / relative.parent / (relative.stem + ".example.ini")
            destination.parent.mkdir(parents=True, exist_ok=True)
            if destination.is_symlink():
                raise ValueError("Review target symlink rejected: " + name)
            destination.write_text(
                "; OpenGenesisLINK update review ONLY. Never automatically overwrite live INI.\n"
                "; Compare this complete upstream template manually against the live file.\n"
                + baseline.read_text(encoding="utf-8-sig"),
                encoding="utf-8")
        report.append({
            "file": name,
            "active_exists": active.is_file(),
            "upstream_template": str(baseline.relative_to(downloaded_bin)),
            "missing_settings": [
                {"section": sect, "name": key}
                for sect, key in missing
            ],
        })
    (output / "missing-settings.json").write_text(
        json.dumps(report, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    return report


def main():
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--active-bin", required=True, type=Path)
    ap.add_argument("--downloaded-bin", required=True, type=Path)
    ap.add_argument("--output", required=True, type=Path)
    args = ap.parse_args()
    try:
        report = sync(args.active_bin, args.downloaded_bin, args.output)
    except (ValueError, OSError) as exc:
        print(f"Config review failed: {exc}", file=sys.stderr)
        return 2
    print(f"Reviewed {len(report)} INI paths; live files untouched. "
          f"Review generated examples at {args.output}")
    return 0


if __name__ == "__main__":
    sys.exit(main())

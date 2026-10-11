#!/usr/bin/env python3
"""Fail-closed operator evidence gate for OpenGenesisLINK 0.9.3.10 releases.

This tool does NOT tag, publish, deploy, infer PASS from GitHub CI, or
independently authenticate human-submitted evidence. Evidence must be verified
by the operator and release maintainer before any release.
"""
import argparse
import datetime as dt
import json
import re
import subprocess
import sys
from pathlib import Path

REPOSITORY = "opengenesislink/opengenesislinkserver"
VERSION = "0.9.3.10"
HISTORIC = {
    "SYS": 4, "EXP": 8, "LSL": 6, "DB": 6, "NAV": 7, "REL": 7
}
# Additional gates introduced after the original 38-case operator report.
EXTRA = (
    "EXP_CREATE", "NAV08", "APP01", "PORTAL_IMAGE",
    "PORTAL_DISCOVERY", "PORTAL_GROUPS", "PORTAL_REGION",
    "PORTAL_AUTH", "RUNTIME_STRESS", "RECOVERY_RESTORE",
)
REQUIRED_CASES = tuple(
    f"{prefix}{number:02d}"
    for prefix, count in HISTORIC.items()
    for number in range(1, count + 1)
) + EXTRA
assert len(REQUIRED_CASES) == 48


def checkout_sha():
    return subprocess.check_output(
        ["git", "rev-parse", "HEAD"], text=True
    ).strip()


def current_flavour(root):
    value = (root / "OpenSim/Framework/VersionInfo.cs").read_text()
    result = re.search(r"VERSION_FLAVOUR\s*=\s*Flavour\.(\w+)\s*;", value)
    return result.group(1) if result else None


def initial_manifest(sha):
    return {
        "format": "ogl-release-evidence-v1",
        "version": VERSION,
        "commit": sha,
        "ci_commit": sha,
        "ci_run_url": "",
        "date": dt.date.today().isoformat(),
        "firestorm_version": "7.2.4.80712",
        "developer_region": "OGL Developer Gen1",
        "region_id": "",
        "physics_engine": "",
        "database_backend": "",
        "grid_topology": "",
        "operator": "",
        "maintainer": "",
        "known_limitations_reviewed": False,
        "cases": {
            test: {
                "status": "NOT_RUN",
                "observed_at": "",
                "evidence_ref": "",
                "notes": "",
            } for test in REQUIRED_CASES
        },
        "rc1_signoff": {"approved": False, "date": "", "by": ""},
        "stable_signoff": {
            "approved": False, "date": "", "by": "",
            "soak_hours": 0, "rc1_tag": "v0.9.3.10-rc1",
            "rollback_restored": False,
        },
    }


def parse_iso_date(value):
    try:
        return bool(value) and dt.date.fromisoformat(value) <= dt.date.today()
    except (ValueError, TypeError):
        return False


def validate(manifest, stage, expected_sha, flavour, rc_commit=None):
    errors = []
    if not isinstance(manifest, dict):
        return ["Evidence must be a JSON object."]
    def need(condition, message):
        if not condition:
            errors.append(message)

    need(manifest.get("format") == "ogl-release-evidence-v1",
         "Evidence format is invalid.")
    need(manifest.get("version") == VERSION,
         "Version must be exactly 0.9.3.10.")
    need(manifest.get("commit") == expected_sha,
         "Evidence commit must equal the currently checked-out SHA.")
    need(manifest.get("ci_commit") == expected_sha,
         "CI run must target the exact release SHA.")
    url = manifest.get("ci_run_url", "")
    need(isinstance(url, str) and bool(re.fullmatch(
        r"https://github\.com/opengenesislink/opengenesislinkserver/actions/runs/[1-9][0-9]*",
        url)), "Supply the exact successful main CI run URL.")
    for field in ("operator", "maintainer", "region_id",
                  "physics_engine", "database_backend", "grid_topology"):
        need(isinstance(manifest.get(field), str) and
             bool(manifest.get(field, "").strip()),
             f"Missing operator environment metadata: {field}.")
    need(parse_iso_date(manifest.get("date")),
         "Evidence date missing, malformed or in the future.")
    need(manifest.get("known_limitations_reviewed") is True,
         "Known limitations must be acknowledged.")

    cases = manifest.get("cases")
    if not isinstance(cases, dict):
        return errors + ["Required test cases are missing."]
    for key in REQUIRED_CASES:
        item = cases.get(key)
        if not isinstance(item, dict):
            errors.append(f"{key}: NOT RUN (no evidence record)")
            continue
        if item.get("status") != "PASS":
            errors.append(f"{key}: NOT RUN/FAIL (not PASS)")
            continue
        if not parse_iso_date(item.get("observed_at")):
            errors.append(f"{key}: invalid/missing observation date")
        evidence = item.get("evidence_ref", "")
        need(isinstance(evidence, str) and
             len(evidence.strip()) >= 8 and
             "TODO" not in evidence.upper() and
             "PLACEHOLDER" not in evidence.upper(),
             f"{key}: missing concrete evidence reference")
        notes = item.get("notes", "")
        need(isinstance(notes, str) and len(notes.strip()) >= 8,
             f"{key}: missing observed result / notes")
    if stage == "rc1":
        need(flavour == "RC1",
             "RC1 requires VersionInfo.VERSION_FLAVOUR = Flavour.RC1.")
    else:
        need(flavour == "Release",
             "Stable requires VersionInfo.VERSION_FLAVOUR = Flavour.Release.")

    rc = manifest.get("rc1_signoff") or {}
    need(isinstance(rc, dict) and rc.get("approved") is True and
         parse_iso_date(rc.get("date")) and bool(rc.get("by", "").strip()),
         "RC1 operator/maintainer approval is missing.")
    if stage == "stable":
        final = manifest.get("stable_signoff") or {}
        need(isinstance(final, dict) and final.get("approved") is True and
             parse_iso_date(final.get("date")) and
             bool(final.get("by", "").strip()),
             "Separate Stable operator/maintainer approval is missing.")
        need(isinstance(final, dict) and final.get("rollback_restored") is True,
             "Stable requires an actually restored rollback test.")
        need(isinstance(final, dict) and
             isinstance(final.get("soak_hours"), (int, float)) and
             not isinstance(final.get("soak_hours"), bool) and
             final["soak_hours"] >= 24,
             "Stable requires at least 24 hours of operator-observed soak.")
        need(isinstance(final, dict) and
             final.get("rc1_tag") == "v0.9.3.10-rc1" and rc_commit is not None,
             "Stable requires a verified RC1 tag in the commit ancestry.")
    return errors


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    operation = parser.add_mutually_exclusive_group(required=True)
    operation.add_argument("--init", metavar="PATH",
                           help="Create an all-NOT_RUN template (never claims PASS)")
    operation.add_argument("--evidence", metavar="PATH",
                           help="Read an operator-completed JSON evidence file")
    parser.add_argument("--stage", choices=("rc1", "stable"), default="rc1")
    args = parser.parse_args()

    try:
        sha = checkout_sha()
    except (subprocess.CalledProcessError, FileNotFoundError):
        parser.error("Run from a real Git checkout.")
    if args.init:
        destination = Path(args.init).expanduser()
        destination.parent.mkdir(parents=True, exist_ok=True)
        try:
            with destination.open("x", encoding="utf-8") as handle:
                json.dump(initial_manifest(sha), handle, indent=2)
                handle.write("\n")
        except FileExistsError:
            parser.error("Refusing to overwrite existing operator evidence.")
        print(f"Created NOT_RUN evidence template for {sha[:12]}: {destination}")
        return 0

    try:
        data = json.loads(Path(args.evidence).read_text(encoding="utf-8"))
    except (OSError, ValueError) as exc:
        parser.error("Cannot read/parse evidence JSON: " + str(exc))
    rc_commit = None
    if args.stage == "stable":
        try:
            rc_commit = subprocess.check_output(
                ["git", "rev-parse", "--verify",
                 "refs/tags/v0.9.3.10-rc1^{commit}"],
                text=True, stderr=subprocess.DEVNULL).strip()
            subprocess.run(["git", "merge-base", "--is-ancestor", rc_commit, sha],
                           check=True, stdout=subprocess.DEVNULL,
                           stderr=subprocess.DEVNULL)
        except (OSError, subprocess.CalledProcessError):
            rc_commit = None
    issues = validate(data, args.stage, sha,
                      current_flavour(Path.cwd()), rc_commit)
    if issues:
        print(f"0.9.3.10 {args.stage.upper()} NOT APPROVED: {len(issues)} missing/failed checks")
        for issue in issues[:35]:
            print(" - " + issue)
        if len(issues) > 35:
            print(f" - ... {len(issues) - 35} additional issues")
        return 1
    print(f"0.9.3.10 {args.stage.upper()}: evidence manifest is structurally complete")
    print("IMPORTANT: Verify external test logs, CI SHA, backup and operator sign-off")
    print("independently. This checker never publishes or authorizes a release.")
    return 0


if __name__ == "__main__":
    sys.exit(main())

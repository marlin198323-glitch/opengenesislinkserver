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
# Explicit new release scope. Historical EXP/LSL/DB/REL/APP case numbers had
# Experience and Firestorm My Outfits dependencies. Do NOT silently reinterpret
# their results as evidence for unrelated features. The original 40-case matrix
# remains historical; all v2 items start NOT_RUN.
CASES_BY_AREA = {
    "core": (
        "CORE_BOOT", "CORE_LOGIN", "CORE_HG", "CORE_REGION", "CORE_LSL",
    ),
    "economy": ("ECON_LEDGER", "ECON_VIEWER", "ECON_MASTERBANK"),
    "inventory": ("INV_CORE", "INV_THUMB"),
    "api": (
        "API_AUTH", "API_IMAGE", "API_DISCOVERY", "API_GROUPS",
        "API_REGIONS",
    ),
    "performance": ("PERF_BASELINE", "PERF_STRESS"),
    "recovery": ("RECOVERY_RESTORE",),
    "security": ("SEC_XP_OFF", "SEC_SECRETS"),
    "release": ("RELEASE_DEPLOY", "RELEASE_OPERATOR", "STABLE_APPROVAL"),
}
REQUIRED_CASES = tuple(
    case for tests in CASES_BY_AREA.values() for case in tests
)
assert len(REQUIRED_CASES) == 23
assert len(set(REQUIRED_CASES)) == len(REQUIRED_CASES)

# Approval of a tagged RC1 is logically impossible before that tag exists.
RC1_POST_RELEASE_CASES = frozenset({"STABLE_APPROVAL"})
DEFERRED_SCOPE = {
    "nexperiences": "DISABLED",
    "ogl_pathfinding_2": "DEFERRED",
    "ogl_my_outfits_2": "DEFERRED",
}


def required_cases_for(stage):
    if stage == "rc1":
        return tuple(x for x in REQUIRED_CASES if x not in RC1_POST_RELEASE_CASES)
    if stage == "stable":
        return REQUIRED_CASES
    raise ValueError("Unknown release stage")



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
        "format": "ogl-release-evidence-v2",
        "scope": "09310-stabilization-no-experiences-myoutfits2-pathfinding2",
        "deferred_features": dict(DEFERRED_SCOPE),
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

    need(manifest.get("format") == "ogl-release-evidence-v2",
         "Evidence format v2 is required; historical Experience results cannot be reused.")
    need(manifest.get("scope") ==
         "09310-stabilization-no-experiences-myoutfits2-pathfinding2",
         "Release scope is incorrect.")
    need(manifest.get("deferred_features") == DEFERRED_SCOPE,
         "Deferred features must stay out of the release scope.")
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
    need(set(cases) == set(REQUIRED_CASES),
         "Evidence case IDs must match the current v2 matrix exactly.")
    for key in required_cases_for(stage):
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
        # Once RC1 has been tagged, its approval is reviewed for Stable
        # only. STABLE_APPROVAL stays NOT_RUN in the RC1 manifest.
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

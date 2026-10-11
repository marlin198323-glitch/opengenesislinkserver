#!/usr/bin/env python3
"""Read-only host preflight for OpenGenesisLINK 0.9.3.10 operator testing.

This examines ONLY source checkouts and the selected INI files. It cannot
read the effective Nini include graph, the running process' binary version,
environment secrets, World API response, viewer CAPS or real test results.
No secret values are printed, logged or included in JSON.
"""
import argparse
import configparser
import json
import subprocess
from pathlib import Path


def git_value(directory, *args):
    try:
        p = subprocess.run(
            ["git", "-C", str(directory), *args],
            stdout=subprocess.PIPE, stderr=subprocess.DEVNULL,
            text=True, timeout=5, check=False,
        )
        return p.stdout.strip() if p.returncode == 0 else None
    except (OSError, subprocess.TimeoutExpired):
        return None


def repo_state(path):
    path = Path(path).expanduser().resolve()
    sha = git_value(path, "rev-parse", "HEAD")
    branch = git_value(path, "branch", "--show-current") if sha else None
    status = git_value(path, "status", "--porcelain", "--", "bin/OpenSim.ini")
    # Only the fact of local modifications is surfaced. Never print a diff.
    return {
        "path": str(path), "git_sha": sha, "branch": branch,
        "opensim_ini_local_changes": status is None or bool(status),
        "source_dir_exists": path.is_dir(),
    }


def ini_read(path):
    cp = configparser.ConfigParser(
        interpolation=None, strict=False, allow_no_value=True,
        inline_comment_prefixes=(";", "#"),
    )
    cp.optionxform = str
    try:
        with Path(path).open(encoding="utf-8") as stream:
            cp.read_file(stream)
        return cp
    except (OSError, UnicodeError, configparser.Error):
        return None


def setting(cp, section, key):
    if cp is None or not cp.has_section(section):
        return ""
    return (cp.get(section, key, fallback="") or "").strip().strip('"')


def enabled(cp, section, key):
    return setting(cp, section, key).lower() in ("true", "yes", "1", "on")


def simulator_settings(cp):
    section = "NexExperiencesViewer"
    result = {
        "ini_readable": cp is not None,
        "experience_module": enabled(cp, section, "Enabled"),
        "experience_read_caps": enabled(cp, section, "FirestormReadCaps"),
        "experience_permission_caps": enabled(cp, section, "FirestormPermissionCaps"),
        "experience_manage_caps": enabled(cp, section, "FirestormManageCaps"),
        "native_experience_prompt": enabled(cp, section, "NativeExperiencePrompt"),
        "script_pending_consent": enabled(cp, section, "ScriptPendingConsent"),
    }
    refs = [setting(cp, section, k) for k in (
        "ApiKey", "ViewerPermissionsApiKey", "ViewerManageApiKey"
    )]
    # Never disclose values, including literal credentials in bad INIs.
    result["three_experience_key_fields_present"] = all(bool(x) for x in refs)
    result["three_experience_key_references_distinct"] = (
        all(bool(x) for x in refs) and len(set(refs)) == 3
    )
    result["experience_base_url_present"] = bool(
        setting(cp, section, "WorldApiBaseUrl")
    )
    return result


def check(robust, simulator):
    robust = repo_state(robust)
    simulator = repo_state(simulator)
    root_r = Path(robust["path"])
    root_s = Path(simulator["path"])
    sim_ini = ini_read(root_s / "bin/OpenSim.ini")
    robust_ini = ini_read(root_r / "bin/Robust.HG.ini")
    flags = simulator_settings(sim_ini)
    robust_ok = robust_ini is not None
    same_commit = (
        bool(robust["git_sha"]) and
        robust["git_sha"] == simulator["git_sha"]
    )
    # Operators MAY intend to run optional modules disabled. State is
    # informational, never interpreted as an unconditional release PASS.
    blockers = []
    if not same_commit:
        blockers.append("Different/missing Git commits for Robust and simulator.")
    if robust["opensim_ini_local_changes"]:
        blockers.append("Robust OpenSim.ini has local modifications: use safe backup/merge.")
    if simulator["opensim_ini_local_changes"]:
        blockers.append("Developer OpenSim.ini has local modifications: use safe backup/merge.")
    if not robust_ok:
        blockers.append("Robust.HG.ini is missing or not parseable.")
    if not flags["ini_readable"]:
        blockers.append("Simulator OpenSim.ini is missing or not parseable.")
    if flags["experience_module"] and flags["experience_manage_caps"]:
        if not (
            flags["experience_read_caps"] and
            flags["experience_permission_caps"] and
            flags["three_experience_key_fields_present"] and
            flags["three_experience_key_references_distinct"] and
            flags["experience_base_url_present"]
        ):
            blockers.append(
                "Experience management opt-in is missing independent CAPS/key config."
            )
    return {
        "product": "OpenGenesisLINK",
        "target_version": "0.9.3.10",
        "robust": robust,
        "developer_simulator": simulator,
        "robust_ini_readable": robust_ok,
        "simulator_flags": flags,
        "same_source_commit": same_commit,
        "configuration_blockers": blockers,
        "runtime_tested": False,
        "release_approved": False,
        "limitations": [
            "Source Git SHA is not the running .NET process binary SHA.",
            "Read-only INI scanner cannot resolve Nini includes or runtime env.",
            "No viewer, two-avatar, live assets, persistence, or backup test.",
            "No credentials, capability URLs or environment values are displayed.",
        ],
    }


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument("--robust-dir", default="/opt/robust")
    p.add_argument("--sim-dir", default="/opt/developer")
    p.add_argument("--json", dest="output", default=None,
                   help="Optional sanitized report file; will not overwrite.")
    args = p.parse_args()
    result = check(args.robust_dir, args.sim_dir)
    print("OpenGenesisLINK 0.9.3.10 read-only host preflight")
    for name in ("robust", "developer_simulator"):
        entry = result[name]
        print(f"[{name}] git={entry['git_sha'] or 'unavailable'} branch={entry['branch'] or '?'}")
        print(f"[{name}] locally modified OpenSim.ini: {entry['opensim_ini_local_changes']}")
    print(f"Same source commit: {result['same_source_commit']}")
    print(f"Robust.HG.ini readable: {result['robust_ini_readable']}")
    print("Developer INI feature flags (not actual running process state):")
    for key, value in result["simulator_flags"].items():
        print(f"  {key} = {value}")
    for b in result["configuration_blockers"]:
        print("[ACTION] " + b)
    print("NOT A RELEASE PASS: real Firestorm, physical navigation, restore,"
          " stress and 48 operator evidence records remain separate.")
    if args.output:
        dst = Path(args.output).expanduser()
        dst.parent.mkdir(parents=True, exist_ok=True)
        try:
            with dst.open("x", encoding="utf-8") as stream:
                json.dump(result, stream, indent=2)
                stream.write("\n")
        except FileExistsError:
            p.error("Refusing to overwrite a prior preflight report.")
        print("Sanitized report written to " + str(dst))
    return 2 if result["configuration_blockers"] else 0


if __name__ == "__main__":
    raise SystemExit(main())

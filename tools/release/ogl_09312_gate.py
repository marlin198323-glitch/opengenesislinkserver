#!/usr/bin/env python3
"""OpenGenesisLINK 0.9.3.12 evidence checker; never changes version, tags or deploys.

Every PASS must be tied to a local evidence file and the exact tested SHA.
Operator remarks in chat are useful leads, not a substitute for test logs.
"""
import argparse
import datetime as dt
import hashlib
import json
import re
import subprocess
from pathlib import Path

FORMAT = "ogl-release-evidence-09312-v1"
VERSION = "0.9.3.12"
CASES = (
    "BOOT01", "VIEW01", "LSL01", "LSL02", "LSL03", "CHAT01", "CHAT02",
    "INV01", "INV02", "IAR01", "IAR02", "IAR03", "PORTAL01", "PORTAL02",
    "PROF01", "ECON01", "PERF01", "REC01", "RC01", "RC02",
    "STABLE01", "STABLE02",
)
STABLE_ONLY = frozenset(("STABLE01", "STABLE02"))
DEFERRED = {
    "ogl_voice": "disabled",
    "viewer_native_login": "disabled",
    "experiences_2": "deferred",
    "pathfinding_2": "deferred",
    "my_outfits_2": "deferred",
    "hydro_renderer": "deferred",
}
HEX = re.compile(r"^[a-fA-F0-9]{40}$")
HASH = re.compile(r"^[a-fA-F0-9]{64}$")
CI_RUN = re.compile(
    r"^https://github[.]com/opengenesislink/opengenesislinkserver/actions/runs/[0-9]+$"
)

def git(root, *args):
    return subprocess.check_output(
        ["git", "-C", str(root), *args], text=True, stderr=subprocess.DEVNULL
    ).strip()

def initial(sha):
    return {
        "format": FORMAT, "version": VERSION, "candidate_commit": sha,
        "ci_commit": "", "ci_run_url": "",
        "created_at": dt.datetime.now(dt.timezone.utc).isoformat(),
        "operator": "", "maintainer": "",
        "server_builds": {"robust": "", "simulator_a": "", "simulator_b": ""},
        "feature_scope": dict(DEFERRED),
        "known_limitations_reviewed": False,
        "cases": {
            c: {"status": "NOT_RUN", "tested_sha": "", "observed_at": "",
                "evidence_ref": "", "evidence_sha256": "", "notes": ""}
            for c in CASES
        },
        "rc1_signoff": {"approved": False, "by": "", "date": ""},
        "stable_signoff": {
            "approved": False, "by": "", "date": "",
            "soak_hours": 0, "rollback_restored": False,
            "rc1_tag": "v0.9.3.12-rc1",
        },
    }

def valid_iso_date(value):
    if not isinstance(value, str) or not value:
        return False
    try:
        if not re.fullmatch(r"[0-9]{4}-[0-9]{2}-[0-9]{2}", value):
            return False
        when = dt.date.fromisoformat(value)
        return when <= dt.datetime.now(dt.timezone.utc).date()
    except ValueError:
        return False

def version_info(root):
    source = (root / "OpenSim/Framework/VersionInfo.cs").read_text()
    number = re.search(r'AssemblyVersionNumber\s*=\s*"([^"]+)"', source)
    flavour = re.search(r"VERSION_FLAVOUR\s*=\s*Flavour[.](\w+)", source)
    return (number.group(1) if number else "", flavour.group(1) if flavour else "")

def evidence_hash(evidence_dir, reference):
    # Evidence never belongs in source control. Reject absolute/traversal paths
    # so a PASS cannot accidentally refer to secrets in arbitrary directories.
    if not isinstance(reference, str) or not reference or "\\" in reference:
        return None
    rel = Path(reference)
    if rel.is_absolute() or ".." in rel.parts or reference.startswith("."):
        return None
    root = evidence_dir.resolve()
    path = (root / rel).resolve()
    if path == root or root not in path.parents or not path.is_file():
        return None
    if path.stat().st_size == 0 or path.stat().st_size > 8 * 1024 * 1024:
        return None
    return hashlib.sha256(path.read_bytes()).hexdigest()

def validate(manifest, stage, root, evidence_dir, head, version, flavour):
    problems = []
    def require(ok, message):
        if not ok:
            problems.append(message)

    if not isinstance(manifest, dict):
        return ["Evidence muss ein JSON-Objekt sein."]
    require(manifest.get("format") == FORMAT, "Evidence-Format ungültig.")
    require(manifest.get("version") == VERSION, "Falsche Release-Version.")
    require(manifest.get("candidate_commit") == head and bool(HEX.fullmatch(head)),
            "Candidate-SHA ist nicht exakt der Checkout-HEAD.")
    require(manifest.get("feature_scope") == DEFERRED,
            "Ein nicht freigegebenes späteres Feature wurde aktiviert.")
    cases = manifest.get("cases")
    if not isinstance(cases, dict):
        return problems + ["Cases fehlen."], 0
    require(set(cases) == set(CASES), "Abnahmematrix muss exakt 22 Fälle enthalten.")
    required = set(CASES) - (STABLE_ONLY if stage == "rc1" else set())
    pass_count = 0
    for case in CASES:
        record = cases.get(case)
        if not isinstance(record, dict):
            problems.append(case + ": Ergebnisobjekt fehlt.")
            continue
        status = record.get("status")
        require(status in ("NOT_RUN", "FAIL", "PASS"), case + ": Ungültiger Status.")
        if status != "PASS":
            if case in required and stage != "inventory":
                problems.append(case + ": Ohne belegtes PASS keine Freigabe.")
            continue
        pass_count += 1
        require(record.get("tested_sha") == head, case + ": Test-SHA stimmt nicht.")
        require(valid_iso_date(record.get("observed_at")), case + ": Datum fehlt.")
        recorded = record.get("evidence_sha256", "")
        actual = evidence_hash(evidence_dir, record.get("evidence_ref"))
        require(actual is not None, case + ": Lokale Evidence-Datei fehlt/ist ungültig.")
        require(isinstance(recorded, str) and bool(HASH.fullmatch(recorded))
                and actual == recorded.lower(),
                case + ": SHA-256 der Evidence-Datei stimmt nicht.")
    if stage == "inventory":
        return problems, pass_count

    require(version == VERSION, "VersionInfo.AssemblyVersionNumber muss 0.9.3.12 sein.")
    require(flavour == ("RC1" if stage == "rc1" else "Release"),
            "VersionInfo hat den falschen RC-/Stable-Flavour.")
    require(manifest.get("ci_commit") == head,
            "GitHub-CI muss auf exakt demselben SHA laufen.")
    require(isinstance(manifest.get("ci_run_url"), str) and
            CI_RUN.fullmatch(manifest.get("ci_run_url")),
            "GitHub-CI-Run-URL fehlt/ist ungültig.")
    builds = manifest.get("server_builds")
    require(isinstance(builds, dict) and
            all(builds.get(k) == head for k in ("robust", "simulator_a", "simulator_b")),
            "Robust und zwei Simulatoren müssen exakt auf Candidate-SHA laufen.")
    require(bool(manifest.get("operator")) and bool(manifest.get("maintainer")),
            "Operator und Maintainer müssen benannt sein.")
    require(manifest.get("known_limitations_reviewed") is True,
            "Bekannte Einschränkungen müssen geprüft sein.")
    approval = manifest.get("rc1_signoff") or {}
    require(approval.get("approved") is True and
            bool(approval.get("by")) and valid_iso_date(approval.get("date")),
            "Explizite, datierte RC1-Freigabe fehlt.")

    if stage == "stable":
        stable = manifest.get("stable_signoff") or {}
        require(stable.get("approved") is True and
                bool(stable.get("by")) and valid_iso_date(stable.get("date")),
                "Explizite, datierte Stable-Freigabe fehlt.")
        require(isinstance(stable.get("soak_hours"), (int, float)) and
                stable.get("soak_hours") >= 24,
                "Mindestens 24 Stunden dokumentierter Testbetrieb fehlen.")
        require(stable.get("rollback_restored") is True,
                "Gesonderter erfolgreicher Rollback-Test fehlt.")
        require(stable.get("rc1_tag") == "v0.9.3.12-rc1",
                "Vorgänger-RC1-Tag fehlt.")
        try:
            rc_sha = git(root, "rev-parse", "refs/tags/v0.9.3.12-rc1^{commit}")
            subprocess.run(
                ["git", "-C", str(root), "merge-base", "--is-ancestor", rc_sha, head],
                check=True, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL,
            )
        except (subprocess.CalledProcessError, OSError):
            problems.append("RC1-Tag muss im Vorfahrenpfad des Stable-Commits liegen.")
    return problems, pass_count

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("command", choices=("init", "inventory", "rc1", "stable"))
    parser.add_argument("--manifest", required=True, type=Path)
    parser.add_argument("--evidence-dir", type=Path)
    parser.add_argument("--repo", type=Path, default=Path(__file__).resolve().parents[2])
    args = parser.parse_args()
    root = args.repo.resolve()
    head = git(root, "rev-parse", "HEAD")
    if args.command == "init":
        if args.manifest.exists():
            parser.error("Manifest existiert bereits; wird nicht überschrieben.")
        args.manifest.parent.mkdir(parents=True, exist_ok=True)
        args.manifest.write_text(
            json.dumps(initial(head), indent=2, ensure_ascii=False) + "\n",
            encoding="utf-8",
        )
        print("Evidence-Vorlage erstellt:", args.manifest)
        print("Alle 22 Prüffälle beginnen mit NOT_RUN. Keine Veröffentlichung.")
        return 0
    data = json.loads(args.manifest.read_text(encoding="utf-8"))
    version, flavour = version_info(root)
    evidence_dir = args.evidence_dir or args.manifest.parent / "logs"
    errors, passed = validate(
        data, args.command, root, evidence_dir, head, version, flavour
    )
    print("OGL", VERSION, args.command.upper(), "|", passed,
          "/22 belegte PASS |", "VersionInfo", version, flavour, "| SHA", head[:12])
    if errors:
        for error in errors:
            print("BLOCKIERT:", error)
        return 1
    print("Formale Prüfkriterien erfüllt. Manuelle Betreiberprüfung bleibt erforderlich.")
    return 0

if __name__ == "__main__":
    raise SystemExit(main())

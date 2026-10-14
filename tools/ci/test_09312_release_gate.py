#!/usr/bin/env python3
"""Regression for 0.9.3.12 RC1/Stable evidence rules; no external services."""
import datetime as dt
import hashlib
import importlib.util
import tempfile
import unittest
from pathlib import Path

GATE = Path(__file__).resolve().parents[1] / "release/ogl_09312_gate.py"
spec = importlib.util.spec_from_file_location("ogl_09312_gate", GATE)
gate = importlib.util.module_from_spec(spec)
spec.loader.exec_module(gate)

SHA = "a" * 40

class GateTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.directory = Path(self.tmp.name)
        self.evidence = self.directory / "logs"
        self.evidence.mkdir()
        self.manifest = gate.initial(SHA)
        self.today = dt.date.today().isoformat()

    def check(self, stage="inventory", flavour="RC1", version=gate.VERSION):
        return gate.validate(
            self.manifest, stage, self.directory,
            self.evidence, SHA, version, flavour
        )[0]

    def mark_pass(self, case):
        p = self.evidence / (case.lower() + ".log")
        p.write_text(case + " verified on " + SHA)
        record = self.manifest["cases"][case]
        record.update({
            "status": "PASS", "tested_sha": SHA,
            "observed_at": self.today,
            "evidence_ref": p.name,
            "evidence_sha256": hashlib.sha256(p.read_bytes()).hexdigest(),
        })

    def fill_rc(self):
        for c in gate.CASES:
            if c not in gate.STABLE_ONLY:
                self.mark_pass(c)
        self.manifest["ci_commit"] = SHA
        self.manifest["ci_run_url"] = (
            "https://github.com/opengenesislink/opengenesislinkserver/actions/runs/123"
        )
        self.manifest["server_builds"] = {
            "robust": SHA, "simulator_a": SHA, "simulator_b": SHA
        }
        self.manifest["operator"] = "Release Operator"
        self.manifest["maintainer"] = "Release Maintainer"
        self.manifest["known_limitations_reviewed"] = True
        self.manifest["rc1_signoff"] = {
            "approved": True, "date": self.today, "by": "Release Operator"
        }

    def test_catalog_has_22_unique_entries(self):
        self.assertEqual(len(gate.CASES), 22)
        self.assertEqual(len(set(gate.CASES)), 22)

    def test_no_pass_in_new_manifest(self):
        self.assertEqual(self.manifest["cases"]["IAR02"]["status"], "NOT_RUN")
        self.assertIn("BOOT01", " ".join(self.check("rc1")))

    def test_operator_claim_without_log_is_not_pass(self):
        self.manifest["cases"]["BOOT01"].update({
            "status": "PASS", "tested_sha": SHA, "observed_at": self.today,
            "notes": "Chat bestaetigt funktioniert"
        })
        self.assertIn("BOOT01", " ".join(self.check()))

    def test_bogus_test_sha_rejected(self):
        self.mark_pass("LSL02")
        self.manifest["cases"]["LSL02"]["tested_sha"] = "b" * 40
        self.assertIn("LSL02", " ".join(self.check()))

    def test_changed_evidence_rejected(self):
        self.mark_pass("IAR03")
        (self.evidence / "iar03.log").write_text("changed")
        self.assertIn("IAR03", " ".join(self.check()))

    def test_evidence_path_traversal_rejected(self):
        self.mark_pass("IAR01")
        self.manifest["cases"]["IAR01"]["evidence_ref"] = "../secrets"
        self.assertIn("IAR01", " ".join(self.check()))

    def test_unknown_gate_rejected(self):
        self.manifest["cases"]["MAGIC99"] = {"status": "PASS"}
        self.assertIn("22", " ".join(self.check()))

    def test_scope_cannot_activate_deferred_features(self):
        self.manifest["feature_scope"]["ogl_voice"] = "enabled"
        self.assertIn("nicht freigegebenes", " ".join(self.check()))

    def test_rc_requires_every_case_and_approval(self):
        self.fill_rc()
        self.assertEqual(self.check("rc1"), [])
        self.manifest["rc1_signoff"]["approved"] = False
        self.assertIn("RC1-Freigabe", " ".join(self.check("rc1")))

    def test_version_stays_a_blocker(self):
        self.fill_rc()
        self.assertIn("VersionInfo", " ".join(self.check("rc1", version="0.9.3.10")))

    def test_wrong_flavour_rejected(self):
        self.fill_rc()
        self.assertIn("Flavour", " ".join(self.check("rc1", flavour="Release")))

    def test_ci_and_simulator_sha_required(self):
        self.fill_rc()
        self.manifest["server_builds"]["simulator_b"] = "b" * 40
        self.assertIn("Simulatoren", " ".join(self.check("rc1")))
        self.manifest["server_builds"]["simulator_b"] = SHA
        self.manifest["ci_commit"] = ""
        self.assertIn("GitHub-CI", " ".join(self.check("rc1")))

    def test_stable_no_24_hour_soak(self):
        self.fill_rc()
        self.mark_pass("STABLE01")
        self.mark_pass("STABLE02")
        self.assertIn("24 Stunden", " ".join(self.check("stable", flavour="Release")))

    def test_inventory_accepts_genuine_per_case_evidence(self):
        self.mark_pass("CHAT01")
        self.assertEqual(self.check(), [])

if __name__ == "__main__":
    unittest.main()

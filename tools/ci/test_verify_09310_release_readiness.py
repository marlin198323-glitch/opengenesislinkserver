#!/usr/bin/env python3
"""Regressions for operator gate; fabricated fixtures do not count as live tests."""
import copy
import datetime as dt
import importlib.util
from pathlib import Path
import unittest

module_path = Path(__file__).with_name("verify_09310_release_readiness.py")
spec = importlib.util.spec_from_file_location("release_gate_09310", module_path)
gate = importlib.util.module_from_spec(spec)
spec.loader.exec_module(gate)
SHA = "a" * 40
NOW = dt.date.today().isoformat()


def make_fixture():
    evidence = gate.initial_manifest(SHA)
    evidence.update({
        "ci_run_url": ("https://github.com/opengenesislink/"
                       "opengenesislinkserver/actions/runs/38147751531"),
        "operator": "Operator Test",
        "maintainer": "Maintainer Test",
        "region_id": "a0b73a25-6c75-453f-936c-cf529baef6e1",
        "physics_engine": "Test-engine",
        "database_backend": "Test-MariaDB",
        "grid_topology": "Robust + 2 Simulators",
        "known_limitations_reviewed": True,
    })
    for case in evidence["cases"].values():
        case.update({
            "status": "PASS",
            "observed_at": NOW,
            "evidence_ref": "tests/artifacts/anonymized-test-case.log",
            "notes": "Observed expected authenticated result.",
        })
    evidence["rc1_signoff"] = {
        "approved": True, "date": NOW, "by": "Test Operator",
    }
    evidence["stable_signoff"] = {
        "approved": True, "date": NOW, "by": "Test Operator",
        "soak_hours": 24, "rc1_tag": "v0.9.3.10-rc1",
        "rollback_restored": True,
    }
    return evidence


class ReleaseGateTests(unittest.TestCase):
    def test_current_scope_excludes_deferred_navigation(self):
        self.assertEqual(len(gate.REQUIRED_CASES), 40)
        self.assertEqual(sum(gate.HISTORIC.values()), 31)
        self.assertFalse(any(x.startswith('NAV') for x in gate.REQUIRED_CASES))
        for code in ("EXP_CREATE", "PORTAL_IMAGE", "RECOVERY_RESTORE"):
            self.assertIn(code, gate.REQUIRED_CASES)

    def test_rc1_does_not_require_approval_that_depends_on_rc1(self):
        self.assertEqual(len(gate.required_cases_for("rc1")), 39)
        self.assertEqual(len(gate.required_cases_for("stable")), 40)
        self.assertNotIn("REL07", gate.required_cases_for("rc1"))
        self.assertIn("REL07", gate.required_cases_for("stable"))
        d = make_fixture()
        d["cases"]["REL07"]["status"] = "NOT_RUN"
        self.assertEqual(gate.validate(d, "rc1", SHA, "RC1"), [])
        self.assertTrue(any("REL07" in issue for issue in
                            gate.validate(d, "stable", SHA, "Release", SHA)))

    def test_template_is_not_a_release(self):
        d = gate.initial_manifest(SHA)
        issues = gate.validate(d, "rc1", SHA, "Dev")
        self.assertGreaterEqual(len(issues), 40)
        self.assertTrue(any("NOT RUN" in error for error in issues))

    def test_rc1_requires_explicit_version_flavour_and_evidence(self):
        d = make_fixture()
        self.assertEqual(gate.validate(d, "rc1", SHA, "RC1"), [])
        self.assertTrue(gate.validate(d, "rc1", SHA, "Dev"))
        self.assertTrue(gate.validate(d, "rc1", SHA, "Release"))

    def test_rejects_different_ci_or_checkout_sha(self):
        d = make_fixture()
        d["ci_commit"] = "b" * 40
        self.assertTrue(gate.validate(d, "rc1", SHA, "RC1"))
        d = make_fixture()
        self.assertTrue(gate.validate(d, "rc1", "b" * 40, "RC1"))

    def test_unperformed_real_world_cases_remain_blocked(self):
        d = make_fixture()
        d["cases"]["EXP_CREATE"]["status"] = "NOT_RUN"
        d["cases"]["EXP02"]["status"] = "FAIL"
        d["cases"]["PORTAL_IMAGE"]["evidence_ref"] = "TODO"
        issues = gate.validate(d, "rc1", SHA, "RC1")
        self.assertTrue(any("EXP_CREATE" in e for e in issues))
        self.assertTrue(any("EXP02" in e for e in issues))
        self.assertTrue(any("PORTAL_IMAGE" in e for e in issues))

    def test_never_forges_stable_on_rc1_or_no_tag(self):
        d = make_fixture()
        self.assertTrue(gate.validate(d, "stable", SHA, "RC1", SHA))
        self.assertTrue(gate.validate(d, "stable", SHA, "Release", None))
        self.assertEqual(gate.validate(d, "stable", SHA, "Release", SHA), [])

    def test_stable_needs_soak_restore_and_separate_approval(self):
        d = make_fixture()
        d["stable_signoff"]["soak_hours"] = 1
        d["stable_signoff"]["rollback_restored"] = False
        d["stable_signoff"]["approved"] = False
        issues = gate.validate(d, "stable", SHA, "Release", SHA)
        self.assertGreaterEqual(len(issues), 3)

    def test_no_release_when_known_limitations_or_logs_missing(self):
        d = make_fixture()
        d["known_limitations_reviewed"] = False
        d["cases"]["EXP03"]["notes"] = ""
        self.assertTrue(gate.validate(d, "rc1", SHA, "RC1"))


if __name__ == "__main__":
    unittest.main()

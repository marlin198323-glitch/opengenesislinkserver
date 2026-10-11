#!/usr/bin/env python3
"""Unit tests: operator preflight never exports INI key values or grants PASS."""
import importlib.util
import json
from pathlib import Path
from tempfile import TemporaryDirectory
from unittest import TestCase, main
from unittest.mock import patch

p = Path(__file__).with_name("ogl_09310_operator_preflight.py")
spec = importlib.util.spec_from_file_location("release_host_preflight", p)
tool = importlib.util.module_from_spec(spec)
spec.loader.exec_module(tool)


class OperatorPreflightTests(TestCase):
    def test_no_credentials_are_returned_or_printable_in_json(self):
        with TemporaryDirectory() as base:
            cfg = Path(base) / "OpenSim.ini"
            cfg.write_text("""
[NexExperiencesViewer]
Enabled = true
FirestormReadCaps = true
FirestormPermissionCaps = true
FirestormManageCaps = true
WorldApiBaseUrl = "https://world.stadt-nexverse.de"
ApiKey = "FAKE_SECRET_SCRIPT_DO_NOT_EXPOSE"
ViewerPermissionsApiKey = "FAKE_SECRET_PERMISSION_DO_NOT_EXPOSE"
ViewerManageApiKey = "FAKE_SECRET_MANAGE_DO_NOT_EXPOSE"
""", encoding="utf-8")
            flags = tool.simulator_settings(tool.ini_read(cfg))
            result = json.dumps(flags)
            self.assertTrue(flags["three_experience_key_fields_present"])
            self.assertTrue(flags["three_experience_key_references_distinct"])
            for secret in ("FAKE_SECRET_SCRIPT_DO_NOT_EXPOSE",
                           "FAKE_SECRET_PERMISSION_DO_NOT_EXPOSE",
                           "FAKE_SECRET_MANAGE_DO_NOT_EXPOSE"):
                self.assertNotIn(secret, result)

    def test_rejects_identical_viewer_management_key(self):
        cp = tool.configparser.ConfigParser(interpolation=None)
        cp.read_string("""
[NexExperiencesViewer]
ApiKey = same-secret
ViewerPermissionsApiKey = other-secret
ViewerManageApiKey = same-secret
FirestormManageCaps = yes
""")
        flags = tool.simulator_settings(cp)
        self.assertFalse(flags["three_experience_key_references_distinct"])

    def test_missing_file_does_not_mean_test_pass(self):
        self.assertIsNone(tool.ini_read("/unavailable/ogl/simulator.ini"))
        result = tool.simulator_settings(None)
        self.assertFalse(result["ini_readable"])
        self.assertFalse(result["experience_module"])

    def test_realistic_inis_and_source_mismatch_are_reported(self):
        with TemporaryDirectory() as base:
            root = Path(base)
            r = root / "robust"
            s = root / "sim"
            (r / "bin").mkdir(parents=True)
            (s / "bin").mkdir(parents=True)
            (r / "bin/Robust.HG.ini").write_text("[NexExperiences]\nEnabled = true\n")
            (s / "bin/OpenSim.ini").write_text("[NexExperiencesViewer]\n"
                "Enabled = true\nFirestormManageCaps = true\n")
            def faux_repo(path):
                return {
                    "path": str(path), "git_sha": "a"*40 if str(path)==str(r) else "b"*40,
                    "branch": "main", "opensim_ini_local_changes": False,
                    "source_dir_exists": True,
                }
            with patch.object(tool, "repo_state", side_effect=faux_repo):
                report = tool.check(r, s)
            self.assertFalse(report["same_source_commit"])
            self.assertTrue(report["configuration_blockers"])
            self.assertFalse(report["runtime_tested"])
            self.assertFalse(report["release_approved"])

    def test_outputs_never_claim_live_grid_success(self):
        with TemporaryDirectory() as base:
            root = Path(base)
            for name in ("robust", "sim"):
                (root / name / "bin").mkdir(parents=True)
            (root / "robust/bin/Robust.HG.ini").write_text(
                "[NexExperiences]\\nEnabled = true\\n".replace("\\\\n", "\\n"))
            (root / "sim/bin/OpenSim.ini").write_text(
                "[NexExperiencesViewer]\\nEnabled = false\\n".replace("\\\\n", "\\n"))
            def faux_repo(path):
                return {
                    "path": str(path), "git_sha": "a" * 40,
                    "branch": "main", "opensim_ini_local_changes": False,
                    "source_dir_exists": True,
                }
            with patch.object(tool, "repo_state", side_effect=faux_repo):
                report = tool.check(root / "robust", root / "sim")
            self.assertTrue(report["same_source_commit"])
            self.assertFalse(report["runtime_tested"])
            self.assertFalse(report["release_approved"])
            self.assertIn("Source Git SHA is not the running .NET process binary SHA.",
                          report["limitations"])


if __name__ == "__main__":
    main()

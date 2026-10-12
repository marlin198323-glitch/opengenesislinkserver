#!/usr/bin/env python3
"""Sanity checks for the machine-readable LSL compatibility inventory."""
import importlib.util
from pathlib import Path
import unittest

SRC = Path(__file__).resolve().parents[1] / "lsl/lsl_matrix.py"
SPEC = importlib.util.spec_from_file_location("ogl_lsl_matrix", SRC)
module = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(module)


class MatrixTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.report = module.build()
        cls.functions = {f["name"]: f for f in cls.report["functions"]}

    def test_complete_schema_and_source_inventory(self):
        self.assertEqual(self.report["format"], "ogl-lsl-conformance-matrix-v1")
        self.assertGreaterEqual(self.report["summary"]["functions"], 250)
        self.assertGreaterEqual(self.report["summary"]["constants"], 100)
        self.assertGreaterEqual(self.report["summary"]["events"], 25)
        self.assertEqual(len(self.functions), self.report["summary"]["functions"])

    def test_cannot_claim_full_from_signature_presence(self):
        for item in self.report["functions"]:
            self.assertIn(item["support_level"], module.CATEGORIES)
            self.assertNotEqual(item["support_level"], "FULL")
            self.assertFalse(item["behaviour_verified"])
            self.assertFalse(item["timing_verified"])
            self.assertFalse(item["permission_verified"])

    def test_deferred_experience_and_pathfinding(self):
        for name in ("llRequestExperiencePermissions", "llGetExperienceDetails",
                     "llCreateKeyValue", "llReadKeyValue"):
            self.assertEqual(self.functions[name]["scope"], "POST_0_9_4_0")
        self.assertEqual(self.functions["llSay"]["scope"], "CORE")
        events = {e["name"]: e for e in self.report["events"]}
        self.assertEqual(events["experience_permissions"]["scope"], "POST_0_9_4_0")
        self.assertEqual(events["path_update"]["scope"], "POST_0_9_4_0")
        self.assertEqual(events["timer"]["scope"], "CORE")

    def test_key_structural_methods(self):
        for name in ("llAbs", "llRound", "llFloor", "llCeil", "llFabs", "llSay",
                     "llRegionSay", "llEstateSay"):
            item = self.functions[name]
            self.assertTrue(item["api_wrapper_present"], name)
            self.assertTrue(item["implementation_present"], name)

    def test_real_event_and_constant_metadata(self):
        events = {x["name"]: x for x in self.report["events"]}
        self.assertEqual(events["state_entry"]["id"], 30)
        self.assertEqual(events["timer"]["id"], 2)
        consts = {x["name"]: x for x in self.report["constants"]}
        self.assertIn("PERMISSION_DEBIT", consts)
        self.assertIn("TRUE", consts)


if __name__ == "__main__":
    unittest.main()

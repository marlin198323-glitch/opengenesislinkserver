#!/usr/bin/env python3
"""Unit tests for configuration-only, secret-free deployment diagnostics."""
import importlib.util
import unittest
from pathlib import Path

spec = importlib.util.spec_from_file_location(
    "preflight", Path(__file__).with_name("ogl_voice_pathfinding_preflight.py"))
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)


class PreflightTests(unittest.TestCase):
    def test_read_ini_ignores_comments_and_keeps_effective_last_value(self):
        cfg = module.parse_opensim_ini("""
; [OGLVoice] Enabled = true
[OGLVoice]
Enabled = false
Enabled = true ; override
MediaGatewayUrl = "https://media.example.org/internal/webrtc/v1/exchange"
SharedKey = "must-not-print"
""")
        self.assertTrue(module.enabled(cfg, "OGLVoice"))
        self.assertEqual(module.setting(cfg, "OGLVoice", "MediaGatewayUrl"),
                         "https://media.example.org/internal/webrtc/v1/exchange")
        self.assertNotIn("must-not-print", str(module.assess({}, cfg)))

    def test_no_false_navmesh_success(self):
        sim = module.parse_opensim_ini("[OGLPathfinding]\nEnabled = true\n")
        issues = module.assess(sim, viewer_log="cannot find capability 'RetrieveNavMeshSrc'")
        codes = {x["code"] for x in issues}
        self.assertIn("NATIVE_TERRAIN_ENABLED", codes)
        self.assertIn("FIRESTORM_NAVMESH_CAP_MISSING", codes)

    def test_missing_gateway_and_old_vivox(self):
        sim = module.parse_opensim_ini("[NexVerseNodeAgent]\nEnabled = true\n")
        robust = module.parse_opensim_ini(
            "[OGLVoice]\nEnabled = true\nEnableFirestormGateway = false\nMediaGatewayUrl = \"\"")
        issues = module.assess(sim, robust, "cannot POST url ''\nUnable to provision voice account.")
        codes = {x["code"] for x in issues}
        self.assertIn("ROBUST_FIRESTORM_GATEWAY_DISABLED", codes)
        self.assertIn("ROBUST_GATEWAY_URL_INVALID", codes)
        self.assertIn("FIRESTORM_VIVOX_FALLBACK", codes)

    def test_configured_not_claimed_functional(self):
        sim = module.parse_opensim_ini(
            "[NexVerseNodeAgent]\nEnabled = true\n[OGLPathfinding]\nEnabled = true")
        robust = module.parse_opensim_ini("""
[OGLVoice]
Enabled = true
EnableFirestormGateway = true
MediaGatewayUrl = "https://media.example.net/internal/webrtc/v1/exchange"
""")
        issues = module.assess(sim, robust)
        code = {x["code"]: x for x in issues}
        self.assertEqual(code["ROBUST_GATEWAY_CONFIGURED"]["severity"], "configuration-only")
        self.assertNotIn("success", [x["severity"] for x in issues])

    def test_media_url_avoids_unsafe_targets(self):
        for value in (
            "https://voice.example.org", "wss://livekit.example.org",
            "http://media.example.org/internal/webrtc/v1/exchange",
            "https://name:secret@media.example.org/internal/webrtc/v1/exchange",
            "https://VOICE-MEDIA-BRIDGE-HOST/internal/webrtc/v1/exchange",
            "https://example.org/internal/webrtc/v1/exchange#frag"
        ):
            self.assertFalse(module.media_url_valid(value), value)
        self.assertTrue(module.media_url_valid(
            "https://media.example.org/internal/webrtc/v1/exchange"))
        self.assertTrue(module.media_url_valid(
            "http://127.0.0.1:19098/internal/webrtc/v1/exchange"))

    def test_standalone_requires_key_in_process_environment(self):
        sim = module.parse_opensim_ini("""
[OGLVoice]
Enabled = true
Mode = "Standalone"
TenantId = "nexverse"
EnableFirestormGateway = true
MediaGatewayUrl = "https://media.example.org/internal/webrtc/v1/exchange"
""")
        codes = {x["code"] for x in module.assess(sim)}
        self.assertIn("STANDALONE_GATEWAY_CONFIGURED", codes)
        self.assertIn("STANDALONE_SIGNATURE_KEY_UNVERIFIED", codes)


    def test_09310_unconfigured_experience_and_npc_gate(self):
        # Match the abbreviated flags shipped by bin/OpenSim.ini.
        sim = module.parse_opensim_ini("""
[NexExperiencesViewer]
Enabled = true
WorldApiBaseUrl = "https://world.stadt-nexverse.de"
ApiKey = "${Environment|NEXVERSE_EXPERIENCES_API_KEY}"
[OGLPathfinding]
Enabled = false
""")
        findings = {x["code"]: x for x in module.assess(sim)}
        for code in (
            "EXPERIENCE_READ_CAPS_DISABLED",
            "EXPERIENCE_PERMISSION_CAPS_DISABLED",
            "EXPERIENCE_CONSENT_DISABLED",
            "EXPERIENCE_NATIVE_PROMPT_DISABLED",
            "EXPERIENCE_VIEWER_KEY_ABSENT",
            "NPC_FOLLOWER_DISABLED",
            "NAV_3D_LAYERS_DISABLED",
            "NATIVE_TERRAIN_DISABLED",
            "EXPERIENCE_VIEWER_CREATE_UNSUPPORTED",
        ):
            self.assertIn(code, findings, code)
        self.assertEqual(findings["EXPERIENCE_VIEWER_CREATE_UNSUPPORTED"]["severity"],
                         "known-limitation")

    def test_09310_opted_in_still_does_not_claim_live_success(self):
        sim = module.parse_opensim_ini("""
[NexExperiencesViewer]
Enabled = true
FirestormReadCaps = true
FirestormPermissionCaps = true
ScriptPendingConsent = true
NativeExperiencePrompt = true
ApiKey = "${Environment|NEXVERSE_EXPERIENCES_API_KEY}"
ViewerPermissionsApiKey = "${Environment|NEXVERSE_EXPERIENCES_VIEWER_PERMISSIONS_API_KEY}"
[OGLPathfinding]
Enabled = true
UseVerifiedLayeredSurfaces = true
PhysicsRaycastLayeredSurfaces = true
[OGLNpcNavigation]
Enabled = true
""")
        findings = module.assess(sim)
        codes = {x["code"] for x in findings}
        for disabled in ("EXPERIENCE_CONSENT_DISABLED",
                         "EXPERIENCE_PERMISSION_CAPS_DISABLED",
                         "NPC_FOLLOWER_DISABLED", "NATIVE_TERRAIN_DISABLED"):
            self.assertNotIn(disabled, codes, disabled)
        for configured in ("EXPERIENCE_KEYS_CONFIGURED_ONLY",
                           "NPC_FOLLOWER_CONFIGURED", "NAV_3D_PHYSICS_CONFIGURED",
                           "EXPERIENCE_VIEWER_CREATE_UNSUPPORTED"):
            self.assertIn(configured, codes, configured)
        self.assertTrue(all(x["severity"] != "success" for x in findings))

    def test_09310_duplicate_experience_keys_are_flagged_without_leak(self):
        sim = module.parse_opensim_ini("""
[NexExperiencesViewer]
Enabled = true
ApiKey = "example-DO-NOT-PRINT-key"
ViewerPermissionsApiKey = "example-DO-NOT-PRINT-key"
""")
        findings = module.assess(sim)
        self.assertIn("EXPERIENCE_KEYS_NOT_SEPARATE",
                      {x["code"] for x in findings})
        self.assertNotIn("example-DO-NOT-PRINT-key", str(findings))


    def test_09310_viewer_acquire_opt_in_and_private_service_key(self):
        sim = module.parse_opensim_ini("""
[NexExperiencesViewer]
Enabled = true
FirestormReadCaps = true
FirestormPermissionCaps = true
FirestormManageCaps = true
ScriptPendingConsent = true
NativeExperiencePrompt = true
ApiKey = "${Environment|NEXVERSE_EXPERIENCES_API_KEY}"
ViewerPermissionsApiKey = "${Environment|NEXVERSE_EXPERIENCES_VIEWER_PERMISSIONS_API_KEY}"
ViewerManageApiKey = "${Environment|NEXVERSE_EXPERIENCES_VIEWER_MANAGE_API_KEY}"
""")
        codes = {entry["code"] for entry in module.assess(sim)}
        self.assertIn("EXPERIENCE_VIEWER_MANAGE_CONFIGURED", codes)
        self.assertNotIn("EXPERIENCE_VIEWER_CREATE_UNSUPPORTED", codes)
        self.assertNotIn("EXPERIENCE_VIEWER_MANAGE_KEY_REUSED", codes)
        self.assertNotIn("EXPERIENCE_VIEWER_MANAGE_KEY_MISSING", codes)
        duplicate = module.parse_opensim_ini("""
[NexExperiencesViewer]
Enabled = true
FirestormReadCaps = true
FirestormPermissionCaps = true
FirestormManageCaps = true
ApiKey = "never-print-this"
ViewerPermissionsApiKey = "other-secret-value"
ViewerManageApiKey = "other-secret-value"
""")
        bad = module.assess(duplicate)
        self.assertIn("EXPERIENCE_VIEWER_MANAGE_KEY_REUSED",
                      {entry["code"] for entry in bad})
        self.assertNotIn("never-print-this", str(bad))
        self.assertNotIn("other-secret-value", str(bad))


if __name__ == "__main__":
    unittest.main()

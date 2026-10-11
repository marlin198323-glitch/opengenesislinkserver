#!/usr/bin/env python3
"""Read-only OGLVoice / native navigation readiness check for a real deployment.

Does not contact the grid, print credentials, modify files or claim a
Firestorm Havok NavMesh implementation that does not exist.
"""
import argparse
import json
import re
from pathlib import Path
from urllib.parse import urlsplit

ASSIGNMENT = re.compile(r"^\s*([A-Za-z][A-Za-z0-9_.-]*)\s*=\s*(.*?)\s*$")
SECTION = re.compile(r"^\s*\[([^\]]+)\]\s*$")


def parse_opensim_ini(text):
    """Parse simple active INI keys without expanding environment secrets.

    OpenSim includes and Nini interpolations require the running process for
    authoritative effective values: this check only inspects supplied files.
    """
    sections = {}
    section = None
    for raw in text.splitlines():
        stripped = raw.strip()
        if not stripped or stripped.startswith((";", "#")):
            continue
        found = SECTION.match(raw)
        if found:
            section = found.group(1).strip().lower()
            sections.setdefault(section, {})
            continue
        if section is None:
            continue
        found = ASSIGNMENT.match(raw)
        if found:
            value = found.group(2).strip()
            if value and value[0] in ('"', "'"):
                quote = value[0]
                last = value.find(quote, 1)
                value = value[1:last] if last != -1 else value.strip(quote)
            else:
                value = value.split(";", 1)[0].strip()
            sections[section][found.group(1).lower()] = value
    return sections


def setting(cfg, section, name, fallback=""):
    return cfg.get(section.lower(), {}).get(name.lower(), fallback)


def enabled(cfg, section, name="Enabled", fallback=False):
    return setting(cfg, section, name, "true" if fallback else "false").lower() in ("yes", "true", "on", "1")


def configured(value):
    return bool(value and value.strip() and "${" not in value and
                not re.search(r"(REPLACE|DEIN-|VOICE-MEDIA-BRIDGE-HOST|YOUR_)", value, flags=re.I))


def media_url_valid(value):
    if not configured(value):
        return False
    try:
        uri = urlsplit(value)
        if uri.scheme != "https" and not (
            uri.scheme == "http" and uri.hostname in ("127.0.0.1", "localhost", "::1")
        ):
            return False
        return bool(uri.hostname and uri.path == "/internal/webrtc/v1/exchange"
                    and not uri.username and not uri.password and not uri.fragment)
    except ValueError:
        return False


def assess(sim_cfg, robust_cfg=None, viewer_log=""):
    findings = []
    def add(area, code, severity, message):
        findings.append({"area": area, "code": code, "severity": severity, "message": message})

    # Release 0.9.3.10 native test prerequisites, deliberately read-only.
    # All values are configuration hints, NOT proof of live authorization.
    # The versioned bin/OpenSim.ini currently omits these opt-in flags,
    # which makes the Firestorm/LSL and NPC acceptance gates untestable.
    xp_enabled = enabled(sim_cfg, "NexExperiencesViewer")
    if not xp_enabled:
        add("experiences", "EXPERIENCES_MODULE_DISABLED", "action",
            "NexExperiencesViewer.Enabled is absent or false.")
    else:
        add("experiences", "EXPERIENCES_MODULE_CONFIGURED", "configuration-only",
            "Region Experience adapter requested; live World API and viewer not verified.")
        flags = (
            ("FirestormReadCaps", "EXPERIENCE_READ_CAPS_DISABLED"),
            ("FirestormPermissionCaps", "EXPERIENCE_PERMISSION_CAPS_DISABLED"),
            ("ScriptPendingConsent", "EXPERIENCE_CONSENT_DISABLED"),
            ("NativeExperiencePrompt", "EXPERIENCE_NATIVE_PROMPT_DISABLED"),
        )
        for key, code in flags:
            if not enabled(sim_cfg, "NexExperiencesViewer", key):
                add("experiences", code, "action",
                    "Set NexExperiencesViewer." + key +
                    "=true only on isolated test regions with the correctly scoped keys.")
        script_key = setting(sim_cfg, "NexExperiencesViewer", "ApiKey")
        viewer_key = setting(sim_cfg, "NexExperiencesViewer", "ViewerPermissionsApiKey")
        if not script_key.strip():
            add("experiences", "EXPERIENCE_SCRIPT_KEY_ABSENT", "action",
                "Set ApiKey to a scoped experiences:script reference (not a literal secret).")
        if not viewer_key.strip():
            add("experiences", "EXPERIENCE_VIEWER_KEY_ABSENT", "action",
                "Set ViewerPermissionsApiKey to a separate experiences:viewer:permissions key reference.")
        if script_key and viewer_key and script_key == viewer_key:
            add("experiences", "EXPERIENCE_KEYS_NOT_SEPARATE", "action",
                "Script and viewer permissions key references are identical; must be separate.")
        if script_key and viewer_key and script_key != viewer_key:
            add("experiences", "EXPERIENCE_KEYS_CONFIGURED_ONLY", "configuration-only",
                "Key references are different. Env interpolation, actual scopes, "
                "World API authentication and reachability require live verification.")
        if not enabled(sim_cfg, "NexExperiencesViewer", "FirestormReadCaps") or \
                not enabled(sim_cfg, "NexExperiencesViewer", "FirestormPermissionCaps"):
            add("experiences", "EXPERIENCE_VIEWER_LISTS_UNAVAILABLE", "action",
                "Firestorm Experience read/list/preferences CAPS cannot be tested until "
                "FirestormReadCaps and FirestormPermissionCaps are enabled.")
        # Acquire/Edit is an independent opt-in, with its OWN privileged
        # simulator credential. No amount of permission read CAPS can replace
        # a valid purchase POST or an authenticated UpdateExperience request.
        if not enabled(sim_cfg, "NexExperiencesViewer", "FirestormManageCaps"):
            add("experiences", "EXPERIENCE_VIEWER_CREATE_UNSUPPORTED", "action",
                "AgentExperiences purchase POST and UpdateExperience are disabled. "
                "Provision a separate experiences:viewer:manage scoped key and "
                "enable FirestormManageCaps only on the developer simulator.")
        else:
            add("experiences", "EXPERIENCE_VIEWER_MANAGE_CONFIGURED", "configuration-only",
                "Free Acquire Experience and limited basic profile editing requested. "
                "No live Firestorm GET/POST, ownership, key or quota verification yet.")
            manage_key = setting(sim_cfg, "NexExperiencesViewer", "ViewerManageApiKey")
            if not manage_key.strip():
                add("experiences", "EXPERIENCE_VIEWER_MANAGE_KEY_MISSING", "action",
                    "Set ViewerManageApiKey to a separate admin-issued "
                    "experiences:viewer:manage service key reference.")
            elif manage_key == script_key or manage_key == viewer_key:
                add("experiences", "EXPERIENCE_VIEWER_MANAGE_KEY_REUSED", "action",
                    "The privileged viewer manage key must not share the "
                    "script or permission key reference.")
            if not enabled(sim_cfg, "NexExperiencesViewer", "FirestormReadCaps") or \
                    not enabled(sim_cfg, "NexExperiencesViewer", "FirestormPermissionCaps"):
                add("experiences", "EXPERIENCE_VIEWER_MANAGE_DEPENDENCY", "action",
                    "FirestormManageCaps needs read and permission CAPS enabled.")

    if not enabled(sim_cfg, "OGLNpcNavigation"):
        add("pathfinding", "NPC_FOLLOWER_DISABLED", "action",
            "OGLNpcNavigation.Enabled is absent or false. Native NPC route tests "
            "need this independent opt-in plus OGLPathfinding.Enabled=true.")
    else:
        add("pathfinding", "NPC_FOLLOWER_CONFIGURED", "configuration-only",
            "OGLNpcNavigation is enabled; owner authorization, actual NPC placement "
            "and runtime IOglNpcRouteService tests are still required.")
    if not enabled(sim_cfg, "OGLPathfinding", "UseVerifiedLayeredSurfaces"):
        add("pathfinding", "NAV_3D_LAYERS_DISABLED", "action",
            "Verified layered navigation is off. NAV02/NAV03 (bridges/ramps) "
            "cannot be accepted using terrain-only routes.")
    elif not enabled(sim_cfg, "OGLPathfinding", "PhysicsRaycastLayeredSurfaces"):
        add("pathfinding", "NAV_3D_PHYSICS_PROVIDER_UNVERIFIED", "configuration-only",
            "UseVerifiedLayeredSurfaces=true but physics raycast source is not "
            "enabled. A real external verified surface provider must exist.")
    else:
        add("pathfinding", "NAV_3D_PHYSICS_CONFIGURED", "configuration-only",
            "Layered physics probes requested. Collision certification, "
            "stair/ramp transitions and real mesh coverage remain live tests.")

    if "RetrieveNavMeshSrc" in viewer_log:
        add("pathfinding", "FIRESTORM_NAVMESH_CAP_MISSING", "known-limitation",
            "Firestorm requested RetrieveNavMeshSrc. Native terrain A* does not provide "
            "a Second Life-compatible binary NavMesh/Havok payload; no fake CAP is advertised.")
    if enabled(sim_cfg, "OGLPathfinding"):
        add("pathfinding", "NATIVE_TERRAIN_ENABLED", "info",
            "OGLPathfinding.Enabled=true; native terrain navigation may operate after initial snapshot. "
            "This does NOT enable Firestorm NavMesh UI.")
    else:
        add("pathfinding", "NATIVE_TERRAIN_DISABLED", "action",
            "OGLPathfinding.Enabled is false or absent. Enable only for a controlled native A*/NPC test; "
            "not a solution for RetrieveNavMeshSrc.")

    if "Unable to provision voice account" in viewer_log or "cannot POST url ''" in viewer_log:
        add("voice", "FIRESTORM_VIVOX_FALLBACK", "observed",
            "Viewer attempted legacy Vivox provisioning with an empty URL. "
            "A verified OGLVoice VoiceServerType=webrtc provider was not established in this log.")

    if not enabled(sim_cfg, "OGLVoiceViewer", fallback=True):
        add("voice", "VIEWER_ADAPTER_DISABLED", "action",
            "OGLVoiceViewer.Enabled=false. WebRTC CAPS are intentionally disabled.")
        return findings

    sim_mode = setting(sim_cfg, "OGLVoice", "Mode", "GridManaged").lower()
    standalone = enabled(sim_cfg, "OGLVoice") and sim_mode == "standalone"
    if standalone:
        gw = setting(sim_cfg, "OGLVoice", "MediaGatewayUrl")
        if (not enabled(sim_cfg, "OGLVoice", "EnableFirestormGateway") or
                not media_url_valid(gw)):
            add("voice", "STANDALONE_GATEWAY_NOT_READY", "action",
                "Standalone must enable the Firestorm gateway and supply a verified HTTPS "
                "MediaGatewayUrl ending /internal/webrtc/v1/exchange.")
        else:
            add("voice", "STANDALONE_GATEWAY_CONFIGURED", "configuration-only",
                "Standalone gateway URI is configured; live bridge/LiveKit/ICE connectivity is NOT verified.")
        if not configured(setting(sim_cfg, "OGLVoice", "TenantId")):
            add("voice", "STANDALONE_TENANT_MISSING", "action", "Standalone OGLVoice.TenantId is not configured.")
        # The standalone signing key is injected at process start, not in INI.
        add("voice", "STANDALONE_SIGNATURE_KEY_UNVERIFIED", "action",
            "Check process environment OGLVOICE_MEDIA_BRIDGE_SHARED_KEY and node ID; "
            "this tool deliberately does not read or print secrets.")
    else:
        if not enabled(sim_cfg, "NexVerseNodeAgent"):
            add("voice", "NODE_AGENT_DISABLED", "action",
                "GridManaged OGLVoice requires NexVerseNodeAgent.Enabled=true on the simulator.")
        else:
            add("voice", "NODE_AGENT_CONFIGURED", "configuration-only",
                "NexVerseNodeAgent is enabled; actual trusted discovery/authentication is unverified.")
        if robust_cfg is None:
            add("voice", "ROBUST_CONFIG_UNAVAILABLE", "action",
                "Supply --robust-config to check central OGLVoice enablement and media gateway URI.")
        else:
            if not enabled(robust_cfg, "OGLVoice"):
                add("voice", "ROBUST_VOICE_DISABLED", "action",
                    "Central Robust [OGLVoice] Enabled is false or absent.")
            if not enabled(robust_cfg, "OGLVoice", "EnableFirestormGateway"):
                add("voice", "ROBUST_FIRESTORM_GATEWAY_DISABLED", "action",
                    "Central Robust EnableFirestormGateway=false: Firestorm WebRTC provider is not advertised.")
            if not media_url_valid(setting(robust_cfg, "OGLVoice", "MediaGatewayUrl")):
                add("voice", "ROBUST_GATEWAY_URL_INVALID", "action",
                    "Central MediaGatewayUrl must point to the real protected HTTPS media exchange "
                    "path /internal/webrtc/v1/exchange, not the LiveKit SFU or voice portal.")
            elif enabled(robust_cfg, "OGLVoice", "EnableFirestormGateway"):
                add("voice", "ROBUST_GATEWAY_CONFIGURED", "configuration-only",
                    "Central media URL is plausible; gateway uptime and LiveKit/ICE are not tested.")
    return findings


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--sim-config", required=True, type=Path)
    parser.add_argument("--robust-config", type=Path)
    parser.add_argument("--viewer-log", type=Path)
    parser.add_argument("--json", action="store_true", help="Emit machine-readable findings")
    args = parser.parse_args()
    for file in (args.sim_config, args.robust_config, args.viewer_log):
        if file is not None and not file.is_file():
            parser.error("Input file does not exist: " + str(file))
    sim = parse_opensim_ini(args.sim_config.read_text(encoding="utf-8", errors="replace"))
    robust = (parse_opensim_ini(args.robust_config.read_text(encoding="utf-8", errors="replace"))
              if args.robust_config else None)
    log = (args.viewer_log.read_text(encoding="utf-8", errors="replace")
           if args.viewer_log else "")
    findings = assess(sim, robust, log)
    if args.json:
        print(json.dumps({"findings": findings}, indent=2, ensure_ascii=False))
    else:
        print("OpenGenesisLINK Voice / Pathfinding Preflight (read-only)")
        for item in findings:
            print(f"[{item['area']}][{item['severity']}] {item['code']}: {item['message']}")
        print("NOTE: INI includes, env interpolation, processes and actual media health "
              "require separate live verification.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

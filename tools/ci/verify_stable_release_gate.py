#!/usr/bin/env python3
"""Keep the immutable 0.9.3.8 publisher isolated from newer dev main commits."""
from pathlib import Path

flow = Path(".github/workflows/publish-0.9.3.8-stable.yml").read_text()
required = (
    "workflows: [\"NexVerse CI\"]",
    "branches: [main]",
    "github.event.workflow_run.conclusion == 'success'",
    "github.event.workflow_run.event == 'push'",
    'test "$(git rev-parse HEAD)" = "$STABLE_SHA"',
    'VersionNumber = "0.9.3.8"',
    "VERSION_FLAVOUR = Flavour.Release;",
    "test -s doc/NexVerse/RELEASE_0.9.3.8.md",
    'echo "eligible=false" >> "$GITHUB_OUTPUT"',
    'echo "eligible=true" >> "$GITHUB_OUTPUT"',
    "if: steps.stable_check.outputs.eligible == 'true'",
    'tag="v0.9.3.8"',
    "Stable release already exists; immutable publication remains unchanged.",
    "Refusing to reuse or overwrite it.",
)
for mark in required:
    assert mark in flow, f"immutable stable release safety gate missing: {mark}"
assert flow.count("gh release create") == 1
assert "gh release delete" not in flow and "git push --force" not in flow
# The 0.9.3.10 train is a stabilization release; deferred viewer features
# must not be smuggled back as RC1 feature-acceptance prerequisites.
# Keep the new runtime/economy/inventory/security/restore gates fail-closed.
release = Path("doc/NexVerse/RELEASE_TRAIN_0.9.3.10.md").read_text()
readiness = Path("doc/NexVerse/09310_RC1_RELEASE_READINESS.md").read_text()
for mark in ("OGL Pathfinding", "**Gate 1 – Funktional:**",
             "**Gate 5 – Operator:**", "**Gate 6 – Release:**",
             "NexExperiences/Firestorm ist pausiert", "23-Fall-v2-Matrix"):
    assert mark in release, f"0.9.3.10 revised scope missing: {mark}"
assert release.count("- [ ] **Gate ") == 6
for mark in ("22 Pflichtfälle für RC1, 23 für Stable",
             "SEC_XP_OFF", "ECON_MASTERBANK", "PERF_BASELINE",
             "RECOVERY_RESTORE", "ogl-release-evidence-v2"):
    assert mark in readiness, f"stabilization scope missing: {mark}"
# A paused, unaccepted viewer adapter cannot be enabled in shipped
# simulator templates. The existing code/security regressions still run.
for ini in ("bin/OpenSim.ini", "bin/OpenSim.ini.example"):
    body = Path(ini).read_text()
    section = body.split("[NexExperiencesViewer]", 1)[1].split(
        "\n[", 1)[0]
    assert "Enabled = false" in section, (
        "0.9.3.10 must not ship active Experience viewer CAPS: " + ini)
assert Path("doc/NexVerse/FUTURE_VIEWER_NAVIGATION.md").is_file()
print("0.9.3.8 immutable; 0.9.3.10 stabilization gates, XP disabled, Viewer 2.0 deferred.")

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
# OGL Pathfinding is fully excluded from the 0.9.3.10 server release.
# Keep all remaining Experiences/Runtime gates fail-closed until live testing.
release = Path("doc/NexVerse/RELEASE_TRAIN_0.9.3.10.md").read_text()
readiness = Path("doc/NexVerse/09310_RC1_RELEASE_READINESS.md").read_text()
for mark in ("OGL Pathfinding", "**Gate 1 – Funktional:**",
             "**Gate 5 – Operator:**", "**Gate 6 – Release:**",
             "Experience", "40-Fall-Abnahmematrix"):
    assert mark in release, f"0.9.3.10 revised scope missing: {mark}"
assert release.count("- [ ] **Gate ") == 6
assert "39 von 40" in readiness and "40/40" in readiness
assert "NAV01–NAV08" in readiness and "keine** 0.9.3.10-Gates" in readiness
assert Path("doc/NexVerse/FUTURE_VIEWER_NAVIGATION.md").is_file()
print("0.9.3.8 immutable; 0.9.3.10 runtime/Experiences gates retained, OGL Pathfinding deferred.")

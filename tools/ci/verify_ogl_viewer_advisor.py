#!/usr/bin/env python3
"""Structural regression guard for the disabled-by-default OGL Viewer Advisor.

This verifies source/config wiring, not the runtime C# behavior: run manual
simulator tests described in doc/NexVerse/OGL_VIEWER_ADVISOR.md as well.
"""
from pathlib import Path
import re

root = Path(__file__).resolve().parents[2]
src = (root / "NexVerse/RegionModules/ViewerAdvisor/OGLViewerAdvisorModule.cs").read_text()
prebuild = (root / "prebuild.xml").read_text()
assert 'Project name="NexVerse.RegionModules"' in prebuild
assert '<Match pattern="*.cs" recurse="true">' in prebuild
for token in (
    'ISharedRegionModule',
    'Id = "OGLViewerAdvisorModule"',
    'Configs["OGLViewerAdvisor"]',
    'GetBoolean("Enabled", false)',
    'TimeSpan.FromMinutes(60)',
    'OnMakeRootAgent += OnMakeRootAgent',
    'OnMakeRootAgent -= OnMakeRootAgent',
    'ForEachRootScenePresence(CheckAndNotify)',
    'GetAgentCircuitData(',
    'MatchesOfficialViewer(circuit.Channel, m_OfficialPrefixes)',
    'MatchesOfficialViewer(circuit.Viewer, m_OfficialPrefixes)',
    'ChatTypeEnum.Owner',
    'm_LastSentUtc.TryGetValue',
    'now - last < m_Interval',
):
    assert token in src, token

for rel in ("bin/OpenSim.ini", "bin/OpenSim.ini.example"):
    ini = (root / rel).read_text()
    assert ini.count("[OGLViewerAdvisor]") == 1, rel
    section = ini.split("[OGLViewerAdvisor]", 1)[1].split("\n[", 1)[0]
    assert re.search(r"(?m)^\s*Enabled\s*=\s*false\s*$", section), rel
    assert re.search(r"(?m)^\s*IntervalMinutes\s*=\s*60\s*$", section), rel
    assert "OfficialViewerPrefixes" in section, rel

print("OK: OGL viewer advisor wired in, disabled in both simulator INIs")

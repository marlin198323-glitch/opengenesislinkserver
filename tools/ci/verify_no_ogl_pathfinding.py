#!/usr/bin/env python3
"""Ensure the retired OGL Pathfinding implementation stays out of 0.9.3.10."""
from pathlib import Path
import re

root = Path(__file__).resolve().parents[2]
for package in ("NexVerse/Core/Pathfinding", "NexVerse/RegionModules/Pathfinding"):
    assert not (root / package).exists(), "retired OGL Pathfinding source: " + package
assert not (root / "OpenSim/Region/Framework/Interfaces/IOglNativeTerrainQuery.cs").exists()
for rel in ("bin/OpenSim.ini", "bin/OpenSim.ini.example"):
    cfg = (root / rel).read_text()
    assert not re.search(r"(?m)^\[(OGLPathfinding|OGLNpcNavigation)\]$", cfg), rel
    assert "FirestormNavMeshCaps =" not in cfg, rel
    assert "OpenNavGraphCaps =" not in cfg, rel
api = (root / "OpenSim/Region/ScriptEngine/Shared/Api/Implementation/LSL_Api.cs").read_text()
assert "IOglNativeTerrainQuery" not in api
assert "ScriptBaseClass.PU_FAILURE_NO_NAVMESH" in api
assert "public LSL_List llGetClosestNavPoint(" in api
assert "return new LSL_List();" in api
release = (root / "tools/ci/verify_09310_release_readiness.py").read_text()
assert '"NAV":' not in release and '"NAV08"' not in release
assert "assert len(REQUIRED_CASES) == 23" in release
assert "SEC_XP_OFF" in release and "ECON_MASTERBANK" in release
print("0.9.3.10 server: OGL Pathfinding fully excluded; LSL gracefully unsupported.")

#!/usr/bin/env python3
"""Static regression guard for OpenGenesisLINK native region dimensions."""
from pathlib import Path
import re

root = Path(__file__).resolve().parents[2]
api = (root / "OpenSim/Region/ScriptEngine/Shared/Api/Implementation/LSL_Api.cs").read_text(encoding="utf-8")
interface = (root / "OpenSim/Region/ScriptEngine/Shared/Api/Interface/ILSL_Api.cs").read_text(encoding="utf-8")
stub = (root / "OpenSim/Region/ScriptEngine/Shared/Api/Runtime/LSL_Stub.cs").read_text(encoding="utf-8")
ossl = (root / "OpenSim/Region/ScriptEngine/Shared/Api/Implementation/OSSL_Api.cs").read_text(encoding="utf-8")
region_info = (root / "OpenSim/Framework/RegionInfo.cs").read_text(encoding="utf-8")

signature = "LSL_Vector oglGetRegionDimensions()"
assert signature in interface, "missing native LSL API contract"
assert "public " + signature in api, "missing native implementation"
assert "public " + signature in stub, "missing script-facing wrapper"
assert "return m_LSL_Functions.oglGetRegionDimensions();" in stub
body = re.search(r"public LSL_Vector oglGetRegionDimensions\(\)\s*\{([^{}]*)\}", api)
assert body, "missing or unexpectedly changed function body"
for axis in ("X", "Y", "Z"):
    assert f"region.RegionSize{axis}" in body.group(1), f"missing {axis} dimension"
assert "World.RegionInfo" in body.group(1), "dimension must use active scene, not a global constant"
assert "RegionSizeZ" in region_info, "configured scene Z dimension is unavailable"

# This is a separate OGL LSL extension: no regression to legacy osGetRegionSize().
old = re.search(r"public LSL_Vector osGetRegionSize\(\)\s*\{([^{}]*)\}", ossl)
assert old, "legacy OSSL function missing"
assert "0.0f" in old.group(1), "legacy osGetRegionSize Z behaviour changed"
assert "oglGetRegionDimensions" not in ossl, "OGL extension must stay outside OSSL gating"

print("OGL region dimensions: LSL interface, wrapper, XYZ and legacy OSSL behaviour OK")

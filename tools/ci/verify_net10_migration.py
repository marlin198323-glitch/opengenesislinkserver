#!/usr/bin/env python3
"""Require SDK-style OGL projects to target .NET 10 for pre-0.9.3.13."""
from pathlib import Path
import json
import re

root=Path(__file__).resolve().parents[2]
projects=[p for p in root.rglob("*.csproj") if "Prebuild" not in p.parts and ".git" not in p.parts]
assert len(projects) >= 90, f"Too few OGL csproj files: {len(projects)}"
old=[]
for p in projects:
    text=p.read_text(encoding="utf-8-sig")
    if re.search(r"<TargetFramework>net8\.0</TargetFramework>",text):
        old.append(str(p.relative_to(root)))
assert not old, f"Old net8.0 projects remain: {old}"
assert sum("<TargetFramework>net10.0</TargetFramework>" in p.read_text(encoding="utf-8-sig")
           for p in projects)>=90
for f in ("OpenSim","Robust"):
    config=json.loads((root/"bin"/f"{f}.runtimeconfig.json").read_text())
    assert config["runtimeOptions"]["tfm"]=="net10.0"
    assert config["runtimeOptions"]["framework"]["version"]=="10.0.0"
ci=(root/".github/workflows/nexjast-ci.yml").read_text()
assert "dotnet-version: '10.0.x'" in ci
assert "dotnet-version: '8.0.x'" not in ci
assert "tools/build/retarget_net10.py" in (root/"runprebuild.sh").read_text()
assert "retarget_net10.py" in (root/"runprebuild.bat").read_text()
assert "rollForward" in (root/"bin/prebuild.runtimeconfig.json").read_text()
print(f"OGL .NET 10 target: OK ({len(projects)} project files)")

#!/usr/bin/env python3
"""Guard the one-click NexGroups groups:read key setup in the World API UI."""

from pathlib import Path

root = Path(__file__).resolve().parents[2]
page = (root / "NexVerse/Server/Api/NexApiDocsPage.cs").read_text(encoding="utf-8")
keys = (root / "NexVerse/Core/Security/NexApiKeyStore.cs").read_text(encoding="utf-8")
docs = (root / "doc/NexVerse/NEXGROUPS_GRID_LAND_HISTORY.md").read_text(encoding="utf-8")
auth = (root / "NexVerse/Server/Api/NexVerseUserApi.cs").read_text(encoding="utf-8")

# The chooser and single-purpose shortcut are separate controls.
assert page.count('id="secretGroupLand"') == 1
assert page.count('id="secretGenerateGroupLand"') == 1
assert "<strong>NexGroups / Gridweite Landabfrage</strong>" in page
assert "<code>groups:read</code>" in page
assert "$('secretGenerateGroupLand').addEventListener('click',()=>secretGenerate(false,true));" in page

generator = page.split("async function secretGenerate(onlyRegistration=false, onlyGroupLand=false) {", 1)[1].split(
    "async function secretCopy()", 1
)[0]
# Quick-create must not inherit the checked Experiences/NexBus selections.
assert "const onlyOne=onlyRegistration||onlyGroupLand;" in generator
assert "const wantRegister=onlyRegistration||(!onlyOne&&$('secretRegister').checked);" in generator
assert "const wantGroupLand=onlyGroupLand||(!onlyOne&&$('secretGroupLand').checked);" in generator
for control in ("secretEconomy", "secretExperiences", "secretExperiencePermissions",
                "secretExperienceManage", "secretNexBus"):
    assert f"const want" in generator
    assert f"!onlyOne&&$('{control}').checked" in generator
assert "if(wantBus){" in generator
assert generator.index("if(wantBus){") < generator.index("const plan=[")
assert "{wanted:wantGroupLand,name:'OGL Simulator Group Land'," in generator
assert "scopes:['groups:read'],env:'NEXVERSE_GROUP_LAND_API_KEY'" in generator
assert "onlyGroupLand?" in generator
assert "$('secretGenerateGroupLand').disabled=true;" in generator
assert "$('secretGenerateGroupLand').disabled=false;" in generator
assert "const inventory=await secretJson('/api/v1/auth/api-keys'" in generator
assert "if(match)throw new Error(" in generator
assert "window.isSecureContext" in generator
assert "const token=await secretAdminToken();" in generator
assert "'Authorization':'Bearer '+token" in generator
assert "values[item.env]=data.api_key;" in generator
assert "secretBuildLines(values);" in generator

# The rendered copy block must contain the exact simulator env assignment.
builder = page.split("function secretBuildLines(values) {", 1)[1].split(
    "function secretKeyFingerprint", 1
)[0]
assert "'NEXVERSE_GROUP_LAND_API_KEY'" in builder
assert "name+'='+values[name]" in builder

# Reuse the real admin-only API key issuer; never issue wildcard/admin scopes.
assert "NexScopes.GroupsRead," in keys
# The machine allowlist is now a shared, explicit policy for create AND edit.
machine_scopes = keys.split("private static readonly HashSet<string> s_SupportedScopes", 1)[1].split(
    "public IReadOnlyCollection<string> SupportedScopes", 1
)[0]
assert "NexScopes.AdminAll" not in machine_scopes
assert "NexScopes.SecurityManage" not in machine_scopes
assert "NexScopes.GroupsRead" in machine_scopes
assert "NexScopes.StatisticsRead" in machine_scopes
assert 'NexScopes.AdminAll,' in auth
assert '"/api/v1/auth/api-keys"' in auth
assert "Nur Gruppenland-API-Key erstellen" in docs
assert "/etc/nexverse/nexverse.env" in docs

print("World API one-click group land key: scoped, isolated, and documented")

#!/usr/bin/env python3
"""Issue #289: verify personal bearer grants stay distinct from machine credentials.

Static contract test for the existing C# auth endpoints and the real World
Control Center JavaScript; native token semantics are tested in .NET.
"""
from pathlib import Path
import re
import shutil
import subprocess
import tempfile

root = Path(__file__).resolve().parents[2]
read = lambda name: (root / name).read_text(encoding="utf-8-sig")
api = read("NexVerse/Server/Api/NexVerseUserApi.cs")
policy = read("NexVerse/Core/Security/NexSecurity.cs")
ui = read("NexVerse/Server/Api/NexApiDocsPage.cs")

for marker in (
    'id="worldPersonalAuthorization"',
    'id="worldAuthPrincipal"',
    'id="worldAuthUseMe"',
    'id="worldAuthLoad"',
    'id="worldAuthSave"',
    "function worldAuthLoad()",
    "function worldAuthSave()",
    "worldAuthSame(latest.explicit_scopes,old.original)",
    "worldAuthSame(latest.roles,old.roles)",
    "body:JSON.stringify({scopes:next,expected_scopes:old.original,",
    "reauthentication_required",
    "worldAuthReset()",
    "adminLogout()",
):
    assert marker in ui, "World personal scope UI regression: " + marker

view = ui.split("/* Issue #289: Personal World-Bearer authorization", 1)[1].split(
    "/* Secrets console:", 1
)[0]
assert "X-NexVerse-Api-Key" not in view, "A machine credential entered the personal scope editor"
assert "economy:transfer" not in view and "admin:*" not in view, (
    "Issue #289 editor must not allow privileged grants"
)
assert "worldReadScopeChoices" in view
assert "/api/v1/auth/authorization-model" in view
assert "'/api/v1/users/'+encodeURIComponent(id)+'/authorization'" in view
assert "Promise.all([" in view

for marker in (
    '"expected_scopes"',
    '"expected_roles"',
    '"authorization_changed"',
    "HttpStatusCode.Conflict",
    "NexAuthorizationPolicy.JoinStoredValues(latest.ExplicitScopes)",
    "NexAuthorizationPolicy.JoinStoredValues(latest.Roles)",
    "m_Users.SetAuthorization(",
    "m_OAuthStore?.RevokeSubjectRefreshTokens(",
    "reauthentication_required = true",
):
    assert marker in api, "World personal scope API regression: " + marker

assert "NexScopes.AdminAll" in policy
assert "NexScopes.StatisticsRead" in policy
assert "NexScopes.EconomyRead" in policy

# Validate the real embedded JavaScript used by the Control Center rather
# than only checking marker strings or a detached sample.
scripts = re.findall(r"<script[^>]*>(.*?)</script>", ui, re.DOTALL)
assert scripts
if shutil.which("node") is None:
    raise SystemExit("Node.js is required for admin-page syntax verification")
with tempfile.TemporaryDirectory() as directory:
    for i, code in enumerate(scripts):
        target = Path(directory) / f"control-center-{i}.js"
        target.write_text(code, encoding="utf-8")
        subprocess.run(["node", "--check", str(target)], check=True)

print("Issue #289 World personal-scope editor, CAS, token and syntax gates PASS")

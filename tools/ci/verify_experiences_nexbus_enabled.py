#!/usr/bin/env python3
"""Regression guard: active NexBus, paused Experiences, admin-only API login."""
from pathlib import Path
import re
import shutil
import subprocess
import tempfile

ROOT=Path(__file__).resolve().parents[2]
ENV="$"+"{Environment|"

def contents(rel):
    return (ROOT/rel).read_text(encoding="utf-8")

def ini(rel, name):
    content=contents(rel)
    marker="["+name+"]"
    assert content.count(marker)==1,(rel,name)
    return content.split(marker,1)[1].split("\n[",1)[0]

for rel in ("bin/OpenSim.ini", "bin/OpenSim.ini.example"):
    agent=ini(rel,"NexVerseNodeAgent")
    exp=ini(rel,"NexExperiencesViewer")
    # 0.9.3.10 is a stabilization release: NexBus stays enabled,
    # but unaccepted Firestorm Experiences must be disabled by default.
    assert re.search(r"(?m)^\s*Enabled\s*=\s*true\s*$",agent),(rel,"NodeAgent")
    assert re.search(r"(?m)^\s*Enabled\s*=\s*false\s*$",exp),(rel,"ExperiencesViewer")
    assert 'SharedKey = "'+ENV+'NEXVERSE_NEXBUS_SHARED_KEY}"' in agent
    assert 'PeerUrl = "'+ENV+'NEXVERSE_NEXBUS_PEER_URL}"' in agent
    assert 'ApiKey = "'+ENV+'NEXVERSE_EXPERIENCES_API_KEY}"' in exp
    assert 'WorldApiBaseUrl = "https://world.stadt-nexverse.de"' in exp
    assert "http://hg.stadt-nexverse.de/internal/nexbus/v1/events" not in agent

for rel in ("bin/Robust.HG.ini","bin/Robust.HG.ini.example"):
    bus=ini(rel,"NexBus")
    assert re.search(r"(?m)^\s*Enabled\s*=\s*true\s*$",bus),rel
    assert 'SharedKey = "'+ENV+'NEXVERSE_NEXBUS_SHARED_KEY}"' in bus
    assert 'Peers = "'+ENV+'NEXVERSE_NEXBUS_PEERS}"' in bus

user=contents("NexVerse/Server/Api/NexVerseUserApi.cs")
admin_route='"/api/v1/auth/admin/session"'
assert admin_route in user
login=user.split("private void HandleNativeSessionLogin(",1)[1].split("private void HandleAuditSearch(",1)[0]
assert "bool requireAdmin = false" in login
assert "if (requireAdmin &&" in login
assert "user.UserLevel < Math.Max(200, m_AdminMinimumLevel)" in login
assert "NexScopes.AdminAll" in login
assert login.index("if (requireAdmin &&") < login.index("m_Tokens.Issue(")
assert '"administrator_required"' in login
assert "m_Security.IsTotpEnabled" in login
me=user.split("private void HandleAdminSessionInfo(",1)[1].split("private void HandleNativeSessionLogin(",1)[0]
assert "NexScopes.AdminAll" in me
assert "account.UserLevel < Math.Max(200, m_AdminMinimumLevel)" in me
assert "account == null" in me
assert '"Cache-Control", "no-store"' in me

connector=contents("NexVerse/Server/Api/NexVerseWorldApiConnector.cs")
assert "eventBus is DistributedNexEventBus" in connector
assert "System.Text.Encoding.UTF8.GetByteCount(sharedKey) < 32" in connector
assert "return new InMemoryNexEventBus();" in connector

store=contents("NexVerse/Core/Security/NexApiKeyStore.cs")
assert "NexScopes.ExperiencesScript" in store
assert "NexScopes.ExperiencesViewerPermissions" in store
assert "NexScopes.ExperiencesViewerManage" in store
assert "Firestorm Experience management credentials must have only experiences:viewer:manage." in store
assert "normalizedScopes.Length != 1" in store
assert "existing.Enabled" in store and "SameScopes(existing.Scopes, normalizedScopes)" in store
assert "record == null || record.Enabled" in store, "cannot delete enabled keys"
assert "LastUsedAt = value.LastUsedAt" in store
assert '"api_key_duplicate"' in user
assert '"api_key_still_enabled"' in user
assert "private void HandleApiKeyDelete(" in user
assert '"auth.api_key.delete"' in user
api=contents("NexVerse/Server/Api/NexVerseWorldApiHandlers.cs")
assert '"/api/v1/auth/api-keys/{keyId}"' in api
assert '"last_used_at"' in api
assert '"ApiKeyDeleteResponse"' in api
template=contents("bin/nexverse.env.example")
for name in ("NEXVERSE_EXPERIENCES_API_KEY",
             "NEXVERSE_EXPERIENCES_VIEWER_PERMISSIONS_API_KEY",
             "NEXVERSE_ECONOMY_API_KEY",
             "NEXVERSE_NEXBUS_SHARED_KEY"):
    assert name in template, name
assert admin_route in api
ui=contents("NexVerse/Server/Api/NexApiDocsPage.cs")
for marker in ('data-page="adminlogin"','id="page-adminlogin"','id="adminLoginPassword"',
               "'/api/v1/docs'","function adminIsActive()", "function adminLogout()",
               "'secrets','nodes','grid'", 'id="page-secrets"',
               "'/api/v1/auth/api-keys'","window.crypto.getRandomValues(bytes)"):
    assert marker in ui,marker
assert 'id="secretExperienceManage"' in ui
assert "'NEXVERSE_EXPERIENCES_VIEWER_MANAGE_API_KEY'" in ui
assert "scopes:['experiences:viewer:manage']" in ui
assert "const wantManage=!onlyOne&&$('secretExperienceManage').checked;" in ui
# Registration credential: the browser issues a dedicated server-only key,
# never a generic users:write/admin key. The shortcut must ignore existing
# Experiences/NexBus defaults (and must not demand NexBus peer URLs).
for marker in ('id="secretRegister"', 'id="secretGenerateRegistration"',
               "'NEXVERSE_REGISTER_API_KEY'",
               "scopes:['users:register'],env:'NEXVERSE_REGISTER_API_KEY'",
               "const wantRegister=onlyRegistration||(!onlyOne&&$('secretRegister').checked);",
               "const wantBus=!onlyOne&&$('secretNexBus').checked;",
               "secretGenerate(true)"):
    assert marker in ui, f"Registration API key control missing: {marker}"
assert "let secretIssuePending=false;" in ui
assert "$('secretGenerateRegistration').disabled=true;" in ui
assert "$('secretGenerateRegistration').disabled=false;" in ui
assert "NEXVERSE_REGISTER_API_KEY" in contents("doc/NexVerse/PORTAL_REGISTRATION_IAR_HARDENING.md")
portal_api=contents("NexVerse/Server/Api/OglPortalRegistrationApi.cs")
assert "NexScopes.UsersRegister" in portal_api
assert 'request.Headers?["X-NexVerse-Api-Key"]' in portal_api
assert 'principal.Subject.StartsWith("api-key:"' in portal_api
assert "localStorage.setItem(" not in ui
assert "sessionStorage.setItem(" not in ui
assert 'id="secretAdminPassword"' not in ui, "secrets must use shared admin session"
for marker in ('id="secretExperiencePermissions"', 'id="secretLoadKeys"',
               'id="secretKeyList"',
               "async function secretManageKey(",
               "function secretKeyFingerprint(",
               "NEXVERSE_EXPERIENCES_VIEWER_PERMISSIONS_API_KEY"):
    assert marker in ui, f"key inventory UI missing: {marker}"

# Browser script is embedded in C# HTML raw string; parse syntax using Node.js when installed.
match=re.search(r"<script>\s*(.*?)\s*</script>",ui,re.S)
assert match,"Control Center JavaScript not found"
node=shutil.which("node")
if node:
    with tempfile.TemporaryDirectory() as directory:
        file=Path(directory)/"world-api-docs.js"
        file.write_text(match.group(1),encoding="utf-8")
        subprocess.run([node,"--check",str(file)],check=True)
else:
    print("Hinweis: Node.js nicht vorhanden; JS-Syntaxpruefung uebersprungen")

print("OpenGenesisLINK NexBus active + Experiences paused + admin-only login: OK")

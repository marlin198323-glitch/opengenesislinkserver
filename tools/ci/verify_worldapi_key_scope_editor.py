#!/usr/bin/env python3
"""Static World API machine-key grant change contract and browser JS syntax gate."""
import re
import subprocess
import tempfile
from pathlib import Path

root = Path(__file__).resolve().parents[2]
read = lambda path: (root / path).read_text(encoding="utf-8-sig")
store = read("NexVerse/Core/Security/NexApiKeyStore.cs")
api = read("NexVerse/Server/Api/NexVerseUserApi.cs")
view = read("NexVerse/Server/Api/NexApiDocsPage.cs")
openapi = read("NexVerse/Server/Api/NexVerseWorldApiHandlers.cs")
docs = read("doc/NexVerse/WORLD_API.md")

for marker in (
    "NexScopes.StatisticsRead",
    "public bool UpdateScopes(",
    "SameScopes(record.Scopes, expected)",
    "SameScopes(other.Scopes, desired)",
    "ValidateScopes(desired);",
    "SecretHash = value.SecretHash",
    "SecretSalt = value.SecretSalt",
    "s_SupportedScopes",
):
    assert marker in store, "Scope store regression: " + marker

for marker in (
    'NexScopes.AdminAll,',
    'out NexPrincipal principal,',
    'm_ApiKeys.UpdateScopes(keyId, scopes, expectedScopes)',
    '"api_key_scope_conflict"',
    '"invalid_api_key_scopes"',
    '"auth.api_key.scopes.update"',
    '"auth.api_key.scopes.changed"',
    'supported_scopes = m_ApiKeys.SupportedScopes',
    'response.AddHeader("Cache-Control", "no-store")',
):
    assert marker in api, "Missing scope REST contract: " + marker

for marker in (
    'function secretEditKeyScopes(key,scopes)',
    'Berechtigungen bearbeiten',
    'expected_scopes:previous',
    'response.supported_scopes||[]',
    'const allowed=new Set(supportedScopes||[])',
    'const selected=boxes.filter(box=>box.checked&&!box.disabled)',
    'window.confirm(',
    "check.type='checkbox'",
):
    assert marker in view, "Missing admin scope UI contract: " + marker

for marker in (
    '"ApiKeyScopeUpdateRequest"',
    '"ApiKeyUpdateRequest"',
    '"ApiKeyUpdateResponse"',
    '"supported_scopes"',
    '"expected_scopes"',
    'SchemaRef("ApiKeyStateRequest")',
    'SchemaRef("ApiKeyScopeUpdateRequest")',
):
    assert marker in openapi, "Missing OpenAPI scope update contract: " + marker

assert "`statistics:read`" in docs and "without" in docs
scripts = re.findall(r"<script(?:\\s[^>]*)?>(.*?)</script>", view, re.DOTALL)
assert scripts, "No embedded page JavaScript found"
with tempfile.TemporaryDirectory() as work:
    for number, script in enumerate(scripts):
        candidate = Path(work) / f"page-{number}.js"
        candidate.write_text(script, encoding="utf-8")
        subprocess.run(["node", "--check", str(candidate)], check=True)

print(f"World API machine-key scope editor: OK ({len(scripts)} JS scripts syntax-checked)")

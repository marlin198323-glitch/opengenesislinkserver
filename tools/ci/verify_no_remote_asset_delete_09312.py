#!/usr/bin/env python3
"""Fail closed: OGL must not expose or initiate remote HTTP asset deletion."""
from pathlib import Path
import re

root = Path(__file__).resolve().parents[2]
read = lambda rel: (root / rel).read_text(encoding="utf-8")

server = read("OpenSim/Server/Handlers/Asset/AssetServerConnector.cs")
remote = read("OpenSim/Services/Connectors/Asset/AssetServicesConnector.cs")
project = read("OpenSim/Server/Handlers/OpenSim.Server.Handlers.csproj")

# No REST asset DELETE handler exists at all, including its compiled class.
assert not (root / "OpenSim/Server/Handlers/Asset/AssetServerDeleteHandler.cs").exists()
assert "AssetServerDeleteHandler" not in server
assert "AssetServerDeleteHandler" not in project
assert "AllowedRemoteDeleteTypes" not in server
assert 'server.AddStreamHandler(new AssetServerGetHandler(' in server
assert 'server.AddStreamHandler(new AssetServerPostHandler(' in server

# No client-side remote deletion via the IAssetService.Delete interface.
delete = remote.split("public virtual bool Delete(string id)", 1)[1].split(
    "public void Get(", 1)[0]
assert re.search(r'^\s*\{\s*//[^\n]*\n(?:\s*//[^\n]*\n)*\s*return false;\s*\}', delete), (
    "Remote asset Delete must be an unconditional failure")
assert 'MakeRequest<int, bool>("DELETE"' not in remote
assert '"/assets/" + id' not in delete
assert "m_Cache?.Expire(id)" not in delete

# Obsolete configuration switches must have no effect, even in user-owned
# legacy .ini files containing Enabled=true. No code may parse either key.
for old_key in ("AllowRemoteDeleteAllTypes", "AllowRemoteDelete"):
    assert old_key not in server
for rel in (
    "bin/Robust.ini.example",
    "bin/Robust.HG.ini.example",
    "bin/Robust.HG.ini",
    "bin/Robust.Tests.ini",
):
    config = read(rel)
    assert re.search(r'(?m)^\s*AllowRemoteDelete(AllTypes)?\s*=', config) is None, rel

# Local administrative maintenance remains distinct from HTTP.
assert 'MainConsole.Instance.Commands.AddCommand("Assets", false,' in server
assert '"delete asset"' in server
print("OGL asset deletion security gate: no HTTP DELETE route/client/config switches")

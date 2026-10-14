#!/usr/bin/env python3
"""Guard the OpenGenesisLINK HG inventory HTTP trust boundary.

Legacy HG does not provide a verified avatar-scoped authorization proof.
Public HG inventory requests must fail closed, including read operations.
"""
from pathlib import Path
import re

root = Path(__file__).resolve().parents[2]
def read(path):
    return (root / path).read_text(encoding="utf-8")

src = read("OpenSim/Server/Handlers/Inventory/XInventoryInConnector.cs")
connector = src.split("public class XInventoryInConnector", 1)[1].split(
    "public class XInventoryConnectorPostHandler", 1)[0]
handler = src.split("public class XInventoryConnectorPostHandler", 1)[1]

assert re.search(r'string\.Equals\(\s*m_ConfigName,\s*"HGInventoryService",\s*StringComparison\.OrdinalIgnoreCase\s*\)', connector)
assert "m_InventoryService, auth, publicHypergridInventory" in connector
assert "m_DenyPublicHypergridInventory = denyPublicHypergridInventory" in handler

process = handler.split("protected override byte[] ProcessRequest", 1)[1]
denial = process.split("string body;", 1)[0]
assert re.search(r'if\s*\(m_DenyPublicHypergridInventory\)', denial)
assert "HttpStatusCode.Forbidden" in denial
assert "return Util.UTF8NoBomEncoding.GetBytes(" in denial
assert "ServerUtils.ParseQueryString(body)" in process
assert process.index("if (m_DenyPublicHypergridInventory)") < process.index("string body;")
assert process.index("HttpStatusCode.Forbidden") < process.index("ServerUtils.ParseQueryString(body)")

# Internal inventory stays available on private listener; do not redirect it to HG.
for path in ("bin/Robust.HG.ini", "bin/Robust.HG.ini.example"):
    cfg = read(path)
    assert "HGInventoryServiceConnector = " in cfg
    assert 'HGInventoryService@' in cfg and 'PublicPort' in cfg
    assert 'InventoryInConnector = ' in cfg and 'PrivatePort' in cfg
assert '"HGInventoryService"' in read(
    "OpenSim/Region/CoreModules/ServiceConnectorsIn/Inventory/InventoryServiceInConnectorModule.cs")
assert "InventoryServiceInConnector       = true" in read(
    "bin/config-include/StandaloneHypergrid.ini")
print("OGL HG external inventory fail-closed boundary: OK")

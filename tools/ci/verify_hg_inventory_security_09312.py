#!/usr/bin/env python3
"""Regression guard for legacy HG inventory opt-in (0.9.3.13).

Legacy HG does not authenticate the individual avatar. When the operator
has NOT opted in, public HG inventory must still fail closed.
The shipped HG profiles enable temporary compatibility explicitly.
This check verifies configuration wiring, NOT the safety of legacy HG.
"""
from pathlib import Path
import re

root = Path(__file__).resolve().parents[2]


def read(path):
    return (root / path).read_text(encoding="utf-8")


def section(config, name):
    match = re.search(r"(?ms)^\[" + re.escape(name) + r"\]\s*\n(.*?)(?=^\[|\Z)", config)
    assert match, f"Missing [{name}] section"
    return match.group(1)


src = read("OpenSim/Server/Handlers/Inventory/XInventoryInConnector.cs")
connector = src.split("public class XInventoryInConnector", 1)[1].split(
    "public class XInventoryConnectorPostHandler", 1)[0]
handler = src.split("public class XInventoryConnectorPostHandler", 1)[1]

assert re.search(r'string\.Equals\(\s*m_ConfigName,\s*"HGInventoryService",\s*StringComparison\.OrdinalIgnoreCase\s*\)', connector)
assert 'serverConfig.GetBoolean("AllowLegacyPublicHGInventory", false)' in connector
assert 'bool allowLegacyPublicHypergridInventory = publicHypergridInventory &&' in connector
assert 'publicHypergridInventory && !allowLegacyPublicHypergridInventory' in connector
assert 'm_DenyPublicHypergridInventory = denyPublicHypergridInventory' in handler
assert "Unberechtigte Inventar-Lese- und Schreibzugriffe sind möglicherweise möglich" in connector

process = handler.split("protected override byte[] ProcessRequest", 1)[1]
denial = process.split("string body;", 1)[0]
assert re.search(r"if\s*\(m_DenyPublicHypergridInventory\)", denial)
assert "HttpStatusCode.Forbidden" in denial
assert "return Util.UTF8NoBomEncoding.GetBytes(" in denial
assert process.index("HttpStatusCode.Forbidden") < process.index("ServerUtils.ParseQueryString(body)")

for path in ("bin/Robust.HG.ini", "bin/Robust.HG.ini.example"):
    cfg = read(path)
    service_list = section(cfg, "ServiceList")
    hg_cfg = section(cfg, "HGInventoryService")
    assert re.search(r'^\s*HGInventoryServiceConnector\s*=\s*"HGInventoryService@\$\{Const\|PublicPort\}/OpenSim\.Server\.Handlers\.dll:XInventoryInConnector"', service_list, re.M), path
    assert re.search(r'^\s*InventoryInConnector\s*=\s*"\$\{Const\|PrivatePort\}/', service_list, re.M), path
    assert re.search(r'^\s*AllowLegacyPublicHGInventory\s*=\s*true\s*$', hg_cfg, re.M), path
    assert "HGSuitcaseInventoryService" in hg_cfg, path

standalone = read("bin/config-include/StandaloneHypergrid.ini")
hg_standalone = section(standalone, "HGInventoryService")
assert re.search(r'^\s*AllowLegacyPublicHGInventory\s*=\s*true\s*$', hg_standalone, re.M)
assert "HGSuitcaseInventoryService" in hg_standalone
assert "InventoryServiceInConnector       = true" in standalone

# A user-controlled UUID or a grid-level shared credential is NOT an
# adequate authorization check; do not describe this opt-in as secure.
print("OGL HG legacy inventory opt-in: config enabled, fail-closed default preserved")

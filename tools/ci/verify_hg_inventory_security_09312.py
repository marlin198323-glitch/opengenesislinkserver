#!/usr/bin/env python3
"""Security contract for the publicly exposed HG inventory endpoint."""
from pathlib import Path
import re

root = Path(__file__).resolve().parents[2]
read = lambda path: (root / path).read_text(encoding="utf-8")

connector = read("OpenSim/Server/Handlers/Inventory/XInventoryInConnector.cs")
legacy = read("OpenSim/Server/Handlers/Inventory/InventoryServerInConnector.cs")
allowed = [
    "GETINVENTORYSKELETON", "GETROOTFOLDER", "GETFOLDERFORTYPE",
    "GETFOLDERCONTENT", "GETMULTIPLEFOLDERSCONTENT", "GETFOLDERITEMS",
    "GETITEM", "GETMULTIPLEITEMS", "GETFOLDER", "GETACTIVEGESTURES",
    "GETASSETPERMISSIONS"
]
mutations = [
    "CREATEUSERINVENTORY", "ADDFOLDER", "UPDATEFOLDER", "MOVEFOLDER",
    "DELETEFOLDERS", "PURGEFOLDER", "ADDITEM", "UPDATEITEM",
    "MOVEITEMS", "DELETEITEMS"
]
assert 'string.Equals(m_ConfigName, "HGInventoryService"' in connector
assert 'new XInventoryConnectorPostHandler(m_InventoryService, auth, publicHypergrid)' in connector
assert "m_PublicHypergridReadOnly && !IsAllowedPublicHypergridMethod(method)" in connector
assert "HttpStatusCode.Forbidden" in connector
assert connector.index("m_PublicHypergridReadOnly &&") < connector.index("switch (method)")
source = connector.split("public static bool IsAllowedPublicHypergridMethod", 1)[1].split("protected override byte[]", 1)[0]
for name in allowed:
    assert 'case "' + name + '":' in source, name
for name in mutations:
    assert 'case "' + name + '":' not in source, name
    assert 'case "' + name + '":' in connector, (name, "missing existing handler")
assert 'string.Equals(m_ConfigName, "HGInventoryService"' in legacy
assert "throw new InvalidOperationException" in legacy
for ini in ("bin/Robust.HG.ini", "bin/Robust.HG.ini.example"):
    body=read(ini)
    assert 'HGInventoryService@' in body
    assert "XInventoryInConnector" in body
    hg = body.split("[HGInventoryService]", 1)[1].split("\n[", 1)[0]
    assert "AuthType = None" in hg  # HG interoperability: deny mutations independently
print("HG public inventory: read-only write deny, legacy connector blocked, private API preserved")

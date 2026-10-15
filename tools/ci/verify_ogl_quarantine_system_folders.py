#!/usr/bin/env python3
"""Static wiring/guard checks for the OGL per-resident quarantine folder hierarchy."""
from pathlib import Path
import re

root = Path(__file__).resolve().parents[2]


def read(p):
    return (root / p).read_text(encoding="utf-8")


source = read("OpenSim/Services/InventoryService/XInventoryService.cs")
ids = read("OpenSim/Services/InventoryService/OglInventoryQuarantineFolders.cs")
assert "CreateInventorySystemFolder" in source
assert "EnsureQuarantineFolders(principalID, rootFolder.ID)" in source
assert "EnsureQuarantineFolders(principalID, actualRoot.ID)" in source
assert "OglInventoryQuarantineFolders.Root(owner)" in source
assert "OglInventoryQuarantineFolders.Pending(owner)" in source
assert "OglInventoryQuarantineFolders.Rejected(owner)" in source
assert "m_Database.StoreFolder(new XInventoryFolder" in source
for method, next_method in [
    ("AddFolder", "UpdateFolder"),
    ("UpdateFolder", "MoveFolder"),
    ("MoveFolder", "DeleteFolders"),
    ("DeleteFolders", "PurgeFolder"),
    ("PurgeFolder", "AddItem"),
    ("AddItem", "UpdateItem"),
    ("UpdateItem", "MoveItems"),
    ("MoveItems", "DeleteItems"),
    ("DeleteItems", "GetItem")
]:
    signature = {
        "DeleteFolders": "public virtual bool DeleteFolders(UUID principalID, List<UUID> folderIDs, bool onlyIfTrash)",
        "PurgeFolder": "public virtual bool PurgeFolder(InventoryFolderBase folder, bool onlyIfTrash)",
    }.get(method, "public virtual bool " + method + "(")
    start = source.index(signature)
    end = source.index("public virtual ", start + 15)
    segment = source[start:end]
    assert "IsProtected" in segment or "HasProtected" in segment, method

assert "if (!IsProtectedFolder(folderID))" in source
assert "IsProtectedFolder(items[0].parentFolderID)" in source
assert "SHA256.HashData(" in ids
assert "FolderType.None" in source
assert "Quarantaene" in ids and "In Pruefung" in ids and "Abgelehnt" in ids

for path in ("bin/Robust.HG.ini", "bin/Robust.HG.ini.example",
             "bin/config-include/StandaloneHypergrid.ini"):
    data = read(path)
    match = re.search(r"(?ms)^\[OGLInventoryGuard\]\s*\n(.*?)(?=^\[|\Z)", data)
    assert match and re.search(
        r"(?m)^\s*CreateInventorySystemFolder\s*=\s*true\s*$",
        match.group(1)), path

print("OGL quarantine managed folder wiring: PASS (static)")

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

assert "if (!IsProtectedFolder(folderID) &&" in source
assert "IsProtectedFolder(items[0].parentFolderID)" in source
assert "SHA256.HashData(" in ids
assert "FolderType.None" in source
assert "Quarantaene" in ids and "In Pruefung" in ids and "Abgelehnt" in ids

# Phase 3a: separate local-only case-status projection. The real HG
# connector is NOT given these methods or an item-release privilege.
assert "public bool TryBeginTrustedQuarantineCase(" in source
assert "public bool TryRejectTrustedQuarantineCase(" in source
assert "OglInventoryQuarantineFolders.Case(verifiedOwner, caseId)" in source
assert 'folderName = OglInventoryQuarantineFolders.CaseLabel(caseId)' in source
assert "m_Database.MoveFolder(id.ToString(), rejected.ToString())" in source
assert "BumpQuarantineFolderVersion(verifiedOwner, pending)" in source
assert "BumpQuarantineFolderVersion(verifiedOwner, rejected)" in source
assert "HashSet<UUID> visited = new HashSet<UUID>()" in source
assert "OglInventoryQuarantineFolders.IsManaged(owner, current)" in source
assert "asset.Data" not in source[source.index("public bool TryBeginTrustedQuarantineCase("):
                                  source.index("protected XInventoryFolder CreateFolder(")]
# No corresponding remote method must be added to generic inventory contract.
iface = read("OpenSim/Services/Interfaces/IInventoryService.cs")
assert "TryBeginTrustedQuarantineCase" not in iface
assert "TryRejectTrustedQuarantineCase" not in iface

# The one-time HMAC proof adapter is LOCAL and cannot be issued from
# the remote inventory connectors. Its spent record is atomically created
# before any viewer-visible change to the pending/rejected case folders.
grants = read("OpenSim/Services/InventoryService/OglQuarantineTransferGrants.cs")
assert "HMACSHA256.HashData(m_secret, payloadBytes)" in grants
assert "CryptographicOperations.FixedTimeEquals(computed, suppliedMac)" in grants
assert "System.IO.FileMode.CreateNew" in grants
assert "UnixCreateMode = FileModePrivate" in grants
assert "expires - issued > 300" in grants
assert "m_authority.TryConsume(proof" in grants
assert grants.index("m_authority.TryConsume(proof") < grants.index("m_inventory.TryBeginTrustedQuarantineCase")
assert "m_inventory.TryRejectTrustedQuarantineCase" in grants
assert "TryConsume(" not in iface
assert "TryBegin(" not in iface

# 3c isolated byte staging: HMAC single-use Stage proof before touching
# raw payload. No direct store to FSAssets or inventory and no release API.
stage = read("OpenSim/Services/InventoryService/OglQuarantineTransferStageStore.cs")
assert "Stage = 3" in grants
assert "OglQuarantineTransferAction.Stage" in grants
assert "m_authority.TryConsume(proof, verifiedOwner, caseId, itemId," in grants
assert "m_stageStore.TryStage(verifiedOwner, caseId, itemId," in grants
assert "m_stageStore.TryVerify(verifiedOwner, caseId, itemId, assetId)" in grants
assert grants.index("m_stageStore.TryStage(") < grants.index("m_inventory.TryBeginTrustedQuarantineCase(verifiedOwner, caseId)", grants.index("m_stageStore.TryStage("))
assert "staged_uninspected" in stage
assert "FileMode.CreateNew" in stage
assert "FileShare.None" in stage
assert "UnixCreateMode = PrivateFile" in stage
assert "File.SetUnixFileMode(m_directory, PrivateDir)" in stage
assert "SHA256.HashData(payload)" in stage
assert "SHA256.HashData(file)" in stage
assert "TryVerify(" in stage and "TryStage(" in stage
assert "m_assetService.Store" not in stage
assert "m_Database.StoreItem" not in stage
assert "TryRelease(" not in stage
assert "TryStage(" not in iface

multi = read("OpenSim/Services/InventoryService/OglQuarantineTransferStageStore.cs")
assert "TryStageDependency(UUID verifiedOwner, UUID caseId," in multi
assert "TryReadVerifiedBundle(UUID verifiedOwner, UUID caseId," in multi
assert "staged_uninspected_dependency" in multi
assert "CryptographicOperations.FixedTimeEquals(" in multi
assert "bundle_incomplete_or_too_many_dependencies" in multi
assert "bundle_dependency_corrupt" in multi
assert "OpenStageLock()" in multi
assert "StageDependency = 4" in grants
assert "OglQuarantineTransferAction.StageDependency" in grants
assert "m_stageStore.TryStageDependency(" in grants
assert "TryStageDependency(" not in iface

for path in ("bin/Robust.HG.ini", "bin/Robust.HG.ini.example",
             "bin/config-include/StandaloneHypergrid.ini"):
    data = read(path)
    match = re.search(r"(?ms)^\[OGLInventoryGuard\]\s*\n(.*?)(?=^\[|\Z)", data)
    assert match and re.search(
        r"(?m)^\s*CreateInventorySystemFolder\s*=\s*true\s*$",
        match.group(1)), path

print("OGL quarantine managed folder wiring: PASS (static)")

#!/usr/bin/env python3
"""Source/config integration checks for 0.9.3.13 HG InventoryGuard phase 1.

These checks do not prove remote caller authorization or asset-byte safety.
Run the C# regression project for executable metadata-validator coverage.
"""
from pathlib import Path
import re

root = Path(__file__).resolve().parents[2]


def read(path):
    return (root / path).read_text(encoding="utf-8")


service = read("OpenSim/Services/HypergridService/HGSuitcaseInventoryService.cs")
guard = read("OpenSim/Services/HypergridService/OglHgInventoryGuard.cs")

assert 'GetBoolean("Enabled", false)' in service
assert "OglHgInventoryGuard.TryValidateItem(item, out string reason)" in service
assert "OglHgInventoryGuard.TryValidateUpdate(existing, item, out string reason)" in service
assert service.index("OglHgInventoryGuard.TryValidateItem") < service.index("return base.AddItem(item)")
assert 'new string[] { "inventoryID" }' in service
assert 'm_Database.GetItems(' in service
assert 'existing != null && existing.Length > 0' in service
assert "existing.AssetID != incoming.AssetID" in guard
assert "existing.Owner != incoming.Owner" in guard
assert "existing.Folder != incoming.Folder" in guard
assert "Legacy /xinventory remains without per-avatar caller authorization" in service

# Strict legacy public HG boundary: no caller principal can be trusted from
# unauthenticated xinventory. Verify that all inherited collection methods and
# mutation methods have a gate BEFORE reading or writing the database.
boundary = read("OpenSim/Services/HypergridService/OglHgLegacyInventoryBoundary.cs")
assert "LegacyCompatible = 0" in boundary
assert "ReadOnly = 1" in boundary
assert "BlockAll = 2" in boundary
assert "return OglHgLegacyInventoryMode.BlockAll;" in boundary
assert 'GetString("LegacyPublicAccessMode", "LegacyCompatible")' in service
assert "bool IInventoryService.HasInventoryForUser(UUID principalID)" in service
assert "public override InventoryFolderBase GetFolder(UUID principalID, UUID folderID)" in service
assert "return base.GetMultipleItems(principalID, ids)" not in service

reads = [
    "GetInventorySkeleton", "GetRootFolder", "GetFolderForType",
    "GetFolderContent", "GetFolderItems", "GetItem", "GetFolder",
    "GetMultipleFoldersContent", "GetMultipleItems", "GetActiveGestures"
]
writes = ["AddFolder", "UpdateFolder", "MoveFolder", "AddItem",
          "UpdateItem", "MoveItems"]
for name in reads + writes:
    import re as _re
    pattern = _re.compile(
        r"public override [^{};]{0,300}\b" + _re.escape(name) + r"\s*\([^)]*\)\s*\{",
        _re.MULTILINE,
    )
    match = pattern.search(service)
    assert match is not None, "Missing public HG method " + name
    rest = service[match.end():match.end()+220]
    gate = "if (!AllowLegacyRead" if name in reads else "if (!AllowLegacyWrite)"
    assert gate in rest, "Missing early public HG gate " + name

for path in ("bin/Robust.HG.ini", "bin/Robust.HG.ini.example",
             "bin/config-include/StandaloneHypergrid.ini"):
    config = read(path)
    assert "LegacyPublicAccessMode = LegacyCompatible" in config, path

for path in (
    "bin/Robust.HG.ini",
    "bin/Robust.HG.ini.example",
    "bin/config-include/StandaloneHypergrid.ini",
):
    data = read(path)
    section = re.search(r"(?ms)^\[OGLInventoryGuard\]\s*\n(.*?)(?=^\[|\Z)", data)
    assert section, f"missing InventoryGuard section: {path}"
    assert re.search(r"(?m)^\s*Enabled\s*=\s*true\s*$", section.group(1)), path

asset_handler = read("OpenSim/Services/HypergridService/HGAssetService.cs")
asset_scanner = read("OpenSim/Services/HypergridService/OglHgInventoryAssetInspector.cs")
assert 'GetBoolean("ScanInboundAssets", false)' in asset_handler
assert "OglHgInventoryAssetInspector.TryInspect(asset," in asset_handler
assert asset_handler.index("OglHgInventoryAssetInspector.TryInspect(asset,") < asset_handler.index("m_AssetPerms.AllowedImport(asset.Type)")
assert "DtdProcessing = DtdProcessing.Prohibit" in asset_scanner
assert "XmlResolver = null" in asset_scanner
assert "MaximumObjectDepth" in asset_scanner and "MaximumObjectNodes" in asset_scanner
assert "AssetType.LSLBytecode" in asset_scanner
assert "UTF8Encoding(false, true)" in asset_scanner
nested = read("OpenSim/Services/HypergridService/OglHgNestedInventoryInspector.cs")
assert "OglHgNestedInventoryInspector.TryExtract(asset, maxAssetBytes," in asset_scanner
assert "public const int MaxNestedItems = 2048" in nested
assert "DtdProcessing = DtdProcessing.Prohibit" in nested
assert "XmlResolver = null" in nested
assert "MaxCharactersInDocument = maxBytes" in nested
assert "duplicate_nested_inventory_item_id" in nested
assert "AssetType.LSLBytecode" in nested
assert "HashSet<UUID> seenItems" in nested
assert "TryReadShortValue" in nested
assert "out OglHgNestedAssetReference[] references" in nested

graph = read("OpenSim/Services/HypergridService/OglHgQuarantineDependencyGraphVerifier.cs")
assert "IReadOnlyDictionary<UUID, AssetBase> isolatedAssets" in graph
assert "IReadOnlyDictionary<UUID, string> expectedSha256Pins" in graph
assert "OglHgNestedInventoryInspector.TryExtract(asset, maxAssetBytes" in graph
assert "OglHgInventoryAssetInspector.TryInspect(" in graph
assert "SHA256.HashData(asset.Data)" in graph
assert "CryptographicOperations.FixedTimeEquals(actual, pinned)" in graph
assert "graph_dependency_missing" in graph
assert "graph_dependency_cycle" in graph
assert "graph_depth_limit" in graph
assert "graph_reference_count_limit" in graph
assert "graph_asset_hash_mismatch" in graph
assert 'structurally_inspected_not_authorized' in graph
assert "m_assetService.Get" not in graph
assert "m_assetService.Store" not in graph
assert "m_Database.StoreItem" not in graph
assert "GetCached(" not in graph
assert "TryRelease(" not in graph

bundle_adapter = read("OpenSim/Services/HypergridService/OglHgStagedQuarantineBundleInspector.cs")
assert "store.TryReadVerifiedBundle(" in bundle_adapter
assert "OglHgQuarantineDependencyGraphVerifier.TryInspect(" in bundle_adapter
assert "report.AssetsInspected != staged.Length" in bundle_adapter
assert "bundle_unreferenced_staged_asset" in bundle_adapter
assert "m_assetService.Get" not in bundle_adapter
assert "m_assetService.Store" not in bundle_adapter
assert "TryRelease(" not in bundle_adapter
assert "TryInspectBound(" in bundle_adapter
assert "TryIsBindingCurrent(" in bundle_adapter
assert "OGL_QUARANTINE_STRUCTURAL_BINDING_V1" in bundle_adapter
assert "writer.Write(verifiedOwner" not in bundle_adapter
assert "writer.Write(owner.ToString())" in bundle_adapter
assert "writer.Write(caseId.ToString())" in bundle_adapter
assert "writer.Write(rootItemId.ToString())" in bundle_adapter
assert "writer.Write(rootAssetId.ToString())" in bundle_adapter
assert "writer.Write(maxAssetBytes)" in bundle_adapter
assert "writer.Write(maxScriptBytes)" in bundle_adapter
assert "writer.Write(maxReferences)" in bundle_adapter
assert "writer.Write(maxDepth)" in bundle_adapter
assert "writer.Write(maxTotalBytes)" in bundle_adapter
assert "writer.Write(blob.AssetType)" in bundle_adapter
assert "SHA256.HashData(blob.Data)" in bundle_adapter
assert "bundle_changed_during_inspection" in bundle_adapter
assert "bundle_binding_stale" in bundle_adapter
assert "CryptographicOperations.FixedTimeEquals(expected, actual)" in bundle_adapter
assert "structurally_inspected_not_authorized" in bundle_adapter
quarantine = read("OpenSim/Services/HypergridService/OglHgInventoryQuarantine.cs")
assert 'GetBoolean("QuarantineRejectedAssets", false)' in asset_handler
assert "OglHgInventoryQuarantine.TryCapture" not in asset_handler  # instance method
assert "m_OglInventoryQuarantine.TryCapture(asset, reason" in asset_handler
assert "return string.Empty;" in asset_handler.split("m_OglInventoryQuarantine.TryCapture(asset, reason", 1)[1]
assert "FileMode.CreateNew" in quarantine
assert "UnixCreateMode = PrivateFileMode" in quarantine
assert "File.SetUnixFileMode(m_directory, PrivateDirectoryMode)" in quarantine
assert "SHA256.HashData(asset.Data)" in quarantine
assert '"rejected_evidence_only"' in quarantine
assert "MaxCaptureBytes" in quarantine
for path in (
    "bin/Robust.HG.ini",
    "bin/Robust.HG.ini.example",
    "bin/config-include/StandaloneHypergrid.ini",
):
    cfg = read(path)
    section = re.search(r"(?ms)^\[OGLInventoryGuard\]\s*\n(.*?)(?=^\[|\Z)", cfg)
    assert section and re.search(r"(?m)^\s*ScanInboundAssets\s*=\s*true\s*$", section.group(1)), path
    assert "MaxInboundAssetBytes = 33554432" in section.group(1), path
    assert "MaxInboundScriptBytes = 1048576" in section.group(1), path
    assert "QuarantineRejectedAssets = false" in section.group(1), path
    assert "QuarantineMaxCaptureBytes = 262144" in section.group(1), path
    assert "QuarantineMaxRecords = 5000" in section.group(1), path

assert "AllowLegacyPublicHGInventory = true" in read("bin/Robust.HG.ini")
print("OGL HG InventoryGuard: metadata, bounded nested task references, asset ingress and rejected-evidence wiring verified (no HG avatar authorization)")

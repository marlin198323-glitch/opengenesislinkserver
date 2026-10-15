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

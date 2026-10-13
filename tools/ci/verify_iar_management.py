#!/usr/bin/env python3
from pathlib import Path

mgmt=Path("NexVerse/RegionModules/Archives/OglIarManagement.cs").read_text()
ops=Path("NexVerse/RegionModules/Archives/OglIarOperationManager.cs").read_text()
mod=Path("NexVerse/RegionModules/Archives/OglIarManagementModule.cs").read_text()
archiver=Path("OpenSim/Region/CoreModules/Avatar/Inventory/Archiver/InventoryArchiverModule.cs").read_text()
required=[
(mgmt,"class OglIarInspector"),(mgmt,'"inventory/"'),(mgmt,'"assets/"'),(mgmt,"SHA256.Create()"),
(mgmt,"class OglIarStoragePolicy"),(ops,"IInventoryArchiverModule"),(ops,"ArchiveInventory("),
(ops,"DearchiveInventory("),(ops,'["merge"] = merge'),(ops,"OnInventoryArchiveSaved"),
(ops,"OnInventoryArchiveLoaded"),(ops,"InventoryPath"),(mod,'Configs["OpenGenesisLINKIAR"]'),
(mod,"RegionLoaded(Scene scene)"),(mod,"RegisterModuleInterface<IOglIarOperations>"),
(archiver,"scene.RegisterModuleInterface<IInventoryArchiverModule>(this);"),
(archiver,"scene.UnregisterModuleInterface<IInventoryArchiverModule>(this);"),
(archiver,"m_scenes.Remove(scene.RegionInfo.RegionID)")
]
missing=[n for t,n in required if n not in t]
if missing: raise SystemExit("IAR management foundation missing: "+", ".join(missing))
if "InventoryArchiveWriteRequest" in ops or "InventoryArchiveReadRequest" in ops:
    raise SystemExit("OGL IAR manager must not duplicate original archiver internals")

# The archiver's Stream overloads accept raw TAR bytes, unlike their
# filename overloads which wrap GZip themselves. The manager must wrap
# its file streams at BOTH boundaries or .iar archives become unreadable.
read_request = Path("OpenSim/Region/CoreModules/Avatar/Inventory/Archiver/InventoryArchiveReadRequest.cs").read_text()
write_request = Path("OpenSim/Region/CoreModules/Avatar/Inventory/Archiver/InventoryArchiveWriteRequest.cs").read_text()
assert "archive = new TarArchiveReader(m_loadStream);" in read_request
assert "m_archiveWriter = new TarArchiveWriter(m_saveStream);" in write_request
assert "using System.IO.Compression;" in ops
assert "GZipStream output = new(File.Create(path), CompressionLevel.Optimal);" in ops
assert "GZipStream input = new(File.OpenRead(path), CompressionMode.Decompress);" in ops
assert "FileStream input = File.OpenRead(path);" not in ops
assert ops.index("GZipStream input =") < ops.index("m_Archiver.DearchiveInventory(")
print("OpenGenesisLINK IAR management foundation: OK")

register_pos=archiver.find("scene.RegisterModuleInterface<IInventoryArchiverModule>(this);")
first_region_pos=archiver.find("if (m_scenes.Count == 0)")
if register_pos < 0 or first_region_pos < 0 or register_pos > first_region_pos:
    raise SystemExit("IInventoryArchiverModule must be registered on every region before first-region-only setup")

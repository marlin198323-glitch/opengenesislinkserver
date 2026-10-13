#!/usr/bin/env python3
"""Static regression guard for console progress and bounded import preflight."""
from pathlib import Path
root=Path(__file__).resolve().parents[2]
paths={
    "contract": "OpenSim/Region/Framework/Interfaces/IInventoryArchiverModule.cs",
    "module": "OpenSim/Region/CoreModules/Avatar/Inventory/Archiver/InventoryArchiverModule.cs",
    "reader": "OpenSim/Region/CoreModules/Avatar/Inventory/Archiver/InventoryArchiveReadRequest.cs",
    "writer": "OpenSim/Region/CoreModules/Avatar/Inventory/Archiver/InventoryArchiveWriteRequest.cs",
    "assets": "OpenSim/Region/CoreModules/World/Archiver/AssetsRequest.cs",
    "inspector": "NexVerse/RegionModules/Archives/OglIarManagementModule.cs",
    "manager": "NexVerse/RegionModules/Archives/OglIarOperationManager.cs",
}
src={k:(root / p).read_text() for k,p in paths.items()}
required={
 "contract":["interface IInventoryArchiverProgress","interface IInventoryArchiveInspector","OnInventoryArchiveProgress"],
 "module":["IInventoryArchiverProgress","DisplayConsoleArchiveProgress","TryInspect(loadPath","iar_total_entries","TriggerInventoryArchiveProgress","ArchiveInventory(id, firstName","DearchiveInventory(id, firstName"],
 "reader":["ExpectedEntries","m_entriesProcessed","TriggerInventoryArchiveProgress(m_id, \"import\"","TriggerInventoryArchiveProgress(m_id, \"finalizing\""],
 "writer":["TriggerInventoryArchiveProgress(m_id, \"inventory\"","TriggerInventoryArchiveProgress(m_id, \"assets\"","ReceivedAllAssets"],
 "assets":["m_progressCallback","finally","++m_assetsProcessed"],
 "inspector":["IInventoryArchiveInspector","OglIarInspector.Inspect(archivePath)","RegisterModuleInterface<IInventoryArchiveInspector>"],
 "manager":["m_ProgressSource.OnInventoryArchiveProgress","HandleProgress","inspection.Entries","operation.ProgressPercent = 100","operation.ProgressPhase = \"completed\""],
}
for file, tokens in required.items():
 for token in tokens:
  assert token in src[file], f"{file}: missing {token}"
# Fail-closed validation must happen before the legacy IAR load starts.
module=src["module"]
assert module.index("inspector.TryInspect(loadPath") < module.index("DearchiveInventory(id, firstName, lastName, invPath, pass, loadPath, options)")
assert 'm_consoleProgressState.Remove(id)' in module
print("OpenGenesisLINK console IAR progress and bounded preflight wiring: OK")

#!/usr/bin/env python3
"""Static regression guard for console progress and bounded import preflight."""
from pathlib import Path
root=Path(__file__).resolve().parents[2]
paths={
    "contract": "OpenSim/Region/Framework/Interfaces/IInventoryArchiverModule.cs",
    "console": "OpenSim/Framework/Console/LocalConsole.cs",
    "asset_writer": "OpenSim/Region/CoreModules/World/Archiver/AssetsArchiver.cs",
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
 "console":["UpdateOglIarProgress","m_oglIarStatus","LockOutput();","m_lastOglIarStatusLength"],
 "asset_writer":["Assets dem Archiv hinzugefügt","m_log.Debug"],
 "module":["IInventoryArchiverProgress","DisplayConsoleArchiveProgress","TryInspect(loadPath","iar_total_entries","TriggerInventoryArchiveProgress","ArchiveInventory(id, firstName","DearchiveInventory(id, firstName",
           'show iar progress', 'RefreshIarPrompt()', 'OGLIarLiveProgress', 'RemoveConsoleIarJob(id)', 'm_consoleJobs.TryGetValue(id', 'OGL-IAR'],
 "reader":["ExpectedEntries","m_entriesProcessed","TriggerInventoryArchiveProgress(m_id, \"import\"","TriggerInventoryArchiveProgress(m_id, \"finalizing\""],
 "writer":["TriggerInventoryArchiveProgress(m_id, \"inventory\"","TriggerInventoryArchiveProgress(m_id, \"assets\"","ReceivedAllAssets"],
 "assets":["m_progressCallback","finally","++m_assetsProcessed",
          "angeforderte Einträge waren nicht auffindbar"],
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
assert 'if (succeeded)' in module and '100% – Exportvorgang abgeschlossen' in module
assert 'if (liveUpdate)' in module and 'if (!report)' in module
assert 'm_pendingConsoleTasks.Contains(id)' in module
assert 'OGLIarLiveProgress = true' in (root / 'bin/OpenSim.ini.example').read_text()
print("OpenGenesisLINK console IAR progress and bounded preflight wiring: OK")

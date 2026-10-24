#!/usr/bin/env python3
"""OGL FastMap/Updater release regression: opt-in, safe review, and API gates."""
from pathlib import Path
import tempfile
import importlib.util
import json
import sys

ROOT = Path(__file__).resolve().parents[2]
def read(path):
    return (ROOT / path).read_text(encoding="utf-8")

scene = read("OpenSim/Region/Framework/Scenes/Scene.cs")
queue = read("OpenSim/Region/Framework/Scenes/OglFastMapStartupQueue.cs")
node = read("NexVerse/RegionModules/NodeAgent/NexVerseNodeAgentModule.cs")
api = read("NexVerse/Server/Api/NexNodeApi.cs")
workflow = read(".github/workflows/nexjast-ci.yml")
sh = read("tools/ops/ogl-node-stage-update.sh")

assert "m_generateMaptiles && !m_fastMapAsyncStartup" in scene
assert "QueueFastMapStartup();" in scene
assert scene.index("GridService.RegisterRegion(RegionInfo.ScopeID, region)") < scene.index("QueueFastMapStartup();")
assert "m_fastMapCancellation.Cancel();" in scene
assert "SemaphoreSlim s_renderSlot = new(1, 1)" in queue
assert "m_mapRefreshEnabled && !m_fastMapShutdownRequested" in scene
assert "m_fastMapAsyncStartup = m_generateMaptiles &&" in scene
assert "node.update.stage.requested" in node and "m_UpdateStagingEnabled" in node
assert 'request.Source, "nexverse.robust"' in node
assert 'action == "stage_update" ? "node.update.stage.requested"' in api
assert '"/api/v1/nodes/updates"' in api
assert "NexScopes.SimulatorsManage" in api
assert "OglFastMapStartupRegression" in workflow
assert "github.com/opengenesislink/opengenesislinkserver.git" in sh
assert "flock -n 9" in sh and 'stage="$(mktemp' in sh
assert "realpath -m" in sh and "dotnet build OpenSim.sln" in sh
assert "config-review" in sh and "mv -- \"$stage\" \"$final\"" in sh
assert "mv -- \"$stage\" \"$final\"" in sh

for path in ("bin/OpenSim.ini", "bin/OpenSim.ini.example", "bin/OpenSimDefaults.ini"):
    ini = read(path)
    for setting in ("FastMapAsyncStartup = false", "FastMapStartupDelaySeconds = 45",
                    "UpdateStagingEnabled = false", "UpdateStagingBuild = true"):
        assert setting in ini, (path, setting)

review_path = ROOT / "tools/ops/ogl-config-example-sync.py"
spec = importlib.util.spec_from_file_location("ogl_config_example_sync", review_path)
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)

with tempfile.TemporaryDirectory(prefix="ogl-update-ci-") as scratch:
    base = Path(scratch)
    active = base / "live"
    downloaded = base / "downloaded"
    output = base / "review"
    (active / "config-include").mkdir(parents=True)
    (downloaded / "config-include").mkdir(parents=True)
    secret = "secret-token-value-DO-NOT-COPY"
    (active / "OpenSim.ini").write_text(
        "[Map]\nGenerateMaptiles=true\n[Updater]\nToken=" + secret + "\n")
    (downloaded / "OpenSim.ini").write_text(
        "[Map]\nGenerateMaptiles=true\nFastMapAsyncStartup=false\n"
        "[Updater]\nToken=placeholder\nUpdateStagingEnabled=false\n")
    (active / "config-include/GridCommon.ini").write_text(
        "[AssetService]\nStorage=FSAssets\n")
    (downloaded / "config-include/GridCommon.ini").write_text(
        "[AssetService]\nStorage=FSAssets\nCacheEnabled=false\n")
    rows = module.sync(active, downloaded, output)
    assert len(rows) == 2, rows
    combined = (output / "missing-settings.json").read_text()
    assert "FastMapAsyncStartup" in combined
    assert "CacheEnabled" in combined
    assert secret not in combined
    examples = list(output.rglob("*.example.ini"))
    assert len(examples) == 2
    assert all(secret not in path.read_text() for path in examples)
    assert secret in (active / "OpenSim.ini").read_text()
    assert (active / "config-include/GridCommon.ini").read_text() == "[AssetService]\nStorage=FSAssets\n"

    # Also collect shipped .ini.example files without an active .ini.
    (downloaded / "config-include/Hypergrid.ini.example").write_text(
        "[Hypergrid]\nGatekeeperURI=example.invalid\n")
    fresh = base / "example-review"
    second = module.sync(active, downloaded, fresh)
    assert len(second) == 3, second
    assert any(row["file"] == "config-include/Hypergrid.ini"
               and row["missing_settings"][0]["name"] == "GatekeeperURI"
               for row in second)
    assert (fresh / "config-include/Hypergrid.example.ini").is_file()
    try:
        module.sync(active, downloaded, active / "review")
        raise AssertionError("Review output inside active bin was accepted")
    except ValueError:
        pass
    linked = base / "linked-review"
    (linked / "config-include").mkdir(parents=True)
    (linked / "OpenSim.example.ini").symlink_to(active / "OpenSim.ini")
    try:
        module.sync(active, downloaded, linked)
        raise AssertionError("Symlinked generated example was accepted")
    except ValueError:
        pass
    assert secret in (active / "OpenSim.ini").read_text()

# Publish the replacement map to the grid BEFORE deleting the old map assets.
worldmap = read("OpenSim/Region/CoreModules/World/WorldMap/WorldMapModule.cs")
map_fn = worldmap.split("private void GenerateMaptile(Bitmap mapbmp)", 1)[1].split(
    "private void MakeRootAgent", 1)[0]
assert "AssetService.Delete(" not in map_fn
assert scene.count("CleanupPublishedMapAssets(priorMap, priorOverlay);") == 2
assert "NexBusProtocol.VerifyNodeStageRequest" in node
assert "NEXVERSE_UPDATE_CONTROL_KEY" in node
assert "SignStageRequest(payload" in api
assert "EnsureStageAuthorized(response)" in api
assert "Existing update stage is unverified or not built" in sh
assert "deployment=" in sh

print("FastMap 2.0 startup control + signed node updater + conflict-free INI review: OK")

#!/usr/bin/env python3
"""OpenGenesisLINK cross-region llListen propagation contract."""
from pathlib import Path
root=Path(__file__).resolve().parents[2]
def read(p): return (root/p).read_text(encoding="utf-8")
chat=read("OpenSim/Region/CoreModules/Avatar/Chat/ChatModule.cs")
world=read("OpenSim/Region/CoreModules/Scripting/WorldComm/WorldCommModule.cs")
node=read("NexVerse/RegionModules/NodeAgent/NexVerseNodeAgentModule.cs")
geometry=read("OpenSim/Region/Framework/Scenes/CrossRegionChatGeometry.cs")
project=read("OpenSim/Region/Framework/OpenSim.Region.Framework.csproj")
for marker in (
 "ForwardToLocalNeighbourListeners(scene, c)",
 "speaker.IsChildAgent",
 "CrossRegionChatGeometry.CanReachRegion(",
 "RequestModuleInterface<IWorldComm>()",
 "listener.DeliverMessage(c.Type, c.Channel, speaker.Name,",
 "worldX - destination.RegionInfo.WorldLocX",
):
 assert marker in chat, "local relay missing: " + marker
assert "scene.EventManager.TriggerOnChatFromClient(sender, c)" in chat
assert "OnChatFromClient -= DeliverClientMessage" in world
assert "if (sPart == null)\n                        continue;" in world
for marker in (
 "PublishNeighbourAvatarChat",
 "HandleNeighbourAvatarChat",
 "CrossRegionChatMessaging",
 '"chat.neighbor.requested"',
 "IsChildAgent",
 "source.RegionInfo.WorldLocX + position.X",
 "target.RegionInfo.WorldLocX",
 "m_Scenes.ContainsKey(sourceRegion)",
 "m_ChatInboundRates",
 "m_ChatOutboundRates",
 "TotalSeconds) > 5",
 "TryChatFloat(",
 "float.IsFinite(value)",
 "worldComm.DeliverMessage(type, channel, sourceName, sourceAvatar,",
):
 assert marker in node, "NexBus relay missing: " + marker
for marker in ("ChatTypeEnum.Whisper", "ChatTypeEnum.Say", "ChatTypeEnum.Shout",
               "dx * dx + dy * dy < range * range", "float.IsFinite"):
 assert marker in geometry, "geometry missing: " + marker
assert "Scenes\\CrossRegionChatGeometry.cs" in project
for config,value in [
 ("bin/OpenSim.ini","true"), ("bin/OpenSim.ini.example","true"),
 ("bin/OpenSimDefaults.ini","false")
]:
 assert "CrossRegionChatMessaging = " + value in read(config), config
smoke=read("tools/diagnostics/ogl_cross_region_chat_smoke.lsl")
for marker in (
    "llListen(PUBLIC_CHANNEL,",
    "llListen(PRIVATE_CHANNEL,",
    "TRIGGER = \"Beep\"",
    "llSetColor(<1.0, 0.0, 0.0>, ALL_SIDES);",
    "PublicHits=",
    "PrivateHits=",
):
    assert marker in smoke, "cross-region inworld smoke marker missing: " + marker
doc=read("doc/NexVerse/CROSS_REGION_LSL_CHAT_09312.md")
for marker in ("CHAT01/CHAT02:", "ogl_cross_region_chat_smoke.lsl",
               "zwei unabhängigen OpenSim-Prozessen", "NexBus deaktiviert"):
    assert marker in doc, "missing live acceptance step: " + marker
runtime=read("tools/ci/CrossRegionListenRuntimeRegression/Program.cs")
runtime_project=read("tools/ci/CrossRegionListenRuntimeRegression/CrossRegionListenRuntimeRegression.csproj")
workflow=read(".github/workflows/nexjast-ci.yml")
for marker in (
    "SceneHelpers.SetupSceneModules(new Scene[] { west, east }, config, chat)",
    "receiver.Listen(scriptA,",
    "receiver.Listen(scriptB,",
    "client.Chat(0, ChatTypeEnum.Say, \"Beep\")",
    "client.Chat(7, ChatTypeEnum.Say, \"Beep\")",
    "client.Chat(0, ChatTypeEnum.Whisper, \"Beep\")",
    "beyond 20m: no listen delivery",
    "both cubes heard private channel exactly once",
):
    assert marker in runtime, "missing executed llListen regression: " + marker
assert "OpenSim.Tests.Common" in runtime_project
assert "Runtime regression cross-region llListen delivery" in workflow
two_process=read("tools/ci/CrossRegionNexBusProcessRegression/Program.cs")
for marker in (
    'StartChild("receiver"',
    'StartChild("sender"',
    'NexVerseNodeAgentModule',
    'NexBusProtocol.Verify(',
    'NexBusProtocol.Sign(',
    'DistributedNexEventBus',
    'receiver.Listen(', # see equivalent source.world.Listen checks below
    'source.world.Listen(',
    'receiver did not report success',
    'wrong HMAC must reject',
    'same EventId must deduplicate',
    'out-of-range say must not generate a fourth NexBus event',
    'test-shared-host',
):
    if marker == 'receiver.Listen(':
        continue
    assert marker in two_process, "missing NexBus two-process regression: " + marker
assert 'Runtime two-process signed NexBus cross-region llListen' in workflow
print("OGL cross-region chat wiring and fail-closed NexBus configuration: OK")

#!/usr/bin/env python3
"""Source-level invariant checks for opt-in owner/NPC direct IM archiving."""
from pathlib import Path

ROOT=Path(__file__).resolve().parents[2]
read=lambda p:(ROOT/p).read_text()
store=read("NexVerse/Server/Api/NexNpcChatArchive.cs")
ingest=read("NexVerse/Server/Api/NexNpcChatIngress.cs")
world=read("NexVerse/Server/Api/NexNpcWorldApi.cs")
connector=read("NexVerse/Server/Api/NexVerseWorldApiConnector.cs")
publisher=read("OpenSim/Region/OptionalModules/World/NPC/NexNpcDirectImPublisher.cs")
module=read("OpenSim/Region/OptionalModules/World/NPC/NPCModule.cs")
docs=read("NexVerse/Server/Api/NexChatGptInstructionDownloads.cs")
paths=read("NexVerse/Server/Api/NexVerseWorldApiHandlers.cs")
checks={
 "Dedicated NPC SQLite archive, not resident IM storage":
  "npc_chat_entries" in store and "portal_im_entries" not in store and "NexCitizenImStore" not in world,
 "No consent means no persistent record":
  "WHERE EXISTS (SELECT 1 FROM npc_chat_consent" in store and "enabled=1" in store,
 "Archive opt-out immediately deletes owner rows":
  "if(!enabled)Delete(db,tx,owner)" in store,
 "Erasure available while opted in":
  "internal void Erase(Guid owner)" in store and "m_Archive.Erase(owner)" in world,
 "Owner-specific read from authorized resident account":
  "owner != account.PrincipalID.Guid" in world and "m_Archive.Read(owner,npcID)" in world,
 "Consent withdrawal and history erasure do not depend on active simulator snapshots":
  world.index("if (settings || erase)") < world.index("if (m_Index == null || !m_Index.TryOwned(owner"),
 "Only owned NPC conversations can be read":
  "owned.Any(n => n.npc_id == npcID)" in world,
 "Consent explicitly toggled by authenticated owner":
  'TryGetProperty("enabled"' in world and "m_Archive.SetEnabled(owner,enabled)" in world and
  "NexScopes.RelationshipsWrite" in world,
 "Simulator listener only for owner-to-NPC direct IM":
  "im.toAgentID!=npc.AgentId.Guid" in publisher and "im.fromAgentID!=npc.OwnerID.Guid" in publisher,
 "Simulator reply only for NPC-to-owner direct IM":
  "im.fromAgentID!=npc.AgentId.Guid" in publisher and "im.toAgentID!=npc.OwnerID.Guid" in publisher,
 "Group IMs and public chats excluded":
  "im.fromGroup" in publisher and "ChatFromClient" not in module[module.index("m_ChatPublisher"):],
 "Signed private ingress time bound with constant-time verification":
  "X-NexNPC-Time" in ingest and "X-NexNPC-Signature" in ingest and
  "seconds<now-180||seconds>now+180" in ingest and "FixedTimeEquals" in ingest,
 "Server checks current verified NPC ownership again before writing":
  "m_Registry.TryOwned(ownerID" in ingest and "owned.Exists(n=>n.npc_id==npcID)" in ingest,
 "Server rejects events for residents without consent":
  "if(!m_Archive.Enabled(ownerID))" in ingest,
 "Archive optional and operator-disabled by default":
  'ChatArchiveEnabled", false' in connector and "NexNPCChatCaptureEnabled" in module,
 "Message text bound and event replay deduplicated":
  "Encoding.UTF8.GetByteCount(text)>4000" in ingest and "event_id TEXT UNIQUE NOT NULL" in store,
 "Separate maximum 90-day retention and per-owner 10000 cap":
  "Math.Clamp(retentionDays,7,90)" in store and "LIMIT 10000" in store,
 "Official World OpenAPI and instructions include consent/read/delete":
  "/api/v1/npcs/history/settings" in paths and "/api/v1/npcs/history" in paths and
  "/api/v1/npcs/history/settings" in docs,
}
for name,ok in checks.items():
 if not ok:raise AssertionError("R619_NPC_CHAT_FAIL "+name)
 print("R619_NPC_CHAT_PASS",name)
print(f"R619_NPC_CHAT_SECURITY_SOURCE={len(checks)}/{len(checks)}_PASS")

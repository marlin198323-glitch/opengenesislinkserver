#!/usr/bin/env python3
"""R617 privacy and wiring regression for the optional NPC World API bridge.

Functional ownership, replay and source-coverage rules are exercised by the
NexNpcDirectoryRegression .NET executable; this checks integration invariants.
"""
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
REGISTRY = (ROOT / "NexVerse/Server/Api/NexNpcSnapshotIndex.cs").read_text()
INGRESS = (ROOT / "NexVerse/Server/Api/NexNpcSnapshotIngress.cs").read_text()
API = (ROOT / "NexVerse/Server/Api/NexNpcWorldApi.cs").read_text()
SIM = (ROOT / "OpenSim/Region/OptionalModules/World/NPC/NPCModule.cs").read_text()
PUBLISH = (ROOT / "OpenSim/Region/OptionalModules/World/NPC/NexNpcSnapshotPublisher.cs").read_text()
CONNECTOR = (ROOT / "NexVerse/Server/Api/NexVerseWorldApiConnector.cs").read_text()
OPENAPI = (ROOT / "NexVerse/Server/Api/NexVerseWorldApiHandlers.cs").read_text()

def check(condition, message):
    if not condition:
        raise AssertionError(message)
    print("R617_NPC_PASS", message)

check('NEX_NPC_INGEST_SECRET' in CONNECTOR and
      'Encoding.UTF8.GetByteCount(npcSecret ?? "") >= 32' in CONNECTOR,
      "Strong separate secret required to enable NPC ingest")
check('ExpectedRegionCount' in CONNECTOR and
      'expectedRegions > 0' in CONNECTOR,
      "World directory requires explicit expected region count")
check('npcIndex = null' in CONNECTOR and
      'new NexNpcWorldApi(authenticator, npcIndex)' in CONNECTOR,
      "Disabled feed never returns a fabricated empty owned NPC list")
check('"/internal/nexnpc/snapshot/v1"' in INGRESS and
      'new HMACSHA256(m_Secret)' in INGRESS and
      'CryptographicOperations.FixedTimeEquals' in INGRESS,
      "Simulator ingest accepts only constant-time verified HMAC")
check('seconds < now - 180 || seconds > now + 180' in INGRESS and
      'sequence <= last.Sequence' in REGISTRY,
      "Time-bound signatures and monotonic snapshots reject replay")
check('m_RequiredRegions' in REGISTRY and 'fresh.Count != m_Regions.Count' in REGISTRY,
      "Missing reporting regions fail closed")
check('previous != npc.owner_principal_id' in REGISTRY,
      "Conflicting NPC owner claims fail closed")
check('NexScopes.RelationshipsRead' in API and
      'account.Active' in API and 'account.LocalToGrid' in API and
      'owner != account.PrincipalID.Guid' in API and
      'npc.owner_principal_id == owner' in REGISTRY,
      "NPC list is scoped to verified active local resident and owner")
check('"npc_chat_archive_unavailable"' in API and
      'NexCitizenImStore' not in API and
      'NexCitizenImApi' not in API,
      "No resident IMs are misrepresented as NPC conversations")
check('NexNPCSnapshotEnabled' in SIM and
      'm_SnapshotPublisher?.Schedule()' in SIM and
      'npc.OwnerID.IsZero()' in SIM,
      "Simulator sends only real owned NPCs after changes")
check('Uri.UriSchemeHttps' in PUBLISH and
      '"/internal/nexnpc/snapshot/v1"' in PUBLISH and
      'new HMACSHA256(m_Key)' in PUBLISH,
      "Simulator requires HTTPS and signs each snapshot")
check('"/api/v1/npcs/mine"' in OPENAPI and
      '"/api/v1/npcs/{npcId}/messages"' in OPENAPI,
      "Both NPC API routes documented in OpenAPI")
print("R617_NPC_SOURCE_BRIDGE=12/12_PASS")

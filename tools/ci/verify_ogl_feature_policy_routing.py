#!/usr/bin/env python3
"""Check feature-policy routing never accepts чужde foreign node updates."""
from pathlib import Path

root = Path(__file__).resolve().parents[2]
read = lambda p: (root / p).read_text(encoding="utf-8")
gate = read("OpenSim/Framework/OglArchiveFeatureGate.cs")
simulator = read("OpenSim/Region/Application/OpenSim.cs")
agent = read("NexVerse/RegionModules/NodeAgent/NexVerseNodeAgentModule.cs")
checks = [
    ('public static string ResolveNodeId(', gate),
    ('public static bool IsForConfiguredNode(', gate),
    ('string.Equals(nodeId, s_NodeId, StringComparison.Ordinal)', gate),
    ('OglArchiveFeatureGate.ResolveNodeId(', agent),
    ('OglArchiveFeatureGate.ResolveNodeId(', simulator),
    ('OglArchiveFeatureGate.IsForConfiguredNode(node)', agent),
    ('Interlocked.CompareExchange(', agent),
    ('OglArchiveFeatureGate.Apply(node, revision, expires, flags, signature)', agent),
]
for value, body in checks:
    assert value in body, value
policy = agent.split("private void HandleFeaturePolicy(", 1)[1].split(
    "private void PublishHeartbeatSafe()", 1)[0]
assert policy.index("IsForConfiguredNode(node)") < policy.index("OglArchiveFeatureGate.Apply(")
assert "Ungültige, fremde oder abgelaufene Richtlinie verworfen." not in policy
assert "m_Log.WarnFormat(" in policy
assert "m_Log.DebugFormat(" in policy
assert "m_LastOwnFeaturePolicyWarningSeconds" in policy
print("Feature policy node routing and warning throttling: OK")

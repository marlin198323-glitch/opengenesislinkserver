#!/usr/bin/env python3
"""Guard readable, low-noise ROBUST console auditing without losing durable history."""
from pathlib import Path

root = Path(__file__).resolve().parents[2]
connector = (root / "NexVerse/Server/Api/NexVerseWorldApiConnector.cs").read_text(encoding="utf-8")
handlers = (root / "NexVerse/Server/Api/NexVerseWorldApiHandlers.cs").read_text(encoding="utf-8")
audit = (root / "NexVerse/Core/Audit/NexAudit.cs").read_text(encoding="utf-8")

for path in (
    "bin/Robust.HG.ini",
    "bin/Robust.HG.ini.example",
    "bin/Robust.HG.OGLAsset.ini.example",
    "bin/Robust.ini.example",
):
    config = (root / path).read_text(encoding="utf-8")
    section = config.split("\n[NexVerseWorldApi]\n", 1)[1].split("\n[", 1)[0]
    for setting in (
        'AuditConsoleMode = "important"',
        "AuditConsolePublicReads = false",
        "AuditConsoleShowIds = false",
        'AuditStorePath = "data/nexverse-audit.jsonl"',
    ):
        assert setting in section, (path, setting)

for marker in (
    'apiConfig.GetString("AuditConsoleMode", "important")',
    'apiConfig.GetBoolean("AuditConsolePublicReads", false)',
    'apiConfig.GetBoolean("AuditConsoleShowIds", false)',
    'new LogNexAuditSink(',
    'new PersistentNexAuditStore(auditStorePath)',
    'new CompositeNexAuditSink(',
    'publicAuditSink,\n                        auditStore)',
    'normalized != "important" && normalized != "all" && normalized != "off"',
    'if (m_Mode == "off")',
    'if (!m_PublicReads && IsPublicRoutine(auditEvent))',
    'return m_Mode != "important" || !IsRoutine(auditEvent);',
    'action.Equals("worldapi.root", StringComparison.OrdinalIgnoreCase)',
    'action.StartsWith("worldapi.", StringComparison.OrdinalIgnoreCase)',
    'action.EndsWith(".read", StringComparison.OrdinalIgnoreCase)',
    'auditEvent.CorrelationId, auditEvent.EventId',
    '[NEX-AUDIT] {0} | Aktion: {1} | Akteur: {2} | Ziel: {3}',
):
    assert marker in connector, "Audit console/persistence regression: " + marker

assert connector.index("new LogNexAuditSink(") < connector.index("new PersistentNexAuditStore(")
assert connector.index("new PersistentNexAuditStore(") < connector.index("new CompositeNexAuditSink(")
assert 'BeginRequest(response, "worldapi.health.read", "/api/v1/health")' in handlers
assert 'm_AuditSink.Record(new NexAuditEvent(' in handlers
assert "NexMetricsRegistry.Default.IncrementCounter(" in handlers
assert "foreach (INexAuditSink sink in m_Sinks)" in audit

# Explicitly verify control-plane actions are excluded only from console,
# never from the persisted administrative audit store.
console = connector.split("internal sealed class LogNexAuditSink", 1)[1]
assert "PersistentNexAuditStore" not in console
assert "m_Log.InfoFormat(" in console
assert "public void Record(NexAuditEvent auditEvent)" in console
assert console.index("if (auditEvent == null || !ShouldLog(auditEvent))") < console.index(
    "m_Log.InfoFormat(",
    console.index("public void Record(NexAuditEvent auditEvent)")
)

print("Audit console modes/quiet health checks/JSONL persistence: OK")

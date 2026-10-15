#!/usr/bin/env python3
"""Guard against silent YEngine script reset or deletion on .NET 10 load failures."""
from pathlib import Path

root = Path(__file__).resolve().parents[2]
path = root / "OpenSim/Region/ScriptEngine/YEngine/XMRInstCtor.cs"
src = path.read_text(encoding="utf-8-sig")
method = src.split("private void LoadInitialState()", 1)[1]
method = method.split("private void ", 1)[0]
assert "File.Delete(m_StateFileName)" not in method, (
    "Failed script state must not be deleted")
assert "PostEvent(EventParams.StateEntryParams);" in method, (
    "Fresh scripts still require normal state entry initialization")
assert "catch (Exception error)" in method, "Load failures must be handled"
assert "throw new InvalidDataException(" in method, (
    "Incompatible existing script state must abort initialization")
assert "was NOT reset" in method and "original .state file was preserved" in method, (
    "Operator-facing state retention explanation missing")
# Also ensure no restart in the failed-existing-state branch.
failure_branch = method.split("catch (Exception error)", 1)[1]
assert "PostEvent(EventParams.StateEntryParams)" not in failure_branch
capture = (root / "OpenSim/Region/ScriptEngine/YEngine/XMRInstCapture.cs").read_text(encoding="utf-8")
engine = (root / "OpenSim/Region/ScriptEngine/YEngine/XMREngine.cs").read_text(encoding="utf-8")
runtime = (root / "tools/ci/YEngineExceptionCodecRegression/Program.cs").read_text(encoding="utf-8")
assert "WriteStateFileAtomically(m_StateFileName, scriptStateN.OuterXml);" in capture
assert "XMRInstance.WriteStateFileAtomically(statePath, scriptStateN.OuterXml);" in engine
assert "File.Create(m_StateFileName)" not in capture, "snapshot writing must not truncate the old .state"
assert "File.Create(statePath)" not in engine, "OAR script restore must not truncate the old .state"
helper = capture.split("internal static void WriteStateFileAtomically(", 1)[1].split("        /**", 1)[0]
for marker in ("FileMode.CreateNew", "FileShare.None", "Guid.NewGuid()", "writer.Flush()", "stream.Flush(flushToDisk: true)", "File.Move(temporary, statePath, overwrite: true)", "File.Delete(temporary)"):
    assert marker in helper, "atomic YEngine writer missing: " + marker
for marker in ("complete UTF-8 script state atomically replaces previous snapshot",
               "failed replacement leaves the previous snapshot intact",
               "failed writer cleans temporary snapshots"):
    assert marker in runtime, "missing YEngine atomic writer regression: " + marker

constructor = (root / "OpenSim/Region/ScriptEngine/YEngine/XMRInstCtor.cs").read_text(encoding="utf-8")
for marker in (
    "DtdProcessing = DtdProcessing.Prohibit",
    "XmlResolver = null",
    "MaxCharactersInDocument = MaxPersistedStateXmlBytes",
    "MaxPersistedStateXmlBytes = 8 * 1024 * 1024",
    "Encoding.UTF8.GetByteCount(xml) > MaxPersistedStateXmlBytes",
    "ReadPersistedStateXmlFile(string path)",
    "ParsePersistedStateXml(string xml)",
    "stream.Length > MaxPersistedStateXmlBytes",
):
    assert marker in capture, "unsafe YEngine XML handling: " + marker
assert "ReadPersistedStateXmlFile(m_StateFileName)" in constructor, (
    "persisted .state read must use bounded XML parser")
assert "doc.LoadXml(xml)" not in constructor, "unbounded YEngine .state loader reintroduced"
assert "XMRInstance.ParsePersistedStateXml(xml)" in engine, (
    "imported OAR/crossing state must use bounded XML parser")
assert "doc.LoadXml(xml)" not in engine, "unbounded imported state parser reintroduced"
for marker in (
    "DTD and custom entities are rejected",
    "oversized imported XML state rejected before parse",
    "file reader blocks XML DTD",
    "oversized save cannot destroy previous script state",
    "oversized on-disk XML blocked before allocation",
):
    assert marker in runtime, "missing actual YEngine XML security regression: " + marker

print("YEngine .state preservation and no-restart gate: PASS")

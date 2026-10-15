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
print("YEngine .state preservation and no-restart gate: PASS")

#!/usr/bin/env python3
"""No arbitrary graph deserialization may execute in YEngine migration."""
from pathlib import Path
root=Path(__file__).resolve().parents[2]
abstract=(root/"OpenSim/Region/ScriptEngine/YEngine/XMRInstAbstract.cs").read_text()
codec=(root/"OpenSim/Region/ScriptEngine/YEngine/ScriptStateExceptionCodec.cs").read_text()
assert "BinaryFormatter bformatter" not in abstract
assert ".Deserialize(memoryStream)" not in abstract
for code in ("SYSERIALSAFE", "THROWNEXSAFE"):
    assert code in abstract
assert "case Ser.SYSERIAL:" in abstract and "case Ser.THROWNEX:" in abstract
assert "Legacy BinaryFormatter YEngine state is not supported" in abstract
assert "ScriptStateExceptionCodec.Write(mow, exception)" in abstract
assert "ScriptStateExceptionCodec.Read(mir)" in abstract
assert "ScriptStateExceptionCodec.WriteBoundedText(" in abstract
assert "ScriptStateExceptionCodec.ReadLimitedString(" in abstract
assert "throw new NotSupportedException(" in abstract
assert "GetType().FullName" in abstract
assert "Type.GetType(" not in codec
assert "new BinaryFormatter" not in codec
assert "Formatters.Binary" not in codec
assert "MaxTextLength = 16384" in codec
print("YEngine script state serialization rejects BinaryFormatter and unknown CLR types: OK")

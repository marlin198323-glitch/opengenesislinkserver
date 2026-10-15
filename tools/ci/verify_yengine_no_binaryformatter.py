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
# Reject assembly-qualified type resolution from untrusted script/OAR array tags.
assert "Type.GetType(" not in abstract
assert "Unsupported YEngine array type tag" in abstract
assert "YEngine array nesting limit exceeded" in abstract
assert "Unsupported YEngine state array element type" in abstract
assert "if (length < 0 || length > 65536)" in abstract
assert "new BinaryFormatter" not in codec
assert "Formatters.Binary" not in codec
assert "MaxTextLength = 16384" in codec
for bound in ("MaxMigrationDepth = 64", "MaxMigrationNodes = 131072",
              "MaxMigrationFrames = 4096", "MaxMigrationStringLength = 262144"):
    assert bound in abstract, f"Missing migration safety bound: {bound}"
assert "migrateInDepth > MaxMigrationDepth" in abstract
assert "migrateInNodes > MaxMigrationNodes" in abstract
assert "migrateOutNodes > MaxMigrationNodes" in abstract
assert "migrateOutDepth > MaxMigrationDepth" in abstract
assert "array.Length > 65536" in abstract
assert "sg.Length > MaxMigrationStringLength" in abstract
assert "frameHeaders > MaxMigrationFrames + 1" in abstract
assert "ScriptStateExceptionCodec.ReadLimitedString(" in abstract
assert "mir.ReadString()" not in abstract, "Unbounded migration string read remains"
assert "ReadLimitedString(BinaryReader reader, int maxCharacters)" in codec
assert "if (len > maxCharacters * 4)" in codec

print("YEngine script state rejects BinaryFormatter, unknown CLR types and unsafe array type tags: OK")

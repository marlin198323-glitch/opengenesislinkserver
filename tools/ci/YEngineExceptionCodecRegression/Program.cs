using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using OpenSim.Region.ScriptEngine.Yengine;

internal static class Program
{
    private static int checks;

    private static void Check(bool ok, string name)
    {
        ++checks;
        if (!ok) throw new Exception("YEngine typed exception regression failed: " + name);
    }

    private static void Invalid(Action a, string name)
    {
        ++checks;
        try { a(); }
        catch (InvalidDataException) { return; }
        catch (EndOfStreamException) { return; }
        throw new Exception("YEngine codec accepted invalid data: " + name);
    }

    private static byte[] Write(Exception e)
    {
        using MemoryStream buffer = new();
        using (BinaryWriter bw = new(buffer, Encoding.UTF8, true))
            ScriptStateExceptionCodec.Write(bw, e);
        return buffer.ToArray();
    }

    private static Exception Read(byte[] b)
    {
        using MemoryStream buffer = new(b);
        using BinaryReader br = new(buffer, Encoding.UTF8, true);
        Exception value = ScriptStateExceptionCodec.Read(br);
        Check(buffer.Position == buffer.Length, "no extra bytes on roundtrip");
        return value;
    }

    // Private serializer helpers are inspected through reflection to exercise
    // the actual engine implementation, not a duplicated test-only resolver.
    private static readonly MethodInfo EncodeType = typeof(XMRInstAbstract).GetMethod(
        "SysType2String", BindingFlags.NonPublic | BindingFlags.Static);
    private static readonly MethodInfo DecodeType = typeof(XMRInstAbstract).GetMethod(
        "String2SysType", BindingFlags.NonPublic | BindingFlags.Static);

    private static string Encode(Type t) => (string)EncodeType.Invoke(null, new object[] { t });
    private static Type Decode(string tag) => (Type)DecodeType.Invoke(null, new object[] { tag });

    private static void RejectType(Action action, string description)
    {
        ++checks;
        try { action(); }
        catch (TargetInvocationException e)
            when (e.InnerException is InvalidDataException ||
                  e.InnerException is NotSupportedException) { return; }
        throw new Exception("YEngine accepted unsafe CLR type: " + description);
    }

    private static void CheckType(Type expected, string wireTag)
    {
        Check(Encode(expected) == wireTag, "array serializer tag " + wireTag);
        Check(Decode(wireTag) == expected, "array deserializer tag " + wireTag);
    }

    private static readonly FieldInfo StateReader = typeof(XMRInstAbstract).GetField(
        "migrateInReader", BindingFlags.NonPublic | BindingFlags.Instance);
    private static readonly FieldInfo StateObjects = typeof(XMRInstAbstract).GetField(
        "migrateInObjects", BindingFlags.NonPublic | BindingFlags.Instance);
    private static readonly FieldInfo StateDepth = typeof(XMRInstAbstract).GetField(
        "migrateInDepth", BindingFlags.NonPublic | BindingFlags.Instance);
    private static readonly FieldInfo StateNodes = typeof(XMRInstAbstract).GetField(
        "migrateInNodes", BindingFlags.NonPublic | BindingFlags.Instance);
    private static readonly Type TokenEnum = typeof(XMRInstAbstract).GetNestedType(
        "Ser", BindingFlags.NonPublic);

    private static byte Token(string name) =>
        Convert.ToByte(Enum.Parse(TokenEnum, name));

    private static XMRInstAbstract NewStateReader(BinaryReader reader)
    {
        // This intentionally bypasses the live region script constructor;
        // only the isolated RecvObjValue parser is exercised.
        XMRInstAbstract state = (XMRInstAbstract)
            RuntimeHelpers.GetUninitializedObject(typeof(XMRInstance));
        StateReader.SetValue(state, reader);
        StateObjects.SetValue(state, new Dictionary<int, object>());
        return state;
    }

    private static object ParseState(byte[] data, int initialDepth = 0,
                                     int initialNodes = 0)
    {
        using MemoryStream bytes = new(data, false);
        using BinaryReader reader = new(bytes, Encoding.UTF8);
        XMRInstAbstract state = NewStateReader(reader);
        StateDepth.SetValue(state, initialDepth);
        StateNodes.SetValue(state, initialNodes);
        return state.RecvObjValue();
    }

    private static byte[] CreateState(Action<BinaryWriter> write)
    {
        using MemoryStream bytes = new();
        using (BinaryWriter writer = new(bytes, Encoding.UTF8, true))
            write(writer);
        return bytes.ToArray();
    }

    private static readonly FieldInfo StateWriter = typeof(XMRInstAbstract).GetField(
        "migrateOutWriter", BindingFlags.NonPublic | BindingFlags.Instance);
    private static readonly FieldInfo WriterObjects = typeof(XMRInstAbstract).GetField(
        "migrateOutObjects", BindingFlags.NonPublic | BindingFlags.Instance);
    private static readonly FieldInfo WriterDepth = typeof(XMRInstAbstract).GetField(
        "migrateOutDepth", BindingFlags.NonPublic | BindingFlags.Instance);
    private static readonly FieldInfo WriterNodes = typeof(XMRInstAbstract).GetField(
        "migrateOutNodes", BindingFlags.NonPublic | BindingFlags.Instance);

    private static byte[] EncodeSavedState(object value,
                                            int initialDepth = 0, int initialNodes = 0)
    {
        using MemoryStream output = new();
        using BinaryWriter writer = new(output, Encoding.UTF8);
        XMRInstAbstract state = (XMRInstAbstract)
            RuntimeHelpers.GetUninitializedObject(typeof(XMRInstance));
        StateWriter.SetValue(state, writer);
        WriterObjects.SetValue(state, new Dictionary<object, int>());
        WriterDepth.SetValue(state, initialDepth);
        WriterNodes.SetValue(state, initialNodes);
        state.SendObjValue(value);
        writer.Flush();
        return output.ToArray();
    }

    private static int Main()
    {
        Exception[] samples = {
            new Exception("base"),
            new ArgumentException("argument"),
            new ArgumentNullException("parameter"),
            new ArgumentOutOfRangeException("parameter"),
            new InvalidOperationException("bad state"),
            new NullReferenceException("null"),
            new IndexOutOfRangeException("index"),
            new DivideByZeroException("division"),
            new FormatException("format"),
            new OverflowException("overflow"),
            new NotSupportedException("unsupported"),
            new InvalidCastException("cast")
        };
        foreach (Exception sample in samples)
        {
            Exception cloned = Read(Write(sample));
            Check(cloned.GetType() == sample.GetType(),
                "roundtrip type " + sample.GetType().Name);
            Check(cloned.Message.Length != 0, "preserve nonempty message");
        }
        Check(Read(Write(new InvalidCastException("LSLString cannot cast"))).Message ==
            "LSLString cannot cast", "Firestorm-style exception text");

        bool unsupportedThrown = false;
        try { Write(new System.Net.Http.HttpRequestException("bad")); }
        catch (NotSupportedException) { unsupportedThrown = true; }
        Check(unsupportedThrown, "reject unknown CLR exception type");

        Invalid(() => Read(new byte[] { 5,0,0,0 }), "unknown version");
        Invalid(() => Read(new byte[] { 1,0,0,0, 0xff,0xff,0x7f }), "oversized type tag");
        Invalid(() => Read(new byte[] { 1,0,0,0, 0xff,0xff,0xff }), "unterminated length prefix");

        using (MemoryStream malformed = new())
        {
            using (BinaryWriter writer = new(malformed, Encoding.UTF8, true))
            {
                writer.Write(1);
                writer.Write("not-allowlisted");
                writer.Write("message");
            }
            Invalid(() => Read(malformed.ToArray()), "unknown type tag");
        }

        using (MemoryStream malformed = new())
        {
            using (BinaryWriter writer = new(malformed, Encoding.UTF8, true))
            {
                writer.Write(1);
                writer.Write("exception");
                writer.Write(new string('x', 16385));
            }
            Invalid(() => Read(malformed.ToArray()), "oversized message");
        }

        bool hugeRejected = false;
        try { Write(new Exception(new string('x', 16385))); }
        catch (InvalidDataException) { hugeRejected = true; }
        Check(hugeRejected, "oversized write rejected");

        using (MemoryStream str = new())
        {
            using (BinaryWriter writer = new(str, Encoding.UTF8, true))
                ScriptStateExceptionCodec.WriteBoundedText(writer, "thrown-event");
            str.Position = 0;
            using BinaryReader reader = new(str, Encoding.UTF8, true);
            Check(ScriptStateExceptionCodec.ReadLimitedString(reader) == "thrown-event",
                "ScriptThrownException bounded message roundtrip");
        }
        // Script-state arrays must not load arbitrary types or assemblies
        // from attacker-supplied OAR / script-state bytes.
        Check(EncodeType != null && DecodeType != null, "array codec methods present");
        CheckType(typeof(bool), "bo");
        CheckType(typeof(int[]), "in[]");
        CheckType(typeof(string[]), "st[]");
        CheckType(typeof(object[][]), "ob[][]");
        CheckType(typeof(object[][][][]), "ob[][][][]");
        CheckType(typeof(OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLFloat[]), "lf[]");
        CheckType(typeof(OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLInteger[]), "ln[]");
        CheckType(typeof(OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLString[]), "lk[]");
        RejectType(() => Decode("System.IO.FileInfo"), "reflection-qualified type");
        RejectType(() => Decode("System.String, System.Private.CoreLib"), "assembly type");
        RejectType(() => Decode("st[][][][][]"), "excessive array nesting");
        RejectType(() => Decode("st[, ]"), "multidimensional array");
        RejectType(() => Decode(""), "empty type");
        RejectType(() => Decode(new string('x', 100)), "oversized type string");
        RejectType(() => Encode(typeof(Uri[])), "unlisted CLR array element");
        RejectType(() => Encode(typeof(string[,])), "multidimensional array output");
        RejectType(() => Encode(typeof(object[][][][][])), "excessive array output");

        Check(TokenEnum != null && StateReader != null && StateObjects != null &&
              StateDepth != null && StateNodes != null,
              "real YEngine saved-state parser fields and tokens present");

        byte[] integers = CreateState(w =>
        {
            w.Write(Token("SYSARRAY"));
            w.Write("in");
            w.Write(2);
            w.Write(Token("SYSINT"));
            w.Write(42);
            w.Write(Token("SYSINT"));
            w.Write(-7);
        });
        int[] decodedInts = (int[])ParseState(integers);
        Check(decodedInts.Length == 2 && decodedInts[0] == 42 &&
              decodedInts[1] == -7, "real persisted integer array roundtrip");

        byte[] cyclic = CreateState(w =>
        {
            w.Write(Token("SYSARRAY"));
            w.Write("ob");
            w.Write(1);
            w.Write(Token("DUPREF"));
            w.Write(0);
        });
        object[] cyclicValue = (object[])ParseState(cyclic);
        Check(ReferenceEquals(cyclicValue, cyclicValue[0]),
            "reference-preserving circular array decoded");

        byte[] scalar = CreateState(w => w.Write(Token("NULL")));
        Invalid(() => ParseState(scalar, initialDepth: 64),
            "saved-state recursion cap");
        Invalid(() => ParseState(scalar, initialNodes: 131072),
            "saved-state node cap");

        byte[] invalidArray = CreateState(w =>
        {
            w.Write(Token("SYSARRAY"));
            w.Write("System.IO.FileInfo");
            w.Write(1);
            w.Write(Token("NULL"));
        });
        Invalid(() => ParseState(invalidArray),
            "saved-state CLR type resolution blocked");

        byte[] oversizedArray = CreateState(w =>
        {
            w.Write(Token("SYSARRAY"));
            w.Write("ob");
            w.Write(65537);
        });
        Invalid(() => ParseState(oversizedArray),
            "saved-state array element allocation capped");

        string longerText = new string('L', 20000);
        byte[] ordinaryString = CreateState(w =>
        {
            w.Write(Token("SYSSTR"));
            w.Write(longerText);
        });
        Check((string)ParseState(ordinaryString) == longerText,
            "legacy-compatible long LSL script string under new cap");
        byte[] hugeText = CreateState(w =>
        {
            w.Write(Token("SYSSTR"));
            w.Write(new string('L', 262145));
        });
        Invalid(() => ParseState(hugeText),
            "oversized persisted LSL script string blocked");

        Check(StateWriter != null && WriterObjects != null &&
              WriterDepth != null && WriterNodes != null,
              "real YEngine saved-state writer fields present");
        int[] roundtripInts = (int[])ParseState(EncodeSavedState(
            new int[] { 42, -7 }));
        Check(roundtripInts.Length == 2 && roundtripInts[0] == 42 &&
              roundtripInts[1] == -7, "native graph writer-to-reader roundtrip");
        object[] recursiveArray = new object[1];
        recursiveArray[0] = recursiveArray;
        object[] restoredRecursive = (object[])ParseState(
            EncodeSavedState(recursiveArray));
        Check(ReferenceEquals(restoredRecursive, restoredRecursive[0]),
            "writer/reader preserve circular references");
        Invalid(() => EncodeSavedState(null, initialNodes: 131072),
            "writer node cap");
        Invalid(() => EncodeSavedState(null, initialDepth: 64),
            "writer nesting cap");
        Invalid(() => EncodeSavedState(new object[65537]),
            "writer array allocation cap");
        Invalid(() => EncodeSavedState(new string('Q', 262145)),
            "writer string length cap");
        Check((string)ParseState(EncodeSavedState(
            new string('Q', 20000))) == new string('Q', 20000),
            "writer/reader preserve valid long script strings");

        Console.WriteLine("YEngine exception state codec: PASS (" + checks + " checks)");
        return 0;
    }
}

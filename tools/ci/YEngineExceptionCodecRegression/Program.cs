using System;
using System.IO;
using System.Reflection;
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

        Console.WriteLine("YEngine exception state codec: PASS (" + checks + " checks)");
        return 0;
    }
}

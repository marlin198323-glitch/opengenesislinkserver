using System;
using System.IO;
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
        Console.WriteLine("YEngine exception state codec: PASS (" + checks + " checks)");
        return 0;
    }
}

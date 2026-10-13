using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using NexVerse.RegionModules.Archives;

internal static class Program
{
    private record Entry(string Path, byte[] Payload, byte Type = (byte)'0');

    private static void Assert(bool test, string reason)
    {
        if (!test) throw new InvalidOperationException("IAR preflight regression: " + reason);
    }

    private static void WriteOctal(byte[] header, int offset, int width, long value)
    {
        string oct = Convert.ToString(value, 8) ?? "0";
        Assert(oct.Length <= width - 1, "test octal field overflow");
        byte[] bytes = Encoding.ASCII.GetBytes(oct.PadLeft(width - 1, '0') + '\0');
        Array.Copy(bytes, 0, header, offset, width);
    }

    private static void WriteEntry(Stream tar, Entry e, bool corruptChecksum = false)
    {
        byte[] header = new byte[512];
        byte[] name = Encoding.ASCII.GetBytes(e.Path);
        Assert(name.Length <= 100, "test name too long");
        Array.Copy(name, header, name.Length);
        WriteOctal(header, 100, 8, 420);
        WriteOctal(header, 108, 8, 0);
        WriteOctal(header, 116, 8, 0);
        WriteOctal(header, 124, 12, e.Payload.LongLength);
        WriteOctal(header, 136, 12, 0);
        for (int i = 148; i < 156; i++) header[i] = 32;
        header[156] = e.Type;
        Array.Copy(Encoding.ASCII.GetBytes("ustar\0"), 0, header, 257, 6);
        long sum = 0;
        foreach (byte x in header) sum += x;
        WriteOctal(header, 148, 8, sum + (corruptChecksum ? 1 : 0));
        tar.Write(header);
        tar.Write(e.Payload);
        int pad = (int)((512 - e.Payload.LongLength % 512) % 512);
        if (pad > 0) tar.Write(new byte[pad]);
    }

    private static string MakeArchive(Entry[] entries, bool corruptChecksum = false)
    {
        string destination = Path.Combine(Path.GetTempPath(), "ogl-iar-test-" + Guid.NewGuid().ToString("N") + ".iar");
        using (FileStream file = File.Create(destination))
        using (GZipStream gz = new(file, CompressionLevel.Fastest))
        {
            for (int i = 0; i < entries.Length; i++)
                WriteEntry(gz, entries[i], corruptChecksum && i == 0);
            gz.Write(new byte[1024]);
        }
        return destination;
    }

    private static void Evaluate(bool expected, string reason, Entry[] entries, bool badChecksum = false)
    {
        string file = MakeArchive(entries, badChecksum);
        try
        {
            OglIarInspection result = OglIarInspector.Inspect(file);
            Assert(result.Valid == expected, reason + ": " + result.Error);
        }
        finally { File.Delete(file); }
    }

    private static int Main()
    {
        var xml = new Entry("archive.xml", Encoding.UTF8.GetBytes("<archive major_version=\"1\" minor_version=\"0\"/>"));
        var inventory = new Entry("inventory/Test/item.xml", Encoding.UTF8.GetBytes("<InventoryItem/>"));
        var asset = new Entry("assets/01234567-89ab-cdef-0123-456789abcdef_texture.jp2", new byte[] { 1, 2, 3 });
        Evaluate(true, "valid IAR", new[] { xml, inventory, asset });
        Evaluate(false, "traversal", new[] { xml, new Entry("inventory/../evil.xml", new byte[] { 1 }) });
        Evaluate(false, "absolute path", new[] { xml, new Entry("/inventory/evil.xml", new byte[] { 1 }) });
        Evaluate(false, "duplicate inventory path", new[] { xml, inventory, inventory });
        Evaluate(false, "unsupported link type", new[] { xml, new Entry("inventory/evil", Array.Empty<byte>(), (byte)'2') });
        Evaluate(false, "wrong ordering", new[] { xml, asset, inventory });
        Evaluate(false, "invalid major version", new[] { new Entry("archive.xml", Encoding.UTF8.GetBytes("<archive major_version=\"9\" minor_version=\"0\"/>")), inventory });
        Evaluate(false, "XXE/DTD", new[] { new Entry("archive.xml", Encoding.UTF8.GetBytes("<!DOCTYPE archive [ <!ENTITY leak SYSTEM \"file:///etc/passwd\"> ]><archive major_version=\"1\" minor_version=\"0\"/>")), inventory });
        Evaluate(false, "no control file", new[] { inventory });
        Evaluate(false, "checksum mismatch", new[] { xml, inventory }, badChecksum: true);
        Evaluate(false, "overlarge single payload", new[] { xml, new Entry("assets/big.bin", new byte[OglIarInspector.MaxFileBytes > int.MaxValue ? 1 : (int)OglIarInspector.MaxFileBytes + 1]), inventory });
        Console.WriteLine("OGL IAR bounded preflight: PASS (11 valid/adversarial fixtures)");
        return 0;
    }
}

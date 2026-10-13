using System;
using System.Formats.Tar;
using System.IO;
using System.IO.Compression;
using System.Text;
using OpenSim.Region.CoreModules.Avatar.Inventory.Archiver;

internal static class Program
{
    static int m_Tests;
    static void Test(string name, bool expected, byte[] bytes)
    {
        using MemoryStream input = new MemoryStream(bytes, writable: false);
        bool actual = OglIarImportGuard.TryValidate(input, out string error);
        if (actual != expected || input.Position != 0)
            throw new Exception(name + ": expected=" + expected + " actual=" + actual +
                                " restored=" + input.Position + " error=" + error);
        Console.WriteLine("PASS " + name + (actual ? "" : " (rejected)"));
        m_Tests++;
    }

    static byte[] Archive(params (string Path, string Value)[] entries)
    {
        using MemoryStream output = new MemoryStream();
        using (GZipStream gzip = new GZipStream(output, CompressionLevel.Fastest, leaveOpen: true))
        using (TarWriter writer = new TarWriter(gzip, TarEntryFormat.Ustar, leaveOpen: true))
            foreach (var (path, value) in entries)
            {
                UstarTarEntry item = new UstarTarEntry(TarEntryType.RegularFile, path);
                item.DataStream = new MemoryStream(Encoding.UTF8.GetBytes(value));
                writer.WriteEntry(item);
            }
        return output.ToArray();
    }

    static readonly (string Path, string Value) Control =
        ("archive.xml", "<archive major_version='0' minor_version='3' />");
    static readonly (string Path, string Value) Item =
        ("inventory/folder_item.xml", "<InventoryItem><Name>Test</Name></InventoryItem>");

    static int Main()
    {
        byte[] valid = Archive(Control, Item);
        Test("clean gzip/TAR with control and inventory XML", true, valid);
        Test("missing control file", false, Archive(Item));
        Test("no inventory", false, Archive(Control));
        Test("duplicate archive entry", false, Archive(Control, Item, Item));
        Test("invalid archive control XML", false,
            Archive(("archive.xml", "<wrong />"), Item));
        Test("invalid inventory item XML", false,
            Archive(Control, ("inventory/bad.xml", "<a>")));
        Test("unsafe traversal", false,
            Archive(Control, ("inventory/../bad.xml", "<a />")));
        Test("extra unrelated file", false,
            Archive(Control, Item, ("unknown/thing.txt", "hello")));
        byte[] truncated = new byte[valid.Length - 12];
        Array.Copy(valid, truncated, truncated.Length);
        Test("truncated gzip", false, truncated);
        Test("not gzip", false, Encoding.UTF8.GetBytes("not an archive"));
        Console.WriteLine("OGL IAR preflight regression: " + m_Tests + " cases PASS");
        return 0;
    }
}

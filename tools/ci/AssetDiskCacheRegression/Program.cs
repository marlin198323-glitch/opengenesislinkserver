using System;
using System.IO;
using OpenSim.Framework;

internal static class Program
{
    private static int checks;
    private static void Check(bool correct, string description)
    {
        checks++;
        if (!correct) throw new Exception("Safe asset cache: " + description);
    }

    private static void RejectInvalid(Action action, string description)
    {
        checks++;
        try { action(); }
        catch (InvalidDataException) { return; }
        catch (EndOfStreamException) { return; }
        throw new Exception("Unsafe asset cache accepted: " + description);
    }

    public static int Main()
    {
        AssetBase original = new()
        {
            ID = "asset-example-id-456",
            Name = "Ä texture",
            Description = "Safe typed disk serialization",
            CreatorID = "creator-example",
            Type = 0,
            Local = true,
            Temporary = false,
            Flags = AssetFlags.Rewritable | AssetFlags.Collectable,
            UploadAttempts = 7,
            Data = new byte[] {0, 1, 2, 0, 253, 254, 255}
        };
        original.Metadata.CreationDate =
            new DateTime(2026, 10, 14, 12, 34, 56, DateTimeKind.Utc);
        original.Metadata.SHA1 = new byte[] { 1, 2, 3 };

        using MemoryStream stream = new();
        AssetDiskCacheCodec.Write(stream, original);
        byte[] payload = stream.ToArray();
        stream.Position = 0;
        AssetBase restored = AssetDiskCacheCodec.Read(stream, original.ID);
        Check(restored.ID == original.ID, "asset ID");
        Check(restored.Name == original.Name && restored.Description == original.Description,
            "UTF-8 name and description");
        Check(restored.CreatorID == original.CreatorID, "creator");
        Check(restored.Type == original.Type && restored.Local == original.Local &&
            restored.Temporary == original.Temporary && restored.Flags == original.Flags,
            "all essential metadata");
        Check(restored.UploadAttempts == 7 && restored.Data.Length == 7 &&
            restored.Data[4] == 253 && restored.Data[6] == 255, "binary payload");
        Check(restored.Metadata.CreationDate == original.Metadata.CreationDate &&
            restored.Metadata.SHA1.Length == 3, "created date and SHA1");

        stream.Position = 0;
        RejectInvalid(() => AssetDiskCacheCodec.Read(stream, "another-id"),
            "wrong cache key");
        RejectInvalid(() => AssetDiskCacheCodec.Read(new MemoryStream(new byte[32])),
            "unknown/BinaryFormatter legacy format");

        byte[] truncated = new byte[payload.Length - 3];
        Array.Copy(payload, truncated, truncated.Length);
        RejectInvalid(() => AssetDiskCacheCodec.Read(new MemoryStream(truncated)),
            "truncated asset data");

        using MemoryStream hugeLength = new();
        using (BinaryWriter writer = new(hugeLength, System.Text.Encoding.UTF8, true))
        {
            writer.Write(0x414C474Fu);
            writer.Write(1);
            writer.Write(int.MaxValue); // malicious first string length
        }
        hugeLength.Position = 0;
        RejectInvalid(() => AssetDiskCacheCodec.Read(hugeLength),
            "malicious oversized string field");

        byte[] trailing = new byte[payload.Length + 1];
        Array.Copy(payload, trailing, payload.Length);
        RejectInvalid(() => AssetDiskCacheCodec.Read(new MemoryStream(trailing)),
            "unexpected trailing bytes");

        Console.WriteLine("Safe Flotsam disk-cache codec: PASS (" + checks + " checks)");
        return 0;
    }
}

// Real zlib-wrapped LLSD mesh data: no mocked decode success.
using System;
using System.IO;
using System.IO.Compression;
using OpenMetaverse.StructuredData;
using OpenSim.Region.CoreModules.World.Warp3DMap;

internal static class Program
{
    private static void Check(bool ok, string reason)
    {
        if (!ok) throw new InvalidOperationException("Warp3D mesh preflight: " + reason);
    }

    private static byte[] Compress(OSD lod)
    {
        byte[] bytes = OSDParser.SerializeLLSDBinary(lod, true);
        using MemoryStream stream = new MemoryStream();
        using (ZLibStream compressor = new ZLibStream(stream, CompressionLevel.Optimal, true))
            compressor.Write(bytes, 0, bytes.Length);
        return stream.ToArray();
    }

    private static byte[] BuildAsset(string lodName, byte[] lod, int offset = 0, int? size = null)
    {
        var header = new OSDMap
        {
            [lodName] = new OSDMap {
                ["offset"] = OSD.FromInteger(offset),
                ["size"] = OSD.FromInteger(size ?? lod.Length)
            }
        };
        byte[] first = OSDParser.SerializeLLSDBinary(header, true);
        byte[] bytes = new byte[first.Length + lod.Length];
        Buffer.BlockCopy(first, 0, bytes, 0, first.Length);
        Buffer.BlockCopy(lod, 0, bytes, first.Length, lod.Length);
        return bytes;
    }

    private static byte[] ValidLod()
    {
        var meshFace = new OSDMap
        {
            ["Position"] = OSD.FromBinary(new byte[18]),
            ["TriangleList"] = OSD.FromBinary(new byte[] { 0, 0, 1, 0, 2, 0 })
        };
        return Compress(new OSDArray { meshFace });
    }

    private static int Main()
    {
        byte[] block = ValidLod();
        byte[] high = BuildAsset("high_lod", block);
        Check(WarpMeshAssetPreflight.TryValidate(high, 3, out int selected, out _) &&
            selected == 3, "valid high visual LOD was rejected");
        Check(WarpMeshAssetPreflight.TryValidate(high, 0, out selected, out _) &&
            selected == 3, "missing requested low LOD did not fall back to valid high LOD");
        Check(!WarpMeshAssetPreflight.TryValidate(null, 3, out _, out _),
            "null payload accepted");
        Check(!WarpMeshAssetPreflight.TryValidate(new byte[10], 3, out _, out _),
            "truncated LLSD header accepted");
        Check(!WarpMeshAssetPreflight.TryValidate(
            BuildAsset("high_lod", block, int.MaxValue, block.Length), 3,
            out _, out _), "overflowing compressed block offset accepted");
        Check(!WarpMeshAssetPreflight.TryValidate(
            BuildAsset("high_lod", block, 0, int.MaxValue), 3,
            out _, out _), "overflowing compressed block size accepted");
        Check(!WarpMeshAssetPreflight.TryValidate(
            BuildAsset("high_lod", new byte[12]), 3, out _, out _),
            "non-zlib LOD accepted");
        Check(!WarpMeshAssetPreflight.TryValidate(
            BuildAsset("high_lod", Compress(new OSDMap())), 3, out _, out _),
            "LOD that is not an LLSD face array accepted");
        var invalidFaces = new OSDArray {
            new OSDMap {
                ["Position"] = OSD.FromBinary(new byte[6]),
                ["TriangleList"] = OSD.FromBinary(new byte[] { 0, 0, 1, 0, 2, 0 })
            }
        };
        Check(!WarpMeshAssetPreflight.TryValidate(
            BuildAsset("high_lod", Compress(invalidFaces)), 3, out _, out _),
            "out-of-range triangle vertex accepted");
        var noTriangles = new OSDArray {
            new OSDMap { ["Position"] = OSD.FromBinary(new byte[18]) }
        };
        Check(!WarpMeshAssetPreflight.TryValidate(
            BuildAsset("high_lod", Compress(noTriangles)), 3, out _, out _),
            "incomplete visual face accepted");

        Console.WriteLine("OGL Warp3D mesh LLSD+zlib preflight and LOD fallback: OK");
        return 0;
    }
}

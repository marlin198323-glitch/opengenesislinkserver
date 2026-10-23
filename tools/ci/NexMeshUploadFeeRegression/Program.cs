// SPDX-License-Identifier: MPL-2.0
// Real LLSD instance structures as sent in mesh upload fee requests.
using System;
using System.Collections;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using OpenSim.Framework.Capabilities;
using OpenSim.Region.ClientStack.Linden;
using OSDMap = OpenMetaverse.StructuredData.OSDMap;
using OSDArray = OpenMetaverse.StructuredData.OSDArray;

internal static class Program
{
    private static void Assert(bool result, string reason)
    {
        if (!result) throw new InvalidOperationException("Mesh fee LLSD regression: " + reason);
    }

    private static OSDMap Instance(OSD scale, OSD mesh = null)
    {
        var map = new OSDMap { ["scale"] = scale };
        if (mesh != null) map["mesh"] = mesh;
        return map;
    }

    private static OSDArray Scale(double x, double y, double z)
    {
        return new OSDArray {
            OSD.FromReal(x), OSD.FromReal(y), OSD.FromReal(z)
        };
    }

    // Exercise the actual private fee-stage decoders with real zlib-wrapped
    // binary LLSD, not just the newer typed LLSD instance converter.
    private static byte[] CompressLlsd(OSD value)
    {
        byte[] serialized = OSDParser.SerializeLLSDBinary(value, true);
        using (MemoryStream output = new MemoryStream())
        {
            using (ZLibStream compressor = new ZLibStream(output, CompressionLevel.Optimal, true))
                compressor.Write(serialized, 0, serialized.Length);
            return output.ToArray();
        }
    }

    private static bool DecodePrivateSection(string methodName, byte[] compressed, object[] args)
    {
        MethodInfo method = typeof(ModelCost).GetMethod(methodName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert(method != null, "missing mesh fee decoder: " + methodName);
        return (bool)method.Invoke(new ModelCost(), args);
    }

    private static void VerifyZlibGeometryDecoding()
    {
        var triangle = new OSDMap {
            ["Position"] = OSD.FromBinary(new byte[6]),
            ["TriangleList"] = OSD.FromBinary(new byte[6])
        };
        byte[] lod = CompressLlsd(new OSDArray { triangle });
        object[] lodArgs = { lod, 0, lod.Length, 0, 0 };
        Assert(DecodePrivateSection("submesh", lod, lodArgs),
            "valid zlib-wrapped LOD LLSD rejected");
        Assert((int)lodArgs[3] == 1 && (int)lodArgs[4] == 1,
            "LOD triangle/side counts were not decoded");

        var convex = new OSDMap {
            ["BoundingVerts"] = OSD.FromBinary(new byte[18])
        };
        byte[] hull = CompressLlsd(convex);
        object[] hullArgs = { hull, 0, hull.Length, 0, 0, string.Empty };
        Assert(DecodePrivateSection("hulls", hull, hullArgs),
            "valid zlib-wrapped convex hull LLSD rejected");
        Assert((int)hullArgs[3] == 3 && (int)hullArgs[4] == 1,
            "convex hull vertex count was not decoded");

        // The parser must also reject invalid compressed payloads and
        // invalid geometry, rather than silently accepting malformed data.
        byte[] invalidZlib = { 0, 0, 0, 0, 0, 0 };
        object[] invalidArgs = { invalidZlib, 0, invalidZlib.Length, 0, 0 };
        Assert(!DecodePrivateSection("submesh", invalidZlib, invalidArgs),
            "invalid zlib data accepted as LOD");
        byte[] noTriangles = CompressLlsd(new OSDArray {
            new OSDMap { ["Position"] = OSD.FromBinary(new byte[6]) }
        });
        object[] missingTriangleArgs = { noTriangles, 0, noTriangles.Length, 0, 0 };
        Assert(!DecodePrivateSection("submesh", noTriangles, missingTriangleArgs),
            "LOD without TriangleList accepted");

        object[] invalidHullArgs = { invalidZlib, 0, invalidZlib.Length, 0, 0, string.Empty };
        Assert(!DecodePrivateSection("hulls", invalidZlib, invalidHullArgs),
            "invalid zlib data accepted as hull");
        byte[] missingVerts = CompressLlsd(new OSDMap());
        object[] missingVertsArgs = { missingVerts, 0, missingVerts.Length, 0, 0, string.Empty };
        Assert(!DecodePrivateSection("hulls", missingVerts, missingVertsArgs),
            "hull without BoundingVerts accepted");
    }

    private static void Main()
    {
        // These are OSD maps/arrays after real LLSD decoding, NOT
        // legacy Hashtable/ArrayList. Valid uploads used to throw.
        var ok = Instance(Scale(1.5, 2.0, 0.5), OSD.FromInteger(0));
        Assert(ModelCost.TryParseModelInstance(ok, 1, out Vector3 scale,
            out int mesh, out string error),
            "valid mesh instance rejected: " + error);
        Assert(mesh == 0 && scale.X == 1.5f && scale.Y == 2f &&
            scale.Z == 0.5f, "valid LLSD values were not preserved");

        var other = Instance(Scale(1, 1, 1), OSD.FromInteger(2));
        Assert(ModelCost.TryParseModelInstance(other, 3, out _,
            out mesh, out _) && mesh == 2,
            "multi-mesh index rejected");
        var prim = Instance(Scale(1, 2, 3));
        Assert(ModelCost.TryParseModelInstance(prim, 0, out _,
            out mesh, out _) && mesh == -1,
            "non-mesh instance rejected");
        Assert(!ModelCost.TryParseModelInstance(other, 2, out _,
            out _, out error) && error.Contains("bounds"),
            "out-of-range mesh index accepted");
        Assert(!ModelCost.TryParseModelInstance(
            Instance(Scale(1, 1, 1), OSD.FromInteger(-1)), 2,
            out _, out _, out _), "negative mesh index accepted");
        Assert(!ModelCost.TryParseModelInstance(
            Instance(Scale(1, 1, 1), OSD.FromString("0")), 2,
            out _, out _, out _), "noninteger mesh reference accepted");
        Assert(!ModelCost.TryParseModelInstance(
            Instance(new OSDArray { OSD.FromInteger(1) }), 2,
            out _, out _, out _), "truncated scale accepted");
        Assert(!ModelCost.TryParseModelInstance(
            Instance(Scale(1, 0, 1)), 1,
            out _, out _, out _), "zero scale accepted");
        Assert(!ModelCost.TryParseModelInstance(
            Instance(Scale(double.NaN, 1, 1)), 1,
            out _, out _, out _), "NaN scale accepted");
        Assert(!ModelCost.TryParseModelInstance(
            Instance(Scale(double.PositiveInfinity, 1, 1)), 1,
            out _, out _, out _), "infinite scale accepted");
        Assert(!ModelCost.TryParseModelInstance(
            Instance(Scale(double.MaxValue, 1, 1)), 1,
            out _, out _, out _), "overflow scale accepted");
        Assert(!ModelCost.TryParseModelInstance(new OSDMap(), 1,
            out _, out _, out _), "missing scale accepted");
        Assert(!ModelCost.TryParseModelInstance(OSD.FromString("bad"), 1,
            out _, out _, out _), "non-map instance accepted");
        // Real CAPS fee decoding uses the legacy LLSD XML parser: nested
        // arrays retain Hashtable / ArrayList / byte[] rather than OSD values.
        const string feeXml = @"<llsd><map>
          <key>asset_resources</key><map>
            <key>instance_list</key><array><map>
              <key>scale</key><array><real>1.5</real><real>2</real><real>0.5</real></array>
              <key>mesh</key><integer>0</integer>
            </map></array>
            <key>mesh_list</key><array><binary encoding=""base64"">AQID</binary></array>
          </map></map></llsd>";
        var legacyRoot = (Hashtable)LLSD.LLSDDeserialize(Encoding.UTF8.GetBytes(feeXml));
        var legacyRequest = new LLSDAssetUploadRequest();
        LLSDHelpers.DeserialiseOSDMap(legacyRoot, legacyRequest);
        var resources = legacyRequest.asset_resources;
        Assert(resources.mesh_list.Array.Count == 1 &&
            resources.mesh_list.Array[0] is byte[],
            "legacy fee decoder must reproduce raw binary bytes");
        Assert(resources.instance_list.Array.Count == 1 &&
            resources.instance_list.Array[0] is Hashtable,
            "legacy fee decoder must reproduce untyped instance maps");
        Assert(ModelCost.TryGetMeshBinary(resources.mesh_list.Array[0], out byte[] meshBytes) &&
            meshBytes.Length == 3 && meshBytes[0] == 1 && meshBytes[2] == 3,
            "decoded LLSD binary mesh must be accepted");
        Assert(ModelCost.TryParseModelInstance(resources.instance_list.Array[0], 1,
            out scale, out mesh, out error) && mesh == 0 && scale.X == 1.5f,
            "decoded legacy LLSD instance must be accepted: " + error);
        Assert(ModelCost.TryGetMeshBinary(OSD.FromBinary(meshBytes), out byte[] typedBytes) &&
            typedBytes.Length == meshBytes.Length,
            "typed OSD binary mesh must remain accepted");
        Assert(!ModelCost.TryGetMeshBinary("AQID", out _) &&
            !ModelCost.TryGetMeshBinary(Array.Empty<byte>(), out _),
            "nonbinary or empty mesh payload must be rejected");

        var brokenScale = new Hashtable { ["scale"] = new ArrayList { 1.0, 0.0, 1.0 }, ["mesh"] = 0 };
        Assert(!ModelCost.TryParseModelInstance((object)brokenScale, 1,
            out _, out _, out _), "legacy zero scale must be rejected");
        var brokenIndex = new Hashtable { ["scale"] = new ArrayList { 1.0, 1.0, 1.0 }, ["mesh"] = 2 };
        Assert(!ModelCost.TryParseModelInstance((object)brokenIndex, 1,
            out _, out _, out _), "legacy out-of-range mesh index must be rejected");
        var brokenType = new Hashtable { ["scale"] = new ArrayList { 1.0, 1.0, 1.0 }, ["mesh"] = "0" };
        Assert(!ModelCost.TryParseModelInstance((object)brokenType, 1,
            out _, out _, out _), "legacy string mesh index must be rejected");

        VerifyZlibGeometryDecoding();
        Console.WriteLine("OpenGenesisLINK Mesh upload fee LLSD instance + zlib geometry regression: OK");
    }
}

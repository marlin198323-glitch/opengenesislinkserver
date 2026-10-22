// SPDX-License-Identifier: MPL-2.0
// Real LLSD instance structures as sent in mesh upload fee requests.
using System;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using OpenSim.Region.ClientStack.Linden;

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
        Console.WriteLine("OpenGenesisLINK Mesh upload fee LLSD instance regression: OK");
    }
}

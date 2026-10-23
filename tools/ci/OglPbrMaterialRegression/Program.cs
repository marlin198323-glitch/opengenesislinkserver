// SPDX-License-Identifier: MPL-2.0
// Regression: removing a PBR material from a non-existent face is a no-op.
using System;
using System.Reflection;
using OpenMetaverse;
using OpenSim.Region.OptionalModules.Materials;
using static OpenMetaverse.Primitive.RenderMaterials;

internal static class Program
{
    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException("OGL PBR material regression: " + message);
    }

    private static RenderMaterialEntry Entry(byte side)
    {
        return new RenderMaterialEntry { te_index = side, id = UUID.Random() };
    }

    private static bool Remove(ref RenderMaterialEntry[] entries, int side)
    {
        var method = typeof(MaterialsModule).GetMethod("RemoveMaterialEntry",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert(method != null, "material deletion helper missing");
        object[] args = { entries, side };
        bool changed = (bool)method.Invoke(null, args);
        entries = (RenderMaterialEntry[])args[0];
        return changed;
    }

    private static void Main()
    {
        RenderMaterialEntry[] empty = null;
        Assert(!Remove(ref empty, 8) && empty == null,
            "deletion from null array must not mutate");
        empty = Array.Empty<RenderMaterialEntry>();
        Assert(!Remove(ref empty, 8) && empty.Length == 0,
            "deletion from empty array must not mutate");

        RenderMaterialEntry[] materials = { Entry(0), Entry(3), Entry(7) };
        UUID first = materials[0].id, middle = materials[1].id, last = materials[2].id;
        RenderMaterialEntry[] original = materials;
        Assert(!Remove(ref materials, 5),
            "deleting an absent middle face incorrectly succeeded");
        Assert(object.ReferenceEquals(materials, original) && materials.Length == 3,
            "absent middle face changed materials");
        Assert(!Remove(ref materials, 255),
            "deleting an absent face above all entries incorrectly succeeded");
        Assert(object.ReferenceEquals(materials, original),
            "absent high face changed materials");

        Assert(Remove(ref materials, 3) && materials.Length == 2,
            "deleting a present middle face failed");
        Assert(materials[0].id == first && materials[1].id == last,
            "middle face deletion shifted wrong entries");
        Assert(Remove(ref materials, 0) && materials.Length == 1 &&
            materials[0].id == last, "first face removal lost last face");
        Assert(Remove(ref materials, 7) && materials == null,
            "last face removal must clear array");
        Assert(!Remove(ref materials, 7),
            "deleting from cleared array must be harmless");

        Console.WriteLine("OpenGenesisLINK PBR material face deletion regression: OK");
    }
}

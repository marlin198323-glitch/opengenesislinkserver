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

        // Exercise the existing glTF JSON -> Firestorm render-material override
        // conversion path without a simulator or external services.
        var add = typeof(MaterialsModule).GetMethod("AddMaterialOverride",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert(add != null, "glTF material override helper missing");
        string gltf = "{\\"materials\\":[{\\"pbrMetallicRoughness\\":{"
            + "\\"baseColorFactor\\":[1.0,0.5,0.25,1.0],"
            + "\\"metallicFactor\\":0.2,\\"roughnessFactor\\":0.75},"
            + "\\"emissiveFactor\\":[0.1,0.2,0.3],\\"doubleSided\\":true}]}";
        RenderMaterialOverrideEntry[] overrideEntries = null;
        object[] addArgs = { overrideEntries, gltf, 4 };
        Assert((bool)add.Invoke(null, addArgs), "valid glTF PBR override rejected");
        overrideEntries = (RenderMaterialOverrideEntry[])addArgs[0];
        Assert(overrideEntries != null && overrideEntries.Length == 1 &&
            overrideEntries[0].te_index == 4, "glTF PBR face not preserved");
        string serialized = overrideEntries[0].data;
        Assert(!string.IsNullOrWhiteSpace(serialized) &&
            serialized.Contains("bc") && serialized.Contains("mf") &&
            serialized.Contains("rf") && serialized.Contains("ec") &&
            serialized.Contains("ds"), "glTF PBR factors lost during conversion");
        addArgs = new object[] { overrideEntries, gltf, 4 };
        Assert(!(bool)add.Invoke(null, addArgs),
            "identical PBR data incorrectly reported as modified");
        addArgs = new object[] { overrideEntries, "{\\"materials\\":[]}", 4 };
        Assert(!(bool)add.Invoke(null, addArgs),
            "malformed glTF material payload accepted");
        overrideEntries = (RenderMaterialOverrideEntry[])addArgs[0];
        Assert(overrideEntries.Length == 1 && overrideEntries[0].data == serialized,
            "malformed glTF material mutated prior state");

        Console.WriteLine("OpenGenesisLINK PBR face deletion and glTF override regression: OK");
    }
}

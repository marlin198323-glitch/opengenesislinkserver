// SPDX-License-Identifier: MPL-2.0
// Regression: removing a PBR material from a non-existent face is a no-op.
using System;
using System.Reflection;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
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

    private static bool ParseSide(OSD raw, out int side)
    {
        var method = typeof(MaterialsModule).GetMethod("TryParseMaterialSide",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert(method != null, "typed PBR face decoder missing");
        object[] args = { raw, -1 };
        bool accepted = (bool)method.Invoke(null, args);
        side = (int)args[1];
        return accepted;
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
        string gltf = "{\"materials\":[{\"pbrMetallicRoughness\":{"
            + "\"baseColorFactor\":[1.0,0.5,0.25,1.0],"
            + "\"metallicFactor\":0.2,\"roughnessFactor\":0.75},"
            + "\"emissiveFactor\":[0.1,0.2,0.3],\"doubleSided\":true}]}";
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
        addArgs = new object[] { overrideEntries, "{\"materials\":[]}", 4 };
        Assert(!(bool)add.Invoke(null, addArgs),
            "malformed glTF material payload accepted");
        overrideEntries = (RenderMaterialOverrideEntry[])addArgs[0];
        Assert(overrideEntries.Length == 1 && overrideEntries[0].data == serialized,
            "malformed glTF material mutated prior state");

        // LLSD side indices are byte encoded. Negative, overflowing and
        // non-integer values must not alias another user's material face.
        Assert(ParseSide(OSD.FromInteger(0), out int parsedSide) && parsedSide == 0,
            "face zero must be allowed");
        Assert(ParseSide(OSD.FromInteger(255), out parsedSide) && parsedSide == 255,
            "upper byte face must be preserved");
        Assert(!ParseSide(OSD.FromInteger(-1), out parsedSide),
            "negative face wrapped to byte 255");
        Assert(!ParseSide(OSD.FromInteger(256), out parsedSide),
            "out-of-range face wrapped to byte zero");
        Assert(!ParseSide(OSD.FromString("0"), out parsedSide) &&
            !ParseSide(OSD.FromReal(0), out parsedSide),
            "non-integer LLSD face accepted");

        // The glTF converter must reject out-of-bounds texture and source
        // references without changing the previously valid material.
        string imageUri = UUID.Random().ToString();
        string textureGltf = "{\"images\":[{\"uri\":\"" + imageUri
            + "\"}],\"textures\":[{\"source\":0}],"
            + "\"materials\":[{\"pbrMetallicRoughness\":{\"baseColorTexture\":{\"index\":0}}}]}";
        addArgs = new object[] { overrideEntries, textureGltf, 4 };
        Assert((bool)add.Invoke(null, addArgs),
            "valid glTF texture zero reference rejected");
        overrideEntries = (RenderMaterialOverrideEntry[])addArgs[0];
        string validTexture = overrideEntries[0].data;
        Assert(validTexture.Contains("tex"), "glTF texture mapping lost");

        string[] invalidGlTf = {
            textureGltf.Replace("\"index\":0", "\"index\":-1"),
            textureGltf.Replace("\"index\":0", "\"index\":1"),
            textureGltf.Replace("\"source\":0", "\"source\":-1"),
            textureGltf.Replace("\"source\":0", "\"source\":2")
        };
        foreach (string invalid in invalidGlTf)
        {
            addArgs = new object[] { overrideEntries, invalid, 4 };
            Assert(!(bool)add.Invoke(null, addArgs),
                "invalid indexed glTF material unexpectedly accepted");
            overrideEntries = (RenderMaterialOverrideEntry[])addArgs[0];
            Assert(overrideEntries.Length == 1 && overrideEntries[0].data == validTexture,
                "invalid glTF input modified a valid material override");
        }
        addArgs = new object[] { overrideEntries, textureGltf, 256 };
        Assert(!(bool)add.Invoke(null, addArgs),
            "byte-overflowing PBR face accepted by override converter");

        Console.WriteLine("OpenGenesisLINK PBR face bounds and glTF texture validation regression: OK");
    }
}

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

    private static OSDMap DecodeOverride(RenderMaterialOverrideEntry[] entries)
    {
        Assert(entries != null && entries.Length == 1, "unexpected override entries");
        OSDMap result = OSDParser.DeserializeLLSDNotation(entries[0].data) as OSDMap;
        Assert(result != null, "invalid serialized LLSD material override");
        return result;
    }

    private static void AssertUv(OSDArray transforms, int slot,
        double scaleU, double scaleV, double offsetU, double offsetV)
    {
        Assert(transforms.Count > slot && transforms[slot] is OSDMap,
            "missing PBR UV transform for channel " + slot);
        OSDMap channel = (OSDMap)transforms[slot];
        Assert(channel["s"] is OSDArray && channel["o"] is OSDArray,
            "PBR scale or offset missing for channel " + slot);
        OSDArray scales = (OSDArray)channel["s"];
        OSDArray offsets = (OSDArray)channel["o"];
        Assert(scales.Count == 2 && offsets.Count == 2,
            "wrong PBR vector length for channel " + slot);
        Assert(Math.Abs(scales[0].AsReal() - scaleU) < 0.002 &&
               Math.Abs(scales[1].AsReal() - scaleV) < 0.002 &&
               Math.Abs(offsets[0].AsReal() - offsetU) < 0.002 &&
               Math.Abs(offsets[1].AsReal() - offsetV) < 0.002,
            "PBR UV transform changed for channel " + slot);
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

        // Reproduce Firestorm's four-channel glTF KHR_texture_transform
        // contract and verify the exact LLSD 'ti' channel positions.
        const string uvBase = "{\"extensions\":{\"KHR_texture_transform\":{\"scale\":[1.5,1.25],\"offset\":[0.1,-0.1],\"rotation\":0.0}}}";
        const string uvNormal = "{\"extensions\":{\"KHR_texture_transform\":{\"scale\":[2.0,2.25],\"offset\":[0.0,0.2],\"rotation\":0.0}}}";
        const string uvOrm = "{\"extensions\":{\"KHR_texture_transform\":{\"scale\":[3.0,3.25],\"offset\":[0.25,0.0],\"rotation\":0.0}}}";
        const string uvOcclusion = "{\"extensions\":{\"KHR_texture_transform\":{\"scale\":[9.0,9.0],\"offset\":[0.75,0.75],\"rotation\":0.0}}}";
        const string uvEmissive = "{\"extensions\":{\"KHR_texture_transform\":{\"scale\":[4.0,4.25],\"offset\":[0.0,0.25],\"rotation\":0.0}}}";

        UUID metallicAsset = UUID.Random(), occlusionAsset = UUID.Random();
        string allChannels = "{\"images\":[{\"uri\":\"" + metallicAsset +
            "\"},{\"uri\":\"" + occlusionAsset + "\"}],\"textures\":[{\"source\":0},{\"source\":1}]," +
            "\"materials\":[{\"pbrMetallicRoughness\":{\"baseColorTexture\":" + uvBase +
            ",\"metallicRoughnessTexture\":{\"index\":0,\"extensions\":" +
            uvOrm.Substring(14, uvOrm.Length - 15) + "}},\"normalTexture\":" + uvNormal +
            ",\"occlusionTexture\":{\"index\":1,\"extensions\":" +
            uvOcclusion.Substring(14, uvOcclusion.Length - 15) + "},\"emissiveTexture\":" + uvEmissive + "}]}";
        // Build the indexed entries without losing the KHR extension; the
        // metallic and occlusion textures deliberately carry different UVs.
        RenderMaterialOverrideEntry[] uvEntries = null;
        object[] uvArgs = { uvEntries, allChannels, 3 };
        Assert((bool)add.Invoke(null, uvArgs), "four-channel PBR UV update rejected");
        uvEntries = (RenderMaterialOverrideEntry[])uvArgs[0];
        OSDMap decoded = DecodeOverride(uvEntries);
        OSDArray transforms = decoded["ti"] as OSDArray;
        Assert(transforms != null && transforms.Count == 4,
            "four-channel PBR UV array has wrong size");
        AssertUv(transforms, 0, 1.5, 1.25, 0.1, -0.1);
        AssertUv(transforms, 1, 2.0, 2.25, 0.0, 0.2);
        AssertUv(transforms, 2, 3.0, 3.25, 0.25, 0.0);
        AssertUv(transforms, 3, 4.0, 4.25, 0.0, 0.25);
        OSDArray mappedTextures = decoded["tex"] as OSDArray;
        Assert(mappedTextures != null && mappedTextures.Count == 4 &&
               mappedTextures[2].AsUUID() == metallicAsset,
            "occlusion replaced the metallic-roughness (ORM) texture ID");

        // Sparse updates must not shift emissive to ORM or ORM to normal.
        uvArgs = new object[] { null, "{\"materials\":[{\"emissiveTexture\":" + uvEmissive + "}]}", 3 };
        Assert((bool)add.Invoke(null, uvArgs), "emissive-only override rejected");
        OSDArray sparse = DecodeOverride((RenderMaterialOverrideEntry[])uvArgs[0])["ti"] as OSDArray;
        Assert(sparse != null && sparse.Count == 4 && sparse[0] is not OSDMap &&
               sparse[1] is not OSDMap && sparse[2] is not OSDMap,
            "emissive-only UV was assigned to the wrong channel");
        AssertUv(sparse, 3, 4.0, 4.25, 0.0, 0.25);

        uvArgs = new object[] { null, "{\"materials\":[{\"pbrMetallicRoughness\":{\"metallicRoughnessTexture\":" + uvOrm + "}}]}", 3 };
        Assert((bool)add.Invoke(null, uvArgs), "ORM-only override rejected");
        sparse = DecodeOverride((RenderMaterialOverrideEntry[])uvArgs[0])["ti"] as OSDArray;
        Assert(sparse != null && sparse.Count == 3 && sparse[0] is not OSDMap &&
               sparse[1] is not OSDMap, "ORM-only UV shifted to an earlier channel");
        AssertUv(sparse, 2, 3.0, 3.25, 0.25, 0.0);

        // Compatibility fallback: occlusion-only glTF still populates ORM.
        uvArgs = new object[] { null, "{\"materials\":[{\"occlusionTexture\":" + uvOcclusion + "}]}", 3 };
        Assert((bool)add.Invoke(null, uvArgs), "occlusion-only material rejected");
        sparse = DecodeOverride((RenderMaterialOverrideEntry[])uvArgs[0])["ti"] as OSDArray;
        AssertUv(sparse, 2, 9.0, 9.0, 0.75, 0.75);

        // A malformed or nonnumeric KHR transform must never mutate the last
        // successfully committed face, including on a channel-only update.
        string pristine = uvEntries[0].data;
        foreach (string malformed in new[] {
            "{\"materials\":[{\"emissiveTexture\":{\"extensions\":{\"KHR_texture_transform\":{\"scale\":[1.0]}}}}]}",
            "{\"materials\":[{\"pbrMetallicRoughness\":{\"metallicRoughnessTexture\":{\"extensions\":{\"KHR_texture_transform\":{\"offset\":[\"invalid\",0]}}}}}]}",
            "{\"materials\":[{\"emissiveTexture\":{\"extensions\":{\"KHR_texture_transform\":{\"rotation\":\"invalid\"}}}}]}"
        })
        {
            uvArgs = new object[] { uvEntries, malformed, 3 };
            Assert(!(bool)add.Invoke(null, uvArgs), "invalid glTF UV transform accepted");
            uvEntries = (RenderMaterialOverrideEntry[])uvArgs[0];
            Assert(uvEntries[0].data == pristine, "invalid UV mutated existing PBR face");
        }

        Console.WriteLine("OpenGenesisLINK PBR ORM/emissive UV channel roundtrip and invalid-input regression: OK");

        Console.WriteLine("OpenGenesisLINK PBR face bounds and glTF texture validation regression: OK");
    }
}

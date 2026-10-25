// SPDX-License-Identifier: MPL-2.0
// OpenGenesisLINK: validate LLSD mesh LOD blocks before invoking the
// OpenMetaverse renderer decoder, which logs stack traces for corrupt data.
using System;
using System.IO;
using System.IO.Compression;
using OpenMetaverse.StructuredData;

namespace OpenSim.Region.CoreModules.World.Warp3DMap
{
    internal static class WarpMeshAssetPreflight
    {
        // Bounds apply to a single mesh asset and one uncompressed LOD.
        // Corrupt size fields must never allocate unlimited memory.
        private const int MaxAssetBytes = 64 * 1024 * 1024;
        private const int MaxDecodedLodBytes = 32 * 1024 * 1024;
        private static readonly string[] s_lodNames =
            { "lowest_lod", "low_lod", "medium_lod", "high_lod" };

        internal static bool TryValidate(byte[] data, int requestedLod,
            out int selectedLod, out string reason)
        {
            selectedLod = -1;
            reason = "invalid mesh asset header";
            if (data == null || data.Length < 16 || data.Length > MaxAssetBytes)
            {
                reason = "empty, truncated or oversized asset payload";
                return false;
            }

            OSDMap header;
            int payloadStart;
            try
            {
                using MemoryStream input = new MemoryStream(data, false);
                header = OSDParser.DeserializeLLSDBinary(input) as OSDMap;
                payloadStart = (int)input.Position;
            }
            catch (Exception e) when (e is InvalidDataException ||
                e is EndOfStreamException || e is ArgumentException ||
                e is InvalidOperationException || e is IOException)
            {
                reason = "invalid LLSD mesh header (" + e.GetType().Name + ")";
                return false;
            }
            catch (Exception e)
            {
                reason = "unreadable LLSD mesh header (" + e.GetType().Name + ")";
                return false;
            }

            if (header == null || payloadStart <= 0 || payloadStart >= data.Length)
            {
                reason = "missing mesh LLSD header or compressed blocks";
                return false;
            }

            // Prefer the requested LOD. A valid alternate LOD makes the mesh
            // visible even if one of its original LOD blocks is damaged.
            int level = Math.Clamp(requestedLod, 0, 3);
            if (TryValidateBlock(header, data, payloadStart, level))
            {
                selectedLod = level;
                reason = string.Empty;
                return true;
            }
            for (int candidate = 3; candidate >= 0; candidate--)
            {
                if (candidate == level)
                    continue;
                if (TryValidateBlock(header, data, payloadStart, candidate))
                {
                    selectedLod = candidate;
                    reason = "requested LOD unavailable; using " + s_lodNames[candidate];
                    return true;
                }
            }

            reason = "no valid, bounded, zlib-compressed visual LOD block";
            return false;
        }

        private static bool TryValidateBlock(OSDMap header, byte[] data,
            int payloadStart, int lodLevel)
        {
            if (!header.ContainsKey(s_lodNames[lodLevel]) ||
                header[s_lodNames[lodLevel]] is not OSDMap block ||
                !block.ContainsKey("offset") || !block.ContainsKey("size") ||
                block["offset"] is not OSDInteger offsetValue ||
                block["size"] is not OSDInteger sizeValue)
                return false;

            int offset = offsetValue.AsInteger();
            int size = sizeValue.AsInteger();
            int available = data.Length - payloadStart;
            if (offset < 0 || size < 6 || offset > available ||
                size > available - offset)
                return false;

            try
            {
                using MemoryStream source =
                    new MemoryStream(data, payloadStart + offset, size, false);
                using ZLibStream zlib = new ZLibStream(source, CompressionMode.Decompress);
                using MemoryStream decompressed = new MemoryStream();
                byte[] chunk = new byte[8192];
                int len;
                while ((len = zlib.Read(chunk, 0, chunk.Length)) != 0)
                {
                    if (decompressed.Length > MaxDecodedLodBytes - len)
                        return false;
                    decompressed.Write(chunk, 0, len);
                }
                decompressed.Position = 0;
                if (OSDParser.DeserializeLLSDBinary(decompressed) is not OSDArray faces ||
                    faces.Count == 0)
                    return false;

                bool hasGeometry = false;
                foreach (OSD face in faces)
                {
                    if (face is not OSDMap m)
                        return false;
                    if (m.ContainsKey("NoGeometry") && m["NoGeometry"].AsBoolean())
                        continue;
                    if (!m.ContainsKey("Position") || !m.ContainsKey("TriangleList") ||
                        m["Position"] is not OSDBinary positions ||
                        m["TriangleList"] is not OSDBinary triangles)
                        return false;
                    byte[] pos = positions.AsBinary();
                    byte[] tris = triangles.AsBinary();
                    // Each position component and each triangle index is
                    // represented as a 16-bit integer.
                    if (pos.Length < 6 || (pos.Length % 6) != 0 ||
                        tris.Length < 6 || (tris.Length % 6) != 0)
                        return false;
                    int vertexCount = pos.Length / 6;
                    for (int j = 0; j < tris.Length; j += 2)
                    {
                        int index = tris[j] | (tris[j + 1] << 8);
                        if (index >= vertexCount)
                            return false;
                    }
                    hasGeometry = true;
                }
                return hasGeometry;
            }
            catch (Exception)
            {
                // Bad asset bytes are input data, not a simulator exception.
                return false;
            }
        }
    }
}

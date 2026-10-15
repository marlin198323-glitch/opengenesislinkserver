// OpenGenesisLINK HG InventoryGuard - phase 2: bounded inspection of incoming HG asset bytes.
// This is intentionally NOT a malware detector, a script behavior analyzer, or a quarantine store.
using System;
using System.IO;
using System.Text;
using System.Xml;
using OpenMetaverse;
using OpenSim.Framework;

namespace OpenSim.Services.HypergridService
{
    /// <summary>
    /// Rejects structurally invalid and oversized HG asset bodies before HGAssetService.Store().
    /// Object assets use strict, streaming XML parsing. It does not fetch transitive
    /// references in object inventories, run scripts, or prove a caller is authorized.
    /// </summary>
    public static class OglHgInventoryAssetInspector
    {
        public const int DefaultMaxAssetBytes = 32 * 1024 * 1024;
        public const int DefaultMaxScriptBytes = 1024 * 1024;
        public const int MaximumObjectDepth = 64;
        public const int MaximumObjectNodes = 200000;

        public static bool TryInspect(AssetBase asset, int maxAssetBytes,
            int maxScriptBytes, out string reason)
        {
            if (asset == null || asset.FullID == UUID.Zero)
            {
                reason = "missing_asset_or_id";
                return false;
            }

            byte[] data = asset.Data;
            if (data == null || data.Length == 0)
            {
                reason = "empty_asset";
                return false;
            }

            if (maxAssetBytes < 1 || maxAssetBytes > 256 * 1024 * 1024 ||
                maxScriptBytes < 1 || maxScriptBytes > maxAssetBytes)
            {
                reason = "invalid_inspector_limits";
                return false;
            }

            if (data.Length > maxAssetBytes)
            {
                reason = "asset_size_limit";
                return false;
            }

            if (asset.Type == (sbyte)AssetType.LSLBytecode)
            {
                // Never treat compiled foreign executable bytecode as a vetted LSL source.
                reason = "foreign_script_bytecode";
                return false;
            }

            if (asset.Type == (sbyte)AssetType.LSLText)
            {
                if (data.Length > maxScriptBytes)
                {
                    reason = "script_size_limit";
                    return false;
                }

                try
                {
                    // Reject broken UTF-8 and embedded NUL; this is NOT semantic
                    // analysis of LSL/OSSL behavior.
                    string script = new UTF8Encoding(false, true).GetString(data);
                    if (script.IndexOf('\0') >= 0)
                    {
                        reason = "script_contains_nul";
                        return false;
                    }
                }
                catch (DecoderFallbackException)
                {
                    reason = "invalid_script_utf8";
                    return false;
                }
            }

            if (asset.Type == (sbyte)AssetType.Object)
            {
                // HGAssetService already expects object assets to be XML and
                // sanitizes their textual representation when importing them.
                try
                {
                    using (MemoryStream stream = new MemoryStream(data, false))
                    using (XmlReader reader = XmlReader.Create(stream,
                        new XmlReaderSettings
                        {
                            DtdProcessing = DtdProcessing.Prohibit,
                            XmlResolver = null,
                            MaxCharactersInDocument = maxAssetBytes,
                            MaxCharactersFromEntities = 0,
                            IgnoreComments = true,
                            IgnoreProcessingInstructions = true,
                            CloseInput = false
                        }))
                    {
                        int nodes = 0;
                        bool hasElement = false;
                        while (reader.Read())
                        {
                            if (++nodes > MaximumObjectNodes)
                            {
                                reason = "object_xml_node_limit";
                                return false;
                            }

                            if (reader.Depth > MaximumObjectDepth)
                            {
                                reason = "object_xml_depth_limit";
                                return false;
                            }

                            if (reader.NodeType == XmlNodeType.Element)
                                hasElement = true;
                        }

                        if (!hasElement)
                        {
                            reason = "object_xml_without_root";
                            return false;
                        }
                    }
                }
                catch (XmlException)
                {
                    reason = "invalid_or_unsafe_object_xml";
                    return false;
                }
                catch (ArgumentException)
                {
                    reason = "invalid_object_xml";
                    return false;
                }

                // Phase 3d: inspect task inventory references inside prims.
                // The dependency graph is NOT fetched here and the existence
                // or safety of referenced scripts/assets is NOT implied.
                if (!OglHgNestedInventoryInspector.TryExtract(asset, maxAssetBytes,
                    out _, out reason))
                    return false;
            }

            reason = null;
            return true;
        }
    }
}

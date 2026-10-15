// OGL InventoryGuard phase 3d: bounded extraction of nested prim inventory references.
// Not a recursive asset fetcher. Results MUST NEVER be treated as safe-to-release.
using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using OpenMetaverse;
using OpenSim.Framework;

namespace OpenSim.Services.HypergridService
{
    /// <summary>
    /// Inventory pointers encoded inside object XML. A non-empty list is only
    /// a dependency list requiring further validation, never evidence of safety.
    /// </summary>
    public readonly struct OglHgNestedAssetReference
    {
        public UUID ItemId { get; }
        public UUID AssetId { get; }
        public int AssetType { get; }
        public int InventoryType { get; }

        public OglHgNestedAssetReference(UUID itemId, UUID assetId,
            int assetType, int inventoryType)
        {
            ItemId = itemId;
            AssetId = assetId;
            AssetType = assetType;
            InventoryType = inventoryType;
        }
    }

    public static class OglHgNestedInventoryInspector
    {
        public const int MaxNestedItems = 2048;
        private const int MaxXmlNodes = 200000;
        private const int MaxXmlDepth = 64;

        public static bool TryExtract(AssetBase asset, int maxBytes,
            out OglHgNestedAssetReference[] references, out string reason)
        {
            references = Array.Empty<OglHgNestedAssetReference>();
            reason = "invalid_object_asset";
            if (asset == null || asset.FullID == UUID.Zero ||
                asset.Type != (sbyte)AssetType.Object ||
                asset.Data == null || asset.Data.Length == 0 ||
                maxBytes <= 0 || maxBytes > 256 * 1024 * 1024 ||
                asset.Data.Length > maxBytes)
                return false;

            List<OglHgNestedAssetReference> collected = new();
            HashSet<UUID> seenItems = new();
            try
            {
                using MemoryStream data = new MemoryStream(asset.Data, false);
                using XmlReader reader = XmlReader.Create(data, new XmlReaderSettings
                {
                    DtdProcessing = DtdProcessing.Prohibit,
                    XmlResolver = null,
                    MaxCharactersInDocument = maxBytes,
                    MaxCharactersFromEntities = 0,
                    IgnoreComments = true,
                    IgnoreProcessingInstructions = true
                });

                int nodes = 0;
                int taskDepth = -1;
                while (reader.Read())
                {
                    if (++nodes > MaxXmlNodes || reader.Depth > MaxXmlDepth)
                    {
                        reason = "nested_inventory_xml_complexity_limit";
                        return false;
                    }

                    if (reader.NodeType == XmlNodeType.EndElement &&
                        reader.LocalName == "TaskInventory" &&
                        reader.Depth == taskDepth)
                    {
                        taskDepth = -1;
                        continue;
                    }

                    if (reader.NodeType != XmlNodeType.Element)
                        continue;

                    if (reader.LocalName == "TaskInventory")
                    {
                        if (taskDepth >= 0)
                        {
                            reason = "nested_inventory_container";
                            return false;
                        }
                        taskDepth = reader.IsEmptyElement ? -1 : reader.Depth;
                        continue;
                    }

                    if (reader.LocalName != "TaskInventoryItem")
                        continue;

                    if (taskDepth < 0 || reader.Depth != taskDepth + 1 ||
                        reader.IsEmptyElement)
                    {
                        reason = "malformed_nested_inventory_item";
                        return false;
                    }

                    if (collected.Count >= MaxNestedItems)
                    {
                        reason = "nested_inventory_item_limit";
                        return false;
                    }

                    if (!TryReadItem(reader, out OglHgNestedAssetReference entry,
                        out reason))
                        return false;

                    if (!seenItems.Add(entry.ItemId))
                    {
                        reason = "duplicate_nested_inventory_item_id";
                        return false;
                    }
                    collected.Add(entry);
                }

                if (taskDepth >= 0)
                {
                    reason = "unterminated_nested_inventory_container";
                    return false;
                }
            }
            catch (XmlException)
            {
                reason = "invalid_or_unsafe_nested_inventory_xml";
                return false;
            }
            catch (ArgumentException)
            {
                reason = "invalid_nested_inventory_xml";
                return false;
            }
            catch (InvalidOperationException)
            {
                reason = "invalid_nested_inventory_field";
                return false;
            }

            references = collected.ToArray();
            reason = null;
            return true;
        }

        private static bool TryReadItem(XmlReader outer,
            out OglHgNestedAssetReference reference, out string reason)
        {
            reference = default;
            reason = "invalid_nested_inventory_item";
            UUID itemId = UUID.Zero;
            UUID assetId = UUID.Zero;
            int type = int.MinValue;
            int invType = int.MinValue;
            int itemFound = 0, assetFound = 0, typeFound = 0, invTypeFound = 0;
            using XmlReader item = outer.ReadSubtree();
            while (item.Read())
            {
                if (item.Depth != 1 || item.NodeType != XmlNodeType.Element)
                    continue;

                string field = item.LocalName;
                if (field != "ItemID" && field != "AssetID" &&
                    field != "Type" && field != "InvType")
                    continue;

                if (!TryReadShortValue(item, field == "ItemID" || field == "AssetID",
                    out string raw))
                {
                    reason = "malformed_nested_inventory_field";
                    return false;
                }

                if (field == "ItemID")
                {
                    if (++itemFound != 1 || !UUID.TryParse(raw, out itemId) ||
                        itemId == UUID.Zero)
                    {
                        reason = "invalid_nested_inventory_item_id";
                        return false;
                    }
                }
                else if (field == "AssetID")
                {
                    if (++assetFound != 1 || !UUID.TryParse(raw, out assetId) ||
                        assetId == UUID.Zero)
                    {
                        reason = "invalid_nested_inventory_asset_id";
                        return false;
                    }
                }
                else if (field == "Type")
                {
                    if (++typeFound != 1 || !int.TryParse(raw, out type) ||
                        type < sbyte.MinValue || type > sbyte.MaxValue ||
                        type == (int)AssetType.LSLBytecode)
                    {
                        reason = "invalid_or_executable_nested_inventory_type";
                        return false;
                    }
                }
                else if (++invTypeFound != 1 || !int.TryParse(raw, out invType) ||
                         invType < sbyte.MinValue || invType > sbyte.MaxValue)
                {
                    reason = "invalid_nested_inventory_invtype";
                    return false;
                }
            }

            if (itemFound != 1 || assetFound != 1 ||
                typeFound != 1 || invTypeFound != 1)
            {
                reason = "incomplete_nested_inventory_item";
                return false;
            }

            reference = new OglHgNestedAssetReference(itemId, assetId, type, invType);
            reason = null;
            return true;
        }

        private static bool TryReadShortValue(XmlReader reader,
            bool allowGuidWrapper, out string value)
        {
            value = null;
            string text = null;
            using XmlReader subtree = reader.ReadSubtree();
            while (subtree.Read())
            {
                if (subtree.NodeType == XmlNodeType.Element && subtree.Depth > 0)
                {
                    if (!allowGuidWrapper || subtree.Depth != 1 ||
                        (subtree.LocalName != "UUID" && subtree.LocalName != "Guid"))
                        return false;
                }

                if (subtree.NodeType == XmlNodeType.Text ||
                    subtree.NodeType == XmlNodeType.CDATA)
                {
                    // XML may be attacker-controlled; never allocate an
                    // unbounded field value while inspecting remote objects.
                    if (subtree.Value.Length > 128 || text != null)
                        return false;
                    text = subtree.Value.Trim();
                }
            }
            if (string.IsNullOrEmpty(text))
                return false;
            value = text;
            return true;
        }
    }
}

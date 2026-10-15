// OpenGenesisLINK HG InventoryGuard - phase 1: metadata/invariant checks.
// This is NOT an avatar authentication mechanism or an asset-content scanner.
// Copyright (c) OpenGenesisLINK contributors. Distributed under the repository license.
using System;
using OpenMetaverse;
using OpenSim.Framework;

namespace OpenSim.Services.HypergridService
{
    /// <summary>
    /// Pure validation rules for untrusted legacy HG inventory item metadata.
    /// The calling service enforces owner/folder lookup and duplicate-ID protection.
    /// Asset bodies and embedded scripts must be inspected in a later, isolated stage.
    /// </summary>
    public static class OglHgInventoryGuard
    {
        public static bool TryValidateItem(InventoryItemBase item, out string reason)
        {
            if (item == null)
            {
                reason = "null_item";
                return false;
            }

            if (item.ID == UUID.Zero || item.Owner == UUID.Zero || item.Folder == UUID.Zero)
            {
                reason = "missing_item_owner_or_folder";
                return false;
            }

            // Serialized InventoryItemBase types are stored as signed-byte values.
            if (item.AssetType < sbyte.MinValue || item.AssetType > sbyte.MaxValue ||
                item.InvType < sbyte.MinValue || item.InvType > sbyte.MaxValue)
            {
                reason = "invalid_inventory_type";
                return false;
            }

            // Leave ordinary UTF-8 names intact, including accented characters.
            // Disallow control characters which can corrupt logs/protocol records.
            if (string.IsNullOrWhiteSpace(item.Name) || item.Name.Length > 255)
            {
                reason = "invalid_name";
                return false;
            }

            foreach (char c in item.Name)
            {
                if (char.IsControl(c))
                {
                    reason = "control_character_in_name";
                    return false;
                }
            }

            if ((item.Description != null && item.Description.Length > 4096) ||
                (item.CreatorIdentification != null && item.CreatorIdentification.Length > 1024))
            {
                reason = "oversized_item_metadata";
                return false;
            }

            reason = null;
            return true;
        }

        public static bool TryValidateUpdate(
            InventoryItemBase existing, InventoryItemBase incoming, out string reason)
        {
            if (!TryValidateItem(incoming, out reason))
                return false;

            if (existing == null)
            {
                reason = "missing_original";
                return false;
            }

            if (existing.ID != incoming.ID ||
                existing.Owner != incoming.Owner ||
                existing.Folder != incoming.Folder ||
                existing.InvType != incoming.InvType ||
                existing.AssetType != incoming.AssetType ||
                !string.Equals(existing.CreatorIdentification, incoming.CreatorIdentification,
                               StringComparison.Ordinal))
            {
                reason = "immutable_item_fields_changed";
                return false;
            }

            // A legacy UPDATEITEM may otherwise replace an existing item's
            // reference with uninspected remote asset bytes.
            if (existing.AssetID != incoming.AssetID)
            {
                reason = "asset_reference_changed";
                return false;
            }

            reason = null;
            return true;
        }
    }
}

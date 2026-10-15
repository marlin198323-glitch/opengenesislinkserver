// OpenGenesisLINK - deterministic private inventory folder identities.
// These are server-managed ordinary-format inventory folders so older viewers
// can display them. IDs are markers, NOT authorization credentials.
using System;
using System.Security.Cryptography;
using System.Text;
using OpenMetaverse;

namespace OpenSim.Services.InventoryService
{
    public static class OglInventoryQuarantineFolders
    {
        public const string RootName = "Quarantaene";
        public const string PendingName = "In Pruefung";
        public const string RejectedName = "Abgelehnt";

        public static UUID Root(UUID owner) => FolderId(owner, "root");
        public static UUID Pending(UUID owner) => FolderId(owner, "pending");
        public static UUID Rejected(UUID owner) => FolderId(owner, "rejected");

        // Per-transaction status subfolders contain NO executable assets.
        // They are not created from unauthenticated HG inventory requests.
        public static UUID Case(UUID owner, UUID caseId)
        {
            if (owner == UUID.Zero || caseId == UUID.Zero)
                return UUID.Zero;

            return FolderId(owner, "case/" + caseId);
        }

        public static string CaseLabel(UUID caseId)
        {
            return "Vorgang " + caseId.ToString().Substring(0, 8).ToUpperInvariant();
        }

        public static bool IsManaged(UUID owner, UUID id)
        {
            if (owner == UUID.Zero || id == UUID.Zero)
                return false;

            return id == Root(owner) || id == Pending(owner) || id == Rejected(owner);
        }

        private static UUID FolderId(UUID owner, string role)
        {
            if (owner == UUID.Zero)
                return UUID.Zero;

            byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(
                "OpenGenesisLINK/inventory/quarantine/v1/" + owner + "/" + role));
            byte[] bytes = new byte[16];
            Buffer.BlockCopy(hash, 0, bytes, 0, bytes.Length);
            // RFC 4122 variant and version 5-shaped deterministic identifier;
            // SHA-256 is intentionally used for the namespace derivation.
            bytes[7] = (byte)((bytes[7] & 0x0f) | 0x50);
            bytes[8] = (byte)((bytes[8] & 0x3f) | 0x80);
            return UUID.Parse(new Guid(bytes).ToString());
        }
    }
}

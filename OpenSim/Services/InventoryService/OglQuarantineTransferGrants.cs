// OpenGenesisLINK InventoryGuard - local, short-lived, one-time transfer proof.
// These proofs do NOT establish the identity of a legacy remote HG caller.
// A trusted local service must authenticate an avatar and operation BEFORE issuing one.
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OpenMetaverse;

namespace OpenSim.Services.InventoryService
{
    public enum OglQuarantineTransferAction
    {
        Begin = 1,
        Reject = 2,
        Stage = 3
    }

    /// <summary>
    /// Issues strictly local audience-bound HMAC proofs and persistently consumes
    /// them once across coordinator instances on the SAME shared filesystem.
    /// No HTTP endpoint, remote issuance, item acceptance or release is exposed.
    /// </summary>
    public sealed class OglQuarantineTransferGrantAuthority
    {
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);
        private static readonly UnixFileMode DirectoryMode =
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
        private static readonly UnixFileMode FileModePrivate =
            UnixFileMode.UserRead | UnixFileMode.UserWrite;
        private readonly byte[] m_secret;
        private readonly string m_audience;
        private readonly string m_ledgerDirectory;

        public OglQuarantineTransferGrantAuthority(byte[] secret, string audience,
            string ledgerDirectory)
        {
            if (secret == null || secret.Length < 32)
                throw new ArgumentException("A secret with at least 32 random bytes is required");
            if (string.IsNullOrWhiteSpace(audience) || audience.Length > 128)
                throw new ArgumentException("An explicit, nonempty local grid audience is required");
            foreach (char c in audience)
                if (!((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') ||
                      (c >= '0' && c <= '9') || c == '.' || c == '_' || c == '-'))
                    throw new ArgumentException("Audience contains unsupported characters");
            if (!OperatingSystem.IsLinux())
                throw new PlatformNotSupportedException(
                    "Grant replay ledger requires an audited private Linux filesystem");
            if (string.IsNullOrWhiteSpace(ledgerDirectory) ||
                !Path.IsPathFullyQualified(ledgerDirectory))
                throw new ArgumentException("Replay ledger must be an absolute private directory");

            string fullPath = Path.GetFullPath(ledgerDirectory);
            string root = Path.GetPathRoot(fullPath);
            if (fullPath == root || fullPath == "/tmp" ||
                fullPath == "/var" || fullPath == "/var/lib" ||
                fullPath == "/opt" || fullPath == "/home" || fullPath == "/root")
                throw new ArgumentException("Replay ledger requires a dedicated directory");

            Directory.CreateDirectory(fullPath);
            if ((File.GetAttributes(fullPath) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Replay ledger must not be a symlink");
            File.SetUnixFileMode(fullPath, DirectoryMode);
            m_secret = (byte[])secret.Clone();
            m_audience = audience;
            m_ledgerDirectory = fullPath;
        }

        // Only a trusted local authenticator may issue these proofs.
        // Do NOT call with raw principal/owner data from /xinventory.
        public string Issue(UUID verifiedOwner, UUID caseId, UUID itemId, UUID assetId,
            OglQuarantineTransferAction action, TimeSpan lifetime)
        {
            if (verifiedOwner == UUID.Zero || caseId == UUID.Zero ||
                itemId == UUID.Zero || assetId == UUID.Zero ||
                (action != OglQuarantineTransferAction.Begin &&
                 action != OglQuarantineTransferAction.Reject &&
                 action != OglQuarantineTransferAction.Stage) ||
                lifetime < TimeSpan.FromSeconds(1) ||
                lifetime > TimeSpan.FromMinutes(5))
                throw new ArgumentException("Invalid or overlong quarantine grant");

            long issued = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            long expiration = issued + (long)Math.Ceiling(lifetime.TotalSeconds);
            string nonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));

            // Fixed number and format of fields; audience character set excludes
            // separator bytes. Binary data is not exposed in the proof.
            string payload = string.Join("|",
                "OGLQ1", m_audience, ((int)action).ToString(),
                verifiedOwner.ToString(), caseId.ToString(),
                itemId.ToString(), assetId.ToString(),
                issued.ToString(), expiration.ToString(), nonce);
            byte[] payloadBytes = StrictUtf8.GetBytes(payload);
            byte[] signature = HMACSHA256.HashData(m_secret, payloadBytes);
            return Base64Url(payloadBytes) + "." + Base64Url(signature);
        }

        /// <summary>
        /// Validate and atomically spend one proof on disk. Expected identities
        /// MUST come from a trusted authenticated local transfer context.
        /// If the ledger is not writable, fail closed.
        /// </summary>
        public bool TryConsume(string proof, UUID verifiedOwner, UUID caseId,
            UUID itemId, UUID assetId, OglQuarantineTransferAction action)
        {
            if (string.IsNullOrEmpty(proof) || proof.Length > 1024 ||
                verifiedOwner == UUID.Zero || caseId == UUID.Zero ||
                itemId == UUID.Zero || assetId == UUID.Zero)
                return false;

            try
            {
                int dot = proof.IndexOf('.');
                if (dot <= 0 || dot != proof.LastIndexOf('.') ||
                    dot >= proof.Length - 1)
                    return false;

                byte[] payload = DecodeBase64Url(proof.Substring(0, dot));
                byte[] suppliedMac = DecodeBase64Url(proof.Substring(dot + 1));
                if (payload.Length > 512 || suppliedMac.Length != 32)
                    return false;
                byte[] computed = HMACSHA256.HashData(m_secret, payload);
                if (!CryptographicOperations.FixedTimeEquals(computed, suppliedMac))
                    return false;

                string[] fields = StrictUtf8.GetString(payload).Split('|');
                if (fields.Length != 10 || fields[0] != "OGLQ1" ||
                    fields[1] != m_audience || fields[2] != ((int)action).ToString() ||
                    fields[3] != verifiedOwner.ToString() ||
                    fields[4] != caseId.ToString() ||
                    fields[5] != itemId.ToString() ||
                    fields[6] != assetId.ToString() ||
                    fields[9].Length != 32)
                    return false;

                if (!long.TryParse(fields[7], out long issued) ||
                    !long.TryParse(fields[8], out long expires))
                    return false;

                long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                if (issued > now + 5 || issued < now - 300 ||
                    expires < now || expires < issued ||
                    expires - issued > 300)
                    return false;

                // SHA-256 name contains no untrusted path fragments. O_EXCL
                // FileMode.CreateNew provides cross-process replay prevention
                // when all nodes use ONE shared atomic filesystem.
                string identifier = Convert.ToHexString(SHA256.HashData(
                    StrictUtf8.GetBytes(proof)));
                string receipt = Path.Combine(m_ledgerDirectory, identifier + ".spent");
                using FileStream stream = new FileStream(receipt,
                    new FileStreamOptions
                    {
                        Mode = System.IO.FileMode.CreateNew,
                        Access = FileAccess.Write,
                        Share = FileShare.None,
                        UnixCreateMode = FileModePrivate,
                        Options = FileOptions.WriteThrough
                    });
                // Durable, non-executable audit metadata binds the consumed
                // proof to one transfer case. No secret/token or payload bytes
                // are stored here; a truncated crash record remains spent.
                byte[] data = JsonSerializer.SerializeToUtf8Bytes(new
                {
                    status = "proof_spent",
                    consumed_utc = DateTimeOffset.UtcNow.ToString("O"),
                    audience = m_audience,
                    action = action.ToString(),
                    owner_id = verifiedOwner.ToString(),
                    case_id = caseId.ToString(),
                    item_id = itemId.ToString(),
                    asset_id = assetId.ToString(),
                    issued_unix = issued,
                    expires_unix = expires
                });
                stream.Write(data, 0, data.Length);
                stream.Flush(true);
                return true;
            }
            catch (FormatException) { return false; }
            catch (DecoderFallbackException) { return false; }
            catch (OverflowException) { return false; }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
            catch (System.Security.SecurityException) { return false; }
            catch (ArgumentException) { return false; }
        }

        private static string Base64Url(byte[] bytes)
        {
            return Convert.ToBase64String(bytes).TrimEnd('=')
                .Replace('+', '-').Replace('/', '_');
        }

        private static byte[] DecodeBase64Url(string data)
        {
            if (data.Length > 1024)
                throw new FormatException();
            foreach (char c in data)
                if (!((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') ||
                      (c >= '0' && c <= '9') || c == '-' || c == '_'))
                    throw new FormatException();
            string value = data.Replace('-', '+').Replace('_', '/');
            if (value.Length % 4 == 1)
                throw new FormatException();
            return Convert.FromBase64String(value.PadRight(
                ((value.Length + 3) / 4) * 4, '='));
        }
    }

    /// <summary>
    /// Strictly internal adapter between local proof verification and EMPTY
    /// quarantine status folders. It never writes an inventory item or asset.
    /// </summary>
    public sealed class OglQuarantineTransferCoordinator
    {
        private readonly OglQuarantineTransferGrantAuthority m_authority;
        private readonly XInventoryService m_inventory;
        private readonly OglQuarantineTransferStageStore m_stageStore;

        public OglQuarantineTransferCoordinator(
            OglQuarantineTransferGrantAuthority authority, XInventoryService inventory)
            : this(authority, inventory, null)
        {
        }

        public OglQuarantineTransferCoordinator(
            OglQuarantineTransferGrantAuthority authority, XInventoryService inventory,
            OglQuarantineTransferStageStore stageStore)
        {
            m_authority = authority ?? throw new ArgumentNullException(nameof(authority));
            m_inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            m_stageStore = stageStore;
        }

        /// <summary>
        /// Trusted local integration ONLY. Stage untrusted bytes in an isolated
        /// folder with a signed, one-use Stage proof; do NOT persist to FSAssets
        /// or regular inventory. Status is projected only AFTER durable staging.
        /// A status failure leaves evidence isolated for operator reconciliation.
        /// </summary>
        public bool TryStage(string proof, UUID verifiedOwner, UUID caseId,
            UUID itemId, UUID assetId, int assetType, byte[] payload)
        {
            if (m_stageStore == null || payload == null)
                return false;

            if (!m_authority.TryConsume(proof, verifiedOwner, caseId, itemId,
                assetId, OglQuarantineTransferAction.Stage))
                return false;

            return m_stageStore.TryStage(verifiedOwner, caseId, itemId,
                       assetId, assetType, payload, out _) &&
                   m_stageStore.TryVerify(verifiedOwner, caseId, itemId, assetId) &&
                   m_inventory.TryBeginTrustedQuarantineCase(verifiedOwner, caseId);
        }

        public bool TryBegin(string proof, UUID verifiedOwner, UUID caseId,
            UUID itemId, UUID assetId)
        {
            return m_authority.TryConsume(proof, verifiedOwner, caseId, itemId,
                       assetId, OglQuarantineTransferAction.Begin) &&
                   m_inventory.TryBeginTrustedQuarantineCase(verifiedOwner, caseId);
        }

        public bool TryReject(string proof, UUID verifiedOwner, UUID caseId,
            UUID itemId, UUID assetId)
        {
            return m_authority.TryConsume(proof, verifiedOwner, caseId, itemId,
                       assetId, OglQuarantineTransferAction.Reject) &&
                   m_inventory.TryRejectTrustedQuarantineCase(verifiedOwner, caseId);
        }
    }
}

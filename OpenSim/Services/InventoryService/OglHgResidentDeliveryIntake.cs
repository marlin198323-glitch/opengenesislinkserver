// OpenGenesisLINK InventoryGuard phase 3i: trust-gated HG purchase/copy/gift INTAKE.
// This is a local integration seam. It is not an HTTP endpoint, an HG handshake,
// a resident-session verifier or an inventory item release implementation.
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OpenMetaverse;

namespace OpenSim.Services.InventoryService
{
    public enum OglHgDeliveryKind
    {
        Purchase = 1,
        TakeCopy = 2,
        Gift = 3
    }

    public sealed class OglHgResidentDeliveryRequest
    {
        public UUID ResidentId { get; init; }
        public UUID SessionOrDelegationId { get; init; }
        public UUID CaseId { get; init; }
        public UUID ForeignTransferId { get; init; }
        public UUID InventoryItemId { get; init; }
        public UUID AssetId { get; init; }
        public int AssetType { get; init; }
        public OglHgDeliveryKind Kind { get; init; }
        public string SourceGridUri { get; init; }
        // HOME-GRID signed, session/grids/scopes/time-bound delegation.
        // A remote HG sender cannot mint this. Not sufficient without
        // independently verified source, rights and payment evidence.
        public string RoamingConsentTicket { get; init; }
        public string PurchaseReceipt { get; init; }
        public long PurchaseAmount { get; init; }
    }

    /// <summary>
    /// These are TRUST requirements, not implementations. A future OGL
    /// authenticated roaming session connector must implement them and
    /// cryptographically bind resident, source grid, operation, source item,
    /// target item, bytes and (for purchases) settled payment.
    /// An untrusted xinventory caller cannot provide authoritative evidence.
    /// </summary>
    public interface IOglHgVerifiedDeliveryEvidence
    {
        bool HasAuthenticatedResidentConsent(OglHgResidentDeliveryRequest request);
        bool HasAuthenticatedSourceTransfer(OglHgResidentDeliveryRequest request,
            string sha256);
        bool HasValidPermissions(OglHgResidentDeliveryRequest request);
        bool IsPurchaseSettled(OglHgResidentDeliveryRequest request);
    }

    /// <summary>
    /// Caller-private, one-time receipt before isolated staging. This layer
    /// must never accept claims directly from unauthenticated /xinventory.
    /// </summary>
    public sealed class OglHgResidentDeliveryIntake
    {
        private static readonly UnixFileMode PrivateDirectory =
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
        private static readonly UnixFileMode PrivateFile =
            UnixFileMode.UserRead | UnixFileMode.UserWrite;

        private readonly IOglHgVerifiedDeliveryEvidence m_evidence;
        private readonly OglQuarantineTransferGrantAuthority m_grants;
        private readonly OglQuarantineTransferCoordinator m_coordinator;
        private readonly string m_receipts;

        public OglHgResidentDeliveryIntake(IOglHgVerifiedDeliveryEvidence evidence,
            OglQuarantineTransferGrantAuthority grants,
            OglQuarantineTransferCoordinator coordinator, string receiptDirectory)
        {
            m_evidence = evidence ?? throw new ArgumentNullException(nameof(evidence));
            m_grants = grants ?? throw new ArgumentNullException(nameof(grants));
            m_coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
            if (!OperatingSystem.IsLinux())
                throw new PlatformNotSupportedException(
                    "Trusted intake requires private Linux receipt storage");
            if (string.IsNullOrWhiteSpace(receiptDirectory) ||
                !Path.IsPathFullyQualified(receiptDirectory))
                throw new ArgumentException("Use dedicated absolute private receipt storage");
            m_receipts = Path.GetFullPath(receiptDirectory);
            if (m_receipts == Path.GetPathRoot(m_receipts) ||
                m_receipts == "/tmp" || m_receipts == "/var" ||
                m_receipts == "/var/lib" || m_receipts == "/home" ||
                m_receipts == "/root" || m_receipts == "/opt")
                throw new ArgumentException("Receipt path cannot be a shared system folder");
            Directory.CreateDirectory(m_receipts);
            if ((File.GetAttributes(m_receipts) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("No symbolic-link receipt directory");
            File.SetUnixFileMode(m_receipts, PrivateDirectory);
        }

        /// <summary>
        /// Fail-closed purchase / TakeCopy / gift receiver. It saves raw bytes
        /// only in the existing protected StageStore. No foreign sender receives
        /// a bearer token, and no conventional AddItem/StoreAsset is called.
        /// A true return means 'quarantined', NEVER 'delivered'.
        /// </summary>
        public bool TryReceive(OglHgResidentDeliveryRequest request, byte[] rawBytes,
            out string reason)
        {
            reason = "delivery_invalid";
            if (request == null || request.ResidentId == UUID.Zero ||
                request.SessionOrDelegationId == UUID.Zero ||
                request.CaseId == UUID.Zero || request.ForeignTransferId == UUID.Zero ||
                request.InventoryItemId == UUID.Zero || request.AssetId == UUID.Zero ||
                request.AssetType < sbyte.MinValue ||
                request.AssetType > sbyte.MaxValue ||
                rawBytes == null || rawBytes.Length == 0 ||
                rawBytes.Length > OglQuarantineTransferStageStore.DefaultMaxAssetBytes ||
                !ValidGridUri(request.SourceGridUri) ||
                (request.Kind != OglHgDeliveryKind.Purchase &&
                 request.Kind != OglHgDeliveryKind.TakeCopy &&
                 request.Kind != OglHgDeliveryKind.Gift))
                return false;

            // Purchases require a verified receipt even when the negotiated
            // amount is zero; copies and gifts may NOT smuggle paid metadata.
            bool purchase = request.Kind == OglHgDeliveryKind.Purchase;
            if (purchase)
            {
                if (string.IsNullOrWhiteSpace(request.PurchaseReceipt) ||
                    request.PurchaseReceipt.Length > 256 ||
                    request.PurchaseAmount < 0)
                    return false;
            }
            else if (!string.IsNullOrEmpty(request.PurchaseReceipt) ||
                     request.PurchaseAmount != 0)
                return false;

            // A caller may reuse or mutate its byte[] after invocation. Bind
            // verification and staging to the SAME private byte snapshot.
            byte[] isolatedCopy = (byte[])rawBytes.Clone();
            string contentHash = Convert.ToHexString(SHA256.HashData(isolatedCopy));
            try
            {
                // The checks below are independent: a grid key alone does
                // not prove resident consent, sale settlement or transfer rights.
                if (!m_evidence.HasAuthenticatedResidentConsent(request))
                {
                    reason = "delivery_resident_consent_missing";
                    return false;
                }
                if (!m_evidence.HasAuthenticatedSourceTransfer(request, contentHash))
                {
                    reason = "delivery_unverified_foreign_source";
                    return false;
                }
                if (!m_evidence.HasValidPermissions(request))
                {
                    reason = "delivery_permissions_missing";
                    return false;
                }
                if (purchase && !m_evidence.IsPurchaseSettled(request))
                {
                    reason = "delivery_purchase_not_settled";
                    return false;
                }
            }
            catch (Exception exception) when (
                exception is InvalidOperationException ||
                exception is IOException ||
                exception is UnauthorizedAccessException)
            {
                reason = "delivery_evidence_provider_unavailable";
                return false;
            }

            // O_EXCL/CREATE_NEW: two workers cannot accept the same
            // foreign-grid transaction. All nodes must share this atomic
            // ledger; a local-only path does NOT prevent cross-node replay.
            string key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
                request.SourceGridUri + "|" + request.ForeignTransferId)));
            string receiptPath = Path.Combine(m_receipts, key + ".intent");
            try
            {
                using FileStream receipt = new FileStream(receiptPath,
                    new FileStreamOptions
                    {
                        Mode = FileMode.CreateNew,
                        Access = FileAccess.Write,
                        Share = FileShare.None,
                        UnixCreateMode = PrivateFile,
                        Options = FileOptions.WriteThrough
                    });
                byte[] audit = JsonSerializer.SerializeToUtf8Bytes(new
                {
                    status = "intake_attempt_spent",
                    created_utc = DateTimeOffset.UtcNow.ToString("O"),
                    source_grid = request.SourceGridUri,
                    foreign_transfer_id = request.ForeignTransferId.ToString(),
                    resident_id = request.ResidentId.ToString(),
                    case_id = request.CaseId.ToString(),
                    item_id = request.InventoryItemId.ToString(),
                    asset_id = request.AssetId.ToString(),
                    sha256 = contentHash,
                    kind = request.Kind.ToString()
                });
                receipt.Write(audit);
                receipt.Flush(true);
            }
            catch (IOException)
            {
                reason = "delivery_duplicate_or_unavailable_ledger";
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                reason = "delivery_unavailable_ledger";
                return false;
            }

            // Issue a local grant only AFTER independent authentication,
            // payment/rights checks and a durable one-use foreign transfer
            // receipt. Never return or transmit the grant to an HG peer.
            string grant = m_grants.Issue(request.ResidentId, request.CaseId,
                request.InventoryItemId, request.AssetId,
                OglQuarantineTransferAction.Stage, TimeSpan.FromSeconds(45));
            if (!m_coordinator.TryStage(grant, request.ResidentId, request.CaseId,
                    request.InventoryItemId, request.AssetId, request.AssetType, isolatedCopy))
            {
                // Intentionally consumed and NOT retried in another context.
                // Operator reconciliation is required for partial failures.
                reason = "delivery_quarantine_failed_requires_reconciliation";
                return false;
            }

            reason = "delivery_quarantined_not_released";
            return true;
        }

        private static bool ValidGridUri(string gridUri)
        {
            if (string.IsNullOrWhiteSpace(gridUri) || gridUri.Length > 255 ||
                !Uri.TryCreate(gridUri, UriKind.Absolute, out Uri uri) ||
                (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp) ||
                string.IsNullOrWhiteSpace(uri.Host) || !string.IsNullOrEmpty(uri.UserInfo) ||
                uri.AbsolutePath != "/" || !string.IsNullOrEmpty(uri.Query) ||
                !string.IsNullOrEmpty(uri.Fragment))
                return false;

            // Require canonical endpoint identity for replay-key binding.
            return gridUri == uri.GetLeftPart(UriPartial.Authority) ||
                   gridUri == uri.GetLeftPart(UriPartial.Authority) + "/";
        }
    }
}

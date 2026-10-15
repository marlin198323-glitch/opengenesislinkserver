// OpenGenesisLINK HG InventoryGuard phase 3j: short-lived HOME-GRID issued
// roaming-resident consent. No public HG endpoint / login implementation.
// Ticket proves a trusted local issuer attested to an authenticated session,
// NOT that any foreign grid/user/asset/payment is trustworthy.
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using OpenMetaverse;

namespace OpenSim.Services.InventoryService
{
    [Flags]
    public enum OglHgRoamingDeliveryScopes
    {
        None = 0,
        Purchase = 1,
        TakeCopy = 2,
        Gift = 4
    }

    /// <summary>
    /// Must be implemented against the HOME GRID's authenticated login /
    /// persistent session service. Raw HG caller-supplied UUIDs are insufficient.
    /// A failed or unavailable source must DENY issuance.
    /// </summary>
    public interface IOglHgAuthenticatedResidentSessionSource
    {
        bool CanDelegate(UUID residentId, UUID authenticatedSessionId,
            string destinationGridUri);

        // At inbound transfer time, query the home-grid's revocable
        // persisted travel-session state. A previously signed token alone
        // is not sufficient once the delegation is no longer active.
        bool IsDelegationActive(UUID residentId, UUID authenticatedSessionId,
            string destinationGridUri);
    }

    /// <summary>
    /// Home-grid-only signed delegation. A roaming ticket is restricted to
    /// the resident, original authenticated session, destination grid,
    /// permitted delivery actions and a short UTC validity window.
    ///
    /// Tickets may span multiple DIFFERENT transfers within that session;
    /// one-time foreign transfer IDs are independently enforced by the
    /// quarantine intake receipt ledger. Never expose the signing key to
    /// visited grids; do not allow legacy /xinventory to issue tickets.
    /// </summary>
    public sealed class OglHgRoamingConsentAuthority
    {
        private static readonly UTF8Encoding StrictUtf8 = new(false, true);
        private readonly byte[] m_secret;
        private readonly string m_issuer;
        private readonly IOglHgAuthenticatedResidentSessionSource m_sessions;

        public OglHgRoamingConsentAuthority(byte[] secret, string homeGridIssuer,
            IOglHgAuthenticatedResidentSessionSource trustedSessions)
        {
            if (secret == null || secret.Length < 32)
                throw new ArgumentException("At least 32 random signing-key bytes required");
            if (string.IsNullOrWhiteSpace(homeGridIssuer) ||
                homeGridIssuer.Length > 128)
                throw new ArgumentException("Explicit home grid issuer required");
            foreach (char c in homeGridIssuer)
                if (!((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') ||
                      (c >= '0' && c <= '9') || c == '.' || c == '_' || c == '-'))
                    throw new ArgumentException("Invalid home grid issuer");
            m_secret = (byte[])secret.Clone();
            m_issuer = homeGridIssuer;
            m_sessions = trustedSessions ??
                throw new ArgumentNullException(nameof(trustedSessions));
        }

        /// <summary>
        /// Only invoke on the HOME GRID after verified local account login.
        /// A remote system MUST NOT call the issuer directly. A false session
        /// provider or invalid action mask prevents issuance.
        /// </summary>
        public bool TryIssueForAuthenticatedSession(UUID residentId,
            UUID authenticatedSessionId, string destinationGridUri,
            OglHgRoamingDeliveryScopes scopes, TimeSpan lifetime,
            out string ticket)
        {
            ticket = null;
            if (residentId == UUID.Zero || authenticatedSessionId == UUID.Zero ||
                !IsCanonicalGrid(destinationGridUri) || !ValidScopes(scopes) ||
                lifetime < TimeSpan.FromSeconds(1) ||
                lifetime > TimeSpan.FromMinutes(10))
                return false;
            try
            {
                if (!m_sessions.CanDelegate(residentId, authenticatedSessionId,
                    destinationGridUri))
                    return false;
            }
            catch (Exception ex) when (ex is InvalidOperationException ||
                                       ex is IOException ||
                                       ex is UnauthorizedAccessException)
            {
                return false;
            }

            long issued = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            long expiration = issued + (long)Math.Ceiling(lifetime.TotalSeconds);
            string nonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
            string encodedGrid = UrlEncode(StrictUtf8.GetBytes(destinationGridUri));
            string payload = string.Join("|",
                "OGL_ROAM1", m_issuer, residentId.ToString(),
                authenticatedSessionId.ToString(), encodedGrid,
                ((int)scopes).ToString(System.Globalization.CultureInfo.InvariantCulture),
                issued.ToString(System.Globalization.CultureInfo.InvariantCulture),
                expiration.ToString(System.Globalization.CultureInfo.InvariantCulture),
                nonce);
            byte[] bytes = StrictUtf8.GetBytes(payload);
            ticket = UrlEncode(bytes) + "." +
                UrlEncode(HMACSHA256.HashData(m_secret, bytes));
            return true;
        }

        /// <summary>
        /// Verify without trusting request IDs: all expected context comes
        /// from the local, independently source-verified transfer request.
        /// This DOES NOT verify foreign grid attestation, rights or payment.
        /// </summary>
        public bool VerifyForDelivery(OglHgResidentDeliveryRequest request)
        {
            if (request == null || request.ResidentId == UUID.Zero ||
                request.SessionOrDelegationId == UUID.Zero ||
                !IsCanonicalGrid(request.SourceGridUri) ||
                string.IsNullOrEmpty(request.RoamingConsentTicket) ||
                request.RoamingConsentTicket.Length > 1024)
                return false;

            OglHgRoamingDeliveryScopes required = request.Kind switch
            {
                OglHgDeliveryKind.Purchase => OglHgRoamingDeliveryScopes.Purchase,
                OglHgDeliveryKind.TakeCopy => OglHgRoamingDeliveryScopes.TakeCopy,
                OglHgDeliveryKind.Gift => OglHgRoamingDeliveryScopes.Gift,
                _ => OglHgRoamingDeliveryScopes.None
            };
            if (required == OglHgRoamingDeliveryScopes.None)
                return false;

            try
            {
                string signed = request.RoamingConsentTicket;
                int dot = signed.IndexOf('.');
                if (dot < 1 || dot != signed.LastIndexOf('.') ||
                    dot == signed.Length - 1)
                    return false;

                byte[] payload = UrlDecode(signed.Substring(0, dot));
                byte[] received = UrlDecode(signed.Substring(dot + 1));
                if (payload.Length > 512 || received.Length != 32)
                    return false;
                byte[] expected = HMACSHA256.HashData(m_secret, payload);
                if (!CryptographicOperations.FixedTimeEquals(expected, received))
                    return false;

                string[] fields = StrictUtf8.GetString(payload).Split('|');
                if (fields.Length != 9 || fields[0] != "OGL_ROAM1" ||
                    fields[1] != m_issuer ||
                    fields[2] != request.ResidentId.ToString() ||
                    fields[3] != request.SessionOrDelegationId.ToString() ||
                    fields[4] != UrlEncode(StrictUtf8.GetBytes(request.SourceGridUri)) ||
                    fields[8].Length != 32 ||
                    !int.TryParse(fields[5], System.Globalization.NumberStyles.None,
                        System.Globalization.CultureInfo.InvariantCulture, out int mask))
                    return false;

                OglHgRoamingDeliveryScopes scopes = (OglHgRoamingDeliveryScopes)mask;
                if (!ValidScopes(scopes) || (scopes & required) != required)
                    return false;

                if (!long.TryParse(fields[6], System.Globalization.NumberStyles.None,
                        System.Globalization.CultureInfo.InvariantCulture, out long issued) ||
                    !long.TryParse(fields[7], System.Globalization.NumberStyles.None,
                        System.Globalization.CultureInfo.InvariantCulture, out long expires))
                    return false;

                long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                if (!(issued <= now + 5 &&
                      issued >= now - 600 && expires >= now &&
                      expires >= issued && expires - issued <= 600))
                    return false;

                // A valid MAC cannot override a server-side session revoke.
                // This check must use HOME-GRID authoritative session state,
                // never merely a UUID supplied by the foreign HTTP caller.
                try
                {
                    return m_sessions.IsDelegationActive(request.ResidentId,
                        request.SessionOrDelegationId, request.SourceGridUri);
                }
                catch (Exception ex) when (ex is InvalidOperationException ||
                                           ex is IOException ||
                                           ex is UnauthorizedAccessException)
                {
                    return false;
                }
            }
            catch (FormatException) { return false; }
            catch (DecoderFallbackException) { return false; }
            catch (OverflowException) { return false; }
            catch (ArgumentException) { return false; }
        }

        private static bool ValidScopes(OglHgRoamingDeliveryScopes scopes)
        {
            int value = (int)scopes;
            return value > 0 && (value & ~7) == 0;
        }

        // Canonical authority form; schemes HTTP/HTTPS are both supported by
        // legacy HG, but operators should require authenticated transport.
        private static bool IsCanonicalGrid(string gridUri)
        {
            if (string.IsNullOrWhiteSpace(gridUri) || gridUri.Length > 255 ||
                !Uri.TryCreate(gridUri, UriKind.Absolute, out Uri uri) ||
                (uri.Scheme != Uri.UriSchemeHttps &&
                 uri.Scheme != Uri.UriSchemeHttp) ||
                string.IsNullOrEmpty(uri.Host) ||
                !string.IsNullOrEmpty(uri.UserInfo) ||
                uri.AbsolutePath != "/" ||
                !string.IsNullOrEmpty(uri.Query) ||
                !string.IsNullOrEmpty(uri.Fragment))
                return false;
            return gridUri == uri.GetLeftPart(UriPartial.Authority) ||
                gridUri == uri.GetLeftPart(UriPartial.Authority) + "/";
        }

        private static string UrlEncode(byte[] bytes) =>
            Convert.ToBase64String(bytes).TrimEnd('=')
                .Replace('+', '-').Replace('/', '_');

        private static byte[] UrlDecode(string input)
        {
            if (string.IsNullOrEmpty(input) || input.Length > 1024)
                throw new FormatException();
            foreach (char c in input)
                if (!((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') ||
                      (c >= '0' && c <= '9') || c == '-' || c == '_'))
                    throw new FormatException();
            string b64 = input.Replace('-', '+').Replace('_', '/');
            if (b64.Length % 4 == 1)
                throw new FormatException();
            return Convert.FromBase64String(b64.PadRight(
                ((b64.Length + 3) / 4) * 4, '='));
        }
    }

    /// <summary>
    /// Compose a real home-grid roaming consent check with THREE independent
    /// foreign transfer / permissions / settlement validators. A consent
    /// ticket alone must NEVER authorize purchase, TakeCopy or Gift.
    /// </summary>
    public sealed class OglHgRoamingDeliveryEvidence : IOglHgVerifiedDeliveryEvidence
    {
        private readonly OglHgRoamingConsentAuthority m_homeConsent;
        private readonly IOglHgVerifiedDeliveryEvidence m_foreignEvidence;

        public OglHgRoamingDeliveryEvidence(OglHgRoamingConsentAuthority homeConsent,
            IOglHgVerifiedDeliveryEvidence foreignEvidence)
        {
            m_homeConsent = homeConsent ?? throw new ArgumentNullException(nameof(homeConsent));
            m_foreignEvidence = foreignEvidence ??
                throw new ArgumentNullException(nameof(foreignEvidence));
        }

        public bool HasAuthenticatedResidentConsent(OglHgResidentDeliveryRequest request)
            => m_homeConsent.VerifyForDelivery(request);

        public bool HasAuthenticatedSourceTransfer(
            OglHgResidentDeliveryRequest request, string sha256)
            => m_foreignEvidence.HasAuthenticatedSourceTransfer(request, sha256);

        public bool HasValidPermissions(OglHgResidentDeliveryRequest request)
            => m_foreignEvidence.HasValidPermissions(request);

        public bool IsPurchaseSettled(OglHgResidentDeliveryRequest request)
            => m_foreignEvidence.IsPurchaseSettled(request);
    }
}

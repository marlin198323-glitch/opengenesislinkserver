// OpenGenesisLINK InventoryGuard: trusted HOME-GRID lifecycle boundary.
// Not an HTTP handler. Caller must authenticate session ownership and
// destination using the local travel pipeline before invoking BeginTravel.
using System;
using OpenMetaverse;
using OpenSim.Services.Interfaces;

namespace OpenSim.Services.InventoryService
{
    public sealed class OglHgRoamingTravelCoordinator
    {
        private readonly IPresenceService m_presence;
        private readonly OglHgRoamingGrantRepository m_grants;
        private readonly OglHgRoamingConsentAuthority m_authority;
        private readonly Func<UUID, UUID, string, bool> m_verifiedLocalTravel;

        public OglHgRoamingTravelCoordinator(IPresenceService presence,
            OglHgRoamingGrantRepository grants, byte[] signingSecret,
            string issuer, Func<UUID, UUID, string, bool> verifiedLocalTravel)
        {
            m_presence = presence ?? throw new ArgumentNullException(nameof(presence));
            m_grants = grants ?? throw new ArgumentNullException(nameof(grants));
            m_verifiedLocalTravel = verifiedLocalTravel ??
                throw new ArgumentNullException(nameof(verifiedLocalTravel));
            m_authority = new OglHgRoamingConsentAuthority(signingSecret, issuer,
                new OglHgPresenceSessionSource(presence, grants));
        }

        // The trusted home-grid travel pipeline supplies a destination
        // that it independently resolved and authenticated.
        public bool TryBeginAuthenticatedTravel(UUID resident, UUID session,
            string verifiedDestination, OglHgRoamingDeliveryScopes scopes,
            TimeSpan lifetime, out string ticket)
        {
            ticket = null;
            if (resident == UUID.Zero || session == UUID.Zero ||
                lifetime <= TimeSpan.Zero || lifetime > TimeSpan.FromMinutes(10))
                return false;
            UUID grantId = UUID.Zero;
            try
            {
                PresenceInfo presence = m_presence.GetAgent(session);
                if (presence == null ||
                    !UUID.TryParse(presence.UserID, out UUID owner) ||
                    owner != resident ||
                    !m_verifiedLocalTravel(resident, session, verifiedDestination))
                    return false;

                // Local travel verification is a required independent check.
                // Never wire this delegate to a caller-supplied assertion.
                grantId = UUID.Random();
                if (!m_grants.TryCreateTrustedGrant(grantId, resident, session,
                    verifiedDestination, DateTimeOffset.UtcNow.Add(lifetime)))
                    return false;

                if (m_authority.TryIssueForAuthenticatedSession(resident,
                    session, verifiedDestination, scopes, lifetime, out ticket))
                    return true;

                // Never leave a usable grant after ticket issuance fails.
                m_grants.TryRevokeGrant(grantId, resident, session);
                ticket = null;
                return false;
            }
            catch (Exception)
            {
                if (grantId != UUID.Zero)
                    m_grants.TryRevokeGrant(grantId, resident, session);
                ticket = null;
                return false;
            }
        }

        // Home-grid logout integration contract: call BEFORE removing the
        // authenticated presence. An absent/invalid session is denied.
        // A failed DB revoke is an operational failure, not a success.
        public bool TryRevokeOnAuthenticatedLogout(UUID resident, UUID session)
        {
            if (resident == UUID.Zero || session == UUID.Zero)
                return false;
            try
            {
                PresenceInfo current = m_presence.GetAgent(session);
                if (current == null ||
                    !UUID.TryParse(current.UserID, out UUID owner) ||
                    owner != resident)
                    return false;
                return m_grants.TryRevokeSession(resident, session);
            }
            catch (Exception)
            {
                return false;
            }
        }

        // Read-only mode: retain travel but refuse to issue delivery scopes.
        // Caller must not silently downgrade to an unrestricted ticket.
        public bool TryBeginReadOnlyTravel(UUID resident, UUID session,
            string verifiedDestination)
        {
            if (resident == UUID.Zero || session == UUID.Zero ||
                string.IsNullOrWhiteSpace(verifiedDestination))
                return false;
            try
            {
                PresenceInfo presence = m_presence.GetAgent(session);
                return presence != null &&
                    UUID.TryParse(presence.UserID, out UUID owner) &&
                    owner == resident &&
                    m_verifiedLocalTravel(resident, session, verifiedDestination) &&
                    m_grants.TryRevokeSession(resident, session);
            }
            catch (Exception)
            {
                return false;
            }
        }

        // A new destination invalidates prior session grants before any
        // replacement is issued. This prevents tickets for the previous
        // grid from remaining usable after a successful onward teleport.
        // A failed revoke denies the new ticket.
        public bool TryChangeAuthenticatedDestination(UUID resident,
            UUID session, string verifiedDestination,
            OglHgRoamingDeliveryScopes scopes, TimeSpan lifetime,
            out string ticket)
        {
            ticket = null;
            if (resident == UUID.Zero || session == UUID.Zero)
                return false;
            if (!m_grants.TryRevokeSession(resident, session))
                return false;
            return TryBeginAuthenticatedTravel(resident, session,
                verifiedDestination, scopes, lifetime, out ticket);
        }

        // Called by the trusted travel pipeline if a teleport fails or
        // the destination changes. A previously issued ticket becomes
        // unusable after successful session-wide revocation.
        public bool TryAbortTravel(UUID resident, UUID session) =>
            m_grants.TryRevokeSession(resident, session);

        // Called by trusted local logout/teleport-abort path. The caller
        // must treat false as revocation failure and escalate operationally.
        public bool TryEndSession(UUID resident, UUID session) =>
            m_grants.TryRevokeSession(resident, session);

        // Bulk logout caller must supply authenticated session IDs obtained
        // before presence is cleared. Fail closed on any invalid or failed
        // revocation; never infer a session from PresenceInfo (it has no ID).
        public bool TryRevokeSessionsBeforeRegionLogout(
            System.Collections.Generic.IEnumerable<(UUID Resident, UUID Session)> sessions)
        {
            if (sessions == null)
                return false;
            try
            {
                foreach (var entry in sessions)
                    if (!TryRevokeOnAuthenticatedLogout(entry.Resident, entry.Session))
                        return false;
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public bool TryRevokeResident(UUID resident) =>
            m_grants.TryRevokeResident(resident);

        public bool VerifyIncomingConsent(OglHgResidentDeliveryRequest request) =>
            m_authority.VerifyForDelivery(request);
    }
}

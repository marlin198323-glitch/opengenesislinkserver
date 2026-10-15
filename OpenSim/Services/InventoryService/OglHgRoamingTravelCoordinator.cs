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

        public OglHgRoamingTravelCoordinator(IPresenceService presence,
            OglHgRoamingGrantRepository grants, byte[] signingSecret,
            string issuer)
        {
            m_presence = presence ?? throw new ArgumentNullException(nameof(presence));
            m_grants = grants ?? throw new ArgumentNullException(nameof(grants));
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
            try
            {
                PresenceInfo presence = m_presence.GetAgent(session);
                if (presence == null ||
                    !UUID.TryParse(presence.UserID, out UUID owner) ||
                    owner != resident)
                    return false;

                // Grant creation is not an authentication mechanism:
                // this method must be called ONLY by trusted local travel.
                UUID grantId = UUID.Random();
                if (!m_grants.TryCreateTrustedGrant(grantId, resident, session,
                    verifiedDestination, DateTimeOffset.UtcNow.Add(lifetime)))
                    return false;

                if (m_authority.TryIssueForAuthenticatedSession(resident,
                    session, verifiedDestination, scopes, lifetime, out ticket))
                    return true;

                // Never leave a usable grant after ticket issuance fails.
                m_grants.TryRevokeSession(resident, session);
                ticket = null;
                return false;
            }
            catch (Exception)
            {
                m_grants.TryRevokeSession(resident, session);
                ticket = null;
                return false;
            }
        }

        // Called by trusted local logout/teleport-abort path. The caller
        // must treat false as revocation failure and escalate operationally.
        public bool TryEndSession(UUID resident, UUID session) =>
            m_grants.TryRevokeSession(resident, session);

        public bool TryRevokeResident(UUID resident) =>
            m_grants.TryRevokeResident(resident);

        public bool VerifyIncomingConsent(OglHgResidentDeliveryRequest request) =>
            m_authority.VerifyForDelivery(request);
    }
}

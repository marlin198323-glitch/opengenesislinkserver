// OpenGenesisLINK InventoryGuard phase 3k.
// Home-grid session adapter. Not wired to public HG routes.
// The destination binding MUST be supplied by a trusted local
// roaming-state service, not a remote HTTP caller.
using System;
using OpenMetaverse;
using OpenSim.Services.Interfaces;

namespace OpenSim.Services.InventoryService
{
    public sealed class OglHgPresenceSessionSource : IOglHgAuthenticatedResidentSessionSource
    {
        private readonly IPresenceService m_presence;
        private readonly Func<UUID, UUID, string, bool> m_authoritativeTravelGrant;

        public OglHgPresenceSessionSource(IPresenceService presence,
            Func<UUID, UUID, string, bool> authoritativeTravelGrant)
        {
            m_presence = presence ?? throw new ArgumentNullException(nameof(presence));
            m_authoritativeTravelGrant = authoritativeTravelGrant ??
                throw new ArgumentNullException(nameof(authoritativeTravelGrant));
        }

        public bool CanDelegate(UUID residentId, UUID authenticatedSessionId,
            string destinationGridUri) =>
            Check(residentId, authenticatedSessionId, destinationGridUri);

        public bool IsDelegationActive(UUID residentId, UUID authenticatedSessionId,
            string destinationGridUri) =>
            Check(residentId, authenticatedSessionId, destinationGridUri);

        private bool Check(UUID residentId, UUID sessionId, string destination)
        {
            if (residentId == UUID.Zero || sessionId == UUID.Zero ||
                string.IsNullOrWhiteSpace(destination))
                return false;

            try
            {
                // Presence proves only session ownership/existence. It does
                // not establish destination consent or authenticated travel.
                PresenceInfo info = m_presence.GetAgent(sessionId);
                if (info == null || !UUID.TryParse(info.UserID, out UUID owner) ||
                    owner != residentId)
                    return false;

                // Must consult persisted, revocable home-grid travel state.
                // This callback must never default to true.
                return m_authoritativeTravelGrant(residentId, sessionId, destination);
            }
            catch (Exception)
            {
                // Fail closed on backend errors; never trust remote UUIDs.
                return false;
            }
        }
    }
}

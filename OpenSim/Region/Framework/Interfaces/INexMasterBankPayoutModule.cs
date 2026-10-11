// SPDX-License-Identifier: MPL-2.0
using OpenMetaverse;

namespace OpenSim.Region.Framework.Interfaces
{
    /// <summary>
    /// Only the server-side script engine can invoke delegated NV$ Master Bank
    /// payments. Never expose a capability that accepts object/script IDs from
    /// an untrusted viewer or in-world HTTP client.
    /// </summary>
    public interface INexMasterBankPayoutModule
    {
        bool IsAuthorizedBankScript(UUID objectId, UUID scriptItemId, UUID ownerId);
        bool TryBankPayout(UUID objectId, UUID scriptItemId, UUID ownerId,
            UUID residentId, int amount, UUID transactionId, out string reason);
    }
}

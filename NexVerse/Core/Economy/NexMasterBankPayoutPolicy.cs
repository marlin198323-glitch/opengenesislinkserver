// SPDX-License-Identifier: MPL-2.0
using System;
using System.Collections.Generic;

namespace NexVerse.Core.Economy
{
    /// <summary>
    /// Explicit administrator-managed Master Bank delegations.
    /// A wallet UUID alone grants NO script payout rights.
    /// Inventory script IDs must be paired with their exact rezzed object IDs.
    /// </summary>
    public sealed class NexMasterBankPayoutPolicy
    {
        private readonly HashSet<string> m_Bindings;

        public Guid AccountId { get; }
        public Guid OperatorId { get; }
        public long MaxAmount { get; }
        public bool Enabled => AccountId != Guid.Empty &&
            OperatorId != Guid.Empty && MaxAmount > 0 &&
            m_Bindings.Count > 0;

        public NexMasterBankPayoutPolicy(Guid accountId, Guid operatorId,
            long maxAmount, string authorizedBindings)
        {
            if (maxAmount < 0 || maxAmount > int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(maxAmount));
            if (accountId != Guid.Empty && accountId == operatorId)
                throw new ArgumentException(
                    "The Master Bank account must be different from the operator's wallet.");

            AccountId = accountId;
            OperatorId = operatorId;
            MaxAmount = maxAmount;
            m_Bindings = new HashSet<string>(StringComparer.Ordinal);

            foreach (string raw in (authorizedBindings ?? string.Empty).Split(
                         new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string[] pair = raw.Trim().Split('/');
                if (pair.Length != 2 ||
                    !Guid.TryParse(pair[0], out Guid objectId) ||
                    !Guid.TryParse(pair[1], out Guid scriptId) ||
                    objectId == Guid.Empty || scriptId == Guid.Empty)
                    throw new ArgumentException(
                        "Master Bank allowed scripts must use objectUUID/scriptItemUUID pairs.",
                        nameof(authorizedBindings));
                m_Bindings.Add(Key(objectId, scriptId));
            }
        }

        private static string Key(Guid objectId, Guid scriptId) =>
            objectId.ToString("D") + "/" + scriptId.ToString("D");

        public bool IsAuthorized(Guid objectId, Guid scriptId, Guid ownerId)
        {
            return Enabled && objectId != Guid.Empty &&
                scriptId != Guid.Empty && ownerId == OperatorId &&
                m_Bindings.Contains(Key(objectId, scriptId));
        }

        public bool CanPayout(Guid objectId, Guid scriptId, Guid ownerId,
            Guid recipientId, long amount)
        {
            return IsAuthorized(objectId, scriptId, ownerId) &&
                recipientId != Guid.Empty && recipientId != OperatorId &&
                recipientId != AccountId && amount > 0 &&
                amount <= MaxAmount;
        }
    }
}

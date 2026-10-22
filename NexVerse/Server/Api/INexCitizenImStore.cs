// SPDX-License-Identifier: MPL-2.0
using System;
using System.Collections.Generic;

namespace NexVerse.Server.Api
{
    // Unified opt-in API contract. The SQLite and MySQL/MariaDB implementations
    // must enforce consent and per-owner access identically.
    internal interface INexCitizenImStore
    {
        bool IsEnabled(Guid owner);
        void SetEnabled(Guid owner, bool enabled);
        void DeleteHistory(Guid owner);
        bool Append(Guid eventId, Guid sender, Guid receiver,
            string senderName, string message, string source);
        List<NexCitizenImEntry> Read(Guid owner, long after, int limit,
            Guid? peer = null);
        List<object> Conversations(Guid owner, int limit);
    }

    internal sealed class NexCitizenImEntry
    {
        public long seq { get; set; }
        public string event_id { get; set; }
        public string from_agent_id { get; set; }
        public string to_agent_id { get; set; }
        public string peer_id { get; set; }
        public string sender_name { get; set; }
        public string message { get; set; }
        public string source { get; set; }
        public DateTimeOffset accepted_at { get; set; }
    }
}

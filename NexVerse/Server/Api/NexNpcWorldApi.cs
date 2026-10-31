// SPDX-License-Identifier: MPL-2.0
// R617: Source-verified, owner-scoped live NPC directory. The trusted simulator
// must publish complete signed region snapshots; no NPCs are inferred from IMs.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text.Json;
using NexVerse.Core.Security;
using OpenMetaverse;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Services.Interfaces;

namespace NexVerse.Server.Api
{
    internal sealed class NexNpcWorldApi
    {
        private readonly NexApiAuthenticator m_Authentication;
        private readonly NexNpcSnapshotIndex m_Index;

        internal NexNpcWorldApi(NexApiAuthenticator authentication,
            NexNpcSnapshotIndex index)
        {
            m_Authentication = authentication ?? throw new ArgumentNullException(nameof(authentication));
            m_Index = index; // null means no verified simulator feed configured
        }

        internal void Handle(IOSHttpRequest request, IOSHttpResponse response)
        {
            response.AddHeader("Cache-Control", "private, no-store");
            response.AddHeader("X-Content-Type-Options", "nosniff");
            string path = (request.UriPath ?? "").TrimEnd('/');
            bool mine = path == "/api/v1/npcs/mine";
            string prefix = "/api/v1/npcs/";
            bool history = path.StartsWith(prefix, StringComparison.Ordinal) &&
                path.EndsWith("/messages", StringComparison.Ordinal);
            if (!mine && !history)
            {
                Reply(response,HttpStatusCode.NotFound,new { error="not_found" });
                return;
            }
            if (!string.Equals(request.HttpMethod,"GET",StringComparison.OrdinalIgnoreCase))
            {
                response.AddHeader("Allow", "GET");
                Reply(response,HttpStatusCode.MethodNotAllowed,new { error="method_not_allowed" });
                return;
            }
            if (!m_Authentication.TryAuthenticate(request,
                NexScopes.RelationshipsRead, out NexPrincipal principal,
                out UserAccount account, out int status, out string reason))
            {
                response.AddHeader("WWW-Authenticate","Bearer");
                Reply(response,(HttpStatusCode)status,new { error=reason });
                return;
            }
            if (account == null || !account.Active || !account.LocalToGrid ||
                !Guid.TryParse(principal.Subject,out Guid owner) ||
                owner == Guid.Empty || owner != account.PrincipalID.Guid)
            {
                Reply(response,HttpStatusCode.Forbidden,new { error="resident_required" });
                return;
            }
            if (m_Index == null || !m_Index.TryOwned(owner,
                out List<NexNpcOwnerEntry> owned, out long revision))
            {
                Reply(response,HttpStatusCode.ServiceUnavailable,
                    new { error="npc_registry_unavailable",
                          message="No complete fresh verified simulator snapshot is available." });
                return;
            }
            if (mine)
            {
                // An opaque generation-bound cursor avoids silently skipped
                // NPCs if a region changes while the owner pages through it.
                string cursor = request.QueryString?["cursor"] ?? "";
                int offset = 0;
                if (cursor.Length > 0)
                {
                    string[] pieces = cursor.Split('-');
                    if (pieces.Length != 2 ||
                        !long.TryParse(pieces[0],NumberStyles.None,CultureInfo.InvariantCulture,out long at) ||
                        !int.TryParse(pieces[1],NumberStyles.None,CultureInfo.InvariantCulture,out offset) ||
                        offset < 0 || offset > 10000)
                    {
                        Reply(response,HttpStatusCode.BadRequest,new { error="invalid_cursor" });
                        return;
                    }
                    if (at != revision)
                    {
                        Reply(response,HttpStatusCode.Conflict,new { error="npc_snapshot_changed" });
                        return;
                    }
                }
                var items = owned.Skip(offset).Take(500).Select(n => new
                {
                    npc_id=n.npc_id, owner_principal_id=n.owner_principal_id,
                    name=n.name, region_name=n.region_name
                }).ToArray();
                int next = offset + items.Length;
                Reply(response,HttpStatusCode.OK,new
                {
                    npcs=items, next_cursor=next < owned.Count
                        ? revision.ToString(CultureInfo.InvariantCulture)+"-"+next.ToString(CultureInfo.InvariantCulture)
                        : ""
                });
                return;
            }
            string uuid = path.Substring(prefix.Length,path.Length - prefix.Length - "/messages".Length);
            if (!Guid.TryParse(uuid,out Guid npcID) || npcID == Guid.Empty)
            {
                Reply(response,HttpStatusCode.BadRequest,new { error="invalid_npc_id" });
                return;
            }
            if (!owned.Any(n => n.npc_id == npcID))
            {
                Reply(response,HttpStatusCode.NotFound,new { error="npc_not_found" });
                return;
            }
            // An NPC is not the same as a resident peer. Never convert a
            // resident opt-in IM archive into a guessed NPC conversation.
            Reply(response,HttpStatusCode.ServiceUnavailable,new
            {
                error="npc_chat_archive_unavailable",
                message="NPC conversations require an explicitly authorized source and retention policy."
            });
        }

        private static void Reply(IOSHttpResponse response,
            HttpStatusCode code, object payload)
        {
            response.KeepAlive = false;
            response.StatusCode = (int)code;
            response.ContentType = "application/json; charset=utf-8";
            response.AddHeader("Cache-Control","private, no-store");
            response.RawBuffer = JsonSerializer.SerializeToUtf8Bytes(payload);
        }
    }
}

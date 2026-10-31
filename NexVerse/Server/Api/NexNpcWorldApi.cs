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
        private readonly NexNpcChatArchive m_Archive;

        internal NexNpcWorldApi(NexApiAuthenticator authentication,
            NexNpcSnapshotIndex index, NexNpcChatArchive archive = null)
        {
            m_Authentication = authentication ?? throw new ArgumentNullException(nameof(authentication));
            m_Index = index; // null means no verified simulator feed configured
            m_Archive = archive; // null means opt-in archive not configured
        }

        internal void Handle(IOSHttpRequest request, IOSHttpResponse response)
        {
            response.AddHeader("Cache-Control", "private, no-store");
            response.AddHeader("X-Content-Type-Options", "nosniff");
            string path = (request.UriPath ?? "").TrimEnd('/');
            bool mine = path == "/api/v1/npcs/mine";
            bool settings = path == "/api/v1/npcs/history/settings";
            bool erase = path == "/api/v1/npcs/history";
            string prefix = "/api/v1/npcs/";
            bool history = path.StartsWith(prefix, StringComparison.Ordinal) &&
                path.EndsWith("/messages", StringComparison.Ordinal);
            if (!mine && !history && !settings && !erase)
            {
                Reply(response,HttpStatusCode.NotFound,new { error="not_found" });
                return;
            }
            string method=request.HttpMethod?.ToUpperInvariant() ?? "";
            if (mine || history ? method!="GET" :
                settings ? method!="GET"&&method!="POST" :
                method!="DELETE")
            {
                response.AddHeader("Allow",settings?"GET, POST":erase?"DELETE":"GET");
                Reply(response,HttpStatusCode.MethodNotAllowed,new { error="method_not_allowed" });
                return;
            }
            if (!m_Authentication.TryAuthenticate(request,
                method=="GET" ? NexScopes.RelationshipsRead : NexScopes.RelationshipsWrite,
                out NexPrincipal principal,
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
            // Consent withdrawal and erasure MUST work even when every
            // simulator is offline or its owner snapshots have expired.
            if (settings || erase)
            {
                if (m_Archive == null)
                {
                    Reply(response,HttpStatusCode.ServiceUnavailable,new { error="npc_chat_archive_unavailable" });
                    return;
                }
                if (settings && method=="GET")
                {
                    Reply(response,HttpStatusCode.OK,new {
                        enabled=m_Archive.Enabled(owner),
                        retention_days=m_Archive.RetentionDays,
                        scope="only direct owner-NPC instant messages"
                    });
                    return;
                }
                if (erase)
                {
                    m_Archive.Erase(owner);
                    Reply(response,HttpStatusCode.OK,new { ok=true,erased=true });
                    return;
                }
                // Default disabled; enabling requires an explicit owner
                // request. Turning off always deletes recorded messages.
                try
                {
                    using var reader=new System.IO.MemoryStream();
                    byte[] part=new byte[1024];int n;
                    while((n=request.InputStream.Read(part,0,part.Length))>0)
                    {
                        if(reader.Length+n>2048)
                        {
                            Reply(response,HttpStatusCode.RequestEntityTooLarge,new { error="body_too_large" });
                            return;
                        }
                        reader.Write(part,0,n);
                    }
                    using var doc=JsonDocument.Parse(reader.ToArray());
                    if(doc.RootElement.ValueKind!=JsonValueKind.Object ||
                        !doc.RootElement.TryGetProperty("enabled",out JsonElement value) ||
                        (value.ValueKind!=JsonValueKind.True&&value.ValueKind!=JsonValueKind.False))
                    {
                        Reply(response,HttpStatusCode.BadRequest,new { error="invalid_consent" });
                        return;
                    }
                    bool enabled=value.GetBoolean();
                    m_Archive.SetEnabled(owner,enabled);
                    Reply(response,HttpStatusCode.OK,new { ok=true,enabled });
                }
                catch(JsonException)
                {
                    Reply(response,HttpStatusCode.BadRequest,new { error="invalid_json" });
                }
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
            if (m_Archive == null)
            {
                Reply(response,HttpStatusCode.ServiceUnavailable,new
                {
                    error="npc_chat_archive_unavailable",
                    message="Direct owner-NPC IM archive is not configured."
                });
                return;
            }
            if (!m_Archive.Enabled(owner))
            {
                Reply(response,HttpStatusCode.Forbidden,new {
                    error="npc_chat_consent_required"
                });
                return;
            }
            // Never read a citizen IM archive. Only explicitly permitted
            // owner-NPC direct IM events are recorded in the dedicated store.
            Reply(response,HttpStatusCode.OK,new {
                npc_id=npcID,owner_principal_id=owner,
                messages=m_Archive.Read(owner,npcID),next_cursor=""
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

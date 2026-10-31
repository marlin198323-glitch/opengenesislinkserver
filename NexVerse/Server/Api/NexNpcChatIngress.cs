// SPDX-License-Identifier: MPL-2.0
// R619 signed NPC owner-only IM ingestion. The claimed owner is verified
// against the current trusted simulator directory on the server.
using System;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OpenSim.Framework.Servers.HttpServer;

namespace NexVerse.Server.Api
{
    internal sealed class NexNpcChatIngress
    {
        private readonly byte[] m_Key;
        private readonly NexNpcChatArchive m_Archive;
        private readonly NexNpcSnapshotIndex m_Registry;
        internal NexNpcChatIngress(string secret,NexNpcChatArchive archive,NexNpcSnapshotIndex registry)
        {
            if(Encoding.UTF8.GetByteCount(secret??"")<32)
                throw new ArgumentException("A private 32-byte NPC archive signature secret is required");
            m_Key=Encoding.UTF8.GetBytes(secret);
            m_Archive=archive??throw new ArgumentNullException(nameof(archive));
            m_Registry=registry??throw new ArgumentNullException(nameof(registry));
        }
        internal void Handle(IOSHttpRequest request,IOSHttpResponse response)
        {
            if(request.HttpMethod!="POST"||
               (request.UriPath??"").TrimEnd('/')!="/internal/nexnpc/chat/v1")
            { Reply(response,HttpStatusCode.NotFound,"not_found");return; }
            byte[] body;
            try
            {
                using var buffer=new MemoryStream();
                byte[] temp=new byte[2048];
                int size;
                while((size=request.InputStream.Read(temp,0,temp.Length))>0)
                {
                    if(buffer.Length+size>8192)
                    {Reply(response,HttpStatusCode.RequestEntityTooLarge,"too_large");return;}
                    buffer.Write(temp,0,size);
                }
                body=buffer.ToArray();
            }
            catch{Reply(response,HttpStatusCode.BadRequest,"invalid_request");return;}
            string stamp=request.Headers?["X-NexNPC-Time"]??"";
            string given=request.Headers?["X-NexNPC-Signature"]??"";
            long now=DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if(!long.TryParse(stamp,out long seconds)||
                seconds<now-180||seconds>now+180||
                !Verify(stamp,body,given))
            {Reply(response,HttpStatusCode.Unauthorized,"signature_invalid");return;}
            try
            {
                using JsonDocument doc=JsonDocument.Parse(body);
                JsonElement j=doc.RootElement;
                if(j.ValueKind!=JsonValueKind.Object||
                   !Uuid(j,"event_id",out Guid eventID)||
                   !Uuid(j,"npc_id",out Guid npcID)||
                   !Uuid(j,"owner_principal_id",out Guid ownerID)||
                   !j.TryGetProperty("sender",out JsonElement senderField)||
                   senderField.ValueKind!=JsonValueKind.String||
                   !j.TryGetProperty("text",out JsonElement textField)||
                   textField.ValueKind!=JsonValueKind.String)
                {Reply(response,HttpStatusCode.BadRequest,"invalid_event");return;}
                string sender=senderField.GetString();
                string text=textField.GetString();
                if((sender!="owner"&&sender!="npc")||
                   string.IsNullOrWhiteSpace(text)||
                   Encoding.UTF8.GetByteCount(text)>4000)
                {Reply(response,HttpStatusCode.BadRequest,"invalid_message");return;}
                if(!m_Registry.TryOwned(ownerID,out var owned,out _)||
                   !owned.Exists(n=>n.npc_id==npcID))
                {Reply(response,HttpStatusCode.Forbidden,"owner_unverified");return;}
                if(!m_Archive.Enabled(ownerID))
                {Reply(response,HttpStatusCode.Forbidden,"owner_consent_disabled");return;}
                bool saved=m_Archive.Append(eventID,ownerID,npcID,sender,text);
                Reply(response,HttpStatusCode.Accepted,saved?"recorded":"duplicate_or_disabled");
            }
            catch(JsonException){Reply(response,HttpStatusCode.BadRequest,"invalid_json");}
            catch{Reply(response,HttpStatusCode.ServiceUnavailable,"archive_unavailable");}
        }
        private static bool Uuid(JsonElement j,string key,out Guid id)
        {
            id=Guid.Empty;
            return j.TryGetProperty(key,out JsonElement v)&&v.ValueKind==JsonValueKind.String
                &&Guid.TryParse(v.GetString(),out id)&&id!=Guid.Empty;
        }
        private bool Verify(string stamp,byte[] body,string given)
        {
            try
            {
                byte[] supplied=Convert.FromBase64String(given);
                byte[] stampBytes=Encoding.ASCII.GetBytes(stamp+"\n");
                byte[] input=new byte[stampBytes.Length+body.Length];
                Buffer.BlockCopy(stampBytes,0,input,0,stampBytes.Length);
                Buffer.BlockCopy(body,0,input,stampBytes.Length,body.Length);
                using var hmac=new HMACSHA256(m_Key);
                byte[] expected=hmac.ComputeHash(input);
                return expected.Length==supplied.Length&&
                    CryptographicOperations.FixedTimeEquals(expected,supplied);
            }
            catch(FormatException){return false;}
        }
        private static void Reply(IOSHttpResponse response,HttpStatusCode code,string status)
        {
            response.KeepAlive=false;response.StatusCode=(int)code;
            response.ContentType="application/json; charset=utf-8";
            response.AddHeader("Cache-Control","no-store");
            response.RawBuffer=JsonSerializer.SerializeToUtf8Bytes(new { status });
        }
    }
}

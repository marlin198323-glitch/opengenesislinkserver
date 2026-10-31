// SPDX-License-Identifier: MPL-2.0
// R617 private simulator-to-ROBUST snapshot ingress. This is never called
// with a resident browser token or service API key.
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OpenSim.Framework.Servers.HttpServer;

namespace NexVerse.Server.Api
{
    internal sealed class NexNpcSnapshotIngress
    {
        private readonly byte[] m_Secret;
        private readonly NexNpcSnapshotIndex m_Index;
        private const int MaxBodySize = 256 * 1024;
        internal NexNpcSnapshotIngress(string secret, NexNpcSnapshotIndex index)
        {
            if (Encoding.UTF8.GetByteCount(secret ?? "") < 32)
                throw new ArgumentException("NPC snapshot bridge requires a 32+ byte secret");
            m_Secret = Encoding.UTF8.GetBytes(secret);
            m_Index = index ?? throw new ArgumentNullException(nameof(index));
        }

        internal void Handle(IOSHttpRequest request, IOSHttpResponse response)
        {
            if (!string.Equals(request.HttpMethod,"POST",StringComparison.OrdinalIgnoreCase))
            {
                Reply(response,HttpStatusCode.MethodNotAllowed,"method_not_allowed");
                return;
            }
            if ((request.UriPath ?? "").TrimEnd('/') != "/internal/nexnpc/snapshot/v1")
            {
                Reply(response,HttpStatusCode.NotFound,"not_found");
                return;
            }
            byte[] body;
            try
            {
                using var stream = new MemoryStream();
                byte[] block = new byte[4096];
                int count;
                while ((count = request.InputStream.Read(block,0,block.Length)) > 0)
                {
                    if (stream.Length + count > MaxBodySize)
                    {
                        Reply(response,HttpStatusCode.RequestEntityTooLarge,"payload_too_large");
                        return;
                    }
                    stream.Write(block,0,count);
                }
                body = stream.ToArray();
            }
            catch
            {
                Reply(response,HttpStatusCode.BadRequest,"invalid_payload");
                return;
            }

            string stamp = request.Headers?["X-NexNPC-Time"] ?? "";
            string signature = request.Headers?["X-NexNPC-Signature"] ?? "";
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            // Range comparison is overflow-safe even for attacker-supplied
            // Int64.MinValue timestamps.
            if (!long.TryParse(stamp,out long seconds) ||
                seconds < now - 180 || seconds > now + 180 ||
                !ValidHmac(stamp,body,signature))
            {
                Reply(response,HttpStatusCode.Unauthorized,"signature_invalid");
                return;
            }
            try
            {
                using JsonDocument document = JsonDocument.Parse(body);
                JsonElement root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object ||
                    !TryGuid(root,"region_id",out Guid region) ||
                    !root.TryGetProperty("sequence",out JsonElement seq) ||
                    !seq.TryGetInt64(out long sequence) || sequence <= 0 ||
                    !root.TryGetProperty("npcs",out JsonElement npcs) ||
                    npcs.ValueKind != JsonValueKind.Array ||
                    npcs.GetArrayLength() > 1000)
                {
                    Reply(response,HttpStatusCode.BadRequest,"invalid_snapshot");
                    return;
                }
                var entries = new List<NexNpcOwnerEntry>();
                foreach (JsonElement row in npcs.EnumerateArray())
                {
                    if (row.ValueKind != JsonValueKind.Object ||
                        !TryGuid(row,"npc_id",out Guid npcID) ||
                        !TryGuid(row,"owner_principal_id",out Guid owner) ||
                        !TryString(row,"name",120,out string name) ||
                        !TryString(row,"region_name",120,out string label))
                    {
                        Reply(response,HttpStatusCode.BadRequest,"invalid_npc");
                        return;
                    }
                    entries.Add(new NexNpcOwnerEntry
                    {
                        npc_id=npcID,owner_principal_id=owner,name=name,region_name=label
                    });
                }
                if (!m_Index.Replace(region,sequence,entries))
                {
                    Reply(response,HttpStatusCode.Conflict,"snapshot_rejected");
                    return;
                }
                Reply(response,HttpStatusCode.Accepted,"snapshot_accepted");
            }
            catch (JsonException)
            {
                Reply(response,HttpStatusCode.BadRequest,"invalid_json");
            }
            catch
            {
                Reply(response,HttpStatusCode.ServiceUnavailable,"snapshot_unavailable");
            }
        }

        private bool ValidHmac(string stamp,byte[] body,string received)
        {
            try
            {
                byte[] supplied = Convert.FromBase64String(received);
                byte[] head = Encoding.ASCII.GetBytes(stamp+"\n");
                byte[] signed = new byte[head.Length+body.Length];
                Buffer.BlockCopy(head,0,signed,0,head.Length);
                Buffer.BlockCopy(body,0,signed,head.Length,body.Length);
                using var hmac = new HMACSHA256(m_Secret);
                byte[] actual = hmac.ComputeHash(signed);
                return supplied.Length==actual.Length &&
                       CryptographicOperations.FixedTimeEquals(supplied,actual);
            }
            catch(FormatException){return false;}
        }
        private static bool TryGuid(JsonElement row,string key,out Guid result)
        {
            result=Guid.Empty;
            return row.TryGetProperty(key,out JsonElement item) &&
                item.ValueKind == JsonValueKind.String &&
                Guid.TryParse(item.GetString(),out result) &&
                result != Guid.Empty;
        }
        private static bool TryString(JsonElement row,string key,int max,out string value)
        {
            value="";
            if (!row.TryGetProperty(key,out JsonElement item) ||
                item.ValueKind!=JsonValueKind.String)return false;
            value=(item.GetString()??"").Trim();
            return value.Length > 0 && value.Length <= max;
        }
        private static void Reply(IOSHttpResponse response,HttpStatusCode code,string status)
        {
            response.KeepAlive=false;
            response.StatusCode=(int)code;
            response.ContentType="application/json; charset=utf-8";
            response.AddHeader("Cache-Control","no-store");
            response.AddHeader("X-Content-Type-Options","nosniff");
            response.RawBuffer=JsonSerializer.SerializeToUtf8Bytes(new { status });
        }
    }
}

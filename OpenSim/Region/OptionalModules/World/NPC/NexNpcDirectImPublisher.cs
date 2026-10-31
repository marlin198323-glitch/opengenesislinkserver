// SPDX-License-Identifier: MPL-2.0
// R619: optional owner/NPC direct IM publisher. No public chat, no third
// parties, no service bearer and no transcript when World consent is off.
using System;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Region.OptionalModules.World.NPC
{
    internal sealed class NexNpcDirectImPublisher : IDisposable
    {
        private readonly Uri m_Endpoint;
        private readonly byte[] m_Secret;
        private readonly ConcurrentQueue<byte[]> m_Events=new ConcurrentQueue<byte[]>();
        private readonly HttpClient m_Client=new HttpClient {Timeout=TimeSpan.FromSeconds(7)};
        private readonly SemaphoreSlim m_SendGate=new SemaphoreSlim(1,1);
        private volatile bool m_Disposed;
        private int m_Pending;
        internal NexNpcDirectImPublisher(string url,string secret)
        {
            if(!Uri.TryCreate(url,UriKind.Absolute,out Uri endpoint)||
                endpoint.Scheme!=Uri.UriSchemeHttps||
                endpoint.AbsolutePath!="/internal/nexnpc/chat/v1"||
                !string.IsNullOrEmpty(endpoint.Query))
                throw new ArgumentException("NPC chat publishing requires private HTTPS ingress URL");
            if(Encoding.UTF8.GetByteCount(secret??"")<32)
                throw new ArgumentException("NPC chat HMAC secret must be at least 32 bytes");
            m_Endpoint=endpoint;m_Secret=Encoding.UTF8.GetBytes(secret);
        }
        internal void Received(NPCAvatar npc,GridInstantMessage im)
        {
            if(npc==null||im==null||npc.OwnerID.IsZero()||
                im.toAgentID!=npc.AgentId.Guid||
                im.fromAgentID!=npc.OwnerID.Guid||
                im.fromGroup||im.dialog!=(byte)InstantMessageDialog.MessageFromAgent)return;
            Enqueue(npc,"owner",im.message);
        }
        internal void Sent(NPCAvatar npc,IClientAPI sender,GridInstantMessage im)
        {
            if(npc==null||im==null||npc.OwnerID.IsZero()||
                im.fromAgentID!=npc.AgentId.Guid||
                im.toAgentID!=npc.OwnerID.Guid||
                im.fromGroup||im.dialog!=(byte)InstantMessageDialog.MessageFromAgent)return;
            Enqueue(npc,"npc",im.message);
        }
        private void Enqueue(NPCAvatar npc,string sender,string message)
        {
            if(m_Disposed||string.IsNullOrWhiteSpace(message)||
                Encoding.UTF8.GetByteCount(message)>4000)return;
            if(Interlocked.Increment(ref m_Pending)>300)
            {Interlocked.Decrement(ref m_Pending);return;}
            byte[] payload=JsonSerializer.SerializeToUtf8Bytes(new {
                event_id=Guid.NewGuid().ToString("D"),
                owner_principal_id=npc.OwnerID.ToString(),
                npc_id=npc.AgentId.ToString(),
                sender,text=message
            });
            m_Events.Enqueue(payload);
            _=Task.Run(FlushAsync);
        }
        private async Task FlushAsync()
        {
            if(!await m_SendGate.WaitAsync(0).ConfigureAwait(false))return;
            try
            {
                while(!m_Disposed&&m_Events.TryDequeue(out byte[] payload))
                {
                    try
                    {
                        for(int attempt=0;attempt<3&&!m_Disposed;attempt++)
                        {
                            string timestamp=DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
                            byte[] prefix=Encoding.ASCII.GetBytes(timestamp+"\n");
                            byte[] data=new byte[prefix.Length+payload.Length];
                            Buffer.BlockCopy(prefix,0,data,0,prefix.Length);
                            Buffer.BlockCopy(payload,0,data,prefix.Length,payload.Length);
                            using var hmac=new HMACSHA256(m_Secret);
                            string signature=Convert.ToBase64String(hmac.ComputeHash(data));
                            try
                            {
                                using var request=new HttpRequestMessage(HttpMethod.Post,m_Endpoint);
                                request.Content=new ByteArrayContent(payload);
                                request.Content.Headers.ContentType=
                                    new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
                                request.Headers.Add("X-NexNPC-Time",timestamp);
                                request.Headers.Add("X-NexNPC-Signature",signature);
                                using HttpResponseMessage response=
                                    await m_Client.SendAsync(request).ConfigureAwait(false);
                                if(response.IsSuccessStatusCode||
                                   (int)response.StatusCode<500)break;
                            }
                            catch(HttpRequestException){}
                            catch(TaskCanceledException){}
                            if(attempt<2)
                                await Task.Delay(200*(attempt+1)).ConfigureAwait(false);
                        }
                    }
                    finally{Interlocked.Decrement(ref m_Pending);}
                }
            }
            finally
            {
                m_SendGate.Release();
                if(!m_Disposed&&!m_Events.IsEmpty)_=Task.Run(FlushAsync);
            }
        }
        public void Dispose()
        {
            m_Disposed=true;
            while(m_Events.TryDequeue(out _)) Interlocked.Decrement(ref m_Pending);
            // Existing in-flight request retains its own managed lifetime.
        }
    }
}

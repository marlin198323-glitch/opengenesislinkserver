// SPDX-License-Identifier: MPL-2.0
// R617 optional region -> ROBUST NPC directory snapshots. Live NPC metadata
// only. Not an IM/chat archive, and never sends messages or personal tokens.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Region.OptionalModules.World.NPC
{
    internal sealed class NexNpcPublishEntry
    {
        public string npc_id { get; set; }
        public string owner_principal_id { get; set; }
        public string name { get; set; }
        public string region_name { get; set; }
    }

    internal sealed class NexNpcSnapshotPublisher : IDisposable
    {
        private readonly Uri m_Endpoint;
        private readonly byte[] m_Key;
        private readonly Func<Scene,List<NexNpcPublishEntry>> m_List;
        private readonly HashSet<Scene> m_Scenes = new HashSet<Scene>();
        private readonly object m_Guard = new object();
        private readonly System.Timers.Timer m_Timer;
        private readonly HttpClient m_Http;
        private readonly SemaphoreSlim m_Once = new SemaphoreSlim(1,1);
        private long m_Sequence = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 100;
        private bool m_Disposed;

        internal NexNpcSnapshotPublisher(Uri endpoint,string secret,
            Func<Scene,List<NexNpcPublishEntry>> list)
        {
            if (endpoint == null || endpoint.Scheme != Uri.UriSchemeHttps ||
                endpoint.AbsolutePath != "/internal/nexnpc/snapshot/v1" ||
                !string.IsNullOrEmpty(endpoint.Query))
                throw new ArgumentException("NPC ingest must use the HTTPS internal snapshot path");
            if (Encoding.UTF8.GetByteCount(secret ?? "") < 32)
                throw new ArgumentException("NPC ingress secret must have 32 or more UTF-8 bytes");
            m_Endpoint=endpoint;
            m_Key=Encoding.UTF8.GetBytes(secret);
            m_List=list ?? throw new ArgumentNullException(nameof(list));
            m_Http=new HttpClient { Timeout=TimeSpan.FromSeconds(8) };
            m_Timer=new System.Timers.Timer(25000);
            m_Timer.AutoReset=true;
            m_Timer.Elapsed+=(_,__)=>Schedule();
            m_Timer.Start();
        }
        internal void Add(Scene scene)
        {
            lock(m_Guard) if(!m_Disposed) m_Scenes.Add(scene);
            Schedule();
        }
        internal void Remove(Scene scene)
        {
            lock(m_Guard) m_Scenes.Remove(scene);
            // A stopped region is intentionally treated as unavailable on
            // ROBUST after lease expiry, not as a falsely empty directory.
        }
        internal void Schedule()
        {
            lock(m_Guard) if(m_Disposed)return;
            _=Task.Run(PublishAsync);
        }
        private async Task PublishAsync()
        {
            if(!await m_Once.WaitAsync(0).ConfigureAwait(false))return;
            try
            {
                Scene[] scenes;
                lock(m_Guard)scenes=m_Scenes.ToArray();
                foreach(Scene scene in scenes)
                {
                    List<NexNpcPublishEntry> npcs;
                    try{npcs=m_List(scene);}catch{continue;}
                    // Max NPCs per region defaults to 40, capped here at 1000.
                    if(npcs==null || npcs.Count>1000)continue;
                    var body=JsonSerializer.SerializeToUtf8Bytes(new
                    {
                        region_id=scene.RegionInfo.RegionID.ToString(),
                        sequence=Interlocked.Increment(ref m_Sequence),
                        npcs
                    });
                    string time=DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
                    byte[] header=Encoding.ASCII.GetBytes(time+"\n");
                    byte[] signed=new byte[header.Length+body.Length];
                    Buffer.BlockCopy(header,0,signed,0,header.Length);
                    Buffer.BlockCopy(body,0,signed,header.Length,body.Length);
                    using var hmac=new HMACSHA256(m_Key);
                    string sig=Convert.ToBase64String(hmac.ComputeHash(signed));
                    try
                    {
                        using var message=new HttpRequestMessage(HttpMethod.Post,m_Endpoint);
                        message.Content=new ByteArrayContent(body);
                        message.Content.Headers.ContentType=
                            new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
                        message.Headers.Add("X-NexNPC-Time",time);
                        message.Headers.Add("X-NexNPC-Signature",sig);
                        using HttpResponseMessage response=await m_Http.SendAsync(message).ConfigureAwait(false);
                        // A rejected snapshot is not authorization to alter
                        // NPC ownership; the next periodic snapshot retries.
                    }
                    catch (HttpRequestException) {}
                    catch (TaskCanceledException) {}
                }
            }
            finally{m_Once.Release();}
        }
        public void Dispose()
        {
            lock(m_Guard){if(m_Disposed)return;m_Disposed=true;m_Scenes.Clear();}
            m_Timer.Stop();
            m_Timer.Dispose();
            // Do not dispose the client/semaphore while an already running
            // in-flight callback may still own the lease. These bounded
            // process-lifetime resources are cleaned on simulator exit.
        }
    }
}

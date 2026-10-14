// SPDX-License-Identifier: MPL-2.0
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using NexVerse.Core.ControlPlane;
using log4net;
using NexVerse.Core.Messaging;
using NexVerse.Core.Security;
using OpenSim.Framework;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Services.Interfaces;

namespace NexVerse.Server.Api
{
    /// <summary>
    /// Persistent Robust authority. Privileged node policies are pushed via NexBus,
    /// signed by Robust's private ES256 key and verified with public keys. Not a software DRM.
    /// </summary>
    internal sealed class OglFeatureControlApi : IDisposable
    {
        private static readonly ILog m_Log = LogManager.GetLogger(typeof(OglFeatureControlApi));
        private sealed class NodeRule
        {
            public NodeRule() { }
            public string NodeId { get; set; } = "";
            public int Flags { get; set; }
            public long Revision { get; set; }
            public string LicenseKeyHash { get; set; } = "";
        }

        private readonly object m_Sync = new object();
        private readonly Dictionary<string, NodeRule> m_Rules =
            new Dictionary<string, NodeRule>(StringComparer.OrdinalIgnoreCase);
        private readonly string m_Path;
        private readonly string m_PrivateKey;
        private readonly INexEventBus m_Bus;
        private readonly NexNodeRegistry m_Nodes;
        private readonly NexApiAuthenticator m_Auth;
        private readonly IDisposable m_Heartbeat;
        private readonly IDisposable m_Online;
        private readonly IDisposable m_Requested;

        public OglFeatureControlApi(string path, string privateKeyPem, NexApiAuthenticator auth,
            NexNodeRegistry nodes, INexEventBus bus)
        {
            if (string.IsNullOrWhiteSpace(privateKeyPem))
                throw new InvalidOperationException(
                    "[OGL FEATURE CONTROL] Privater ES256-Schlüssel fehlt.");
            using (ECDsa signer = ECDsa.Create())
            {
                signer.ImportFromPem(privateKeyPem);
                if (signer.KeySize != 256)
                    throw new InvalidOperationException("ES256 erfordert P-256.");
            }
            m_Path = Path.GetFullPath(path);
            m_PrivateKey = privateKeyPem;
            m_Auth = auth ?? throw new ArgumentNullException(nameof(auth));
            m_Nodes = nodes ?? throw new ArgumentNullException(nameof(nodes));
            m_Bus = bus ?? throw new ArgumentNullException(nameof(bus));
            Load();
            m_Heartbeat = bus.Subscribe("node.heartbeat", Respond);
            m_Online = bus.Subscribe("node.online", Respond);
            m_Requested = bus.Subscribe("feature.policy.requested", Respond);
        }

        private void Load()
        {
            if (!File.Exists(m_Path)) return;
            NodeRule[] rules = JsonSerializer.Deserialize<NodeRule[]>(File.ReadAllText(m_Path));
            if (rules == null) return;
            lock (m_Sync)
                foreach (NodeRule rule in rules)
                    if (ValidNodeId(rule.NodeId) && (rule.Flags & ~OglArchiveFeatureGate.All) == 0)
                        m_Rules[rule.NodeId] = rule;
        }

        private void SaveLocked()
        {
            string folder = Path.GetDirectoryName(m_Path);
            Directory.CreateDirectory(folder);
            string tempfile = m_Path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(tempfile, JsonSerializer.Serialize(
                    m_Rules.Values.OrderBy(x => x.NodeId).ToArray()));
                File.Move(tempfile, m_Path, true);
            }
            finally { if (File.Exists(tempfile)) File.Delete(tempfile); }
        }

        private static bool ValidNodeId(string id) =>
            !string.IsNullOrWhiteSpace(id) && id.Length <= 128 &&
            id.All(c => char.IsLetterOrDigit(c) || c == '-' || c == '_' || c == '.');

        private void Respond(NexEvent ev)
        {
            if (ev == null || ev.Data == null ||
                !string.Equals(ev.Source, "nexverse.simulator", StringComparison.Ordinal) ||
                Math.Abs((DateTimeOffset.UtcNow - ev.Timestamp).TotalSeconds) > 30 ||
                !ev.Data.TryGetValue("node_id", out string node) || !ValidNodeId(node))
                return;

            // Do not synthesize a policy for unregistered arbitrary NodeIDs.
            if (m_Nodes.Get(node) == null &&
                !string.Equals(ev.Name, "node.online", StringComparison.Ordinal))
                return;
            Publish(node);
        }

        private void Publish(string node)
        {
            NodeRule rule;
            lock (m_Sync) m_Rules.TryGetValue(node, out rule);
            long revision = rule?.Revision ?? 0;
            int flags = rule?.Flags ?? 0; // default deny
            long expires = DateTimeOffset.UtcNow.AddSeconds(120).ToUnixTimeSeconds();
            m_Bus.Publish(new NexEvent("feature.policy.updated", "opengenesislink.world-api",
                new Dictionary<string, string>
                {
                    ["node_id"] = node,
                    ["revision"] = revision.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["expires"] = expires.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["flags"] = flags.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["signature"] = OglArchiveFeatureGate.Sign(m_PrivateKey, node, revision, expires, flags)
                }));
        }

        public bool Permitted(string node, int capability)
        {
            lock (m_Sync)
                return ValidNodeId(node) && m_Rules.TryGetValue(node, out NodeRule policy) &&
                    (policy.Flags & capability) == capability;
        }

        private object ListSnapshot()
        {
            var nodes = m_Nodes.List();
            lock (m_Sync)
            {
                return new
                {
                    mode = "enforced",
                    operations = new[] { "oar_import", "oar_export", "iar_import", "iar_export" },
                    nodes = nodes.Select(node =>
                    {
                        m_Rules.TryGetValue(node.NodeId, out NodeRule rule);
                        int flags = rule?.Flags ?? 0;
                        return new
                        {
                            node_id = node.NodeId,
                            state = node.State,
                            oar_import = (flags & 1) != 0,
                            oar_export = (flags & 2) != 0,
                            iar_import = (flags & 4) != 0,
                            iar_export = (flags & 8) != 0,
                            revision = rule?.Revision ?? 0,
                            assigned = rule != null
                        };
                    }).ToArray()
                };
            }
        }

        public void Handle(IOSHttpRequest request, IOSHttpResponse response)
        {
            string method = request?.HttpMethod ?? "";
            bool write = method.Equals("PATCH", StringComparison.OrdinalIgnoreCase);
            bool read = method.Equals("GET", StringComparison.OrdinalIgnoreCase);
            if (!write && !read)
            {
                Reply(response, HttpStatusCode.MethodNotAllowed, new { error = "method_not_allowed" });
                return;
            }
            if (!m_Auth.TryAuthenticate(request,
                write ? NexScopes.SimulatorsManage : NexScopes.SimulatorsRead,
                out NexPrincipal principal, out UserAccount account,
                out int status, out string error) || account == null || account.UserLevel < 200)
            {
                Reply(response, status >= 400 && status <= 599 ? (HttpStatusCode)status : HttpStatusCode.Forbidden,
                    new { error = "feature_control_forbidden", message = "Grid-Administrator erforderlich." });
                return;
            }

            string path = (request.UriPath ?? "").TrimEnd('/');
            if (read && path.Equals("/api/v1/feature-control/nodes", StringComparison.OrdinalIgnoreCase))
            {
                Reply(response, HttpStatusCode.OK, ListSnapshot());
                return;
            }
            const string prefix = "/api/v1/feature-control/nodes/";
            if (!write || !path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                Reply(response, HttpStatusCode.NotFound, new { error = "not_found" });
                return;
            }
            string nodeId = Uri.UnescapeDataString(path.Substring(prefix.Length));
            if (!ValidNodeId(nodeId) || (m_Nodes.Get(nodeId) == null &&
                !HasPolicy(nodeId)))
            {
                Reply(response, HttpStatusCode.NotFound,
                    new { error = "node_not_found", message = "Unbekannte Simulator-NodeID." });
                return;
            }

            try
            {
                using var buffer = new MemoryStream();
                request.InputStream.CopyTo(buffer);
                if (buffer.Length > 8192) throw new InvalidDataException("Payload zu groß.");
                using JsonDocument document = JsonDocument.Parse(buffer.ToArray());
                JsonElement obj = document.RootElement;
                int flags = 0;
                if (Enabled(obj, "oar_import")) flags |= 1;
                if (Enabled(obj, "oar_export")) flags |= 2;
                if (Enabled(obj, "iar_import")) flags |= 4;
                if (Enabled(obj, "iar_export")) flags |= 8;
                NodeRule previous;
                lock (m_Sync)
                {
                    m_Rules.TryGetValue(nodeId, out previous);
                    string keyHash = previous?.LicenseKeyHash ?? "";
                    if (obj.TryGetProperty("license_key", out JsonElement rawKey) &&
                        rawKey.ValueKind == JsonValueKind.String)
                    {
                        string license = rawKey.GetString();
                        if (license.Length < 16 || license.Length > 128)
                            throw new InvalidDataException("Lizenzkennung: 16 bis 128 Zeichen.");
                        keyHash = Convert.ToHexString(
                            SHA256.HashData(Encoding.UTF8.GetBytes(license)));
                    }
                    NodeRule rule = new NodeRule
                    {
                        NodeId = nodeId, Flags = flags,
                        Revision = Math.Max(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                            (previous?.Revision ?? 0) + 1),
                        LicenseKeyHash = keyHash
                    };
                    m_Rules[nodeId] = rule;
                    try { SaveLocked(); }
                    catch
                    {
                        if (previous == null) m_Rules.Remove(nodeId);
                        else m_Rules[nodeId] = previous;
                        throw;
                    }
                }
                m_Log.WarnFormat(
                    "[OGL LICENSE AUDIT]: Administrator {0} (Level {1}) hat Node {2} auf Archivflags {3} gesetzt.",
                    account.PrincipalID, account.UserLevel, nodeId, flags);
                Publish(nodeId);
                Reply(response, HttpStatusCode.OK,
                    new { node_id = nodeId, flags, updated = true });
            }
            catch (Exception)
            {
                Reply(response, HttpStatusCode.BadRequest,
                    new { error = "invalid_policy", message = "Ungültige Freigabe oder Speicherfehler." });
            }
        }

        private bool HasPolicy(string node)
        {
            lock (m_Sync) return m_Rules.ContainsKey(node);
        }

        private static bool Enabled(JsonElement doc, string key)
        {
            if (!doc.TryGetProperty(key, out JsonElement value) ||
                (value.ValueKind != JsonValueKind.True && value.ValueKind != JsonValueKind.False))
                throw new InvalidDataException("Fehlendes boolesches Feld: " + key);
            return value.ValueKind == JsonValueKind.True;
        }

        private static void Reply(IOSHttpResponse response, HttpStatusCode status, object payload)
        {
            response.StatusCode = (int)status;
            response.ContentType = "application/json; charset=utf-8";
            response.RawBuffer = JsonSerializer.SerializeToUtf8Bytes(payload);
        }

        public void Dispose()
        {
            m_Heartbeat.Dispose();
            m_Online.Dispose();
            m_Requested.Dispose();
        }
    }
}

// SPDX-License-Identifier: MPL-2.0
// Separate OGL Viewer JSON adapter for the existing OpenSim login service.
// Never creates a parallel account, password or CAPS/session store.
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text.Json;
using Nini.Config;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Services.Interfaces;

namespace OpenSim.Server.Handlers.Login
{
    public sealed class OglViewerLoginHandler
    {
        public const string Route = "/ogl/v1/login";
        private const int MaxBodyBytes = 8192;

        private readonly ILoginService m_Login;
        private readonly bool m_Enabled;
        private readonly IPAddress m_TrustedTlsProxy;
        private readonly int m_AttemptsPerMinute;
        private readonly object m_ThrottleLock = new object();
        private readonly Dictionary<string, Bucket> m_Throttle = new Dictionary<string, Bucket>();

        private sealed class Bucket
        {
            internal long Window;
            internal int Attempts;
        }

        public OglViewerLoginHandler(ILoginService login, IConfigSource config)
        {
            m_Login = login ?? throw new ArgumentNullException(nameof(login));
            IConfig settings = config.Configs["OglViewerLogin"];
            m_Enabled = settings != null && settings.GetBoolean("Enabled", false);
            m_AttemptsPerMinute = Math.Clamp(settings?.GetInt("MaxAttemptsPerMinute", 5) ?? 5, 1, 60);

            string proxyIp = settings?.GetString("TrustedTlsProxyAddress", "")?.Trim();
            if (!string.IsNullOrWhiteSpace(proxyIp))
            {
                if (!IPAddress.TryParse(proxyIp, out IPAddress parsed))
                    throw new InvalidOperationException("[OglViewerLogin] TrustedTlsProxyAddress must be a literal IP address.");
                m_TrustedTlsProxy = parsed;
            }
        }

        public void Handle(IOSHttpRequest request, IOSHttpResponse response)
        {
            response.KeepAlive = false;
            response.ContentType = "application/json; charset=utf-8";
            response.AddHeader("Cache-Control", "no-store");
            response.AddHeader("Pragma", "no-cache");
            response.AddHeader("X-Content-Type-Options", "nosniff");

            if (!string.Equals(request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase))
            {
                response.AddHeader("Allow", "POST");
                Error(response, 405, "method_not_allowed");
                return;
            }
            if (!m_Enabled)
            {
                Error(response, 404, "not_available");
                return;
            }

            // Only trust X-Forwarded-Proto from one explicitly configured
            // reverse-proxy IP; unauthenticated header claims mean nothing.
            bool trustedProxy = m_TrustedTlsProxy != null &&
                request.RemoteIPEndPoint != null &&
                request.RemoteIPEndPoint.Address.Equals(m_TrustedTlsProxy);
            bool encrypted = request.IsSecured ||
                (trustedProxy && string.Equals(request.Headers["X-Forwarded-Proto"], "https", StringComparison.OrdinalIgnoreCase));

            if (!encrypted)
            {
                Error(response, 426, "tls_required");
                return;
            }
            if (!string.Equals(request.ContentType?.Split(';')[0].Trim(), "application/json", StringComparison.OrdinalIgnoreCase))
            {
                Error(response, 415, "unsupported_media_type");
                return;
            }
            if (request.ContentLength64 > MaxBodyBytes || request.ContentLength64 < -1 ||
                request.InputStream == null || !Throttle(request.RemoteIPEndPoint))
            {
                Error(response, request.ContentLength64 > MaxBodyBytes ? 413 : 429,
                    request.ContentLength64 > MaxBodyBytes ? "request_too_large" : "rate_limited");
                return;
            }

            try
            {
                byte[] body = ReadLimited(request.InputStream);
                if (body == null)
                {
                    Error(response, 413, "request_too_large");
                    return;
                }

                using JsonDocument document = JsonDocument.Parse(body, new JsonDocumentOptions { MaxDepth = 6 });
                JsonElement root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object ||
                    !root.TryGetProperty("protocol_version", out JsonElement proto) ||
                    proto.ValueKind != JsonValueKind.Number || !proto.TryGetInt32(out int protocol) || protocol != 1)
                {
                    Error(response, 400, "unsupported_protocol");
                    return;
                }

                string first = GetString(root, "first", 128);
                string last = GetString(root, "last", 128);
                string password = GetString(root, "passwd", 1024);
                if (string.IsNullOrWhiteSpace(first) ||
                    string.IsNullOrWhiteSpace(last) || string.IsNullOrEmpty(password))
                {
                    Error(response, 400, "invalid_request");
                    return;
                }

                UUID scope = UUID.Zero;
                string scopeStr = GetString(root, "scope_id", 40);
                if (!string.IsNullOrEmpty(scopeStr) && !UUID.TryParse(scopeStr, out scope))
                {
                    Error(response, 400, "invalid_request");
                    return;
                }

                // Reuse the SAME service as Firestorm/LLSD so the same
                // identity, bans, simulator placement, seed CAPS and
                // inventory are generated. No second authentication path.
                LoginResponse login = m_Login.Login(
                    first, last, password,
                    GetString(root, "start", 512) ?? "last",
                    scope,
                    GetString(root, "version", 128) ?? "OGL Viewer v1",
                    GetString(root, "channel", 128) ?? "OpenGenesisLINK Viewer",
                    GetString(root, "mac", 128) ?? "Unknown",
                    GetString(root, "id0", 128) ?? "Unknown",
                    request.RemoteIPEndPoint);

                if (login == null || login is FailedLoginResponse)
                {
                    // Do not leak account enumeration or authentication details.
                    Error(response, 401, "login_failed");
                    return;
                }

                OSDMap payload = new OSDMap
                {
                    ["protocol_version"] = OSD.FromInteger(1),
                    ["login_response"] = login.ToOSDMap()
                };
                response.StatusCode = 200;
                response.RawBuffer = OSDParser.SerializeJsonToBytes(payload);
            }
            catch (JsonException)
            {
                Error(response, 400, "invalid_json");
            }
            catch (InvalidDataException)
            {
                Error(response, 400, "invalid_request");
            }
            catch (Exception)
            {
                // Never log request JSON, password, tokens or session data.
                Error(response, 503, "login_unavailable");
            }
        }

        private static string GetString(JsonElement root, string name, int maxLength)
        {
            if (!root.TryGetProperty(name, out JsonElement value))
                return null;
            if (value.ValueKind != JsonValueKind.String)
                throw new InvalidDataException("Invalid JSON field type.");
            string text = value.GetString();
            if (text != null && text.Length > maxLength)
                throw new InvalidDataException("JSON field too long.");
            return text;
        }

        private static byte[] ReadLimited(Stream input)
        {
            using MemoryStream output = new MemoryStream();
            byte[] buffer = new byte[2048];
            int read;
            while ((read = input.Read(buffer, 0, Math.Min(buffer.Length, MaxBodyBytes + 1 - (int)output.Length))) > 0)
            {
                output.Write(buffer, 0, read);
                if (output.Length > MaxBodyBytes)
                    return null;
            }
            return output.ToArray();
        }

        private bool Throttle(IPEndPoint endpoint)
        {
            if (endpoint == null)
                return false;

            string key = endpoint.Address.ToString();
            long minute = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 60;
            lock (m_ThrottleLock)
            {
                if (m_Throttle.Count >= 2048)
                {
                    List<string> expired = new List<string>();
                    foreach (KeyValuePair<string, Bucket> row in m_Throttle)
                        if (row.Value.Window != minute)
                            expired.Add(row.Key);
                    foreach (string expiredKey in expired)
                        m_Throttle.Remove(expiredKey);
                    if (m_Throttle.Count >= 2048 && !m_Throttle.ContainsKey(key))
                        return false; // fail closed rather than clear rate limits
                }
                if (!m_Throttle.TryGetValue(key, out Bucket bucket) || bucket.Window != minute)
                {
                    bucket = new Bucket { Window = minute };
                    m_Throttle[key] = bucket;
                }
                return ++bucket.Attempts <= m_AttemptsPerMinute;
            }
        }

        private static void Error(IOSHttpResponse response, int status, string code)
        {
            response.StatusCode = status;
            response.RawBuffer = JsonSerializer.SerializeToUtf8Bytes(new
            {
                protocol_version = 1,
                error = code,
                message = "The requested OGL viewer login cannot be completed."
            });
        }
    }
}

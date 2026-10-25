// SPDX-License-Identifier: MPL-2.0
// Privileged server-to-server password recovery. Never accept a browser
// token as authority to mutate a World account without a World challenge.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Security.Cryptography;
using NexVerse.Core.Security;
using OpenMetaverse;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Services.Interfaces;
using log4net;

namespace NexVerse.Server.Api
{
    internal sealed class NexPasswordRecoveryApi
    {
        private const int MaxBodyBytes = 4096;
        private static readonly ILog s_Log = LogManager.GetLogger(typeof(NexPasswordRecoveryApi));
        private readonly NexApiAuthenticator m_Authenticator;
        private readonly IUserAccountService m_Accounts;
        private readonly INexUserService m_Users;
        private readonly INexOAuthStore m_OAuth;
        private readonly NexPasswordRecoveryStore m_Store;
        private readonly bool m_Enabled;
        private readonly HashSet<string> m_TrustedProxies;
        private readonly HashSet<string> m_TrustedHttpPeers;
        private readonly object m_ThrottleSync = new();
        private readonly Dictionary<string, (long Minute, int Count)> m_Throttle = new();

        internal NexPasswordRecoveryApi(NexApiAuthenticator authenticator,
            IUserAccountService accounts, INexUserService users,
            INexOAuthStore oauth, NexPasswordRecoveryStore store, bool enabled,
            string trustedProxyIps, string trustedHttpPeers)
        {
            m_Authenticator = authenticator ?? throw new ArgumentNullException(nameof(authenticator));
            m_Accounts = accounts ?? throw new ArgumentNullException(nameof(accounts));
            m_Users = users ?? throw new ArgumentNullException(nameof(users));
            m_OAuth = oauth;
            m_Store = store;
            m_Enabled = enabled;
            m_TrustedProxies = ParseIps(trustedProxyIps);
            m_TrustedHttpPeers = ParseIps(trustedHttpPeers);
        }

        private static HashSet<string> ParseIps(string csv)
        {
            HashSet<string> ips = new(StringComparer.OrdinalIgnoreCase);
            foreach (string part in (csv ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                if (!IPAddress.TryParse(part.Trim(), out IPAddress parsed))
                    throw new ArgumentException("Trusted password-recovery peers must be literal IP addresses.");
                ips.Add(parsed.IsIPv4MappedToIPv6 ? parsed.MapToIPv4().ToString() : parsed.ToString());
            }
            return ips;
        }

        private static string Peer(IOSHttpRequest request)
        {
            IPAddress ip = request?.RemoteIPEndPoint?.Address;
            if (ip == null) return null;
            return ip.IsIPv4MappedToIPv6 ? ip.MapToIPv4().ToString() : ip.ToString();
        }

        private bool HasSecureTransport(IOSHttpRequest request)
        {
            if (request.IsSecured) return true;
            string peer = Peer(request);
            if (peer == null) return false;
            if (m_TrustedHttpPeers.Contains(peer)) return true;
            IPAddress ip = request.RemoteIPEndPoint.Address;
            if (!(IPAddress.IsLoopback(ip) || m_TrustedProxies.Contains(peer)))
                return false;
            string proto = request.Headers?["X-Forwarded-Proto"];
            return !string.IsNullOrWhiteSpace(proto) &&
                proto.Split(',').All(x => string.Equals(x.Trim(), "https", StringComparison.OrdinalIgnoreCase));
        }

        private bool RateAllowed(string peer, long now)
        {
            if (string.IsNullOrEmpty(peer)) return false;
            long minute = now / 60;
            lock (m_ThrottleSync)
            {
                if (m_Throttle.Count > 1000)
                {
                    foreach (string key in m_Throttle.Where(x => x.Value.Minute < minute)
                                 .Select(x => x.Key).ToArray())
                        m_Throttle.Remove(key);
                    if (m_Throttle.Count > 1000 && !m_Throttle.ContainsKey(peer))
                        return false;
                }
                if (!m_Throttle.TryGetValue(peer, out var counter) || counter.Minute != minute)
                    counter = (minute, 0);
                counter.Count++;
                m_Throttle[peer] = counter;
                return counter.Count <= 30;
            }
        }

        private static void Reply(IOSHttpResponse response, HttpStatusCode code,
            string status, string correlation)
        {
            response.StatusCode = (int)code;
            response.ContentType = "application/json; charset=utf-8";
            response.RawBuffer = JsonSerializer.SerializeToUtf8Bytes(new {
                status, correlation_id = correlation
            });
        }

        private static string Value(JsonElement root, string name)
            => root.TryGetProperty(name, out JsonElement el) &&
               el.ValueKind == JsonValueKind.String ? el.GetString() : null;

        private static bool ValidPassword(string password)
        {
            if (string.IsNullOrEmpty(password) ||
                Encoding.UTF8.GetByteCount(password) < 10 ||
                Encoding.UTF8.GetByteCount(password) > 256 ||
                password.Contains('\0'))
                return false;
            return password.Any(c => c >= 'A' && c <= 'Z') &&
                   password.Any(c => c >= 'a' && c <= 'z') &&
                   password.Any(c => c >= '0' && c <= '9');
        }

        private bool Authorize(IOSHttpRequest request, IOSHttpResponse response,
            string scope, string correlation)
        {
            if (!m_Enabled || m_Store == null)
            {
                Reply(response, HttpStatusCode.ServiceUnavailable, "recovery_disabled", correlation);
                return false;
            }
            if (!HasSecureTransport(request))
            {
                Reply(response, HttpStatusCode.Forbidden, "https_required", correlation);
                return false;
            }
            if (string.IsNullOrWhiteSpace(request.Headers?["X-NexVerse-Api-Key"]))
            {
                Reply(response, HttpStatusCode.Unauthorized, "service_key_required", correlation);
                return false;
            }
            if (!m_Authenticator.TryAuthenticate(request, scope,
                out NexPrincipal principal, out UserAccount _,
                out int status, out string _))
            {
                Reply(response, (HttpStatusCode)status, status == 403 ?
                    "insufficient_scope" : "invalid_service_key", correlation);
                return false;
            }
            if (!principal.Subject.StartsWith("api-key:", StringComparison.Ordinal))
            {
                Reply(response, HttpStatusCode.Forbidden, "machine_credential_required", correlation);
                return false;
            }
            return true;
        }

        public void Handle(IOSHttpRequest request, IOSHttpResponse response)
        {
            string correlation = Guid.NewGuid().ToString("N");
            response.AddHeader("Cache-Control", "no-store");
            response.AddHeader("Pragma", "no-cache");
            response.AddHeader("X-Content-Type-Options", "nosniff");
            if (request == null || !string.Equals(request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase))
            {
                response.AddHeader("Allow", "POST");
                Reply(response, HttpStatusCode.MethodNotAllowed, "method_not_allowed", correlation);
                return;
            }
            string path = (request.UriPath ?? "").TrimEnd('/');
            bool issue = path.Equals("/api/v1/auth/password-recovery/challenges",
                StringComparison.OrdinalIgnoreCase);
            bool complete = path.Equals("/api/v1/auth/password-recovery/complete",
                StringComparison.OrdinalIgnoreCase);
            if (!issue && !complete)
            {
                Reply(response, HttpStatusCode.NotFound, "not_found", correlation);
                return;
            }
            if (!Authorize(request, response,
                    issue ? NexScopes.PasswordRecoveryIssue : NexScopes.PasswordRecoveryComplete,
                    correlation))
                return;
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (!RateAllowed(Peer(request), now))
            {
                Reply(response, HttpStatusCode.TooManyRequests, "rate_limited", correlation);
                return;
            }
            if (request.ContentLength64 > MaxBodyBytes ||
                (request.ContentLength64 < 0 && !request.HasEntityBody))
            {
                Reply(response, HttpStatusCode.RequestEntityTooLarge, "invalid_body_size", correlation);
                return;
            }
            try
            {
                using var reader = new StreamReader(request.InputStream, Encoding.UTF8,
                    detectEncodingFromByteOrderMarks: true, bufferSize: 1024, leaveOpen: true);
                char[] buffer = new char[MaxBodyBytes + 1];
                int used = 0, count;
                while (used < buffer.Length &&
                    (count = reader.Read(buffer, used, buffer.Length - used)) > 0)
                    used += count;
                if (used == 0 || used == buffer.Length)
                {
                    Reply(response, HttpStatusCode.RequestEntityTooLarge, "invalid_body_size", correlation);
                    return;
                }
                using JsonDocument doc = JsonDocument.Parse(
                    new string(buffer, 0, used), new JsonDocumentOptions { MaxDepth = 5 });
                if (doc.RootElement.ValueKind != JsonValueKind.Object)
                {
                    Reply(response, HttpStatusCode.BadRequest, "invalid_json", correlation);
                    return;
                }
                if (issue)
                    Issue(doc.RootElement, response, now, correlation);
                else
                    Complete(doc.RootElement, response, now, correlation);
            }
            catch (JsonException)
            {
                Reply(response, HttpStatusCode.BadRequest, "invalid_json", correlation);
            }
            catch (Exception ex)
            {
                // Do not log token, password, email, UUID or request contents.
                s_Log.ErrorFormat("[WORLD RECOVERY]: operation failed ({0}), correlation={1}",
                    ex.GetType().Name, correlation);
                Reply(response, HttpStatusCode.ServiceUnavailable,
                    "recovery_backend_unavailable", correlation);
            }
        }

        private void Issue(JsonElement data, IOSHttpResponse response, long now, string correlation)
        {
            string email = Value(data, "email")?.Trim();
            if (string.IsNullOrEmpty(email) || email.Length > 254 ||
                !email.Contains('@') || email.Contains('\r') || email.Contains('\n'))
            {
                Reply(response, HttpStatusCode.BadRequest, "invalid_email", correlation);
                return;
            }
            // Return no account identity to unauthorized or public callers.
            // Only accounts with one exact verified local email match qualify.
            var matching = (m_Accounts.GetUserAccounts(UUID.Zero, email) ??
                new List<UserAccount>())
                .Where(x => x != null && x.LoginAllowed &&
                    string.Equals(x.Email?.Trim(), email, StringComparison.OrdinalIgnoreCase))
                .Take(2).ToArray();
            if (matching.Length != 1)
            {
                Reply(response, HttpStatusCode.Accepted, "accepted", correlation);
                return;
            }
            string token = m_Store.Issue(matching[0].PrincipalID.Guid, now);
            if (token == null)
            {
                Reply(response, HttpStatusCode.Accepted, "accepted", correlation);
                return;
            }
            // Token is returned ONLY to the separately-scoped trusted backend,
            // which must send the email to the World-verified address.
            response.StatusCode = (int)HttpStatusCode.Created;
            response.ContentType = "application/json; charset=utf-8";
            response.RawBuffer = JsonSerializer.SerializeToUtf8Bytes(new {
                status = "issued", token, expires_in = 900,
                delivery_email = matching[0].Email, correlation_id = correlation
            });
        }

        private void Complete(JsonElement data, IOSHttpResponse response, long now, string correlation)
        {
            string token = Value(data, "token");
            string password = Value(data, "new_password");
            if (!ValidPassword(password))
            {
                Reply(response, HttpStatusCode.BadRequest, "invalid_password", correlation);
                return;
            }
            if (!m_Store.TryClaim(token, now, out Guid principal, out string claim))
            {
                Reply(response, HttpStatusCode.Gone, "invalid_or_expired_challenge", correlation);
                return;
            }

            bool passwordWritten = false;
            try
            {
                // Account ID comes from the World-owned challenge, NEVER JSON.
                // A deactivated account must not be revived by recovery.
                NexUserRecord user = m_Users.GetById(principal.ToString("D"));
                if (user == null)
                {
                    Reply(response, HttpStatusCode.Gone, "invalid_or_expired_challenge", correlation);
                    return;
                }
                UserAccount account = m_Accounts.GetUserAccount(UUID.Zero, new UUID(principal));
                if (account == null || !account.LoginAllowed)
                {
                    Reply(response, HttpStatusCode.Gone, "invalid_or_expired_challenge", correlation);
                    return;
                }
                passwordWritten = m_Users.SetPassword(principal.ToString("D"), password);
                if (!passwordWritten)
                {
                    Reply(response, HttpStatusCode.ServiceUnavailable, "password_update_failed", correlation);
                    return;
                }
                // Mark challenge consumed before responding success. If a
                // storage failure occurs now, it remains claimed/fail-closed.
                if (!m_Store.Finish(token, claim, true, DateTimeOffset.UtcNow.ToUnixTimeSeconds()))
                {
                    Reply(response, HttpStatusCode.ServiceUnavailable,
                        "challenge_finalize_failed", correlation);
                    return;
                }
                m_OAuth?.RevokeSubjectRefreshTokens(principal.ToString("D"));
                Reply(response, HttpStatusCode.OK, "password_updated", correlation);
            }
            finally
            {
                // On a known failed mutation, return the claim to pending;
                // if the process crashes while claimed, require a new email.
                if (!passwordWritten)
                    m_Store.Finish(token, claim, false, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            }
        }
    }
}

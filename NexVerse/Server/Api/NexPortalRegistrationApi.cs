// SPDX-License-Identifier: MPL-2.0
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using NexVerse.Core.Audit;
using NexVerse.Core.Security;
using OpenMetaverse;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Services.Interfaces;

namespace NexVerse.Server.Api
{
    /// <summary>
    /// Server-to-server only. The Stadtportal backend authenticates using its
    /// separate, minimally scoped API key; the browser NEVER receives that key.
    /// This route does not allow user-submitted model account names.
    /// </summary>
    internal sealed class NexPortalRegistrationApi
    {
        private static readonly Regex s_Name = new Regex(
            @"^\p{L}[\p{L}\p{M}\p{Nd}_-]{0,30}$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);
        private readonly NexApiAuthenticator m_Auth;
        private readonly IUserAccountService m_Accounts;
        private readonly IOglStarterAccountRegistration m_Starter;
        private readonly INexAuditSink m_Audit;
        private readonly bool m_Enabled;
        private readonly bool m_TrustedTlsProxy;
        private readonly IPAddress m_ProxyIp;
        private readonly NexFixedWindowRateLimiter m_RateLimiter =
            new NexFixedWindowRateLimiter(20, 60);
        private static readonly object s_CreateLock = new object();

        public NexPortalRegistrationApi(
            NexApiAuthenticator auth,
            IUserAccountService accounts,
            INexAuditSink audit,
            bool enabled,
            bool trustedTlsProxy,
            string trustedProxyIp)
        {
            m_Auth = auth ?? throw new ArgumentNullException(nameof(auth));
            m_Accounts = accounts ?? throw new ArgumentNullException(nameof(accounts));
            m_Starter = accounts as IOglStarterAccountRegistration;
            m_Audit = audit ?? NullNexAuditSink.Instance;
            m_Enabled = enabled;
            m_TrustedTlsProxy = trustedTlsProxy;
            m_ProxyIp = IPAddress.TryParse(trustedProxyIp, out IPAddress address)
                ? address : IPAddress.None;
        }

        public void Handle(IOSHttpRequest request, IOSHttpResponse response)
        {
            response.KeepAlive = false;
            response.AddHeader("Cache-Control", "no-store");
            response.AddHeader("X-Content-Type-Options", "nosniff");

            if (request == null ||
                !string.Equals(request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase))
            {
                response.AddHeader("Allow", "POST");
                Error(response, HttpStatusCode.MethodNotAllowed,
                    "method_not_allowed", "Nur POST ist erlaubt.");
                return;
            }

            if (!m_Enabled || m_Starter == null)
            {
                Error(response, HttpStatusCode.ServiceUnavailable,
                    "registration_unavailable",
                    "Avatar-Registrierung ist derzeit nicht freigeschaltet.");
                return;
            }

            // An HTTP request from a public browser or an untrusted proxy must
            // not carry password or server API key. Trust proxy protocol
            // headers ONLY from a statically configured loopback/private proxy.
            bool secure = request.IsSecured ||
                (m_TrustedTlsProxy &&
                 m_ProxyIp.Equals(request.RemoteIPEndPoint?.Address) &&
                 string.Equals(request.Headers?["X-Forwarded-Proto"],
                     "https", StringComparison.OrdinalIgnoreCase));
            if (!secure)
            {
                Error(response, HttpStatusCode.UpgradeRequired,
                    "https_required", "Registrierung ist nur ueber HTTPS moeglich.");
                return;
            }

            if (string.IsNullOrWhiteSpace(request.Headers?["X-NexVerse-Api-Key"]))
            {
                Error(response, HttpStatusCode.Unauthorized,
                    "portal_key_required", "Der Portalserver benoetigt einen Registrierungs-API-Schluessel.");
                return;
            }

            if (!m_Auth.TryAuthenticate(
                request, NexScopes.PortalRegister,
                out NexPrincipal principal, out UserAccount _,
                out int status, out string _error))
            {
                Error(response, (HttpStatusCode)status,
                    "portal_access_denied", "Portalserver ist fuer Registrierungen nicht autorisiert.");
                return;
            }

            string client = request.RemoteIPEndPoint?.Address?.ToString() ?? "unknown";
            NexRateLimitDecision quota = m_RateLimiter.TryConsume(
                principal.Subject + "|" + client,
                DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            if (!quota.Accepted)
            {
                response.AddHeader("Retry-After", quota.RetryAfterSeconds.ToString());
                Error(response, HttpStatusCode.TooManyRequests,
                    "rate_limited", "Zu viele Registrierungen. Bitte spaeter erneut versuchen.");
                return;
            }

            if (request.ContentLength64 > 8192 ||
                request.ContentType == null ||
                !request.ContentType.StartsWith("application/json", StringComparison.OrdinalIgnoreCase))
            {
                Error(response, HttpStatusCode.BadRequest,
                    "invalid_request", "JSON-Anfrage ist erforderlich (maximal 8 KiB).");
                return;
            }

            try
            {
                using MemoryStream body = new MemoryStream();
                byte[] buffer = new byte[2048];
                int read;
                while ((read = request.InputStream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    if (body.Length + read > 8192)
                    {
                        Error(response, HttpStatusCode.RequestEntityTooLarge,
                            "request_too_large", "Registrierungsdaten sind zu gross.");
                        return;
                    }
                    body.Write(buffer, 0, read);
                }
                using JsonDocument doc = JsonDocument.Parse(body.ToArray());
                JsonElement root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object)
                {
                    Error(response, HttpStatusCode.BadRequest, "invalid_json",
                        "Ein JSON-Objekt ist erforderlich.");
                    return;
                }

                string first = ReadString(root, "first_name")?.Trim();
                string last = ReadString(root, "last_name")?.Trim();
                string password = ReadString(root, "password");
                string email = ReadString(root, "email")?.Trim();
                string gender = ReadString(root, "gender")?.Trim().ToLowerInvariant();
                if (string.IsNullOrWhiteSpace(last))
                    last = "Resident";

                if (string.IsNullOrEmpty(first) || !s_Name.IsMatch(first) ||
                    !s_Name.IsMatch(last))
                {
                    Error(response, HttpStatusCode.BadRequest, "invalid_avatar_name",
                        "Vor- und Nachname muessen 1-31 gueltige Zeichen enthalten.");
                    return;
                }
                if (string.IsNullOrEmpty(password) || password.Length < 8 ||
                    password.Length > 256)
                {
                    Error(response, HttpStatusCode.BadRequest, "invalid_password",
                        "Das Passwort muss 8 bis 256 Zeichen enthalten.");
                    return;
                }
                if (string.IsNullOrEmpty(email) || email.Length > 64 ||
                    !MailAddress.TryCreate(email, out MailAddress parsedMail) ||
                    !string.Equals(parsedMail.Address, email, StringComparison.OrdinalIgnoreCase))
                {
                    Error(response, HttpStatusCode.BadRequest, "invalid_email",
                        "Bitte eine gueltige E-Mail-Adresse angeben.");
                    return;
                }

                string model;
                string displayModel;
                switch (gender)
                {
                    case "male":
                    case "maennlich":
                    case "männlich":
                        model = "ogl:benjamin";
                        displayModel = "Benjamin Resident";
                        break;
                    case "female":
                    case "weiblich":
                        model = "ogl:luna";
                        displayModel = "Luna Resident";
                        break;
                    default:
                        Error(response, HttpStatusCode.BadRequest, "invalid_gender",
                            "Bitte male oder female auswaehlen.");
                        return;
                }

                // Ignore no caller-selected model, permissions or UUID. The
                // gender mapping above is the only allowed source.
                UserAccount account;
                lock (s_CreateLock)
                {
                    if (m_Accounts.GetUserAccount(UUID.Zero, first, last) != null)
                    {
                        Error(response, HttpStatusCode.Conflict, "avatar_name_taken",
                            "Der Avatarname ist bereits vergeben.");
                        return;
                    }
                    account = m_Starter.CreateOglStarterAccount(
                        UUID.Random(), first, last, password, email, model);
                }

                if (account == null || !account.LoginAllowed)
                {
                    Error(response, HttpStatusCode.ServiceUnavailable,
                        "starter_model_unavailable",
                        "Registrierung derzeit nicht moeglich: Avatarvorlage oder erforderliche Dienste sind nicht freigegeben. Bitte Administrator kontaktieren.");
                    return;
                }

                m_Audit.Record(new NexAuditEvent(
                    principal.Subject, "portal.avatar.register",
                    account.PrincipalID.ToString(), Guid.NewGuid().ToString("N"),
                    new Dictionary<string, string> {
                        ["model_id"] = model,
                        ["first_name"] = first,
                        ["last_name"] = last
                    }));

                Write(response, HttpStatusCode.Created, new
                {
                    success = true,
                    message = "Avatar " + account.Name + " wurde erfolgreich registriert.",
                    avatar_name = account.Name,
                    first_name = account.FirstName,
                    last_name = account.LastName,
                    model_name = displayModel,
                    avatar_id = account.PrincipalID.ToString()
                });
            }
            catch (JsonException)
            {
                Error(response, HttpStatusCode.BadRequest,
                    "invalid_json", "Registrierungsdaten sind kein gueltiges JSON.");
            }
            catch (Exception)
            {
                // No password, email, stack or backend secrets in responses.
                Error(response, HttpStatusCode.ServiceUnavailable,
                    "registration_failed", "Avatar konnte nicht registriert werden. Bitte spaeter erneut versuchen.");
            }
        }

        private static string ReadString(JsonElement root, string name)
        {
            return root.TryGetProperty(name, out JsonElement e) &&
                   e.ValueKind == JsonValueKind.String
                ? e.GetString()
                : null;
        }

        private static void Error(IOSHttpResponse response,
            HttpStatusCode code, string error, string message)
        {
            Write(response, code, new { success = false, error, message });
        }

        private static void Write(IOSHttpResponse response,
            HttpStatusCode code, object payload)
        {
            response.StatusCode = (int)code;
            response.ContentType = "application/json; charset=utf-8";
            response.RawBuffer = JsonSerializer.SerializeToUtf8Bytes(payload);
        }
    }
}

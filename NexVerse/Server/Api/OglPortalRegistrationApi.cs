// SPDX-License-Identifier: MPL-2.0
using System;
using System.IO;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using NexVerse.Core.Security;
using OpenMetaverse;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Services.Interfaces;

namespace NexVerse.Server.Api
{
    /// <summary>
    /// Server-to-server registration only: the Stadtportal's backend must hold
    /// a dedicated users:register API key, NEVER its browser client.
    /// Disabled by default until published starter outfits have been approved.
    /// </summary>
    internal sealed class OglPortalRegistrationApi
    {
        private const int MaxBodyCharacters = 8192;
        private static readonly Regex s_Username = new(
            @"^[\p{L}][\p{L}\p{M}\p{Nd}_-]{1,30}$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled,
            TimeSpan.FromMilliseconds(100));
        private readonly NexApiAuthenticator m_Authenticator;
        private readonly IUserAccountService m_Accounts;
        private readonly IOglPortalAccountProvisioner m_Provisioner;
        private readonly bool m_Enabled;

        public OglPortalRegistrationApi(
            NexApiAuthenticator authenticator, IUserAccountService accounts, bool enabled)
        {
            m_Authenticator = authenticator ?? throw new ArgumentNullException(nameof(authenticator));
            m_Accounts = accounts ?? throw new ArgumentNullException(nameof(accounts));
            m_Provisioner = accounts as IOglPortalAccountProvisioner;
            m_Enabled = enabled;
        }

        public void Handle(IOSHttpRequest request, IOSHttpResponse response)
        {
            response.AddHeader("Cache-Control", "no-store");
            response.AddHeader("X-Content-Type-Options", "nosniff");
            if (request == null || !string.Equals(request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase))
            {
                response.AddHeader("Allow", "POST");
                Reply(response, HttpStatusCode.MethodNotAllowed,
                    "method_not_allowed", "POST ist erforderlich.");
                return;
            }
            if (!m_Enabled || m_Provisioner == null)
            {
                Reply(response, HttpStatusCode.ServiceUnavailable,
                    "registration_disabled", "Avatarregistrierung ist derzeit nicht freigeschaltet.");
                return;
            }

            // Proxy may terminate TLS locally. Only a *loopback peer* can
            // assert X-Forwarded-Proto. Never trust a public remote client.
            bool secured = request.IsSecured ||
                (request.RemoteIPEndPoint != null &&
                 IPAddress.IsLoopback(request.RemoteIPEndPoint.Address) &&
                 string.Equals(request.Headers?["X-Forwarded-Proto"], "https",
                     StringComparison.OrdinalIgnoreCase));
            if (!secured)
            {
                Reply(response, HttpStatusCode.Forbidden,
                    "https_required", "Eine verschluesselte Verbindung ist erforderlich.");
                return;
            }

            // Restrict this route to a machine credential, not interactive
            // resident or OAuth bearer tokens. All other API keys lack scope.
            if (string.IsNullOrWhiteSpace(request.Headers?["X-NexVerse-Api-Key"]) ||
                !m_Authenticator.TryAuthenticate(request, NexScopes.UsersRegister,
                    out NexPrincipal principal, out UserAccount _,
                    out int statusCode, out string error) ||
                !principal.Subject.StartsWith("api-key:", StringComparison.Ordinal))
            {
                response.AddHeader("WWW-Authenticate", "ApiKey");
                Reply(response, HttpStatusCode.Unauthorized, "unauthorized_portal",
                    "Portalzugang nicht autorisiert.");
                return;
            }

            if (request.ContentLength64 > MaxBodyCharacters * 4L ||
                request.ContentLength64 < 0 && !request.HasEntityBody)
            {
                Reply(response, HttpStatusCode.RequestEntityTooLarge,
                    "invalid_body_size", "Registrierungsanfrage ist zu gross oder leer.");
                return;
            }

            try
            {
                using StreamReader reader = new(request.InputStream, Encoding.UTF8, true, 1024, leaveOpen: true);
                char[] buffer = new char[MaxBodyCharacters + 1];
                int used = 0, count;
                while (used < buffer.Length &&
                    (count = reader.Read(buffer, used, buffer.Length - used)) > 0)
                    used += count;
                if (used == buffer.Length || used == 0)
                {
                    Reply(response, HttpStatusCode.RequestEntityTooLarge,
                        "invalid_body_size", "Registrierungsanfrage ist zu gross oder leer.");
                    return;
                }

                using JsonDocument doc = JsonDocument.Parse(
                    new string(buffer, 0, used), new JsonDocumentOptions { MaxDepth = 8 });
                JsonElement root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object)
                {
                    Reply(response, HttpStatusCode.BadRequest, "invalid_json", "JSON-Objekt erforderlich.");
                    return;
                }

                string first = Value(root, "first_name");
                string last = Value(root, "last_name");
                string pass = Value(root, "password");
                string email = Value(root, "email");
                string gender = Value(root, "gender");

                if (string.IsNullOrWhiteSpace(last))
                    last = "Resident";
                if (!IsValidName(first) || !IsValidName(last))
                {
                    Reply(response, HttpStatusCode.BadRequest, "invalid_avatar_name",
                        "Vor- und Nachname muessen 2 bis 31 gueltige Zeichen besitzen.");
                    return;
                }
                if (pass == null || pass.Length < 12 || pass.Length > 128 ||
                    pass.IndexOf('\0') >= 0)
                {
                    Reply(response, HttpStatusCode.BadRequest, "invalid_password",
                        "Passwort muss zwischen 12 und 128 Zeichen lang sein.");
                    return;
                }
                if (!IsValidEmail(email))
                {
                    Reply(response, HttpStatusCode.BadRequest, "invalid_email",
                        "Ungueltige E-Mail-Adresse.");
                    return;
                }

                string selected = gender?.Trim().ToLowerInvariant();
                string modelAlias;
                string modelName;
                if (selected == "male" || selected == "maennlich" || selected == "männlich")
                {
                    modelAlias = "ogl:benjamin";
                    modelName = "Benjamin Resident";
                }
                else if (selected == "female" || selected == "weiblich")
                {
                    modelAlias = "ogl:luna";
                    modelName = "Luna Resident";
                }
                else
                {
                    Reply(response, HttpStatusCode.BadRequest, "invalid_gender",
                        "Bitte Maennlich oder Weiblich auswaehlen.");
                    return;
                }

                if (m_Accounts.GetUserAccount(UUID.Zero, first, last) != null)
                {
                    Reply(response, HttpStatusCode.Conflict, "avatar_name_taken",
                        "Dieser Avatarname ist bereits vergeben.");
                    return;
                }
                if (m_Accounts.GetUserAccount(UUID.Zero, email) != null)
                {
                    Reply(response, HttpStatusCode.Conflict, "email_registered",
                        "Die E-Mail-Adresse ist bereits registriert.");
                    return;
                }

                UserAccount newAccount = m_Provisioner.CreatePortalAvatar(first, last, pass, email, modelAlias);
                if (newAccount == null)
                {
                    Reply(response, HttpStatusCode.UnprocessableEntity, "registration_failed",
                        "Registrierung fehlgeschlagen. Startervorlage, Berechtigungen oder Account-Service pruefen.");
                    return;
                }

                response.StatusCode = (int)HttpStatusCode.Created;
                response.ContentType = "application/json; charset=utf-8";
                response.RawBuffer = JsonSerializer.SerializeToUtf8Bytes(new
                {
                    success = true,
                    avatar_name = newAccount.FirstName + " " + newAccount.LastName,
                    first_name = newAccount.FirstName,
                    last_name = newAccount.LastName,
                    model_name = modelName,
                    avatar_id = newAccount.PrincipalID.ToString(),
                    message = "Avatar " + newAccount.FirstName + " " + newAccount.LastName + " wurde erfolgreich registriert."
                });
            }
            catch (JsonException)
            {
                Reply(response, HttpStatusCode.BadRequest, "invalid_json",
                    "Registrierungsdaten haben kein gueltiges JSON-Format.");
            }
            catch (Exception)
            {
                // Do not expose internal SQL, password or inventory errors.
                Reply(response, HttpStatusCode.ServiceUnavailable, "registration_unavailable",
                    "Registrierung zurzeit nicht moeglich. Bitte spaeter erneut versuchen.");
            }
        }

        private static string Value(JsonElement root, string key)
        {
            if (!root.TryGetProperty(key, out JsonElement value) ||
                value.ValueKind != JsonValueKind.String)
                return null;
            return value.GetString()?.Trim();
        }

        private static bool IsValidName(string name)
        {
            return !string.IsNullOrWhiteSpace(name) && s_Username.IsMatch(name);
        }

        private static bool IsValidEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email) || email.Length > 254 || email.Contains(' '))
                return false;
            try
            {
                MailAddress parsed = new(email);
                return string.Equals(parsed.Address, email, StringComparison.OrdinalIgnoreCase);
            }
            catch (FormatException)
            {
                return false;
            }
        }

        private static void Reply(IOSHttpResponse response, HttpStatusCode status,
            string code, string message)
        {
            response.StatusCode = (int)status;
            response.ContentType = "application/json; charset=utf-8";
            response.RawBuffer = JsonSerializer.SerializeToUtf8Bytes(new
            {
                success = false,
                error = code,
                message
            });
        }
    }
}

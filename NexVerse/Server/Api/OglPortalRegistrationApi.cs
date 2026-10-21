// SPDX-License-Identifier: MPL-2.0
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using NexVerse.Core.Security;
using log4net;
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
        private static readonly ILog m_Log = LogManager.GetLogger(typeof(OglPortalRegistrationApi));
        private static readonly Regex s_Username = new(
            @"^[\p{L}][\p{L}\p{M}\p{Nd}_-]{1,30}$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled,
            TimeSpan.FromMilliseconds(100));
        private readonly NexApiAuthenticator m_Authenticator;
        private readonly IUserAccountService m_Accounts;
        private readonly IOglPortalAccountProvisioner m_Provisioner;
        private readonly bool m_Enabled;
        private readonly HashSet<string> m_TrustedProxyIps;
        private readonly HashSet<string> m_TrustedHttpPeers;
        private readonly string m_DefaultRegionName;
        private readonly float m_DefaultHomeX;
        private readonly float m_DefaultHomeY;
        private readonly float m_DefaultHomeZ;

        public OglPortalRegistrationApi(
            NexApiAuthenticator authenticator, IUserAccountService accounts, bool enabled,
            string trustedProxyIps = "", string trustedHttpPeers = "",
            string defaultRegionName = "Magdeburg",
            float defaultX = 134, float defaultY = 111, float defaultZ = 23)
        {
            m_Authenticator = authenticator ?? throw new ArgumentNullException(nameof(authenticator));
            m_Accounts = accounts ?? throw new ArgumentNullException(nameof(accounts));
            m_Provisioner = accounts as IOglPortalAccountProvisioner;
            m_Enabled = enabled;
            m_TrustedProxyIps = ParseIpAddresses(trustedProxyIps);
            m_TrustedHttpPeers = ParseIpAddresses(trustedHttpPeers);
            m_DefaultRegionName = defaultRegionName;
            m_DefaultHomeX = defaultX;
            m_DefaultHomeY = defaultY;
            m_DefaultHomeZ = defaultZ;
        }

        private static HashSet<string> ParseIpAddresses(string csv)
        {
            var addresses = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string value in (csv ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                if (!IPAddress.TryParse(value.Trim(), out IPAddress address))
                    throw new ArgumentException("Portal registration trusted peer list requires literal IP addresses.");
                addresses.Add(address.IsIPv4MappedToIPv6
                    ? address.MapToIPv4().ToString() : address.ToString());
            }
            return addresses;
        }

        private static bool TrustedPeer(IOSHttpRequest request, HashSet<string> allowed)
        {
            IPAddress peer = request.RemoteIPEndPoint?.Address;
            if (peer == null)
                return false;
            string value = peer.IsIPv4MappedToIPv6 ? peer.MapToIPv4().ToString() : peer.ToString();
            return allowed.Contains(value);
        }

        // Both signup and live name lookup must share identical
        // transport, enablement and machine-key security requirements.
        private bool AuthorizePortal(IOSHttpRequest request, IOSHttpResponse response)
        {
            if (!m_Enabled || m_Provisioner == null)
            {
                Reply(response, HttpStatusCode.ServiceUnavailable,
                    "registration_disabled", "Avatarregistrierung ist derzeit nicht freigeschaltet.");
                return false;
            }

            // A KeyHelp proxy may terminate HTTPS on another host before
            // forwarding HTTP to ROBUST. Trust X-Forwarded-Proto only from
            // loopback or an explicitly configured exact proxy IP address.
            IPAddress peer = request.RemoteIPEndPoint?.Address;
            bool isLoopback = peer != null && IPAddress.IsLoopback(peer);
            bool trustedProxy = isLoopback || TrustedPeer(request, m_TrustedProxyIps);
            bool forwardedHttps = trustedProxy &&
                string.Equals(request.Headers?["X-Forwarded-Proto"], "https",
                    StringComparison.OrdinalIgnoreCase);
            // Backend HTTP is a separate, explicit per-IP opt-in. Use
            // loopback or a private/VPN link to protect passwords/API keys.
            bool trustedBackendHttp = TrustedPeer(request, m_TrustedHttpPeers);
            bool secured = request.IsSecured || forwardedHttps || trustedBackendHttp;
            if (!secured)
            {
                Reply(response, HttpStatusCode.Forbidden,
                    "https_required", "Eine verschluesselte Verbindung ist erforderlich.");
                return false;
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
                return false;
            }

            return true;
        }

        /// <summary>
        /// Read-only preflight. The final POST repeats this authoritative
        /// lookup; this response must never be interpreted as a reservation.
        /// </summary>
        public void HandleNameAvailability(IOSHttpRequest request, IOSHttpResponse response)
        {
            response.AddHeader("Cache-Control", "no-store");
            response.AddHeader("X-Content-Type-Options", "nosniff");
            if (request == null || !string.Equals(request.HttpMethod, "GET", StringComparison.OrdinalIgnoreCase))
            {
                response.AddHeader("Allow", "GET");
                Reply(response, HttpStatusCode.MethodNotAllowed,
                    "method_not_allowed", "GET ist erforderlich.");
                return;
            }
            if (!AuthorizePortal(request, response))
                return;

            string first = (request.QueryString?["first_name"] ?? string.Empty).Trim();
            string last = (request.QueryString?["last_name"] ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(last))
                last = "Resident";
            if (!IsValidName(first) || !IsValidName(last))
            {
                Reply(response, HttpStatusCode.BadRequest, "invalid_avatar_name",
                    "Vor- und Nachname müssen 2 bis 31 gültige Zeichen besitzen.");
                return;
            }
            try
            {
                bool available = m_Accounts.GetUserAccount(UUID.Zero, first, last) == null;
                response.StatusCode = (int)HttpStatusCode.OK;
                response.ContentType = "application/json; charset=utf-8";
                response.RawBuffer = JsonSerializer.SerializeToUtf8Bytes(new
                {
                    available,
                    status = available ? "available" : "taken",
                    first_name = first,
                    last_name = last,
                    avatar_name = first + " " + last
                });
            }
            catch (Exception e)
            {
                m_Log.Error("[OGL PORTAL REGISTRATION]: Name availability lookup failed; correlation_id=" +
                    NexApiRequestContext.Ensure(response), e);
                Reply(response, HttpStatusCode.ServiceUnavailable, "name_check_unavailable",
                    "Namensprüfung ist momentan nicht verfügbar.");
            }
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
            if (!AuthorizePortal(request, response))
                return;

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
                string pass = Value(root, "password", trim: false);
                string email = Value(root, "email");
                string gender = Value(root, "gender");
                // Older portal forms may send FirstName/LastName and a
                // selected start avatar rather than the gender field.
                // Accept only the two server-side allowlisted outfits.
                string selectedModel = Value(root, "start_avatar") ??
                    Value(root, "avatar_model") ??
                    Value(root, "model");

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
                string selectedAvatar = selectedModel?.Trim().ToLowerInvariant();
                bool selectedMale = selected == "male" || selected == "maennlich" ||
                    selected == "männlich" || selected == "m";
                bool selectedFemale = selected == "female" || selected == "weiblich" ||
                    selected == "w" || selected == "f";
                bool maleModel = selectedAvatar == "benjamin" ||
                    selectedAvatar == "benjamin resident" || selectedAvatar == "ogl:benjamin";
                bool femaleModel = selectedAvatar == "luna" ||
                    selectedAvatar == "luna resident" || selectedAvatar == "ogl:luna";

                if ((selectedModel != null && !maleModel && !femaleModel) ||
                    (gender != null && !selectedMale && !selectedFemale) ||
                    (selectedMale && femaleModel) || (selectedFemale && maleModel) ||
                    (!selectedMale && !selectedFemale && !maleModel && !femaleModel))
                {
                    Reply(response, HttpStatusCode.BadRequest, "invalid_avatar_model",
                        "Bitte Benjamin Resident oder Luna Resident auswaehlen.");
                    return;
                }

                string modelAlias = selectedMale || maleModel ? "ogl:benjamin" : "ogl:luna";
                string modelName = modelAlias == "ogl:benjamin"
                    ? "Benjamin Resident" : "Luna Resident";

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

                if (!TryReadHomeRegion(root, out string homeRegionId,
                    out string homeRegionName, out Vector3 homePosition))
                {
                    Reply(response, HttpStatusCode.BadRequest, "invalid_home_region",
                        "Ungueltige Startregion oder Position: home_region.id/name/position pruefen.");
                    return;
                }

                UserAccount newAccount = m_Provisioner.CreatePortalAvatar(
                    first, last, pass, email, modelAlias,
                    homeRegionId, homeRegionName, homePosition);
                if (newAccount == null)
                {
                    m_Log.ErrorFormat(
                        "[OGL-REGISTRIERUNG] FEHLER: Anlegen von {0} {1} mit Modell {2} gescheitert; Region {3}/{4}; Vorgang={5}. Details im USER ACCOUNT/AVATAR TEMPLATE-Log.",
                        first, last, modelName, homeRegionName, homePosition,
                        NexApiRequestContext.Ensure(response));
                    Reply(response, HttpStatusCode.UnprocessableEntity, "registration_failed",
                        "Registrierung fehlgeschlagen. Modell/Outfit, Region und Home/Last im ROBUST-Log pruefen.");
                    return;
                }

                m_Log.InfoFormat(
                    "[OGL-REGISTRIERUNG] ERFOLG: {0} {1} | UUID={2} | Vorlage={3} | Home/Last={4}/{5} | Vorgang={6}",
                    newAccount.FirstName, newAccount.LastName, newAccount.PrincipalID,
                    modelName, homeRegionName, homePosition,
                    NexApiRequestContext.Ensure(response));

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
                    ready = true,
                    home_region = new {
                        id = string.IsNullOrWhiteSpace(homeRegionId) ? null : homeRegionId,
                        name = homeRegionName,
                        position = new { x = homePosition.X, y = homePosition.Y, z = homePosition.Z }
                    },
                    last_position = new { x = homePosition.X, y = homePosition.Y, z = homePosition.Z },
                    message = "Avatar " + newAccount.FirstName + " " + newAccount.LastName + " wurde erfolgreich registriert."
                });
            }
            catch (JsonException)
            {
                Reply(response, HttpStatusCode.BadRequest, "invalid_json",
                    "Registrierungsdaten haben kein gueltiges JSON-Format.");
            }
            catch (Exception e)
            {
                // Never put password, API key or database details into a
                // response. Correlate failures with restricted server logs.
                m_Log.Error("[OGL PORTAL REGISTRATION]: Provisioning failed; correlation_id=" +
                    NexApiRequestContext.Ensure(response), e);
                Reply(response, HttpStatusCode.ServiceUnavailable, "registration_unavailable",
                    "Registrierung zurzeit nicht moeglich. Bitte spaeter erneut versuchen.");
            }
        }

        private bool TryReadHomeRegion(JsonElement root, out string regionId,
            out string regionName, out Vector3 position)
        {
            regionId = null;
            regionName = m_DefaultRegionName;
            position = new Vector3(m_DefaultHomeX, m_DefaultHomeY, m_DefaultHomeZ);
            if (!root.TryGetProperty("home_region", out JsonElement region))
                return true;
            if (region.ValueKind != JsonValueKind.Object)
                return false;

            bool hasId = region.TryGetProperty("id", out JsonElement id);
            bool hasName = region.TryGetProperty("name", out JsonElement name);
            if (hasId && id.ValueKind != JsonValueKind.String)
                return false;
            if (hasName && name.ValueKind != JsonValueKind.String)
                return false;
            string selectedId = hasId ? id.GetString()?.Trim() : null;
            string selectedName = hasName ? name.GetString()?.Trim() : null;
            if (!string.IsNullOrWhiteSpace(selectedId))
            {
                if (!UUID.TryParse(selectedId, out UUID parsed) || parsed.IsZero())
                    return false;
                regionId = parsed.ToString();
                regionName = !string.IsNullOrWhiteSpace(selectedName) ? selectedName : null;
            }
            else if (!string.IsNullOrWhiteSpace(selectedName))
                regionName = selectedName;

            if (!region.TryGetProperty("position", out JsonElement coords))
                return true;
            if (coords.ValueKind != JsonValueKind.Object ||
                !coords.TryGetProperty("x", out JsonElement x) ||
                !coords.TryGetProperty("y", out JsonElement y) ||
                !coords.TryGetProperty("z", out JsonElement z) ||
                !x.TryGetSingle(out float px) ||
                !y.TryGetSingle(out float py) ||
                !z.TryGetSingle(out float pz) ||
                !float.IsFinite(px) || !float.IsFinite(py) || !float.IsFinite(pz))
                return false;
            position = new Vector3(px, py, pz);
            return true;
        }

        // Accept conventional Portal/Go JSON names (first_name,
        // firstName, FirstName) without changing the canonical API contract.
        // The name/sex/model whitelist and server-to-server auth remain strict.
        private static string Value(JsonElement root, string key, bool trim = true)
        {
            if (!root.TryGetProperty(key, out JsonElement value))
            {
                string normalized = key.Replace("_", "").Replace("-", "");
                foreach (JsonProperty property in root.EnumerateObject())
                {
                    if (string.Equals(property.Name.Replace("_", "").Replace("-", ""),
                        normalized, StringComparison.OrdinalIgnoreCase))
                    {
                        value = property.Value;
                        break;
                    }
                }
            }
            if (value.ValueKind != JsonValueKind.String)
                return null;
            string raw = value.GetString();
            return trim ? raw?.Trim() : raw;
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
            // Always give the ROBUST operator a useful failure code,
            // without exposing the submitted password, email or API key.
            string correlation = NexApiRequestContext.Ensure(response);
            m_Log.WarnFormat(
                "[OGL-REGISTRIERUNG] FEHLER: HTTP {0} | Grund={1} | Vorgang={2}",
                (int)status, code, correlation);
            response.StatusCode = (int)status;
            response.ContentType = "application/json; charset=utf-8";
            response.RawBuffer = JsonSerializer.SerializeToUtf8Bytes(new
            {
                success = false,
                error = code,
                message,
                correlation_id = NexApiRequestContext.CurrentCorrelationId
            });
        }
    }
}

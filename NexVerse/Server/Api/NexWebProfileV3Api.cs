// SPDX-License-Identifier: MPL-2.0

using System;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using OpenMetaverse.Imaging;
using System.Net;
using System.Text;
using System.Text.Json;
using NexVerse.Core.Security;
using NexVerse.Core.Audit;
using System.Collections.Generic;
using log4net;
using OpenMetaverse;
using OpenSim.Data;
using OpenSim.Framework;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Services.Interfaces;

namespace NexVerse.Server.Api
{
    /// <summary>
    /// NexVerse WebProfileV3 projection over the authoritative UserAccount and
    /// OpenSim profile stores. This deliberately reuses IProfilesData so viewer
    /// profiles and web/API profiles never diverge.
    /// </summary>
    internal sealed class NexWebProfileV3Api
    {
        private static readonly ILog m_Log = LogManager.GetLogger(typeof(NexWebProfileV3Api));
        private readonly IUserAccountService m_Accounts;
        private readonly IProfilesData m_Profiles;
        private readonly IAssetService m_Assets;
        private readonly Func<IAssetService> m_AssetResolver;
        private readonly IXInventoryData m_InventoryData;
        private const int MaxProfileAssetBytes = 8 * 1024 * 1024;
        private const int MaxProfileImageWidth = 2048;
        private readonly NexApiAuthenticator m_Authenticator;
        private readonly INexAuditSink m_Audit;

        public NexWebProfileV3Api(
            IUserAccountService accounts,
            IProfilesData profiles,
            IAssetService assets,
            NexApiAuthenticator authenticator,
            INexAuditSink audit = null,
            Func<IAssetService> assetResolver = null,
            IXInventoryData inventoryData = null)
        {
            m_Accounts = accounts ?? throw new ArgumentNullException(nameof(accounts));
            m_Profiles = profiles ?? throw new ArgumentNullException(nameof(profiles));
            m_Assets = assets;
            m_AssetResolver = assetResolver;
            m_InventoryData = inventoryData;
            m_Authenticator = authenticator ?? throw new ArgumentNullException(nameof(authenticator));
            m_Audit = audit ?? NullNexAuditSink.Instance;
        }

        public void Handle(IOSHttpRequest httpRequest, IOSHttpResponse httpResponse)
        {
            string[] segments = (httpRequest?.UriPath ?? string.Empty).Trim('/').Split('/');
            // /api/v1/profiles/{uuid} and profile-bound binary image:
            // /api/v1/profiles/{uuid}/image. Never accept arbitrary asset IDs.
            bool imageRequest = segments.Length == 5 &&
                segments[4].Equals("image", StringComparison.OrdinalIgnoreCase);
            if ((segments.Length != 4 && !imageRequest) ||
                !segments[0].Equals("api", StringComparison.OrdinalIgnoreCase) ||
                !segments[1].Equals("v1", StringComparison.OrdinalIgnoreCase) ||
                !segments[2].Equals("profiles", StringComparison.OrdinalIgnoreCase) ||
                !UUID.TryParse(segments[3], out UUID userId))
            {
                WriteJson(httpResponse, HttpStatusCode.NotFound, new { error = "profile_not_found" });
                return;
            }

            UserAccount account = m_Accounts.GetUserAccount(UUID.Zero, userId);
            if (account == null)
            {
                WriteJson(httpResponse, HttpStatusCode.NotFound, new { error = "profile_not_found" });
                return;
            }

            if (imageRequest)
            {
                if (string.Equals(httpRequest.HttpMethod, "GET", StringComparison.OrdinalIgnoreCase))
                    GetImage(account, httpRequest, httpResponse);
                else
                {
                    httpResponse.AddHeader("Allow", "GET");
                    WriteJson(httpResponse, HttpStatusCode.MethodNotAllowed, new { error = "method_not_allowed" });
                }
                return;
            }

            if (string.Equals(httpRequest.HttpMethod, "GET", StringComparison.OrdinalIgnoreCase))
            {
                Get(account, httpRequest, httpResponse);
                return;
            }

            if (string.Equals(httpRequest.HttpMethod, "PATCH", StringComparison.OrdinalIgnoreCase))
            {
                Patch(account, httpRequest.InputStream, httpRequest, httpResponse);
                return;
            }

            httpResponse.AddHeader("Allow", "GET, PATCH");
            WriteJson(httpResponse, HttpStatusCode.MethodNotAllowed, new { error = "method_not_allowed" });
        }

        private void Get(UserAccount account, IOSHttpRequest request, IOSHttpResponse response)
        {
            // Publication status and the linked picture come from userprofile.
            // Resolve and authorize them before reading optional userdata privacy.
            UserProfileProperties profile = new UserProfileProperties { UserId = account.PrincipalID };
            string error = string.Empty;
            if (!m_Profiles.GetAvatarProperties(ref profile, ref error))
            {
                m_Log.Error("[NEX-WEBPROFILE-V3]: Unable to load avatar userprofile.");
                WriteJson(response, HttpStatusCode.ServiceUnavailable,
                    new { error = "profile_service_unavailable" });
                return;
            }

            if (!profile.PublishProfile)
            {
                if (!m_Authenticator.TryAuthenticate(request, NexScopes.ProfileRead,
                        out NexPrincipal principal, out UserAccount _, out int status, out string authError))
                {
                    WriteJson(response, (HttpStatusCode)status, new { error = authError });
                    return;
                }
                if ((!UUID.TryParse(principal.Subject, out UUID subject) ||
                     subject != account.PrincipalID) &&
                    !principal.HasScope(NexScopes.AdminAll))
                {
                    WriteJson(response, HttpStatusCode.NotFound, new { error = "profile_not_found" });
                    return;
                }
            }

            ProfilePrivacy privacy = LoadPrivacy(account.PrincipalID);
            WriteJson(response, HttpStatusCode.OK, new
            {
                id = account.PrincipalID.ToString(),
                username = account.Username,
                display_name = account.EffectiveDisplayName,
                profile_image = profile.ImageId.ToString(),
                first_life_image = profile.FirstLifeImageId.ToString(),
                first_life_text = profile.FirstLifeText ?? string.Empty,
                profile_image_url = profile.ImageId.IsZero() || m_Assets == null
                    ? null : "/api/v1/profiles/" + account.PrincipalID + "/image",
                about = profile.AboutText ?? string.Empty,
                web_url = profile.WebUrl ?? string.Empty,
                interests = new
                {
                    want_to_mask = profile.WantToMask,
                    want_to_text = profile.WantToText ?? string.Empty,
                    skills_mask = profile.SkillsMask,
                    skills_text = profile.SkillsText ?? string.Empty,
                    language = profile.Language ?? string.Empty
                },
                partner_id = profile.PartnerId.IsZero() ? null : profile.PartnerId.ToString(),
                visibility = profile.PublishProfile ? "public" : "private",
                mature = profile.PublishMature,
                online_visibility = privacy.OnlineVisibility,
                groups_visibility = privacy.GroupsVisibility,
                search_visibility = privacy.SearchVisibility,
                picks = m_Profiles.GetAvatarPicks(account.PrincipalID),
                classifieds = m_Profiles.GetClassifiedRecords(account.PrincipalID)
            });
        }

        /// <summary>
        /// Only the avatar's *current profile picture* can be retrieved.
        /// OpenSim asset IDs alone never confer authority. Published profiles
        /// may be read anonymously; private pictures require owner/admin.
        /// In particular scripts, inventory textures and friends-only assets
        /// can never be enumerated through this route.
        /// </summary>
        private void GetImage(UserAccount account, IOSHttpRequest request,
            IOSHttpResponse response)
        {
            // The ROBUST AssetServiceConnector might initialize after this API.
            // Resolve the already configured local service on each image request,
            // rather than freezing a startup-time null and returning HTTP 503.
            IAssetService assets = m_AssetResolver?.Invoke() ?? m_Assets;
            if (assets == null)
            {
                m_Log.Warn("[NEX-WEBPROFILE-V3]: Profile image asset service is not initialized. Check the ROBUST [AssetService] connector and startup logs.");
                WriteJson(response, HttpStatusCode.ServiceUnavailable,
                    new { error = "profile_image_service_unavailable" });
                return;
            }

            UserProfileProperties profile =
                new UserProfileProperties { UserId = account.PrincipalID };
            string error = string.Empty;
            if (!m_Profiles.GetAvatarProperties(ref profile, ref error))
            {
                m_Log.Error("[NEX-WEBPROFILE-V3]: Failed to load avatar userprofile for image lookup.");
                WriteJson(response, HttpStatusCode.ServiceUnavailable,
                    new { error = "profile_image_service_unavailable" });
                return;
            }
            if (!profile.PublishProfile)
            {
                if (!m_Authenticator.TryAuthenticate(request, NexScopes.ProfileRead,
                    out NexPrincipal principal, out UserAccount _,
                    out int status, out string authError))
                {
                    WriteJson(response, (HttpStatusCode)status,
                        new { error = authError });
                    return;
                }
                if ((!UUID.TryParse(principal.Subject, out UUID subject) ||
                      subject != account.PrincipalID) &&
                    !principal.HasScope(NexScopes.AdminAll))
                {
                    WriteJson(response, HttpStatusCode.NotFound,
                        new { error = "profile_image_not_found" });
                    return;
                }
            }
            if (profile.ImageId.IsZero())
            {
                WriteJson(response, HttpStatusCode.NotFound,
                    new { error = "profile_image_not_found" });
                return;
            }

            try
            {
                // Asset origin, MIME metadata and the image extension from a
                // browser request are untrusted. Validate the actual bytes.
                AssetBase asset = assets.Get(profile.ImageId.ToString());
                if (asset == null)
                {
                    m_Log.Warn("[NEX-WEBPROFILE-V3]: Referenced profile image is missing from the asset provider.");
                    WriteJson(response, HttpStatusCode.NotFound,
                        new { error = "profile_image_not_found" });
                    return;
                }
                if (asset.Data == null || asset.Data.Length == 0)
                {
                    m_Log.Warn("[NEX-WEBPROFILE-V3]: Referenced profile image has no readable payload in the asset provider.");
                    WriteJson(response, HttpStatusCode.NotFound,
                        new { error = "profile_image_not_found" });
                    return;
                }
                if (asset.Data.Length > MaxProfileAssetBytes ||
                    (asset.Type != (sbyte)AssetType.Texture &&
                     asset.Type != (sbyte)AssetType.ImageJPEG &&
                     asset.Type != (sbyte)AssetType.TextureTGA &&
                     asset.Type != (sbyte)AssetType.ImageTGA))
                {
                    m_Log.Warn("[NEX-WEBPROFILE-V3]: Referenced profile image exceeds the size limit or has an unsupported asset type.");
                    WriteJson(response, HttpStatusCode.NotFound,
                        new { error = "profile_image_not_found" });
                    return;
                }
                // The FSAssets SQL schema does not carry CreatorID. Do not
                // fail every legitimate FSAsset texture or simply trust a
                // profile-supplied UUID: verify same-owner inventory evidence
                // for this exact asset. A creator-owned inventory upload is
                // allowed; third-party content must explicitly permit export.
                bool hasAssetCreator = UUID.TryParse(asset.CreatorID, out UUID creator);
                bool creatorMatches = hasAssetCreator &&
                    creator == account.PrincipalID;
                if (!creatorMatches)
                {
                    if (m_InventoryData == null)
                    {
                        m_Log.Warn("[NEX-WEBPROFILE-V3]: Profile image inventory provenance service unavailable.");
                        WriteJson(response, HttpStatusCode.ServiceUnavailable,
                            new { error = "profile_image_service_unavailable" });
                        return;
                    }

                    XInventoryItem[] candidates;
                    try
                    {
                        candidates = m_InventoryData.GetItems(
                            new[] { "avatarID", "assetID" },
                            new[] { account.PrincipalID.ToString(), profile.ImageId.ToString() });
                    }
                    catch (Exception e)
                    {
                        m_Log.Error("[NEX-WEBPROFILE-V3]: Profile image inventory provenance lookup failed.", e);
                        WriteJson(response, HttpStatusCode.ServiceUnavailable,
                            new { error = "profile_image_service_unavailable" });
                        return;
                    }

                    const int requiredExportPermissions =
                        (int)(PermissionMask.Copy | PermissionMask.Transfer | PermissionMask.Export);
                    bool permitted = false;
                    foreach (XInventoryItem item in candidates ?? Array.Empty<XInventoryItem>())
                    {
                        if (item == null ||
                            item.avatarID != account.PrincipalID ||
                            item.assetID != profile.ImageId ||
                            item.assetType != (int)asset.Type)
                            continue;

                        bool uploadedByOwner = !hasAssetCreator &&
                            UUID.TryParse(item.creatorID, out UUID itemCreator) &&
                            itemCreator == account.PrincipalID;
                        bool exportAllowed =
                            (item.inventoryCurrentPermissions & requiredExportPermissions) ==
                            requiredExportPermissions;
                        if (uploadedByOwner || exportAllowed)
                        {
                            permitted = true;
                            break;
                        }
                    }

                    if (!permitted)
                    {
                        m_Log.Warn("[NEX-WEBPROFILE-V3]: Profile image not publishable: owner inventory proof or export permission missing.");
                        WriteJson(response, HttpStatusCode.NotFound,
                            new { error = "profile_image_not_found" });
                        return;
                    }
                }
                byte[] raw = asset.Data;
                byte[] result;
                string mime;
                if (raw.Length >= 8 && raw[0] == 0x89 && raw[1] == 0x50 &&
                    raw[2] == 0x4e && raw[3] == 0x47 &&
                    raw[4] == 0x0d && raw[5] == 0x0a &&
                    raw[6] == 0x1a && raw[7] == 0x0a)
                {
                    result = raw;
                    mime = "image/png";
                }
                else if (raw.Length >= 3 && raw[0] == 0xff &&
                         raw[1] == 0xd8 && raw[2] == 0xff)
                {
                    result = raw;
                    mime = "image/jpeg";
                }
                else
                {
                    // OpenSim profile textures are commonly JPEG2000. Decode
                    // to a browser-supported image; do not forward raw J2K.
                    ManagedImage managedImage;
                    Image image;
                    if (!OpenJPEG.DecodeToImage(raw, out managedImage, out image) ||
                        image == null)
                    {
                        WriteJson(response, HttpStatusCode.UnsupportedMediaType,
                            new { error = "profile_image_decode_failed" });
                        return;
                    }
                    using (image)
                    {
                        if (image.Width <= 0 || image.Height <= 0 ||
                            image.Width > MaxProfileImageWidth ||
                            image.Height > MaxProfileImageWidth)
                        {
                            WriteJson(response, HttpStatusCode.RequestEntityTooLarge,
                                new { error = "profile_image_too_large" });
                            return;
                        }
                        using MemoryStream buffer = new MemoryStream();
                        image.Save(buffer, ImageFormat.Png);
                        if (buffer.Length > MaxProfileAssetBytes)
                        {
                            WriteJson(response, HttpStatusCode.RequestEntityTooLarge,
                                new { error = "profile_image_too_large" });
                            return;
                        }
                        result = buffer.ToArray();
                        mime = "image/png";
                    }
                }
                response.StatusCode = (int)HttpStatusCode.OK;
                response.ContentType = mime;
                response.AddHeader("X-Content-Type-Options", "nosniff");
                // Conservative cache policy: an avatar may revoke public
                // visibility immediately without old CDN/profile copies.
                response.AddHeader("Cache-Control", "private, no-store");
                response.KeepAlive = false;
                response.RawBuffer = result;
            }
            catch (Exception e)
            {
                // Keep backend details server-side; the client sees only a controlled error.
                m_Log.Error("[NEX-WEBPROFILE-V3]: Profile image loading or conversion failed.", e);
                WriteJson(response, HttpStatusCode.ServiceUnavailable,
                    new { error = "profile_image_service_unavailable" });
            }
        }

        private void Patch(
            UserAccount account,
            Stream body,
            IOSHttpRequest request,
            IOSHttpResponse response)
        {
            if (!m_Authenticator.TryAuthenticate(
                    request, NexScopes.ProfileWrite,
                    out NexPrincipal principal, out UserAccount _,
                    out int status, out string authError))
            {
                WriteJson(response, (HttpStatusCode)status, new { error = authError });
                return;
            }

            bool self = UUID.TryParse(principal.Subject, out UUID subjectId) &&
                subjectId == account.PrincipalID;
            if (!self && !principal.HasScope(NexScopes.AdminAll))
            {
                WriteJson(response, HttpStatusCode.Forbidden, new { error = "profile_owner_required" });
                return;
            }

            UserProfileProperties profile =
                new UserProfileProperties { UserId = account.PrincipalID };
            string storeError = string.Empty;
            if (!m_Profiles.GetAvatarProperties(ref profile, ref storeError))
            {
                WriteJson(response, HttpStatusCode.ServiceUnavailable,
                    new { error = "profile_service_unavailable" });
                return;
            }
            UUID oldPartnerId = profile.PartnerId;
            var columns = new HashSet<string>(StringComparer.Ordinal);

            try
            {
                using JsonDocument doc = JsonDocument.Parse(body);
                JsonElement root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object)
                {
                    WriteJson(response, HttpStatusCode.BadRequest,
                        new { error = "invalid_profile_patch" });
                    return;
                }

                foreach (JsonProperty property in root.EnumerateObject())
                {
                    if (property.Name == "interests")
                    {
                        if (property.Value.ValueKind != JsonValueKind.Object)
                        {
                            WriteJson(response, HttpStatusCode.BadRequest,
                                new { error = "invalid_profile_field_value" });
                            return;
                        }
                        foreach (JsonProperty nested in property.Value.EnumerateObject())
                        {
                            if (!TrySetPortalProfileField(
                                    nested.Name, nested.Value, profile, columns,
                                    out string nestedError))
                            {
                                WriteJson(response,
                                    nestedError == "profile_field_readonly"
                                        ? HttpStatusCode.Forbidden : HttpStatusCode.BadRequest,
                                    new { error = nestedError, field = nested.Name });
                                return;
                            }
                        }
                        continue;
                    }

                    if (!TrySetPortalProfileField(
                            property.Name, property.Value, profile, columns,
                            out string fieldError))
                    {
                        WriteJson(response,
                            fieldError == "profile_field_readonly"
                                ? HttpStatusCode.Forbidden : HttpStatusCode.BadRequest,
                            new { error = fieldError, field = property.Name });
                        return;
                    }
                }
            }
            catch (JsonException)
            {
                WriteJson(response, HttpStatusCode.BadRequest, new { error = "invalid_json" });
                return;
            }

            if (columns.Count == 0)
            {
                WriteJson(response, HttpStatusCode.BadRequest,
                    new { error = "empty_profile_patch" });
                return;
            }

            // The datastore updates only the selected, whitelisted columns.
            // Never call UpdateAvatarProperties: it also writes profileImage
            // and profileFirstImage even if they are absent from the request.
            if (!m_Profiles.UpdateAvatarPortalProperties(profile, columns, ref storeError))
            {
                m_Log.Error("[NEX-WEBPROFILE-V3]: Portal profile field update failed: " + storeError);
                WriteJson(response, HttpStatusCode.InternalServerError,
                    new { error = "profile_update_failed" });
                return;
            }

            if (oldPartnerId != profile.PartnerId)
            {
                m_Audit.Record(new NexAuditEvent(
                    principal.Subject,
                    "relationship.partner.update",
                    "relationship:" + account.PrincipalID,
                    details: new Dictionary<string, string>
                    {
                        ["owner_id"] = account.PrincipalID.ToString(),
                        ["old_partner_id"] = oldPartnerId.IsZero() ? string.Empty : oldPartnerId.ToString(),
                        ["partner_id"] = profile.PartnerId.IsZero() ? string.Empty : profile.PartnerId.ToString()
                    }));
            }

            Get(account, request, response);
        }

        private bool TrySetPortalProfileField(
            string name, JsonElement value, UserProfileProperties profile,
            HashSet<string> columns, out string error)
        {
            error = "invalid_profile_field_value";
            string column;
            switch (name)
            {
                case "profilePartner":
                case "partner_id":
                    column = "profilePartner";
                    if (value.ValueKind != JsonValueKind.Null &&
                        value.ValueKind != JsonValueKind.String)
                        return false;
                    string partnerRaw = value.ValueKind == JsonValueKind.Null
                        ? string.Empty : value.GetString();
                    if (string.IsNullOrWhiteSpace(partnerRaw))
                        profile.PartnerId = UUID.Zero;
                    else if (UUID.TryParse(partnerRaw, out UUID partnerId) &&
                             (partnerId == UUID.Zero ||
                              (partnerId != profile.UserId &&
                               m_Accounts.GetUserAccount(UUID.Zero, partnerId) != null)))
                        profile.PartnerId = partnerId;
                    else
                        return false;
                    break;
                case "profileAllowPublish":
                    column = "profileAllowPublish";
                    if (!TryReadProfileBool(value, out bool pub))
                        return false;
                    profile.PublishProfile = pub;
                    break;
                case "visibility":
                    column = "profileAllowPublish";
                    if (value.ValueKind != JsonValueKind.String)
                        return false;
                    string visibility = value.GetString();
                    if (visibility != "public" && visibility != "private")
                        return false;
                    profile.PublishProfile = visibility == "public";
                    break;
                case "profileMaturePublish":
                case "mature":
                    column = "profileMaturePublish";
                    if (!TryReadProfileBool(value, out bool mature))
                        return false;
                    profile.PublishMature = mature;
                    break;
                case "profileURL":
                case "web_url":
                    column = "profileURL";
                    if (!TryReadProfileString(value, 255, out string url))
                        return false;
                    profile.WebUrl = url;
                    break;
                case "profileWantToMask":
                case "want_to_mask":
                    column = "profileWantToMask";
                    if (!value.TryGetInt32(out int wantMask))
                        return false;
                    profile.WantToMask = wantMask;
                    break;
                case "profileWantToText":
                case "want_to_text":
                    column = "profileWantToText";
                    if (!TryReadProfileString(value, 510, out string wantText))
                        return false;
                    profile.WantToText = wantText;
                    break;
                case "profileSkillsMask":
                case "skills_mask":
                    column = "profileSkillsMask";
                    if (!value.TryGetInt32(out int skillsMask))
                        return false;
                    profile.SkillsMask = skillsMask;
                    break;
                case "profileSkillsText":
                case "skills_text":
                    column = "profileSkillsText";
                    if (!TryReadProfileString(value, 510, out string skillsText))
                        return false;
                    profile.SkillsText = skillsText;
                    break;
                case "profileLanguages":
                case "language":
                    column = "profileLanguages";
                    if (!TryReadProfileString(value, 255, out string language))
                        return false;
                    profile.Language = language;
                    break;
                case "profileAboutText":
                case "about":
                    column = "profileAboutText";
                    if (!TryReadProfileString(value, 510, out string about))
                        return false;
                    profile.AboutText = about;
                    break;
                case "profileFirstText":
                case "first_life_text":
                    column = "profileFirstText";
                    if (!TryReadProfileString(value, 510, out string firstLifeText))
                        return false;
                    profile.FirstLifeText = firstLifeText;
                    break;
                default:
                    // Includes useruuid, profileImage, profileFirstImage,
                    // their JSON aliases and any non-userprofile fields.
                    error = "profile_field_readonly";
                    return false;
            }

            if (!columns.Add(column))
            {
                error = "duplicate_profile_field";
                return false;
            }
            error = string.Empty;
            return true;
        }

        private static bool TryReadProfileBool(JsonElement value, out bool parsed)
        {
            parsed = false;
            if (value.ValueKind != JsonValueKind.True &&
                value.ValueKind != JsonValueKind.False)
                return false;
            parsed = value.GetBoolean();
            return true;
        }

        private static bool TryReadProfileString(JsonElement value, int maxLength, out string parsed)
        {
            parsed = string.Empty;
            if (value.ValueKind != JsonValueKind.String)
                return false;
            string s = value.GetString() ?? string.Empty;
            if (s.Length > maxLength)
                return false;
            parsed = s.Trim();
            return true;
        }

        private sealed class ProfilePrivacy
        {
            public string OnlineVisibility { get; set; } = "friends";
            public string GroupsVisibility { get; set; } = "public";
            public string SearchVisibility { get; set; } = "public";
        }

        private ProfilePrivacy LoadPrivacy(UUID userId)
        {
            UserAppData data = new UserAppData { UserId = userId.ToString(), TagId = "NexVerse.WebProfileV3", DataKey = "privacy" };
            string error = string.Empty;
            if (!m_Profiles.GetUserAppData(ref data, ref error) || string.IsNullOrWhiteSpace(data.DataVal))
                return new ProfilePrivacy();
            try { return JsonSerializer.Deserialize<ProfilePrivacy>(data.DataVal) ?? new ProfilePrivacy(); }
            catch { return new ProfilePrivacy(); }
        }

        private bool SavePrivacy(UUID userId, ProfilePrivacy privacy, ref string error)
        {
            UserAppData data = new UserAppData
            {
                UserId = userId.ToString(),
                TagId = "NexVerse.WebProfileV3",
                DataKey = "privacy",
                DataVal = JsonSerializer.Serialize(privacy)
            };
            return m_Profiles.SetUserAppData(data, ref error);
        }

        private static bool ReadVisibility(JsonElement root, string name, ref string target)
        {
            if (!root.TryGetProperty(name, out JsonElement value) || value.ValueKind != JsonValueKind.String) return false;
            string next = (value.GetString() ?? string.Empty).Trim().ToLowerInvariant();
            if (next != "public" && next != "friends" && next != "private") return false;
            if (target == next) return false;
            target = next;
            return true;
        }

        private static string Limit(string value, int max)
        {
            value = (value ?? string.Empty).Trim();
            return value.Length <= max ? value : value.Substring(0, max);
        }

        private static void WriteJson(IOSHttpResponse response, HttpStatusCode status, object payload)
        {
            response.StatusCode = (int)status;
            response.ContentType = "application/json; charset=utf-8";
            response.AddHeader("Cache-Control", "no-store");
            byte[] bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload));
            response.KeepAlive = false;
            response.AddHeader("X-Content-Type-Options", "nosniff");
            response.RawBuffer = bytes;
        }
    }
}

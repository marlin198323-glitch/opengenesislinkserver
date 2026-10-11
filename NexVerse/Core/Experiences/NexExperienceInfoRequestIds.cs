// SPDX-License-Identifier: MPL-2.0

using System;

namespace NexVerse.Core.Experiences
{
    /// <summary>
    /// Parse Firestorm's GetExperienceInfo URL without confusing the random
    /// viewer-session capability with the appended Experience identifier.
    /// No path outside that exact CAP can retrieve an Experience.
    /// </summary>
    public static class NexExperienceInfoRequestIds
    {
        public static bool TryParse(string requestPath, string capPath,
            string[] queryIds, out Guid[] ids)
        {
            ids = Array.Empty<Guid>();
            if (string.IsNullOrWhiteSpace(capPath) ||
                string.IsNullOrWhiteSpace(requestPath))
                return false;

            string root = capPath.TrimEnd('/');
            string path = requestPath.TrimEnd('/');
            if (root.Length == 0 || root[0] != '/')
                return false;

            // Legacy GET /cap?public_id=uuid&public_id=uuid
            if (string.Equals(path, root, StringComparison.Ordinal))
            {
                if (queryIds == null || queryIds.Length == 0 ||
                    queryIds.Length > 64)
                    return false;

                Guid[] parsed = new Guid[queryIds.Length];
                for (int i = 0; i < queryIds.Length; i++)
                {
                    if (!Guid.TryParseExact(queryIds[i], "D", out Guid id) ||
                        id == Guid.Empty)
                        return false;
                    parsed[i] = id;
                }

                ids = parsed;
                return true;
            }

            // Never allow both input forms or a path belonging to another CAP.
            if (queryIds != null && queryIds.Length != 0 ||
                !path.StartsWith(root + "/", StringComparison.Ordinal))
                return false;

            // Firestorm /cap/id/uuid[/] and compatible /cap/uuid[/].
            string suffix = path.Substring(root.Length + 1);
            if (suffix.StartsWith("id/", StringComparison.OrdinalIgnoreCase))
                suffix = suffix.Substring(3);

            if (!Guid.TryParseExact(suffix, "D", out Guid experience) ||
                experience == Guid.Empty)
                return false;

            ids = new[] { experience };
            return true;
        }
    }

    /// <summary>
    /// Only Firestorm PRIVATE and DISABLED may be edited by the resident.
    /// Never silently discard GRID, PRIVILEGED, SUSPENDED, INVALID or unknown
    /// flags: accepting them would claim permissions the server did not grant.
    /// </summary>
    public static class NexExperienceViewerFlags
    {
        public const int Private = 1 << 5;
        public const int Disabled = 1 << 6;
        public const int EditableMask = Private | Disabled;

        public static bool TryDecode(int flags, out bool enabled,
            out bool isPrivate, out int rejectedFlags)
        {
            rejectedFlags = flags & ~EditableMask;
            enabled = (flags & Disabled) == 0;
            isPrivate = (flags & Private) != 0;
            return rejectedFlags == 0;
        }

        public static int Encode(bool enabled, bool isPrivate)
        {
            return (enabled ? 0 : Disabled) | (isPrivate ? Private : 0);
        }
    }
}

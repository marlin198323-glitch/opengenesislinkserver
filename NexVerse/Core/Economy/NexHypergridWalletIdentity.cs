// SPDX-License-Identifier: MPL-2.0
using System;
using System.Security.Cryptography;
using System.Text;

namespace NexVerse.Core.Economy
{
    /// <summary>
    /// Stable, domain-separated NV$ account identity for an authenticated
    /// Hypergrid session. A home URI is an identity input, not an authorization
    /// proof and never triggers a network fetch.
    /// </summary>
    public static class NexHypergridWalletIdentity
    {
        public static string CanonicalHome(string verifiedHomeUri)
        {
            if (!Uri.TryCreate(verifiedHomeUri, UriKind.Absolute, out Uri uri) ||
                (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp) ||
                !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) ||
                !string.IsNullOrEmpty(uri.Fragment) ||
                (uri.AbsolutePath.TrimEnd('/').Length != 0) ||
                uri.Host.Length == 0)
                throw new ArgumentException(
                    "Verified HG home must be a canonical HTTP(S) origin.",
                    nameof(verifiedHomeUri));

            return uri.GetLeftPart(UriPartial.Authority).TrimEnd('/');
        }

        public static string Reference(string verifiedHomeUri, Guid originalAvatarId)
        {
            if (originalAvatarId == Guid.Empty)
                throw new ArgumentException("Original HG avatar UUID is required.",
                    nameof(originalAvatarId));

            string material = "opengenesislink:nvd:hypergrid:wallet:v1\n" +
                CanonicalHome(verifiedHomeUri) + "\n" +
                originalAvatarId.ToString("D");
            return "hg:" + Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(material))).ToLowerInvariant();
        }

        public static Guid AccountId(string verifiedHomeUri, Guid originalAvatarId)
        {
            string reference = Reference(verifiedHomeUri, originalAvatarId);
            // 128 bits of the domain-separated SHA256 digest. Set RFC 4122
            // version/variant bits for a stable name-derived UUID.
            byte[] bytes = Convert.FromHexString(reference.Substring(3, 32));
            bytes[7] = (byte)((bytes[7] & 0x0F) | 0x50);
            bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
            return new Guid(bytes);
        }
    }
}

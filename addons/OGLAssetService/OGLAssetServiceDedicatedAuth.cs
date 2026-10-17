using System;
using System.Collections.Specialized;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using OpenSim.Framework.ServiceAuth;

namespace OpenGenesisLINK.Addons.AssetService
{
    /// <summary>
    /// Dedicated admin Basic credential verifier; never inherits [Network]
    /// credentials or a shared ROBUST service authentication policy.
    /// </summary>
    public sealed class OGLAssetServiceDedicatedAuth : IServiceAuth
    {
        private readonly byte[] expected;
        public string Name => "OGLAdminBasic";

        public OGLAssetServiceDedicatedAuth(string username, string password)
        {
            if (String.IsNullOrWhiteSpace(username) || username.Contains(":") ||
                String.IsNullOrWhiteSpace(password))
                throw new ArgumentException("Dedicated admin credentials required.");
            expected = SHA256.HashData(Encoding.UTF8.GetBytes(username + ":" + password));
        }

        public bool Authenticate(string data)
        {
            if (String.IsNullOrEmpty(data) || data.Length > 8192) return false;
            try
            {
                byte[] decoded = Convert.FromBase64String(data);
                byte[] digest = SHA256.HashData(decoded);
                bool valid = CryptographicOperations.FixedTimeEquals(expected, digest);
                CryptographicOperations.ZeroMemory(decoded);
                CryptographicOperations.ZeroMemory(digest);
                return valid;
            }
            catch (FormatException) { return false; }
        }

        public bool Authenticate(NameValueCollection headers,
            AddHeaderDelegate addHeader, out HttpStatusCode statusCode)
        {
            string authorization = headers?["Authorization"];
            bool valid = authorization != null &&
                authorization.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase) &&
                Authenticate(authorization.Substring(6).Trim());
            statusCode = valid ? HttpStatusCode.OK : HttpStatusCode.Unauthorized;
            if (!valid) addHeader?.Invoke("WWW-Authenticate", "Basic realm=\"OGL AssetService Admin\"");
            return valid;
        }

        // This authenticator is inbound-only: never emit its credentials.
        public void AddAuthorization(NameValueCollection headers)
            => throw new NotSupportedException("Inbound authentication only.");
        public void AddAuthorization(HttpRequestHeaders headers)
            => throw new NotSupportedException("Inbound authentication only.");
    }
}

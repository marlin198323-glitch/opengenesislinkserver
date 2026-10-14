// SPDX-License-Identifier: MPL-2.0
using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace OpenSim.Framework
{
    /// <summary>
    /// Grid-controlled permission gate for all OAR/IAR entry points.
    /// Disabled for legacy installations; when enabled, expired/missing policy DENIES.
    /// Policies are ES256 signed by Robust. Simulators hold verification keys only.
    /// </summary>
    public static class OglArchiveFeatureGate
    {
        public const int OarImport = 1;
        public const int OarExport = 2;
        public const int IarImport = 4;
        public const int IarExport = 8;
        public const int All = OarImport | OarExport | IarImport | IarExport;
        public const string Support = "info@stadt-nexverse.de";

        private static readonly object s_Sync = new object();
        private static bool s_Enabled;
        private static string s_NodeId = "";
        private static string s_PublicKey = "";
        private static long s_Revision = -1;
        private static long s_Expires;
        private static int s_Allowed;

        public static bool Enabled { get { lock (s_Sync) return s_Enabled; } }

        public static void Configure(bool enabled, string nodeId, string publicKeyPem)
        {
            lock (s_Sync)
            {
                // Always invalidate the previous snapshot when configuration changes.
                s_Enabled = enabled;
                s_NodeId = nodeId?.Trim() ?? "";
                s_PublicKey = publicKeyPem ?? "";
                s_Revision = -1;
                s_Expires = 0;
                s_Allowed = 0;
            }
        }

        private static string Payload(string node, long revision, long expires, int flags)
            => "OGL-FEATURE-V1\n" + node + "\n" +
               revision.ToString(CultureInfo.InvariantCulture) + "\n" +
               expires.ToString(CultureInfo.InvariantCulture) + "\n" +
               flags.ToString(CultureInfo.InvariantCulture);

        public static string Sign(string privateKeyPem, string node, long revision, long expires, int flags)
        {
            if (string.IsNullOrWhiteSpace(privateKeyPem))
                throw new ArgumentException("Robust benötigt einen privaten ES256-Signaturschlüssel.");
            using ECDsa signer = ECDsa.Create();
            signer.ImportFromPem(privateKeyPem);
            return Convert.ToHexString(signer.SignData(
                Encoding.UTF8.GetBytes(Payload(node, revision, expires, flags)),
                HashAlgorithmName.SHA256)).ToLowerInvariant();
        }

        public static bool Apply(string node, long revision, long expires, int flags, string signature)
        {
            lock (s_Sync)
            {
                if (!s_Enabled || s_NodeId.Length == 0 || string.IsNullOrWhiteSpace(s_PublicKey) ||
                    !string.Equals(node, s_NodeId, StringComparison.Ordinal) ||
                    revision < s_Revision || revision < 0 || (flags & ~All) != 0 ||
                    expires <= DateTimeOffset.UtcNow.ToUnixTimeSeconds() ||
                    expires > DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 600)
                    return false;
                try
                {
                    using ECDsa verifier = ECDsa.Create();
                    verifier.ImportFromPem(s_PublicKey);
                    byte[] actual = Convert.FromHexString(signature ?? "");
                    if (!verifier.VerifyData(
                            Encoding.UTF8.GetBytes(Payload(node, revision, expires, flags)),
                            actual, HashAlgorithmName.SHA256))
                        return false;
                }
                catch { return false; }

                // Same revision is allowed for heartbeat renewals from Robust.
                s_Revision = revision;
                s_Expires = expires;
                s_Allowed = flags;
                return true;
            }
        }

        public static bool Allowed(int capability)
        {
            lock (s_Sync)
            {
                if (!s_Enabled) return true;  // backwards-compatible opt-in
                return s_Revision >= 0 &&
                    s_Expires > DateTimeOffset.UtcNow.ToUnixTimeSeconds() &&
                    capability != 0 && (capability & ~All) == 0 &&
                    (s_Allowed & capability) == capability;
            }
        }

        public static string DeniedMessage(int capability)
            => "[OGL-LIC-403] Für diesen Simulator liegt keine gültige Freigabe für " +
                (capability == OarImport ? "OAR-Import" :
                 capability == OarExport ? "OAR-Export" :
                 capability == IarImport ? "IAR-Import" : "IAR-Export") +
                " vor. Bitte wenden Sie sich an " + Support + ".";

        public static void Require(int capability)
        {
            if (!Allowed(capability))
                throw new InvalidOperationException(DeniedMessage(capability));
        }
    }
}

// SPDX-License-Identifier: MPL-2.0

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace NexVerse.Core.Security
{
    public sealed class NexApiKeyRecord
    {
        public string KeyId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string[] Scopes { get; set; } = Array.Empty<string>();
        public string SecretSalt { get; set; } = string.Empty;
        public string SecretHash { get; set; } = string.Empty;
        public bool Enabled { get; set; } = true;
        public long CreatedAt { get; set; }
        public long UpdatedAt { get; set; }
        public long LastUsedAt { get; set; }
    }

    public sealed class NexApiKeyRegistration
    {
        public NexApiKeyRecord Record { get; }
        public string ApiKey { get; }

        public NexApiKeyRegistration(
            NexApiKeyRecord record,
            string apiKey)
        {
            Record = record;
            ApiKey = apiKey ?? string.Empty;
        }
    }

    public interface INexApiKeyStore
    {
        NexApiKeyRegistration Create(
            string name,
            IEnumerable<string> scopes);

        IReadOnlyList<NexApiKeyRecord> List();
        IReadOnlyCollection<string> SupportedScopes { get; }
        // Compare-and-set replaces grants on an existing credential without
        // rotating the secret, and rejects stale admin edits.
        bool UpdateScopes(string keyId, IEnumerable<string> scopes,
            IEnumerable<string> expectedScopes);
        bool SetEnabled(string keyId, bool enabled);
        bool Delete(string keyId);
        bool TryValidate(
            string apiKey,
            out NexApiKeyRecord record);
    }

    public sealed class PersistentNexApiKeyStore : INexApiKeyStore
    {
        private const int SecretIterations = 120000;

        private static readonly HashSet<string> s_SupportedScopes =
            new HashSet<string>(
                new[]
                {
                    NexScopes.UsersRead,
                    NexScopes.UsersWrite,
                    NexScopes.UsersRegister,
                    NexScopes.InventoryRead,
                    NexScopes.InventoryWrite,
                    NexScopes.FriendsManage,
                    NexScopes.RegionsRead,
                    NexScopes.RegionsManage,
                    NexScopes.SimulatorsRead,
                    NexScopes.SimulatorsManage,
                    NexScopes.StatisticsRead,
                    NexScopes.EstatesRead,
                    NexScopes.EstatesManage,
                    NexScopes.EconomyRead,
                    NexScopes.EconomyTransfer,
                    // Viewer simulator backend may read group-owned land
                    // only via an explicitly granted groups:read machine key.
                    NexScopes.GroupsRead,
                    NexScopes.ExperiencesScript,
                    // Separate trusted simulator CAP transport. This is
                    // deliberately NOT experiences:manage or admin:*.
                    NexScopes.ExperiencesViewerPermissions,
                    // Firestorm Experience acquisition / owner-only edits
                    // require a separate privileged simulator credential.
                    NexScopes.ExperiencesViewerManage
                },
                StringComparer.OrdinalIgnoreCase);

        public IReadOnlyCollection<string> SupportedScopes =>
            s_SupportedScopes
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToArray();

        private readonly object m_Sync = new object();
        private readonly string m_Path;
        private readonly Mutex m_CrossProcessMutex;
        private StoreDocument m_Document;

        // A World API process may serve an API key that was edited by a
        // different ROBUST instance. An instance-local lock or initial Load()
        // alone is insufficient: a successful authentication could then
        // persist its stale copy, undoing freshly granted scopes.
        private sealed class StoreLease : IDisposable
        {
            private readonly Mutex m_Mutex;
            private readonly FileStream m_File;
            public StoreLease(Mutex mutex, FileStream file)
            {
                m_Mutex = mutex;
                m_File = file;
            }

            public void Dispose()
            {
                try
                {
                    m_File.Unlock(0, 1);
                }
                finally
                {
                    try
                    {
                        m_File.Dispose();
                    }
                    finally
                    {
                        m_Mutex.ReleaseMutex();
                    }
                }
            }
        }

        /// <summary>
        /// Lock all key-store operations across instances on the same host,
        /// acquire an advisory lock alongside the JSON file (for shared
        /// filesystems that support it), then load the latest on-disk grants.
        /// Never return a cached document from a different request.
        /// </summary>
        private StoreLease OpenLatestLocked()
        {
            bool ownsMutex = false;
            FileStream file = null;
            bool fileLocked = false;
            try
            {
                try
                {
                    ownsMutex = m_CrossProcessMutex.WaitOne(
                        TimeSpan.FromSeconds(15));
                }
                catch (AbandonedMutexException)
                {
                    ownsMutex = true;
                }

                if (!ownsMutex)
                    throw new IOException(
                        "Timed out waiting for the API key store update lock.");

                string directory = Path.GetDirectoryName(m_Path);
                if (!string.IsNullOrWhiteSpace(directory))
                    Directory.CreateDirectory(directory);

                file = new FileStream(m_Path + ".lock",
                    FileMode.OpenOrCreate, FileAccess.ReadWrite,
                    FileShare.ReadWrite);
                Stopwatch watch = Stopwatch.StartNew();
                while (true)
                {
                    try
                    {
                        file.Lock(0, 1);
                        fileLocked = true;
                        break;
                    }
                    catch (IOException) when (watch.Elapsed <
                        TimeSpan.FromSeconds(15))
                    {
                        Thread.Sleep(25);
                    }
                }

                m_Document = Load() ?? new StoreDocument();
                m_Document.Keys ??= new List<NexApiKeyRecord>();
                return new StoreLease(m_CrossProcessMutex, file);
            }
            catch
            {
                try
                {
                    if (fileLocked)
                        file?.Unlock(0, 1);
                }
                finally
                {
                    file?.Dispose();
                    if (ownsMutex)
                        m_CrossProcessMutex.ReleaseMutex();
                }
                throw;
            }
        }


        public PersistentNexApiKeyStore(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException(
                    "API key store path is required.",
                    nameof(path));

            m_Path = Path.GetFullPath(path);
            // Stable per-store name. A named mutex also serializes multiple
            // PersistentNexApiKeyStore instances inside the same process.
            string mutexName = "ogl-worldapi-apikeys-" +
                Convert.ToHexString(SHA256.HashData(
                    Encoding.UTF8.GetBytes(m_Path))).ToLowerInvariant();
            m_CrossProcessMutex = new Mutex(false, mutexName);
            lock (m_Sync)
            {
                using StoreLease lease = OpenLatestLocked();
            }
        }

        public NexApiKeyRegistration Create(
            string name,
            IEnumerable<string> scopes)
        {
            string[] normalizedScopes = NormalizeScopes(scopes);
            if (normalizedScopes.Length == 0)
                throw new ArgumentException(
                    "At least one API key scope is required.",
                    nameof(scopes));

            ValidateScopes(normalizedScopes);

            string normalizedName = string.IsNullOrWhiteSpace(name)
                ? "NexVerse API key" : name.Trim();
            if (normalizedName.Length > 120)
                throw new ArgumentException("API key name is too long.", nameof(name));

            string keyId =
                "nxk_" + Guid.NewGuid().ToString("N");
            string secret =
                Base64UrlEncode(
                    RandomNumberGenerator.GetBytes(48));

            HashSecret(
                secret,
                out string salt,
                out string hash);

            long now =
                DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            NexApiKeyRecord record = new NexApiKeyRecord
            {
                KeyId = keyId,
                Name = normalizedName,
                Scopes = normalizedScopes,
                SecretSalt = salt,
                SecretHash = hash,
                Enabled = true,
                CreatedAt = now,
                UpdatedAt = now
            };

            lock (m_Sync)
            {
                using StoreLease lease = OpenLatestLocked();
                // A secret is only returned once. Accidentally recreating
                // an already active *identical service identity* leaves
                // orphaned credentials. Require explicit disable/rotation.
                if (m_Document.Keys.Any(existing =>
                        existing.Enabled &&
                        string.Equals(existing.Name?.Trim(), normalizedName,
                            StringComparison.OrdinalIgnoreCase) &&
                        SameScopes(existing.Scopes, normalizedScopes)))
                    throw new InvalidOperationException(
                        "An enabled API key with this name and these scopes already exists. " +
                        "Review and explicitly disable the old key before replacing it.");

                m_Document.Keys.Add(record);
                SaveLocked();
            }

            return new NexApiKeyRegistration(
                Clone(record),
                keyId + "." + secret);
        }

        public IReadOnlyList<NexApiKeyRecord> List()
        {
            lock (m_Sync)
            {
                using StoreLease lease = OpenLatestLocked();
                return m_Document.Keys
                    .Select(Clone)
                    .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(x => x.KeyId, StringComparer.Ordinal)
                    .ToArray();
            }
        }

        /// <summary>
        /// Edit an existing credential's grants in place. The token ID, salt
        /// and PBKDF2 hash must remain unchanged. Both the expected scopes and
        /// the new scopes are normalized so clients can use the GET inventory
        /// as an optimistic-concurrency token without exposing any secret.
        /// </summary>
        public bool UpdateScopes(
            string keyId,
            IEnumerable<string> scopes,
            IEnumerable<string> expectedScopes)
        {
            if (string.IsNullOrWhiteSpace(keyId) || expectedScopes == null)
                return false;

            string[] desired = NormalizeScopes(scopes);
            ValidateScopes(desired);
            string[] expected = NormalizeScopes(expectedScopes);

            lock (m_Sync)
            {
                using StoreLease lease = OpenLatestLocked();
                NexApiKeyRecord record = m_Document.Keys.FirstOrDefault(x =>
                    string.Equals(x.KeyId, keyId, StringComparison.Ordinal));

                if (record == null || !SameScopes(record.Scopes, expected))
                    return false;

                // Do not allow two simultaneously enabled credentials to
                // acquire identical name+scope identities, matching Create.
                if (record.Enabled && m_Document.Keys.Any(other =>
                    !string.Equals(other.KeyId, keyId, StringComparison.Ordinal) &&
                    other.Enabled &&
                    string.Equals(other.Name?.Trim(), record.Name?.Trim(),
                        StringComparison.OrdinalIgnoreCase) &&
                    SameScopes(other.Scopes, desired)))
                    return false;

                if (SameScopes(record.Scopes, desired))
                    return true;

                string[] previous = record.Scopes;
                long previousUpdatedAt = record.UpdatedAt;
                record.Scopes = desired;
                record.UpdatedAt = Math.Max(
                    DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    previousUpdatedAt + 1);
                try
                {
                    SaveLocked();
                }
                catch
                {
                    record.Scopes = previous;
                    record.UpdatedAt = previousUpdatedAt;
                    throw;
                }
                return true;
            }
        }

        public bool SetEnabled(
            string keyId,
            bool enabled)
        {
            if (string.IsNullOrWhiteSpace(keyId))
                return false;

            lock (m_Sync)
            {
                using StoreLease lease = OpenLatestLocked();
                NexApiKeyRecord record =
                    m_Document.Keys.FirstOrDefault(x =>
                        string.Equals(
                            x.KeyId,
                            keyId,
                            StringComparison.Ordinal));

                if (record == null)
                    return false;

                // Re-enabling a superseded key must not undo the duplicate
                // protection enforced at creation time.
                if (enabled && !record.Enabled &&
                    m_Document.Keys.Any(other =>
                        !string.Equals(other.KeyId, keyId, StringComparison.Ordinal) &&
                        other.Enabled &&
                        string.Equals(other.Name?.Trim(), record.Name?.Trim(),
                            StringComparison.OrdinalIgnoreCase) &&
                        SameScopes(other.Scopes, NormalizeScopes(record.Scopes))))
                    return false;

                record.Enabled = enabled;
                record.UpdatedAt =
                    DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                SaveLocked();
                return true;
            }
        }

        public bool Delete(string keyId)
        {
            if (string.IsNullOrWhiteSpace(keyId)) return false;
            lock (m_Sync)
            {
                using StoreLease lease = OpenLatestLocked();
                // Key deletion is irreversible and is only allowed after
                // explicitly disabling it. Never delete an active key.
                NexApiKeyRecord record = m_Document.Keys.FirstOrDefault(x =>
                    string.Equals(x.KeyId, keyId, StringComparison.Ordinal));
                if (record == null || record.Enabled) return false;
                int removed = m_Document.Keys.RemoveAll(x =>
                    string.Equals(x.KeyId, keyId, StringComparison.Ordinal));
                if (removed > 0) SaveLocked();
                return removed > 0;
            }
        }

        public bool TryValidate(
            string apiKey,
            out NexApiKeyRecord record)
        {
            record = null;

            if (string.IsNullOrWhiteSpace(apiKey))
                return false;

            int separator = apiKey.IndexOf('.');
            if (separator <= 0 ||
                separator == apiKey.Length - 1)
                return false;

            string keyId = apiKey.Substring(0, separator);
            string secret = apiKey.Substring(separator + 1);

            lock (m_Sync)
            {
                using StoreLease lease = OpenLatestLocked();
                NexApiKeyRecord stored =
                    m_Document.Keys.FirstOrDefault(x =>
                        string.Equals(
                            x.KeyId,
                            keyId,
                            StringComparison.Ordinal));

                if (stored == null ||
                    !stored.Enabled ||
                    !VerifySecret(
                        secret,
                        stored.SecretSalt,
                        stored.SecretHash))
                    return false;

                long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                // Last-used is operational metadata, not an authorization
                // grant. Avoid rewriting the entire credential file for
                // every asset/statistics HTTP request under high traffic.
                // First use is always persisted; later uses are sampled
                // with a maximum age of 30 seconds.
                if (stored.LastUsedAt <= 0 ||
                    now - stored.LastUsedAt >= 30)
                {
                    stored.LastUsedAt = now;
                    SaveLocked();
                }
                record = Clone(stored);
                return true;
            }
        }

        private StoreDocument Load()
        {
            if (!File.Exists(m_Path))
                return new StoreDocument();

            try
            {
                return JsonSerializer.Deserialize<StoreDocument>(
                    File.ReadAllText(m_Path, Encoding.UTF8))
                    ?? new StoreDocument();
            }
            catch (Exception e)
            {
                throw new InvalidDataException(
                    "Unable to load NexVerse API key store: " +
                    m_Path,
                    e);
            }
        }

        private void SaveLocked()
        {
            string directory =
                Path.GetDirectoryName(m_Path);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            string temp = m_Path + ".tmp";
            File.WriteAllText(
                temp,
                JsonSerializer.Serialize(
                    m_Document,
                    new JsonSerializerOptions
                    {
                        WriteIndented = true
                    }),
                new UTF8Encoding(false));
            File.Move(temp, m_Path, true);
        }

        private static bool SameScopes(string[] left, string[] right)
        {
            return NormalizeScopes(left).SequenceEqual(
                NormalizeScopes(right), StringComparer.OrdinalIgnoreCase);
        }

        private static void ValidateScopes(string[] normalizedScopes)
        {
            if (normalizedScopes.Length == 0)
                throw new ArgumentException(
                    "At least one API key scope is required.", nameof(normalizedScopes));

            foreach (string scope in normalizedScopes)
            {
                if (!s_SupportedScopes.Contains(scope))
                    throw new ArgumentException(
                        "API keys may only receive explicit supported machine scopes; wildcard, admin and interactive identity scopes are forbidden.",
                        nameof(normalizedScopes));
            }

            if (normalizedScopes.Contains(NexScopes.ExperiencesViewerPermissions,
                    StringComparer.OrdinalIgnoreCase) &&
                normalizedScopes.Length != 1)
                throw new ArgumentException(
                    "Experiences viewer permission credentials must have only experiences:viewer:permissions.",
                    nameof(normalizedScopes));

            if (normalizedScopes.Contains(NexScopes.ExperiencesViewerManage,
                    StringComparer.OrdinalIgnoreCase) &&
                normalizedScopes.Length != 1)
                throw new ArgumentException(
                    "Firestorm Experience management credentials must have only experiences:viewer:manage.",
                    nameof(normalizedScopes));
        }

        private static string[] NormalizeScopes(
            IEnumerable<string> scopes)
        {
            return (scopes ?? Array.Empty<string>())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim().ToLowerInvariant())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private static void HashSecret(
            string secret,
            out string salt,
            out string hash)
        {
            byte[] saltBytes =
                RandomNumberGenerator.GetBytes(24);

            byte[] derived =
                Rfc2898DeriveBytes.Pbkdf2(
                    secret,
                    saltBytes,
                    SecretIterations,
                    HashAlgorithmName.SHA256,
                    32);

            salt = Convert.ToBase64String(saltBytes);
            hash = Convert.ToBase64String(derived);
        }

        private static bool VerifySecret(
            string secret,
            string salt,
            string hash)
        {
            try
            {
                byte[] saltBytes =
                    Convert.FromBase64String(salt);
                byte[] expected =
                    Convert.FromBase64String(hash);
                byte[] actual =
                    Rfc2898DeriveBytes.Pbkdf2(
                        secret,
                        saltBytes,
                        SecretIterations,
                        HashAlgorithmName.SHA256,
                        expected.Length);

                return CryptographicOperations
                    .FixedTimeEquals(expected, actual);
            }
            catch
            {
                return false;
            }
        }

        private static string Base64UrlEncode(
            byte[] value)
        {
            return Convert
                .ToBase64String(value)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }

        private static NexApiKeyRecord Clone(
            NexApiKeyRecord value)
        {
            return new NexApiKeyRecord
            {
                KeyId = value.KeyId,
                Name = value.Name,
                Scopes =
                    (value.Scopes ??
                     Array.Empty<string>())
                    .ToArray(),
                SecretSalt = value.SecretSalt,
                SecretHash = value.SecretHash,
                Enabled = value.Enabled,
                CreatedAt = value.CreatedAt,
                UpdatedAt = value.UpdatedAt,
                LastUsedAt = value.LastUsedAt
            };
        }

        private sealed class StoreDocument
        {
            public List<NexApiKeyRecord> Keys { get; set; } =
                new List<NexApiKeyRecord>();
        }
    }
}

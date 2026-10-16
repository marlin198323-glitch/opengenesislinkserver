using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;

namespace OpenGenesisLINK.Addons.AssetService
{
    /// <summary>
    /// Offline verification of a metadata export's content hashes against FSAssets.
    /// Input is a newline-separated list of SHA-256 hex hashes exported from a
    /// consistent database snapshot. No database connection or writes occur.
    /// </summary>
    public static class OGLFsassetsHashAudit
    {
        public sealed class Result
        {
            public long Checked { get; set; }
            public long Valid { get; set; }
            public long Missing { get; set; }
            public long Mismatched { get; set; }
            public long Invalid { get; set; }
        }

        public static Result Verify(string baseDirectory, IEnumerable<string> hashes, bool osgridFormat = false)
        {
            string root = Path.GetFullPath(baseDirectory);
            if (!Directory.Exists(root)) throw new DirectoryNotFoundException(root);
            var result = new Result();
            foreach (string raw in hashes)
            {
                if (raw == null) { result.Checked++; result.Invalid++; continue; }
                string hash = raw.Trim().ToLowerInvariant();
                if (hash.Length == 0) continue;
                result.Checked++;
                if (hash.Length != 64 || !IsHex(hash))
                {
                    result.Invalid++;
                    continue;
                }
                string path = osgridFormat
                    ? Path.Combine(root, hash.Substring(0, 3), hash.Substring(3, 3), hash)
                    : Path.Combine(root, hash.Substring(0, 2), hash.Substring(2, 2),
                        hash.Substring(4, 2), hash.Substring(6, 4), hash);
                if (!File.Exists(path) && !File.Exists(path + ".gz"))
                {
                    result.Missing++;
                    continue;
                }
                try
                {
                    using Stream input = File.Exists(path + ".gz")
                        ? new GZipStream(File.OpenRead(path + ".gz"), CompressionMode.Decompress)
                        : File.OpenRead(path);
                    string actual = Convert.ToHexString(SHA256.HashData(input)).ToLowerInvariant();
                    if (String.Equals(actual, hash, StringComparison.Ordinal))
                        result.Valid++;
                    else
                        result.Mismatched++;
                }
                catch (IOException) { result.Mismatched++; }
                catch (InvalidDataException) { result.Mismatched++; }
                catch (UnauthorizedAccessException) { result.Mismatched++; }
            }
            return result;
        }

        private static bool IsHex(string value)
        {
            foreach (char c in value)
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f')))
                    return false;
            return true;
        }
    }
}

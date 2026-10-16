using System;
using System.Collections.Generic;
using System.IO;

namespace OpenGenesisLINK.Addons.AssetService
{
    public static class OGLFsassetsMetadataAudit
    {
        public sealed class Result
        {
            public long Rows { get; set; }
            public long InvalidRows { get; set; }
            public long ConflictingIds { get; set; }
            public long DuplicateIds { get; set; }
            public long UniqueAssets { get; set; }
            public long UniqueHashes { get; set; }
            public long ValidFiles { get; set; }
            public long MissingFiles { get; set; }
            public long MismatchedFiles { get; set; }
        }

        // Offline TSV format: asset UUID, TAB, SHA-256 hash.
        public static Result Verify(string root, IEnumerable<string> rows, bool osgrid = false)
        {
            var result = new Result();
            var ids = new Dictionary<Guid, string>();
            var hashes = new HashSet<string>(StringComparer.Ordinal);
            foreach (string line in rows)
            {
                if (String.IsNullOrWhiteSpace(line)) { result.Rows++; result.InvalidRows++; continue; }
                result.Rows++;
                string[] columns = line.Split('\t');
                if (columns.Length != 2 || !Guid.TryParse(columns[0].Trim(), out Guid id))
                {
                    result.InvalidRows++;
                    continue;
                }
                string hash = columns[1].Trim().ToLowerInvariant();
                if (hash.Length != 64 || !IsHex(hash))
                {
                    result.InvalidRows++;
                    continue;
                }
                if (ids.TryGetValue(id, out string previous))
                {
                    result.DuplicateIds++;
                    if (previous != hash) result.ConflictingIds++;
                    continue;
                }
                ids.Add(id, hash);
                hashes.Add(hash);
            }
            result.UniqueAssets = ids.Count;
            result.UniqueHashes = hashes.Count;
            var files = OGLFsassetsHashAudit.Verify(root, hashes, osgrid);
            result.ValidFiles = files.Valid;
            result.MissingFiles = files.Missing;
            result.MismatchedFiles = files.Mismatched;
            return result;
        }

        private static bool IsHex(string hash)
        {
            foreach (char c in hash)
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f')))
                    return false;
            return true;
        }
    }
}

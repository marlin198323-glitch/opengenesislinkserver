// SPDX-License-Identifier: MPL-2.0
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;

namespace OpenGenesisLINK.Addons.AssetService
{
    /// <summary>
    /// Offline/resumable import from a *consistent snapshot* of SQL UUID->hash
    /// metadata, FSAssets disk and spool into the parallel OGL object store.
    /// Does not mutate SQL, FSAssets files, or their spool.
    /// </summary>
    public sealed class OGLParallelSnapshotMigrator
    {
        public sealed class Result
        {
            public long Rows { get; set; }
            public long Valid { get; set; }
            public long Imported { get; set; }
            public long VerifiedExisting { get; set; }
            public long Bytes { get; set; }
            public long Failed { get; set; }
        }

        public Result Run(string fsBase, string spool, string metadataTsv,
            string objectDirectory, string indexPath, long expectedRows,
            bool copy, bool osgrid = false)
        {
            if (expectedRows <= 0)
                throw new ArgumentOutOfRangeException(nameof(expectedRows));
            if (!File.Exists(metadataTsv))
                throw new FileNotFoundException("SQL snapshot UUID/hash TSV missing.", metadataTsv);
            string source = Path.GetFullPath(fsBase);
            string spoolRoot = Path.GetFullPath(spool);
            string objects = Path.GetFullPath(objectDirectory);
            string index = Path.GetFullPath(indexPath);
            if (!Directory.Exists(source) || !Directory.Exists(spoolRoot))
                throw new DirectoryNotFoundException("Snapshot FSAssets base/spool directory missing.");
            if (OGLParallelAssetStore.IsInside(objects, source) ||
                OGLParallelAssetStore.IsInside(objects, spoolRoot) ||
                OGLParallelAssetStore.IsInside(source, objects) ||
                OGLParallelAssetStore.IsInside(spoolRoot, objects) ||
                OGLParallelAssetStore.IsInside(index, source) ||
                OGLParallelAssetStore.IsInside(index, spoolRoot) ||
                OGLParallelAssetStore.IsInside(index, objects))
                throw new InvalidOperationException("Source snapshot, OGL object root and index must be disjoint.");
            RejectLinkedDirectory(source);
            RejectLinkedDirectory(spoolRoot);
            RejectLinkedFile(metadataTsv);

            // A dry run cannot even create the OGL directories or SQLite file.
            OGLParallelAssetStore target = copy ? new OGLParallelAssetStore(objects, index) : null;
            var result = new Result();
            var ids = new HashSet<Guid>();
            foreach (string line in File.ReadLines(metadataTsv))
            {
                result.Rows++;
                if (result.Rows > expectedRows)
                    throw new InvalidDataException("Metadata export has more rows than expected.");
                string[] parts = line.Split('\t');
                if (parts.Length != 2 ||
                    !Guid.TryParseExact(parts[0], "D", out Guid uuid) || uuid == Guid.Empty ||
                    !ValidHash(parts[1]) || !ids.Add(uuid))
                {
                    result.Failed++;
                    continue;
                }
                try
                {
                    string id = uuid.ToString("D");
                    string hash = parts[1].ToLowerInvariant();
                    byte[] bytes = ReadSnapshotPayload(source, spoolRoot, hash, osgrid);
                    if (bytes == null ||
                        !String.Equals(Convert.ToHexString(SHA256.HashData(bytes)),
                            hash, StringComparison.OrdinalIgnoreCase))
                    {
                        result.Failed++;
                        continue;
                    }
                    result.Valid++;
                    result.Bytes += bytes.LongLength;
                    if (!copy) continue;

                    byte[] existing = target.TryRead(id, hash);
                    if (existing != null)
                    {
                        result.VerifiedExisting++;
                        continue;
                    }
                    if (target.Mirror(id, hash, bytes))
                        result.Imported++;
                    else
                        result.Failed++;
                }
                catch (Exception error) when (error is IOException ||
                    error is InvalidDataException || error is UnauthorizedAccessException ||
                    error is InvalidOperationException)
                {
                    result.Failed++;
                    if (result.Failed <= 10)
                        Console.Error.WriteLine("Snapshot row " + result.Rows +
                            " could not be imported: " + error.GetType().Name);
                }
            }
            if (result.Rows != expectedRows)
                throw new InvalidDataException("Metadata export row count differs from expected count.");
            return result;
        }

        private static bool ValidHash(string hash)
        {
            if (hash == null || hash.Length != 64) return false;
            foreach (char c in hash)
                if (!((c >= '0' && c <= '9') ||
                      (c >= 'A' && c <= 'F') ||
                      (c >= 'a' && c <= 'f')))
                    return false;
            return true;
        }

        private static byte[] ReadSnapshotPayload(string baseDir, string spool,
            string hash, bool osgrid)
        {
            // FSAssets resolves pending spool writes first, then compressed
            // files, then uncompressed files. Copying these inputs without SQL
            // from the same snapshot is NOT a valid migration.
            string spoolFile = Path.Combine(spool, hash + ".asset");
            string relative = osgrid
                ? Path.Combine(hash.Substring(0, 3), hash.Substring(3, 3), hash)
                : Path.Combine(hash.Substring(0, 2), hash.Substring(2, 2),
                    hash.Substring(4, 2), hash.Substring(6, 4), hash);
            string file = Path.Combine(baseDir, relative);
            string gzip = file + ".gz";
            if (File.Exists(spoolFile))
                return ReadLimited(spoolFile, false);
            if (File.Exists(gzip))
                return ReadLimited(gzip, true);
            if (File.Exists(file))
                return ReadLimited(file, false);
            return null;
        }

        private static byte[] ReadLimited(string path, bool compressed)
        {
            RejectLinkedFile(path);
            RejectLinkedDirectory(Path.GetDirectoryName(path));
            const long MaxSize = 512L * 1024 * 1024;
            using (var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (Stream reader = compressed
                ? new GZipStream(input, CompressionMode.Decompress)
                : input)
            using (var output = new MemoryStream())
            {
                byte[] buffer = new byte[81920];
                int count;
                while ((count = reader.Read(buffer, 0, buffer.Length)) > 0)
                {
                    if (output.Length > MaxSize - count)
                        throw new InvalidDataException("Snapshot asset exceeds 512 MiB safety limit.");
                    output.Write(buffer, 0, count);
                }
                return output.ToArray();
            }
        }

        private static void RejectLinkedDirectory(string directory)
        {
            string current = Path.GetFullPath(directory);
            while (Directory.Exists(current))
            {
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Snapshot directory contains symbolic links.");
                string parent = Path.GetDirectoryName(current);
                if (String.IsNullOrEmpty(parent) || parent == current) break;
                current = parent;
            }
        }

        private static void RejectLinkedFile(string file)
        {
            if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Snapshot input must not be a symbolic link.");
        }
    }
}

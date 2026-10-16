using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;

namespace OpenGenesisLINK.Addons.AssetService
{
    /// <summary>
    /// Copy-only filesystem migration with resumable SHA-256 verification.
    /// This is a file migration primitive, NOT a complete FSAssets database migration.
    /// Requires an offline/quiesced source and a separately migrated metadata database.
    /// </summary>
    public sealed class OGLFsassetsMigrator
    {
        public sealed class Result
        {
            public long FilesScanned { get; set; }
            public long FilesCopied { get; set; }
            public long FilesVerified { get; set; }
            public long BytesCopied { get; set; }
            public long Failures { get; set; }
        }

        public Result Run(string source, string destination, bool dryRun = true)
        {
            string src = Path.GetFullPath(source ?? throw new ArgumentNullException(nameof(source)));
            string dst = Path.GetFullPath(destination ?? throw new ArgumentNullException(nameof(destination)));
            if (!Directory.Exists(src)) throw new DirectoryNotFoundException(src);
            string srcPrefix = Path.TrimEndingDirectorySeparator(src) + Path.DirectorySeparatorChar;
            string dstPrefix = Path.TrimEndingDirectorySeparator(dst) + Path.DirectorySeparatorChar;
            if (String.Equals(src, dst, StringComparison.Ordinal) ||
                dstPrefix.StartsWith(srcPrefix, StringComparison.Ordinal) ||
                srcPrefix.StartsWith(dstPrefix, StringComparison.Ordinal))
                throw new InvalidOperationException("Source and destination must be disjoint directories.");
            var result = new Result();
            foreach (string file in Directory.EnumerateFiles(src, "*", SearchOption.AllDirectories))
            {
                result.FilesScanned++;
                string relative = Path.GetRelativePath(src, file);
                string target = Path.GetFullPath(Path.Combine(dst, relative));
                if (!target.StartsWith(dstPrefix, StringComparison.Ordinal))
                    throw new InvalidOperationException("Unsafe migration path.");
                if (dryRun) continue;
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(target));
                    string sourceHash = Hash(file);
                    if (File.Exists(target) && Hash(target) == sourceHash)
                    {
                        result.FilesVerified++;
                        continue;
                    }
                    string temp = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
                    try
                    {
                        using (var input = File.OpenRead(file))
                        using (var output = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                        {
                            result.BytesCopied += input.Length;
                            input.CopyTo(output);
                            output.Flush(true);
                        }
                        if (Hash(temp) != sourceHash)
                            throw new IOException("Copied file checksum mismatch: " + relative);
                        File.Move(temp, target, true);
                        result.FilesCopied++;
                        result.FilesVerified++;
                    }
                    finally { if (File.Exists(temp)) File.Delete(temp); }
                }
                catch (IOException) { result.Failures++; }
                catch (UnauthorizedAccessException) { result.Failures++; }
            }
            return result;
        }

        private static string Hash(string path)
        {
            using var stream = File.OpenRead(path);
            return Convert.ToHexString(SHA256.HashData(stream));
        }
    }
}

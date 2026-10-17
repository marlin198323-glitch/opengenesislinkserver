using System;
using System.IO;
using System.Text;
using System.Text.Json;

namespace OpenGenesisLINK.Addons.AssetService
{
    public sealed class OGLAssetServiceAdminAuditJournal : IDisposable
    {
        private readonly object sync = new object();
        private FileStream stream;
        private readonly string fullPath;
        private readonly long maxBytes;
        private readonly int retainedFiles;
        private bool disposed;

        public OGLAssetServiceAdminAuditJournal(string path)
            : this(path, 0, 0) { }

        public OGLAssetServiceAdminAuditJournal(string path, long maxBytes, int retainedFiles)
        {
            if (maxBytes < 0 || maxBytes > 1073741824L)
                throw new ArgumentOutOfRangeException(nameof(maxBytes));
            if (retainedFiles < 0 || retainedFiles > 100)
                throw new ArgumentOutOfRangeException(nameof(retainedFiles));
            if ((maxBytes == 0) != (retainedFiles == 0))
                throw new ArgumentException("Rotation requires both maximum size and retention count.");
            this.maxBytes = maxBytes;
            this.retainedFiles = retainedFiles;
            if (String.IsNullOrWhiteSpace(path))
                throw new ArgumentException("A journal path is required.", nameof(path));
            fullPath = Path.GetFullPath(path);
            string directory = Path.GetDirectoryName(fullPath);
            if (!Directory.Exists(directory))
                throw new DirectoryNotFoundException("Audit directory must exist.");
            if (File.Exists(fullPath) && (File.GetAttributes(fullPath) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Audit journal link targets are not supported.");
            stream = OpenJournal();
        }

        public void Record(string endpoint, string outcome)
        {
            if (endpoint != "status" && endpoint != "configuration")
                throw new ArgumentOutOfRangeException(nameof(endpoint));
            if (outcome != "allowed" && outcome != "unauthorized" &&
                outcome != "forbidden" && outcome != "throttled" && outcome != "error")
                throw new ArgumentOutOfRangeException(nameof(outcome));
            string line = JsonSerializer.Serialize(new
            {
                timestampUtc = DateTimeOffset.UtcNow,
                endpoint,
                outcome
            }) + "\n";
            byte[] bytes = Encoding.UTF8.GetBytes(line);
            lock (sync)
            {
                if (disposed) throw new ObjectDisposedException(nameof(OGLAssetServiceAdminAuditJournal));
                if (maxBytes > 0 && stream.Length + bytes.Length > maxBytes)
                    Rotate();
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }
        }

        private static void AssertRegularFileOrMissing(string path)
        {
            if (File.Exists(path) &&
                (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Audit rotation does not allow linked files.");
        }

        private FileStream OpenJournal()
        {
            if (File.Exists(fullPath) &&
                (File.GetAttributes(fullPath) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Audit journal links are not supported.");
            return new FileStream(fullPath, FileMode.Append, FileAccess.Write,
                FileShare.Read, 4096, FileOptions.WriteThrough);
        }

        private void Rotate()
        {
            stream.Flush(true);
            stream.Dispose();
            // Rotation and retention are serialized within this instance.
            // Host must guarantee exclusive ownership of the journal path.
            try
            {
                string oldest = fullPath + "." + retainedFiles;
                AssertRegularFileOrMissing(oldest);
                if (File.Exists(oldest)) File.Delete(oldest);
                for (int i = retainedFiles - 1; i >= 1; i--)
                {
                    string source = fullPath + "." + i;
                    string target = fullPath + "." + (i + 1);
                    AssertRegularFileOrMissing(source);
                    AssertRegularFileOrMissing(target);
                    if (File.Exists(source)) File.Move(source, target, true);
                }
                AssertRegularFileOrMissing(fullPath);
                AssertRegularFileOrMissing(fullPath + ".1");
                if (File.Exists(fullPath))
                    File.Move(fullPath, fullPath + ".1", true);
            }
            finally
            {
                // Reopening failures propagate; HTTP admin then fails closed.
                stream = OpenJournal();
            }
        }

        public void Dispose()
        {
            lock (sync)
            {
                if (disposed) return;
                disposed = true;
                stream.Dispose();
            }
        }
    }
}

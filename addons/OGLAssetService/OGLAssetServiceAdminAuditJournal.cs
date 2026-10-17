using System;
using System.IO;
using System.Text;
using System.Text.Json;

namespace OpenGenesisLINK.Addons.AssetService
{
    public sealed class OGLAssetServiceAdminAuditJournal : IDisposable
    {
        private readonly object sync = new object();
        private readonly FileStream stream;
        private bool disposed;

        public OGLAssetServiceAdminAuditJournal(string path)
        {
            if (String.IsNullOrWhiteSpace(path))
                throw new ArgumentException("A journal path is required.", nameof(path));
            string fullPath = Path.GetFullPath(path);
            string directory = Path.GetDirectoryName(fullPath);
            if (!Directory.Exists(directory))
                throw new DirectoryNotFoundException("Audit directory must exist.");
            if (File.Exists(fullPath) && (File.GetAttributes(fullPath) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Audit journal link targets are not supported.");
            stream = new FileStream(fullPath, FileMode.Append, FileAccess.Write,
                FileShare.Read, 4096, FileOptions.WriteThrough);
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
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
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

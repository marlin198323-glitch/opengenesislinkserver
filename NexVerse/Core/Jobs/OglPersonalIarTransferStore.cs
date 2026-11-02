// SPDX-License-Identifier: MPL-2.0
// R633: private byte-transfer staging, NOT a complete or public IAR archive API.
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;

namespace NexVerse.Core.Jobs
{
    /// <summary>
    /// Bounded, owner-bound IAR upload staging. Filenames are derived solely
    /// from server-issued operation IDs. A verified checksum does NOT mean the
    /// bytes contain a valid TAR/IAR; validation must precede any import.
    /// No public handler exposes this storage without World authentication.
    /// </summary>
    public sealed class OglPersonalIarTransferStore
    {
        public const long MaximumArchiveBytes = 512L * 1024 * 1024;
        public const int MaximumChunkBytes = 1024 * 1024;
        private readonly string m_Root;
        private readonly object m_Sync = new();

        private sealed class TransferRecord
        {
            public Guid OperationId { get; set; }
            public Guid OwnerId { get; set; }
            public long ExpectedBytes { get; set; }
            public long ReceivedBytes { get; set; }
            public bool ChecksumVerified { get; set; }
            public string Sha256 { get; set; } = string.Empty;
        }

        public OglPersonalIarTransferStore(string rootDirectory)
        {
            if (string.IsNullOrWhiteSpace(rootDirectory))
                throw new ArgumentException("Private staging directory is required.",nameof(rootDirectory));
            m_Root = Path.GetFullPath(rootDirectory);
            Directory.CreateDirectory(m_Root);
            CheckDirectory();
        }

        public void Begin(Guid operationId, Guid ownerId, long expectedBytes)
        {
            ValidateIds(operationId, ownerId);
            if (expectedBytes < 1 || expectedBytes > MaximumArchiveBytes)
                throw new ArgumentOutOfRangeException(nameof(expectedBytes));
            lock(m_Sync)
            {
                CheckDirectory();
                string manifest = ManifestPath(operationId);
                if (File.Exists(manifest) || File.Exists(DataPath(operationId,false)) || File.Exists(DataPath(operationId,true)))
                    throw new InvalidOperationException("An IAR transfer already exists for this operation.");
                Save(new TransferRecord { OperationId=operationId, OwnerId=ownerId, ExpectedBytes=expectedBytes });
            }
        }

        public long Append(Guid operationId, Guid ownerId, long offset, ReadOnlyMemory<byte> bytes)
        {
            ValidateIds(operationId, ownerId);
            if (bytes.Length < 1 || bytes.Length > MaximumChunkBytes)
                throw new ArgumentOutOfRangeException(nameof(bytes));
            lock(m_Sync)
            {
                TransferRecord record = GetOwned(operationId,ownerId);
                if (record.ChecksumVerified || offset != record.ReceivedBytes ||
                    bytes.Length > record.ExpectedBytes - record.ReceivedBytes)
                    throw new InvalidOperationException("Invalid IAR transfer state, offset or size.");
                string file=DataPath(operationId,false);
                CheckRegularFile(file);
                using (FileStream output = new(file,FileMode.OpenOrCreate,FileAccess.Write,FileShare.None))
                {
                    if (output.Length != record.ReceivedBytes)
                        throw new InvalidDataException("IAR data and manifest lengths disagree.");
                    output.Position=offset;
                    output.Write(bytes.Span);
                    output.Flush(true);
                }
                record.ReceivedBytes+=bytes.Length;
                Save(record);
                return record.ReceivedBytes;
            }
        }

        public string VerifyChecksum(Guid operationId, Guid ownerId, string expectedSha256)
        {
            ValidateIds(operationId, ownerId);
            if (expectedSha256==null || expectedSha256.Length!=64 ||
                !IsHex(expectedSha256))
                throw new ArgumentException("A complete SHA-256 checksum is required.",nameof(expectedSha256));
            lock(m_Sync)
            {
                TransferRecord record=GetOwned(operationId,ownerId);
                if (record.ChecksumVerified)throw new InvalidOperationException("Transfer is already verified.");
                if (record.ReceivedBytes!=record.ExpectedBytes)
                    throw new InvalidOperationException("IAR transfer is incomplete.");
                string pending=DataPath(operationId,false);
                CheckRegularFile(pending);
                string digest;
                using(FileStream source=new(pending,FileMode.Open,FileAccess.Read,FileShare.None))
                {
                    if(source.Length!=record.ExpectedBytes)
                        throw new InvalidDataException("IAR transfer size mismatch.");
                    digest=Convert.ToHexString(SHA256.HashData(source)).ToLowerInvariant();
                }
                if(!string.Equals(digest,expectedSha256,StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("IAR checksum does not match.");
                string complete=DataPath(operationId,true);
                if(File.Exists(complete))throw new InvalidOperationException("Verified transfer already exists.");
                File.Move(pending,complete);
                record.Sha256=digest;
                record.ChecksumVerified=true;
                Save(record);
                return digest;
            }
        }

        /// <summary>Returns a private stream, not a TAR/IAR-validity verdict.</summary>
        public Stream OpenChecksumVerified(Guid operationId, Guid ownerId)
        {
            ValidateIds(operationId, ownerId);
            lock(m_Sync)
            {
                TransferRecord record=GetOwned(operationId,ownerId);
                if(!record.ChecksumVerified)throw new InvalidOperationException("IAR transfer not verified.");
                string file=DataPath(operationId,true);
                CheckRegularFile(file);
                FileStream stream=new(file,FileMode.Open,FileAccess.Read,FileShare.Read);
                if(stream.Length==record.ExpectedBytes)return stream;
                stream.Dispose();
                throw new InvalidDataException("Verified IAR file length has changed.");
            }
        }

        public void Remove(Guid operationId, Guid ownerId)
        {
            ValidateIds(operationId,ownerId);
            lock(m_Sync)
            {
                GetOwned(operationId,ownerId);
                foreach(string file in new[]{DataPath(operationId,false),DataPath(operationId,true),ManifestPath(operationId)})
                {
                    CheckRegularFile(file);
                    if(File.Exists(file))File.Delete(file);
                }
            }
        }

        private TransferRecord GetOwned(Guid operationId, Guid ownerId)
        {
            CheckDirectory();
            string file=ManifestPath(operationId);
            CheckRegularFile(file);
            if (!File.Exists(file))throw new FileNotFoundException("IAR operation not found.");
            TransferRecord record;
            using(FileStream source=new(file,FileMode.Open,FileAccess.Read,FileShare.Read))
                record=JsonSerializer.Deserialize<TransferRecord>(source);
            if(record==null || record.OperationId!=operationId || record.OwnerId!=ownerId)
                throw new FileNotFoundException("IAR operation not found.");
            if(record.ExpectedBytes<1 || record.ExpectedBytes>MaximumArchiveBytes ||
                record.ReceivedBytes<0 || record.ReceivedBytes>record.ExpectedBytes)
                throw new InvalidDataException("Invalid IAR transfer manifest.");
            return record;
        }

        private void Save(TransferRecord record)
        {
            CheckDirectory();
            string file=ManifestPath(record.OperationId);
            CheckRegularFile(file);
            string temp=file+"."+Guid.NewGuid().ToString("N")+".tmp";
            try
            {
                using(FileStream output=new(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None))
                {
                    JsonSerializer.Serialize(output,record);
                    output.Flush(true);
                }
                File.Move(temp,file,true);
            }
            finally { if(File.Exists(temp))File.Delete(temp); }
        }

        private string ManifestPath(Guid id)=>Path.Combine(m_Root,id.ToString("N")+".json");
        private string DataPath(Guid id,bool verified)=>Path.Combine(m_Root,id.ToString("N")+(verified?".iar":".part"));
        private void CheckDirectory()
        {
            if ((File.GetAttributes(m_Root)&FileAttributes.ReparsePoint)!=0)
                throw new InvalidOperationException("Symlinked IAR staging roots are forbidden.");
        }
        private static void CheckRegularFile(string file)
        {
            if (File.Exists(file) && (File.GetAttributes(file)&FileAttributes.ReparsePoint)!=0)
                throw new InvalidDataException("Symlinked IAR transfer files are forbidden.");
        }
        private static void ValidateIds(Guid operationId,Guid ownerId)
        {
            if(operationId==Guid.Empty || ownerId==Guid.Empty)
                throw new ArgumentException("Operation and owner identities are required.");
        }
        private static bool IsHex(string value)
        {
            foreach(char c in value)
                if(!((c>='0'&&c<='9')||(c>='a'&&c<='f')||(c>='A'&&c<='F')))return false;
            return true;
        }
    }
}
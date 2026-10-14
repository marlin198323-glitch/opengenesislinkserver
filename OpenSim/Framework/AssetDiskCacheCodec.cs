// SPDX-License-Identifier: MPL-2.0
using System;
using System.IO;
using System.Text;

namespace OpenSim.Framework
{
    /// <summary>
    /// Safe, strictly typed, versioned Flotsam disk-cache format.
    /// Cache files are disposable and MUST NOT use BinaryFormatter:
    /// its deserializer can instantiate attacker-selected object graphs.
    /// </summary>
    public static class AssetDiskCacheCodec
    {
        private const uint Magic = 0x414C474F; // "OGLA" in little endian
        private const int FormatVersion = 1;
        private const int MaxTextBytes = 1024 * 1024;
        private const int MaxAssetBytes = 256 * 1024 * 1024;
        private const int MaxShaBytes = 128;
        private static readonly UTF8Encoding StrictUtf8 = new(false, true);

        public static void Write(Stream stream, AssetBase asset)
        {
            if (stream == null || !stream.CanWrite)
                throw new ArgumentException("Cache stream is not writable", nameof(stream));
            if (asset == null || asset.Metadata == null)
                throw new ArgumentNullException(nameof(asset));

            using BinaryWriter writer = new(stream, StrictUtf8, true);
            writer.Write(Magic);
            writer.Write(FormatVersion);

            AssetMetadata meta = asset.Metadata;
            WriteText(writer, meta.ID);
            WriteText(writer, meta.Name);
            WriteText(writer, meta.Description);
            WriteText(writer, meta.CreatorID);
            writer.Write(meta.Type);
            writer.Write(meta.Local);
            writer.Write(meta.Temporary);
            writer.Write((int)meta.Flags);
            writer.Write(meta.CreationDate.ToBinary());
            WriteText(writer, meta.ContentType);
            WriteBytes(writer, meta.SHA1, MaxShaBytes);
            writer.Write(asset.UploadAttempts);
            WriteBytes(writer, asset.Data, MaxAssetBytes);
            writer.Flush();
        }

        public static AssetBase Read(Stream stream, string expectedId = null)
        {
            if (stream == null || !stream.CanRead || !stream.CanSeek)
                throw new ArgumentException("Seekable readable cache stream required", nameof(stream));
            // Deny excessively large or malformed cache files before allocation.
            if (stream.Length - stream.Position > MaxAssetBytes + 5L * MaxTextBytes + 256)
                throw new InvalidDataException("Asset cache exceeds maximum file size");

            using BinaryReader reader = new(stream, StrictUtf8, true);
            if (reader.ReadUInt32() != Magic ||
                reader.ReadInt32() != FormatVersion)
                throw new InvalidDataException("Old or unknown asset cache format");

            string id = ReadText(reader);
            string name = ReadText(reader);
            string description = ReadText(reader);
            string creatorId = ReadText(reader);
            sbyte type = reader.ReadSByte();
            bool local = reader.ReadBoolean();
            bool temporary = reader.ReadBoolean();
            AssetFlags flags = (AssetFlags)reader.ReadInt32();
            long creationDate = reader.ReadInt64();
            string contentType = ReadText(reader);
            byte[] sha1 = ReadBytes(reader, MaxShaBytes);
            int uploadAttempts = reader.ReadInt32();
            byte[] data = ReadBytes(reader, MaxAssetBytes);

            if (stream.Position != stream.Length)
                throw new InvalidDataException("Unexpected trailing asset cache bytes");
            if (string.IsNullOrWhiteSpace(id) ||
                (expectedId != null &&
                    !string.Equals(expectedId, id, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidDataException("Asset cache ID does not match request");

            DateTime created;
            try { created = DateTime.FromBinary(creationDate); }
            catch (ArgumentException e)
            {
                throw new InvalidDataException("Invalid asset creation timestamp", e);
            }
            AssetMetadata meta = new()
            {
                ID = id,
                Name = name,
                Description = description,
                CreatorID = creatorId,
                ContentType = contentType,
                Type = type,
                Local = local,
                Temporary = temporary,
                Flags = flags,
                CreationDate = created,
                SHA1 = sha1
            };
            return new AssetBase
            {
                Metadata = meta,
                Data = data,
                UploadAttempts = uploadAttempts
            };
        }

        private static void WriteText(BinaryWriter writer, string value)
        {
            if (value == null) { writer.Write(-1); return; }
            byte[] bytes = StrictUtf8.GetBytes(value);
            if (bytes.Length > MaxTextBytes)
                throw new InvalidDataException("Asset cache text exceeds limit");
            writer.Write(bytes.Length);
            writer.Write(bytes);
        }

        private static string ReadText(BinaryReader reader)
        {
            int count = reader.ReadInt32();
            if (count == -1) return null;
            if (count < 0 || count > MaxTextBytes ||
                count > reader.BaseStream.Length - reader.BaseStream.Position)
                throw new InvalidDataException("Invalid asset cache text length");
            byte[] data = reader.ReadBytes(count);
            if (data.Length != count) throw new EndOfStreamException();
            try { return StrictUtf8.GetString(data); }
            catch (DecoderFallbackException e)
            {
                throw new InvalidDataException("Invalid asset cache UTF-8", e);
            }
        }

        private static void WriteBytes(BinaryWriter writer, byte[] data, int max)
        {
            if (data == null) { writer.Write(-1); return; }
            if (data.Length > max)
                throw new InvalidDataException("Asset cache byte payload exceeds limit");
            writer.Write(data.Length);
            writer.Write(data);
        }

        private static byte[] ReadBytes(BinaryReader reader, int max)
        {
            int count = reader.ReadInt32();
            if (count == -1) return null;
            if (count < 0 || count > max ||
                count > reader.BaseStream.Length - reader.BaseStream.Position)
                throw new InvalidDataException("Invalid asset cache byte length");
            byte[] data = reader.ReadBytes(count);
            if (data.Length != count) throw new EndOfStreamException();
            return data;
        }
    }
}

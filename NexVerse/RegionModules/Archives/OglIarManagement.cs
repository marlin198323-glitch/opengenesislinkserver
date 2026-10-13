// OpenGenesisLINK - IAR management foundation
// SPDX-License-Identifier: MPL-2.0

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace NexVerse.RegionModules.Archives
{
    public sealed class OglIarInspection
    {
        public string Path { get; set; } = string.Empty;
        public long SizeBytes { get; set; }
        public string Sha256 { get; set; } = string.Empty;
        public bool Valid { get; set; }
        public bool HasControlFile { get; set; }
        public int Entries { get; set; }
        public int InventoryEntries { get; set; }
        public int Assets { get; set; }
        public string Error { get; set; } = string.Empty;
    }

    public static class OglIarInspector
    {
        // Bounded inspection happens BEFORE the archive is handed to the
        // mutating inventory/FSAssets importer. Values are conservative and
        // intentionally independent of the compressed-file storage quota.
        public const long MaxCompressedBytes = 1024L * 1024 * 1024;
        public const long MaxExpandedBytes = 2L * 1024 * 1024 * 1024;
        public const long MaxFileBytes = 64L * 1024 * 1024;
        public const int MaxEntries = 100000;
        private const int MaxControlBytes = 65536;

        public static OglIarInspection Inspect(string archivePath)
        {
            OglIarInspection result = new() { Path = archivePath ?? string.Empty };
            if (string.IsNullOrWhiteSpace(archivePath) || !File.Exists(archivePath))
            {
                result.Error = "IAR-Datei wurde nicht gefunden.";
                return result;
            }

            try
            {
                FileInfo info = new(archivePath);
                result.SizeBytes = info.Length;
                if (info.Length < 128 || info.Length > MaxCompressedBytes)
                    throw new InvalidDataException("Komprimierte IAR-Datei ausserhalb der erlaubten Groesse.");

                // SHA-256 is useful for audit, deduplication and later
                // pre-import integrity comparisons, not archive validation.
                using (FileStream hashStream = File.OpenRead(archivePath))
                using (SHA256 sha = SHA256.Create())
                    result.Sha256 = Convert.ToHexString(sha.ComputeHash(hashStream)).ToLowerInvariant();

                HashSet<string> visitedFiles = new(StringComparer.Ordinal);
                long expandedBytes = 0;
                bool assetsStarted = false;
                using FileStream input = File.OpenRead(archivePath);
                using GZipStream gzip = new(input, CompressionMode.Decompress);
                using BinaryReader tar = new(gzip, Encoding.UTF8, leaveOpen: true);

                while (OglTarReader.TryReadEntry(tar, out string path, out long size, out byte type))
                {
                    bool directory = type == (byte)'5';
                    if (type != 0 && type != (byte)'0' && !directory)
                        throw new InvalidDataException("IAR enthaelt unerlaubte TAR-Typen (Links/PAX/Sparse).");

                    ValidatePath(path);
                    if (directory && size != 0)
                        throw new InvalidDataException("IAR-Ordner darf keinen Dateninhalt haben.");

                    if (++result.Entries > MaxEntries)
                        throw new InvalidDataException("IAR enthaelt zu viele Eintraege.");
                    if (size > MaxFileBytes)
                        throw new InvalidDataException("Einzelne IAR-Datei ueberschreitet das Groessenlimit.");
                    long padded = checked(((size + 511L) / 512L) * 512L);
                    expandedBytes = checked(expandedBytes + 512L + padded);
                    if (expandedBytes > MaxExpandedBytes)
                        throw new InvalidDataException("IAR expandiert ueber das Gesamtlimit.");

                    if (!directory)
                    {
                        if (!visitedFiles.Add(path))
                            throw new InvalidDataException("IAR enthaelt doppelte Dateipfade.");

                        if (path == "archive.xml")
                        {
                            if (size <= 0 || size > MaxControlBytes)
                                throw new InvalidDataException("Ungueltige IAR-Kontrolldatei.");
                            byte[] xml = OglTarReader.ReadPayload(tar, size);
                            ValidateControlFile(xml);
                            result.HasControlFile = true;
                            OglTarReader.SkipPadding(tar, size);
                            continue;
                        }
                        if (path.StartsWith("inventory/", StringComparison.Ordinal))
                        {
                            if (assetsStarted)
                                throw new InvalidDataException("Inventareintraege muessen vor Assets liegen.");
                            ++result.InventoryEntries;
                        }
                        else if (path.StartsWith("assets/", StringComparison.Ordinal))
                        {
                            assetsStarted = true;
                            ++result.Assets;
                        }
                        else
                            throw new InvalidDataException("Unbekannter IAR-Eintrag.");
                    }
                    OglTarReader.SkipPayload(tar, size);
                }

                // Consume the rest of the compressed stream so gzip CRC and
                // truncated trailers are checked. TAR permits zero padding.
                OglTarReader.CheckTrailingZeros(tar);
                if (expandedBytes > 32L * 1024 * 1024 &&
                    expandedBytes > result.SizeBytes * 200L)
                    throw new InvalidDataException("IAR weist ein unplausibles Entpackungsverhaeltnis auf.");

                result.Valid = result.HasControlFile && result.InventoryEntries > 0;
                if (!result.Valid)
                    result.Error = "Archiv enthaelt keine gueltige IAR-Struktur.";
            }
            catch (Exception e) when (e is IOException || e is InvalidDataException ||
                e is EndOfStreamException || e is FormatException || e is OverflowException ||
                e is XmlException || e is ArgumentException)
            {
                result.Valid = false;
                result.Error = "IAR-Vorabpruefung abgelehnt: " + e.Message;
            }
            return result;
        }

        private static void ValidatePath(string path)
        {
            if (string.IsNullOrEmpty(path) || path.Length > 512 ||
                path.StartsWith("/", StringComparison.Ordinal) ||
                path.IndexOf('\\') >= 0 || path.IndexOf(':') >= 0 ||
                path.IndexOf('\0') >= 0)
                throw new InvalidDataException("Unsicherer IAR-Pfad.");

            foreach (string segment in path.Split('/'))
                if (segment == "." || segment == "..")
                    throw new InvalidDataException("IAR-Pfad enthaelt Traversal-Segmente.");

            if (!(path == "archive.xml" ||
                path.StartsWith("inventory/", StringComparison.Ordinal) ||
                path.StartsWith("assets/", StringComparison.Ordinal)))
                throw new InvalidDataException("IAR-Dateipfad ausserhalb der erlaubten Verzeichnisse.");
        }

        private static void ValidateControlFile(byte[] payload)
        {
            using MemoryStream stream = new(payload, writable: false);
            using XmlReader reader = XmlReader.Create(stream, new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                MaxCharactersInDocument = MaxControlBytes,
                MaxCharactersFromEntities = 0
            });
            XDocument document = XDocument.Load(reader);
            XElement archive = document.Element("archive");
            if (archive == null ||
                !int.TryParse((string)archive.Attribute("major_version"), out int major) ||
                !int.TryParse((string)archive.Attribute("minor_version"), out int minor) ||
                major < 0 || major > 1 || minor < 0)
                throw new InvalidDataException("IAR-Kontrolldatei hat eine inkompatible Version.");
        }
    }

    internal static class OglTarReader
    {
        public static bool TryReadEntry(BinaryReader reader, out string path, out long size, out byte type)
        {
            path = string.Empty; size = 0; type = 0;
            byte[] header = reader.ReadBytes(512);
            if (header.Length == 0)
                throw new InvalidDataException("TAR endet ohne abschliessenden Nullblock.");
            if (header.Length != 512)
                throw new InvalidDataException("Unvollstaendiger TAR-Header.");
            if (Array.TrueForAll(header, x => x == 0))
                return false;

            long expectedChecksum = ReadOctal(header, 148, 8);
            long calculatedChecksum = 0;
            for (int i = 0; i < header.Length; ++i)
                calculatedChecksum += (i >= 148 && i < 156) ? 32 : header[i];
            if (expectedChecksum != calculatedChecksum)
                throw new InvalidDataException("Ungueltige TAR-Header-Pruefsumme.");

            path = ReadText(header, 0, 100);
            string prefix = ReadText(header, 345, 155);
            if (!string.IsNullOrEmpty(prefix)) path = prefix + "/" + path;
            size = ReadOctal(header, 124, 12);
            type = header[156];
            return true;
        }

        private static long ReadOctal(byte[] header, int offset, int length)
        {
            string value = ReadText(header, offset, length).Trim();
            if (string.IsNullOrWhiteSpace(value)) return 0;
            long result = 0;
            foreach (char ch in value)
            {
                if (ch < '0' || ch > '7')
                    throw new InvalidDataException("Ungueltiger TAR-Oktalwert.");
                result = checked(result * 8 + (ch - '0'));
            }
            return result;
        }

        private static string ReadText(byte[] buffer, int offset, int length)
        {
            string value = Encoding.ASCII.GetString(buffer, offset, length);
            int nul = value.IndexOf('\0');
            return (nul >= 0 ? value.Substring(0, nul) : value).Trim();
        }

        public static byte[] ReadPayload(BinaryReader reader, long length)
        {
            if (length < 0 || length > 65536)
                throw new InvalidDataException("IAR-Kontrollinhalt ueberschreitet Limit.");
            byte[] data = reader.ReadBytes((int)length);
            if (data.Length != length)
                throw new EndOfStreamException("IAR-Kontrollinhalt unvollstaendig.");
            return data;
        }

        public static void SkipPadding(BinaryReader reader, long size)
        {
            SkipExact(reader, checked(((size + 511L) / 512L) * 512L - size));
        }

        public static void SkipPayload(BinaryReader reader, long size)
        {
            if (size < 0) throw new InvalidDataException("Ungueltige TAR-Eintragsgroesse.");
            SkipExact(reader, checked(((size + 511L) / 512L) * 512L));
        }

        private static void SkipExact(BinaryReader reader, long size)
        {
            while (size > 0)
            {
                int take = (int)Math.Min(8192, size);
                if (reader.ReadBytes(take).Length != take)
                    throw new EndOfStreamException("IAR/TAR endet innerhalb eines Eintrags.");
                size -= take;
            }
        }

        public static void CheckTrailingZeros(BinaryReader reader)
        {
            byte[] buffer = new byte[8192];
            long count = 0;
            int n;
            while ((n = reader.Read(buffer, 0, buffer.Length)) > 0)
            {
                count += n;
                if (count > 65536)
                    throw new InvalidDataException("TAR hat zu viel nachgestellte Daten.");
                for (int i = 0; i < n; ++i)
                    if (buffer[i] != 0)
                        throw new InvalidDataException("TAR enthaelt nach dem Ende weitere Daten.");
            }
        }
    }

    public sealed class OglIarStoragePolicy
    {
        public string RootDirectory { get; }
        public long MaximumArchiveBytes { get; }

        public OglIarStoragePolicy(string rootDirectory, long maximumArchiveBytes)
        {
            if (string.IsNullOrWhiteSpace(rootDirectory)) throw new ArgumentException("IAR-Speicherverzeichnis darf nicht leer sein.", nameof(rootDirectory));
            if (maximumArchiveBytes <= 0) throw new ArgumentOutOfRangeException(nameof(maximumArchiveBytes));
            RootDirectory = System.IO.Path.GetFullPath(rootDirectory);
            MaximumArchiveBytes = maximumArchiveBytes;
        }

        public string ResolveArchivePath(string fileName)
        {
            string safeName = System.IO.Path.GetFileName(fileName ?? string.Empty);
            if (string.IsNullOrWhiteSpace(safeName) || !safeName.EndsWith(".iar", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Nur .iar-Dateien sind im verwalteten Archivspeicher erlaubt.", nameof(fileName));
            string candidate = System.IO.Path.GetFullPath(System.IO.Path.Combine(RootDirectory, safeName));
            string prefix = RootDirectory.TrimEnd(System.IO.Path.DirectorySeparatorChar) + System.IO.Path.DirectorySeparatorChar;
            if (!candidate.StartsWith(prefix, StringComparison.Ordinal))
                throw new InvalidOperationException("IAR-Pfad verlaesst den verwalteten Speicherbereich.");
            return candidate;
        }

        public bool AcceptsExistingArchive(string path, out string reason)
        {
            reason = string.Empty;
            if (!File.Exists(path)) { reason = "IAR-Datei wurde nicht gefunden."; return false; }
            long size = new FileInfo(path).Length;
            if (size <= 0 || size > MaximumArchiveBytes) { reason = "IAR-Dateigroesse liegt ausserhalb der erlaubten Speicher-Policy."; return false; }
            return true;
        }
    }
}

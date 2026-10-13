// SPDX-License-Identifier: MPL-2.0
// OpenGenesisLINK -- check the full gzip/TAR structure BEFORE calling any
// inventory/asset mutation. Applies to both classic "load iar" and World API.
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Xml;

namespace OpenSim.Region.CoreModules.Avatar.Inventory.Archiver
{
    public static class OglIarImportGuard
    {
        // Intentionally conservative. In particular, gzip bombs are rejected
        // by bounded decompressed bytes even when they advertise tiny size.
        public const long MaxCompressedBytes = 2L * 1024 * 1024 * 1024;
        public const long MaxExpandedBytes = 4L * 1024 * 1024 * 1024;
        public const long MaxSingleAssetBytes = 64L * 1024 * 1024;
        public const long MaxInventoryItemBytes = 4L * 1024 * 1024;
        public const int MaxEntries = 100000;
        public const int MaxAssets = 25000;

        public static bool TryValidate(Stream input, out string reason)
        {
            reason = string.Empty;
            if (input == null || !input.CanRead || !input.CanSeek)
            {
                reason = "IAR benoetigt eine lesbare, seek-faehige Archivdatei.";
                return false;
            }

            long original = input.Position;
            try
            {
                long archiveLength = input.Length - original;
                if (archiveLength < 20 || archiveLength > MaxCompressedBytes)
                    throw new InvalidDataException("IAR ist leer oder ueberschreitet das komprimierte Groessenlimit.");

                int magic1 = input.ReadByte(), magic2 = input.ReadByte();
                if (magic1 != 0x1f || magic2 != 0x8b)
                    throw new InvalidDataException("IAR ist kein gueltiges gzip-komprimiertes TAR.");

                input.Position = original;
                using GZipStream gzip = new GZipStream(input, CompressionMode.Decompress, leaveOpen: true);
                byte[] header = new byte[512];
                byte[] buffer = new byte[65536];
                long expanded = 0;
                int entries = 0, assets = 0, inventory = 0;
                bool control = false;
                bool endMarker = false;
                HashSet<string> names = new HashSet<string>(StringComparer.Ordinal);

                void ReadExactly(byte[] target, int length)
                {
                    int pos = 0;
                    while (pos < length)
                    {
                        int got = gzip.Read(target, pos, length - pos);
                        if (got <= 0)
                            throw new InvalidDataException("IAR ist unvollstaendig oder abgeschnitten.");
                        expanded = checked(expanded + got);
                        if (expanded > MaxExpandedBytes)
                            throw new InvalidDataException("IAR ueberschreitet das entpackte Groessenlimit.");
                        pos += got;
                    }
                }

                while (true)
                {
                    // A clean TAR must terminate with at least two empty blocks.
                    ReadExactly(header, 512);
                    bool zeros = true;
                    for (int i = 0; i < header.Length; i++)
                        if (header[i] != 0) { zeros = false; break; }
                    if (zeros)
                    {
                        ReadExactly(header, 512);
                        for (int i = 0; i < header.Length; i++)
                            if (header[i] != 0)
                                throw new InvalidDataException("IAR hat keine korrekte TAR-Endmarkierung.");
                        endMarker = true;
                        break;
                    }

                    entries++;
                    if (entries > MaxEntries)
                        throw new InvalidDataException("IAR enthaelt zu viele Eintraege.");

                    long declaredChecksum = Octal(header, 148, 8);
                    long actualChecksum = 0;
                    for (int i = 0; i < 512; i++)
                        actualChecksum += i >= 148 && i < 156 ? (byte)32 : header[i];
                    if (declaredChecksum != actualChecksum)
                        throw new InvalidDataException("IAR hat einen fehlerhaften TAR-Header.");

                    string name = Text(header, 0, 100);
                    string prefix = Text(header, 345, 155);
                    if (!string.IsNullOrEmpty(prefix))
                        name = prefix + "/" + name;
                    long size = Octal(header, 124, 12);
                    byte kind = header[156];
                    bool directory = kind == (byte)'5';
                    if (kind != 0 && kind != (byte)'0' && !directory)
                        throw new InvalidDataException("IAR enthaelt nicht erlaubte Link- oder Spezialdateien.");
                    if (directory && size != 0)
                        throw new InvalidDataException("IAR-Verzeichnis hat ungueltige Nutzdaten.");
                    if (!IsAllowedPath(name, directory))
                        throw new InvalidDataException("IAR enthaelt unsichere oder unbekannte Archivpfade.");

                    if (!names.Add(name.TrimEnd('/')))
                        throw new InvalidDataException("IAR enthaelt doppelte Datei-/Verzeichnispfade.");

                    bool asset = name.StartsWith("assets/", StringComparison.Ordinal) && !directory;
                    bool inventoryEntry = name.StartsWith("inventory/", StringComparison.Ordinal) && !directory;
                    bool archiveControl = string.Equals(name, "archive.xml", StringComparison.Ordinal);

                    if (asset && ++assets > MaxAssets)
                        throw new InvalidDataException("IAR enthaelt zu viele Assets.");
                    if (inventoryEntry) inventory++;
                    if (archiveControl)
                    {
                        if (control || size <= 0 || size > 524288)
                            throw new InvalidDataException("IAR-Steuerdatei fehlt oder ist ungueltig.");
                        control = true;
                    }
                    long perItemLimit = asset ? MaxSingleAssetBytes :
                                        archiveControl ? 524288 :
                                        inventoryEntry ? MaxInventoryItemBytes : 0;
                    if (!directory && (size <= 0 || size > perItemLimit))
                        throw new InvalidDataException("IAR-Eintrag ueberschreitet das erlaubte Groessenlimit.");

                    if (archiveControl || inventoryEntry)
                    {
                        byte[] xmlBytes = new byte[checked((int)size)];
                        ReadExactly(xmlBytes, xmlBytes.Length);
                        XmlReaderSettings settings = new XmlReaderSettings
                        {
                            DtdProcessing = DtdProcessing.Prohibit,
                            XmlResolver = null,
                            MaxCharactersInDocument = 524288
                        };
                        using XmlReader xml = XmlReader.Create(new MemoryStream(xmlBytes), settings);
                        xml.MoveToContent();
                        if (archiveControl &&
                            !string.Equals(xml.LocalName, "archive", StringComparison.OrdinalIgnoreCase))
                            throw new InvalidDataException("IAR-Steuerdatei hat eine ungueltige XML-Wurzel.");
                        while (xml.Read()) { }
                    }
                    else
                    {
                        long remaining = size;
                        while (remaining > 0)
                        {
                            int count = checked((int)Math.Min(remaining, buffer.Length));
                            ReadExactly(buffer, count);
                            remaining -= count;
                        }
                    }

                    int pad = checked((int)((512 - (size % 512)) % 512));
                    if (pad > 0) ReadExactly(buffer, pad);
                }

                if (!endMarker || !control || inventory == 0)
                    throw new InvalidDataException("IAR hat keine gueltige Steuerdatei oder kein Inventar.");

                // Reject appended decompressed payloads rather than trusting a
                // misleading early TAR EOF. Bound even the padding/trailer.
                long trailing = 0;
                int lastRead;
                while ((lastRead = gzip.Read(buffer, 0, buffer.Length)) > 0)
                {
                    trailing += lastRead;
                    if (trailing > 1024 * 1024)
                        throw new InvalidDataException("IAR enthaelt einen uebergrossen TAR-Anhang.");
                    for (int i = 0; i < lastRead; i++)
                        if (buffer[i] != 0)
                            throw new InvalidDataException("IAR enthaelt Daten nach TAR-Ende.");
                }

                if (expanded > archiveLength * 500L && expanded > 32L * 1024 * 1024)
                    throw new InvalidDataException("IAR weist ein verdaechtiges Kompressionsverhaeltnis auf.");

                return true;
            }
            catch (Exception ex) when (ex is IOException || ex is InvalidDataException ||
                                       ex is OverflowException || ex is FormatException ||
                                       ex is XmlException || ex is ArgumentException)
            {
                reason = "IAR-Vorabpruefung abgelehnt: " + ex.Message;
                return false;
            }
            finally
            {
                input.Position = original;
            }
        }

        private static string Text(byte[] header, int offset, int size)
        {
            int count = 0;
            while (count < size && header[offset + count] != 0)
                count++;
            return Encoding.UTF8.GetString(header, offset, count);
        }

        private static long Octal(byte[] header, int offset, int size)
        {
            if ((header[offset] & 0x80) != 0)
                throw new InvalidDataException("Nicht unterstuetzte TAR-Groessenkodierung.");
            string field = Text(header, offset, size).Trim(' ', '\0');
            if (field.Length == 0) return 0;
            long result = 0;
            foreach (char ch in field)
            {
                if (ch < '0' || ch > '7')
                    throw new InvalidDataException("Fehlerhafte oktale TAR-Metadaten.");
                result = checked(result * 8 + (ch - '0'));
            }
            return result;
        }

        private static bool IsAllowedPath(string raw, bool directory)
        {
            if (string.IsNullOrWhiteSpace(raw) || raw.Length > 255 ||
                raw.StartsWith("/", StringComparison.Ordinal) ||
                raw.Contains('\\') || raw.Contains(':') ||
                raw.IndexOf('\0') >= 0)
                return false;
            string expanded;
            try { expanded = Uri.UnescapeDataString(raw); }
            catch { return false; }
            if (expanded.Contains('\\') || expanded.StartsWith("/", StringComparison.Ordinal))
                return false;
            string[] segments = expanded.TrimEnd('/').Split('/');
            foreach (string part in segments)
                if (string.IsNullOrEmpty(part) || part == "." || part == ".." ||
                    part.IndexOf('\0') >= 0)
                    return false;

            if (directory)
                return raw == "inventory/" || raw == "assets/" ||
                       raw.StartsWith("inventory/", StringComparison.Ordinal) ||
                       raw.StartsWith("assets/", StringComparison.Ordinal);
            return raw == "archive.xml" ||
                   (raw.StartsWith("inventory/", StringComparison.Ordinal) && segments.Length > 1) ||
                   (raw.StartsWith("assets/", StringComparison.Ordinal) && segments.Length == 2);
        }
    }
}

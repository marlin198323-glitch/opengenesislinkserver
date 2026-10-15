/*
 * OpenGenesisLINK YEngine script-state exception serialization.
 * Only recognized CLR Exception types are constructed; no type names are
 * loaded via reflection, and no BinaryFormatter graphs are accepted.
 */
using System;
using System.IO;

namespace OpenSim.Region.ScriptEngine.Yengine
{
    public static class ScriptStateExceptionCodec
    {
        private const int FormatVersion = 1;
        private const int MaxTextLength = 16384;

        public static void Write(BinaryWriter output, Exception exception)
        {
            if (output == null) throw new ArgumentNullException(nameof(output));
            if (exception == null) throw new ArgumentNullException(nameof(exception));

            string type = exception.GetType() == typeof(ArgumentException) ? "argument"
                : exception.GetType() == typeof(ArgumentNullException) ? "argument-null"
                : exception.GetType() == typeof(ArgumentOutOfRangeException) ? "argument-range"
                : exception.GetType() == typeof(InvalidOperationException) ? "invalid-operation"
                : exception.GetType() == typeof(NullReferenceException) ? "null-reference"
                : exception.GetType() == typeof(IndexOutOfRangeException) ? "index-range"
                : exception.GetType() == typeof(DivideByZeroException) ? "divide-zero"
                : exception.GetType() == typeof(FormatException) ? "format"
                : exception.GetType() == typeof(OverflowException) ? "overflow"
                : exception.GetType() == typeof(NotSupportedException) ? "not-supported"
                : exception.GetType() == typeof(InvalidCastException) ? "invalid-cast"
                : exception.GetType() == typeof(Exception) ? "exception"
                : null;

            // Never turn arbitrary script-controlled exception type names into
            // CLR constructor calls: fail rather than silently lose semantics.
            if (type == null)
                throw new NotSupportedException("Unsupported YEngine persisted exception type: " +
                    exception.GetType().FullName);
            if ((exception.Message?.Length ?? 0) > MaxTextLength)
                throw new InvalidDataException("YEngine exception message too long");

            output.Write(FormatVersion);
            output.Write(type);
            output.Write(exception.Message ?? "");
        }

        public static Exception Read(BinaryReader input)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (input.ReadInt32() != FormatVersion)
                throw new InvalidDataException("Unknown YEngine exception codec version");

            // BinaryReader.ReadString() allocates based on a length prefix.
            // Bound the underlying stream *before* each read to avoid
            // attacker-chosen arbitrary allocations.
            string type = ReadLimitedString(input);
            string message = ReadLimitedString(input);
            return type switch
            {
                "argument" => new ArgumentException(message),
                "argument-null" => new ArgumentNullException(paramName: null, message: message),
                "argument-range" => new ArgumentOutOfRangeException(paramName: null, message: message),
                "invalid-operation" => new InvalidOperationException(message),
                "null-reference" => new NullReferenceException(message),
                "index-range" => new IndexOutOfRangeException(message),
                "divide-zero" => new DivideByZeroException(message),
                "format" => new FormatException(message),
                "overflow" => new OverflowException(message),
                "not-supported" => new NotSupportedException(message),
                "invalid-cast" => new InvalidCastException(message),
                "exception" => new Exception(message),
                _ => throw new InvalidDataException("Unsupported YEngine exception tag")
            };
        }

        public static void WriteBoundedText(BinaryWriter writer, string value)
        {
            if (writer == null) throw new ArgumentNullException(nameof(writer));
            value ??= "";
            if (value.Length > MaxTextLength)
                throw new InvalidDataException("Oversized YEngine exception text");
            writer.Write(value);
        }

        public static string ReadLimitedString(BinaryReader reader)
        {
            return ReadLimitedString(reader, MaxTextLength);
        }

        // Larger limits are available only to ordinary LSL script-state
        // strings, not CLR exception class tags or exception text.
        public static string ReadLimitedString(BinaryReader reader, int maxCharacters)
        {
            if (reader == null) throw new ArgumentNullException(nameof(reader));
            if (maxCharacters < 1 || maxCharacters > 262144)
                throw new ArgumentOutOfRangeException(nameof(maxCharacters));
            // Read7BitEncodedInt is not exposed publicly in all targets.
            // Limit encoding length by reading individual prefix bytes, then
            // precisely the UTF-8 payload from the bounded stream.
            int len = 0, shift = 0;
            for (int i = 0; i < 3; ++i)
            {
                byte b = reader.ReadByte();
                len |= (b & 0x7f) << shift;
                if ((b & 0x80) == 0)
                {
                    if (len > maxCharacters * 4)
                        throw new InvalidDataException("Oversized YEngine persisted text");
                    byte[] bytes = reader.ReadBytes(len);
                    if (bytes.Length != len) throw new EndOfStreamException();
                    string result = new System.Text.UTF8Encoding(false, true).GetString(bytes);
                    if (result.Length > maxCharacters)
                        throw new InvalidDataException("Oversized YEngine persisted text");
                    return result;
                }
                shift += 7;
            }
            throw new InvalidDataException("Malformed YEngine exception text length");
        }
    }
}

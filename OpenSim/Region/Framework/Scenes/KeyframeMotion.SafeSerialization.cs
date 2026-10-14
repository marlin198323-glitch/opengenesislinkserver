// SPDX-License-Identifier: MPL-2.0
using System;
using System.Collections.Generic;
using System.IO;
using OpenMetaverse;

namespace OpenSim.Region.Framework.Scenes
{
    /// <summary>
    /// Explicit versioned encoding of persisted keyframe motion.
    /// Never deserialize arbitrary CLR object graphs from region DB / OAR data.
    /// </summary>
    public partial class KeyframeMotion
    {
        private const uint MotionMagic = 0x4D4B474F; // O G K M (little endian)
        private const int MotionVersion = 1;
        private const int MaxMotionFrames = 4096;
        private const int MaxMotionBytes = 4 * 1024 * 1024;

        private byte[] SerializeState()
        {
            if (m_frames.Count > MaxMotionFrames ||
                (m_keyframes != null && m_keyframes.Length > MaxMotionFrames))
                throw new InvalidDataException("Keyframe motion exceeds allowed frame count");

            using MemoryStream bytes = new();
            using (BinaryWriter writer = new(bytes, System.Text.Encoding.UTF8, true))
            {
                writer.Write(MotionMagic);
                writer.Write(MotionVersion);
                writer.Write((int)m_mode);
                writer.Write((int)m_data);
                writer.Write(m_running);
                writer.Write(m_iterations);
                writer.Write(m_skipLoops);
                WriteMotionVector(writer, m_serializedPosition);
                WriteMotionVector(writer, m_basePosition);
                WriteMotionRotation(writer, m_baseRotation);
                WriteMotionFrame(writer, m_currentFrame);
                writer.Write(m_frames.Count);
                foreach (Keyframe frame in m_frames)
                    WriteMotionFrame(writer, frame);
                writer.Write(m_keyframes == null ? -1 : m_keyframes.Length);
                if (m_keyframes != null)
                    foreach (Keyframe frame in m_keyframes)
                        WriteMotionFrame(writer, frame);
                writer.Flush();
            }
            if (bytes.Length > MaxMotionBytes)
                throw new InvalidDataException("Keyframe motion exceeds maximum encoded size");
            return bytes.ToArray();
        }

        private static KeyframeMotion DeserializeState(byte[] data)
        {
            if (data == null || data.Length < 8 || data.Length > MaxMotionBytes)
                throw new InvalidDataException("Invalid keyframe-motion size");

            using MemoryStream stream = new(data, false);
            using BinaryReader reader = new(stream, System.Text.Encoding.UTF8, true);
            if (reader.ReadUInt32() != MotionMagic ||
                reader.ReadInt32() != MotionVersion)
                throw new InvalidDataException(
                    "Unsupported keyframe format; legacy BinaryFormatter payloads are not accepted");

            int rawMode = reader.ReadInt32();
            int rawData = reader.ReadInt32();
            if (rawMode < 0 || rawMode > 3 || (rawData & ~3) != 0)
                throw new InvalidDataException("Invalid keyframe play mode or format");
            KeyframeMotion result = new(null, (PlayMode)rawMode, (DataFormat)rawData)
            {
                m_running = reader.ReadBoolean(),
                m_iterations = reader.ReadInt32(),
                m_skipLoops = reader.ReadInt32(),
                m_serializedPosition = ReadMotionVector(reader),
                m_basePosition = ReadMotionVector(reader),
                m_baseRotation = ReadMotionRotation(reader),
                m_currentFrame = ReadMotionFrame(reader)
            };

            int frameCount = ReadFrameCount(reader, false);
            result.m_frames = new List<Keyframe>(frameCount);
            for (int i = 0; i < frameCount; ++i)
                result.m_frames.Add(ReadMotionFrame(reader));

            int templateCount = ReadFrameCount(reader, true);
            if (templateCount >= 0)
            {
                result.m_keyframes = new Keyframe[templateCount];
                for (int i = 0; i < templateCount; ++i)
                    result.m_keyframes[i] = ReadMotionFrame(reader);
            }

            if (stream.Position != stream.Length)
                throw new InvalidDataException("Unexpected trailing keyframe-motion data");
            return result;
        }

        private static int ReadFrameCount(BinaryReader reader, bool allowNull)
        {
            int count = reader.ReadInt32();
            if (count < (allowNull ? -1 : 0) || count > MaxMotionFrames)
                throw new InvalidDataException("Invalid keyframe count");
            return count;
        }

        private static void WriteMotionVector(BinaryWriter writer, Vector3 vector)
        {
            writer.Write(vector.X);
            writer.Write(vector.Y);
            writer.Write(vector.Z);
        }

        private static Vector3 ReadMotionVector(BinaryReader reader)
        {
            float x = reader.ReadSingle();
            float y = reader.ReadSingle();
            float z = reader.ReadSingle();
            if (!float.IsFinite(x) || !float.IsFinite(y) || !float.IsFinite(z))
                throw new InvalidDataException("Nonfinite keyframe position");
            return new Vector3(x, y, z);
        }

        private static void WriteMotionRotation(BinaryWriter writer, Quaternion rotation)
        {
            writer.Write(rotation.X);
            writer.Write(rotation.Y);
            writer.Write(rotation.Z);
            writer.Write(rotation.W);
        }

        private static Quaternion ReadMotionRotation(BinaryReader reader)
        {
            float x = reader.ReadSingle();
            float y = reader.ReadSingle();
            float z = reader.ReadSingle();
            float w = reader.ReadSingle();
            if (!float.IsFinite(x) || !float.IsFinite(y) ||
                !float.IsFinite(z) || !float.IsFinite(w))
                throw new InvalidDataException("Nonfinite keyframe rotation");
            return new Quaternion(x, y, z, w);
        }

        private static void WriteMotionFrame(BinaryWriter writer, Keyframe frame)
        {
            writer.Write(frame.Position.HasValue);
            if (frame.Position.HasValue)
                WriteMotionVector(writer, frame.Position.Value);
            writer.Write(frame.Rotation.HasValue);
            if (frame.Rotation.HasValue)
                WriteMotionRotation(writer, frame.Rotation.Value);
            WriteMotionRotation(writer, frame.StartRotation);
            writer.Write(frame.TimeMS);
            writer.Write(frame.TimeTotal);
            WriteMotionVector(writer, frame.AngularVelocity);
            WriteMotionVector(writer, frame.StartPosition);
        }

        private static Keyframe ReadMotionFrame(BinaryReader reader)
        {
            Keyframe frame = new();
            if (reader.ReadBoolean())
                frame.Position = ReadMotionVector(reader);
            if (reader.ReadBoolean())
                frame.Rotation = ReadMotionRotation(reader);
            frame.StartRotation = ReadMotionRotation(reader);
            frame.TimeMS = reader.ReadInt32();
            frame.TimeTotal = reader.ReadInt32();
            frame.AngularVelocity = ReadMotionVector(reader);
            frame.StartPosition = ReadMotionVector(reader);
            return frame;
        }
    }
}

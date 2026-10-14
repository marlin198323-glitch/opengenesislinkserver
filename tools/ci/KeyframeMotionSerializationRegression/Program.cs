using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using OpenMetaverse;
using OpenSim.Region.Framework.Scenes;

internal static class Program
{
    private static int checks;

    private static void Check(bool condition, string description)
    {
        ++checks;
        if (!condition)
            throw new Exception("Keyframe serializer: " + description);
    }

    private static void Field(KeyframeMotion value, string name, object data)
    {
        typeof(KeyframeMotion).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(value, data);
    }

    private static T ReadField<T>(KeyframeMotion value, string name)
    {
        return (T)typeof(KeyframeMotion)
            .GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(value);
    }

    public static int Main()
    {
        KeyframeMotion value = new(null, KeyframeMotion.PlayMode.PingPong,
            KeyframeMotion.DataFormat.Translation | KeyframeMotion.DataFormat.Rotation);

        KeyframeMotion.Keyframe frame = new()
        {
            Position = new Vector3(8.5f, -2, 27),
            Rotation = new Quaternion(0, 0, 0, 1),
            StartRotation = new Quaternion(0, 0, 0, 1),
            TimeMS = 250,
            TimeTotal = 2000,
            AngularVelocity = new Vector3(0, 0, 0.25f),
            StartPosition = new Vector3(2, 4, 6)
        };
        KeyframeMotion.Keyframe missing = new()
        {
            Position = null,
            Rotation = null,
            StartRotation = new Quaternion(0, 0, 0, 1),
            TimeMS = 1000,
            TimeTotal = 3000,
            StartPosition = new Vector3(5, 0, 0)
        };

        value.SetKeyframes(new[] { frame, missing });
        Field(value, "m_frames", new List<KeyframeMotion.Keyframe> { frame });
        Field(value, "m_currentFrame", missing);
        Field(value, "m_basePosition", new Vector3(1, 2, 3));
        Field(value, "m_baseRotation", new Quaternion(0, 0, 0, 1));
        Field(value, "m_serializedPosition", new Vector3(1, 2, 3));
        Field(value, "m_iterations", 12);
        Field(value, "m_skipLoops", 4);

        byte[] saved = value.Serialize();
        Check(saved.Length > 50, "encoded data has a header and fields");
        KeyframeMotion restored = KeyframeMotion.FromData(null, saved);
        Check(restored != null, "valid typed data deserializes");
        Check(restored.Data == value.Data, "data flags preserved");
        Check(ReadField<KeyframeMotion.PlayMode>(restored, "m_mode") ==
              KeyframeMotion.PlayMode.PingPong, "play mode preserved");
        Check(ReadField<int>(restored, "m_iterations") == 12 &&
              ReadField<int>(restored, "m_skipLoops") == 4, "iteration counters preserved");
        KeyframeMotion.Keyframe[] template =
            ReadField<KeyframeMotion.Keyframe[]>(restored, "m_keyframes");
        Check(template.Length == 2 && template[0].Position.HasValue &&
              template[0].Position.Value.X == 8.5f &&
              !template[1].Position.HasValue && !template[1].Rotation.HasValue,
              "template keyframes and nullable values preserved");
        Check(ReadField<List<KeyframeMotion.Keyframe>>(restored, "m_frames").Count == 1,
              "active frames preserved");
        Check(ReadField<KeyframeMotion.Keyframe>(restored, "m_currentFrame").TimeMS == 1000,
              "current frame preserved");
        Check(ReadField<Vector3>(restored, "m_serializedPosition").Z == 3,
              "serialized position preserved");
        byte[] roundtrip = restored.Serialize();
        Check(saved.AsSpan().SequenceEqual(roundtrip), "full binary roundtrip");

        byte[] trailing = new byte[saved.Length + 1];
        Array.Copy(saved, trailing, saved.Length);
        Check(KeyframeMotion.FromData(null, trailing) == null, "trailing payload rejected");
        Check(KeyframeMotion.FromData(null, saved[..^4]) == null, "truncated payload rejected");
        Check(KeyframeMotion.FromData(null, new byte[] { 0, 1, 2, 3, 4, 5, 6, 7 }) == null,
              "legacy BinaryFormatter or foreign payload rejected");
        Check(KeyframeMotion.FromData(null, new byte[4 * 1024 * 1024 + 1]) == null,
              "oversized payload rejected");
        byte[] corrupt = (byte[])saved.Clone();
        // The frame count begins after the 8-byte header, two enum values,
        // a boolean, two integers, two Vector3s, one Quaternion and current frame.
        // A random truncated or wrong-version payload must never be interpreted as CLR objects.
        corrupt[4] = 12;
        Check(KeyframeMotion.FromData(null, corrupt) == null, "unknown version rejected");
        Console.WriteLine("Safe keyframe-motion serializer: PASS (" + checks + " checks)");
        return 0;
    }
}

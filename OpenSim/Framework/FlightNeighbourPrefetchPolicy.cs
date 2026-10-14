// SPDX-License-Identifier: MPL-2.0
using System;
using OpenMetaverse;

namespace OpenSim.Framework
{
    /// <summary>
    /// Bounded early neighbour refresh when a fast-flying avatar is
    /// approaching a real X/Y region edge. Does NOT initiate the crossing.
    /// </summary>
    public static class FlightNeighbourPrefetchPolicy
    {
        public const float MinimumHorizontalSpeed = 20f; // metres / second
        public const float MaximumLookAheadMetres = 128f;

        public static bool NeedsPrefetch(bool isFlying, Vector3 position,
            Vector3 velocity, uint regionSizeX, uint regionSizeY)
        {
            if (!isFlying || regionSizeX <= 0 || regionSizeY <= 0)
                return false;

            if (!float.IsFinite(position.X) || !float.IsFinite(position.Y) ||
                !float.IsFinite(velocity.X) || !float.IsFinite(velocity.Y) ||
                position.X < 0 || position.Y < 0 ||
                position.X >= regionSizeX || position.Y >= regionSizeY)
                return false;

            float speedSquared =
                velocity.X * velocity.X + velocity.Y * velocity.Y;
            if (!float.IsFinite(speedSquared) ||
                speedSquared < MinimumHorizontalSpeed * MinimumHorizontalSpeed)
                return false;

            float horizon = MathF.Min(MaximumLookAheadMetres,
                MathF.Max(32f, MathF.Sqrt(speedSquared) * 1.5f));

            // Directional checks prevent needless traffic while an avatar
            // flies parallel to, rather than towards, a region boundary.
            return (velocity.X > 0 && regionSizeX - position.X <= horizon) ||
                   (velocity.X < 0 && position.X <= horizon) ||
                   (velocity.Y > 0 && regionSizeY - position.Y <= horizon) ||
                   (velocity.Y < 0 && position.Y <= horizon);
        }
    }
}

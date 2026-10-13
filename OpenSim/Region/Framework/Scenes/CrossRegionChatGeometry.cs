// SPDX-License-Identifier: BSD-3-Clause
using System;
using OpenSim.Framework;

namespace OpenSim.Region.Framework.Scenes
{
    /// <summary>
    /// Computes chat coverage in world metres, independent of region origins
    /// and region dimensions (including variable-sized regions).
    /// </summary>
    public static class CrossRegionChatGeometry
    {
        public static bool CanReachRegion(float worldX, float worldY,
            float originX, float originY, float sizeX, float sizeY, float range)
        {
            if (!float.IsFinite(worldX) || !float.IsFinite(worldY) ||
                !float.IsFinite(originX) || !float.IsFinite(originY) ||
                sizeX <= 0 || sizeY <= 0 || range <= 0)
                return false;

            float dx = worldX < originX ? originX - worldX :
                worldX > originX + sizeX ? worldX - (originX + sizeX) : 0f;
            float dy = worldY < originY ? originY - worldY :
                worldY > originY + sizeY ? worldY - (originY + sizeY) : 0f;
            return dx * dx + dy * dy < range * range;
        }

        public static float ChatRange(ChatTypeEnum type, float whisper, float say, float shout)
        {
            switch (type)
            {
                case ChatTypeEnum.Whisper: return whisper;
                case ChatTypeEnum.Say: return say;
                case ChatTypeEnum.Shout: return shout;
                default: return 0;
            }
        }
    }
}

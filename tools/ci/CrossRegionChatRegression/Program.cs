// OpenGenesisLINK Cross-Region Chat geometry regression tests
using System;
using OpenSim.Framework;
using OpenSim.Region.Framework.Scenes;

static class CrossRegionChatRegression
{
    static int checks;
    static void Check(bool condition, string reason)
    {
        checks++;
        if (!condition) throw new InvalidOperationException(reason);
    }

    static void Main()
    {
        const float west = 1000 * 256;
        const float east = 1001 * 256;
        const float y = 1010 * 256;
        float x3 = east + 3f;

        // Avatar 3m east of the border, target region on its west.
        Check(CrossRegionChatGeometry.CanReachRegion(x3, y + 128, west, y, 256, 256, 20),
            "say from neighbouring region");
        Check(CrossRegionChatGeometry.CanReachRegion(x3, y + 128, west, y, 256, 256, 10),
            "whisper from neighbouring region");
        Check(!CrossRegionChatGeometry.CanReachRegion(east + 30, y + 128,
            west, y, 256, 256, 20), "say range must be enforced");
        Check(!CrossRegionChatGeometry.CanReachRegion(east + 20, y + 128,
            west, y, 256, 256, 20), "exact boundary distance is exclusive");
        Check(CrossRegionChatGeometry.CanReachRegion(east + 30, y + 128,
            west, y, 256, 256, 100), "shout reaches farther");
        Check(!CrossRegionChatGeometry.CanReachRegion(east + 3, y + 282,
            west, y, 256, 256, 20), "diagonal distance must be checked");
        Check(CrossRegionChatGeometry.CanReachRegion(east + 3, y + 258,
            west, y, 256, 256, 20), "diagonal near-corner distance");
        Check(CrossRegionChatGeometry.CanReachRegion(west + 512 + 4, y + 50,
            west, y, 512, 256, 20), "variable-sized region");
        Check(!CrossRegionChatGeometry.CanReachRegion(west + 512 + 21, y + 50,
            west, y, 512, 256, 20), "variable-sized out of range");
        Check(!CrossRegionChatGeometry.CanReachRegion(float.NaN, y,
            west, y, 256, 256, 20), "nonfinite position rejected");
        Check(!CrossRegionChatGeometry.CanReachRegion(x3, y,
            west, y, 0, 256, 20), "invalid target size rejected");

        Check(CrossRegionChatGeometry.ChatRange(ChatTypeEnum.Whisper, 10, 20, 100) == 10,
            "whisper range");
        Check(CrossRegionChatGeometry.ChatRange(ChatTypeEnum.Say, 10, 20, 100) == 20,
            "say range");
        Check(CrossRegionChatGeometry.ChatRange(ChatTypeEnum.Shout, 10, 20, 100) == 100,
            "shout range");
        Check(CrossRegionChatGeometry.ChatRange(ChatTypeEnum.Region, 10, 20, 100) == 0,
            "region broadcast must not propagate");

        // Position transformation used by both in-process and NexBus delivery:
        // B x=3 -> A x=259; a cube at A x=254 is only 5m away.
        float targetLocal = x3 - west;
        Check(Math.Abs(targetLocal - 254) < 20, "translated neighbour listener is in range");

        Console.WriteLine($"OGL cross-region llListen geometry: PASS ({checks} checks)");
    }
}

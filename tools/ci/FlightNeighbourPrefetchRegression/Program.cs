using System;
using OpenMetaverse;
using OpenSim.Framework;

internal static class Program
{
    private static int count;
    private static void Assert(bool result, string label)
    {
        count++;
        if (!result) throw new Exception("Fast region crossing prefetch: " + label);
    }
    private static bool Pref(bool fly, float x, float y, float vx, float vy,
        int sizeX = 256, int sizeY = 256) =>
        FlightNeighbourPrefetchPolicy.NeedsPrefetch(fly,
            new Vector3(x, y, 22), new Vector3(vx, vy, 0), sizeX, sizeY);

    public static int Main()
    {
        Assert(!Pref(false, 250, 50, 60, 0), "ground agents unchanged");
        Assert(!Pref(true, 250, 50, 5, 0), "slow flying agents unchanged");
        Assert(Pref(true, 235, 50, 30, 0), "fast east-bound approach");
        Assert(!Pref(true, 100, 50, 30, 0), "do not prefetch too far ahead");
        Assert(Pref(true, 18, 50, -45, 0), "fast west-bound approach");
        Assert(Pref(true, 50, 243, 0, 38), "northbound approach");
        Assert(Pref(true, 50, 16, 0, -40), "southbound approach");
        Assert(!Pref(true, 250, 50, 0, 60), "flight parallel to nearest east edge");
        Assert(Pref(true, 242, 245, 65, 65), "diagonal approach");
        Assert(!Pref(true, 100, 100, float.NaN, 24), "invalid velocity");
        Assert(!Pref(true, -1, 50, -100, 0), "already outside current region");
        Assert(!Pref(true, 255, 50, -100, 0), "away from nearest edge");
        Assert(Pref(true, 1002, 80, 70, 0, 1024, 1024), "varregion east edge");
        Assert(!Pref(true, 500, 80, 70, 0, 1024, 1024), "varregion internal position");
        Assert(Pref(true, 131, 100, 120, 0), "bounded high-speed lookahead");
        Assert(!Pref(true, 125, 100, 120, 0), "lookahead cap is enforced");
        Console.WriteLine("Bounded flight neighbour prefetch: PASS (" + count + " checks)");
        return 0;
    }
}

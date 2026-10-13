// OpenGenesisLINK in-world smoke test
// Compile in the OGL simulator; stock Second Life/OpenSimulator does not offer ogl*.
default
{
    state_entry()
    {
        vector dimensions = oglGetRegionDimensions();
        llOwnerSay("Region: " + llGetRegionName()
            + "\nX: " + (string)dimensions.x + " m"
            + "\nY: " + (string)dimensions.y + " m"
            + "\nZ: " + (string)dimensions.z + " m");

        if (dimensions.x <= 0.0 || dimensions.y <= 0.0 || dimensions.z <= 0.0)
            llOwnerSay("FAIL: invalid region dimensions");
        else
            llOwnerSay("PASS: positive scene dimensions");
    }
}

// OpenGenesisLINK regression check: held touch() without pointer motion
// Rez a prim, add this script, then hold the left mouse button still for
// at least 2 seconds. A long hold must print PASS on release even when the
// cursor never moves. Short clicks must continue to generate touch_end.

integer touchCount;
integer longHoldSeen;

default
{
    state_entry()
    {
        llOwnerSay("OGL touch hold test ready. Click and hold without moving the mouse.");
    }

    touch_start(integer detected)
    {
        touchCount = 0;
        longHoldSeen = FALSE;
        llResetTime();
    }

    touch(integer detected)
    {
        ++touchCount;
        if (llGetTime() > 1.0)
            longHoldSeen = TRUE;
    }

    touch_end(integer detected)
    {
        float seconds = llGetTime();
        if (seconds > 1.5)
        {
            if (longHoldSeen && touchCount > 1)
                llOwnerSay("PASS: stationary hold produced " + (string)touchCount
                    + " touch() events in " + (string)seconds + " seconds.");
            else
                llOwnerSay("FAIL: hold produced only " + (string)touchCount
                    + " touch() events. Waited " + (string)seconds + " seconds.");
        }
        else
        {
            llOwnerSay("Short click: touch_end after " + (string)seconds
                + " seconds; touch() count " + (string)touchCount + ".");
        }
    }
}

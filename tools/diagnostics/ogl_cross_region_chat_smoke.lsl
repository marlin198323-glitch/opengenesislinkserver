// OpenGenesisLINK 0.9.3.12: cross-region llListen live acceptance (CHAT01/CHAT02)
// Copy this script into EACH test cube on Region A, near the Region A/B border.
// In Region B, stand 5m from the cubes (measured in world coordinates).
// Type exactly "Beep", then "/7 Beep". The cubes must turn red once per
// message and their hover texts must each increment by exactly one.
// Repeat from >20m away. Neither cube may react to llSay-range messages.
// This is an in-world diagnostic, not proof of correct cross-node deployment.

integer PUBLIC_CHANNEL = 0;
integer PRIVATE_CHANNEL = 7;
string TRIGGER = "Beep";
integer publicHits = 0;
integer privateHits = 0;
integer publicListener = -1;
integer privateListener = -1;

setIdleAppearance()
{
    llSetColor(<1.0, 1.0, 1.0>, ALL_SIDES);
    llSetText(
        "OGL Region Chat Test\n"
        + llGetRegionName() + "\n"
        + "Public: " + (string)publicHits
        + " | /7: " + (string)privateHits,
        <1.0, 1.0, 1.0>, 1.0);
}

default
{
    state_entry()
    {
        publicHits = 0;
        privateHits = 0;
        publicListener = llListen(PUBLIC_CHANNEL, "", NULL_KEY, TRIGGER);
        privateListener = llListen(PRIVATE_CHANNEL, "", NULL_KEY, TRIGGER);
        setIdleAppearance();

        if (publicListener <= 0 || privateListener <= 0)
            llOwnerSay("CHAT FAIL: listen() konnte nicht aktiviert werden.");
        else
            llOwnerSay("CHAT READY auf " + llGetRegionName()
                + " bei " + (string)llGetPos()
                + " – teste Beep und /7 Beep ueber die Regionsgrenze.");
    }

    listen(integer channel, string name, key id, string message)
    {
        if (message != TRIGGER)
            return;

        if (channel == PUBLIC_CHANNEL)
            ++publicHits;
        else if (channel == PRIVATE_CHANNEL)
            ++privateHits;
        else
            return;

        llSetColor(<1.0, 0.0, 0.0>, ALL_SIDES);
        llSetText(
            "OGL CHAT EMPFANGEN\n"
            + llGetRegionName() + "\n"
            + "Public: " + (string)publicHits
            + " | /7: " + (string)privateHits,
            <1.0, 0.0, 0.0>, 1.0);
        llOwnerSay("CHAT RECEIVED Region=" + llGetRegionName()
            + " Channel=" + (string)channel
            + " Sender=" + name
            + " SenderID=" + (string)id
            + " PublicHits=" + (string)publicHits
            + " PrivateHits=" + (string)privateHits);
        llSetTimerEvent(3.0);
    }

    timer()
    {
        llSetTimerEvent(0.0);
        setIdleAppearance();
    }

    on_rez(integer startParam)
    {
        llResetScript();
    }
}

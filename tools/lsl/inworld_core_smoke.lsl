// OGL LSL 0.9.3.11 smoke: drop in a prim in an isolated test region.
// Output is deliberately deterministic. No money, external HTTP or permissions.
integer failures = 0;
integer timerSeen = FALSE;

assertEqual(integer actual, integer expected, string test)
{
    if (actual != expected)
    {
        ++failures;
        llOwnerSay("FAIL " + test + " actual=" + (string)actual + " expected=" + (string)expected);
    }
}

default
{
    state_entry()
    {
        failures = 0;
        timerSeen = FALSE;
        assertEqual(llAbs(-7), 7, "llAbs");
        assertEqual(llFloor(-1.2), -2, "llFloor negative");
        assertEqual(llCeil(-1.2), -1, "llCeil negative");
        assertEqual(llRound(2.5), 3, "llRound positive halfway");
        assertEqual(llRound(-2.5), -3, "llRound negative halfway");
        assertEqual(llStringLength("abc"), 3, "llStringLength");
        assertEqual(llGetListLength([1, 2, 3]), 3, "llGetListLength");
        llSetTimerEvent(0.25);
    }

    timer()
    {
        if (timerSeen)
            return;
        timerSeen = TRUE;
        llSetTimerEvent(0.0);
        if (failures == 0)
            llOwnerSay("OGL LSL 0.9.3.11 smoke PASS (7 assertions + timer event)");
        else
            llOwnerSay("OGL LSL 0.9.3.11 smoke FAIL: " + (string)failures);
    }
}

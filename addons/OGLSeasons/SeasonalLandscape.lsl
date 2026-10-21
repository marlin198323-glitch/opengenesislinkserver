// Stadt NexVerse: saisonales Landschafts-Linkset (Eigentümer-Opt-in).
// 1 Rootprim mit Script, 4 Child-Prims/Meshes mit alternativen
// Fruehling-/Sommer-/Herbst-/Winteransichten (alle am selben Standort).
// Ausschliesslich Alpha, KEINE Texturen, Assets, Terrain oder Fremdprims.
// Den vier Child-Prims die passenden Texturen vorab zuweisen.
// Bei Script-Reset/Modulausfall wird die definierte Basisansicht angezeigt.

integer WEATHER_CHANNEL = -835011;
integer LINK_FRUEHLING = 2;
integer LINK_SOMMER = 3;
integer LINK_HERBST = 4;
integer LINK_WINTER = 5;
integer ENABLED = TRUE;
integer m_lastSeen;
integer m_activeSeason = -1;
integer m_valid = FALSE;

integer validateLinks()
{
    integer maxLinks = llGetNumberOfPrims();
    list links = [LINK_FRUEHLING, LINK_SOMMER, LINK_HERBST, LINK_WINTER];
    integer i;
    for (i = 0; i < 4; ++i)
    {
        integer v = llList2Integer(links, i);
        if (v <= 1 || v > maxLinks) return FALSE;
        integer j;
        for (j = i + 1; j < 4; ++j)
            if (v == llList2Integer(links, j)) return FALSE;
    }
    return TRUE;
}

setSeason(integer which)
{
    if (!m_valid) return;
    list links = [LINK_FRUEHLING, LINK_SOMMER, LINK_HERBST, LINK_WINTER];
    integer i;
    for (i = 0; i < 4; ++i)
    {
        // One visible variant, no need to alter textures or inventory.
        integer linkNum = llList2Integer(links, i);
        float opacity = 0.0;
        if (i == which) opacity = 1.0;
        llSetLinkAlpha(linkNum, opacity, ALL_SIDES);
    }
    m_activeSeason = which;
}

default
{
    state_entry()
    {
        m_valid = validateLinks();
        if (!m_valid)
        {
            llOwnerSay("OGLSeasons: Vier unterschiedliche Link-Prims (2 bis 5) erforderlich.");
            return;
        }
        llListen(WEATHER_CHANNEL, "OGLSeasons", NULL_KEY, "");
        m_lastSeen = 0;
        setSeason(0);
        llSetTimerEvent(60.0);
    }
    listen(integer channel, string name, key id, string message)
    {
        if (!ENABLED || !m_valid || channel != WEATHER_CHANNEL ||
            name != "OGLSeasons" || id != NULL_KEY) return;
        list t = llParseStringKeepNulls(message, ["|"], []);
        if (llGetListLength(t) != 9 ||
            llList2String(t, 0) != "OGLSEASONS" ||
            llList2String(t, 1) != "1") return;
        string season = llList2String(t, 3);
        integer target = -1;
        if (season == "fruehling") target = 0;
        else if (season == "sommer") target = 1;
        else if (season == "herbst") target = 2;
        else if (season == "winter") target = 3;
        if (target < 0) return;
        m_lastSeen = llGetUnixTime();
        if (m_activeSeason != target) setSeason(target);
    }
    timer()
    {
        // Conservative fallback after weather module stops responding.
        if (m_lastSeen != 0 && llGetUnixTime() - m_lastSeen > 600)
        {
            m_lastSeen = 0;
            setSeason(0);
        }
    }
    touch_start(integer count)
    {
        // Only the owner can manually disable/re-enable seasonal switching.
        if (llDetectedKey(0) != llGetOwner()) return;
        ENABLED = !ENABLED;
        if (!ENABLED) setSeason(0);
        llOwnerSay(ENABLED ? "OGLSeasons: Automatik aktiv." :
            "OGLSeasons: Automatik aus, Fruehlingsansicht wiederhergestellt.");
    }
    changed(integer change)
    {
        if (change & (CHANGED_LINK | CHANGED_OWNER))
            llResetScript();
    }
    on_rez(integer param) { llResetScript(); }
}

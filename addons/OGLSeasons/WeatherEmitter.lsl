// Stadt NexVerse – optionaler Wetter-Emitter für OGLSeasons.
// In ein Eigentümer-Prim in der Region legen, das in der Luft
// (z.B. 10–20 m über dem Boden) positioniert wird.
// Pro Emitter nur lokale Partikel: mehrere Emitter für große Bereiche nötig.
// Kein automatischer Terrain-, Wasser- oder Physikeingriff.
integer CHANNEL = -835011;
string SOURCE_NAME = "OGLSeasons";
string m_weather = "heiter";
integer m_intensity = 0;
integer m_lastMessageTime = 0;

updateParticles()
{
    if (m_weather != "regen" && m_weather != "nieselregen" &&
        m_weather != "starkregen" && m_weather != "gewitter" &&
        m_weather != "sturm" && m_weather != "schneefall" &&
        m_weather != "schneesturm" && m_weather != "hagel")
    {
        llParticleSystem([]);
        return;
    }

    integer snow = (m_weather == "schneefall" || m_weather == "schneesturm");
    integer hail = (m_weather == "hagel");
    vector startColor = snow ? <0.97, 0.98, 1.0> : (hail ? <0.80, 0.95, 1.0> : <0.63, 0.77, 1.0>);
    vector startScale = snow ? <0.14, 0.14, 0.0> : (hail ? <0.08, 0.08, 0.0> : <0.035, 0.50, 0.0>);
    vector accel = snow ? <0.0, 0.0, -0.65> : <0.0, 0.0, -5.0>;
    integer count = 2 + (m_intensity / 13);
    if (count > 10) count = 10;
    float rate = 0.08;
    llParticleSystem([
        PSYS_PART_FLAGS, PSYS_PART_INTERP_COLOR_MASK | PSYS_PART_INTERP_SCALE_MASK,
        PSYS_SRC_PATTERN, PSYS_SRC_PATTERN_EXPLODE,
        PSYS_PART_START_COLOR, startColor,
        PSYS_PART_END_COLOR, startColor,
        PSYS_PART_START_ALPHA, 0.8,
        PSYS_PART_END_ALPHA, 0.0,
        PSYS_PART_START_SCALE, startScale,
        PSYS_PART_END_SCALE, snow ? <0.06, 0.06, 0.0> : <0.03, 0.25, 0.0>,
        PSYS_PART_MAX_AGE, snow ? 8.0 : 5.0,
        PSYS_SRC_BURST_RATE, rate,
        PSYS_SRC_BURST_PART_COUNT, count,
        PSYS_SRC_BURST_RADIUS, 7.5,
        PSYS_SRC_BURST_SPEED_MIN, snow ? 0.1 : 0.6,
        PSYS_SRC_BURST_SPEED_MAX, snow ? 0.8 : 1.4,
        PSYS_SRC_ACCEL, accel,
        PSYS_SRC_MAX_AGE, 0.0
    ]);
}

default
{
    state_entry()
    {
        llParticleSystem([]);
        llListen(CHANNEL, SOURCE_NAME, NULL_KEY, "");
        llSetTimerEvent(30.0);
        llOwnerSay("OGLSeasons: Wetter-Emitter aktiv. Warte auf Simulator-Wettersignal.");
    }

    listen(integer channel, string name, key id, string message)
    {
        if (channel != CHANNEL || name != SOURCE_NAME) return;
        list fields = llParseStringKeepNulls(message, ["|"], []);
        if (llGetListLength(fields) != 9 ||
            llList2String(fields, 0) != "OGLSEASONS" ||
            llList2String(fields, 1) != "1") return;
        string kind = llList2String(fields, 4);
        // Allow-list: fremde Chat-Nachrichten duerfen keine beliebigen
        // LSL-Befehle ausfuehren. Sie steuern ausschliesslich Partikel.
        if (kind != "heiter" && kind != "bewoelkt" &&
            kind != "nieselregen" && kind != "regen" &&
            kind != "starkregen" && kind != "schneefall" &&
            kind != "schneesturm" && kind != "nebel" &&
            kind != "gewitter" && kind != "sturm" &&
            kind != "hagel" && kind != "hitzewelle") return;
        m_weather = kind;
        m_intensity = llList2Integer(fields, 5);
        if (m_intensity < 0) m_intensity = 0;
        if (m_intensity > 100) m_intensity = 100;
        m_lastMessageTime = llGetUnixTime();
        updateParticles();
    }

    timer()
    {
        // Keine ewigen Niederschlagseffekte, falls das Wetter-Modul
        // deaktiviert oder die Region nach einem Update neu gestartet wurde.
        if (m_lastMessageTime != 0 && llGetUnixTime() - m_lastMessageTime > 600)
        {
            m_lastMessageTime = 0;
            m_weather = "heiter";
            m_intensity = 0;
            llParticleSystem([]);
        }
    }

    on_rez(integer startParam)
    {
        llResetScript();
    }
}

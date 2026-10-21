// Stadt NexVerse – saisonale Info-Tafel. Prim aufstellen und Script einfügen.
// Reagiert auf serverseitige OGLSEASONS-Region-Nachrichten.
integer CHANNEL = -835011;
integer lastReceived;
string germanSeason(string s)
{
    if (s == "fruehling") return "Frühling";
    if (s == "sommer") return "Sommer";
    if (s == "herbst") return "Herbst";
    if (s == "winter") return "Winter";
    return s;
}
string germanWeather(string w)
{
    if (w == "heiter") return "Heiter";
    if (w == "bewoelkt") return "Bewölkt";
    if (w == "nieselregen") return "Nieselregen";
    if (w == "regen") return "Regen";
    if (w == "starkregen") return "Starkregen";
    if (w == "schneefall") return "Schneefall";
    if (w == "schneesturm") return "Schneesturm";
    if (w == "nebel") return "Nebel";
    if (w == "gewitter") return "Gewitter";
    if (w == "sturm") return "Sturm";
    if (w == "hagel") return "Hagel";
    if (w == "hitzewelle") return "Hitzewelle";
    return w;
}
default
{
    state_entry()
    {
        llListen(CHANNEL, "OGLSeasons", NULL_KEY, "");
        llSetText("Stadt NexVerse\nWetterdaten werden geladen …", <0.8,0.95,1.0>, 1.0);
        llSetTimerEvent(45.0);
    }
    listen(integer channel, string speaker, key id, string message)
    {
        if (channel != CHANNEL || speaker != "OGLSeasons" || id != NULL_KEY) return;
        list t = llParseStringKeepNulls(message, ["|"], []);
        if (llGetListLength(t) != 9 ||
            llList2String(t, 0) != "OGLSEASONS" ||
            llList2String(t, 1) != "1") return;
        string season = germanSeason(llList2String(t, 3));
        string condition = germanWeather(llList2String(t, 4));
        string temp = llList2String(t, 6);
        string wind = llList2String(t, 7);
        string strength = llList2String(t, 5);
        llSetText("Stadt NexVerse\n" + season + " · " + condition +
            "\n" + temp + " °C · Wind " + wind + " km/h" +
            "\nIntensität: " + strength + " %", <0.8,0.95,1.0>, 1.0);
        lastReceived = llGetUnixTime();
    }
    timer()
    {
        if (lastReceived != 0 && llGetUnixTime() - lastReceived > 600)
        {
            lastReceived = 0;
            llSetText("Stadt NexVerse\nWetterstatus derzeit nicht verfügbar", <1.0,0.7,0.45>, 1.0);
        }
    }
}

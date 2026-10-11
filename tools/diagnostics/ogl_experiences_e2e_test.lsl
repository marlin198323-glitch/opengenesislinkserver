// SPDX-License-Identifier: MPL-2.0
// OpenGenesisLINK 0.9.3.10 Dev - NexExperiences E2E LSL-Test
// Testregion: OGL Developer Gen1; zuerst echte Experience zuweisen.
// Avatar A = Objekt-Eigentuemer, Avatar B = normaler Testbewohner.
// WICHTIG: Kein automatisches Allow. Keine API-Keys im Script.
integer gMenuChannel;
integer gListenHandle;
list gPending;
string gKvKey;
string gValueOriginal = "ogl09310-test-v1";
string gValueUpdated = "ogl09310-test-v2";

report(string text)
{
    llOwnerSay("[OGL XP TEST] " + text);
}

track(key requestId, string operation)
{
    if (requestId == NULL_KEY)
    {
        report("FAIL " + operation + ": Keine Datenserver-Request-ID.");
        return;
    }
    gPending += [requestId, operation];
    report("Gesendet: " + operation + " (Antwort folgt im dataserver-Event).");
}

showStatus()
{
    list details = llGetExperienceDetails(NULL_KEY);
    if (llGetListLength(details) < 6)
    {
        report("FAIL INFO: Script hat keine gueltige Experience-Zuordnung "
             + "oder der zentrale Experience-Dienst ist nicht erreichbar.");
        return;
    }
    report("Experience: " + llList2String(details, 0)
         + " | ID: " + llList2String(details, 2)
         + " | Aktiv: " + llList2String(details, 3)
         + " | Owner in Experience: "
         + (string)llAgentInExperience(llGetOwner()));
}

askForConsent(key resident)
{
    report("Berechtigung wird angefragt fuer Avatar " + (string)resident
         + ". Erwartet: bewusste Entscheidung im Viewer.");
    llRequestExperiencePermissions(resident,
        "OpenGenesisLINK 0.9.3.10: Experience-Test (keine automatische Freigabe)");
}

showMenu()
{
    if (gListenHandle != 0)
        llListenRemove(gListenHandle);
    gListenHandle = llListen(gMenuChannel, "", llGetOwner(), "");
    llSetTimerEvent(60.0);
    llDialog(llGetOwner(),
        "OGL NexExperiences Test\n"
        + "KV-Schluessel bleibt nach Script-/Robust-Neustart erhalten.\n"
        + "KV CAS: nur nach KV Anlegen testen.\n"
        + "KV Loeschen: separate Bestaetigung.",
        ["Info", "Anfrage", "KV Anlegen", "KV Lesen",
         "KV CAS", "KV Konflikt", "KV Groesse", "KV Anzahl",
         "KV Liste", "KV Fehlt", "KV Loeschen"],
        gMenuChannel);
}

default
{
    state_entry()
    {
        gPending = [];
        // Objekt-UUID bleibt bei Script-Reset und Robust-Restart gleich.
        // Keine fremden Experience-Keys veraendern.
        gKvKey = "ogl09310-e2e/" + (string)llGetKey();
        gMenuChannel = -1000 - (integer)llFrand(1000000000.0);
        gListenHandle = 0;
        report("Bereit. Eigentuemer beruehrt Prim fuer Testmenue.");
        report("Zweitavatar beruehrt Prim fuer Experience-Berechtigungsanfrage.");
        report("KV-Testschluessel: " + gKvKey);
        showStatus();
    }

    on_rez(integer start)
    {
        llResetScript();
    }

    changed(integer changes)
    {
        if (changes & CHANGED_OWNER)
            llResetScript();
    }

    touch_start(integer total)
    {
        integer i;
        for (i = 0; i < total; ++i)
        {
            key resident = llDetectedKey(i);
            if (resident == llGetOwner())
                showMenu();
            else
            {
                llRegionSayTo(resident, 0,
                    "OpenGenesisLINK Experience-Test: Berechtigungsanfrage "
                    + "wird gestellt. Bitte im Viewer bewusst Allow oder Block "
                    + "waehlen; ohne Zustimmung wird nichts freigegeben.");
                askForConsent(resident);
            }
        }
    }

    listen(integer channel, string name, key speaker, string message)
    {
        if (channel != gMenuChannel || speaker != llGetOwner())
            return;

        if (message == "Info")
            showStatus();
        else if (message == "Anfrage")
            askForConsent(speaker);
        else if (message == "KV Anlegen")
            track(llCreateKeyValue(gKvKey, gValueOriginal), "KV Anlegen");
        else if (message == "KV Lesen")
            track(llReadKeyValue(gKvKey), "KV Lesen");
        else if (message == "KV CAS")
            track(llUpdateKeyValue(gKvKey, gValueUpdated, TRUE,
                gValueOriginal), "KV CAS");
        else if (message == "KV Konflikt")
            track(llUpdateKeyValue(gKvKey, "nicht-schreiben", TRUE,
                "absichtlich-falscher-altwert"), "KV Konflikt");
        else if (message == "KV Groesse")
            track(llDataSizeKeyValue(), "KV Groesse");
        else if (message == "KV Anzahl")
            track(llKeyCountKeyValue(), "KV Anzahl");
        else if (message == "KV Liste")
            track(llKeysKeyValue(0, 100), "KV Liste");
        else if (message == "KV Fehlt")
            track(llReadKeyValue(gKvKey + "/nicht-existent"),
                "KV Fehlt");
        else if (message == "KV Loeschen")
            llDialog(llGetOwner(),
                "ACHTUNG: Nur den eigenen OGL E2E-Testschluessel loeschen? "
                + "Damit endet auch der Persistenznachweis.",
                ["Ja Loeschen", "Abbrechen"], gMenuChannel);
        else if (message == "Ja Loeschen")
            track(llDeleteKeyValue(gKvKey), "KV Loeschen");
        else if (message == "Abbrechen")
            report("Loeschen abgebrochen.");
    }

    experience_permissions(key resident)
    {
        if (llAgentInExperience(resident) == 1)
            report("PASS EXPERIENCE ALLOW: " + (string)resident
                 + " hat ein aktives, serverseitig bestaetigtes Recht.");
        else
            report("FAIL EXPERIENCE ALLOW: Event ohne positives "
                 + "llAgentInExperience fuer " + (string)resident);
    }

    experience_permissions_denied(key resident, integer code)
    {
        report("EXPERIENCE DENIED: " + (string)resident
             + " | XP_ERROR=" + (string)code
             + " (" + llGetExperienceErrorMessage(code) + ")");
        if (llAgentInExperience(resident) == 0)
            report("PASS: Kein aktives Experience-Recht nach Deny/Block.");
        else
            report("FAIL: Resident hat nach Deny/Block weiterhin ein Recht.");
    }

    dataserver(key requestId, string data)
    {
        integer idx = llListFindList(gPending, [requestId]);
        if (idx < 0)
        {
            report("WARN: Nicht zugeordnete Datenserver-Antwort.");
            return;
        }
        string operation = llList2String(gPending, idx + 1);
        gPending = llDeleteSubList(gPending, idx, idx + 1);

        list fields = llParseStringKeepNulls(data, [","], []);
        integer success = (llList2String(fields, 0) == "1");
        integer code = (integer)llList2String(fields, 1);

        if (operation == "KV Konflikt")
        {
            if (!success && code == XP_ERROR_RETRY_UPDATE)
                report("PASS KV Konflikt: Compare-and-Swap verhindert "
                     + "Ueberschreiben mit falschem Altwert.");
            else
                report("FAIL KV Konflikt: Erwartet 0,"
                     + (string)XP_ERROR_RETRY_UPDATE + " erhalten " + data);
            return;
        }
        if (operation == "KV Fehlt")
        {
            if (!success && code == XP_ERROR_KEY_NOT_FOUND)
                report("PASS KV Fehlt: Schluessel existiert nicht.");
            else
                report("FAIL KV Fehlt: Erwartet 0,"
                     + (string)XP_ERROR_KEY_NOT_FOUND + " erhalten " + data);
            return;
        }
        if (!success)
        {
            report("FAIL " + operation + ": XP_ERROR=" + (string)code
                 + " (" + llGetExperienceErrorMessage(code) + ")");
            return;
        }

        // Bei Read und CAS auch den Inhalt pruefen, nicht nur Status 1.
        if (operation == "KV Lesen")
        {
            string value = llGetSubString(data, 2, -1);
            if (value != gValueOriginal && value != gValueUpdated)
            {
                report("FAIL KV Lesen: Unerwarteter Datenwert: " + value);
                return;
            }
        }
        if (operation == "KV CAS" &&
            llGetSubString(data, 2, -1) != gValueUpdated)
        {
            report("FAIL KV CAS: Server bestaetigt falschen neuen Wert.");
            return;
        }

        report("PASS " + operation + ": " + data);
    }

    timer()
    {
        llSetTimerEvent(0.0);
        if (gListenHandle != 0)
        {
            llListenRemove(gListenHandle);
            gListenHandle = 0;
        }
    }
}

// SPDX-License-Identifier: MPL-2.0
// OGL 0.9.3.10 Master Bank - NUR MANUELLER OWNER-TEST.
// 1. Dieses Script in ein bewusst registriertes Bank-Root-Prim legen.
// 2. Admin registriert Root-Objekt-UUID / SCRIPT-INVENTARITEM-UUID.
// 3. Bankoperator erteilt PERMISSION_DEBIT nach bewusstem Dialog.
// 4. TARGET_AVATAR mit echter lokalen oder anwesenden HG-Avatar-UUID ersetzen.
// Kein API-Key, kein freier Touch-Faucet, kein unlimitierter Timer.
// llNexBankPayout kann NIE aus dem privaten Avatar-Wallet bezahlen.
key TARGET_AVATAR = NULL_KEY;
integer AMOUNT_NV = 1;

default
{
    state_entry()
    {
        llOwnerSay("Masterbank-Test: Owner beruehrt Prim fuer Debit-Freigabe "
                 + "oder kontrollierte Testauszahlung.");
    }

    touch_start(integer total)
    {
        if (llDetectedKey(0) != llGetOwner())
            return;
        if ((llGetPermissions() & PERMISSION_DEBIT) == 0 ||
            llGetPermissionsKey() != llGetOwner())
        {
            llOwnerSay("Bankoperator muss PERMISSION_DEBIT ausdruecklich bestaetigen.");
            llRequestPermissions(llGetOwner(), PERMISSION_DEBIT);
            return;
        }
        if (TARGET_AVATAR == NULL_KEY || AMOUNT_NV <= 0)
        {
            llOwnerSay("TEST NICHT GESTARTET: TARGET_AVATAR und Betrag konfigurieren.");
            return;
        }
        key txn = llNexBankPayout(TARGET_AVATAR, AMOUNT_NV);
        llOwnerSay("Masterbank-Transaktion angefragt: " + (string)txn);
    }

    run_time_permissions(integer permissions)
    {
        if (permissions & PERMISSION_DEBIT)
            llOwnerSay("Debit-Freigabe erteilt. Owner kann Test erneut ausloesen.");
        else
            llOwnerSay("Debit-Freigabe verweigert, keine Auszahlung.");
    }

    transaction_result(key txn, integer success, string data)
    {
        if (success)
            llOwnerSay("NV$ Auszahlung bestaetigt: " + data +
                       " (Transaktion " + (string)txn + ")");
        else
            llOwnerSay("NV$ Auszahlung verweigert: " + data +
                       " (Transaktion " + (string)txn + ")");
    }
}

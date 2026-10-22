// SPDX-License-Identifier: MPL-2.0
// Real MariaDB opt-in archive regression in the existing grid database.
using System;
using NexVerse.Server.Api;

string connection = Environment.GetEnvironmentVariable("NEX_PORTAL_IM_MARIADB");
if (string.IsNullOrWhiteSpace(connection))
    throw new InvalidOperationException("NEX_PORTAL_IM_MARIADB is required");
INexCitizenImStore store = new NexCitizenImMySqlStore(
    "OpenSim.Data.MySQL.dll", connection, 30);
Guid alice = Guid.NewGuid(), bob = Guid.NewGuid();
Guid event1 = Guid.NewGuid(), event2 = Guid.NewGuid();
try
{
    Check(!store.IsEnabled(alice) && !store.IsEnabled(bob),
        "default must be opted out");
    Check(!store.Append(event1, alice, bob, "Alice", "unconsented", "portal"),
        "no archive before explicit consent");
    Check(store.Read(alice, 0, 20).Count == 0 &&
        store.Read(bob, 0, 20).Count == 0,
        "unconsented data persisted");

    store.SetEnabled(alice, true);
    Check(store.IsEnabled(alice), "Alice opt-in not persisted");
    Check(store.Append(event1, alice, bob, "Alice", "first", "portal"),
        "opted-in sender not archived");
    Check(!store.Append(event1, alice, bob, "Alice", "first", "portal"),
        "duplicate event was archived twice");
    Check(store.Read(alice, 0, 20).Count == 1 &&
        store.Read(bob, 0, 20).Count == 0,
        "participant opt-in isolation failed");

    store.SetEnabled(bob, true);
    Check(store.Append(event2, bob, alice, "Bob", "second", "viewer"),
        "reply not archived");
    var aliceRows = store.Read(alice, 0, 20);
    var bobRows = store.Read(bob, 0, 20);
    Check(aliceRows.Count == 2 && bobRows.Count == 1,
        "wrong participant archive counts");
    Check(aliceRows[1].source == "viewer" &&
        aliceRows[1].message == "second",
        "viewer reply content mismatch");
    Check(store.Read(alice, aliceRows[0].seq, 100).Count == 1,
        "cursor pagination failed");
    Check(store.Read(alice, 0, 100, bob).Count == 2,
        "peer filter failed");
    Check(store.Conversations(alice, 20).Count == 1,
        "conversation aggregation failed");

    INexCitizenImStore restarted = new NexCitizenImMySqlStore(
        "OpenSim.Data.MySQL.dll", connection, 30);
    Check(restarted.IsEnabled(bob) &&
        restarted.Read(bob, 0, 20).Count == 1,
        "persisted state lost on restart");

    store.SetEnabled(alice, false);
    Check(!store.IsEnabled(alice) &&
        store.Read(alice, 0, 20).Count == 0 &&
        store.Read(bob, 0, 20).Count == 1,
        "opt-out must purge only the requesting owner");
    store.DeleteHistory(bob);
    Check(store.IsEnabled(bob) && store.Read(bob, 0, 20).Count == 0,
        "history delete must preserve consent setting");
    Console.WriteLine("MariaDB portal IM archive real SQL regression: OK");
}
finally
{
    store.SetEnabled(alice, false);
    store.SetEnabled(bob, false);
}
static void Check(bool result, string message)
{
    if (!result) throw new InvalidOperationException("NexPortalIM SQL: " + message);
}

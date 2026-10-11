// SPDX-License-Identifier: MPL-2.0

using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using NexVerse.Core.Experiences;

internal static class Program
{
    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }


    private static void VerifyViewerInfoCapabilityIds()
    {
        string cap = "/CAPS/" + Guid.NewGuid().ToString("D");
        Guid first = Guid.NewGuid();
        Guid second = Guid.NewGuid();
        string id = first.ToString("D");
        string other = second.ToString("D");

        void Accept(string path, string[] query, params Guid[] expected)
        {
            Require(NexExperienceInfoRequestIds.TryParse(path, cap, query, out Guid[] actual) &&
                    actual.SequenceEqual(expected),
                "GetExperienceInfo rejected a valid Firestorm URL: " + path);
        }

        void Deny(string path, string[] query)
        {
            Require(!NexExperienceInfoRequestIds.TryParse(path, cap, query, out Guid[] ids) &&
                    ids.Length == 0,
                "GetExperienceInfo accepted a forged/ambiguous URL: " + path);
        }

        // Real Firestorm CAPS routing plus a compatible query-based client.
        Accept(cap + "/id/" + id, null, first);
        Accept(cap + "/id/" + id + "/", null, first);
        Accept(cap + "/" + id, null, first);
        Accept(cap, new[] { id }, first);
        Accept(cap + "/", new[] { id, other }, first, second);

        // A CAP may never become a generic Experience query endpoint.
        Deny(cap, null);
        Deny(cap + "/id/", null);
        Deny(cap + "/id/not-an-id", null);
        Deny(cap + "/id/" + Guid.Empty.ToString("D"), null);
        Deny(cap + "/id/" + id + "/garbage", null);
        Deny(cap + "/id/" + id, new[] { other });
        Deny(cap + "-foreign/id/" + id, null);
        Deny("/CAPS/foreign/id/" + id, null);
        Deny(cap, new[] { id, "bad-value" });
        Deny(cap, new[] { Guid.Empty.ToString("D") });
        Deny(cap, new string[65]);
    }

    private static void Main()
    {
        VerifyViewerInfoCapabilityIds();
        string root =
            Path.Combine(
                Path.GetTempPath(),
                "ogl-experiences-" +
                Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        string path =
            Path.Combine(
                root,
                "experiences.json");

        Guid owner = Guid.NewGuid();
        Guid admin = Guid.NewGuid();
        Guid contributor = Guid.NewGuid();
        Guid resident = Guid.NewGuid();
        Guid blocked = Guid.NewGuid();
        Guid estate = Guid.NewGuid();
        Guid parcel = Guid.NewGuid();
        Guid script = Guid.NewGuid();

        NexExperienceStore store =
            new NexExperienceStore(path);

        // A second Robust process must never acquire the writer lease,
        // even when it is running on the same machine.
        bool secondWriterRejected = false;
        try { using var secondWriter = new NexExperienceStore(path); }
        catch (InvalidOperationException) { secondWriterRejected = true; }
        Require(secondWriterRejected,
            "multiple active Robust writers could corrupt the JSON authority");

        NexExperience created =
            store.Create(
                owner,
                Guid.NewGuid(),
                "CI Experience",
                "Chapter 11 regression",
                NexExperienceMaturity.Moderate,
                "ci");

        Require(
            created.OwnerId == owner &&
            created.Admins.Contains(owner),
            "owner/admin foundation failed");

        // Firestorm "Acquire Experience" is free but strictly bounded and
        // owner-bound on the central authoritative Robust store.
        Guid viewerOwner = Guid.NewGuid();
        NexExperience firstViewer = store.CreateViewerExperience(
            viewerOwner, "simulator:viewer");
        Require(firstViewer.OwnerId == viewerOwner &&
                firstViewer.Admins.Contains(viewerOwner) &&
                firstViewer.GroupId == Guid.Empty &&
                firstViewer.Name.StartsWith("New Experience ", StringComparison.Ordinal),
            "viewer purchase must create a valid own Experience without payment");
        bool foreignEditDenied = false;
        try
        {
            store.UpdateViewerExperience(Guid.NewGuid(),
                firstViewer.ExperienceId, "Hijacked Experience", "",
                NexExperienceMaturity.General, true, "another-avatar");
        }
        catch (UnauthorizedAccessException) { foreignEditDenied = true; }
        Require(foreignEditDenied,
            "viewer:manage cannot update someone else's Experience");
        NexExperience renamedViewer = store.UpdateViewerExperience(viewerOwner,
            firstViewer.ExperienceId, "NexVerse Viewer Demo", "editable",
            NexExperienceMaturity.Moderate, true, "simulator:viewer");
        Require(renamedViewer.Name == "NexVerse Viewer Demo" &&
                renamedViewer.Description == "editable" &&
                renamedViewer.Maturity == NexExperienceMaturity.Moderate &&
                renamedViewer.OwnerId == viewerOwner,
            "owner profile editor did not persist name, description and maturity");
        for (int i = 1; i < NexExperienceStore.MaxViewerExperiencesPerResident; i++)
            store.CreateViewerExperience(viewerOwner, "simulator:viewer");
        bool quotaRejected = false;
        try { store.CreateViewerExperience(viewerOwner, "simulator:viewer"); }
        catch (InvalidOperationException) { quotaRejected = true; }
        Require(quotaRejected, "viewer purchase can exceed the per-avatar quota");
        Require(store.GetResidentLists(viewerOwner).Owned.Length ==
                NexExperienceStore.MaxViewerExperiencesPerResident,
            "viewer owner list is inconsistent with purchased Experiences");
        NexExperienceSnapshot acquiredSnapshot =
            JsonSerializer.Deserialize<NexExperienceSnapshot>(File.ReadAllText(path));
        Require(acquiredSnapshot.Experiences.Count(x => x.OwnerId == viewerOwner) ==
                NexExperienceStore.MaxViewerExperiencesPerResident,
            "purchases/edits did not reach persistent snapshot");

        store.SetRole(
            created.ExperienceId,
            owner,
            admin,
            "admin",
            true,
            "ci");
        store.SetRole(
            created.ExperienceId,
            admin,
            contributor,
            "contributor",
            true,
            "ci");

        Require(
            store.Get(created.ExperienceId).Contributors.Contains(contributor),
            "role persistence failed");

        // Firestorm read-only role tabs must derive only this resident's own
        // roles, without leaking owner/admin lists to unrelated residents.
        var ownerLists = store.GetResidentLists(owner);
        var adminLists = store.GetResidentLists(admin);
        var contributorLists = store.GetResidentLists(contributor);
        var unrelatedLists = store.GetResidentLists(Guid.NewGuid());
        Require(ownerLists.Owned.Contains(created.ExperienceId),
            "AgentExperiences must include owner's experience");
        Require(adminLists.Admin.Contains(created.ExperienceId),
            "GetAdminExperiences must include assigned admin role");
        Require(contributorLists.Contributor.Contains(created.ExperienceId),
            "GetCreatorExperiences must include contributor role");
        Require(!adminLists.Owned.Contains(created.ExperienceId) &&
                !contributorLists.Owned.Contains(created.ExperienceId) &&
                !unrelatedLists.Admin.Contains(created.ExperienceId) &&
                !unrelatedLists.Contributor.Contains(created.ExperienceId),
            "Firestorm role lists leaked another resident's authority");
        Require(store.GetGroupExperiences(created.GroupId).Contains(created.ExperienceId),
            "GroupExperiences must include enabled group-owned experience");
        Require(store.GetGroupExperiences(Guid.NewGuid()).Length == 0,
            "GroupExperiences returned experiences for unrelated group");


        store.SetResidentPermission(
            created.ExperienceId,
            admin,
            resident,
            NexExperiencePermissionStatus.Allowed,
            "ci");
        store.SetResidentPermission(
            created.ExperienceId,
            owner,
            blocked,
            NexExperiencePermissionStatus.Blocked,
            "ci");

        Require(
            store.GetResidentPermission(
                created.ExperienceId,
                resident) ==
                NexExperiencePermissionStatus.Allowed,
            "allowed resident failed");
        Require(
            store.GetResidentPermission(
                created.ExperienceId,
                blocked) ==
                NexExperiencePermissionStatus.Blocked,
            "blocked resident failed");
        Guid viewerResident = Guid.NewGuid();
        store.SetOwnResidentPermission(created.ExperienceId, viewerResident,
            NexExperiencePermissionStatus.Allowed, "viewer");
        Require(store.GetResidentLists(viewerResident).Allowed.Contains(created.ExperienceId),
            "Firestorm GetExperiences allowed list");
        store.SetOwnResidentPermission(created.ExperienceId, viewerResident,
            NexExperiencePermissionStatus.Blocked, "viewer");
        Require(store.GetResidentLists(viewerResident).Blocked.Contains(created.ExperienceId) &&
            !store.GetResidentLists(viewerResident).Allowed.Contains(created.ExperienceId),
            "Firestorm ExperiencePreferences block/allow isolation");
        Require(!store.GetResidentLists(resident).Blocked.Contains(created.ExperienceId),
            "one resident's block leaked to another");
        // Read the persisted bytes without opening another writer instance:
        // an independent second writer must always be rejected.
        NexExperienceSnapshot persistedViewer = JsonSerializer.Deserialize<NexExperienceSnapshot>(
            File.ReadAllText(path));
        Require(persistedViewer.Experiences.Single(x => x.ExperienceId == created.ExperienceId)
                .BlockedResidents.Contains(viewerResident),
            "Firestorm permission did not reach the durable snapshot");
        store.SetOwnResidentPermission(created.ExperienceId, viewerResident,
            NexExperiencePermissionStatus.None, "viewer");
        Require(store.GetResidentPermission(created.ExperienceId, viewerResident) ==
            NexExperiencePermissionStatus.None, "Firestorm Forget did not revoke consent");
        Require(store.GetResidentLists(owner).Owned.Contains(created.ExperienceId),
            "GetExperiences owned list");
        bool blockOwnerRejected = false;
        try
        {
            store.SetOwnResidentPermission(created.ExperienceId, owner,
                NexExperiencePermissionStatus.Blocked, "viewer");
        }
        catch (InvalidOperationException) { blockOwnerRejected = true; }
        Require(blockOwnerRejected, "owner cannot block their own Experience");


        store.SetLocationPolicy(
            created.ExperienceId,
            owner,
            "estate",
            estate,
            true,
            true,
            "ci");
        store.SetLocationPolicy(
            created.ExperienceId,
            admin,
            "parcel",
            parcel,
            true,
            true,
            "ci");

        Require(
            store.IsLocationAllowed(
                created.ExperienceId,
                estate,
                parcel),
            "allowed estate/parcel failed");
        Require(
            !store.IsLocationAllowed(
                created.ExperienceId,
                Guid.NewGuid(),
                parcel),
            "estate allow-list was not enforced");

        store.BindScript(
            created.ExperienceId,
            owner,
            script,
            "ci");

        Require(
            store.ResolveScript(script) ==
                created.ExperienceId,
            "script binding failed");

        Require(
            store.CreateKeyValue(
                created.ExperienceId,
                script,
                "alpha",
                "one"),
            "K/V create failed");

        Require(
            store.TryReadKeyValue(
                created.ExperienceId,
                script,
                "alpha",
                out string value) &&
            value == "one",
            "K/V read failed");

        Require(
            !store.UpdateKeyValue(
                created.ExperienceId,
                script,
                "alpha",
                "two",
                true,
                "stale",
                out bool retryMismatch) &&
            retryMismatch,
            "checked K/V retry semantics failed");

        Require(
            store.UpdateKeyValue(
                created.ExperienceId,
                script,
                "alpha",
                "two",
                true,
                "one",
                out retryMismatch) &&
            !retryMismatch,
            "checked K/V update failed");

        Require(
            !store.UpdateKeyValue(
                created.ExperienceId,
                script,
                "missing-checked",
                "should-not-be-created",
                true,
                string.Empty,
                out retryMismatch) && retryMismatch,
            "CAS update of missing key must fail, even with empty original");
        Require(
            !store.TryReadKeyValue(
                created.ExperienceId,
                script,
                "missing-checked",
                out _),
            "failed checked update must not create a key");
        NexExperienceSnapshot persistedAfterCas = JsonSerializer.Deserialize<NexExperienceSnapshot>(
            File.ReadAllText(path));
        Require(!persistedAfterCas.Experiences.Single(
                x => x.ExperienceId == created.ExperienceId).KeyValues.ContainsKey("missing-checked"),
            "failed checked update must not persist a phantom key");
        Require(
            store.UpdateKeyValue(
                created.ExperienceId,
                script,
                "created-by-update",
                "value",
                false,
                string.Empty,
                out retryMismatch),
            "missing-key update did not create");

        Require(
            store.GetKeyCount(
                created.ExperienceId,
                script) == 2,
            "K/V count mismatch");

        Require(
            store.ListKeys(
                created.ExperienceId,
                script,
                0,
                10).SequenceEqual(
                    new[]
                    {
                        "alpha",
                        "created-by-update"
                    }),
            "K/V key listing mismatch");

        Require(
            store.GetDataSize(
                created.ExperienceId,
                script) > 0,
            "K/V data size missing");

        Require(
            store.DeleteKeyValue(
                created.ExperienceId,
                script,
                "alpha"),
            "K/V delete failed");

        Require(
            store.GetLogs(
                created.ExperienceId,
                0,
                100).Count >= 6,
            "Experience audit log history missing");

        store.Dispose();
        bool disposedWriterRejected = false;
        try { store.Get(created.ExperienceId); }
        catch (ObjectDisposedException) { disposedWriterRejected = true; }
        Require(disposedWriterRejected,
            "released authority must reject stale reads and writes");

        // Explicit stop/restart is required before another Robust writer
        // is allowed to load the persisted committed data.
        NexExperienceStore reopened =
            new NexExperienceStore(path);

        Require(
            reopened.Get(created.ExperienceId) != null &&
            reopened.ResolveScript(script) ==
                created.ExperienceId &&
            reopened.GetKeyCount(
                created.ExperienceId,
                script) == 1,
            "Experience persistence/reopen failed");

        // A disabled Experience must immediately lose *all* script K/V
        // authority even if its script binding and stored data remain.
        reopened.UpdateProfile(created.ExperienceId, owner,
            "CI Experience", "temporarily disabled", created.GroupId,
            NexExperienceMaturity.Moderate, false, "ci");
        Require(reopened.GetViewerOwnedExperienceIds(owner).Contains(created.ExperienceId),
            "owner must still see disabled Experience to re-enable it in Firestorm");
        Require(!reopened.GetResidentLists(admin).Admin.Contains(created.ExperienceId) &&
                !reopened.GetResidentLists(contributor).Contributor.Contains(created.ExperienceId) &&
                !reopened.GetGroupExperiences(created.GroupId).Contains(created.ExperienceId),
            "Disabled experiences must disappear from Firestorm role/group lists");
        Require(reopened.GetResidentPermission(created.ExperienceId, resident) ==
            NexExperiencePermissionStatus.None,
            "disabled Experience still granted resident permissions");
        bool disabledReadRejected = false;
        bool disabledWriteRejected = false;
        try
        {
            reopened.TryReadKeyValue(created.ExperienceId, script,
                "created-by-update", out _);
        }
        catch (InvalidOperationException) { disabledReadRejected = true; }
        try
        {
            reopened.CreateKeyValue(created.ExperienceId, script, "blocked", "write");
        }
        catch (InvalidOperationException) { disabledWriteRejected = true; }
        Require(disabledReadRejected && disabledWriteRejected,
            "disabled Experience still allowed script K/V access");
        reopened.UpdateProfile(created.ExperienceId, owner,
            "CI Experience", "reenabled", created.GroupId,
            NexExperienceMaturity.Moderate, true, "ci");
        Require(reopened.TryReadKeyValue(created.ExperienceId, script,
            "created-by-update", out string resumedValue) && resumedValue == "value",
            "Experience enable should restore previously stored script data");

        reopened.Delete(
            created.ExperienceId,
            owner,
            "ci");

        Require(
            reopened.Get(created.ExperienceId) == null &&
            reopened.ResolveScript(script) == Guid.Empty,
            "Experience deletion cleanup failed");

        // Pending LSL consent never itself writes central permission. The
        // viewer has to Allow, Block or Forget through authenticated CAPS.
        // Exercise independent residents, duplicate scripts, expiration,
        // per-resident bounds and simulator-region cancellation.
        var pending = new NexPendingExperienceQueue();
        Guid pendingExperience = Guid.NewGuid();
        Guid pendingRegion = Guid.NewGuid();
        Guid pendingAvatar = Guid.NewGuid();
        Guid pendingObject = Guid.NewGuid();
        Guid pendingScript = Guid.NewGuid();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        int callbackStatus = -1;
        NexPendingExperienceRequest consent = new()
        {
            ResidentId = pendingAvatar,
            ExperienceId = pendingExperience,
            RegionId = pendingRegion,
            ScriptId = pendingScript,
            ObjectId = pendingObject,
            ParcelId = parcel,
            Deadline = now.AddSeconds(60),
            Completion = result => callbackStatus = result
        };
        Require(pending.TryAdd(consent, now), "pending first consent failed");
        Require(!pending.TryAdd(consent, now),
            "same script/resident must not duplicate pending consent");
        Require(pending.Take(Guid.NewGuid(), pendingExperience).Length == 0 &&
                pending.Count == 1,
            "another avatar could drain resident consent");
        var resolved = pending.Take(pendingAvatar, pendingExperience);
        Require(resolved.Length == 1 && pending.Count == 0,
            "viewer consent must resolve only matching resident/experience");
        resolved[0].Completion(0);
        Require(callbackStatus == 0, "deferred LSL success callback lost");
        Require(pending.TryAdd(consent, now), "re-request after consent is resolved");
        Require(pending.Expire(now.AddSeconds(61)).Length == 1 &&
                pending.Count == 0,
            "unanswered consent did not expire");

        // Two independent scripts requesting the same Experience for one
        // resident must not both be denied by one native ScriptAnswerYes.
        Guid secondScript = Guid.NewGuid();
        Guid secondObject = Guid.NewGuid();
        Require(pending.TryAdd(consent, now), "first parallel consent failed");
        Require(pending.TryAdd(new NexPendingExperienceRequest
        {
            ResidentId = pendingAvatar,
            ExperienceId = pendingExperience,
            RegionId = pendingRegion,
            ObjectId = secondObject,
            ScriptId = secondScript,
            Deadline = now.AddSeconds(60),
            Completion = _ => { }
        }, now), "second parallel consent failed");
        Require(pending.TakeSpecific(pendingAvatar, pendingExperience,
                pendingObject, pendingScript).Length == 1 &&
                pending.Count == 1,
            "one script Deny drained another script's request");
        Require(pending.TakeSpecific(pendingAvatar, pendingExperience,
                pendingObject, pendingScript).Length == 0 &&
                pending.Count == 1,
            "duplicate native Deny should not resolve a second request");
        Require(pending.TakeSpecific(pendingAvatar, pendingExperience,
                secondObject, pendingScript).Length == 0 &&
                pending.Count == 1,
            "mismatched object and script must never resolve consent");
        Require(pending.Take(pendingAvatar, pendingExperience).Length == 1 &&
                pending.Count == 0,
            "central viewer CAP decision still resolves remaining requests");

        for (int i = 0; i < 16; i++)
        {
            Require(pending.TryAdd(new NexPendingExperienceRequest
            {
                ResidentId = pendingAvatar,
                ExperienceId = pendingExperience,
                RegionId = pendingRegion,
                ObjectId = pendingObject,
                ScriptId = Guid.NewGuid(),
                Deadline = now.AddSeconds(60),
                Completion = _ => { }
            }, now), "16 pending scripts should respect per-resident bound");
        }
        Require(!pending.TryAdd(consent, now),
            "resident limit must reject excessive pending requests");
        Require(pending.CancelRegion(Guid.NewGuid()).Length == 0 &&
                pending.Count == 16,
            "unrelated region canceled pending requests");
        Require(pending.CancelRegion(pendingRegion).Length == 16 &&
                pending.Count == 0,
            "region shutdown did not cancel its pending requests");
        Require(!pending.TryAdd(new NexPendingExperienceRequest
        {
            ResidentId = pendingAvatar,
            ExperienceId = pendingExperience,
            RegionId = pendingRegion,
            ObjectId = pendingObject,
            ScriptId = pendingScript,
            Deadline = now.AddMinutes(10),
            Completion = _ => { }
        }, now), "unbounded consent deadline accepted");
        // Logout/child-agent, script reset and removal must discard native
        // handlers without delivering stale success/denied events to scripts.
        int staleEvents = 0;
        int cancellations = 0;
        Guid anotherRegion = Guid.NewGuid();
        Guid otherResident = Guid.NewGuid();
        Guid scriptReset = Guid.NewGuid();
        NexPendingExperienceRequest Request(Guid region, Guid resident, Guid script) =>
            new()
            {
                ResidentId = resident,
                ExperienceId = pendingExperience,
                RegionId = region,
                ObjectId = pendingObject,
                ScriptId = script,
                Deadline = now.AddSeconds(60),
                Completion = _ => staleEvents++,
                Cancel = () => cancellations++
            };
        Require(pending.TryAdd(Request(pendingRegion, pendingAvatar, scriptReset), now),
            "script reset fixture rejected");
        Require(pending.TryAdd(Request(pendingRegion, otherResident, Guid.NewGuid()), now),
            "unrelated resident fixture rejected");
        Require(pending.TryAdd(Request(anotherRegion, pendingAvatar, Guid.NewGuid()), now),
            "unrelated region fixture rejected");
        var onReset = pending.CancelScript(pendingRegion, scriptReset);
        Require(onReset.Length == 1 && pending.Count == 2,
            "script reset must cancel only the matching region/script");
        foreach (var removed in onReset) removed.Cancel?.Invoke();
        var onLogout = pending.CancelResident(pendingRegion, otherResident);
        Require(onLogout.Length == 1 && pending.Count == 1,
            "logout must cancel only the matching region/resident");
        foreach (var removed in onLogout) removed.Cancel?.Invoke();
        Require(pending.CancelResident(pendingRegion, pendingAvatar).Length == 0 &&
            pending.Count == 1, "old region logout canceled another region");
        var onOtherRegionShutdown = pending.CancelRegion(anotherRegion);
        foreach (var removed in onOtherRegionShutdown) removed.Cancel?.Invoke();
        Require(pending.Count == 0 && cancellations == 3 && staleEvents == 0,
            "native listener cleanup emitted a stale LSL event");
        Require(pending.CancelScript(Guid.Empty, scriptReset).Length == 0 &&
            pending.CancelResident(pendingRegion, Guid.Empty).Length == 0,
            "invalid cancellation keys must not drain queue");


        // Old bearer URLs MUST NOT gain access if an avatar logs out and
        // returns to the exact same region using a new session.
        NexExperienceCapLeaseRegistry leases = new();
        Guid capRegion = Guid.NewGuid(), capResident = Guid.NewGuid();
        Guid capOtherRegion = Guid.NewGuid(), capOtherResident = Guid.NewGuid();
        var originalCap = leases.Issue(capRegion, capResident);
        var otherAvatarCap = leases.Issue(capRegion, capOtherResident);
        var otherRegionCap = leases.Issue(capOtherRegion, capResident);
        Require(leases.IsCurrent(originalCap) && leases.Count == 3,
            "new viewer bearer capability leases must be active");
        var nextLoginCap = leases.Issue(capRegion, capResident);
        Require(!leases.IsCurrent(originalCap) && originalCap.IsRevoked &&
                leases.IsCurrent(nextLoginCap) && leases.Count == 3,
            "new login to same region must revoke original URL without growing inventory");
        leases.InvalidateResident(capRegion, capResident);
        Require(!leases.IsCurrent(nextLoginCap) &&
                leases.IsCurrent(otherAvatarCap) &&
                leases.IsCurrent(otherRegionCap) && leases.Count == 2,
            "logout or root-to-child transition only revokes matching resident and region");
        var thirdLogin = leases.Issue(capRegion, capResident);
        Require(leases.IsCurrent(thirdLogin) &&
                !leases.IsCurrent(originalCap),
            "returning resident must never revive old URL");
        leases.InvalidateRegion(capRegion);
        Require(!leases.IsCurrent(thirdLogin) &&
                !leases.IsCurrent(otherAvatarCap) &&
                leases.IsCurrent(otherRegionCap) && leases.Count == 1,
            "region shutdown revokes all region URLs without touching another region");
        leases.InvalidateAll();
        Require(leases.Count == 0 && !leases.IsCurrent(otherRegionCap) &&
                !leases.IsCurrent(null),
            "simulator shutdown revokes every remaining URL");
        bool invalidLeaseRejected = false;
        try { leases.Issue(Guid.Empty, capResident); }
        catch (ArgumentException) { invalidLeaseRejected = true; }
        Require(invalidLeaseRejected, "empty region must not issue a capability");

        reopened.Dispose();

        // Invalid JSON and an interrupted *initial* write must fail closed.
        // Never silently initialize an empty permission database.
        string nullSnapshot = Path.Combine(root, "null-experiences.json");
        File.WriteAllText(nullSnapshot, "null");
        bool nullRejected = false;
        try { using var invalid = new NexExperienceStore(nullSnapshot); }
        catch (InvalidOperationException) { nullRejected = true; }
        Require(nullRejected, "null snapshot silently reset Experience authority");

        string truncatedSnapshot = Path.Combine(root, "truncated-experiences.json");
        File.WriteAllText(truncatedSnapshot, "{bad");
        bool invalidJsonRejected = false;
        try { using var invalid = new NexExperienceStore(truncatedSnapshot); }
        catch (JsonException) { invalidJsonRejected = true; }
        Require(invalidJsonRejected, "corrupt JSON did not prevent Robust startup");

        string unfinished = Path.Combine(root, "interrupted-first-write.json");
        File.WriteAllText(unfinished + ".tmp", "{incomplete");
        bool incompleteRejected = false;
        try { using var invalid = new NexExperienceStore(unfinished); }
        catch (InvalidOperationException) { incompleteRejected = true; }
        Require(incompleteRejected, "incomplete initial commit discarded consent database");

        // The existing primary snapshot always takes precedence over a
        // stray stale .tmp file after a later interrupted replacement.
        string staleTemp = Path.Combine(root, "stale-temp.json");
        using (var committed = new NexExperienceStore(staleTemp))
            committed.Create(Guid.NewGuid(), Guid.Empty, "Durable", "", 
                NexExperienceMaturity.General, "ci");
        File.WriteAllText(staleTemp + ".tmp", "{incomplete");
        using (var recovered = new NexExperienceStore(staleTemp))
            Require(recovered.Search("Durable", 0, 10).Count == 1,
                "stale temp incorrectly overrode last committed snapshot");

        Directory.Delete(root, true);

        Console.WriteLine(
            "OpenGenesisLINK NexExperiences persistence, permissions and K/V regression: OK");
    }
}

using System;
using System.IO;
using System.Linq;
using NexVerse.Core.Security;

internal static class Program
{
    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static int Main()
    {
        string root = Path.Combine(
            Path.GetTempPath(),
            "nexverse-apikey-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string path = Path.Combine(root, "keys.json");

        try
        {
            PersistentNexApiKeyStore store =
                new PersistentNexApiKeyStore(path);

            NexApiKeyRegistration registration =
                store.Create(
                    "Region read integration",
                    new[]
                    {
                        NexScopes.RegionsRead,
                        NexScopes.SimulatorsRead,
                        NexScopes.UsersRead
                    });

            Require(
                registration.ApiKey.StartsWith(
                    registration.Record.KeyId + ".",
                    StringComparison.Ordinal),
                "API key format mismatch");

            Require(
                store.TryValidate(
                    registration.ApiKey,
                    out NexApiKeyRecord validated),
                "new API key did not validate");

            Require(
                validated.Scopes.Length == 3 &&
                Array.Exists(
                    validated.Scopes,
                    scope => string.Equals(
                        scope,
                        NexScopes.SimulatorsRead,
                        StringComparison.OrdinalIgnoreCase)),
                "API key scopes were not retained");

            // Existing-key scope editing must never rotate the secret, lose
            // last-used metadata, or require a new environment variable.
            string originalSalt = validated.SecretSalt;
            string originalHash = validated.SecretHash;
            long originalCreated = validated.CreatedAt;
            long originalUsed = validated.LastUsedAt;
            string[] originalScopes = validated.Scopes.ToArray();
            Require(store.SupportedScopes.Contains(NexScopes.StatisticsRead),
                "statistics:read missing from the machine-key scope catalog");
            Require(!store.SupportedScopes.Contains(NexScopes.AdminAll) &&
                    !store.SupportedScopes.Contains(NexScopes.ExperiencesManage),
                "Machine-key scope catalog exposed privileged resident/admin scopes");
            string[] withStatistics =
                originalScopes.Concat(new[] { NexScopes.StatisticsRead }).ToArray();
            Require(store.UpdateScopes(registration.Record.KeyId,
                    withStatistics, originalScopes),
                "Could not add statistics:read to an existing machine key");
            Require(store.TryValidate(registration.ApiKey,
                    out NexApiKeyRecord augmented) &&
                    augmented.Scopes.Contains(NexScopes.StatisticsRead) &&
                    augmented.SecretHash == originalHash &&
                    augmented.SecretSalt == originalSalt &&
                    augmented.CreatedAt == originalCreated &&
                    augmented.LastUsedAt >= originalUsed,
                "Scope update rotated or invalidated existing key material");
            Require(!store.UpdateScopes(registration.Record.KeyId,
                    new[] { NexScopes.UsersRead }, originalScopes),
                "Stale expected scopes must not overwrite concurrent admin changes");
            Require(store.TryValidate(registration.ApiKey,
                    out NexApiKeyRecord afterStale) &&
                    afterStale.Scopes.Contains(NexScopes.StatisticsRead),
                "Stale scope update changed a key");

            foreach (string forbidden in new[]
            {
                NexScopes.AdminAll, "regions:*", NexScopes.SecurityManage,
                NexScopes.ExperiencesManage, NexScopes.ProfileWrite
            })
            {
                bool rejected = false;
                try
                {
                    store.UpdateScopes(registration.Record.KeyId,
                        new[] { forbidden }, withStatistics);
                }
                catch (ArgumentException) { rejected = true; }
                Require(rejected, "Unsafe machine API key scope update accepted " + forbidden);
            }
            bool emptyRejected = false;
            try
            {
                store.UpdateScopes(registration.Record.KeyId,
                    Array.Empty<string>(), withStatistics);
            }
            catch (ArgumentException) { emptyRejected = true; }
            Require(emptyRejected, "Clearing all API key scopes must be rejected");

            NexApiKeyRegistration duplicateIdentity = store.Create(
                "Region read integration", new[] { NexScopes.UsersRead });
            Require(!store.UpdateScopes(registration.Record.KeyId,
                    new[] { NexScopes.UsersRead }, withStatistics),
                "Scope editor bypassed active duplicate name/scope protection");
            Require(store.SetEnabled(duplicateIdentity.Record.KeyId, false) &&
                    store.Delete(duplicateIdentity.Record.KeyId),
                "Scope editor regression fixture cleanup failed");
            Require(store.UpdateScopes(registration.Record.KeyId,
                    originalScopes, withStatistics),
                "Existing-key scope removal failed");
            Require(store.TryValidate(registration.ApiKey,
                    out NexApiKeyRecord restored) &&
                    restored.Scopes.Length == originalScopes.Length &&
                    restored.Scopes.SequenceEqual(originalScopes),
                "Removing a scope failed to preserve original key");
            PersistentNexApiKeyStore editedReloaded =
                new PersistentNexApiKeyStore(path);
            Require(editedReloaded.TryValidate(registration.ApiKey,
                    out NexApiKeyRecord persistedEdited) &&
                    persistedEdited.Scopes.SequenceEqual(originalScopes) &&
                    persistedEdited.SecretSalt == originalSalt &&
                    persistedEdited.SecretHash == originalHash,
                "Edited key lost identity or scopes after service restart");

            // Regression: one ROBUST instance edits a key while a second,
            // already running API-key authenticator still holds the old JSON
            // document. Validation must refresh grants and MUST NOT write an
            // old scope set over the new persistent configuration.
            PersistentNexApiKeyStore adminInstance =
                new PersistentNexApiKeyStore(path);
            PersistentNexApiKeyStore apiInstance =
                new PersistentNexApiKeyStore(path);
            Require(adminInstance.UpdateScopes(registration.Record.KeyId,
                    withStatistics, originalScopes),
                "Another World API instance could not add statistics:read");
            Require(apiInstance.TryValidate(registration.ApiKey,
                    out NexApiKeyRecord freshFromApiInstance) &&
                    freshFromApiInstance.Scopes.Contains(NexScopes.StatisticsRead),
                "An already-running API authenticator did not receive new scopes");
            PersistentNexApiKeyStore observer =
                new PersistentNexApiKeyStore(path);
            Require(observer.TryValidate(registration.ApiKey,
                    out NexApiKeyRecord persistedAfterAuth) &&
                    persistedAfterAuth.Scopes.Contains(NexScopes.StatisticsRead),
                "Another instance's last-used write erased a new scope grant");
            Require(apiInstance.List().First(x =>
                    x.KeyId == registration.Record.KeyId).Scopes
                    .Contains(NexScopes.StatisticsRead),
                "The inventory retained stale scope metadata after another instance's update");
            Require(!apiInstance.UpdateScopes(registration.Record.KeyId,
                    new[] { NexScopes.UsersRead }, originalScopes),
                "A stale administrator overwrote another process's new grants");
            Require(adminInstance.UpdateScopes(registration.Record.KeyId,
                    originalScopes, withStatistics),
                "Removing statistics:read across processes failed");
            Require(!apiInstance.TryValidate(registration.ApiKey,
                    out NexApiKeyRecord removedFromApiInstance) ||
                    !removedFromApiInstance.Scopes.Contains(NexScopes.StatisticsRead),
                "A long-running authenticator retained a revoked scope");
            Require(adminInstance.SetEnabled(registration.Record.KeyId, false),
                "Disabling a key from another process failed");
            Require(!apiInstance.TryValidate(registration.ApiKey, out _),
                "An already-running authenticator accepted an externally disabled key");
            Require(adminInstance.SetEnabled(registration.Record.KeyId, true) &&
                    store.TryValidate(registration.ApiKey, out _),
                "Re-enabling a key did not propagate across processes");

            Require(
                !store.TryValidate(
                    registration.ApiKey + "x",
                    out _),
                "modified API key validated");

            PersistentNexApiKeyStore reloaded =
                new PersistentNexApiKeyStore(path);

            Require(
                reloaded.TryValidate(
                    registration.ApiKey,
                    out _),
                "API key did not persist across restart");

            Require(
                reloaded.SetEnabled(
                    registration.Record.KeyId,
                    false),
                "API key disable failed");

            Require(
                !reloaded.TryValidate(
                    registration.ApiKey,
                    out _),
                "disabled API key remained valid");

            // Dedicated groups:read-only simulator key must be accepted,
            // persisted and must not accidentally obtain finance/admin scopes.
            NexApiKeyRegistration groupLand = reloaded.Create(
                "OGL Simulator Group Land",
                new[] { NexScopes.GroupsRead });
            Require(
                reloaded.TryValidate(groupLand.ApiKey,
                    out NexApiKeyRecord groupLandRecord) &&
                groupLandRecord.Scopes.Length == 1 &&
                string.Equals(groupLandRecord.Scopes[0],
                    NexScopes.GroupsRead, StringComparison.OrdinalIgnoreCase),
                "Group land API key did not retain exactly groups:read");
            PersistentNexApiKeyStore groupsReloaded =
                new PersistentNexApiKeyStore(path);
            Require(groupsReloaded.TryValidate(groupLand.ApiKey,
                    out NexApiKeyRecord persistedLand) &&
                persistedLand.Scopes.Length == 1 &&
                persistedLand.Scopes[0] == NexScopes.GroupsRead,
                "Group land API key did not survive restart with least privilege");

            // New simulator Economy keys may read NV$ group history only
            // when groups:read is explicitly included. No admin scope.
            NexApiKeyRegistration groupHistoryEconomy = reloaded.Create(
                "OGL Simulator Economy",
                new[] {
                    NexScopes.EconomyRead,
                    NexScopes.EconomyTransfer,
                    NexScopes.GroupsRead
                });
            PersistentNexApiKeyStore economyReloaded =
                new PersistentNexApiKeyStore(path);
            Require(economyReloaded.TryValidate(
                    groupHistoryEconomy.ApiKey,
                    out NexApiKeyRecord economyScopes) &&
                economyScopes.Scopes.Length == 3 &&
                economyScopes.Scopes.Contains(NexScopes.EconomyRead) &&
                economyScopes.Scopes.Contains(NexScopes.EconomyTransfer) &&
                economyScopes.Scopes.Contains(NexScopes.GroupsRead) &&
                !economyScopes.Scopes.Contains(NexScopes.AdminAll),
                "Group-history Economy key lacks correct persistent scopes");

            bool rejectedAdmin = false;
            try
            {
                reloaded.Create(
                    "unsafe",
                    new[] { NexScopes.AdminAll });
            }
            catch (ArgumentException)
            {
                rejectedAdmin = true;
            }

            Require(
                rejectedAdmin,
                "API key store accepted admin:* scope");

            NexApiKeyRegistration experienceRegistration =
                reloaded.Create(
                    "Simulator Experiences adapter",
                    new[] { NexScopes.ExperiencesScript });

            Require(
                reloaded.TryValidate(
                    experienceRegistration.ApiKey,
                    out NexApiKeyRecord experienceKey) &&
                experienceKey.Scopes.Length == 1 &&
                string.Equals(
                    experienceKey.Scopes[0],
                    NexScopes.ExperiencesScript,
                    StringComparison.OrdinalIgnoreCase),
                "Experiences script machine key could not be issued or validated");

            PersistentNexApiKeyStore experiencesReloaded =
                new PersistentNexApiKeyStore(path);

            Require(
                experiencesReloaded.TryValidate(
                    experienceRegistration.ApiKey,
                    out _),
                "Experiences script machine key did not survive a restart");

            bool rejectedExperienceManagement = false;
            try
            {
                experiencesReloaded.Create(
                    "unsafe Experiences management",
                    new[] { NexScopes.ExperiencesManage });
            }
            catch (ArgumentException)
            {
                rejectedExperienceManagement = true;
            }

            Require(
                rejectedExperienceManagement,
                "Machine API keys must not receive experiences:manage");

            // Firestorm viewer-permission CAPS use a *separate* machine
            // credential; it must never carry administrator or script rights.
            NexApiKeyRegistration viewerPermissions =
                experiencesReloaded.Create(
                    "NexVerse Simulator Experience Permissions",
                    new[] { NexScopes.ExperiencesViewerPermissions });
            Require(experiencesReloaded.TryValidate(
                    viewerPermissions.ApiKey, out NexApiKeyRecord scopedViewer) &&
                scopedViewer.Scopes.Length == 1 &&
                scopedViewer.Scopes[0] == NexScopes.ExperiencesViewerPermissions,
                "Dedicated Firestorm permissions machine key failed");

            bool rejectedMixedPermissions = false;
            try
            {
                experiencesReloaded.Create("unsafe mixed permissions",
                    new[] { NexScopes.ExperiencesViewerPermissions,
                            NexScopes.ExperiencesScript });
            }
            catch (ArgumentException) { rejectedMixedPermissions = true; }
            Require(rejectedMixedPermissions,
                "Viewer permission credentials must not be mixed with script rights");

            // Firestorm Experience creation / owner editor must receive a
            // dedicated key with ONLY experiences:viewer:manage, never
            // script, resident permission, admin or economy rights.
            NexApiKeyRegistration viewerManage =
                experiencesReloaded.Create(
                    "NexVerse Simulator Experience Management",
                    new[] { NexScopes.ExperiencesViewerManage });
            Require(experiencesReloaded.TryValidate(
                    viewerManage.ApiKey, out NexApiKeyRecord scopedManage) &&
                scopedManage.Scopes.Length == 1 &&
                scopedManage.Scopes[0] == NexScopes.ExperiencesViewerManage,
                "Dedicated Firestorm acquisition/editor machine key failed");

            PersistentNexApiKeyStore viewerReloaded =
                new PersistentNexApiKeyStore(path);
            Require(viewerReloaded.TryValidate(viewerManage.ApiKey, out _),
                "Viewer management key did not survive store reload");

            foreach (string forbidden in new[] {
                NexScopes.ExperiencesScript,
                NexScopes.ExperiencesViewerPermissions,
                NexScopes.EconomyTransfer,
                NexScopes.AdminAll
            })
            {
                bool rejected = false;
                try
                {
                    viewerReloaded.Create("unsafe viewer management " + forbidden,
                        new[] { NexScopes.ExperiencesViewerManage, forbidden });
                }
                catch (ArgumentException) { rejected = true; }
                Require(rejected,
                    "Viewer management key must not combine with " + forbidden);
            }

            // Duplicate detection is scoped to the exact service identity,
            // case-insensitive name and normalized set of scopes. It must
            // never reveal or silently replace the existing plaintext secret.
            bool rejectedDuplicate = false;
            try
            {
                experiencesReloaded.Create(
                    " nexverse simulator experience permissions ",
                    new[] { NexScopes.ExperiencesViewerPermissions });
            }
            catch (InvalidOperationException) { rejectedDuplicate = true; }
            Require(rejectedDuplicate, "Duplicate enabled service key accepted");
            Require(experiencesReloaded.List().Count == 6,
                "Duplicate rejection modified the persistent key inventory");

            // Deletion is an irreversible second step after explicit disable.
            Require(!experiencesReloaded.Delete(viewerPermissions.Record.KeyId),
                "Deleting an active machine key must be forbidden");
            Require(experiencesReloaded.SetEnabled(
                    viewerPermissions.Record.KeyId, false),
                "Disabling permission key failed");
            NexApiKeyRegistration replacement = experiencesReloaded.Create(
                "NexVerse Simulator Experience Permissions",
                new[] { NexScopes.ExperiencesViewerPermissions });
            Require(!experiencesReloaded.SetEnabled(
                    viewerPermissions.Record.KeyId, true),
                "Re-enabling an old credential must not create an active duplicate");
            Require(experiencesReloaded.Delete(viewerPermissions.Record.KeyId),
                "Deleting disabled key failed");
            Require(!experiencesReloaded.TryValidate(
                    viewerPermissions.ApiKey, out _),
                "Deleted viewer permission credential still validated");
            Require(replacement.Record.KeyId != viewerPermissions.Record.KeyId &&
                experiencesReloaded.TryValidate(replacement.ApiKey, out _),
                "Explicit credential rotation failed");

            NexApiKeyRecord metadata = experiencesReloaded.List()
                .First(x => x.KeyId == replacement.Record.KeyId);
            Require(metadata.LastUsedAt > 0,
                "Last-used metadata lost when cloning records");
            PersistentNexApiKeyStore afterCleanup =
                new PersistentNexApiKeyStore(path);
            Require(afterCleanup.List().Count == 6 &&
                    afterCleanup.TryValidate(replacement.ApiKey, out _),
                "Key cleanup or replacement did not survive restart");

            Console.WriteLine(
                "NexVerse scoped API key regression: OK");
            return 0;
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { }
        }
    }
}

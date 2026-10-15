// OpenGenesisLINK - integration regression with a real SQLite inventory.
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Nini.Config;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Console;
using OpenSim.Services.InventoryService;

internal static class Program
{
    private static void Check(bool condition, string description)
    {
        if (!condition)
            throw new InvalidOperationException("FAIL " + description);
        Console.WriteLine("PASS " + description);
    }

    private static XInventoryService Service(IConfigSource config)
    {
        return new XInventoryService(config);
    }

    private static InventoryItemBase NewItem(UUID owner, UUID folder, string name)
    {
        return new InventoryItemBase(UUID.Random(), owner)
        {
            AssetID = UUID.Random(),
            AssetType = (int)AssetType.Object,
            InvType = (int)InventoryType.Object,
            Folder = folder,
            Name = name,
            CreatorId = owner.ToString()
        };
    }

    public static int Main()
    {
        MainConsole.Instance = new MockConsole();
        string dir = Path.Combine(Path.GetTempPath(),
            "ogl-quarantine-folder-regression-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var cfg = new IniConfigSource();
            var db = cfg.AddConfig("DatabaseService");
            string provider = Path.GetFullPath(
                Path.Combine("bin", "OpenSim.Data.SQLite.dll"));
            Check(File.Exists(provider), "SQLite storage provider exists");
            db.Set("StorageProvider", provider);
            db.Set("ConnectionString", "URI=file:" +
                Path.Combine(dir, "inventory.db") + ",version=3,UseUTF16Encoding=True");

            // Existing-account migration: initial inventory with the feature off.
            UUID previousOwner = UUID.Random();
            XInventoryService legacy = Service(cfg);
            Check(legacy.CreateUserInventory(previousOwner), "legacy owner created");
            int before = legacy.GetInventorySkeleton(previousOwner).Count;

            IConfig guard = cfg.AddConfig("OGLInventoryGuard");
            guard.Set("Enabled", "true");
            guard.Set("CreateInventorySystemFolder", "true");
            XInventoryService service = Service(cfg);

            // Reserved folder IDs are protected even before the resident's
            // first skeleton/login has actually created those folders.
            UUID unbornOwner = UUID.Random();
            Check(!service.AddItem(NewItem(unbornOwner,
                OglInventoryQuarantineFolders.Pending(unbornOwner), "injection")),
                "cannot preseed a reserved folder item before registration");
            var preseededChild = new InventoryFolderBase(UUID.Random(),
                "unexpected", unbornOwner, (short)FolderType.None,
                OglInventoryQuarantineFolders.Root(unbornOwner), 1);
            Check(!service.AddFolder(preseededChild),
                "cannot preseed a reserved folder child before registration");

            var oldInventory = service.GetInventorySkeleton(previousOwner);
            Check(oldInventory.Count == before + 3,
                "existing resident receives three folders on next inventory skeleton");
            var previousRoot = service.GetRootFolder(previousOwner);
            Check(previousRoot != null, "legacy inventory root is unchanged");

            UUID owner = UUID.Random();
            Check(service.CreateUserInventory(owner), "new owner initialized");
            UUID root = OglInventoryQuarantineFolders.Root(owner);
            UUID pending = OglInventoryQuarantineFolders.Pending(owner);
            UUID rejected = OglInventoryQuarantineFolders.Rejected(owner);
            var folder = service.GetFolder(owner, root);
            var pendingFolder = service.GetFolder(owner, pending);
            var rejectedFolder = service.GetFolder(owner, rejected);
            Check(folder != null && folder.ParentID == service.GetRootFolder(owner).ID &&
                  folder.Name == OglInventoryQuarantineFolders.RootName,
                  "protected root appears under My Inventory");
            Check(pendingFolder != null && pendingFolder.ParentID == root &&
                  rejectedFolder != null && rejectedFolder.ParentID == root,
                  "pending/rejected children placed under quarantine root");
            Check(folder.Type == (short)FolderType.None,
                  "legacy viewer compatible folder type");

            int originalCount = service.GetInventorySkeleton(owner).Count;
            Check(!service.CreateUserInventory(owner), "repeat registration is idempotent");
            Check(service.GetInventorySkeleton(owner).Count == originalCount,
                  "repeat skeleton creates no duplicates");
            XInventoryService newProcess = Service(cfg);
            Check(newProcess.GetInventorySkeleton(owner).Count == originalCount,
                  "restart preserves folder identities");

            var renamed = newProcess.GetFolder(owner, root);
            renamed.Name = "Unsafe rename";
            renamed.Version++;
            Check(!newProcess.UpdateFolder(renamed), "cannot rename managed root");
            var moved = newProcess.GetFolder(owner, pending);
            moved.ParentID = newProcess.GetRootFolder(owner).ID;
            Check(!newProcess.MoveFolder(moved), "cannot move managed child");
            Check(!newProcess.DeleteFolders(owner, new List<UUID> { root }, false),
                  "cannot delete managed root");
            Check(!newProcess.PurgeFolder(new InventoryFolderBase(root), false),
                  "cannot purge managed root");

            var ordinaryChild = new InventoryFolderBase(UUID.Random(), "Untrusted child",
                owner, (short)FolderType.None, pending, 1);
            Check(!newProcess.AddFolder(ordinaryChild),
                  "viewer cannot insert arbitrary children into managed folder");

            InventoryItemBase direct = NewItem(owner, pending, "Unsafe payload");
            Check(!newProcess.AddItem(direct),
                  "viewer cannot add executable inventory item in quarantine");

            InventoryFolderBase objects = newProcess.GetFolderForType(owner, FolderType.Object);
            InventoryItemBase normal = NewItem(owner, objects.ID, "Ordinary transfer");
            Check(newProcess.AddItem(normal), "ordinary inventory still accepts legitimate items");
            InventoryItemBase attempt = (InventoryItemBase)normal.Clone();
            attempt.Folder = pending;
            Check(!newProcess.MoveItems(owner, new List<InventoryItemBase> { attempt }),
                  "cannot move existing item into pending quarantine");
            Check(newProcess.GetItem(owner, normal.ID).Folder == objects.ID,
                  "rejected move leaves original in place");

            Check(newProcess.GetFolderContent(owner, pending).Items.Count == 0 &&
                  newProcess.GetFolderItems(owner, rejected).Count == 0,
                  "normal viewers see no executable content in protected folders");

            // Phase 3a: a trusted local coordinator can project status as an
            // empty *folder*, not as an uninspected inventory/asset reference.
            UUID caseId = UUID.Random();
            UUID secondCase = UUID.Random();
            UUID caseFolderId = OglInventoryQuarantineFolders.Case(owner, caseId);
            Check(!newProcess.TryRejectTrustedQuarantineCase(owner, caseId),
                "unregistered case cannot be rejected");
            ushort pendingVersion = newProcess.GetFolder(owner, pending).Version;
            Check(newProcess.TryBeginTrustedQuarantineCase(owner, caseId),
                "trusted local coordinator can register pending status");
            Check(newProcess.GetFolder(owner, pending).Version > pendingVersion,
                "pending version invalidated for viewer refresh");
            Check(newProcess.TryBeginTrustedQuarantineCase(owner, caseId),
                "case registration is idempotent");
            Check(!newProcess.TryBeginTrustedQuarantineCase(UUID.Random(), caseId),
                "another owner cannot claim existing case identity");

            InventoryFolderBase caseFolder = newProcess.GetFolder(owner, caseFolderId);
            Check(caseFolder != null && caseFolder.ParentID == pending &&
                  caseFolder.Name == OglInventoryQuarantineFolders.CaseLabel(caseId),
                  "viewer sees a safe and owner-scoped pending status folder");
            Check(newProcess.GetFolderContent(owner, pending).Folders.Any(f =>
                f.ID == caseFolderId), "pending status appears in folder contents");
            Check(newProcess.GetFolderContent(owner, caseFolderId).Items.Count == 0,
                "case folder cannot serve executable items");

            var insideCase = NewItem(owner, caseFolderId, "malicious object");
            Check(!newProcess.AddItem(insideCase),
                "ordinary viewer cannot add executable items to case folder");
            var attemptedChild = new InventoryFolderBase(UUID.Random(), "subfolder",
                owner, (short)FolderType.None, caseFolderId, 1);
            Check(!newProcess.AddFolder(attemptedChild),
                "ordinary viewer cannot create nested case folders");

            caseFolder.Name = "tampered";
            caseFolder.Version++;
            Check(!newProcess.UpdateFolder(caseFolder),
                "ordinary viewer cannot rename case folder");
            caseFolder.ParentID = objects.ID;
            Check(!newProcess.MoveFolder(caseFolder),
                "ordinary viewer cannot move case folder out");
            Check(!newProcess.DeleteFolders(owner, new List<UUID> { caseFolderId }, false),
                "ordinary viewer cannot delete case folder");
            Check(!newProcess.PurgeFolder(new InventoryFolderBase(caseFolderId), false),
                "ordinary viewer cannot purge case folder");

            InventoryItemBase unsafeMove = (InventoryItemBase)normal.Clone();
            unsafeMove.Folder = caseFolderId;
            Check(!newProcess.MoveItems(owner, new List<InventoryItemBase> { unsafeMove }),
                "ordinary item cannot be moved into case folder");
            Check(newProcess.GetItem(owner, normal.ID).Folder == objects.ID,
                "safe item still in original folder after blocked move");

            Check(newProcess.TryBeginTrustedQuarantineCase(owner, secondCase),
                "independent second pending case registered");
            ushort rejectedVersion = newProcess.GetFolder(owner, rejected).Version;
            Check(newProcess.TryRejectTrustedQuarantineCase(owner, caseId),
                "trusted local coordinator marks case rejected");
            Check(newProcess.TryRejectTrustedQuarantineCase(owner, caseId),
                "repeated reject is idempotent");
            Check(!newProcess.TryBeginTrustedQuarantineCase(owner, caseId),
                "rejected case cannot return to pending");
            Check(!newProcess.TryRejectTrustedQuarantineCase(UUID.Random(), caseId),
                "wrong owner cannot reject another resident's case");
            Check(newProcess.GetFolder(owner, rejected).Version > rejectedVersion,
                "rejected status version invalidated for viewer refresh");
            Check(newProcess.GetFolder(owner, caseFolderId).ParentID == rejected &&
                  newProcess.GetFolderContent(owner, rejected).Folders.Any(f =>
                    f.ID == caseFolderId),
                "rejected case is visible under Abgelehnt");
            Check(newProcess.GetFolderContent(owner, pending).Folders.Any(f =>
                f.ID == OglInventoryQuarantineFolders.Case(owner, secondCase)),
                "other pending cases remain unaffected");

            XInventoryService postRestart = Service(cfg);
            Check(postRestart.GetFolder(owner, caseFolderId).ParentID == rejected &&
                  postRestart.GetFolderContent(owner, caseFolderId).Items.Count == 0,
                "rejected case survives restart without exposing HG assets");

            // Phase 3b: crypto-proof-based local coordinator, NO public HG
            // route and NO automatic acceptance of remote owner claims.
            byte[] secret = RandomNumberGenerator.GetBytes(32);
            string ledgerDir = Path.Combine(dir, "private-spent-tokens");
            var authority = new OglQuarantineTransferGrantAuthority(
                secret, "stadt-nexverse-hg-test", ledgerDir);
            var coordinator = new OglQuarantineTransferCoordinator(authority, postRestart);

            UUID grantCase = UUID.Random();
            UUID grantItem = UUID.Random();
            UUID grantAsset = UUID.Random();
            UUID otherOwner = UUID.Random();
            string beginProof = authority.Issue(owner, grantCase, grantItem, grantAsset,
                OglQuarantineTransferAction.Begin, TimeSpan.FromSeconds(60));
            Check(!coordinator.TryBegin(beginProof, otherOwner, grantCase,
                grantItem, grantAsset), "proof cannot be used for another resident");
            Check(!coordinator.TryBegin(beginProof, owner, grantCase,
                UUID.Random(), grantAsset), "proof bound to inventory item ID");
            Check(!coordinator.TryBegin(beginProof, owner, grantCase,
                grantItem, UUID.Random()), "proof bound to asset ID");
            Check(!coordinator.TryReject(beginProof, owner, grantCase,
                grantItem, grantAsset), "begin proof cannot authorize rejection");
            string forgedProof = beginProof.Substring(0, beginProof.Length - 2) + "ZZ";
            Check(!coordinator.TryBegin(forgedProof, owner, grantCase,
                grantItem, grantAsset), "tampered HMAC proof rejected");

            var wrongAudience = new OglQuarantineTransferGrantAuthority(
                secret, "other-grid", ledgerDir);
            Check(!new OglQuarantineTransferCoordinator(wrongAudience, postRestart)
                .TryBegin(beginProof, owner, grantCase, grantItem, grantAsset),
                "proof restricted to audience and cannot cross grids");

            Check(!coordinator.TryBegin("not.a.proof", owner, grantCase,
                grantItem, grantAsset), "malformed proof rejected");
            var wrongKeyAuthority = new OglQuarantineTransferGrantAuthority(
                RandomNumberGenerator.GetBytes(32), "stadt-nexverse-hg-test", ledgerDir);
            Check(!new OglQuarantineTransferCoordinator(wrongKeyAuthority, postRestart)
                .TryBegin(beginProof, owner, grantCase, grantItem, grantAsset),
                "ticket signed by another secret is rejected");
            UUID expiredCase = UUID.Random();
            UUID expiredItem = UUID.Random();
            UUID expiredAsset = UUID.Random();
            string expiredProof = authority.Issue(owner, expiredCase, expiredItem,
                expiredAsset, OglQuarantineTransferAction.Begin,
                TimeSpan.FromSeconds(1));
            Thread.Sleep(2500);
            Check(!coordinator.TryBegin(expiredProof, owner, expiredCase,
                expiredItem, expiredAsset), "expired proof cannot create status case");
            Check(postRestart.GetFolder(owner,
                OglInventoryQuarantineFolders.Case(owner, expiredCase)) == null,
                "expired proof did not create resident inventory folder");
            Check(coordinator.TryBegin(beginProof, owner, grantCase, grantItem, grantAsset),
                "authenticated locally issued begin proof creates empty case");
            Check(postRestart.GetFolder(owner,
                OglInventoryQuarantineFolders.Case(owner, grantCase)).ParentID == pending,
                "verified proof projects pending case");
            Check(!coordinator.TryBegin(beginProof, owner, grantCase, grantItem, grantAsset),
                "replay prevented by spent-token ledger");

            // A second server object with the same ledger still cannot replay.
            var restartedAuthority = new OglQuarantineTransferGrantAuthority(
                secret, "stadt-nexverse-hg-test", ledgerDir);
            var restartedCoordinator = new OglQuarantineTransferCoordinator(
                restartedAuthority, Service(cfg));
            Check(!restartedCoordinator.TryBegin(beginProof, owner, grantCase,
                grantItem, grantAsset), "replay prevention survives process restart");

            string rejectionProof = authority.Issue(owner, grantCase, grantItem, grantAsset,
                OglQuarantineTransferAction.Reject, TimeSpan.FromSeconds(60));
            Check(coordinator.TryReject(rejectionProof, owner, grantCase,
                grantItem, grantAsset), "verified single-use rejection transitions status");
            Check(!coordinator.TryReject(rejectionProof, owner, grantCase,
                grantItem, grantAsset), "rejection proof cannot be replayed");
            Check(postRestart.GetFolder(owner,
                OglInventoryQuarantineFolders.Case(owner, grantCase)).ParentID == rejected,
                "crypto-authorized rejection persists as folder status");

            UUID concurrentCase = UUID.Random();
            UUID concurrentItem = UUID.Random();
            UUID concurrentAsset = UUID.Random();
            string oneProof = authority.Issue(owner, concurrentCase, concurrentItem,
                concurrentAsset, OglQuarantineTransferAction.Begin,
                TimeSpan.FromSeconds(60));
            // FileMode.CreateNew must allow exactly ONE successful consumer,
            // including when another inventory service races it.
            Task<bool> a = Task.Run(() => coordinator.TryBegin(oneProof, owner,
                concurrentCase, concurrentItem, concurrentAsset));
            Task<bool> b = Task.Run(() => restartedCoordinator.TryBegin(oneProof, owner,
                concurrentCase, concurrentItem, concurrentAsset));
            Task.WaitAll(a, b);
            Check((a.Result ? 1 : 0) + (b.Result ? 1 : 0) == 1,
                "concurrent cross-instance proof replay admits exactly one");
            Check(postRestart.GetFolder(owner,
                OglInventoryQuarantineFolders.Case(owner, concurrentCase)) != null,
                "winning concurrent proof creates exactly one pending case");

            string[] spentProofs = Directory.GetFiles(ledgerDir, "*.spent");
            Check(spentProofs.Length == 3,
                "exactly three valid proofs were consumed; invalid proofs are not spent");
            bool verifiedAudit = false;
            foreach (string receipt in spentProofs)
            {
                Check(File.GetUnixFileMode(receipt) ==
                    (UnixFileMode.UserRead | UnixFileMode.UserWrite),
                    "spent-proof audit record stays private");
                using JsonDocument audit = JsonDocument.Parse(File.ReadAllText(receipt));
                var fields = audit.RootElement;
                Check(fields.GetProperty("status").GetString() == "proof_spent",
                    "audit records only a consumed signed proof");
                Check(fields.GetProperty("owner_id").GetString() == owner.ToString() &&
                      fields.GetProperty("case_id").GetString() == grantCase.ToString() &&
                      fields.GetProperty("item_id").GetString() == grantItem.ToString() &&
                      fields.GetProperty("asset_id").GetString() == grantAsset.ToString(),
                      "audit binds proof to owner, case, item and asset");
                Check(!fields.TryGetProperty("proof", out _) &&
                      !fields.TryGetProperty("secret", out _),
                      "audit does not expose proof or signing secret");
                verifiedAudit = true;
            }
            Check(verifiedAudit, "replay ledger has non-executable audit metadata");

            // Secret never comes from a source-code/config default.
            bool weakSecretRejected = false;
            try
            {
                new OglQuarantineTransferGrantAuthority(new byte[16],
                    "stadt-nexverse-hg-test", Path.Combine(dir, "weak-ledger"));
            }
            catch (ArgumentException)
            {
                weakSecretRejected = true;
            }
            Check(weakSecretRejected, "weak signing key refused");
            bool excessiveLifetimeRejected = false;
            try
            {
                authority.Issue(owner, UUID.Random(), UUID.Random(), UUID.Random(),
                    OglQuarantineTransferAction.Begin, TimeSpan.FromMinutes(6));
            }
            catch (ArgumentException)
            {
                excessiveLifetimeRejected = true;
            }
            Check(excessiveLifetimeRejected, "proof lifetime capped to five minutes");

            Console.WriteLine("OGL quarantine system folder integration PASS");
            return 0;
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
}

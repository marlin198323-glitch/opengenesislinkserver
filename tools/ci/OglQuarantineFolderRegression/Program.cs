// OpenGenesisLINK - integration regression with a real SQLite inventory.
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Nini.Config;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Console;
using OpenSim.Services.InventoryService;
using OpenSim.Services.HypergridService;
using OpenSim.Services.Interfaces;

internal static class Program
{
    private static void Check(bool condition, string description)
    {
        if (!condition)
            throw new InvalidOperationException("FAIL " + description);
        Console.WriteLine("PASS " + description);
    }

    // Tests demonstrate the injectable trust contracts; real HG/roaming
    // session evidence is NOT implemented by this fake.
    private sealed class FakeDeliveryEvidence : IOglHgVerifiedDeliveryEvidence
    {
        public bool ResidentConsented = true;
        public bool SourceAttested = true;
        public bool RightsConfirmed = true;
        public bool PaymentSettled = true;
        public string ExpectedSha256;
        public int PaymentsChecked;

        public bool HasAuthenticatedResidentConsent(OglHgResidentDeliveryRequest request)
            => ResidentConsented;
        public bool HasAuthenticatedSourceTransfer(OglHgResidentDeliveryRequest request,
            string sha256)
            => SourceAttested && (ExpectedSha256 == null || ExpectedSha256 == sha256);
        public bool HasValidPermissions(OglHgResidentDeliveryRequest request)
            => RightsConfirmed;
        public bool IsPurchaseSettled(OglHgResidentDeliveryRequest request)
        {
            PaymentsChecked++;
            return PaymentSettled;
        }
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

            // Strict policy regression against an actual HG Suitcase service
            // backed by SQLite, with no remote request principal assumed.
            UUID suitcaseID = UUID.Random();
            var suitcaseFolder = new InventoryFolderBase(suitcaseID,
                InventoryFolderBase.SUITCASE_FOLDER_NAME, owner,
                (short)FolderType.Suitcase,
                newProcess.GetRootFolder(owner).ID, 1);
            Check(newProcess.AddFolder(suitcaseFolder),
                "resident has an existing HG suitcase");
            var suitcaseChild = new InventoryFolderBase(UUID.Random(),
                "Public Outfit", owner, (short)FolderType.None, suitcaseID, 1);
            Check(newProcess.AddFolder(suitcaseChild),
                "suitcase has a pre-existing child without read side effects");
            InventoryItemBase suitcaseItem = NewItem(owner, suitcaseID,
                "Allowed read-only suitcase item");
            Check(newProcess.AddItem(suitcaseItem),
                "legitimate local item stored in suitcase");

            guard.Set("LegacyPublicAccessMode", "ReadOnly");
            var readonlyHg = new HGSuitcaseInventoryService(cfg,
                "HGPolicyRegressionNoExternalService");
            IInventoryService readonlyApi = readonlyHg;
            Check(readonlyApi.GetRootFolder(owner)?.ID == suitcaseID,
                "ReadOnly public HG can view pre-existing suitcase");
            Check(readonlyApi.GetItem(owner, suitcaseItem.ID)?.ID == suitcaseItem.ID,
                "ReadOnly HG can read suitcase-scoped item");
            Check(readonlyApi.GetItem(owner, normal.ID) == null,
                "ReadOnly HG cannot fetch arbitrary resident inventory item");
            InventoryItemBase[] scoped = readonlyApi.GetMultipleItems(owner,
                new UUID[] { suitcaseItem.ID, normal.ID });
            Check(scoped.Length == 2 && scoped[0]?.ID == suitcaseItem.ID &&
                scoped[1] == null, "ReadOnly HG batch query remains suitcase-scoped");
            var folderCollections = readonlyApi.GetMultipleFoldersContent(owner,
                new UUID[] { suitcaseID, objects.ID });
            Check(folderCollections.Length == 2 &&
                folderCollections[0].Items.Any(i => i.ID == suitcaseItem.ID) &&
                (folderCollections[1].Items == null ||
                 folderCollections[1].Items.Count == 0),
                "ReadOnly HG folder batch query does not bypass scope");
            Check(!readonlyApi.AddItem(NewItem(owner, suitcaseID, "HG injection")),
                "ReadOnly HG blocks public AddItem");
            Check(!readonlyApi.AddFolder(new InventoryFolderBase(UUID.Random(),
                "HG injection", owner, (short)FolderType.None, suitcaseID, 1)),
                "ReadOnly HG blocks public AddFolder");
            Check(!readonlyApi.UpdateItem(suitcaseItem) &&
                  !readonlyApi.MoveItems(owner, new List<InventoryItemBase>
                  { suitcaseItem }), "ReadOnly HG blocks public update and move");
            Check(readonlyApi.GetAssetPermissions(owner, suitcaseItem.AssetID) == 0,
                "ReadOnly HG hides general inventory asset permission union");

            guard.Set("LegacyPublicAccessMode", "BlockAll");
            var blockedHg = new HGSuitcaseInventoryService(cfg,
                "HGPolicyRegressionNoExternalService");
            IInventoryService blockedApi = blockedHg;
            Check(blockedApi.GetRootFolder(owner) == null &&
                  blockedApi.GetItem(owner, suitcaseItem.ID) == null &&
                  blockedApi.GetFolder(owner, suitcaseID) == null,
                "BlockAll HG rejects single inventory reads");
            Check(blockedApi.GetMultipleItems(owner, new UUID[]
                  { suitcaseItem.ID }).Length == 0 &&
                  blockedApi.GetMultipleFoldersContent(owner,
                  new UUID[] { suitcaseID }).Length == 0 &&
                  blockedApi.GetActiveGestures(owner).Count == 0,
                "BlockAll HG rejects batch and gestures reads");
            Check(!blockedApi.HasInventoryForUser(owner),
                "BlockAll HG does not disclose resident inventory existence");
            Check(!blockedApi.AddItem(NewItem(owner, suitcaseID, "HG blocked")) &&
                  !blockedApi.UpdateFolder(suitcaseFolder),
                "BlockAll HG denies mutations");

            guard.Set("LegacyPublicAccessMode", "not-recognized");
            var invalidHg = new HGSuitcaseInventoryService(cfg,
                "HGPolicyRegressionNoExternalService");
            Check(((IInventoryService)invalidHg).GetRootFolder(owner) == null,
                "invalid policy is BlockAll, not legacy public access");
            guard.Set("LegacyPublicAccessMode", "LegacyCompatible");


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
                bool originalCase = fields.GetProperty("case_id").GetString() ==
                    grantCase.ToString() &&
                    fields.GetProperty("item_id").GetString() == grantItem.ToString() &&
                    fields.GetProperty("asset_id").GetString() == grantAsset.ToString();
                bool parallelCase = fields.GetProperty("case_id").GetString() ==
                    concurrentCase.ToString() &&
                    fields.GetProperty("item_id").GetString() == concurrentItem.ToString() &&
                    fields.GetProperty("asset_id").GetString() == concurrentAsset.ToString();
                Check(fields.GetProperty("owner_id").GetString() == owner.ToString() &&
                      (originalCase || parallelCase),
                      "audit binds each proof to its own owner, case, item and asset");
                Check(!fields.TryGetProperty("proof", out _) &&
                      !fields.TryGetProperty("secret", out _),
                      "audit does not expose proof or signing secret");
                verifiedAudit = true;
            }
            Check(verifiedAudit, "replay ledger has non-executable audit metadata");

            // Phase 3c: raw HG bytes stay in a separate non-executable,
            // durable store; no normal inventory item or FSAssets is created.
            string stagePath = Path.Combine(dir, "private-transfer-staging");
            var staging = new OglQuarantineTransferStageStore(stagePath, 4096, 2, 8192);
            var stageCoordinator = new OglQuarantineTransferCoordinator(
                authority, Service(cfg), staging);
            UUID stageCase = UUID.Random();
            UUID stageItem = UUID.Random();
            UUID stageAsset = UUID.Random();
            byte[] rawUntrustedAsset = Encoding.UTF8.GetBytes(
                "<SceneObjectGroup><RootPart>Untrusted</RootPart></SceneObjectGroup>");
            string stageProof = authority.Issue(owner, stageCase, stageItem, stageAsset,
                OglQuarantineTransferAction.Stage, TimeSpan.FromSeconds(60));
            Check(!stageCoordinator.TryStage(stageProof, otherOwner, stageCase,
                stageItem, stageAsset, (int)AssetType.Object, rawUntrustedAsset),
                "untrusted Stage proof cannot change resident owner");
            Check(!stageCoordinator.TryStage(stageProof, owner, stageCase,
                stageItem, UUID.Random(), (int)AssetType.Object, rawUntrustedAsset),
                "stage proof cannot change asset ID");
            Check(!stageCoordinator.TryStage(beginProof, owner, stageCase,
                stageItem, stageAsset, (int)AssetType.Object, rawUntrustedAsset),
                "Begin proof cannot stage untrusted bytes");
            Check(stageCoordinator.TryStage(stageProof, owner, stageCase,
                stageItem, stageAsset, (int)AssetType.Object, rawUntrustedAsset),
                "valid signed Stage proof persists isolated bytes");
            Check(!stageCoordinator.TryStage(stageProof, owner, stageCase,
                stageItem, stageAsset, (int)AssetType.Object, rawUntrustedAsset),
                "stage proof is single-use after first successful staging");
            Check(service.GetItem(owner, stageItem) == null,
                "staged foreign asset never becomes a live inventory item");
            UUID projectedStageCase = OglInventoryQuarantineFolders.Case(owner, stageCase);
            Check(service.GetFolder(owner, projectedStageCase).ParentID == pending &&
                service.GetFolderContent(owner, projectedStageCase).Items.Count == 0,
                "staging produces empty pending status marker, not executable asset");
            Check(staging.TryVerify(owner, stageCase, stageItem, stageAsset),
                "staged asset survives disk checksum verification");
            Check(!staging.TryVerify(otherOwner, stageCase, stageItem, stageAsset),
                "cross-owner staged evidence lookup denied");
            Check(File.GetUnixFileMode(stagePath) ==
                (UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute),
                "stage directory is private 0700");
            string[] payloadFiles = Directory.GetFiles(stagePath, "*.payload");
            string[] manifestFiles = Directory.GetFiles(stagePath, "*.json");
            Check(payloadFiles.Length == 1 && manifestFiles.Length == 1,
                "one staged asset creates exactly one manifest and one payload");
            Check(File.GetUnixFileMode(payloadFiles[0]) ==
                (UnixFileMode.UserRead | UnixFileMode.UserWrite) &&
                File.GetUnixFileMode(manifestFiles[0]) ==
                (UnixFileMode.UserRead | UnixFileMode.UserWrite),
                "isolated staged payload and manifest use private 0600 permissions");
            using (JsonDocument stageManifest = JsonDocument.Parse(
                File.ReadAllText(manifestFiles[0])))
            {
                Check(stageManifest.RootElement.GetProperty("status").GetString() ==
                      "staged_uninspected", "stage never claims validated or released");
                Check(stageManifest.RootElement.GetProperty("asset_id").GetString() ==
                      stageAsset.ToString(), "stage manifest binds foreign asset ID");
            }

            var stagedAfterRestart = new OglQuarantineTransferStageStore(
                stagePath, 4096, 2, 8192);
            Check(stagedAfterRestart.TryVerify(owner, stageCase, stageItem, stageAsset),
                "stage metadata and byte integrity survive service restart");
            Check(!stagedAfterRestart.TryStage(owner, stageCase, stageItem, stageAsset,
                (int)AssetType.Object, rawUntrustedAsset, out _),
                "duplicate staged case is not silently overwritten");

            UUID oversizedStageCase = UUID.Random();
            UUID oversizedStageItem = UUID.Random();
            UUID oversizedStageAsset = UUID.Random();
            byte[] tooLarge = new byte[4097];
            string sizeProof = authority.Issue(owner, oversizedStageCase, oversizedStageItem,
                oversizedStageAsset, OglQuarantineTransferAction.Stage, TimeSpan.FromSeconds(60));
            Check(!stageCoordinator.TryStage(sizeProof, owner, oversizedStageCase,
                oversizedStageItem, oversizedStageAsset, (int)AssetType.Texture, tooLarge),
                "oversized foreign asset rejected before writing staged bytes");
            Check(service.GetFolder(owner,
                OglInventoryQuarantineFolders.Case(owner, oversizedStageCase)) == null,
                "oversized stage created no viewer status marker");

            UUID quotaCase = UUID.Random();
            UUID quotaItem = UUID.Random();
            UUID quotaAsset = UUID.Random();
            byte[] smallPayload = Encoding.UTF8.GetBytes("second opaque test payload");
            string quotaProof = authority.Issue(owner, quotaCase, quotaItem, quotaAsset,
                OglQuarantineTransferAction.Stage, TimeSpan.FromSeconds(60));
            Check(stageCoordinator.TryStage(quotaProof, owner, quotaCase, quotaItem,
                quotaAsset, (int)AssetType.Texture, smallPayload),
                "second staged asset accepted under record quota");
            UUID deniedCase = UUID.Random();
            UUID deniedItem = UUID.Random();
            UUID deniedAsset = UUID.Random();
            string deniedProof = authority.Issue(owner, deniedCase, deniedItem,
                deniedAsset, OglQuarantineTransferAction.Stage, TimeSpan.FromSeconds(60));
            Check(!stageCoordinator.TryStage(deniedProof, owner, deniedCase,
                deniedItem, deniedAsset, (int)AssetType.Texture, smallPayload),
                "third staging attempt blocked by record quota");
            Check(service.GetFolder(owner,
                OglInventoryQuarantineFolders.Case(owner, deniedCase)) == null,
                "quota rejection does not create executable or visible pending item");

            // Corrupted evidence remains unusable for future authorization;
            // there is deliberately no release endpoint to import bytes.
            File.WriteAllText(payloadFiles[0], "corrupted");
            Check(!stagedAfterRestart.TryVerify(owner, stageCase, stageItem, stageAsset),
                "modified staged bytes fail full integrity check");
            Check(service.GetItem(owner, stageItem) == null,
                "corruption cannot promote staged asset to inventory");

            // Phase 3f: a different signed action is mandatory for each
            // dependency, and a rejected case must never accept late assets.
            string multiPath = Path.Combine(dir, "private-multi-staging");
            var multiStore = new OglQuarantineTransferStageStore(
                multiPath, 4096, 5, 20480);
            var multiCoordinator = new OglQuarantineTransferCoordinator(
                authority, Service(cfg), multiStore);
            UUID multiCase = UUID.Random();
            UUID multiItem = UUID.Random();
            UUID multiRoot = UUID.Random();
            UUID multiChild = UUID.Random();
            byte[] multiRootBytes = Encoding.UTF8.GetBytes(
                "<SceneObjectGroup><RootPart><TaskInventory/></RootPart></SceneObjectGroup>");
            byte[] multiChildBytes = Encoding.UTF8.GetBytes(
                "default { state_entry() {} }");
            string multiRootProof = authority.Issue(owner, multiCase, multiItem,
                multiRoot, OglQuarantineTransferAction.Stage, TimeSpan.FromSeconds(60));
            string multiChildProof = authority.Issue(owner, multiCase, multiItem,
                multiChild, OglQuarantineTransferAction.StageDependency,
                TimeSpan.FromSeconds(60));
            Check(!multiCoordinator.TryStageDependency(multiChildProof,
                owner, multiCase, multiItem, multiChild,
                (int)AssetType.LSLText, multiChildBytes),
                "child grant cannot create a case before root staging");
            Check(multiCoordinator.TryStage(multiRootProof, owner, multiCase,
                multiItem, multiRoot, (int)AssetType.Object, multiRootBytes),
                "root proof stages and registers pending case");
            Check(!multiCoordinator.TryStageDependency(multiRootProof, owner,
                multiCase, multiItem, multiChild, (int)AssetType.LSLText,
                multiChildBytes), "Stage root proof cannot stage child");
            Check(!multiCoordinator.TryStageDependency(multiChildProof, owner,
                multiCase, multiItem, UUID.Random(), (int)AssetType.LSLText,
                multiChildBytes), "child proof bound to exact dependent asset");
            Check(!multiCoordinator.TryStageDependency(multiChildProof, owner,
                multiCase, UUID.Random(), multiChild, (int)AssetType.LSLText,
                multiChildBytes), "child proof bound to root item ID");
            Check(multiCoordinator.TryStageDependency(multiChildProof,
                owner, multiCase, multiItem, multiChild,
                (int)AssetType.LSLText, multiChildBytes),
                "authenticated per-child proof stages dependency");
            Check(!multiCoordinator.TryStageDependency(multiChildProof,
                owner, multiCase, multiItem, multiChild,
                (int)AssetType.LSLText, multiChildBytes),
                "spent dependency proof cannot be replayed");
            Check(multiStore.TryReadVerifiedBundle(owner, multiCase, multiItem,
                5, 20480, out OglQuarantineStagedAsset[] signedBundle,
                out _) && signedBundle.Length == 2,
                "authenticated multi-asset case reads root and child without inventory");
            Check(Service(cfg).GetItem(owner, multiItem) == null,
                "signed child staging never publishes an inventory item");

            string multiRejectProof = authority.Issue(owner, multiCase, multiItem,
                multiRoot, OglQuarantineTransferAction.Reject,
                TimeSpan.FromSeconds(60));
            Check(multiCoordinator.TryReject(multiRejectProof, owner, multiCase,
                multiItem, multiRoot), "trusted rejection closes multi-asset pending case");
            UUID lateChild = UUID.Random();
            string lateProof = authority.Issue(owner, multiCase, multiItem,
                lateChild, OglQuarantineTransferAction.StageDependency,
                TimeSpan.FromSeconds(60));
            Check(!multiCoordinator.TryStageDependency(lateProof, owner,
                multiCase, multiItem, lateChild,
                (int)AssetType.LSLText, multiChildBytes),
                "rejected case cannot accept a late dependent asset");

            // Phase 3i: purchase, take-copy and gift use the same local
            // quarantine intake, but distinct authentic consent / rights /
            // settled payment obligations. No legacy remote API may call this.
            var trust = new FakeDeliveryEvidence();
            string deliveryLedger = Path.Combine(dir, "trusted-hg-delivery-ledger");
            string deliveryStage = Path.Combine(dir, "trusted-hg-delivery-stage");
            var deliveryStore = new OglQuarantineTransferStageStore(
                deliveryStage, 4096, 10, 40960);
            var deliveryCoordinator = new OglQuarantineTransferCoordinator(
                authority, Service(cfg), deliveryStore);
            var intake = new OglHgResidentDeliveryIntake(trust, authority,
                deliveryCoordinator, deliveryLedger);
            UUID roamingSession = UUID.Random();
            UUID foreignTransfer = UUID.Random();
            byte[] offeredBytes = Encoding.UTF8.GetBytes(
                "<SceneObjectGroup><RootPart><TaskInventory/></RootPart></SceneObjectGroup>");

            OglHgResidentDeliveryRequest PurchaseRequest(UUID externalTransfer,
                UUID caseID, UUID itemID, UUID assetID) =>
                new OglHgResidentDeliveryRequest
                {
                    ResidentId = owner, SessionOrDelegationId = roamingSession,
                    CaseId = caseID, ForeignTransferId = externalTransfer,
                    InventoryItemId = itemID, AssetId = assetID,
                    AssetType = (int)AssetType.Object,
                    Kind = OglHgDeliveryKind.Purchase,
                    SourceGridUri = "https://osgrid.org",
                    PurchaseReceipt = "paid-confirmation-123", PurchaseAmount = 25
                };

            var paid = PurchaseRequest(foreignTransfer,
                UUID.Random(), UUID.Random(), UUID.Random());

            trust.ResidentConsented = false;
            Check(!intake.TryReceive(paid, offeredBytes, out string consentFailure) &&
                consentFailure == "delivery_resident_consent_missing",
                "HG paid delivery requires verified resident/delegated session");
            trust.ResidentConsented = true;

            trust.SourceAttested = false;
            Check(!intake.TryReceive(paid, offeredBytes, out string sourceFailure) &&
                sourceFailure == "delivery_unverified_foreign_source",
                "unverified foreign grid cannot invent purchase");
            trust.SourceAttested = true;

            trust.RightsConfirmed = false;
            Check(!intake.TryReceive(paid, offeredBytes, out string rightsFailure) &&
                rightsFailure == "delivery_permissions_missing",
                "HG transfer requires verified transfer/copy rights");
            trust.RightsConfirmed = true;

            trust.PaymentSettled = false;
            Check(!intake.TryReceive(paid, offeredBytes, out string paymentFailure) &&
                paymentFailure == "delivery_purchase_not_settled",
                "purchase requires settlement confirmation, not just sale metadata");
            trust.PaymentSettled = true;

            trust.ExpectedSha256 = new string('0', 64);
            Check(!intake.TryReceive(paid, offeredBytes, out _) &&
                Directory.GetFiles(deliveryLedger, "*.intent").Length == 0,
                "payload hash must match remote verified delivery assertion");
            trust.ExpectedSha256 = Convert.ToHexString(
                SHA256.HashData(offeredBytes));

            Check(intake.TryReceive(paid, offeredBytes, out string paidReason) &&
                paidReason == "delivery_quarantined_not_released",
                "confirmed paid item lands ONLY in private quarantine");
            Check(trust.PaymentsChecked >= 2,
                "purchase settlement verifier was actually invoked");
            Check(Service(cfg).GetItem(owner, paid.InventoryItemId) == null &&
                Service(cfg).GetFolder(owner,
                    OglInventoryQuarantineFolders.Case(owner, paid.CaseId)).ParentID ==
                        OglInventoryQuarantineFolders.Pending(owner),
                "paid item visible only as empty In Pruefung case");

            var repeatedBuy = PurchaseRequest(foreignTransfer,
                UUID.Random(), UUID.Random(), UUID.Random());
            Check(!intake.TryReceive(repeatedBuy, offeredBytes,
                out string replayReason) &&
                replayReason == "delivery_duplicate_or_unavailable_ledger",
                "foreign purchase transaction cannot be replayed with new item IDs");
            var restartIntake = new OglHgResidentDeliveryIntake(trust, authority,
                new OglQuarantineTransferCoordinator(authority,
                    Service(cfg), deliveryStore), deliveryLedger);
            Check(!restartIntake.TryReceive(repeatedBuy, offeredBytes, out _),
                "foreign purchase replay prevention survives restart");

            var takeCopy = new OglHgResidentDeliveryRequest
            {
                ResidentId = owner, SessionOrDelegationId = roamingSession,
                CaseId = UUID.Random(), ForeignTransferId = UUID.Random(),
                InventoryItemId = UUID.Random(), AssetId = UUID.Random(),
                AssetType = (int)AssetType.Object, Kind = OglHgDeliveryKind.TakeCopy,
                SourceGridUri = "http://grid.example.net:8002"
            };
            int beforeCopy = trust.PaymentsChecked;
            Check(intake.TryReceive(takeCopy, offeredBytes, out string copyReason) &&
                copyReason == "delivery_quarantined_not_released" &&
                trust.PaymentsChecked == beforeCopy,
                "Take Copy requires consent, signed source and rights but no purchase");
            Check(Service(cfg).GetItem(owner, takeCopy.InventoryItemId) == null,
                "Take Copy item remains isolated, not executable");

            var gift = new OglHgResidentDeliveryRequest
            {
                ResidentId = owner, SessionOrDelegationId = roamingSession,
                CaseId = UUID.Random(), ForeignTransferId = UUID.Random(),
                InventoryItemId = UUID.Random(), AssetId = UUID.Random(),
                AssetType = (int)AssetType.Object, Kind = OglHgDeliveryKind.Gift,
                SourceGridUri = "https://osgrid.org"
            };
            Check(intake.TryReceive(gift, offeredBytes, out _) &&
                Service(cfg).GetItem(owner, gift.InventoryItemId) == null,
                "consented gift is staged, never directly added to inventory");

            var forgedFreePurchase = PurchaseRequest(UUID.Random(),
                UUID.Random(), UUID.Random(), UUID.Random());
            forgedFreePurchase = new OglHgResidentDeliveryRequest {
                ResidentId = forgedFreePurchase.ResidentId,
                SessionOrDelegationId = forgedFreePurchase.SessionOrDelegationId,
                CaseId = forgedFreePurchase.CaseId,
                ForeignTransferId = forgedFreePurchase.ForeignTransferId,
                InventoryItemId = forgedFreePurchase.InventoryItemId,
                AssetId = forgedFreePurchase.AssetId,
                AssetType = forgedFreePurchase.AssetType,
                Kind = OglHgDeliveryKind.Purchase,
                SourceGridUri = forgedFreePurchase.SourceGridUri,
                PurchaseAmount = 25
            };
            Check(!intake.TryReceive(forgedFreePurchase, offeredBytes, out _) &&
                Directory.GetFiles(deliveryLedger, "*.intent").Length == 3,
                "missing payment receipt cannot become a purchase intent");

            foreach (string receipt in Directory.GetFiles(deliveryLedger, "*.intent"))
            {
                Check(File.GetUnixFileMode(receipt) ==
                    (UnixFileMode.UserRead | UnixFileMode.UserWrite),
                    "foreign transfer receipt is private 0600");
                using JsonDocument audit = JsonDocument.Parse(File.ReadAllText(receipt));
                Check(audit.RootElement.GetProperty("status").GetString() ==
                    "intake_attempt_spent", "foreign transfer marked spent durably");
                Check(!audit.RootElement.TryGetProperty("PurchaseReceipt", out _) &&
                      !audit.RootElement.TryGetProperty("rawBytes", out _),
                    "receipt exposes no payment token or raw object bytes");
            }

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

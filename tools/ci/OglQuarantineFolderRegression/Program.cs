// OpenGenesisLINK - integration regression with a real SQLite inventory.
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
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
            Console.WriteLine("OGL quarantine system folder integration PASS");
            return 0;
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
}

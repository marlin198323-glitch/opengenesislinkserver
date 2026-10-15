// Runtime regression tests for OGL HG InventoryGuard metadata validation.
using System;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Services.HypergridService;

internal static class Program
{
    private static int failures;

    private static InventoryItemBase ValidItem()
    {
        return new InventoryItemBase
        {
            ID = UUID.Random(),
            Owner = UUID.Random(),
            Folder = UUID.Random(),
            AssetID = UUID.Random(),
            Name = "Gültiges Objekt",
            Description = "Legitimate HG transfer",
            CreatorIdentification = UUID.Random().ToString(),
            AssetType = (int)AssetType.Object,
            InvType = (int)InventoryType.Object
        };
    }

    private static void Expect(string name, bool passed)
    {
        if (!passed)
        {
            Console.Error.WriteLine("FAIL " + name);
            failures++;
        }
        else
            Console.WriteLine("PASS " + name);
    }

    public static int Main()
    {
        InventoryItemBase valid = ValidItem();
        Expect("valid metadata", OglHgInventoryGuard.TryValidateItem(valid, out _));
        Expect("null rejected", !OglHgInventoryGuard.TryValidateItem(null, out _));

        InventoryItemBase zeroOwner = (InventoryItemBase)valid.Clone();
        zeroOwner.Owner = UUID.Zero;
        Expect("zero owner rejected", !OglHgInventoryGuard.TryValidateItem(zeroOwner, out _));

        InventoryItemBase zeroFolder = (InventoryItemBase)valid.Clone();
        zeroFolder.Folder = UUID.Zero;
        Expect("zero folder rejected", !OglHgInventoryGuard.TryValidateItem(zeroFolder, out _));

        InventoryItemBase controlName = (InventoryItemBase)valid.Clone();
        controlName.Name = "Injected\nLog";
        Expect("control character rejected", !OglHgInventoryGuard.TryValidateItem(controlName, out _));

        InventoryItemBase longName = (InventoryItemBase)valid.Clone();
        longName.Name = new string('A', 256);
        Expect("oversized name rejected", !OglHgInventoryGuard.TryValidateItem(longName, out _));

        InventoryItemBase invalidType = (InventoryItemBase)valid.Clone();
        invalidType.AssetType = 300;
        Expect("invalid type rejected", !OglHgInventoryGuard.TryValidateItem(invalidType, out _));

        InventoryItemBase safeUpdate = (InventoryItemBase)valid.Clone();
        safeUpdate.Description = "Renamed description";
        Expect("safe update accepted",
            OglHgInventoryGuard.TryValidateUpdate(valid, safeUpdate, out _));

        InventoryItemBase swappedAsset = (InventoryItemBase)valid.Clone();
        swappedAsset.AssetID = UUID.Random();
        Expect("asset swap rejected",
            !OglHgInventoryGuard.TryValidateUpdate(valid, swappedAsset, out _));

        InventoryItemBase movedFolder = (InventoryItemBase)valid.Clone();
        movedFolder.Folder = UUID.Random();
        Expect("update folder swap rejected",
            !OglHgInventoryGuard.TryValidateUpdate(valid, movedFolder, out _));

        InventoryItemBase ownerSwap = (InventoryItemBase)valid.Clone();
        ownerSwap.Owner = UUID.Random();
        Expect("update owner swap rejected",
            !OglHgInventoryGuard.TryValidateUpdate(valid, ownerSwap, out _));

        Expect("missing original rejected",
            !OglHgInventoryGuard.TryValidateUpdate(null, valid, out _));

        Console.WriteLine(failures == 0 ? "OGL HG InventoryGuard metadata: PASS"
                                        : "OGL HG InventoryGuard metadata: FAIL");
        return failures == 0 ? 0 : 1;
    }
}

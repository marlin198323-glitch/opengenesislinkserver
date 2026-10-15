// Runtime regression tests for OGL HG InventoryGuard metadata validation.
using System;
using System.Text;
using System.Linq;
using System.IO;
using System.Text.Json;
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


        // Phase 2: verify the actual HG asset-byte inspection contract.
        AssetBase validObject = new AssetBase(UUID.Random(), "HG object",
            (sbyte)AssetType.Object, valid.CreatorIdentification);
        validObject.Data = Encoding.UTF8.GetBytes(
            "<SceneObjectGroup><RootPart><Name>Safe</Name></RootPart></SceneObjectGroup>");
        Expect("valid object XML accepted",
            OglHgInventoryAssetInspector.TryInspect(validObject, 4096, 1024, out _));

        // Phase 3d: nested prim inventory references are inspected without
        // downloading, running or publishing any referenced asset.
        UUID nestedItemId = UUID.Random();
        UUID nestedScriptId = UUID.Random();
        string embeddedInventory =
            "<SceneObjectGroup><RootPart><TaskInventory><TaskInventoryItem>" +
            "<ItemID><UUID>" + nestedItemId + "</UUID></ItemID>" +
            "<AssetID><UUID>" + nestedScriptId + "</UUID></AssetID>" +
            "<Type>" + (int)AssetType.LSLText + "</Type>" +
            "<InvType>" + (int)InventoryType.LSL + "</InvType>" +
            "</TaskInventoryItem></TaskInventory></RootPart></SceneObjectGroup>";
        AssetBase nestedObject = new AssetBase(UUID.Random(), "nested script reference",
            (sbyte)AssetType.Object, valid.CreatorIdentification);
        nestedObject.Data = Encoding.UTF8.GetBytes(embeddedInventory);
        bool nestedPassed = OglHgNestedInventoryInspector.TryExtract(nestedObject,
            16384, out OglHgNestedAssetReference[] nestedRefs, out _);
        Expect("embedded script asset reference extracted",
            nestedPassed && nestedRefs.Length == 1 &&
            nestedRefs[0].ItemId == nestedItemId &&
            nestedRefs[0].AssetId == nestedScriptId &&
            nestedRefs[0].AssetType == (int)AssetType.LSLText);
        Expect("well-formed nested references structurally valid",
            OglHgInventoryAssetInspector.TryInspect(nestedObject, 16384, 4096, out _));

        AssetBase missingNestedId = new AssetBase(UUID.Random(), "missing nested asset",
            (sbyte)AssetType.Object, valid.CreatorIdentification);
        missingNestedId.Data = Encoding.UTF8.GetBytes(
            embeddedInventory.Replace(nestedScriptId.ToString(), UUID.Zero.ToString()));
        Expect("nested zero asset reference fails closed",
            !OglHgInventoryAssetInspector.TryInspect(missingNestedId, 16384, 4096, out _));

        AssetBase duplicateNestedId = new AssetBase(UUID.Random(), "duplicate nested id",
            (sbyte)AssetType.Object, valid.CreatorIdentification);
        string itemFragment =
            "<TaskInventoryItem><ItemID>" + nestedItemId + "</ItemID>" +
            "<AssetID>" + nestedScriptId + "</AssetID><Type>10</Type><InvType>10</InvType>" +
            "</TaskInventoryItem>";
        duplicateNestedId.Data = Encoding.UTF8.GetBytes(
            "<SceneObjectGroup><TaskInventory>" + itemFragment + itemFragment +
            "</TaskInventory></SceneObjectGroup>");
        Expect("duplicate embedded inventory ItemID rejected",
            !OglHgInventoryAssetInspector.TryInspect(duplicateNestedId, 16384, 4096, out _));

        AssetBase executableNested = new AssetBase(UUID.Random(), "bytecode task inventory",
            (sbyte)AssetType.Object, valid.CreatorIdentification);
        executableNested.Data = Encoding.UTF8.GetBytes(
            embeddedInventory.Replace("<Type>" + (int)AssetType.LSLText + "</Type>",
            "<Type>" + (int)AssetType.LSLBytecode + "</Type>"));
        Expect("embedded script bytecode reference rejected",
            !OglHgInventoryAssetInspector.TryInspect(executableNested, 16384, 4096, out _));

        AssetBase incompleteNested = new AssetBase(UUID.Random(), "missing invtype",
            (sbyte)AssetType.Object, valid.CreatorIdentification);
        incompleteNested.Data = Encoding.UTF8.GetBytes(
            embeddedInventory.Replace("<InvType>" + (int)InventoryType.LSL +
            "</InvType>", ""));
        Expect("incomplete embedded item rejected",
            !OglHgInventoryAssetInspector.TryInspect(incompleteNested, 16384, 4096, out _));

        AssetBase misplacedNested = new AssetBase(UUID.Random(), "inventory outside task",
            (sbyte)AssetType.Object, valid.CreatorIdentification);
        misplacedNested.Data = Encoding.UTF8.GetBytes(
            "<SceneObjectGroup>" + itemFragment + "</SceneObjectGroup>");
        Expect("unparented embedded task inventory item rejected",
            !OglHgInventoryAssetInspector.TryInspect(misplacedNested, 16384, 4096, out _));

        AssetBase overfilledNested = new AssetBase(UUID.Random(), "too many nested items",
            (sbyte)AssetType.Object, valid.CreatorIdentification);
        var itemsXml = new StringBuilder("<SceneObjectGroup><TaskInventory>");
        for (int k = 0; k <= OglHgNestedInventoryInspector.MaxNestedItems; k++)
        {
            itemsXml.Append("<TaskInventoryItem><ItemID>").Append(UUID.Random())
                .Append("</ItemID><AssetID>").Append(UUID.Random())
                .Append("</AssetID><Type>0</Type><InvType>0</InvType></TaskInventoryItem>");
        }
        itemsXml.Append("</TaskInventory></SceneObjectGroup>");
        overfilledNested.Data = Encoding.UTF8.GetBytes(itemsXml.ToString());
        Expect("nested task inventory item count capped",
            !OglHgNestedInventoryInspector.TryExtract(overfilledNested,
                4 * 1024 * 1024, out _, out string cappedReason) &&
            cappedReason == "nested_inventory_item_limit");

        AssetBase xmlWithDtd = new AssetBase(UUID.Random(), "XML DTD",
            (sbyte)AssetType.Object, valid.CreatorIdentification);
        xmlWithDtd.Data = Encoding.UTF8.GetBytes(
            "<!DOCTYPE x [<!ENTITY explode SYSTEM 'file:///etc/passwd'>]>" +
            "<SceneObjectGroup>&explode;</SceneObjectGroup>");
        Expect("DTD/entity rejected",
            !OglHgInventoryAssetInspector.TryInspect(xmlWithDtd, 4096, 1024, out _));

        AssetBase deeplyNested = new AssetBase(UUID.Random(), "nested",
            (sbyte)AssetType.Object, valid.CreatorIdentification);
        deeplyNested.Data = Encoding.UTF8.GetBytes(
            new string(' ', 0) + string.Concat(
                System.Linq.Enumerable.Repeat("<n>", 70)) +
            string.Concat(System.Linq.Enumerable.Repeat("</n>", 70)));
        Expect("excessive XML depth rejected",
            !OglHgInventoryAssetInspector.TryInspect(deeplyNested, 4096, 1024, out _));

        AssetBase oversized = new AssetBase(UUID.Random(), "oversized",
            (sbyte)AssetType.Texture, valid.CreatorIdentification);
        oversized.Data = new byte[128];
        Expect("oversized asset rejected",
            !OglHgInventoryAssetInspector.TryInspect(oversized, 64, 32, out _));

        AssetBase validScript = new AssetBase(UUID.Random(), "script",
            (sbyte)AssetType.LSLText, valid.CreatorIdentification);
        validScript.Data = Encoding.UTF8.GetBytes("default { state_entry() { llSay(0, \"hello\"); } }");
        Expect("valid UTF8 LSL source accepted",
            OglHgInventoryAssetInspector.TryInspect(validScript, 4096, 1024, out _));

        AssetBase corruptScript = new AssetBase(UUID.Random(), "broken script",
            (sbyte)AssetType.LSLText, valid.CreatorIdentification);
        corruptScript.Data = new byte[] { 0xC3, 0x28 };
        Expect("invalid script encoding rejected",
            !OglHgInventoryAssetInspector.TryInspect(corruptScript, 4096, 1024, out _));

        AssetBase bytecode = new AssetBase(UUID.Random(), "foreign bytecode",
            (sbyte)AssetType.LSLBytecode, valid.CreatorIdentification);
        bytecode.Data = new byte[] { 0x01, 0x02, 0x03 };
        Expect("foreign executable bytecode rejected",
            !OglHgInventoryAssetInspector.TryInspect(bytecode, 4096, 1024, out _));

        Expect("null asset rejected",
            !OglHgInventoryAssetInspector.TryInspect(null, 4096, 1024, out _));


        // Linux-only private quarantine: capture a bounded sample of already
        // rejected assets, without inserting them into the normal asset store.
        if (!OperatingSystem.IsWindows())
        {
            string quarantinePath = Path.Combine(Path.GetTempPath(),
                "ogl-hg-quarantine-regression-" + Guid.NewGuid().ToString("N"));
            try
            {
                var quarantine = new OglHgInventoryQuarantine(quarantinePath, 16, 2);
                Expect("private quarantine directory mode",
                    File.GetUnixFileMode(quarantinePath) ==
                    (UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute));

                bool captured = quarantine.TryCapture(xmlWithDtd,
                    "invalid_or_unsafe_object_xml", out string evidenceId);
                Expect("rejected payload captured", captured && evidenceId != null);
                if (captured)
                {
                    string metadataPath = Path.Combine(quarantinePath, evidenceId + ".json");
                    string payloadPath = Path.Combine(quarantinePath, evidenceId + ".payload");
                    Expect("quarantine metadata persisted", File.Exists(metadataPath));
                    Expect("quarantine sample bounded",
                        File.Exists(payloadPath) && new FileInfo(payloadPath).Length == 16);
                    Expect("metadata mode private",
                        File.GetUnixFileMode(metadataPath) ==
                        (UnixFileMode.UserRead | UnixFileMode.UserWrite));
                    using (JsonDocument record = JsonDocument.Parse(File.ReadAllText(metadataPath)))
                    {
                        Expect("quarantine status rejected only",
                            record.RootElement.GetProperty("status").GetString() ==
                            "rejected_evidence_only");
                        Expect("quarantine metadata identifies asset",
                            record.RootElement.GetProperty("asset_id").GetString() ==
                            xmlWithDtd.FullID.ToString());
                        Expect("quarantine does not embed script or original name",
                            !record.RootElement.TryGetProperty("name", out _));
                    }
                }

                Expect("second rejection accepted under quota",
                    quarantine.TryCapture(corruptScript, "invalid_script_utf8", out _));
                Expect("quota rejects additional evidence",
                    !quarantine.TryCapture(bytecode, "foreign_script_bytecode", out _));
                Expect("quota does not silently publish assets",
                    Directory.GetFiles(quarantinePath, "*.json").Length == 2);
            }
            finally
            {
                if (Directory.Exists(quarantinePath))
                    Directory.Delete(quarantinePath, true);
            }

            bool unsafeDirectoryRejected = false;
            try
            {
                new OglHgInventoryQuarantine("/", 16, 2);
            }
            catch (ArgumentException)
            {
                unsafeDirectoryRejected = true;
            }
            Expect("system root cannot be a quarantine directory", unsafeDirectoryRejected);
        }

        Console.WriteLine(failures == 0 ? "OGL HG InventoryGuard metadata: PASS"
                                        : "OGL HG InventoryGuard metadata: FAIL");
        return failures == 0 ? 0 : 1;
    }
}

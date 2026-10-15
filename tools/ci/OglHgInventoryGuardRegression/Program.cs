// Runtime regression tests for OGL HG InventoryGuard metadata validation.
using System;
using System.Text;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text.Json;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Services.HypergridService;
using OpenSim.Services.InventoryService;

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

    private static AssetBase GraphAsset(UUID id, AssetType type, string body)
    {
        AssetBase asset = new AssetBase(id, "isolated graph test",
            (sbyte)type, UUID.Random().ToString());
        asset.Data = Encoding.UTF8.GetBytes(body);
        return asset;
    }

    private static string GraphNestedItem(UUID assetId, AssetType type)
    {
        return "<TaskInventoryItem><ItemID>" + UUID.Random() + "</ItemID>" +
            "<AssetID>" + assetId + "</AssetID>" +
            "<Type>" + (int)type + "</Type><InvType>0</InvType></TaskInventoryItem>";
    }

    private static string GraphObject(params string[] refs)
    {
        return "<SceneObjectGroup><RootPart><TaskInventory>" +
            string.Concat(refs) +
            "</TaskInventory></RootPart></SceneObjectGroup>";
    }

    private static Dictionary<UUID, string> Pins(
        Dictionary<UUID, AssetBase> assets)
    {
        Dictionary<UUID, string> pins = new Dictionary<UUID, string>();
        foreach (KeyValuePair<UUID, AssetBase> pair in assets)
            pins[pair.Key] = Convert.ToHexString(SHA256.HashData(pair.Value.Data));
        return pins;
    }

    private static bool CheckGraph(UUID root, int rootType,
        IReadOnlyDictionary<UUID, AssetBase> assets,
        IReadOnlyDictionary<UUID, string> pins,
        out OglHgDependencyGraphReport report, out string reason,
        int maxAssets = 32, int maxRefs = 32, int maxDepth = 8,
        long maxTotalBytes = 1024 * 1024)
    {
        return OglHgQuarantineDependencyGraphVerifier.TryInspect(
            root, rootType, assets, pins, 65536, 8192,
            maxAssets, maxRefs, maxDepth, maxTotalBytes,
            out report, out reason);
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


        // Phase 3e: no network / normal AssetService fallback is possible.
        // Every nested asset must be available in a trusted, hash-pinned
        // isolated snapshot, including scripts and nested objects.
        UUID graphRoot = UUID.Random();
        UUID graphChild = UUID.Random();
        UUID graphScript = UUID.Random();
        Dictionary<UUID, AssetBase> graph = new Dictionary<UUID, AssetBase>
        {
            [graphRoot] = GraphAsset(graphRoot, AssetType.Object,
                GraphObject(GraphNestedItem(graphChild, AssetType.Object))),
            [graphChild] = GraphAsset(graphChild, AssetType.Object,
                GraphObject(GraphNestedItem(graphScript, AssetType.LSLText))),
            [graphScript] = GraphAsset(graphScript, AssetType.LSLText,
                "default { state_entry() { llOwnerSay(\"checked only, not runnable\"); } }")
        };
        Dictionary<UUID, string> graphPins = Pins(graph);
        Expect("nested object > script dependency graph inspected",
            CheckGraph(graphRoot, (int)AssetType.Object, graph, graphPins,
                out OglHgDependencyGraphReport graphReport, out _) &&
            graphReport.AssetsInspected == 3 &&
            graphReport.ObjectAssetsInspected == 2 &&
            graphReport.ScriptSourcesInspected == 1 &&
            graphReport.NestedReferences == 2 &&
            graphReport.Status == "structurally_inspected_not_authorized");

        var missingChild = new Dictionary<UUID, AssetBase>(graph);
        missingChild.Remove(graphScript);
        Expect("unresolved nested script fails closed",
            !CheckGraph(graphRoot, (int)AssetType.Object, missingChild,
                graphPins, out _, out string missingReason) &&
            missingReason == "graph_dependency_missing");

        var missingPin = new Dictionary<UUID, string>(graphPins);
        missingPin.Remove(graphChild);
        Expect("missing trusted hash pin fails closed",
            !CheckGraph(graphRoot, (int)AssetType.Object, graph, missingPin,
                out _, out string pinReason) &&
            pinReason == "graph_missing_or_malformed_hash_pin");

        var wrongPin = new Dictionary<UUID, string>(graphPins);
        wrongPin[graphScript] = new string('0', 64);
        Expect("tampered script hash pin fails closed",
            !CheckGraph(graphRoot, (int)AssetType.Object, graph, wrongPin,
                out _, out string hashReason) &&
            hashReason == "graph_asset_hash_mismatch");

        var spoiledAsset = new Dictionary<UUID, AssetBase>(graph);
        spoiledAsset[graphChild] = GraphAsset(UUID.Random(), AssetType.Object,
            GraphObject(GraphNestedItem(graphScript, AssetType.LSLText)));
        Expect("asset substitution by UUID rejected",
            !CheckGraph(graphRoot, (int)AssetType.Object, spoiledAsset,
                graphPins, out _, out string identityReason) &&
            identityReason == "graph_asset_identity_or_type_mismatch");

        var wrongTypeRoot = new Dictionary<UUID, AssetBase>(graph);
        wrongTypeRoot[graphChild] = GraphAsset(graphChild, AssetType.Texture,
            "texture body");
        var wrongTypePins = Pins(wrongTypeRoot);
        Expect("nested reference asset type mismatch rejected",
            !CheckGraph(graphRoot, (int)AssetType.Object, wrongTypeRoot,
                wrongTypePins, out _, out string typeReason) &&
            typeReason == "graph_asset_identity_or_type_mismatch");

        Expect("maximum recursive depth enforced",
            !CheckGraph(graphRoot, (int)AssetType.Object, graph, graphPins,
                out _, out string depthReason, maxDepth: 1) &&
            depthReason == "graph_depth_limit");

        Expect("maximum total dependency assets enforced",
            !CheckGraph(graphRoot, (int)AssetType.Object, graph, graphPins,
                out _, out string countReason, maxAssets: 2) &&
            countReason == "graph_asset_count_limit");

        Expect("cumulative byte budget enforced",
            !CheckGraph(graphRoot, (int)AssetType.Object, graph, graphPins,
                out _, out string sizeReason, maxTotalBytes: 50) &&
            sizeReason == "graph_total_or_single_asset_size_limit");

        Expect("total nested reference count enforced",
            !CheckGraph(graphRoot, (int)AssetType.Object, graph, graphPins,
                out _, out string refReason, maxRefs: 1) &&
            refReason == "graph_reference_count_limit");

        var cycle = new Dictionary<UUID, AssetBase>(graph);
        cycle[graphChild] = GraphAsset(graphChild, AssetType.Object,
            GraphObject(GraphNestedItem(graphRoot, AssetType.Object)));
        Expect("recursive object back-edge fails closed",
            !CheckGraph(graphRoot, (int)AssetType.Object, cycle,
                Pins(cycle), out _, out string cycleReason) &&
            cycleReason == "graph_dependency_cycle");

        var shared = new Dictionary<UUID, AssetBase>(graph);
        shared[graphRoot] = GraphAsset(graphRoot, AssetType.Object,
            GraphObject(GraphNestedItem(graphChild, AssetType.Object),
                GraphNestedItem(graphScript, AssetType.LSLText)));
        Expect("shared dependencies may be verified once",
            CheckGraph(graphRoot, (int)AssetType.Object, shared,
                Pins(shared), out OglHgDependencyGraphReport sharedReport, out _) &&
            sharedReport.AssetsInspected == 3 &&
            sharedReport.NestedReferences == 3);

        var conflicting = new Dictionary<UUID, AssetBase>(graph);
        conflicting[graphRoot] = GraphAsset(graphRoot, AssetType.Object,
            GraphObject(GraphNestedItem(graphScript, AssetType.LSLText),
                GraphNestedItem(graphScript, AssetType.Texture)));
        Expect("conflicting embedded asset type declarations rejected",
            !CheckGraph(graphRoot, (int)AssetType.Object, conflicting,
                Pins(conflicting), out _, out string conflictReason) &&
            conflictReason == "graph_conflicting_asset_types");

        var brokenScript = new Dictionary<UUID, AssetBase>(graph);
        AssetBase invalidScript = GraphAsset(graphScript, AssetType.LSLText, "ignored");
        invalidScript.Data = new byte[] { 0xC3, 0x28 };
        brokenScript[graphScript] = invalidScript;
        Expect("malformed nested LSL UTF8 rejected even if hash-pinned",
            !CheckGraph(graphRoot, (int)AssetType.Object, brokenScript,
                Pins(brokenScript), out _, out string scriptReason) &&
            scriptReason == "graph_asset_inspection_invalid_script_utf8");

        var bytecodeGraph = new Dictionary<UUID, AssetBase>(graph);
        bytecodeGraph[graphRoot] = GraphAsset(graphRoot, AssetType.Object,
            GraphObject(GraphNestedItem(graphScript, AssetType.LSLBytecode)));
        bytecodeGraph[graphScript] = GraphAsset(graphScript, AssetType.LSLBytecode,
            "compiled foreign bytecode");
        Expect("foreign executable nested bytecode never accepted",
            !CheckGraph(graphRoot, (int)AssetType.Object, bytecodeGraph,
                Pins(bytecodeGraph), out _, out _));

        // Phase 3f: real disk staging + immutable full-copy snapshot + graph scan.
        if (OperatingSystem.IsLinux())
        {
            string stagedPath = Path.Combine(Path.GetTempPath(),
                "ogl-hg-multistage-regression-" + Guid.NewGuid().ToString("N"));
            try
            {
                UUID stagingOwner = UUID.Random();
                UUID stagingCase = UUID.Random();
                UUID stagingRootItem = UUID.Random();
                var store = new OglQuarantineTransferStageStore(stagedPath, 16384, 8, 65536);
                byte[] rootBytes = graph[graphRoot].Data;
                Expect("multi-asset stage: root created",
                    store.TryStage(stagingOwner, stagingCase, stagingRootItem,
                        graphRoot, (int)AssetType.Object, rootBytes, out _));

                bool BundleInspect(out OglHgDependencyGraphReport result, out string reason)
                    => OglHgStagedQuarantineBundleInspector.TryInspect(store,
                        stagingOwner, stagingCase, stagingRootItem,
                        16384, 4096, 8, 16, 8, 65536,
                        out result, out reason);

                Expect("multi-asset stage: missing child blocks graph",
                    !BundleInspect(out _, out string beforeChild) &&
                    beforeChild == "graph_dependency_missing");

                Expect("multi-asset stage: nested object staged",
                    store.TryStageDependency(stagingOwner, stagingCase,
                        stagingRootItem, graphChild, (int)AssetType.Object,
                        graph[graphChild].Data, out _));
                Expect("multi-asset stage: missing grandchild script blocks graph",
                    !BundleInspect(out _, out string beforeScript) &&
                    beforeScript == "graph_dependency_missing");

                Expect("multi-asset stage: nested script staged",
                    store.TryStageDependency(stagingOwner, stagingCase,
                        stagingRootItem, graphScript, (int)AssetType.LSLText,
                        graph[graphScript].Data, out _));

                Expect("multi-asset stage: complete pinned recursive inspection succeeds",
                    BundleInspect(out OglHgDependencyGraphReport bundleReport,
                        out string bundleReason) &&
                    bundleReport.Status == "structurally_inspected_not_authorized" &&
                    bundleReport.AssetsInspected == 3 &&
                    bundleReport.ObjectAssetsInspected == 2 &&
                    bundleReport.ScriptSourcesInspected == 1 &&
                    bundleReport.NestedReferences == 2);

                var afterRestart = new OglQuarantineTransferStageStore(
                    stagedPath, 16384, 8, 65536);
                Expect("multi-asset stage: hash-pinned bundle survives restart",
                    OglHgStagedQuarantineBundleInspector.TryInspect(afterRestart,
                        stagingOwner, stagingCase, stagingRootItem,
                        16384, 4096, 8, 16, 8, 65536,
                        out OglHgDependencyGraphReport restartReport, out _) &&
                    restartReport.AssetsInspected == 3);

                Expect("multi-asset stage: owner mismatch denied",
                    !OglHgStagedQuarantineBundleInspector.TryInspect(afterRestart,
                        UUID.Random(), stagingCase, stagingRootItem,
                        16384, 4096, 8, 16, 8, 65536, out _, out _));

                Expect("multi-asset stage: child duplicate denied",
                    !store.TryStageDependency(stagingOwner, stagingCase,
                        stagingRootItem, graphScript, (int)AssetType.LSLText,
                        graph[graphScript].Data, out _));

                UUID extraId = UUID.Random();
                Expect("multi-asset stage: unrelated extra asset staged as untrusted",
                    store.TryStageDependency(stagingOwner, stagingCase,
                        stagingRootItem, extraId, (int)AssetType.Texture,
                        Encoding.UTF8.GetBytes("opaque extra bytes"), out _));
                Expect("multi-asset stage: unreferenced extra asset blocks case",
                    !BundleInspect(out _, out string extraReason) &&
                    extraReason == "bundle_unreferenced_staged_asset");

                string[] scriptFiles = Directory.GetFiles(stagedPath,
                    "*.dep." + graphScript + ".payload");
                Expect("multi-asset stage: private script payload located",
                    scriptFiles.Length == 1);
                if (scriptFiles.Length == 1)
                {
                    File.WriteAllText(scriptFiles[0], "corrupted");
                    Expect("multi-asset stage: corrupted script blocks snapshot",
                        !store.TryReadVerifiedBundle(stagingOwner, stagingCase,
                            stagingRootItem, 8, 65536, out _, out string corruptedReason) &&
                        corruptedReason == "bundle_dependency_corrupt");
                }
            }
            finally
            {
                if (Directory.Exists(stagedPath))
                    Directory.Delete(stagedPath, true);
            }
        }

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

using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Platform;

namespace Rebirth.VehicleStudio.Diagnostics
{
    public sealed class ConsoleCmdCamperStationCheck : ConsoleCmdAbstract
    {
        public override bool IsExecuteOnClient => true;
        public override DeviceFlag AllowedDeviceTypesClient => DeviceFlag.StandaloneWindows;
        public override string[] getCommands() => new[] { "camperstationcheck" };
        public override string getDescription() => "Test detached camper forge snapshot round-trip without world/inventory changes.";
        public override string getHelp() => "camperstationcheck: CodexTest only, in-memory model tests; does not open or tick stations.";
        public override void Execute(List<string> args, CommandSenderInfo sender)
        {
            try
            {
                if (GameManager.IsDedicatedServer || GamePrefs.GetString(EnumGamePrefs.GameName) != "CodexTest")
                    throw new InvalidOperationException("Disposable CodexTest only.");
                ItemClass forge = ItemClass.GetItemClass("forge");
                if (forge == null) throw new InvalidOperationException("Native forge item is missing.");
                var module = new ItemStack(new ItemValue(forge.Id, false), 1);
                RebirthCamperCraftingStation.Presentation presentation;
                if (!RebirthCamperCraftingStation.TryGetPresentation(module, out presentation))
                    throw new InvalidOperationException("Installed forge presentation could not be resolved.");
                var ui = LocalPlayerUI.GetUIForPrimaryPlayer();
                GUIWindow stationWindow;
                if (ui == null || !ui.windowManager.TryGetWindow(presentation.WindowName, out stationWindow)
                    || !(stationWindow is XUiWindowGroup) || !(((XUiWindowGroup)stationWindow).Controller is XUiC_WorkstationWindowGroup))
                    throw new InvalidOperationException("Selected native forge window is missing or has an incompatible controller.");
                var windows = new List<object>();
                foreach (string name in new[] { "forge", "workbench", "chemistryStation", "cementMixer" })
                {
                    RebirthCamperCraftingStation.Presentation choice;
                    if (!RebirthCamperCraftingStation.TryGetPresentation(Stack(name, 1), out choice)
                        || !ui.windowManager.TryGetWindow(choice.WindowName, out stationWindow)
                        || !(stationWindow is XUiWindowGroup) || !(((XUiWindowGroup)stationWindow).Controller is XUiC_WorkstationWindowGroup))
                        throw new InvalidOperationException("Installed station window/controller cannot be selected: " + name);
                    windows.Add(new { item = name, block = choice.InstalledBlockName,
                        recipeFamily = choice.RecipeWorkstationName, window = choice.WindowName });
                }
                Guid id = Guid.NewGuid();
                var original = new RebirthCamperStationState(id, module);
                if (original.MaterialNames.Length == 0) throw new InvalidOperationException("Forge smelting materials were not initialized.");
                original.Input[3].count = 100000;
                PopulateNativeState(original);
                var payload = RebirthCamperStationSnapshot.Capture(id, original);
                var restored = RebirthCamperStationSnapshot.Restore(id, module, payload);
                CheckNativeState(original, restored);
                ulong originalClock=original.lastTickTime;
                long originalRevision=original.Revision;
                string recoveryDigest=RebirthCamperStationRecoveryImage.Digest(original,module);
                restored.lastTickTime=originalClock+123;
                restored.RestoreRevision(originalRevision+1);
                if(RebirthCamperStationRecoveryImage.Digest(restored,module)!=recoveryDigest||
                    original.lastTickTime!=originalClock||original.Revision!=originalRevision)
                    throw new InvalidOperationException("Recovery comparison changed live state or retained a transient clock/revision.");
                if (restored.Input[3].count != 100000 || restored.Input[3].itemValue.type != original.Input[3].itemValue.type)
                    throw new InvalidOperationException("Large molten stock did not survive restoration.");
                for (int index = 1; index < original.MaterialNames.Length; index++)
                    if (restored.Input[index + 3].count != 0 || restored.Input[index + 3].itemValue.type != original.Input[index + 3].itemValue.type)
                        throw new InvalidOperationException("Empty molten slot lost its configured material identity.");
                restored.Input[3].count = 9;
                if(RebirthCamperStationRecoveryImage.Digest(restored,module)==recoveryDigest)
                    throw new InvalidOperationException("Recovery comparison ignored changed molten stock.");
                if (original.Input[3].count != 100000) throw new InvalidOperationException("Restored state aliases original state.");
                var second = new RebirthCamperStationState(Guid.NewGuid(), module);
                if (second.Input[3].count != 0) throw new InvalidOperationException("Duplicate station inherited another station's stock.");
                MustReject(() => RebirthCamperStationSnapshot.Restore(Guid.NewGuid(), module, payload));
                byte[] truncated = new byte[payload.Length - 1];
                Array.Copy(payload, truncated, truncated.Length);
                MustReject(() => RebirthCamperStationSnapshot.Restore(id, module, truncated));
                byte[] trailing = new byte[payload.Length + 1];
                Array.Copy(payload, trailing, payload.Length);
                MustReject(() => RebirthCamperStationSnapshot.Restore(id, module, trailing));
                for (int field = 0; field < 4; field++)
                {
                    byte[] mismatchedDefinition = DefinitionMismatch(payload, field);
                    MustReject(() => RebirthCamperStationSnapshot.Restore(id, module, mismatchedDefinition));
                }
                MustReject(() => RebirthCamperStationSnapshot.Restore(id, module, NativeVersionMismatch(payload, false)));
                MustReject(() => RebirthCamperStationSnapshot.Restore(id, module, NativeVersionMismatch(payload, true)));
                Guid vehicleId = Guid.NewGuid();
                var installations = new RebirthCamperStationInstallations(vehicleId);
                var firstInstalled = installations.Install(0, module);
                var secondInstalled = installations.Install(9, module);
                firstInstalled.State.Input[3].count = 100000;
                PopulateNativeState(firstInstalled.State);
                secondInstalled.State.Input[3].count = 7;
                installations.Move(0, 4);
                if (installations.GetSlot(4) != firstInstalled || installations.GetSlot(0) != null)
                    throw new InvalidOperationException("Reordering replaced or duplicated station state.");
                byte[] manifest = installations.Capture();
                var loaded = RebirthCamperStationInstallations.Restore(vehicleId, manifest);
                CheckNativeState(firstInstalled.State, loaded.GetSlot(4).State);
                if (loaded.Revision != installations.Revision || loaded.GetSlot(0) != null
                    || loaded.GetSlot(4).Id != firstInstalled.Id || loaded.GetSlot(9).Id != secondInstalled.Id
                    || loaded.GetSlot(4).State.Input[3].count != 100000 || loaded.GetSlot(9).State.Input[3].count != 7)
                    throw new InvalidOperationException("Truck station manifest lost slot, identity or stock.");
                loaded.GetSlot(4).State.Input[3].count = 1;
                if (firstInstalled.State.Input[3].count != 100000 || loaded.GetSlot(9).State.Input[3].count != 7)
                    throw new InvalidOperationException("Manifest restoration aliases another installation.");
                MustReject(() => RebirthCamperStationInstallations.Restore(Guid.NewGuid(), manifest));
                byte[] shortManifest = new byte[manifest.Length - 1];
                Array.Copy(manifest, shortManifest, shortManifest.Length);
                MustReject(() => RebirthCamperStationInstallations.Restore(vehicleId, shortManifest));
                byte[] extraManifest = new byte[manifest.Length + 1];
                Array.Copy(manifest, extraManifest, manifest.Length);
                MustReject(() => RebirthCamperStationInstallations.Restore(vehicleId, extraManifest));
                var storage = new RebirthCamperStorageInstallations(vehicleId);
                var removalModel = RebirthCamperStationInstallations.Restore(vehicleId, manifest);
                long beforeRemoval = removalModel.Revision;
                MustReject(() => removalModel.Remove(4, beforeRemoval - 1));
                var removedStation = removalModel.Remove(4, beforeRemoval);
                removedStation = new ItemStack(NativeItemRoundTrip(removedStation.itemValue), 1);
                var reinstalled = removalModel.Install(2, removedStation);
                CheckNativeState(firstInstalled.State, reinstalled.State, true);
                if (reinstalled.Id != firstInstalled.Id || reinstalled.State.Input[3].count != 100000
                    || removalModel.GetSlot(4) != null || removalModel.Revision != beforeRemoval + 2)
                    throw new InvalidOperationException("Portable station removal/reinstallation lost state or identity.");
                MustReject(() => removalModel.Install(3, removedStation));
                var stationBackpack = ItemStack.CreateArray(2);
                stationBackpack[0] = removedStation.Clone();
                var emptyStationModel = new RebirthCamperStationInstallations(Guid.NewGuid());
                var installPlan = RebirthCamperStationTransfer.Prepare(emptyStationModel, 0, stationBackpack, 0, 5, true, false);
                if (emptyStationModel.GetSlot(5) != null || stationBackpack[0].count != 1
                    || !installPlan.Backpack[0].IsEmpty() || installPlan.Stations.GetSlot(5).Id != firstInstalled.Id)
                    throw new InvalidOperationException("Station installation transfer did not isolate/debit the module.");
                CheckNativeState(firstInstalled.State, installPlan.Stations.GetSlot(5).State, true);
                var removePlan = RebirthCamperStationTransfer.Prepare(installPlan.Stations, installPlan.Stations.Revision,
                    installPlan.Backpack, 1, 5, false, false);
                RebirthCamperStationState returnedState;
                if (removePlan.Stations.GetSlot(5) != null || !RebirthCamperPortableStation.TryRestore(removePlan.Backpack[1], out returnedState))
                    throw new InvalidOperationException("Station removal transfer lost the returned state.");
                CheckNativeState(firstInstalled.State, returnedState, true);
                var cursorInstall = RebirthCamperStationTransfer.PrepareCursor(emptyStationModel, 0, removedStation, 6);
                if (removedStation.count != 1 || !cursorInstall.Backpack[0].IsEmpty() || emptyStationModel.GetSlot(6) != null)
                    throw new InvalidOperationException("Portable station cursor custody was mutated during preparation.");
                var cursorRemove = RebirthCamperStationTransfer.PrepareCursor(cursorInstall.Stations,
                    cursorInstall.Stations.Revision, ItemStack.Empty.Clone(), 6);
                RebirthCamperStationState cursorState;
                if (!RebirthCamperPortableStation.TryRestore(cursorRemove.Backpack[0], out cursorState))
                    throw new InvalidOperationException("Removed cursor station has no portable state.");
                CheckNativeState(firstInstalled.State, cursorState, true);
                MustReject(() => RebirthCamperStationTransfer.Prepare(emptyStationModel, 0, stationBackpack, 0, 5, true, true));
                storage.Install(0, Stack("cntGunSafe", 1));
                storage.Install(9, Stack("cntFridgeStainlessSteel_Player", 1));
                var sourceCamper = Stack("rebirthCamper80sPlaceable", 1).itemValue;
                sourceCamper.SetMetadata("CamperProbeUnrelated", "preserve-me");
                var itemCandidate = RebirthCamperModuleItemCandidate.Prepare(sourceCamper, storage, installations);
                string unrelated, originalModuleMetadata;
                if (sourceCamper.TryGetMetadata("RebirthCamperModules", out originalModuleMetadata))
                    throw new InvalidOperationException("Preparing portable modules mutated the source item.");
                var portable = NativeItemRoundTrip(itemCandidate);
                RebirthCamperStorageInstallations portableStorage;
                RebirthCamperStationInstallations portableStations;
                if (!portable.TryGetMetadata("CamperProbeUnrelated", out unrelated) || unrelated != "preserve-me"
                    || !RebirthCamperModuleItemCandidate.TryRestore(vehicleId, portable, out portableStorage, out portableStations)
                    || portableStorage.Capacity != storage.Capacity || portableStations.GetSlot(4).Id != firstInstalled.Id)
                    throw new InvalidOperationException("Native portable item lost module identity, capacity or unrelated metadata.");
                CheckNativeState(firstInstalled.State, portableStations.GetSlot(4).State);
                if (portableStations.GetSlot(9).State.Input[3].count != 7)
                    throw new InvalidOperationException("Portable duplicate station state was merged/lost.");
                MustReject(() => RebirthCamperModuleItemCandidate.TryRestore(Guid.NewGuid(), portable, out portableStorage, out portableStations));
                var crowded = sourceCamper.Clone();
                int metadataIndex = 0;
                while (crowded.Metadata == null || crowded.Metadata.Count < 254)
                    crowded.SetMetadata("CamperProbeEntry" + metadataIndex++, "preserved");
                var boundaryCandidate = RebirthCamperModuleItemCandidate.Prepare(crowded, storage, installations);
                var boundaryLoaded = NativeItemRoundTrip(boundaryCandidate);
                if (crowded.Metadata.Count != 254 || boundaryLoaded.Metadata.Count != 255)
                    throw new InvalidOperationException("Native metadata boundary lost entries or mutated source.");
                var replacementCandidate = RebirthCamperModuleItemCandidate.Prepare(boundaryLoaded, storage, installations);
                if (NativeItemRoundTrip(replacementCandidate).Metadata.Count != 255)
                    throw new InvalidOperationException("Existing module metadata replacement incorrectly changed entry count.");
                crowded.SetMetadata("CamperProbeOverflow", "preserved");
                MustReject(() => RebirthCamperModuleItemCandidate.Prepare(crowded, storage, installations));
                Directory.CreateDirectory(CamperDiagnosticsMod.ExportFolder);
                string file = Path.Combine(CamperDiagnosticsMod.ExportFolder, "station_check_" + Guid.NewGuid().ToString("N") + ".json");
                File.WriteAllText(file, JsonConvert.SerializeObject(new {
                    status = "PASS_DETACHED_FORGE_STOCK_CODEC", payloadBytes = payload.Length,
                    exactMoltenCount = 100000, independentInstances = true, malformedFramesRejected = true,
                    stationManifestRoundTrip = true, manifestBytes = manifest.Length,
                    portableStationRemovalRoundTrip = true,
                    stationTransferCandidatesChecked = true,
                    reorderedInstallationPreserved = true, vehicleIdentityChecked = true,
                    populatedNativeStateRoundTrip = true, completionReceiptPreserved = true,
                    canonicalRecoveryImageChecked = true,
                    snapshotFormat = "RCS4", previousInputExactCount = original.lastInput[0].count,
                    resolvedStationWindow = presentation.WindowName, resolvedRecipeFamily = presentation.RecipeWorkstationName,
                    nativeStationWindowPresent = true,
                    nativeStationPresentations = windows,
                    moduleAndRecipeDefinitionsChecked = true,
                    nativePersistenceVersionsChecked = true,
                    nativePortableModuleItemRoundTrip = true, sourceItemUnchanged = true,
                    nativeMetadataEntryBoundaryChecked = true,
                    completionExactCount = original.CraftCompleteList[0].CraftedItemStack.count,
                    mobileCraftingAcceptance = false, queueFuelTimerAcceptance = false
                }, Formatting.Indented));
                SdtdConsole.Instance.Output("[CamperStationCheck] PASS " + file);
            }
            catch (Exception error) { SdtdConsole.Instance.Output("*** ERROR [CamperStationCheck] " + error); }
        }
        private static ItemValue NativeItemRoundTrip(ItemValue item)
        {
            byte[] payload;
            using (var stream = new MemoryStream())
            using (var writer = MemoryPools.poolBinaryWriter.AllocSync(true))
            {
                writer.SetBaseStream(stream);
                item.Write(writer); writer.Flush();
                payload = stream.ToArray();
            }
            using (var stream = new MemoryStream(payload, false))
            using (var reader = MemoryPools.poolBinaryReader.AllocSync(true))
            {
                reader.SetBaseStream(stream);
                var restored = new ItemValue();
                restored.Read(reader);
                if (stream.Position != stream.Length) throw new InvalidOperationException("Native item left unexpected trailing bytes.");
                return restored;
            }
        }
        private static byte[] DefinitionMismatch(byte[] payload, int field)
        {
            // Reframe only an envelope definition; never change native registries
            // or a live station. Native state bytes and identity remain untouched.
            using (var input = new MemoryStream(payload, false))
            using (var reader = new BinaryReader(input))
            using (var output = new MemoryStream())
            using (var writer = new BinaryWriter(output))
            {
                writer.Write(reader.ReadUInt32());
                writer.Write(reader.ReadBytes(16));
                for (int index = 0; index < 4; index++)
                {
                    string definition = reader.ReadString();
                    writer.Write(index == field ? definition + "_incompatible_probe" : definition);
                }
                writer.Write(reader.ReadBytes(checked((int)(input.Length - input.Position))));
                writer.Flush();
                return output.ToArray();
            }
        }
        private static byte[] NativeVersionMismatch(byte[] payload, bool workstation)
        {
            using (var stream = new MemoryStream(payload, false))
            using (var reader = new BinaryReader(stream))
            {
                reader.ReadUInt32(); reader.ReadBytes(16);
                for (int field = 0; field < 4; field++) reader.ReadString();
                reader.ReadInt64();
                for (int group = 0; group < 6; group++)
                {
                    int length = reader.ReadInt32();
                    stream.Position += checked(length * 4L);
                }
                byte[] changed = (byte[])payload.Clone();
                int offset = checked((int)stream.Position + (workstation ? 22 : 0));
                changed[offset] = workstation ? (byte)51 : (byte)20;
                return changed;
            }
        }
        private static ItemStack Stack(string name, int count)
        {
            ItemClass item = ItemClass.GetItemClass(name);
            if (item == null) throw new InvalidOperationException("Required native probe item missing: " + name);
            return new ItemStack(new ItemValue(item.Id, false), count);
        }
        private static void PopulateNativeState(RebirthCamperStationState station)
        {
            Recipe recipe = CraftingManager.GetRecipe("resourceForgedIron");
            if (recipe == null || station.Queue.Length == 0 || station.currentMeltTimesLeft.Length == 0)
                throw new InvalidOperationException("Native forge recipe/queue/timer configuration missing.");
            station.Tools[0] = Stack("toolAnvil", 1);
            station.Fuel[0] = Stack("resourceWood", 12);
            station.Input[0] = Stack("resourceScrapIron", 5);
            station.lastInput[0] = Stack("resourceScrapIron", 100000);
            station.Output[0] = Stack("resourceForgedIron", 4);
            station.lastTickTime = 123456700;
            station.currentBurnTimeLeft = 127.5f;
            station.currentMeltTimesLeft[0] = 12.25f;
            station.isBurning = true;
            station.queue[station.queue.Length - 1] = new RecipeQueueItem {
                Recipe = recipe, Multiplier = 3, CraftingTimeLeft = 34.5f, OneItemCraftTime = 8.25f,
                IsCrafting = true, Quality = 0, StartingEntityId = GameManager.Instance.World.GetPrimaryPlayer().entityId,
                RepairItem = ItemValue.None
            };
            if (station.CraftCompleteList == null) station.CraftCompleteList = new List<CraftCompleteData>();
            station.CraftCompleteList.Add(new CraftCompleteData {
                CrafterEntityID = GameManager.Instance.World.GetPrimaryPlayer().entityId,
                CraftedItemStack = Stack("resourceForgedIron", 100000), RecipeName = recipe.GetName(),
                ItemScrapped = "", RecipeUsedCount = 3, CraftExpGain = 17
            });
            station.setModified();
        }
        private static void CheckNativeState(RebirthCamperStationState original, RebirthCamperStationState restored, bool portable = false)
        {
            if (original.Revision != restored.Revision) throw new InvalidOperationException("Station revision changed during restoration.");
            var before = new[] { original.Tools, original.Fuel, original.Input, original.Output, original.lastInput };
            var after = new[] { restored.Tools, restored.Fuel, restored.Input, restored.Output, restored.lastInput };
            for (int group = 0; group < before.Length; group++)
            {
                if (before[group].Length != after[group].Length) throw new InvalidOperationException("Native inventory dimensions changed.");
                for (int slot = 0; slot < before[group].Length; slot++)
                    if (before[group][slot].count != after[group][slot].count
                        || (before[group][slot].count > 0 && before[group][slot].itemValue.type != after[group][slot].itemValue.type))
                        throw new InvalidOperationException("Populated tools/fuel/input/output did not survive native decoding.");
            }
            if ((portable ? restored.lastTickTime != 0 : original.lastTickTime != restored.lastTickTime) || original.isBurning != restored.isBurning
                || original.currentBurnTimeLeft != restored.currentBurnTimeLeft
                || original.currentMeltTimesLeft.Length != restored.currentMeltTimesLeft.Length)
                throw new InvalidOperationException("Native tick/burn/melt state changed.");
            for (int slot = 0; slot < original.currentMeltTimesLeft.Length; slot++)
                if (original.currentMeltTimesLeft[slot] != restored.currentMeltTimesLeft[slot])
                    throw new InvalidOperationException("Native melt timer changed.");
            if (original.Queue.Length != restored.Queue.Length) throw new InvalidOperationException("Native queue dimensions changed.");
            RecipeQueueItem job = original.Queue[original.Queue.Length - 1];
            RecipeQueueItem loaded = restored.Queue[restored.Queue.Length - 1];
            if (loaded == null || loaded.Recipe == null || loaded.Recipe.GetName() != job.Recipe.GetName()
                || loaded.Multiplier != job.Multiplier || loaded.CraftingTimeLeft != job.CraftingTimeLeft
                || loaded.OneItemCraftTime != job.OneItemCraftTime || loaded.IsCrafting != job.IsCrafting
                || loaded.StartingEntityId != job.StartingEntityId || loaded.Quality != job.Quality)
                throw new InvalidOperationException("Native queued job lost progress, identity or attribution.");
            if (restored.CraftCompleteList.Count != original.CraftCompleteList.Count)
                throw new InvalidOperationException("Pending completion receipts changed.");
            CraftCompleteData receipt = original.CraftCompleteList[0], copy = restored.CraftCompleteList[0];
            if (copy.CrafterEntityID != receipt.CrafterEntityID || copy.RecipeName != receipt.RecipeName
                || copy.RecipeUsedCount != receipt.RecipeUsedCount || copy.CraftExpGain != receipt.CraftExpGain
                || copy.CraftedItemStack.count != receipt.CraftedItemStack.count
                || copy.CraftedItemStack.itemValue.type != receipt.CraftedItemStack.itemValue.type)
                throw new InvalidOperationException("Pending completion receipt lost its owner, item or reward.");
        }
        private static void MustReject(Action operation)
        {
            try { operation(); }
            catch { return; }
            throw new InvalidOperationException("Invalid station frame was accepted.");
        }
    }
}

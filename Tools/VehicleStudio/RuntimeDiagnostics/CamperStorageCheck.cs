using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Platform;

namespace Rebirth.VehicleStudio.Diagnostics
{
    public sealed class ConsoleCmdCamperStorageCheck : ConsoleCmdAbstract
    {
        public override bool IsExecuteOnClient => true;
        public override DeviceFlag AllowedDeviceTypesClient => DeviceFlag.StandaloneWindows;
        public override string[] getCommands() => new[] { "camperstoragecheck" };
        public override string getDescription() => "In-memory ten-container manifest and safe shrink probe.";
        public override string getHelp() => "camperstoragecheck: local CodexTest only; no player inventory, real bag or UI mutation.";
        public override void Execute(List<string> args, CommandSenderInfo sender)
        {
            try
            {
                if (GameManager.IsDedicatedServer || GameManager.Instance.World == null
                    || GameManager.Instance.World.IsRemote() || GamePrefs.GetString(EnumGamePrefs.GameName) != "CodexTest")
                    throw new InvalidOperationException("Local disposable CodexTest required.");
                var safe = Module("cntGunSafe");
                var fridge = Module("cntFridgeStainlessSteel_Player");
                int safeCapacity, fridgeCapacity;
                if (!RebirthCamperStorageContainer.TryGetCapacity(safe, out safeCapacity)
                    || !RebirthCamperStorageContainer.TryGetCapacity(fridge, out fridgeCapacity))
                    throw new InvalidOperationException("Native safe/fridge definitions do not resolve to storage containers.");
                Guid id = Guid.NewGuid();
                var original = new RebirthCamperStorageInstallations(id);
                original.Install(0, safe);
                original.Install(9, fridge);
                original.Move(0, 4);
                if (original.Capacity != safeCapacity + fridgeCapacity) throw new InvalidOperationException("Capacity is not additive.");
                var returnedCopy = original.GetSlot(4);
                returnedCopy.itemValue.type = 0;
                if (original.GetSlot(4).itemValue.type != safe.itemValue.type)
                    throw new InvalidOperationException("Public slot view aliases installed metadata.");
                byte[] payload = original.Capture();
                var restored = RebirthCamperStorageInstallations.Restore(id, payload);
                if (restored.Capacity != original.Capacity || restored.Revision != original.Revision
                    || restored.GetSlot(0) != null || restored.GetSlot(4).itemValue.type != safe.itemValue.type
                    || restored.GetSlot(9).itemValue.type != fridge.itemValue.type)
                    throw new InvalidOperationException("Manifest lost positions, capacities or items.");
                int remaining;
                int[] destinations;
                if (!restored.TryPlanRemoval(4, new[] { original.Capacity - 1 }, out remaining, out destinations)
                    || remaining != fridgeCapacity || destinations.Length != 1 || destinations[0] >= remaining)
                    throw new InvalidOperationException("Sparse cargo shrink plan lost an occupied slot.");
                int[] fullCargo = new int[fridgeCapacity + 1];
                for (int slot = 0; slot < fullCargo.Length; slot++) fullCargo[slot] = slot;
                if (restored.TryPlanRemoval(4, fullCargo, out remaining, out destinations))
                    throw new InvalidOperationException("Overfull cargo allowed container removal.");
                if (restored.Capacity != original.Capacity || restored.GetSlot(4) == null)
                    throw new InvalidOperationException("Removal planning mutated live module state.");
                MustReject(() => RebirthCamperStorageInstallations.Restore(Guid.NewGuid(), payload));
                byte[] truncated = new byte[payload.Length - 1]; Array.Copy(payload, truncated, truncated.Length);
                MustReject(() => RebirthCamperStorageInstallations.Restore(id, truncated));
                byte[] trailing = new byte[payload.Length + 1]; Array.Copy(payload, trailing, payload.Length);
                MustReject(() => RebirthCamperStorageInstallations.Restore(id, trailing));
                // RCV1 header is 32 bytes; first occupied position is slot4.
                byte[] changedCapacity = (byte[])payload.Clone();
                byte[] wrongCapacity = BitConverter.GetBytes(safeCapacity + 1);
                Array.Copy(wrongCapacity, 0, changedCapacity, 37, 4);
                MustReject(() => RebirthCamperStorageInstallations.Restore(id, changedCapacity));
                var cargo = ItemStack.CreateArray(restored.Capacity);
                cargo[cargo.Length - 1] = new ItemStack(safe.itemValue.Clone(), 123456);
                ItemStack[] compacted;
                long removalRevision = restored.Revision;
                MustReject(() => restored.RemoveAndCompact(4, removalRevision - 1, cargo, out compacted));
                var removed = restored.RemoveAndCompact(4, removalRevision, cargo, out compacted);
                if (removed.itemValue.type != safe.itemValue.type || restored.GetSlot(4) != null
                    || restored.Capacity != fridgeCapacity || compacted.Length != fridgeCapacity
                    || compacted[0].count != 123456 || compacted[0].itemValue.type != safe.itemValue.type
                    || cargo[cargo.Length - 1].count != 123456 || restored.Revision != removalRevision + 1)
                    throw new InvalidOperationException("Container removal did not preserve exact cargo and revision.");
                Directory.CreateDirectory(CamperDiagnosticsMod.ExportFolder);
                var emptyStorage = new RebirthCamperStorageInstallations(Guid.NewGuid());
                var backpack = ItemStack.CreateArray(2);
                backpack[0] = new ItemStack(safe.itemValue.Clone(), 2);
                var install = RebirthCamperStorageTransfer.Prepare(emptyStorage, 0, backpack, 0,
                    ItemStack.CreateArray(0), 0, true, false);
                if (emptyStorage.Capacity != 0 || backpack[0].count != 2 || install.Backpack[0].count != 1
                    || install.Storage.Capacity != safeCapacity || install.Cargo.Length != safeCapacity)
                    throw new InvalidOperationException("Installation candidate did not debit exactly one isolated item.");
                var uninstall = RebirthCamperStorageTransfer.Prepare(install.Storage, install.Storage.Revision,
                    install.Backpack, 1, install.Cargo, 0, false, false);
                if (uninstall.Storage.Capacity != 0 || uninstall.Cargo.Length != 0
                    || uninstall.Backpack[1].count != 1 || uninstall.Backpack[1].itemValue.type != safe.itemValue.type
                    || install.Storage.Capacity != safeCapacity || !install.Backpack[1].IsEmpty())
                    throw new InvalidOperationException("Removal candidate lost a module or changed the input inventory.");
                MustReject(() => RebirthCamperStorageTransfer.Prepare(emptyStorage, 0, backpack, 0,
                    ItemStack.CreateArray(0), 0, true, true));
                var cursor = new ItemStack(safe.itemValue.Clone(), 3);
                var cursorInstall = RebirthCamperStorageTransfer.PrepareCursor(emptyStorage, 0, cursor, ItemStack.CreateArray(0), 2);
                if (cursor.count != 3 || cursorInstall.Backpack[0].count != 2 || emptyStorage.Capacity != 0)
                    throw new InvalidOperationException("Cursor install mutated custody or consumed the wrong count.");
                var cursorRemove = RebirthCamperStorageTransfer.PrepareCursor(cursorInstall.Storage,
                    cursorInstall.Storage.Revision, ItemStack.Empty.Clone(), cursorInstall.Cargo, 2);
                if (cursorRemove.Backpack[0].count != 1 || cursorRemove.Backpack[0].itemValue.type != safe.itemValue.type
                    || cursorRemove.Storage.Capacity != 0 || cursorInstall.Storage.GetSlot(2) == null)
                    throw new InvalidOperationException("Cursor removal failed to return the original container in isolation.");
                MustReject(() => RebirthCamperStorageTransfer.PrepareCursor(cursorInstall.Storage,
                    cursorInstall.Storage.Revision - 1, ItemStack.Empty.Clone(), cursorInstall.Cargo, 2));
                string file = Path.Combine(CamperDiagnosticsMod.ExportFolder, "storage_check_" + Guid.NewGuid().ToString("N") + ".json");
                File.WriteAllText(file, JsonConvert.SerializeObject(new {
                    status = "PASS_IN_MEMORY_CONTAINER_MANIFEST_AND_SHRINK_PLAN", safeCapacity, fridgeCapacity,
                    total = original.Capacity, payloadBytes = payload.Length, malformedFramesRejected = true,
                    exactCargoRemovalCommitChecked = true,
                    inventoryTransferCandidatesChecked = true,
                    cursorTransferCandidatesChecked = true,
                    gameplayAcceptance = false, realCargoTransferAcceptance = false, persistenceIntegrationAcceptance = false
                }, Formatting.Indented));
                SdtdConsole.Instance.Output("[CamperStorageCheck] PASS " + file);
            }
            catch (Exception error) { SdtdConsole.Instance.Output("*** ERROR [CamperStorageCheck] " + error); }
        }
        private static ItemStack Module(string name)
        {
            ItemClass item = ItemClass.GetItemClass(name);
            if (item == null) throw new InvalidOperationException("Required native container item is missing: " + name);
            return new ItemStack(new ItemValue(item.Id, false), 1);
        }
        private static void MustReject(Action operation)
        {
            try { operation(); } catch { return; }
            throw new InvalidOperationException("Invalid storage manifest was accepted.");
        }
    }
}

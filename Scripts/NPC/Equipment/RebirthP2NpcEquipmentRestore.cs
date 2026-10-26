using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Xml.Linq;

// P2/G03 reload safety (C2C-105, C2C-109).
//
// ROOT CAUSE (proven from the installed Assembly-CSharp.dll, see P2 doc "C2C-109"): the native persistence of a
// non-player EntityAlive is EntityCreationData(entity): health stats, the bag blob, home, spawner source and
// Entity.Write/Read (spawner source, WorldTimeBorn, deathHealth, stress) plus REBIRTH's appended payloads. It contains NO
// toolbelt (Inventory), NO Equipment grid and NO buffs/custom variables (EntityBuffs.Write is only called by the buff
// network package). Every unload/reload of the entity (quit + load, or chunk unload + load) therefore returns a body
// whose toolbelt and worn gear are empty and whose rb* CVars (rbNpcBirthEquipment, rbNpcNativeEquipment, rbNpcBirthTries,
// rbNpcBirthPending, ...) are 0. Only the bag (cargo) survives natively.
//
// The item FRAMES of the native cells are persisted in the inventory custody store (RebirthNpcNativeStackSet: id, item,
// count, encoded ItemValue payload); the CELL each frame belongs to is persisted in the aggregate humanEquipment element
// (binding area index id); the lost CVars are persisted as humanEquipment attributes birth (1 complete, -1 gave up),
// birthTries / birthPending (a failed birth intent). This rebuilds the cells and CVars from those records through the
// normal Put path (readback verified, custody observed); it never creates items that have no saved record.
//   (a) saved frames + empty native cell        -> restore that frame into that cell.
//   (b) no humanEquipment element               -> nothing here; the birth loadout runs once for a NEW person only.
//   (c) birth attributes or bindings            -> CVars restored from the record; marker never regenerated.
// State is keyed by the entity INSTANCE (not entityId): a chunk unload/reload re-creates the body with the same entityId.
internal static class RebirthP2NpcEquipmentRestore
{
    internal const string MarkerCVar = "rbNpcBirthEquipment", TriesCVar = "rbNpcBirthTries", PendingCVar = "rbNpcBirthPending";
    // Restore write failures per instance: after MaxAttempts the person stays HELD (never settled, never checkpointed over the saved intent).
    private const int MaxAttempts = 3;

    private sealed class State
    {
        internal bool Restored;   // every non-owned saved cell is live again: observing/saving the live cells is safe
        internal bool Done;       // Restored and no pending operation of another owner remains
        internal bool Probed;
        internal int Attempts;
        internal string Held;
        internal float HeldLogAt;
        internal readonly HashSet<int> Conflicts = new HashSet<int>();
    }
    private static readonly ConditionalWeakTable<EntityRebirthHumanoidNPC, State> States = new ConditionalWeakTable<EntityRebirthHumanoidNPC, State>();
    private static State Of(EntityRebirthHumanoidNPC npc) => States.GetValue(npc, _ => new State());

    internal static bool TryLogicalSlot(string area, int index, out int slot)
    {
        slot = -1;
        if (area == "toolbelt")
        {
            if (index >= 0 && index <= 4) slot = index + 4;
            else if (index == RebirthHumanEquipment.LockpicksToolbeltIndex) slot = RebirthHumanEquipment.LockpicksSlot;
        }
        else if (area == "equipment")
        {
            if (index >= 0 && index < 4) slot = index;
            else if (index == (int)EquipmentSlots.Backpack) slot = 9;
        }
        else if (area == "bag" && index >= 0 && index < 169) slot = 10 + index;
        return slot >= 0;
    }

    // Checkpointing before the saved frames were restored would rebuild custody from empty cells and erase them.
    internal static bool IsSettled(EntityRebirthHumanoidNPC npc) =>
        npc != null && ReferenceEquals(npc.world, GameManager.Instance?.World) && States.TryGetValue(npc, out var s) && s.Restored;

    // True for a body that came from a save (ECD Read) and whose records have not been restored yet. Any code that
    // observes/saves the live native cells into custody (ObserveHumanEquipment, Save, Migration, exchanges) must refuse while this holds.
    internal static bool IsRestorePending(EntityRebirthHumanoidNPC npc) => npc != null && npc.RebirthStartingLoadoutRead && !IsSettled(npc);

    // Should the world-save checkpoint write this person's humanEquipment row? Native owners, birth marker holders and failed-birth intents.
    internal static bool ShouldCheckpoint(EntityRebirthHumanoidNPC npc) =>
        npc.Buffs.GetCustomVar(RebirthHumanEquipment.NativeMode) == 1f || npc.Buffs.GetCustomVar(MarkerCVar) != 0f || npc.Buffs.GetCustomVar(TriesCVar) > 0f;

    // The lost CVars as record attributes (called by the checkpoint and by PersistBirth).
    internal static void ApplyBirthAttributes(EntityRebirthHumanoidNPC npc, XElement x)
    {
        float birth = npc.Buffs.GetCustomVar(MarkerCVar), tries = npc.Buffs.GetCustomVar(TriesCVar);
        if (birth != 0f) x.SetAttributeValue("birth", birth < 0f ? -1 : 1);
        if (tries > 0f)
        {
            x.SetAttributeValue("birthTries", (int)tries);
            x.SetAttributeValue("birthPending", (int)npc.Buffs.GetCustomVar(PendingCVar));
        }
        else { x.SetAttributeValue("birthTries", null); x.SetAttributeValue("birthPending", null); }
        // Selected toolbelt slot (the engine does not persist the NPC toolbelt). A book move owns slot 6 and keeps its own "selected": leave sel untouched then.
        if (npc.inventory != null && x.Element("bookMove") == null)
        {
            int sel = npc.inventory.SelectedSlot;
            x.SetAttributeValue("sel", sel > 0 ? (int?)sel : null);
        }
    }

    // Re-hold the saved toolbelt slot once the saved cells are live. Only a non-empty cell is selected (a cell that was not
    // restored, e.g. owned by a pending swap, keeps the default); a pending book move restores its own selection.
    private static void RestoreSelected(EntityRebirthHumanoidNPC npc, XElement x)
    {
        if (x.Element("bookMove") != null || !int.TryParse((string)x.Attribute("sel"), out int sel) || sel <= 0) return;
        if (sel >= npc.inventory.SlotCount || npc.inventory.SelectedSlot == sel) return;
        var cell = npc.inventory.GetItem(sel);
        if (cell == null || cell.IsEmpty()) return;
        npc.inventory.SetSelectedSlot(sel);
        npc.inventory.ReconcileHeldItem();
        RebirthHumanWeaponPassives.SyncNow(npc);
        Log.Out("[REBIRTH NPC Equipment] restored selected toolbelt slot entity=" + npc.entityId + " sel=" + sel);
    }

    // Run once per NPC instance from the existing retry tick (budgeted by the caller) or from Apply. true = concluded.
    internal static bool TryReconcileOnce(EntityRebirthHumanoidNPC npc)
    {
        var world = GameManager.Instance?.World;
        if (world == null || npc == null) return false;
        var state = Of(npc);
        if (state.Done) return true;
        if (Reconcile(npc, state)) { state.Done = true; return true; }
        return false;
    }

    private static bool Hold(EntityRebirthHumanoidNPC npc, State state, string reason)
    {
        // Once per change of reason, then a reminder every 30 s while the person stays held (a hold is never silent and never concludes).
        float now = UnityEngine.Time.unscaledTime;
        if (state.Held != reason) { state.Held = reason; state.HeldLogAt = now; Log.Out("[REBIRTH NPC Equipment] restore held entity=" + npc.entityId + " reason=" + reason); }
        else if (now - state.HeldLogAt >= 30f) { state.HeldLogAt = now; Log.Out("[REBIRTH NPC Equipment] restore still held entity=" + npc.entityId + " reason=" + reason); }
        return false;
    }

    // First failing precondition of Reconcile (null = all satisfied). Every one is a retry, never a conclusion.
    private static string Blocker(EntityRebirthHumanoidNPC npc)
    {
        if (!ThreadManager.IsMainThread()) return "not-main-thread";
        if (npc.world?.IsRemote() != false) return "remote-world";
        if (!ReferenceEquals(npc.world, GameManager.Instance?.World)) return "stale-world";
        if (npc.IsDead()) return "dead";
        if (npc.IsPreparedRestorationPending) return "prepared-restoration-pending";
        if (npc.bag == null || npc.inventory == null || npc.equipment == null) return "native-containers-missing";
        if (npc.RebirthRuntimeState == null) return "runtime-state-missing";
        if (!RebirthHumanEquipment.Human(npc)) return "not-human";
        if (RebirthNpcPersistenceCoordinator.IsRecoveryInstalling) return "recovery-installing";
        if (RebirthNpcLiveRecoveryRuntime.IsNativeActivationDeferred(npc)) return "native-activation-deferred";
        if (!ReferenceEquals(npc.world.GetEntity(npc.entityId), npc)) return "not-registered-in-world";
        return null;
    }

    private static Guid? Id(string text) => Guid.TryParse(text, out var g) ? g : (Guid?)null;

    // true = conclusive (restored, nothing to restore, or nothing recorded); false = retry later.
    private static bool Reconcile(EntityRebirthHumanoidNPC npc, State state)
    {
        string blocker = Blocker(npc);
        if (blocker != null) return Hold(npc, state, blocker);
        var id = npc.RebirthRuntimeState.StableId;
        try
        {
            RebirthNpcAggregatePersistenceStore.EnsureLoaded();
            // TryGet == false is "no row" ONLY when the store is loaded for this world/save; otherwise it is "not known yet": hold.
            if (!RebirthNpcAggregatePersistenceStore.IsRecoveryBackingLoaded) return Hold(npc, state, "aggregate-store-not-loaded");
            if (!RebirthNpcAggregatePersistenceStore.TryGet(id, out var person)) { Probe(npc, state, "no-row", null, 0); state.Restored = true; return true; } // loaded store, no saved row: a genuinely new person (case b)
            RebirthNpcInventoryPersistenceStore.EnsureLoaded();
            XElement x = null;
            foreach (string text in person.UnknownOptionalElements ?? Array.Empty<string>())
            { var e = XElement.Parse(text); if (e.Name == "humanEquipment") { x = e; break; } }
            if (x == null) { Probe(npc, state, "no-humanEquipment", null, 0); state.Restored = true; return true; } // case (b): no saved native equipment intent at all

            // Cells owned by a pending operation of another owner are NOT restored from records (that owner rebuilds them from its own
            // exact frames); every other cell is restored first so the owner's recovery sees a whole body.
            var ownedIds = new HashSet<Guid>(); var ownedSlots = new HashSet<int>();
            var swap = x.Element("swap"); bool swapPending = swap != null && (string)swap.Attribute("done") != "1";
            if (swapPending) { var o = Id((string)swap.Attribute("oldStack")); if (o.HasValue) ownedIds.Add(o.Value); if ((int?)swap.Attribute("slot") is int sw) ownedSlots.Add(sw); }
            var book = x.Element("bookMove"); if (book != null) { var o = Id((string)book.Attribute("id")); if (o.HasValue) ownedIds.Add(o.Value); }
            bool workPending = false, lockPending = false;
            foreach (var move in x.Elements("workMove").Where(m => (int?)m.Attribute("phase") != 4)) { workPending = true; var o = Id((string)move.Attribute("binding")); if (o.HasValue) ownedIds.Add(o.Value); }
            foreach (var attempt in x.Elements("lockpickAttempt").Where(a => (int?)a.Attribute("phase") != 2)) { lockPending = true; ownedSlots.Add(RebirthHumanEquipment.LockpicksSlot); }

            var bindings = x.Element("bindings")?.Elements("binding").ToArray() ?? new XElement[0];
            int recordCount = 0;
            if (bindings.Length > 0)
            {
                // A missing custody store is NOT proof of an empty inventory: hold (unsettled, no checkpoint, no marker).
                if (!RebirthNpcInventoryTransactionService.TryGetExistingNativeCustody(id, out var custody, out _) || custody == null) return Hold(npc, state, "custody-missing");
                if (state.Attempts >= MaxAttempts) return Hold(npc, state, "write-attempts-exhausted"); // held; see Attempts
                var records = new Dictionary<Guid, RebirthNpcNativeStackRecord>();
                foreach (var node in custody.Write().Elements("nativeStack"))
                    if (RebirthNpcNativeStackRecord.TryRead(node, out var record)) records[record.StackId] = record;
                recordCount = records.Count;
                Probe(npc, state, "row", x, recordCount);
                // Per binding (stack ID -> cell). Identical frames under different IDs are distinct stacks: compare by
                // multiset count, never by "this frame exists somewhere".
                var planned = new List<KeyValuePair<int, RebirthNpcNativeStackRecord>>();
                var needed = new Dictionary<string, int>(StringComparer.Ordinal);
                foreach (var binding in bindings.OrderBy(b => (string)b.Attribute("area") == "bag" ? 1 : 0))
                {
                    if (!TryLogicalSlot((string)binding.Attribute("area"), (int?)binding.Attribute("index") ?? -1, out int slot) ||
                        !Guid.TryParse((string)binding.Attribute("id"), out var stackId) || !records.TryGetValue(stackId, out var rec)) continue; // no record: that cell was empty when saved
                    if (ownedIds.Contains(stackId) || ownedSlots.Contains(slot)) continue; // owned by a pending swap / book / work move / lockpick attempt
                    if (!rec.TryDecode(out var decoded)) { Log.Warning("[REBIRTH NPC Equipment] saved frame undecodable entity=" + npc.entityId + " item=" + rec.ItemKey); return false; }
                    planned.Add(new KeyValuePair<int, RebirthNpcNativeStackRecord>(slot, rec));
                    string key = RebirthHumanEquipment.Frame(decoded);
                    needed[key] = (needed.TryGetValue(key, out int n) ? n : 0) + 1;
                }
                var liveCount = new Dictionary<string, int>(StringComparer.Ordinal);
                void Count(ItemStack stack)
                {
                    string f = RebirthHumanEquipment.Frame(stack);
                    if (f.Length != 0) liveCount[f] = (liveCount.TryGetValue(f, out int n) ? n : 0) + 1;
                }
                foreach (var info in RebirthP2NpcEquipmentSlots.GearSlots) Count(RebirthHumanEquipment.Get(npc, info.LogicalSlot));
                for (int i = 0; i < npc.bag.SlotCount; i++) Count(npc.bag.ItemGrid[i]);
                try
                {
                    foreach (var item in planned)
                    {
                        int slot = item.Key;
                        var rec = item.Value; rec.TryDecode(out var stack);
                        string frame = RebirthHumanEquipment.Frame(stack);
                        if (slot >= 10 && slot - 10 >= npc.bag.SlotCount) npc.bag.Resize(new Vector2i(Math.Min(169, slot - 9), 1));
                        var live = RebirthHumanEquipment.Get(npc, slot);
                        if (!live.IsEmpty())                                                            // never overwrite a live cell
                        {
                            string liveFrame = RebirthHumanEquipment.Frame(live);
                            if (liveFrame != frame && state.Conflicts.Add(slot))
                                Log.Warning("[REBIRTH NPC Equipment] saved frame NOT restored, cell occupied by another item entity=" + npc.entityId + " slot=" + slot +
                                    " saved=" + rec.ItemKey + " live=" + (live.itemValue?.ItemClass?.GetItemName() ?? "?"));
                            continue;
                        }
                        // Every saved copy of this frame already lives somewhere else (e.g. moved by the engine): no duplicate.
                        if (liveCount.TryGetValue(frame, out int have) && have >= needed[frame]) continue;
                        // Exact custody/readback (birth:false). Armor goes through Equipment.SetSlotItem which stores the clone
                        // as-is (Equipment.cs 476-488); a saved frame is already post-normalization, so it reads back exactly.
                        RebirthHumanEquipment.Put(npc, slot, stack);
                        liveCount[frame] = have + 1;
                        Log.Out("[REBIRTH NPC Equipment] restored saved frame entity=" + npc.entityId + " slot=" + slot + " item=" + rec.ItemKey + " count=" + rec.Count);
                    }
                }
                catch
                {
                    state.Attempts++; // bounded: no endless retry
                    throw;
                }
                // The native mode CVar is lost with the body too; it is not set by Put when nothing needed writing.
                if (npc.Buffs.GetCustomVar(RebirthHumanEquipment.NativeMode) != 1f) npc.Buffs.SetCustomVar(RebirthHumanEquipment.NativeMode, 1f, true);
                RestoreSelected(npc, x);
            }
            else Probe(npc, state, "row-no-bindings", x, 0);

            // Marker and failed-birth intent: the persisted attributes win; saved bindings imply birth gear was already issued
            // unless a failed intent (birthTries>0) is still being retried. Never regenerate a completed birth.
            string birth = (string)x.Attribute("birth"); int birthTries = (int?)x.Attribute("birthTries") ?? 0;
            float marker = birth == "-1" ? -1f : birth == "1" ? 1f : birthTries > 0 ? 0f : bindings.Length > 0 ? 1f : 0f;
            if (marker != 0f && npc.Buffs.GetCustomVar(MarkerCVar) == 0f) npc.Buffs.SetCustomVar(MarkerCVar, marker, true);
            if (marker == 0f && birthTries > 0 && npc.Buffs.GetCustomVar(TriesCVar) <= 0f)
            {
                npc.Buffs.SetCustomVar(TriesCVar, birthTries, true);
                npc.Buffs.SetCustomVar(PendingCVar, (int?)x.Attribute("birthPending") ?? 0, true);
            }

            if (workPending || lockPending) return Hold(npc, state, lockPending ? "lockpick-attempt-pending" : "work-move-pending"); // own recovery first; not checkpoint-safe
            state.Restored = true;
            if (book != null)
            {
                // Both reading cells were lost with the body: the owner's recovery restores the exact book into its bag cell
                // (all other cells are live now, so its custody save observes a whole body).
                if (!RebirthHumanEquipment.RecoverBookMoveAfterRestore(npc, x) || x.Element("bookMove") != null) return Hold(npc, state, "book-move-recovery-retained");
            }
            if (swapPending) return Hold(npc, state, "swap-pending"); // concluded by its owner (Handle action 2); cells are restored and checkpoint-safe
            return true;
        }
        catch (Exception e)
        {
            Log.Warning("[REBIRTH NPC Equipment] saved-frame restore retained entity=" + npc.entityId + " " + e.GetType().Name + ": " + e.Message);
            return false;
        }
    }

    // One line per loaded body: what the engine returned and what was recorded. Discriminates the reload candidates.
    private static void Probe(EntityRebirthHumanoidNPC npc, State state, string row, XElement x, int records)
    {
        if (state.Probed || !npc.RebirthStartingLoadoutRead) return;
        state.Probed = true;
        int Used(IEnumerable<ItemStack> items) { int n = 0; if (items != null) foreach (var s in items) if (s != null && !s.IsEmpty()) n++; return n; }
        Log.Out("[REBIRTH NPC Equipment] reload-probe entity=" + npc.entityId + " inst=" + RuntimeHelpers.GetHashCode(npc) + " row=" + row +
            " liveToolbelt=" + Used(npc.inventory.ItemGrid.items) + " liveEquipment=" + Used(npc.equipment.ItemGrid.items) + " liveBag=" + Used(npc.bag.ItemGrid.items) + "/" + npc.bag.SlotCount +
            " selected=" + npc.inventory.SelectedSlot + " marker=" + npc.Buffs.GetCustomVar(MarkerCVar) + " tries=" + npc.Buffs.GetCustomVar(TriesCVar) +
            " nativeMode=" + npc.Buffs.GetCustomVar(RebirthHumanEquipment.NativeMode) + " savedBindings=" + (x?.Element("bindings")?.Elements("binding").Count() ?? 0) +
            " savedRecords=" + records + " savedBirth=" + ((string)x?.Attribute("birth") ?? "-") + " savedTries=" + ((string)x?.Attribute("birthTries") ?? "-"));
    }

    // Make the birth gear, marker / failed-birth intent and selected slot DURABLE now (aggregate row + inventory custody + original
    // native snapshot, with readback; the existing Save path) instead of waiting for the next world-save checkpoint: a chunk unload
    // before that checkpoint would otherwise lose the gear and marker. Refuses while this body's records are still to be restored.
    // false = not durable yet; the caller retries from RetryTick (bounded) and the world-save checkpoint remains the fallback.
    internal static bool PersistBirth(EntityRebirthHumanoidNPC npc, int value)
    {
        try
        {
            if (!ThreadManager.IsMainThread() || npc?.RebirthRuntimeState == null || npc.world?.IsRemote() != false ||
                IsRestorePending(npc) || npc.IsPreparedRestorationPending) return false;
            var id = npc.RebirthRuntimeState.StableId;
            if (!RebirthNpcAggregatePersistenceStore.TryGet(id, out _)) RebirthNpcAggregatePersistenceStore.CaptureRuntimeEnvelopes(); // same first-row step Handle uses
            if (!RebirthNpcAggregatePersistenceStore.TryGet(id, out _)) return false;
            var x = RebirthHumanEquipment.Read(npc);
            string before = x.ToString(SaveOptions.DisableFormatting);
            ApplyBirthAttributes(npc, x);
            // A first persistence has no bindings yet: Save even when no attribute changed so the gear itself becomes durable.
            if (x.ToString(SaveOptions.DisableFormatting) == before && x.Element("bindings") != null) return true;
            RebirthHumanEquipment.Save(npc, x);
            return true;
        }
        catch (Exception e) { Log.Warning("[REBIRTH NPC Equipment] birth persistence deferred entity=" + (npc != null ? npc.entityId : -1) + " " + e.GetType().Name + ": " + e.Message); return false; }
    }
}

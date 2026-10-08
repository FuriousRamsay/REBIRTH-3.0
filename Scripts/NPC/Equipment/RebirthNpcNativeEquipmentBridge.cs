using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

#nullable disable

public enum RebirthNpcNativeEquipmentApplyResult : byte
{
    Applied = 0,
    NoLiveEntity = 1,
    UnsupportedEntity = 2,
    ItemUnresolved = 3,
    NativeSurfaceUnavailable = 4,
    StaleRevision = 5
}

public sealed class RebirthNpcNativeItemDescriptor
{
    public string ItemKey { get; internal set; }
    public int ItemType { get; internal set; }
    public int HoldType { get; internal set; }
    public bool Resolved { get; internal set; }
}

/// <summary>
/// Centralized conversion boundary between the authoritative semantic equipment
/// ledger and 7DTD native ItemValue/equipment surfaces. No gameplay authority is
/// held here; this adapter is projection-only and may be rebuilt at any time.
/// </summary>
public static class RebirthNpcNativeEquipmentBridge
{
    private sealed class ProjectionState
    {
        public uint RequestedRevision;
        public uint AppliedRevision;
        public readonly Dictionary<RebirthNpcEquipmentSlot, string> RequestedSlots =
            new Dictionary<RebirthNpcEquipmentSlot, string>();
        public readonly Dictionary<RebirthNpcEquipmentSlot, string> AppliedSlots =
            new Dictionary<RebirthNpcEquipmentSlot, string>();
    }

    private static readonly object Sync = new object();
    private static readonly Dictionary<RebirthNpcStableId, ProjectionState> Applied =
        new Dictionary<RebirthNpcStableId, ProjectionState>();
    private static readonly Dictionary<string, RebirthNpcNativeItemDescriptor> DescriptorCache =
        new Dictionary<string, RebirthNpcNativeItemDescriptor>(StringComparer.OrdinalIgnoreCase);

    private static long resolved;
    private static long unresolved;
    private static long projections;
    private static long stale;
    private static long unavailable;
    private static long removals;
    private static long retries;
    private static long partial;

    public static bool TryResolve(string itemKey, out RebirthNpcNativeItemDescriptor descriptor)
    {
        itemKey = (itemKey ?? string.Empty).Trim();
        descriptor = null;
        if (itemKey.Length == 0) return false;
        lock (Sync)
        {
            if (DescriptorCache.TryGetValue(itemKey, out descriptor) && descriptor.Resolved)
                return true;
        }

        RebirthNpcNativeItemDescriptor created = new RebirthNpcNativeItemDescriptor
        {
            ItemKey = itemKey,
            ItemType = -1,
            HoldType = 0,
            Resolved = false
        };
        try
        {
            ItemValue value = ItemClass.GetItem(itemKey, false);
            if (!value.IsEmpty())
            {
                created.ItemType = value.type;
                ItemClass itemClass = ItemClass.list[value.type];
                created.HoldType = itemClass != null ? itemClass.HoldType.Value : 0;
                created.Resolved = true;
            }
        }
        catch (Exception ex)
        {
            Log.Warning("[REBIRTH NPC Native Equipment] Failed to resolve item '" + itemKey + "': " + ex.GetType().Name + ": " + ex.Message);
        }
        lock (Sync) DescriptorCache[itemKey] = created;
        descriptor = created;
        if (created.Resolved) Interlocked.Increment(ref resolved);
        else Interlocked.Increment(ref unresolved);
        return created.Resolved;
    }

    public static RebirthNpcNativeEquipmentApplyResult ApplyAuthoritativeSnapshot(RebirthNpcEquipmentSnapshot snapshot)
    {
        if (snapshot == null || snapshot.NpcId.IsEmpty)
            return RebirthNpcNativeEquipmentApplyResult.NoLiveEntity;
        int entityId;
        if (!RebirthNpcRuntimeRegistry.TryGetEntityId(snapshot.NpcId, out entityId))
            return RebirthNpcNativeEquipmentApplyResult.NoLiveEntity;
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        EntityRebirthHumanoidNPC npc = world != null ? world.GetEntity(entityId) as EntityRebirthHumanoidNPC : null;
        if (npc == null) return RebirthNpcNativeEquipmentApplyResult.UnsupportedEntity;
        return ApplyToEntity(npc, snapshot.NpcId, snapshot.Revision, snapshot.Slots);
    }

    public static RebirthNpcNativeEquipmentApplyResult ApplyReplicatedSnapshot(
        EntityRebirthHumanoidNPC npc,
        RebirthNpcStableId npcId,
        uint revision,
        IDictionary<RebirthNpcEquipmentSlot, string> slots)
    {
        if (npc == null) return RebirthNpcNativeEquipmentApplyResult.NoLiveEntity;
        return ApplyToEntity(npc, npcId, revision, slots);
    }

    public static void Forget(RebirthNpcStableId npcId)
    {
        lock (Sync) Applied.Remove(npcId);
    }

    public static void InvalidateProjection(RebirthNpcStableId npcId, bool invalidateUnresolvedDescriptors)
    {
        lock (Sync)
        {
            Applied.Remove(npcId);
            if (invalidateUnresolvedDescriptors)
            {
                List<string> remove = null;
                foreach (KeyValuePair<string,RebirthNpcNativeItemDescriptor> pair in DescriptorCache)
                    if (pair.Value == null || !pair.Value.Resolved)
                    { if (remove == null) remove = new List<string>(); remove.Add(pair.Key); }
                if (remove != null) for (int i=0;i<remove.Count;i++) DescriptorCache.Remove(remove[i]);
            }
        }
    }

    public static string GetReport()
    {
        lock (Sync)
        {
            return "[REBIRTH NPC Native Equipment] projections=" + projections +
                " tracked=" + Applied.Count +
                " cache=" + DescriptorCache.Count +
                " resolved=" + resolved +
                " unresolved=" + unresolved +
                " stale=" + stale +
                " unavailable=" + unavailable +
                " removals=" + removals +
                " retries=" + retries +
                " partial=" + partial;
        }
    }

    private static RebirthNpcNativeEquipmentApplyResult ApplyToEntity(
        EntityRebirthHumanoidNPC npc,
        RebirthNpcStableId npcId,
        uint revision,
        IDictionary<RebirthNpcEquipmentSlot, string> slots)
    {
        Dictionary<RebirthNpcEquipmentSlot,string> desired = new Dictionary<RebirthNpcEquipmentSlot,string>();
        if (slots != null)
            foreach (KeyValuePair<RebirthNpcEquipmentSlot,string> pair in slots)
                if (Enum.IsDefined(typeof(RebirthNpcEquipmentSlot), pair.Key) && !string.IsNullOrWhiteSpace(pair.Value))
                    desired[pair.Key] = pair.Value.Trim();

        ProjectionState state;
        lock (Sync)
        {
            if (!Applied.TryGetValue(npcId,out state)) Applied[npcId]=state=new ProjectionState();
            if (revision < state.RequestedRevision)
            { Interlocked.Increment(ref stale); return RebirthNpcNativeEquipmentApplyResult.StaleRevision; }
            if (revision == state.RequestedRevision && state.AppliedRevision < state.RequestedRevision)
                Interlocked.Increment(ref retries);
            state.RequestedRevision=revision;
            state.RequestedSlots.Clear();
            foreach (KeyValuePair<RebirthNpcEquipmentSlot,string> pair in desired) state.RequestedSlots[pair.Key]=pair.Value;
        }

        bool failed=false;
        bool unresolvedFailure=false;
        Dictionary<RebirthNpcEquipmentSlot,string> appliedNow=new Dictionary<RebirthNpcEquipmentSlot,string>();

        // First clear every formerly applied native slot that the authoritative semantic
        // snapshot no longer contains. Removal success is part of convergence, not cosmetic cleanup.
        RebirthNpcEquipmentSlot[] previousSlots;
        lock(Sync){previousSlots=new RebirthNpcEquipmentSlot[state.AppliedSlots.Count];state.AppliedSlots.Keys.CopyTo(previousSlots,0);}
        for(int i=0;i<previousSlots.Length;i++)
        {
            RebirthNpcEquipmentSlot slot=previousSlots[i];
            if(desired.ContainsKey(slot))continue;
            if(TryClearNativeSlot(npc,slot)) Interlocked.Increment(ref removals); else failed=true;
        }

        foreach(KeyValuePair<RebirthNpcEquipmentSlot,string> pair in desired)
        {
            RebirthNpcNativeItemDescriptor descriptor;
            if(!TryResolve(pair.Value,out descriptor))
            { unresolvedFailure=true; failed=true; continue; }
            if(TryApplyNativeSlot(npc,pair.Key,descriptor)) appliedNow[pair.Key]=pair.Value;
            else failed=true;
        }

        lock(Sync)
        {
            // Preserve only slots proven applied during this pass. Failed requested slots remain
            // requested but unapplied, so an equal-revision retry is meaningful and observable.
            state.AppliedSlots.Clear();
            foreach(KeyValuePair<RebirthNpcEquipmentSlot,string> pair in appliedNow) state.AppliedSlots[pair.Key]=pair.Value;
            if(!failed) state.AppliedRevision=revision;
            Applied[npcId]=state;
        }

        npc.RefreshRebirthModelEquipment();
        Interlocked.Increment(ref projections);
        if(failed) Interlocked.Increment(ref partial);
        if(unresolvedFailure) return RebirthNpcNativeEquipmentApplyResult.ItemUnresolved;
        if(failed){Interlocked.Increment(ref unavailable);return RebirthNpcNativeEquipmentApplyResult.NativeSurfaceUnavailable;}
        return RebirthNpcNativeEquipmentApplyResult.Applied;
    }

    private static bool TryApplyNativeSlot(EntityRebirthHumanoidNPC npc, RebirthNpcEquipmentSlot slot,
        RebirthNpcNativeItemDescriptor descriptor)
    {
        try
        {
            ItemValue value = ItemClass.GetItem(descriptor.ItemKey, false);
            int nativeArmorSlot = ToNativeArmorSlot(slot);
            if (nativeArmorSlot >= 0 && npc.equipment != null && nativeArmorSlot < npc.equipment.GetSlotCount())
            {
                npc.equipment.ItemGrid[nativeArmorSlot] = new ItemStack(value.Clone(), 1);
                return true;
            }

            if (slot == RebirthNpcEquipmentSlot.PrimaryWeapon)
                return TrySetHeldItem(npc, value);

            // Secondary and utility remain authoritative semantic slots until a
            // weapon-swap controller selects one for the native held-item surface.
            return slot == RebirthNpcEquipmentSlot.SecondaryWeapon || slot == RebirthNpcEquipmentSlot.Utility;
        }
        catch (Exception ex)
        {
            Log.Warning("[REBIRTH NPC Native Equipment] Apply failed entity=" + npc.entityId +
                " slot=" + slot + " item=" + descriptor.ItemKey + " " + ex.GetType().Name + ": " + ex.Message);
            return false;
        }
    }

    private static bool TryClearNativeSlot(EntityRebirthHumanoidNPC npc, RebirthNpcEquipmentSlot slot)
    {
        try
        {
            int nativeArmorSlot=ToNativeArmorSlot(slot);
            if(nativeArmorSlot>=0)
            {
                if(npc.equipment==null||nativeArmorSlot>=npc.equipment.GetSlotCount())return false;
                npc.equipment.ItemGrid[nativeArmorSlot] = ItemStack.Empty.Clone();
                return true;
            }
            if(slot==RebirthNpcEquipmentSlot.PrimaryWeapon)
            {
                if(npc.inventory==null)return false;
                npc.inventory.SetItem(npc.inventory.holdingItemIdx, ItemStack.Empty.Clone());
                return true;
            }
            // Secondary/utility are semantic-only until selected, so absence is already native-converged.
            return slot==RebirthNpcEquipmentSlot.SecondaryWeapon||slot==RebirthNpcEquipmentSlot.Utility;
        }
        catch(Exception ex)
        {
            Log.Warning("[REBIRTH NPC Native Equipment] Clear failed entity="+npc.entityId+" slot="+slot+" "+ex.GetType().Name+": "+ex.Message);
            return false;
        }
    }

    private static int ToNativeArmorSlot(RebirthNpcEquipmentSlot slot)
    {
        switch (slot)
        {
            case RebirthNpcEquipmentSlot.Head: return 0;
            case RebirthNpcEquipmentSlot.Face: return 1;
            case RebirthNpcEquipmentSlot.Chest: return 2;
            case RebirthNpcEquipmentSlot.Hands: return 3;
            case RebirthNpcEquipmentSlot.Legs: return 4;
            case RebirthNpcEquipmentSlot.Feet: return 5;
            default: return -1;
        }
    }

    private static bool TrySetHeldItem(EntityRebirthHumanoidNPC npc, ItemValue value)
    {
        if (npc == null || npc.inventory == null || value == null)
            return false;

        int index = npc.inventory.holdingItemIdx;
        npc.inventory.SetItem(index, new ItemStack(value.Clone(), 1));
        return true;
    }
}

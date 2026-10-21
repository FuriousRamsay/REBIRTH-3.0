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
public static partial class RebirthNpcNativeEquipmentBridge
{
    private sealed class ProjectionState
    {
        public ulong Generation; public bool Converged,Completed;
        public uint RequestedRevision;
        public uint AppliedRevision;
        public readonly Dictionary<RebirthNpcEquipmentSlot, string> RequestedSlots =
            new Dictionary<RebirthNpcEquipmentSlot, string>();
        public readonly Dictionary<RebirthNpcEquipmentSlot, string> AppliedSlots =
            new Dictionary<RebirthNpcEquipmentSlot, string>();
    }

    private static readonly object Sync = new object();
    private static Dictionary<RebirthNpcStableId, ProjectionState> Applied =
        new Dictionary<RebirthNpcStableId, ProjectionState>();
    private static readonly Dictionary<string, RebirthNpcNativeItemDescriptor> DescriptorCache =
        new Dictionary<string, RebirthNpcNativeItemDescriptor>(StringComparer.OrdinalIgnoreCase);

    private static World projectionWorld;private static string projectionDirectory;private static ulong generationFloor;
    private static void SetProjectionScope()
    {
        var world=GameManager.Instance?.World;string directory=GameIO.GetSaveGameDir();
        if(ReferenceEquals(projectionWorld,world)&&projectionDirectory==directory)return;
        if(Applied.Count!=0)throw new InvalidOperationException("Original native equipment projection targets require typed retirement");
        Applied.Clear();generationFloor=0;projectionWorld=world;projectionDirectory=directory;
    }    private static long resolved;
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
        return ApplyToEntity(npc, snapshot.NpcId, snapshot.Revision, snapshot.Slots,RebirthNpcRecoveryGeneration.RequireCurrent());
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

    internal static bool ObserveRecoveryGeneration(ulong generation){if(!RebirthNpcRecoveryProjectionScope.Observe(generation))return false;lock(Sync){SetProjectionScope();if(generation<generationFloor)return false;if(generation>generationFloor)generationFloor=generation;return true;}}
    internal static bool CanApplyNativeRecoveryImage(RebirthNpcStableId id,ulong generation,uint revision)
    {
        if(!RebirthNpcRecoveryProjectionScope.Observe(generation))return false;
        lock(Sync)
        {
            SetProjectionScope();
            return generation>=generationFloor&&(!Applied.TryGetValue(id,out var state)||generation>state.Generation||
                (generation==state.Generation&&revision>=state.RequestedRevision));
        }
    }
    internal static RebirthNpcNativeEquipmentApplyResult ApplyRecoverySnapshot(EntityRebirthHumanoidNPC npc,RebirthNpcStableId npcId,ulong generation,uint revision,IDictionary<RebirthNpcEquipmentSlot,string> slots)
    {if(npc==null)return RebirthNpcNativeEquipmentApplyResult.NoLiveEntity;return ApplyToEntity(npc,npcId,revision,slots,generation);}
    // Positive current native readback of an already applied snapshot only.
    // Never creates authority from an ACK/probe or reconstructs absent gear.
    internal static bool TryObserveRecoverySnapshot(EntityRebirthHumanoidNPC npc,ulong generation,out uint revision,out Dictionary<RebirthNpcEquipmentSlot,string> slots)
    {
        revision=0;slots=null;if(npc?.RebirthRuntimeState==null)return false;
        var id=npc.RebirthRuntimeState.StableId;
        lock(Sync)
        {
            SetProjectionScope();
            if(!Applied.TryGetValue(id,out var state)||state.Generation!=generation||!state.Completed)return false;
            revision=state.RequestedRevision;slots=new Dictionary<RebirthNpcEquipmentSlot,string>(state.RequestedSlots);
        }
        return ApplyRecoverySnapshot(npc,id,generation,revision,slots)==RebirthNpcNativeEquipmentApplyResult.Applied;
    }    internal static bool IsRecoveryConverged(RebirthNpcStableId npcId,ulong generation,uint revision)
    {lock(Sync)return Applied.TryGetValue(npcId,out var state)&&state.Generation==generation&&state.RequestedRevision==revision&&state.AppliedRevision==revision&&state.Converged;}
    public static void Forget(RebirthNpcStableId npcId)
    {
        lock (Sync) Applied.Remove(npcId);
    }
    internal static object CaptureAuditState(RebirthNpcStableId npcId)
    {
        lock(Sync)
        {
            if(!ReferenceEquals(projectionWorld,GameManager.Instance?.World)||projectionDirectory!=GameIO.GetSaveGameDir())return new{tracked=false,scopeCurrent=false};
            if(!Applied.TryGetValue(npcId,out var state))return new{tracked=false};
            return new{tracked=true,scopeCurrent=true,generation=state.Generation,converged=state.Converged,completed=state.Completed,requestedRevision=state.RequestedRevision,appliedRevision=state.AppliedRevision,
                requestedSlots=new Dictionary<RebirthNpcEquipmentSlot,string>(state.RequestedSlots),
                retainedNativeSlots=new Dictionary<RebirthNpcEquipmentSlot,string>(state.AppliedSlots)};
        }
    }

    public static void InvalidateProjection(RebirthNpcStableId npcId, bool invalidateUnresolvedDescriptors)
    {
        lock (Sync)
        {
            SetProjectionScope();if(Applied.TryGetValue(npcId,out var retained)){retained.Converged=false;retained.Completed=false;}
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
        IDictionary<RebirthNpcEquipmentSlot, string> slots,ulong generation=0)
    {
        if(npc==null||!npc.NativeIdentityReadable)return RebirthNpcNativeEquipmentApplyResult.NoLiveEntity;
        Dictionary<RebirthNpcEquipmentSlot,string> desired = new Dictionary<RebirthNpcEquipmentSlot,string>();
        if (slots != null)
            foreach (KeyValuePair<RebirthNpcEquipmentSlot,string> pair in slots)
                if (Enum.IsDefined(typeof(RebirthNpcEquipmentSlot), pair.Key) && !string.IsNullOrWhiteSpace(pair.Value))
                    desired[pair.Key] = pair.Value.Trim();

        ProjectionState state;bool repeat;
        lock (Sync)
        {
            SetProjectionScope();if(!RebirthNpcRecoveryProjectionScope.Observe(generation)||generation<generationFloor){Interlocked.Increment(ref stale);return RebirthNpcNativeEquipmentApplyResult.StaleRevision;}if(generation>generationFloor)generationFloor=generation;
            if (!Applied.TryGetValue(npcId,out state)) Applied[npcId]=state=new ProjectionState();
            if(generation<state.Generation){Interlocked.Increment(ref stale);return RebirthNpcNativeEquipmentApplyResult.StaleRevision;}
            if(generation>state.Generation)
            {
                // Preserve old native cleanup targets across a lower recovered
                // semantic revision. They disappear only after actual clear/apply.
                state.Generation=generation;state.RequestedRevision=0;state.AppliedRevision=0;state.Converged=false;state.Completed=false;
            }
            if (revision < state.RequestedRevision)
            { Interlocked.Increment(ref stale); return RebirthNpcNativeEquipmentApplyResult.StaleRevision; }
            if (revision == state.RequestedRevision && state.AppliedRevision < state.RequestedRevision)
                Interlocked.Increment(ref retries);
            repeat=state.Completed&&revision==state.RequestedRevision&&SameSlots(state.RequestedSlots,desired);
            if(!repeat)state.Completed=false;
            state.Converged=false;state.RequestedRevision=revision;
            state.RequestedSlots.Clear();
            foreach (KeyValuePair<RebirthNpcEquipmentSlot,string> pair in desired) state.RequestedSlots[pair.Key]=pair.Value;
        }

        if(repeat)
        {
            bool observed=true,unresolved=false;
            foreach(var pair in desired)
            {
                if(!TryResolve(pair.Value,out var descriptor)){unresolved=true;observed=false;continue;}
                if(!NativeSlotMatches(npc,pair.Key,descriptor.ItemType))observed=false;
            }
            lock(Sync)state.Converged=observed;
            // Retry is observation only. A broken/removed/changed native item is
            // not reconstructed from an already completed unchanged snapshot.
            return observed?RebirthNpcNativeEquipmentApplyResult.Applied:unresolved?RebirthNpcNativeEquipmentApplyResult.ItemUnresolved:RebirthNpcNativeEquipmentApplyResult.NativeSurfaceUnavailable;
        }

        bool failed=false;
        bool unresolvedFailure=false;
        Dictionary<RebirthNpcEquipmentSlot,string> appliedNow;
        lock(Sync)appliedNow=new Dictionary<RebirthNpcEquipmentSlot,string>(state.AppliedSlots);

        // First clear every formerly applied native slot that the authoritative semantic
        // snapshot no longer contains. Removal success is part of convergence, not cosmetic cleanup.
        RebirthNpcEquipmentSlot[] previousSlots;
        previousSlots=new RebirthNpcEquipmentSlot[appliedNow.Count];appliedNow.Keys.CopyTo(previousSlots,0);
        for(int i=0;i<previousSlots.Length;i++)
        {
            RebirthNpcEquipmentSlot slot=previousSlots[i];
            if(desired.ContainsKey(slot))continue;
            if(TryClearNativeSlot(npc,slot,appliedNow[slot])){appliedNow.Remove(slot);Interlocked.Increment(ref removals);}else failed=true;
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
            // Retain prior native slots until a clear/replacement succeeds.
            // Forgetting a failed removal would prevent an equal-revision retry
            // from clearing the still-visible old equipment.
            state.AppliedSlots.Clear();
            foreach(KeyValuePair<RebirthNpcEquipmentSlot,string> pair in appliedNow) state.AppliedSlots[pair.Key]=pair.Value;
            if(!failed){state.AppliedRevision=revision;state.Converged=true;state.Completed=true;}
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
            if(npc.Buffs.GetCustomVar(RebirthHumanEquipment.NativeMode)==1f)return true; // native full-item loadout owns these slots
            ItemValue value = ItemClass.GetItem(descriptor.ItemKey, false);
            int nativeArmorSlot = ToNativeArmorSlot(slot);
            if (nativeArmorSlot >= 0 && npc.equipment != null && nativeArmorSlot < npc.equipment.GetSlotCount())
            {
                if(NativeSlotMatches(npc,slot,descriptor.ItemType))return true; // preserve full native wear/metadata
                npc.equipment.ItemGrid[nativeArmorSlot] = new ItemStack(value.Clone(), 1);
                return NativeSlotMatches(npc,slot,descriptor.ItemType);
            }

            if (slot == RebirthNpcEquipmentSlot.PrimaryWeapon)
                return NativeSlotMatches(npc,slot,descriptor.ItemType)||(TrySetHeldItem(npc,value)&&NativeSlotMatches(npc,slot,descriptor.ItemType));

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
    private static bool SameSlots(IDictionary<RebirthNpcEquipmentSlot,string> left,IDictionary<RebirthNpcEquipmentSlot,string> right)
    {if(left.Count!=right.Count)return false;foreach(var pair in left)if(!right.TryGetValue(pair.Key,out var value)||value!=pair.Value)return false;return true;}
    private static bool NativeSlotMatches(EntityRebirthHumanoidNPC npc,RebirthNpcEquipmentSlot slot,int type)
    {
        try
        {
            if(npc?.Buffs.GetCustomVar(RebirthHumanEquipment.NativeMode)==1f)return true;
            int armor=ToNativeArmorSlot(slot);
            if(armor>=0)
            {if(npc?.equipment==null||armor>=npc.equipment.GetSlotCount())return false;var item=npc.equipment.GetSlotItem(armor);return type==0?item==null||item.IsEmpty():item!=null&&!item.IsEmpty()&&item.type==type;}
            if(slot==RebirthNpcEquipmentSlot.PrimaryWeapon)
            {if(npc?.inventory==null)return false;return FindNativeWeapon(npc,type)>=0;}
            return slot==RebirthNpcEquipmentSlot.SecondaryWeapon||slot==RebirthNpcEquipmentSlot.Utility;
        }
        catch{return false;}
    }

    private static bool TryClearNativeSlot(EntityRebirthHumanoidNPC npc, RebirthNpcEquipmentSlot slot,string previousItem)
    {
        try
        {
            if(npc.Buffs.GetCustomVar(RebirthHumanEquipment.NativeMode)==1f)return true;
            int nativeArmorSlot=ToNativeArmorSlot(slot);
            if(nativeArmorSlot>=0)
            {
                if(npc.equipment==null||nativeArmorSlot>=npc.equipment.GetSlotCount())return false;
                npc.equipment.ItemGrid[nativeArmorSlot] = ItemStack.Empty.Clone();
                return NativeSlotMatches(npc,slot,0);
            }
            if(slot==RebirthNpcEquipmentSlot.PrimaryWeapon)
            {
                if(npc.inventory==null)return false;
                if(!TryResolve(previousItem,out var descriptor))return false;
                int index=FindNativeWeapon(npc,descriptor.ItemType);
                if(index<0)return true;
                npc.inventory.SetItem(index, ItemStack.Empty.Clone());
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

    private static int FindNativeWeapon(EntityRebirthHumanoidNPC npc,int type)
    {
        var items=npc?.inventory?.ItemGrid.items;if(items==null)return -1;
        for(int i=0;i<items.Length;i++)
        {
            var item=items[i];
            if(type==0?item==null||item.IsEmpty():item!=null&&!item.IsEmpty()&&item.itemValue.type==type)return i;
        }
        return -1;
    }
    private static bool TrySetHeldItem(EntityRebirthHumanoidNPC npc, ItemValue value)
    {
        if (npc == null || npc.inventory == null || value == null)
            return false;

        int index = FindNativeWeapon(npc,value.type);
        if(index>=0)return true; // preserve the actual weapon when another item is held
        index=FindNativeWeapon(npc,0);
        if(index<0)return false; // do not overwrite other equipment
        npc.inventory.SetItem(index, new ItemStack(value.Clone(), 1));
        return true;
    }
}



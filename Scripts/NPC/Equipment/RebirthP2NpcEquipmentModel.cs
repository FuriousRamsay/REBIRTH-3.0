using System;
using System.Collections.Generic;
using System.Text;

// P2/G03: the one authoritative description of what each human NPC logical equipment
// slot IS. Logical IDs are the saved meanings of RebirthHumanEquipment (0-9 gear, 10-178
// cargo, 179 Lockpicks) and never change. The legacy semantic enum
// (RebirthNpcEquipmentSlot) is only translated into them, never the other way round.
internal enum RebirthP2EquipmentRole : byte
{
    WornArmor = 0, ActiveMelee = 1, ActiveRanged = 2, AmmoReserve = 3, WornBackpack = 4, Lockpicks = 5, Cargo = 6
}

internal enum RebirthP2EquipmentArea : byte { Equipment = 0, Toolbelt = 1, Bag = 2 }

internal sealed class RebirthP2NpcEquipmentSlotInfo
{
    public int LogicalSlot;
    public string Name;
    public RebirthP2EquipmentRole Role;
    public RebirthP2EquipmentArea Area;
    public int NativeIndex;
    // Fixed passive effect from simply being in this cell. Melee/ranged are conditional
    // on being the held item; ammo, Lockpicks and cargo never confer a passive effect.
    public bool AlwaysEffective;
    public bool EffectWhenHeld;
}

internal sealed class RebirthP2NpcEquipmentEntry
{
    public RebirthP2NpcEquipmentSlotInfo Slot;
    public ItemStack Stack;      // detached clone; carries the full native ItemValue (mods, quality, wear)
    public bool Occupied;
    public bool Effective;       // currently contributes a worn/held native effect
    public bool LegacyOccupant;  // Lockpicks cell holding a non-lockpick stack from an older save
}

internal static class RebirthP2NpcEquipmentSlots
{
    private static readonly RebirthP2NpcEquipmentSlotInfo[] Gear = BuildGear();

    private static RebirthP2NpcEquipmentSlotInfo[] BuildGear()
    {
        var list = new List<RebirthP2NpcEquipmentSlotInfo>();
        for (int i = 0; i < RebirthHumanEquipment.Names.Length; i++)
        {
            var s = new RebirthP2NpcEquipmentSlotInfo { LogicalSlot = i, Name = RebirthHumanEquipment.Names[i] };
            if (i < 4) { s.Role = RebirthP2EquipmentRole.WornArmor; s.Area = RebirthP2EquipmentArea.Equipment; s.NativeIndex = i; s.AlwaysEffective = true; }
            else if (i == 9) { s.Role = RebirthP2EquipmentRole.WornBackpack; s.Area = RebirthP2EquipmentArea.Equipment; s.NativeIndex = (int)EquipmentSlots.Backpack; s.AlwaysEffective = true; }
            else
            {
                s.Area = RebirthP2EquipmentArea.Toolbelt; s.NativeIndex = i - 4;
                s.Role = i == 4 ? RebirthP2EquipmentRole.ActiveMelee : i == 5 ? RebirthP2EquipmentRole.ActiveRanged : RebirthP2EquipmentRole.AmmoReserve;
                s.EffectWhenHeld = i <= 5;
            }
            list.Add(s);
        }
        list.Add(new RebirthP2NpcEquipmentSlotInfo
        {
            LogicalSlot = RebirthHumanEquipment.LockpicksSlot, Name = "Lockpicks", Role = RebirthP2EquipmentRole.Lockpicks,
            Area = RebirthP2EquipmentArea.Toolbelt, NativeIndex = RebirthHumanEquipment.LockpicksToolbeltIndex
        });
        return list.ToArray();
    }

    // Gear slots in display order: 0-9 then Lockpicks (179). Do not mutate.
    internal static IReadOnlyList<RebirthP2NpcEquipmentSlotInfo> GearSlots => Gear;

    internal static bool TryDescribe(int logicalSlot, out RebirthP2NpcEquipmentSlotInfo info)
    {
        info = null;
        if (logicalSlot >= 0 && logicalSlot < RebirthHumanEquipment.Names.Length) { info = Gear[logicalSlot]; return true; }
        if (logicalSlot == RebirthHumanEquipment.LockpicksSlot) { info = Gear[Gear.Length - 1]; return true; }
        if (logicalSlot >= 10 && logicalSlot < RebirthHumanEquipment.LockpicksSlot)
        {
            info = new RebirthP2NpcEquipmentSlotInfo
            {
                LogicalSlot = logicalSlot, Name = "Cargo" + (logicalSlot - 10), Role = RebirthP2EquipmentRole.Cargo,
                Area = RebirthP2EquipmentArea.Bag, NativeIndex = logicalSlot - 10
            };
            return true;
        }
        return false;
    }

    // Legacy semantic slot -> logical slot. Face/Legs and Utility have no native human
    // counterpart (-1); Primary/Secondary resolve by what the item actually is.
    internal static bool TryMapSemantic(RebirthNpcEquipmentSlot semantic, ItemValue item, out int logicalSlot)
    {
        logicalSlot = -1;
        switch (semantic)
        {
            case RebirthNpcEquipmentSlot.Head: logicalSlot = 0; return true;
            case RebirthNpcEquipmentSlot.Chest: logicalSlot = 1; return true;
            case RebirthNpcEquipmentSlot.Hands: logicalSlot = 2; return true;
            case RebirthNpcEquipmentSlot.Feet: logicalSlot = 3; return true;
            case RebirthNpcEquipmentSlot.PrimaryWeapon:
            case RebirthNpcEquipmentSlot.SecondaryWeapon:
                if (item == null || item.IsEmpty()) return false;
                var stack = new ItemStack(item, 1);
                if (RebirthHumanEquipment.Accept(4, stack)) { logicalSlot = 4; return true; }
                if (RebirthHumanEquipment.Accept(5, stack)) { logicalSlot = 5; return true; }
                return false;
            default: return false;
        }
    }

    // Operation string for a request that must only apply while the target cell still
    // holds what the client displayed: "<sourceFrame>|<expectedTargetFrame>". Servers
    // without the guard (and clients that omit it) keep the original plain-frame form.
    internal static string GuardedOperation(ItemStack source, ItemStack expectedTarget) =>
        RebirthHumanEquipment.Frame(source) + "|" + RebirthHumanEquipment.Frame(expectedTarget);
}

// Read API for the Companion/Hire windows. Everything here is detached and allocates only
// when called; call on presentation or a settled reply, never every frame.
internal static class RebirthP2NpcEquipmentReadModel
{
    internal static bool IsNativeOwned(EntityRebirthHumanoidNPC npc) =>
        npc != null && npc.Buffs != null && npc.Buffs.GetCustomVar(RebirthHumanEquipment.NativeMode) == 1f;

    // Native mode owns the ten gear cells; the semantic ledger must not also claim them.
    internal static bool IsNativeOwned(RebirthNpcStableId id)
    {
        // Main-thread state only; off-thread callers get a conservative deny.
        if (!ThreadManager.IsMainThread()) return true;
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        int entityId;
        return world != null && RebirthNpcRuntimeRegistry.TryGetEntityId(id, out entityId) &&
            IsNativeOwned(world.GetEntity(entityId) as EntityRebirthHumanoidNPC);
    }

    internal static RebirthP2NpcEquipmentEntry CaptureSlot(EntityRebirthHumanoidNPC npc, int logicalSlot)
    {
        if (npc == null || !RebirthP2NpcEquipmentSlots.TryDescribe(logicalSlot, out var info)) return null;
        var stack = RebirthHumanEquipment.Get(npc, logicalSlot);
        bool occupied = !stack.IsEmpty();
        var entry = new RebirthP2NpcEquipmentEntry { Slot = info, Stack = stack, Occupied = occupied };
        if (logicalSlot == RebirthHumanEquipment.LockpicksSlot)
        {
            entry.LegacyOccupant = occupied && !RebirthHumanEquipment.Accept(logicalSlot, stack);
            entry.Effective = occupied && !entry.LegacyOccupant;
        }
        else if (occupied)
        {
            entry.Effective = info.AlwaysEffective ||
                info.EffectWhenHeld && npc.inventory != null && npc.inventory.SelectedSlot == info.NativeIndex;
        }
        return entry;
    }

    // The ten native gear slots followed by Lockpicks.
    internal static List<RebirthP2NpcEquipmentEntry> CaptureGear(EntityRebirthHumanoidNPC npc)
    {
        var result = new List<RebirthP2NpcEquipmentEntry>(RebirthP2NpcEquipmentSlots.GearSlots.Count);
        foreach (var info in RebirthP2NpcEquipmentSlots.GearSlots) result.Add(CaptureSlot(npc, info.LogicalSlot));
        return result;
    }

    internal static int EquippedLockpickCount(EntityRebirthHumanoidNPC npc) => RebirthHumanEquipment.EquippedLockpickCount(npc);

    // Backpack-style possession never counts: only cells flagged Effective feed native
    // EffectManager.GetValue through npc.equipment/held item.
    internal static bool ConfersWornEffect(EntityRebirthHumanoidNPC npc, int logicalSlot) =>
        CaptureSlot(npc, logicalSlot)?.Effective == true;

    // Stable hash of gear-cell contents (including mods/quality/wear) for change detection.
    internal static string GearToken(EntityRebirthHumanoidNPC npc)
    {
        ulong hash = 14695981039346656037UL;
        foreach (var info in RebirthP2NpcEquipmentSlots.GearSlots)
        {
            string frame = RebirthHumanEquipment.Frame(RebirthHumanEquipment.Get(npc, info.LogicalSlot));
            hash = Mix(hash, (ulong)info.LogicalSlot + 1UL);
            for (int i = 0; i < frame.Length; i++) hash = Mix(hash, frame[i]);
        }
        return hash.ToString("x16");
    }

    // Same token from an already-built snapshot root (its slot frames); no re-encoding.
    internal static string GearToken(System.Xml.Linq.XElement root)
    {
        ulong hash = 14695981039346656037UL;
        foreach (var node in root.Elements("slot"))
        {
            string frame = node.Value;
            hash = Mix(hash, (ulong)(int)node.Attribute("id") + 1UL);
            for (int i = 0; i < frame.Length; i++) hash = Mix(hash, frame[i]);
        }
        return hash.ToString("x16");
    }

    private static ulong Mix(ulong hash, ulong value) => (hash ^ value) * 1099511628211UL;
}

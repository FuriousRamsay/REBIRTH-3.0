using System;
using UnityEngine;

#nullable disable

/// <summary>
/// Crafting-specific bridge over the authoritative player Bag. It deliberately never creates,
/// resizes, or owns storage. The custom Crafting grid is only a presenter for Bag.GetSlots().
/// </summary>
public static class RebirthCraftingInventoryBridge
{
    public const int Columns = 11;
    public const int VisibleRows = 4;
    public const int AuthoredRows = 16; // 176 cells also expose occupied legacy 169-slot bags.
    public const int AuthoredSlotCount = Columns * AuthoredRows;

    public static Bag GetBag(XUi xui)
    {
        return xui != null && xui.PlayerInventory != null ? xui.PlayerInventory.Backpack : null;
    }

    public static int GetPhysicalSlotCount(XUi xui)
    {
        Bag bag = GetBag(xui);
        if (bag == null || bag.ItemGrid.items == null)
            return 0;
        return Math.Min(bag.ItemGrid.items.Length, RebirthSurvivorGearService.MaxPhysicalBagSlots);
    }

    public static int GetUnencumberedSlotCount(XUi xui)
    {
        Bag bag = GetBag(xui);
        if (bag == null)
            return 0;

        int physical = GetPhysicalSlotCount(xui);
        EntityPlayer player = xui != null && xui.playerUI != null ? xui.playerUI.entityPlayer : null;
        if (player == null)
            return physical;

        // V3.2 b10 keeps physical Bag length and encumbrance separate. The effective native
        // CarryCapacity controls burden. Reserve markers remain visible even when a temporary buff
        // (including god mode) increases capacity; the header below still reports actual burden.
        int carryCapacity = Mathf.RoundToInt(EffectManager.GetValue(PassiveEffects.CarryCapacity, _entity: player));
        if (carryCapacity <= 0)
            carryCapacity = physical;
        return Math.Max(0, Math.Min(physical - (RebirthSurvivorGearService.BasePhysicalBagSlots - RebirthSurvivorGearService.BaseUnencumberedBagSlots), carryCapacity));
    }

    public static int GetUsedSlotCount(XUi xui)
    {
        Bag bag = GetBag(xui);
        return bag != null ? bag.GetUsedSlotCount() : 0;
    }

    public static int GetEncumberedUsedSlotCount(XUi xui)
    {
        // The player's encumbrance count is the number of occupied backpack slots above the
        // effective unencumbered CarryCapacity, not the number of authored physical cells that
        // happen to sit beyond a particular array index. Example: 33 used / 27 capacity = 6.
        int used = GetUsedSlotCount(xui);
        int physical = GetPhysicalSlotCount(xui);
        EntityPlayer player = xui?.playerUI?.entityPlayer;
        int unencumbered = player == null ? physical : Mathf.RoundToInt(EffectManager.GetValue(PassiveEffects.CarryCapacity, _entity: player));
        if (unencumbered <= 0) unencumbered = physical;
        return Math.Max(0, used - unencumbered);
    }

    public static PackedBoolArray GetLockedSlots(XUi xui, int physical)
    {
        Bag bag = GetBag(xui);
        if (bag == null)
            return null;

        // A presenter may cover fewer slots than the actual bag. Never truncate
        // persistent locks to a window's authored/visible controller count.
        int capacity = bag.ItemGrid.items?.Length ?? 0;
        PackedBoolArray result = bag.LockedSlots;
        bool changed=result==null||result.Length!=capacity;
        if(result==null)result=new PackedBoolArray(capacity);
        else if(result.Length!=capacity)result.Length=capacity;
        if(changed)bag.ItemGrid.SetSlotLocks(result);
        return result;
    }

    public static void PersistLockedSlots(XUi xui, XUiC_ItemStack[] controllers, int physical)
    {
        Bag bag = GetBag(xui);
        if (bag == null || controllers == null)
            return;

        physical = Math.Max(0, Math.Min(physical, Math.Min(controllers.Length, bag.ItemGrid.items?.Length ?? 0)));
        PackedBoolArray locked = GetLockedSlots(xui, physical);
        bool changed=false;
        for(int i=0;i<physical;i++)if(controllers[i]!=null&&locked[i]!=controllers[i].UserLockedSlot){locked[i]=controllers[i].UserLockedSlot;changed=true;}
        if(changed)bag.ItemGrid.SetSlotLocks(locked);
    }

    public static void ApplyLockedSlots(XUi xui, XUiC_ItemStack[] controllers, int physical)
    {
        if (controllers == null)
            return;

        PackedBoolArray locked = GetLockedSlots(xui, physical);
        int count = Math.Max(0, Math.Min(physical, controllers.Length));
        for (int i = 0; i < controllers.Length; i++)
        {
            XUiC_ItemStack slot = controllers[i];
            if (slot == null)
                continue;
            slot.UserLockedSlot = i < count && locked != null && i < locked.Length && locked[i];
        }
    }
}

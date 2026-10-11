using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

#nullable disable

/// <summary>
/// Lightweight REBIRTH-owned wearable/utility slots. They are deliberately separate from the
/// native four armor slots and do not inherit armor rating, armor durability, set bonuses or
/// native equipment semantics unless a specific item genuinely belongs to the native system.
/// </summary>
public static class RebirthSurvivorGearService
{
    public const int BasePhysicalBagSlots = 44;
    public const int MaxPhysicalBagSlots = 169; // Legacy save ceiling; new tiers reach 143 (44 + 8*11 + 11).
    public const int BaseUnencumberedBagSlots = 22;
    // PC102 TEMPORARY TEST OVERRIDE: requested to exercise the complete scrolling backpack.
    // Keep this isolated so the normal gear-derived 52..169 capacity model can be restored after UI validation.
    public static readonly bool ForceHundredSlotBackpackForPersonalCraftingTest = false;
    public const int PersonalCraftingTestPhysicalBagSlots = 100;

    // Keep the test toggle behind a normal method call. C# reachability analysis
    // cannot fold a method invocation into a compile-time constant, so the
    // normal gear-derived fallback paths remain reachable to the compiler while
    // the temporary 100-slot Personal Crafting test remains enabled at runtime.
    private static bool UseHundredSlotBackpackForPersonalCraftingTest()
    {
        return ForceHundredSlotBackpackForPersonalCraftingTest;
    }
    public const string BackpackSlotId = "backpack";
    public const string BeltSlotId = "belt";
    public const string SupportSlotId = "support";
    public const string WalkmanSlotId = "walkman";
    public const string WalkmanGearItemId = "rebirthGearWalkmanHeadphones";

    // Exact physical equipped payload; never manufacture an empty pack if stored data is corrupt.
    public static bool TryGetEquippedBackpackItem(EntityPlayer player,out ItemValue value)
    {
        value=null;
        if(player==null||player.world==null||player.world.IsRemote()||
            !RebirthWorldCharacterRepository.IsServerAuthority||!RebirthSurvivorMode.IsEnabledForCurrentWorld()||
            RebirthCharacterCreationHoldService.IsHeld(player))return false;
        RebirthWorldCharacterRecord record;
        if(!RebirthWorldCharacterService.TryGet(player,out record)||record==null||!record.IsComplete||record.Support==null)return false;
        string item,data;
        if(!record.Support.EquippedGearBySlot.TryGetValue(BackpackSlotId,out item))return false;
        record.Support.EquippedGearItemDataBySlot.TryGetValue(BackpackSlotId,out data);
        ItemStack stack;
        if(!TryBuildStoredGearStack(item,data,out stack)||stack==null||stack.IsEmpty())return false;
        value=stack.itemValue.Clone();return true;
    }
    public static bool HasEquippedWalkman(EntityPlayer player)
    {
        if (player == null) return false;
        if (player.world != null && player.world.IsRemote())
            return RebirthSurvivorClientState.HasProjectedGear(player, WalkmanSlotId, WalkmanGearItemId);
        RebirthWorldCharacterRecord record;
        return RebirthWorldCharacterService.TryGet(player, out record)
            && IsEquipped(record, WalkmanSlotId, WalkmanGearItemId);
    }

    public static bool TryEquipMatchingInventoryItem(EntityPlayer player, int itemType, ushort seed, out string message)
    {
        message = string.Empty;
        if (player == null || !RebirthWorldCharacterRepository.IsServerAuthority || !RebirthSurvivorMode.IsEnabledForCurrentWorld())
        { message = Localization.Get("xuiRebirthGearUnavailable"); return false; }
        if (RebirthCharacterCreationHoldService.IsHeld(player))
        { message = Localization.Get("xuiRebirthGearFinishCreation"); return false; }

        bool inBackpack; int slot; ItemStack stack;
        if (!TryFindMatchingInventoryStack(player, itemType, seed, out inBackpack, out slot, out stack))
        { message = Localization.Get("xuiRebirthGearItemMissing"); return false; }
        if (stack.itemValue == null || stack.itemValue.ItemClass == null)
        { message = Localization.Get("xuiRebirthGearItemInvalid"); return false; }

        string itemId = stack.itemValue.ItemClass.GetItemName();
        RebirthTraitSupportProfileDefinition profile;
        if (!RebirthSurvivorDefinitionRegistry.TryGetSupportByGearItem(itemId, out profile) || profile == null || !string.Equals(profile.Kind, "survivor_gear", StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(profile.GearSlotId))
        { message = Localization.Get("xuiRebirthGearItemUnsupported"); return false; }

        RebirthWorldCharacterRecord record;
        if (!RebirthWorldCharacterService.TryGet(player, out record) || record == null || !record.IsComplete || record.Support == null)
        { message = Localization.Get("xuiRebirthGearCharacterUnavailable"); return false; }

        if(string.Equals(profile.GearSlotId,SupportSlotId,StringComparison.Ordinal))
        {
            var metabolism=RebirthMetabolismStateRepository.GetOrCreate(player);
            if(metabolism?.HydrationSlotItem!=null&&!metabolism.HydrationSlotItem.IsEmpty())
            { message=Localization.Get("xuiRebirthSupportSlotOccupied");return false; }
        }
        if(RebirthBackpackLibraryReservation.BlocksResourceUse(player))
        {message=Localization.Get("xuiRebirthLibraryTransferPending");return false;}
        float strength = GetAttributeCurrent(record, "strength");
        float constitution = GetAttributeCurrent(record, "constitution");
        if (strength + 0.001f < profile.GearMinStrength || constitution + 0.001f < profile.GearMinConstitution)
        {
            message = string.Format(Localization.Get("xuiRebirthGearRequirements"), profile.GearMinStrength.ToString("0"), profile.GearMinConstitution.ToString("0"));
            return false;
        }

        string existing; string existingData;
        record.Support.EquippedGearItemDataBySlot.TryGetValue(profile.GearSlotId,out existingData);
        if (record.Support.EquippedGearBySlot.TryGetValue(profile.GearSlotId, out existing) && string.Equals(existing, itemId, StringComparison.OrdinalIgnoreCase))
        { message = Localization.Get("xuiRebirthGearAlreadyEquipped"); return false; }

        // Native SetSlot mutates its live stack: keep the original transaction image.
        stack=stack.Clone();
        string incomingData=EncodeItemValue(stack.itemValue);
        if(string.IsNullOrEmpty(incomingData))
        { message=Localization.Get("xuiRebirthGearEquipFailed"); return false; }
        ItemStack displaced=null;
        if(!string.IsNullOrEmpty(existing) && !TryBuildStoredGearStack(existing,existingData,out displaced))
        { message=Localization.Get("xuiRebirthGearReturnFailed"); return false; }

        List<ItemStack> sectionOverflow=null;
        string clearedExistingData=existingData;
        ItemStack incomingRecovery=stack.Clone();
        if(profile.GearSlotId==BackpackSlotId&&displaced!=null)
        {
            if(!RebirthBackpackSectionTransfer.TryPrepare(displaced.itemValue,stack.itemValue,out var emptied,out var filled,out sectionOverflow))
            {message=Localization.Get("xuiRebirthGearReturnFailed");return false;}
            clearedExistingData=EncodeItemValue(emptied);incomingData=EncodeItemValue(filled);
            if(string.IsNullOrEmpty(clearedExistingData)||string.IsNullOrEmpty(incomingData))
            {message=Localization.Get("xuiRebirthGearReturnFailed");return false;}
            displaced=new ItemStack(emptied,displaced.count);incomingRecovery.itemValue=filled;
        }
        int targetSlots = GetDesiredPhysicalBagSlots(record, profile.GearSlotId, itemId);

        // Remove the incoming item from the authoritative inventory. If a backpack is being
        // downgraded, the source slot itself is allowed to be above the new boundary because this
        // exact stack is consumed by the equip transaction.
        ItemStack next = stack.Clone();
        next.count--;
        if (next.count <= 0) next = ItemStack.Empty.Clone();
        if (inBackpack) player.bag.SetSlot(slot, next); else player.inventory.SetItem(slot, next);
        if (!RebirthGearOverflow.TryRelease(player, profile.GearSlotId, targetSlots,
            Math.Min(RebirthToolbeltCapacity.MaximumSlots, RebirthToolbeltCapacity.GetSlotsForLevel(player.Progression.Level) + RebirthBackgroundStorageService.ToolbeltBonus(player) + profile.GearToolbeltSlotBonus), sectionOverflow))
        {
            if (inBackpack) player.bag.SetSlot(slot, stack); else player.inventory.SetItem(slot, stack);
            message = Localization.Get("rebirthGearRecoveryFailed"); return false;
        }


        // Recovery publication succeeded; rollback must not resurrect recovered section contents.
        existingData=clearedExistingData;
        // Update ownership before reconciliation so the desired capacity is derived from the new
        // gear state. Reconcile first: an upgrade may create the only legal cell available for the
        // displaced pack, while a downgrade has already released its occupied tail into recovery drops.
        record.Support.EquippedGearBySlot[profile.GearSlotId] = itemId;
        record.Support.EquippedGearItemDataBySlot[profile.GearSlotId] = incomingData;
        if(!ReconcilePhysicalBagCapacity(player, targetSlots, true))
        {
            if(string.IsNullOrEmpty(existing)){record.Support.EquippedGearBySlot.Remove(profile.GearSlotId);record.Support.EquippedGearItemDataBySlot.Remove(profile.GearSlotId);}
            else {record.Support.EquippedGearBySlot[profile.GearSlotId]=existing;if(string.IsNullOrEmpty(existingData))record.Support.EquippedGearItemDataBySlot.Remove(profile.GearSlotId);else record.Support.EquippedGearItemDataBySlot[profile.GearSlotId]=existingData;}
            if(inBackpack)player.bag.SetSlot(slot,incomingRecovery);else player.inventory.SetItem(slot,incomingRecovery);
            message=Localization.Get("xuiRebirthGearInventoryRestored");return false;
        }
        if (!string.IsNullOrEmpty(existing))
            AddToBackpackOrDrop(player, displaced, targetSlots);
        RebirthWorldCharacterService.MarkDirty(record, "survivor-gear-equip:" + profile.GearSlotId);
        RebirthSurvivorNetworkService.SendOwnerState(player, Math.Max(0L, record.Revision - 1L), true, "survivor-gear-equip");
        message = string.Format(Localization.Get("rebirthGearEquippedNotice"), Localization.Get(itemId));
        return true;
    }

    public static bool TryUnequip(EntityPlayer player, string slotId, out string message, Action<ItemStack> receiveItem = null)
    {
        message = string.Empty;
        if (player == null || string.IsNullOrEmpty(slotId) || !RebirthWorldCharacterRepository.IsServerAuthority || !RebirthSurvivorMode.IsEnabledForCurrentWorld())
        { message = Localization.Get("xuiRebirthGearUnavailable"); return false; }
        if (RebirthCharacterCreationHoldService.IsHeld(player))
        { message = Localization.Get("xuiRebirthGearFinishCreation"); return false; }
        RebirthWorldCharacterRecord record;
        if (!RebirthWorldCharacterService.TryGet(player, out record) || record == null || record.Support == null)
        { message = Localization.Get("xuiRebirthGearCharacterUnavailable"); return false; }
        if(RebirthBackpackLibraryReservation.BlocksResourceUse(player))
        {message=Localization.Get("xuiRebirthLibraryTransferPending");return false;}
        string itemId; string itemData;
        record.Support.EquippedGearItemDataBySlot.TryGetValue(slotId,out itemData);
        if (!record.Support.EquippedGearBySlot.TryGetValue(slotId, out itemId) || string.IsNullOrEmpty(itemId))
        { message = Localization.Get("xuiRebirthGearSlotEmpty"); return false; }
        ItemStack removedGear;
        if(!TryBuildStoredGearStack(itemId,itemData,out removedGear))
        { message=Localization.Get("xuiRebirthGearReturnFailed"); return false; }

        List<ItemStack> sectionOverflow=null;
        if(slotId==BackpackSlotId)
        {
            if(!RebirthBackpackSectionTransfer.TryPrepare(removedGear.itemValue,null,out var emptied,out _,out sectionOverflow))
            {message=Localization.Get("xuiRebirthGearReturnFailed");return false;}
            removedGear=new ItemStack(emptied,removedGear.count);
            itemData=EncodeItemValue(emptied);
            if(string.IsNullOrEmpty(itemData)){message=Localization.Get("xuiRebirthGearReturnFailed");return false;}
        }
        int targetSlots = GetDesiredPhysicalBagSlots(record, slotId, null);
        if (!RebirthGearOverflow.TryRelease(player, slotId, targetSlots, RebirthToolbeltCapacity.GetSlotsForLevel(player.Progression.Level) + RebirthBackgroundStorageService.ToolbeltBonus(player), sectionOverflow))
        { message = Localization.Get("rebirthGearRecoveryFailed"); return false; }

        record.Support.EquippedGearBySlot.Remove(slotId);
        record.Support.EquippedGearItemDataBySlot.Remove(slotId);
        if(!ReconcilePhysicalBagCapacity(player, targetSlots, true))
        {
            record.Support.EquippedGearBySlot[slotId]=itemId;if(!string.IsNullOrEmpty(itemData))record.Support.EquippedGearItemDataBySlot[slotId]=itemData;
            message=Localization.Get("xuiRebirthGearItemRetained");return false;
        }
        if (receiveItem != null) receiveItem(removedGear);
        else AddToBackpackOrDrop(player, removedGear, targetSlots);
        RebirthWorldCharacterService.MarkDirty(record, "survivor-gear-unequip:" + slotId);
        RebirthSurvivorNetworkService.SendOwnerState(player, Math.Max(0L, record.Revision - 1L), true, "survivor-gear-unequip");
        message = string.Format(Localization.Get("rebirthGearUnequippedNotice"), Localization.Get(itemId));
        return true;
    }

    public static int GetToolbeltBonus(EntityPlayer player)
    {
        if (player == null || !RebirthSurvivorMode.IsEnabledForCurrentWorld()) return 0;
        string item = null;
        if (player.world != null && player.world.IsRemote())
            return RebirthSurvivorClientState.GetProjectedToolbeltBonus(player);
        RebirthWorldCharacterRecord record;
        if (RebirthWorldCharacterService.TryGet(player, out record) && record != null && record.IsComplete && record.Support != null)
            record.Support.EquippedGearBySlot.TryGetValue(BeltSlotId, out item);
        return ToolbeltBonusForItem(item);
    }
    public static int ToolbeltBonusForItem(string item)
    {
        RebirthTraitSupportProfileDefinition profile;
        return !string.IsNullOrEmpty(item) && RebirthSurvivorDefinitionRegistry.TryGetSupportByGearItem(item, out profile)
            && profile != null && profile.GearSlotId == BeltSlotId ? profile.GearToolbeltSlotBonus : 0;
    }

    public static bool IsEquipped(RebirthWorldCharacterRecord record, string slotId, string itemId)
    {
        if (record == null || record.Support == null || string.IsNullOrEmpty(slotId) || string.IsNullOrEmpty(itemId)) return false;
        string equipped;
        return record.Support.EquippedGearBySlot.TryGetValue(slotId, out equipped) && string.Equals(equipped, itemId, StringComparison.OrdinalIgnoreCase);
    }

    public static bool TryGetDesiredPhysicalBagSlots(EntityPlayer player, out int desiredSlots)
    {
        desiredSlots = BasePhysicalBagSlots;
        if (player == null || !RebirthSurvivorMode.IsEnabledForCurrentWorld()) return false;
        if (UseHundredSlotBackpackForPersonalCraftingTest())
        {
            desiredSlots = PersonalCraftingTestPhysicalBagSlots;
            return true;
        }
        if (RebirthWorldCharacterRepository.IsServerAuthority)
        {
            RebirthWorldCharacterRecord record;
            if (RebirthWorldCharacterService.TryGet(player, out record) && record != null && record.IsComplete)
            {
                desiredSlots = GetDesiredPhysicalBagSlots(record, null, null);
                return true;
            }
            // Login/load ordering can touch Bag before the world-character repository has the
            // authoritative Survivor record. Capacity is unknown in that window; never guess a capacity.
            return false;
        }
        return RebirthSurvivorClientState.TryGetProjectedPhysicalBagSlots(player, out desiredSlots);
    }

    public static int GetDesiredPhysicalBagSlots(EntityPlayer player)
    {
        int desired;
        return TryGetDesiredPhysicalBagSlots(player, out desired) ? desired : BasePhysicalBagSlots;
    }

    public static int GetDesiredPhysicalBagSlots(RebirthWorldCharacterRecord record)
    {
        return GetDesiredPhysicalBagSlots(record, null, null);
    }

    internal static int GetDesiredPhysicalBagSlots(RebirthWorldCharacterRecord record, string overrideSlotId, string overrideItemId)
    {
        if (UseHundredSlotBackpackForPersonalCraftingTest())
            return PersonalCraftingTestPhysicalBagSlots;
        if (record == null || record.Support == null) return BasePhysicalBagSlots;
        string itemId = null;
        if (string.Equals(overrideSlotId, BackpackSlotId, StringComparison.OrdinalIgnoreCase))
            itemId = overrideItemId;
        else
            record.Support.EquippedGearBySlot.TryGetValue(BackpackSlotId, out itemId);

        int bonus = 0;
        if (!string.IsNullOrEmpty(itemId))
        {
            RebirthTraitSupportProfileDefinition profile;
            if (RebirthSurvivorDefinitionRegistry.TryGetSupportByGearItem(itemId, out profile) && profile != null)
            {
                        float strength = GetAttributeCurrent(record, "strength");
                float constitution = GetAttributeCurrent(record, "constitution");
                if (strength + 0.001f >= profile.GearMinStrength && constitution + 0.001f >= profile.GearMinConstitution)
                    bonus = profile.GearBagSlotBonus;
            }
        }
        return Mathf.Clamp(BasePhysicalBagSlots + bonus + RebirthBackgroundStorageService.BackpackBonus(record), BasePhysicalBagSlots, MaxPhysicalBagSlots);
    }

    public static bool ReconcilePhysicalBagCapacity(EntityPlayer player, int desiredSlots, bool notify)
    {
        if (player == null || player.bag == null) return false;
        desiredSlots = Mathf.Clamp(desiredSlots, BasePhysicalBagSlots, MaxPhysicalBagSlots);
        ItemStack[] slots = player.bag.ItemGrid.items;
        if (slots == null) slots = ItemStack.CreateArray(desiredSlots);
        if (slots.Length == desiredSlots) return true;
        // The original transaction owns backing geometry while custody is held.
        // Native SetSlots may be refused by its guard; do not resize lock metadata
        // or report a successful projection resize after that refusal.
        var localOwner = player as EntityPlayerLocal;
        if (localOwner != null && (RebirthGearOwnerReservation.IsHeld(localOwner)
            || RebirthBackpackLibraryReservation.IsHeld(localOwner))) return false;
        if (slots.Length > desiredSlots)
        {
            for (int i = desiredSlots; i < slots.Length; i++)
                if (slots[i] != null && !slots[i].IsEmpty()) return false;
        }
        ItemStack[] resized = ItemStack.CreateArray(desiredSlots);
        int copy = Math.Min(slots.Length, resized.Length);
        for (int i = 0; i < copy; i++) resized[i] = slots[i] != null ? slots[i].Clone() : ItemStack.Empty.Clone();
        player.bag.SetSlots(resized);
        if (player.bag.ItemGrid.items == null || player.bag.ItemGrid.items.Length != desiredSlots) return false;
        PackedBoolArray locks = player.bag.LockedSlots;
        if (locks != null && locks.Length != desiredSlots) locks.Length = desiredSlots;
        if (notify) { if (RebirthLogSettings.AutomaticLoggingDefault) Log.Out("[REBIRTH Survivor] physical backpack capacity entity=" + player.entityId + " slots=" + desiredSlots); }
        return true;
    }

    public static string BuildDebugSummary(RebirthWorldCharacterRecord record)
    {
        if (record == null || record.Support == null) return "gear=<unavailable>";
        List<string> values = new List<string>();
        foreach (KeyValuePair<string,string> pair in record.Support.EquippedGearBySlot) values.Add(pair.Key + "=" + (pair.Value ?? string.Empty));
        values.Sort(StringComparer.OrdinalIgnoreCase);
        return (values.Count == 0 ? "gear=<none>" : "gear=" + string.Join(";", values.ToArray())) + " physicalBagSlots=" + GetDesiredPhysicalBagSlots(record);
    }

    private static float GetAttributeCurrent(RebirthWorldCharacterRecord record, string id)
    {
        if (record == null || record.Progression == null || string.IsNullOrEmpty(id)) return 0f;
        RebirthAttributeRuntimeState state;
        return record.Progression.Attributes.TryGetValue(id, out state) && state != null ? state.Current : 0f;
    }

    private static bool TryFindMatchingInventoryStack(EntityPlayer player, int itemType, ushort seed, out bool inBackpack, out int slot, out ItemStack stack)
    {
        inBackpack = false; slot = -1; stack = null;
        if (player == null || player.inventory == null || player.bag == null) return false;
        ItemStack[] tool = player.inventory.ItemGrid.items;
        int toolLimit = RebirthToolbeltCapacity.GetOwnedSlotCount(player, tool.Length);
        for (int i = 0; i < toolLimit; i++)
        {
            ItemStack value = tool[i];
            if (value == null || value.IsEmpty() || value.itemValue == null || value.itemValue.type != itemType || value.itemValue.Seed != seed) continue;
            slot = i; stack = value; return true;
        }
        ItemStack[] bag = player.bag.ItemGrid.items;
        for (int i = 0; i < bag.Length; i++)
        {
            ItemStack value = bag[i];
            if (value == null || value.IsEmpty() || value.itemValue == null || value.itemValue.type != itemType || value.itemValue.Seed != seed) continue;
            inBackpack = true; slot = i; stack = value; return true;
        }
        return false;
    }

    private static string EncodeItemValue(ItemValue value)
    {
        if(value==null||value.IsEmpty())return string.Empty;
        try{using(MemoryStream ms=new MemoryStream())using(PooledBinaryWriter bw=MemoryPools.poolBinaryWriter.AllocSync(true)){bw.SetBaseStream(ms);ItemValue.Write(value,bw);bw.Flush();return Convert.ToBase64String(ms.ToArray());}}catch{return string.Empty;}
    }

    private static bool TryBuildStoredGearStack(string itemId,string encoded,out ItemStack stack)
    {
        stack=null;
        if(!string.IsNullOrEmpty(encoded))
        {
            try
            {
                byte[] bytes=Convert.FromBase64String(encoded);
                using(MemoryStream ms=new MemoryStream(bytes))using(PooledBinaryReader br=MemoryPools.poolBinaryReader.AllocSync(true))
                {
                    br.SetBaseStream(ms);
                    ItemValue value=ItemValue.ReadOrNull(br);
                    if(value==null || value.IsEmpty() || value.ItemClass==null || ms.Position!=ms.Length
                        || !string.Equals(value.ItemClass.GetItemName(),itemId,StringComparison.Ordinal))return false;
                    stack=new ItemStack(value,1);return true;
                }
            }
            catch{return false;}
        }
        // Legacy saves recorded only an item ID. Preserve their migration path;
        // corrupt exact data must never be silently replaced with a default item.
        ItemValue legacy=ItemClass.GetItem(itemId,false);
        if(legacy==null || legacy.IsEmpty())return false;
        stack=new ItemStack(legacy,1);return true;
    }

    private static void AddToBackpackOrDrop(EntityPlayer player, ItemStack stack, int maxUsableSlots)
    {
        if (player == null || stack == null || stack.IsEmpty()) return;
        ItemStack[] bag = player.bag.ItemGrid.items;
        int limit = Math.Min(Math.Max(BasePhysicalBagSlots, maxUsableSlots), bag != null ? bag.Length : 0);
        for (int i = 0; i < limit; i++)
        {
            if (bag[i] != null && !bag[i].IsEmpty()) continue;
            player.bag.SetSlot(i, stack);
            return;
        }
        if (player.world != null && player.world.gameManager != null)
            player.world.gameManager.ItemDropServer(stack, player.GetPosition(), Vector3.zero);
    }
}



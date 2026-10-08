using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

public static class RebirthBackpackLibraryCursorGuard
{
    private static readonly Harmony Harmony=new Harmony("rebirth.library.cursor-reservation.3.0");
    private static bool installed;
    public static string Install()
    {
        if(installed)return "library cursor reservation already installed";
        RebirthHarmonyBootstrap.PatchClassOnce(Harmony,typeof(RebirthBackpackLibraryCursorMutationPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(Harmony,typeof(RebirthBackpackLibrarySortPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(Harmony,typeof(RebirthBackpackLibraryCursorReturnPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(Harmony,typeof(RebirthBackpackLibraryTransactionalCursorPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(Harmony,typeof(RebirthBackpackLibraryItemActionPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(Harmony,typeof(RebirthBackpackLibraryHeldUsePatch));
        RebirthHarmonyBootstrap.PatchClassOnce(Harmony,typeof(RebirthBackpackLibraryBagPartialStackPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(Harmony,typeof(RebirthBackpackLibraryBagAddPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(Harmony,typeof(RebirthBackpackLibraryBeltPlacementPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(Harmony,typeof(RebirthBackpackLibraryBeltAddPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(Harmony,typeof(RebirthBackpackLibraryBeltPartialStackPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(Harmony,typeof(RebirthBackpackLibraryMaterialAvailabilityPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(Harmony,typeof(RebirthGearBagSlotGuard));
        RebirthHarmonyBootstrap.PatchClassOnce(Harmony,typeof(RebirthGearBagBackingGuard));
        RebirthHarmonyBootstrap.PatchClassOnce(Harmony,typeof(RebirthGearBeltSlotGuard));
        RebirthHarmonyBootstrap.PatchClassOnce(Harmony,typeof(RebirthGearBeltBackingGuard));
        RebirthHarmonyBootstrap.PatchClassOnce(Harmony,typeof(RebirthGearSandboxAdjustmentPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(Harmony,typeof(RebirthGearColdSpawnAdmissionPatch));
        ModEvents.GameStarting.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SGameStartingData>(OnGameStarting));
        ModEvents.WorldShuttingDown.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SWorldShuttingDownData>(OnWorldShuttingDown));
        ModEvents.GameShutdown.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SGameShutdownData>(OnGameShutdown));
        installed=true;return "library cursor reservation installed";
    }
    private static void OnGameStarting(ref ModEvents.SGameStartingData data){RebirthBackpackLibraryReservation.ResetSession();RebirthGearOwnerReservation.ResetSession();RebirthGearSandboxAdjustmentDeferral.Reset();}
    private static void OnWorldShuttingDown(ref ModEvents.SWorldShuttingDownData data){RebirthBackpackLibraryReservation.ResetSession();RebirthGearOwnerReservation.ResetSession();RebirthGearSandboxAdjustmentDeferral.Reset();}
    private static void OnGameShutdown(ref ModEvents.SGameShutdownData data){RebirthBackpackLibraryReservation.ResetSession();RebirthGearOwnerReservation.ResetSession();RebirthGearSandboxAdjustmentDeferral.Reset();}
}
[HarmonyPatch]
public static class RebirthBackpackLibraryCursorMutationPatch
{
    public static IEnumerable<MethodBase> TargetMethods()
    {
        foreach(string name in new[]{"HandleStackSwap","HandlePartialStackPickup","HandleDropOne","SwapItem","HandleMoveToPreferredLocation"})
        {
            var target=AccessTools.DeclaredMethod(typeof(XUiC_ItemStack),name,Type.EmptyTypes);
            if(target==null)throw new MissingMethodException(typeof(XUiC_ItemStack).FullName,name);
            yield return target;
        }
    }
    [HarmonyPrefix]
    public static bool Prefix(XUiC_ItemStack __instance,MethodBase __originalMethod)
    {
        var player=__instance?.xui?.playerUI?.entityPlayer;
        if(player!=null&&(RebirthBackpackLibraryReservation.IsHeld(player)||RebirthGearOwnerReservation.IsHeld(player)))return false;
        if(__instance is XUiC_RebirthBackpackSectionNativeSlot section&&section.Dispatch(__originalMethod.Name))return false;
        return true;
    }
}

[HarmonyPatch(typeof(XUiM_PlayerInventory),nameof(XUiM_PlayerInventory.SortStacks))]
public static class RebirthBackpackLibrarySortPatch
{
    [HarmonyPrefix]
    public static bool Prefix(XUiM_PlayerInventory __instance)
        =>__instance?.localPlayer==null||!(RebirthBackpackLibraryReservation.IsHeld(__instance.localPlayer)||RebirthGearOwnerReservation.IsHeld(__instance.localPlayer));
}
[HarmonyPatch]
public static class RebirthBackpackLibraryTransactionalCursorPatch
{
    public static IEnumerable<MethodBase> TargetMethods()
    {
        foreach(string name in new[]{"OnCellSelected","MoveToPreferred"})
        {
            var target=AccessTools.DeclaredMethod(typeof(XUiC_ItemStackSlotGrid),name,new[]{typeof(int)});
            if(target==null)throw new MissingMethodException(typeof(XUiC_ItemStackSlotGrid).FullName,name);
            yield return target;
        }
    }
    [HarmonyPrefix]
    public static bool Prefix(XUiC_ItemStackSlotGrid __instance)
    {
        var player=__instance?.xui?.playerUI?.entityPlayer;
        if(player!=null&&(RebirthBackpackLibraryReservation.IsHeld(player)||RebirthGearOwnerReservation.IsHeld(player)))return false;
        return true;
    }
}

[HarmonyPatch]
public static class RebirthBackpackLibraryItemActionPatch
{
    public static IEnumerable<MethodBase> TargetMethods()
    {
        foreach(var type in new[]{typeof(ItemActionEntryUse),typeof(ItemActionEntryDrop),typeof(ItemActionEntryScrap),
            typeof(ItemActionEntryEquip),typeof(ItemActionEntryWear),typeof(ItemActionEntryRepair),
            typeof(ItemActionEntrySell),typeof(ItemActionEntryPurchase),typeof(ItemActionEntryTake),
            typeof(ItemActionEntryCraft),typeof(ItemActionEntryCombine),typeof(ItemActionEntryAssemble),typeof(RebirthLiteratureUseEntry)})
        {
            var target=AccessTools.DeclaredMethod(type,"OnActivated",Type.EmptyTypes);
            if(target==null)throw new MissingMethodException(type.FullName,"OnActivated");
            yield return target;
        }
    }
    [HarmonyPrefix,HarmonyPriority(Priority.First)]
    public static bool Prefix(BaseItemActionEntry __instance)
    {
        var player=__instance?.ItemController?.xui?.playerUI?.entityPlayer;
        if(player==null||!(RebirthBackpackLibraryReservation.IsHeld(player)||RebirthGearOwnerReservation.IsHeld(player)))return true;
        GameManager.ShowTooltip(player,Localization.Get(RebirthGearOwnerReservation.IsHeld(player)?"xuiRebirthGearTransferPending":"xuiRebirthLibraryTransferPending"));
        return false;
    }
}

[HarmonyPatch(typeof(EntityAlive),nameof(EntityAlive.UseHoldingItem),new Type[]{typeof(int),typeof(bool)})]
public static class RebirthBackpackLibraryHeldUsePatch
{
    [HarmonyPrefix,HarmonyPriority(Priority.First)]
    public static bool Prefix(EntityAlive __instance,ref bool __result)
    {
        if(!RebirthBackpackLibraryReservation.BlocksHeldUse(__instance as EntityPlayerLocal)&&!RebirthGearOwnerReservation.IsHeld(__instance as EntityPlayerLocal))return true;
        __result=false;return false;
    }
}

[HarmonyPatch(typeof(Bag),nameof(Bag.TryStackItem))]
public static class RebirthBackpackLibraryBagPartialStackPatch
{
    [HarmonyPrefix,HarmonyPriority(Priority.First)]
    public static bool Prefix(Bag __instance,int startIndex,ItemStack _itemStack,ref (bool anyMoved,bool allMoved) __result)
    {
        if(RebirthGearOwnerReservation.BlocksInventory(__instance,true)){__result=(false,false);return false;}
        if(!RebirthBackpackLibraryReservation.TryGetReservedSlot(__instance,true,out int reserved))return true;
        if(!_itemStack.CanMoveTo(XUiC_ItemStack.StackLocationTypes.Backpack)){__result=(false,false);return false;}
        var slots=__instance.ItemGrid.items;bool any=false;
        // Preserve native partial merge/remainder/event semantics; skip only the reserved slot.
        for(int index=startIndex;index<slots.Length;index++)
        {
            if(index==reserved)continue;
            int count=_itemStack.count;
            if(_itemStack.itemValue.type==slots[index].itemValue.type&&slots[index].CanStackPartly(ref count))
            {
                slots[index].count+=count;_itemStack.count-=count;__instance.onBackpackChanged();any=true;
                if(_itemStack.count==0){__result=(true,true);return false;}
            }
        }
        __result=(any,false);return false;
    }
}

[HarmonyPatch(typeof(Bag),nameof(Bag.AddItem))]
public static class RebirthBackpackLibraryBagAddPatch
{
    [HarmonyPrefix,HarmonyPriority(Priority.First)]
    public static bool Prefix(Bag __instance,ItemStack _itemStack,ref bool __result)
    {
        if(RebirthGearOwnerReservation.BlocksInventory(__instance,true)){__result=false;return false;}
        if(!RebirthBackpackLibraryReservation.TryGetReservedSlot(__instance,true,out int reserved))return true;
        __result=false;
        if(!_itemStack.CanMoveTo(XUiC_ItemStack.StackLocationTypes.Backpack))return false;
        var slots=__instance.ItemGrid.items;
        // Native AddToItemStackArray merges the entire input first, then places into an empty slot.
        for(int index=0;index<slots.Length;index++)
        {
            if(index==reserved||!slots[index].CanStackWith(_itemStack))continue;
            slots[index].count+=_itemStack.count;_itemStack.count=0;
            __instance.onBackpackChanged();__result=true;return false;
        }
        for(int index=0;index<slots.Length;index++)
        {
            if(index==reserved||!slots[index].IsEmpty())continue;
            __instance.SetSlot(index,_itemStack);__instance.onBackpackChanged();__result=true;return false;
        }
        return false;
    }
}

[HarmonyPatch(typeof(Inventory),nameof(Inventory.AddItemAtSlot),new Type[]{typeof(ItemStack),typeof(int)})]
public static class RebirthBackpackLibraryBeltPlacementPatch
{
    [HarmonyPrefix,HarmonyPriority(Priority.First)]
    public static bool Prefix(Inventory __instance,int _slot,ref bool __result)
    {
        if(RebirthGearOwnerReservation.BlocksInventory(__instance,false)){__result=false;return false;}
        if(!RebirthBackpackLibraryReservation.TryGetReservedSlot(__instance,false,out int reserved)||_slot!=reserved)return true;
        __result=false;return false;
    }
}

[HarmonyPatch]
public static class RebirthBackpackLibraryBeltAddPatch
{
    public static MethodBase TargetMethod()
        =>AccessTools.DeclaredMethod(typeof(Inventory),nameof(Inventory.AddItem),new[]{typeof(ItemStack),typeof(int),typeof(int),typeof(int).MakeByRefType()})
        ??throw new MissingMethodException(typeof(Inventory).FullName,"AddItem(ItemStack,int,int,out int)");
    [HarmonyPrefix,HarmonyPriority(Priority.First)]
    public static bool Prefix(Inventory __instance,ItemStack _itemStack,int _startSlot,int _slotCount,ref int _slot,ref bool __result)
    {
        if(RebirthGearOwnerReservation.BlocksInventory(__instance,false)){_slot=-1;__result=false;return false;}
        if(!RebirthBackpackLibraryReservation.TryGetReservedSlot(__instance,false,out int reserved))return true;
        _slot=-1;__result=false;
        if(!_itemStack.CanMoveTo(XUiC_ItemStack.StackLocationTypes.ToolBelt))return false;
        var player=__instance.entity as EntityPlayer;
        if(player==null)return false;
        int limit=Math.Min(__instance.Length,RebirthToolbeltCapacity.GetOwnedSlotCount(player,__instance.Length));
        int start=Math.Max(0,Math.Min(_startSlot,limit));
        int end=start+Math.Max(0,Math.Min(_slotCount,limit-start));
        for(int index=start;index<end;index++)
        {
            if(index==reserved||!__instance.CanMoveToSlot(_itemStack,index)||__instance.GetStackAt(index).itemValue.type!=_itemStack.itemValue.type||
                !__instance.GetStackAt(index).CanStackWith(_itemStack))continue;
            __instance.GetStackAt(index).count+=_itemStack.count;
            __instance.CallOnToolbeltChangedInternal();__instance.entity.bPlayerStatsChanged=!__instance.entity.isEntityRemote;
            _slot=index;__result=true;return false;
        }
        for(int index=start;index<end;index++)
        {
            if(index==reserved||!__instance.CanMoveToSlot(_itemStack,index)||!__instance.GetStackAt(index).IsEmpty())continue;
            __instance.SetItem(index,_itemStack.itemValue,_itemStack.count);
            __instance.CallOnToolbeltChangedInternal();__instance.entity.bPlayerStatsChanged=!__instance.entity.isEntityRemote;
            _slot=index;__result=true;return false;
        }
        return false;
    }
}

[HarmonyPatch(typeof(Inventory),nameof(Inventory.TryStackItem))]
public static class RebirthBackpackLibraryBeltPartialStackPatch
{
    [HarmonyPrefix,HarmonyPriority(Priority.First)]
    public static bool Prefix(Inventory __instance,int startIndex,ItemStack _itemStack,ref (bool anyMoved,bool allMoved) __result)
    {
        if(RebirthGearOwnerReservation.BlocksInventory(__instance,false)){__result=(false,false);return false;}
        if(!RebirthBackpackLibraryReservation.TryGetReservedSlot(__instance,false,out int reserved))return true;
        __result=(false,false);var player=__instance.entity as EntityPlayer;if(player==null)return false;
        int limit=Math.Min(RebirthToolbeltCapacity.GetSlotsForPlayer(player),RebirthToolbeltCapacity.GetOwnedSlotCount(player,__instance.Length));
        bool any=false;
        for(int index=startIndex;index<limit;index++)
        {
            if(index==reserved||!__instance.CanMoveToSlot(_itemStack,index))continue;
            int count=_itemStack.count;var stack=__instance.GetStackAt(index);
            if(_itemStack.itemValue.type!=stack.itemValue.type||stack.IsEmpty()||!stack.CanStackPartly(ref count))continue;
            any=true;stack.count+=count;_itemStack.count-=count;
            __instance.CallOnToolbeltChangedInternal();__instance.entity.bPlayerStatsChanged=!__instance.entity.isEntityRemote;
            if(_itemStack.count==0){__result=(true,true);return false;}
        }
        __result=(any,false);return false;
    }
}

[HarmonyPatch(typeof(XUiM_PlayerInventory),nameof(XUiM_PlayerInventory.HasItems),new Type[]{typeof(IList<ItemStack>),typeof(int)})]
public static class RebirthBackpackLibraryMaterialAvailabilityPatch
{
    [HarmonyPrefix,HarmonyPriority(Priority.First)]
    public static bool Prefix(XUiM_PlayerInventory __instance,ref bool __result)
    {
        if(__instance?.localPlayer==null||!(RebirthBackpackLibraryReservation.IsHeld(__instance.localPlayer)||RebirthGearOwnerReservation.IsHeld(__instance.localPlayer)))return true;
        __result=false;return false;
    }
}

[HarmonyPatch(typeof(XUiC_DragAndDropWindow),nameof(XUiC_DragAndDropWindow.PlaceItemBackInInventory))]
public static class RebirthBackpackLibraryCursorReturnPatch
{
    [HarmonyPrefix]
    public static bool Prefix(XUiC_DragAndDropWindow __instance)
        =>!RebirthBackpackLibraryReservation.IsHeld(__instance?.xui?.playerUI?.entityPlayer);
}
using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
internal static class RebirthPlacedOriginalGuards
{
 internal static void Install(Harmony harmony){foreach(var type in new[]{typeof(RebirthPlacedOriginalGridSlot),typeof(RebirthPlacedOriginalGridBulk),typeof(RebirthPlacedOriginalGridAdd),typeof(RebirthPlacedOriginalGridCount),typeof(RebirthPlacedOriginalDecItem),typeof(RebirthPlacedOriginalBagAdd),typeof(RebirthPlacedOriginalBagPartial),typeof(RebirthPlacedOriginalHeldUse),typeof(RebirthPlacedOriginalActions),typeof(RebirthPlacedOriginalSlotGesture),typeof(RebirthPlacedOriginalCursorSet),typeof(RebirthPlacedOriginalCursorGesture)})RebirthHarmonyBootstrap.PatchClassOnce(harmony,type);}
}
[HarmonyPatch(typeof(ItemStackGrid),nameof(ItemStackGrid.SetItem))]
internal static class RebirthPlacedOriginalGridSlot
{
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(ItemStackGrid __instance,int _index,ItemStack _stack)
        =>!RebirthPlacedOriginal.Blocks(__instance)||RebirthPlacedOriginal.Consume(__instance,_index,_stack);
    private static void Postfix(ItemStackGrid __instance)=>RebirthPlacedOriginal.ObserveDebit(__instance);
}
[HarmonyPatch]
internal static class RebirthPlacedOriginalGridBulk
{
    private static IEnumerable<MethodBase> TargetMethods()
    {foreach(string name in new[]{"SetItems","ClearItems","Compact","Sort","CompactSlots","CopyItemsFrom","CopyFrom","Reset","Clear","Resize","ReadInto","SetSlotLocks","SetLocked"})yield return AccessTools.Method(typeof(ItemStackGrid),name)??throw new MissingMethodException(typeof(ItemStackGrid).Name,name);}
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(ItemStackGrid __instance)=>!RebirthPlacedOriginal.Blocks(__instance);
}
[HarmonyPatch(typeof(ItemStackGrid),nameof(ItemStackGrid.AddItem))]
internal static class RebirthPlacedOriginalGridAdd
{
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(ItemStackGrid __instance,ref int __result){if(!RebirthPlacedOriginal.Blocks(__instance))return true;__result=-1;return false;}
}
[HarmonyPatch(typeof(ItemStackGrid),nameof(ItemStackGrid.ChangeCount))]
internal static class RebirthPlacedOriginalGridCount
{
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(ItemStackGrid __instance,ref int __result){if(!RebirthPlacedOriginal.Blocks(__instance))return true;__result=0;return false;}
}
[HarmonyPatch]
internal static class RebirthPlacedOriginalDecItem
{
    private static IEnumerable<MethodBase> TargetMethods()
    {yield return AccessTools.Method(typeof(Bag),nameof(Bag.DecItem));yield return AccessTools.Method(typeof(Inventory),nameof(Inventory.DecItem));}
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(object __instance,ref int __result)
    {
        var grid=__instance is Bag bag?bag.ItemGrid:(__instance as Inventory)?.ItemGrid;
        if(!RebirthPlacedOriginal.Blocks(grid))return true;__result=0;return false;
    }
}
[HarmonyPatch(typeof(Bag),nameof(Bag.AddItem))]
internal static class RebirthPlacedOriginalBagAdd
{
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(Bag __instance,ref bool __result){if(!RebirthPlacedOriginal.Blocks(__instance.ItemGrid))return true;__result=false;return false;}
}
[HarmonyPatch(typeof(Bag),nameof(Bag.TryStackItem))]
internal static class RebirthPlacedOriginalBagPartial
{
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(Bag __instance,ref (bool anyMoved,bool allMoved) __result){if(!RebirthPlacedOriginal.Blocks(__instance.ItemGrid))return true;__result=(false,false);return false;}
}
[HarmonyPatch(typeof(EntityAlive),nameof(EntityAlive.UseHoldingItem),new[]{typeof(int),typeof(bool)})]
internal static class RebirthPlacedOriginalHeldUse
{
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(EntityAlive __instance,ref bool __result){if(!RebirthPlacedOriginal.IsHeld(__instance as EntityPlayerLocal))return true;__result=false;return false;}
}
[HarmonyPatch]
internal static class RebirthPlacedOriginalActions
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        foreach(var type in new[]{typeof(ItemActionEntryUse),typeof(ItemActionEntryDrop),typeof(ItemActionEntryScrap),typeof(ItemActionEntryEquip),typeof(ItemActionEntryWear),typeof(ItemActionEntryRepair),typeof(ItemActionEntrySell),typeof(ItemActionEntryPurchase),typeof(ItemActionEntryTake),typeof(ItemActionEntryCraft),typeof(ItemActionEntryCombine),typeof(ItemActionEntryAssemble)})
            yield return AccessTools.DeclaredMethod(type,"OnActivated",Type.EmptyTypes)??throw new MissingMethodException(type.FullName,"OnActivated");
    }
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(BaseItemActionEntry __instance)=>!RebirthPlacedOriginal.IsHeld(__instance?.ItemController?.xui?.playerUI?.entityPlayer);
}

// Fence the gesture before its first cursor/native-slot side effect. Guarding only
// the eventual bag setter is too late: native SwapItem writes the cursor first.
[HarmonyPatch]
internal static class RebirthPlacedOriginalSlotGesture
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        foreach(string name in new[]{"HandleStackSwap","HandlePartialStackPickup","HandleDropOne","SwapItem","HandleMoveToPreferredLocation"})
            yield return AccessTools.DeclaredMethod(typeof(XUiC_ItemStack),name,Type.EmptyTypes)??throw new MissingMethodException(typeof(XUiC_ItemStack).Name,name);
    }
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(XUiC_ItemStack __instance)
        =>!RebirthPlacedOriginal.IsHeld(__instance?.xui?.playerUI?.entityPlayer);
}
[HarmonyPatch(typeof(XUiC_DragAndDropWindow),nameof(XUiC_DragAndDropWindow.SetCurrentStack))]
internal static class RebirthPlacedOriginalCursorSet
{
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(XUiC_DragAndDropWindow __instance,ItemStack stack)
        =>!RebirthPlacedOriginal.IsHeld(__instance?.xui?.playerUI?.entityPlayer);
}
[HarmonyPatch]
internal static class RebirthPlacedOriginalCursorGesture
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        foreach(var method in AccessTools.GetDeclaredMethods(typeof(XUiC_DragAndDropWindow)))
            if(method.Name=="DropCurrentItem"||method.Name=="PlaceItemBackInInventory")yield return method;
    }
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(XUiC_DragAndDropWindow __instance)
        =>!RebirthPlacedOriginal.IsHeld(__instance?.xui?.playerUI?.entityPlayer);
}

// Hold only original publication-bound player mutations before their native
// grant/damage effects. Do not block arbitrary raw world writes after their
// callers may already have created falling blocks/explosion output.
[HarmonyPatch(typeof(RebirthBlockPickupService),nameof(RebirthBlockPickupService.ProcessServerRequest))]
internal static class RebirthPlacedOriginalPickupRequestPatch
{private static bool Prefix(World __0,Vector3i __1)=>!RebirthPlacedOriginal.IsUnpublished(__0,__1)&&RemoteResourceTransactions.AllowActionBlockEscape(__0,__1,"rebirth-voluntary-block-pickup");}
[HarmonyPatch(typeof(GameManager),nameof(GameManager.PickupBlockServer))]
internal static class RebirthPlacedOriginalNativePickupPatch
{
 private static bool Prefix(GameManager __instance,Vector3i __0,MethodBase __originalMethod,bool __runOriginal,out object __state)
 {
    __state=null;if(RebirthPlacedOriginal.IsUnpublished(GameManager.Instance?.World,__0))return false;
    if(!__runOriginal||!ThreadManager.IsMainThread())return true;
    if(!RemoteResourceTransactions.AllowActionBlockEscape(__instance.World,__0,"native-voluntary-block-pickup"))return false;
    return RebirthRemoteResourcePlacedStationOriginal.EnterCancellationBlockEscape("take",__instance.World,__0,__originalMethod,true,out __state);
 }
 private static void Postfix(bool __runOriginal,object __state)=>RebirthRemoteResourcePlacedStationOriginal.ObserveCancellationBlockEscape(__runOriginal,__state);
 private static Exception Finalizer(Exception __exception,object __state){RebirthRemoteResourcePlacedStationOriginal.ObserveCancellationBlockEscape(false,__state);return __exception;}
}
[HarmonyPatch]
internal static class RebirthPlacedOriginalDamagePatch
{
 private static IEnumerable<MethodBase> TargetMethods(){foreach(var type in new[]{typeof(Block),typeof(BlockBladeTrap),typeof(BlockElectricWire)})yield return AccessTools.DeclaredMethod(type,nameof(Block.OnBlockDamaged),new[]{typeof(WorldBase),typeof(BlockValueRef),typeof(BlockValue),typeof(int),typeof(int),typeof(ItemActionAttack.AttackHitInfo),typeof(bool),typeof(bool),typeof(int)});}

 [HarmonyPriority(Priority.First+3)]
 private static bool Prefix(WorldBase __0,BlockValueRef __1,int __3,int __4,ItemActionAttack.AttackHitInfo __5,ref int __result)
 {
    if(!__1.TryGetBlockPos(out var position))return true;
    if(RebirthPlacedOriginal.IsUnpublished(__0,position)){__result=0;return false;}
    // Direct player attacks are voluntary destruction. Native explosions,
    // structural collapse, zombie hits and genuine lethal lifecycle pass.
    if(__3>0&&__5!=null&&__0 is World actual&&actual.GetEntity(__4) is EntityPlayer&&
        !RemoteResourceTransactions.AllowActionBlockEscape(__0,position,"native-player-block-damage")){__result=0;return false;}
    return true;
 }
}

[HarmonyPatch]
internal static class RebirthPlacedOriginalActivationPatch
{
 private static IEnumerable<MethodBase> TargetMethods()
 {
    var signature=new[]{typeof(string),typeof(WorldBase),typeof(Vector3i),typeof(BlockValue),typeof(EntityPlayerLocal)};
    foreach(var type in new[]{typeof(Block),typeof(BlockLight),typeof(BlockPowered),typeof(BlockPowerSource),typeof(BlockSwitch),typeof(BlockRanged),typeof(BlockLauncher),typeof(BlockPoweredTrap),typeof(BlockTimerRelay),typeof(BlockPressurePlate),typeof(BlockMotionSensor),typeof(BlockElectricWire),typeof(BlockSolarPanel),typeof(BlockGenerator),typeof(BlockBatteryBank),typeof(BlockBladeTrap)}){var method=type.GetMethod(nameof(Block.OnBlockActivated),BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static|BindingFlags.DeclaredOnly,null,signature,null);if(method!=null)yield return method;}
 }
 [HarmonyPriority(Priority.First)]
 private static bool Prefix(WorldBase __1,Vector3i __2,ref bool __result){if(!RebirthPlacedOriginal.IsPendingTarget(__1,__2))return true;__result=false;return false;}
}

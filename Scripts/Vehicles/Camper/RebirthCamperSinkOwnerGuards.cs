using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;

// Hold the original owner's native actions at their admission boundary. A sink
// transfer never edits arbitrary bag/cursor images to undo a competing action.
[HarmonyPatch]
internal static class RebirthCamperSinkOwnerActions
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        foreach(string name in new[]{"HandleStackSwap","HandlePartialStackPickup","HandleDropOne","SwapItem","HandleMoveToPreferredLocation"})
            yield return AccessTools.DeclaredMethod(typeof(XUiC_ItemStack),name,Type.EmptyTypes);
        foreach(var type in new[]{typeof(ItemActionEntryUse),typeof(ItemActionEntryDrop),typeof(ItemActionEntryScrap),
            typeof(ItemActionEntryEquip),typeof(ItemActionEntryWear),typeof(ItemActionEntryRepair),typeof(ItemActionEntrySell),
            typeof(ItemActionEntryPurchase),typeof(ItemActionEntryTake),typeof(ItemActionEntryCraft),typeof(ItemActionEntryCombine),
            typeof(ItemActionEntryAssemble),typeof(RebirthLiteratureUseEntry)})
            yield return AccessTools.DeclaredMethod(type,"OnActivated",Type.EmptyTypes);
        yield return AccessTools.DeclaredMethod(typeof(PlayerMoveController),"DropHeldItem",Type.EmptyTypes);
    }
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(object __instance)
    {
        var player=__instance is XUiController ui?ui.xui?.playerUI?.entityPlayer:
            __instance is BaseItemActionEntry entry?entry.ItemController?.xui?.playerUI?.entityPlayer:
            (__instance as PlayerMoveController)?.entityPlayerLocal;
        return !RebirthCamperSink.IsHeld(player);
    }
}
[HarmonyPatch]
internal static class RebirthCamperSinkOtherCustody
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        foreach(var method in typeof(RebirthBackpackLibraryReservation).GetMethods(BindingFlags.Public|BindingFlags.Static))
            if(method.Name=="TryAcquire")yield return method;
        yield return AccessTools.DeclaredMethod(typeof(RebirthGearOwnerReservation),"CanAcquire");
        yield return AccessTools.DeclaredMethod(typeof(RebirthGearOwnerReservation),"CanAcquireCold");
        yield return AccessTools.DeclaredMethod(typeof(RebirthCamperEnergyOwnerHold),"Prepare");
        yield return AccessTools.DeclaredMethod(typeof(RebirthCamperStationNativeActorPayment),"Begin");
    }
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(object[] __args,ref bool __result)
    {
        if(!RebirthCamperSink.IsHeld(__args?.OfType<EntityPlayer>().FirstOrDefault()))return true;
        __result=false;return false;
    }
}
[HarmonyPatch(typeof(RebirthBackpackLibraryReservation),nameof(RebirthBackpackLibraryReservation.BlocksResourceUse))]
internal static class RebirthCamperSinkResourceGate
{private static void Postfix(EntityPlayer player,ref bool __result)=>__result|=RebirthCamperSink.IsHeld(player);}

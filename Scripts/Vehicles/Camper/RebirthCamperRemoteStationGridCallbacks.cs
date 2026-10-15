using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;

[HarmonyPatch(typeof(XUiC_ItemStack),nameof(XUiC_ItemStack.HandleStackSwap))]
public static class RebirthCamperRemoteStationSwap
{ private static bool Prefix(XUiC_ItemStack __instance)=>RebirthCamperRemoteStationClient.Transfer(__instance,RebirthCamperRemoteStationGesture.Swap); }
[HarmonyPatch(typeof(XUiC_ItemStack),nameof(XUiC_ItemStack.SwapItem))]
public static class RebirthCamperRemoteStationSwapItem
{ private static bool Prefix(XUiC_ItemStack __instance)=>RebirthCamperRemoteStationClient.Transfer(__instance,RebirthCamperRemoteStationGesture.Swap); }
[HarmonyPatch(typeof(XUiC_ItemStack),nameof(XUiC_ItemStack.HandlePartialStackPickup))]
public static class RebirthCamperRemoteStationHalf
{ private static bool Prefix(XUiC_ItemStack __instance)=>RebirthCamperRemoteStationClient.Transfer(__instance,RebirthCamperRemoteStationGesture.Half); }
[HarmonyPatch(typeof(XUiC_ItemStack),nameof(XUiC_ItemStack.HandleDropOne))]
public static class RebirthCamperRemoteStationOne
{ private static bool Prefix(XUiC_ItemStack __instance)=>RebirthCamperRemoteStationClient.Transfer(__instance,RebirthCamperRemoteStationGesture.One); }
[HarmonyPatch(typeof(XUiC_ItemStack),nameof(XUiC_ItemStack.HandleMoveToPreferredLocation))]
public static class RebirthCamperRemoteStationPreferred
{ private static bool Prefix(XUiC_ItemStack __instance)=>RebirthCamperRemoteStationClient.Transfer(__instance,RebirthCamperRemoteStationGesture.Preferred); }
[HarmonyPatch(typeof(XUiM_Workstation),nameof(XUiM_Workstation.SetIsBurning))]
public static class RebirthCamperRemoteStationFuelSwitch
{
 private static bool Prefix(XUiM_Workstation __instance,bool __0)
 {
  var state=__instance?.TileEntity as RebirthCamperStationState;
  if(state?.BoundVehicle?.world?.IsRemote()!=true)return true;
  RebirthCamperRemoteStationClient.SetBurning(state,__0);return false;
 }
}

// Native backpack shift-transfer calls the grid directly, bypassing a station
// cell's cursor callback. Refuse that offered insertion before it can debit
// native inventory; cursor placement remains the acknowledged transfer path.
[HarmonyPatch]
public static class RebirthCamperRemoteStationDirectInsert
{
 private static IEnumerable<MethodBase> TargetMethods()
 {
  foreach(var method in typeof(XUiC_WorkstationGrid).GetMethods(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic))
   if(method.DeclaringType==typeof(XUiC_WorkstationGrid) && (method.Name=="AddItem" || method.Name=="TryStackItem"))yield return method;
 }
 private static bool Prefix(XUiC_WorkstationGrid __instance,ref bool __result)
 {
  var window=__instance.GetParentByType<XUiC_WorkstationWindowGroup>();
  var state=window?.WorkstationData?.TileEntity as RebirthCamperStationState;
  if(state?.BoundVehicle?.world?.IsRemote()!=true)return true;
  var player=__instance.xui?.playerUI?.entityPlayer;
  if(player!=null)GameManager.ShowTooltip(player,Localization.Get("xuiRebirthCamperUseCursor"));
  __result=false;return false;
 }
}

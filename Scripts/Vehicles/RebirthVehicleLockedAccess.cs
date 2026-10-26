using System;
using HarmonyLib;

// Use the game's saved lock and allow-list as the single source of truth.
internal static class RebirthVehicleLockedAccess
{
    internal static bool Denied(EntityVehicle vehicle,EntityPlayer player)
    {
        if(vehicle==null||player==null||vehicle.vehicle==null||!vehicle.hasLock()||!vehicle.IsLocked())return false;
        var data=GameManager.Instance?.GetPersistentPlayerList()?.GetPlayerDataFromEntityID(player.entityId);
        if(data==null)return true;
        var owner=vehicle.GetOwner();
        return !(data.PrimaryId!=null&&(data.PrimaryId.Equals(owner)||vehicle.isAllowedUser(data.PrimaryId)))
            &&!(data.NativeId!=null&&(data.NativeId.Equals(owner)||vehicle.isAllowedUser(data.NativeId)));
    }

    internal static string Message=>Localization.Get("xuiRebirthVehicleNoRights");
}

[HarmonyPatch(typeof(EntityVehicle),nameof(EntityVehicle.AllowActivationCommand))]
internal static class RebirthVehicleLockedCommandAccess
{
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(EntityVehicle __instance,EntityPlayerLocal __1,ref bool __result)
    {
        if(!RebirthVehicleLockedAccess.Denied(__instance,__1))return true;
        __result=false;
        return false;
    }
}

[HarmonyPatch(typeof(Entity),nameof(Entity.GetActivationCommands))]
internal static class RebirthVehicleLockedCommandVisibility
{
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(Entity __instance,ref EntityActivationCommand[] __result)
    {
        if(__instance is EntityVehicle vehicle && RebirthVehicleLockedAccess.Denied(vehicle,GameManager.Instance?.World?.GetPrimaryPlayer()))
            __result=Array.Empty<EntityActivationCommand>();
    }
}

[HarmonyPatch(typeof(EntityVehicle),nameof(EntityVehicle.GetActivationText))]
internal static class RebirthVehicleLockedActivationText
{
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(EntityVehicle __instance,ref string __result)
    {
        if(RebirthVehicleLockedAccess.Denied(__instance,GameManager.Instance?.World?.GetPrimaryPlayer()))
            __result=RebirthVehicleLockedAccess.Message;
    }
}

[HarmonyPatch(typeof(PlayerMoveController),"HandleInteraction")]
internal static class RebirthVehicleLockedInteractionPrompt
{
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(PlayerMoveController __instance)
    {
        var player=__instance.entityPlayerLocal;
        if(player==null)return;
        var vehicle=XUiC_RebirthVehicleHudStatBar.ResolveLookVehicle(player);
        if(!RebirthVehicleLockedAccess.Denied(vehicle,player))return;
        string message=RebirthVehicleLockedAccess.Message;
        XUiC_InteractionPrompt.SetText(__instance.playerUI,message);
        __instance.strTextLabelPointingTo=message;
    }
}

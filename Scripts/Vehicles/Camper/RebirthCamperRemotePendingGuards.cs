using System;
using HarmonyLib;

// Protect the already prepared native custody when a client closes/disconnects.
// In particular StopUIInteraction's ordinary flags14 must not overwrite a
// candidate with the client's pre-acknowledgement bag and vehicle metadata.
[HarmonyPatch(typeof(EntityVehicle),nameof(EntityVehicle.ReadSyncData))]
public static class RebirthCamperRemotePendingSync
{
    private static bool Prefix(EntityVehicle __instance,ushort __1)
    {
        if(__instance.world==null || __instance.world.IsRemote() || !RebirthCamperRadial.IsCamper(__instance)
            || (__1 & 12)==0)return true;
        // A real prepared transfer already has an authoritative runtime owner.
        // During native save loading the default vehicle item has not yet been
        // replaced by the saved one. Creating the runtime here cached an empty
        // model, whose next tick overwrote the newly loaded module metadata.
        return !RebirthCamperModuleRuntime.TryGetExisting(__instance,out var runtime) || !runtime.TransferPending;
    }
}

[HarmonyPatch(typeof(EntityVehicle),nameof(EntityVehicle.OnLockRequestServer))]
public static class RebirthCamperRemotePendingLock
{
    private static bool Prefix(EntityVehicle __instance,int __0,ushort __2,ref bool __result)
    {
        if(RebirthCamperRemoteTransport.CanTakePendingLock(__instance,__0,__2))return true;
        __result=false;return false;
    }
}

[HarmonyPatch(typeof(EntityVehicle),nameof(EntityVehicle.OnEntityActivated))]
public static class RebirthCamperRemotePendingCommand
{
    private static bool Prefix(EntityVehicle __instance,EntityActivationCommand __0,EntityPlayerLocal __1)
    {
        if(!RebirthCamperRadial.IsCamper(__instance) || RebirthCamperRadial.IsCommand(__0.commandId))return true;
        if(__instance.world!=null && !__instance.world.IsRemote())
        {
            var runtime=RebirthCamperModuleRuntime.For(__instance);
            if(!runtime.TransferPending)
            {
                runtime.SetTransferPending(false);
                return true;
            }
            GameManager.ShowTooltip(__1,Localization.Get("xuiRebirthCamperTransferRecover"));return false;
        }
        string pending;
        if(!__instance.vehicle.GetUpdatedItemValue().TryGetMetadata("RebirthCamperTransferPending",out pending) || pending!="1")return true;
        GameManager.ShowTooltip(__1,Localization.Get("xuiRebirthCamperTransferRecover"));return false;
    }
}

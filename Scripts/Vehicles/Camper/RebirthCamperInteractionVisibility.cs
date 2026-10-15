using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;

// Native channel zero already excludes cargo, management and station use.
// Replicate its owner through native vehicle metadata for radial visibility;
// this is presentation only and never grants a lock or inventory access.
[HarmonyPatch(typeof(EntityVehicle),nameof(EntityVehicle.Update))]
internal static class RebirthCamperInteractionVisibility
{
    internal const string OwnerKey="RebirthCamperInteractionOwner";
    private sealed class Clock {internal float Next;}
    private static readonly ConditionalWeakTable<EntityVehicle,Clock> clocks=new ConditionalWeakTable<EntityVehicle,Clock>();
    private static void Postfix(EntityVehicle __instance)
    {
        if(!RebirthCamperRadial.IsCamper(__instance)||__instance.world==null||__instance.world.IsRemote())return;
        var clock=clocks.GetValue(__instance,_=>new Clock());
        if(Time.realtimeSinceStartup<clock.Next)return;clock.Next=Time.realtimeSinceStartup+.2f;
        string owner=RebirthCamperRemoteAdmission.NativeInteractionOwner(__instance).ToString(System.Globalization.CultureInfo.InvariantCulture);
        var item=__instance.vehicle.GetUpdatedItemValue();string previous;
        if(item.TryGetMetadata(OwnerKey,out previous)&&previous==owner)return;
        item.SetMetadata(OwnerKey,owner);__instance.SendSyncData(4);
    }
}

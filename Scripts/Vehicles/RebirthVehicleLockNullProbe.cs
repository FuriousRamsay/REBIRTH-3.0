using System;
using HarmonyLib;

// V01 probe. Native EntityVehicle.OnLockRequestServer dereferences Entity.bag (Bag.IsEmpty) and
// OnLockResponseServer dereferences Entity.bag.ItemGrid for every channel except 1, so a vehicle
// without a bag is the one provable null source on the vehicle side of LockManager.LockRequestServer.
// The dispatcher stack alone cannot name it, so this logs the exact stage and null candidates once.
// It never changes a lock decision, never swallows an exception and never touches inventory.
internal static class RebirthVehicleLockNullProbe
{
    internal static bool Enabled=true;
    // Candidate-state lines and thrown exceptions use separate, per target+channel keys (bounded) so one
    // vehicle's null-bag observation can never suppress another request's real exception record.
    private static readonly System.Collections.Generic.HashSet<long> loggedCandidates=new System.Collections.Generic.HashSet<long>();
    private static readonly System.Collections.Generic.HashSet<long> loggedExceptions=new System.Collections.Generic.HashSet<long>();
    private static long Key(EntityVehicle vehicle,ushort channel)=>((long)(vehicle?.entityId??0)<<16)|channel;

    internal static string Candidates(EntityVehicle vehicle)
    {
        if(vehicle==null)return "vehicle=null";
        return "bag="+(vehicle.bag==null?"null":"set")+" bagGrid="+(vehicle.bag?.ItemGrid==null?"null":"set")
            +" vehicleData="+(vehicle.vehicle==null?"null":"set")+" world="+(vehicle.world==null?"null":"set");
    }

    internal static void Report(EntityVehicle vehicle,ushort channel,string stage,Exception error)
    {
        if(!Enabled)return;
        string candidates=Candidates(vehicle);
        // Only bag / bag.ItemGrid (the native dereferences) trigger a candidate line; vehicle.vehicle / world may be
        // legitimately null. A thrown callback is always recorded with its full stack, independent of that state.
        bool bagNull=vehicle==null||vehicle.bag==null||vehicle.bag.ItemGrid==null;
        long key=Key(vehicle,channel);
        if(error!=null)
        {
            if(loggedExceptions.Count>=32||!loggedExceptions.Add(key))return;
            try
            {
                Log.Error("[REBIRTH VehicleLock] callback exception stage="+stage+" target="+(vehicle?.entityId??0)+" channel="+channel
                    +" "+candidates+"\n"+error);
            }
            catch(Exception){}
            return;
        }
        if(!bagNull||loggedCandidates.Count>=32||!loggedCandidates.Add(key))return;
        try
        {
            Log.Warning("[REBIRTH VehicleLock] null probe stage="+stage+" target="+(vehicle?.entityId??0)+" channel="+channel+" "+candidates);
        }
        catch(Exception){}
    }
}

[HarmonyPatch(typeof(EntityVehicle),nameof(EntityVehicle.OnLockRequestServer))]
internal static class RebirthVehicleLockRequestNullProbe
{
    [HarmonyPriority(Priority.First)]
    private static void Prefix(EntityVehicle __instance,ushort __2)
    {RebirthVehicleLockNullProbe.Report(__instance,__2,"vehicle-callback-entered",null);}
}

[HarmonyPatch(typeof(EntityVehicle),"OnLockResponseServer",new Type[]{typeof(int),typeof(PooledBinaryWriter),typeof(ushort)})]
internal static class RebirthVehicleLockResponseNullProbe
{
    [HarmonyPriority(Priority.First)]
    private static void Prefix(EntityVehicle __instance,ushort __2)
    {
        RemoteResourceServerLockTransitionPatch.VehicleCallback(__instance?.entityId??0,__2,"vehicle-response-entered");
        RebirthVehicleLockNullProbe.Report(__instance,__2,"vehicle-response-entered",null);
    }
    [HarmonyFinalizer]
    private static Exception Finalizer(EntityVehicle __instance,ushort __2,Exception __exception)
    {
        if(__exception!=null)
        {
            RemoteResourceServerLockTransitionPatch.VehicleCallback(__instance?.entityId??0,__2,"vehicle-response-threw");
            RebirthVehicleLockNullProbe.Report(__instance,__2,"vehicle-response-threw",__exception);
        }
        return __exception;
    }
}

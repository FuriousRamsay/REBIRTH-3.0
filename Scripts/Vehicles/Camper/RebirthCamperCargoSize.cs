using System;
using System.Runtime.CompilerServices;
using HarmonyLib;

// Both peers derive cargo dimensions from the synchronized native vehicle item.
// Legacy campers without module metadata retain their native bag until migration.
[HarmonyPatch(typeof(EntityVehicle), "getStorageSize")]
public static class RebirthCamperCargoSize
{
    private sealed class CapacityCache
    {
        internal string Modules, Identity;
        internal int Capacity;
    }
    private static readonly ConditionalWeakTable<EntityVehicle,CapacityCache> capacities
        =new ConditionalWeakTable<EntityVehicle,CapacityCache>();
    private static bool Prefix(EntityVehicle __instance, ref Vector2i __result)
    {
        if (__instance.vehicle == null || !__instance.vehicle.GetName().Equals("rebirthCamper80s",StringComparison.OrdinalIgnoreCase)) return true;
        var item=__instance.vehicle.GetUpdatedItemValue();
        string modules;
        if (!item.TryGetMetadata("RebirthCamperModules",out modules)) return true;
        string encodedIdentity;
        if (!item.TryGetMetadata("RebirthCamperIdentity",out encodedIdentity))
            throw new InvalidOperationException("Modular camper has no identity; retain cargo for recovery.");
        CapacityCache cached;
        if(capacities.TryGetValue(__instance,out cached)
            && string.Equals(cached.Modules,modules,StringComparison.Ordinal)
            && string.Equals(cached.Identity,encodedIdentity,StringComparison.Ordinal))
        { __result=new Vector2i(cached.Capacity,1); return false; }
        Guid identity;
        if (!Guid.TryParse(encodedIdentity,out identity) || identity==Guid.Empty)
            throw new InvalidOperationException("Modular camper has no valid identity; retain cargo for recovery.");
        RebirthCamperStorageInstallations storage;
        RebirthCamperStationInstallations stations;
        if (!RebirthCamperModuleItemCandidate.TryRestore(identity,item,out storage,out stations))
            throw new InvalidOperationException("Modular camper capacity could not be restored; retain cargo.");
        int capacity=storage.Capacity;
        cached=capacities.GetValue(__instance,_=>new CapacityCache());
        cached.Modules=modules; cached.Identity=encodedIdentity; cached.Capacity=capacity;
        __result=new Vector2i(capacity,1);
        return false;
    }
}

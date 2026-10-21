using System.IO;
using HarmonyLib;

internal static partial class RebirthRemoteResourceFetchedStationOriginal
{
    private static bool queueHydrationInstalled;
    internal static void InstallQueueHydration()
    {
        if(queueHydrationInstalled)return;
        RebirthHarmonyBootstrap.PatchClassOnce(new Harmony("rebirth.remote-resource.fetched-station.queue"),typeof(RebirthFetchedStationSnapshotHydration));
        queueHydrationInstalled=true;
    }
    internal static void HydrateDetachedQueue(RebirthCamperStationState station)
    {
        if(station?.Queue==null)return;
        foreach(var row in station.Queue)
        {
            if(!IsCancellationCandidate(row?.Recipe))continue;
            if(!RebirthCamperStationNativeCrafting.RestoreActorImages(row.Recipe)||!RebirthRemoteResourceFetchedStationPlan.RestoreRecipeFields(row.Recipe))
                throw new InvalidDataException("Fetched station native queue original cannot be hydrated.");
        }
    }
}

// This target returns a NEW detached native model, never an existing world TE.
// Unknown own markers throw before that model can enter runtime processing.
// Only real ingredient metadata is used; no synthetic ingredient is introduced.
[HarmonyPatch(typeof(RebirthCamperStationSnapshot),nameof(RebirthCamperStationSnapshot.Restore))]
internal static class RebirthFetchedStationSnapshotHydration
{
    private static void Postfix(RebirthCamperStationState __result)=>RebirthRemoteResourceFetchedStationOriginal.HydrateDetachedQueue(__result);
}

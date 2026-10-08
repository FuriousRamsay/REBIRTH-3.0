using System;
using HarmonyLib;

[HarmonyPatch(typeof(RegionFileChunkSnapshot), nameof(RegionFileChunkSnapshot.Update))]
public static class RebirthStationSnapshotUpdatePatch
{
    [HarmonyPrefix]
    public static void Prefix(RegionFileChunkSnapshot __instance,Chunk chunk,bool saveIfUnchanged,
        out RebirthStationSnapshotEvidence.Frame __state)
    {
        __state=null;
        try{__state=RebirthStationSnapshotEvidence.Begin(__instance,chunk,GameManager.Instance?.World,
            chunk.X,chunk.Z,saveIfUnchanged||chunk.NeedsSaving);}catch{RebirthStationSnapshotEvidence.Invalidate(__instance);}
    }
    [HarmonyFinalizer]
    public static Exception Finalizer(RegionFileChunkSnapshot __instance,Exception __exception,RebirthStationSnapshotEvidence.Frame __state)
    {
        // Update catches serializer exceptions itself; the separate successful save marker
        // is required even when __exception is null. Never swallow a native exception.
        try{RebirthStationSnapshotEvidence.Finish(__state,__instance.stream,__exception);}catch{RebirthStationSnapshotEvidence.Invalidate(__instance);}
        return __exception;
    }
}
[HarmonyPatch(typeof(Chunk), nameof(Chunk.save), new Type[]{typeof(PooledBinaryWriter)})]
public static class RebirthStationChunkSerializedPatch
{
    [HarmonyPostfix]
    public static void Postfix(Chunk __instance,PooledBinaryWriter __0)
    {RebirthStationSnapshotEvidence.Serialized(__instance,__0.BaseStream);}
}
[HarmonyPatch(typeof(RegionFileChunkSnapshot), nameof(RegionFileChunkSnapshot.Reset))]
public static class RebirthStationSnapshotResetPatch
{
    [HarmonyPrefix]
    public static void Prefix(RegionFileChunkSnapshot __instance){RebirthStationSnapshotEvidence.Invalidate(__instance);}
}
using System;
using HarmonyLib;
using UnityEngine;

[HarmonyPatch(typeof(XUiC_WorkstationWindowGroup), nameof(XUiC_WorkstationWindowGroup.SetTileEntity))]
public static class RebirthCamperStationWindowBinding
{
    private static bool Prefix(XUiC_WorkstationWindowGroup __instance, TileEntityWorkstation __0)
    {
        var mobile = __0 as RebirthCamperStationState;
        if (mobile == null) return true;
        if (mobile.BoundVehicle == null || !RebirthCamperStationWindowSession.Admitted(__instance, mobile))
            throw new InvalidOperationException("Station window requires an authenticated vehicle session.");
        __instance.WorkstationData = new XUiM_Workstation(mobile);
        __instance.workstationBlock = mobile.InstalledBlock;
        __instance.Workstation = mobile.InstalledBlock.Block.GetBlockName();
        return false;
    }
}

[HarmonyPatch(typeof(TileEntity), nameof(TileEntity.ToWorldPos))]
public static class RebirthCamperStationPosition
{
    private static bool Prefix(TileEntity __instance, ref Vector3i __result)
    {
        var mobile = __instance as RebirthCamperStationState;
        if (mobile == null) return true;
        var owner = mobile.BoundVehicle ?? mobile.MobileOwner;
        if (owner == null) throw new InvalidOperationException("Detached station has no world position.");
        __result = new Vector3i(Mathf.FloorToInt(owner.position.x), Mathf.FloorToInt(owner.position.y), Mathf.FloorToInt(owner.position.z));
        return false;
    }
}

[HarmonyPatch(typeof(TileEntity), "get_block")]
public static class RebirthCamperStationBlock
{
    private static bool Prefix(TileEntity __instance, ref Block __result)
    {
        var mobile = __instance as RebirthCamperStationState;
        if (mobile == null) return true;
        __result = mobile.InstalledBlock.Block;
        return false;
    }
}

// Native read calls SetDataFromNet -> UpdateVisible. A restored mobile
// snapshot has no terrain block (and is intentionally not attached to a
// vehicle yet). Its appearance belongs to the vehicle, not a world block.
[HarmonyPatch(typeof(TileEntityWorkstation), "UpdateVisible")]
public static class RebirthCamperStationTerrainVisibility
{
    private static bool Prefix(TileEntityWorkstation __instance)
        => !(__instance is RebirthCamperStationState);
}

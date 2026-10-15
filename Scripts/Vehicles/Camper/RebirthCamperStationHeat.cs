using HarmonyLib;
using UnityEngine;

[HarmonyPatch(typeof(TileEntity), nameof(TileEntity.emitHeatMapEvent))]
public static class RebirthCamperStationHeat
{
    // Set by the production installer only after this specific patch succeeds.
    public static bool IsInstalled { get; internal set; }
    private static bool Prefix(TileEntity __instance, World __0, EnumAIDirectorChunkEvent __1)
    {
        var state = __instance as RebirthCamperStationState;
        if (state == null) return true;
        var owner = state.MobileOwner;
        if (owner == null || !ReferenceEquals(owner.world, __0) || __0.IsRemote()) return false;
        if (__0.worldTime < state.heapMapLastTime) state.heapMapUpdateTime = 0;
        if (__0.worldTime >= state.heapMapUpdateTime && __0.aiDirector != null)
        {
            var pos = new Vector3i(Mathf.FloorToInt(owner.position.x), Mathf.FloorToInt(owner.position.y), Mathf.FloorToInt(owner.position.z));
            __0.aiDirector.NotifyActivity(__1, pos, state.InstalledBlock.Block.HeatMapStrength, 720f);
            state.heapMapLastTime = __0.worldTime;
            state.heapMapUpdateTime = __0.worldTime + AIDirector.GetActivityWorldTimeDelay();
        }
        return false;
    }
}

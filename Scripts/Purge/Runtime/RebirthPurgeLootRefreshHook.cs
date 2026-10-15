using HarmonyLib;

// Installed 3.x moved 2.6 loot-container ticking into TEFeatureStorage.
// Its UpdateTick only handles timed refill; the parent composite still ticks
// other features (doors, heat, workstations, etc.) normally.
[HarmonyPatch(typeof(TEFeatureStorage),nameof(TEFeatureStorage.UpdateTick))]
internal static class RebirthPurgeLootRefreshHook
{
    private static bool Prefix(World __0)=>!RebirthPurgeReleasePolicy.Enabled||
        !RebirthSandboxOptionManager.Current.IsPurge||__0==null||__0.IsRemote();
}

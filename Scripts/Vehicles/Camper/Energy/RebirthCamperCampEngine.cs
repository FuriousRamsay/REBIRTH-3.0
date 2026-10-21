using HarmonyLib;

// Keep native seat/camera admission. Refuse the engine start before it can
// produce a startup sound, consume fuel or begin alternator charging in Camp.
[HarmonyPatch(typeof(VPEngine), "startEngine")]
internal static class RebirthCamperCampEngine
{
    internal static bool Prefix(VPEngine __instance)
        => !RebirthCamperEnergyDrivingInput.Blocked(__instance?.vehicle?.entity);
}

#if REBIRTH_DEBUG
using HarmonyLib;

public sealed class RebirthVehicleTuningRuntimeModApi : IModApi
{
    public void InitMod(Mod mod)
    {
        // This project uses targeted installers; Harmony attributes alone do
        // not install a patch. Retention must keep running after the UI closes.
        var harmony = new Harmony("rebirth.vehicle-tuning.runtime");
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthVehicleTuningPhysicsBaseline));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthVehicleDriveAudit));
    }
}

// A session belongs to its exact local vehicle, not to the open tuning window.
// Reassert only explicit live edits before restoration captures/scales the baseline.
// Closing/hiding the UI must not surrender those edits while test-driving.
[HarmonyPatch(typeof(EntityVehicle), nameof(EntityVehicle.PhysicsFixedUpdate))]
internal static class RebirthVehicleTuningPhysicsBaseline
{
    [HarmonyPrefix, HarmonyPriority(Priority.First)]
    private static void Prefix(EntityVehicle __instance)
    {
        if (__instance == null || __instance.isEntityRemote) return;
        if (RebirthVehicleTuningUi.Sessions.TryGetValue(__instance, out var session) && session.Valid)
            session.ReapplyTunedValues();
    }
}
#endif

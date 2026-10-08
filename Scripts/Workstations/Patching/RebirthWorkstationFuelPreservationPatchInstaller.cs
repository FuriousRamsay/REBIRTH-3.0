using HarmonyLib;

#nullable disable

/// <summary>
/// Explicit Harmony installer for workstation fuel preservation.
/// Reflection is confined to this approved patch-installation boundary.
/// </summary>
public static class RebirthWorkstationFuelPreservationInstaller
{
    private static readonly HarmonyLib.Harmony HarmonyInstance =
        new HarmonyLib.Harmony(RebirthWorkstationFuelPreservation.HarmonyId);

    private static bool installed;
    public static bool IsInstalled { get { return installed; } }

    public static string Install()
    {
        if (installed)
            return "[WorkstationFuelPreservation] patches already installed.";

        try
        {
            RebirthHarmonyBootstrap.PatchClassOnce(HarmonyInstance, typeof(RebirthWorkstationUpdateTickPatch));
            RebirthHarmonyBootstrap.PatchClassOnce(HarmonyInstance, typeof(RebirthWorkstationHandleFuelPatch));
            installed = true;
            return "[WorkstationFuelPreservation] installed UpdateTick transition guard and HandleFuel catch-up cap.";
        }
        catch
        {
            HarmonyInstance.UnpatchSelf();
            installed = false;
            throw;
        }
    }
}

[HarmonyPatch(typeof(TileEntityWorkstation), nameof(TileEntityWorkstation.UpdateTick))]
internal static class RebirthWorkstationUpdateTickPatch
{
    private static void Prefix(
        TileEntityWorkstation __instance,
        World world,
        float[] ___currentMeltTimesLeft,
        bool[] ___isModuleUsed,
        out RebirthWorkstationFuelPreservation.UpdateState __state)
    {
        RebirthWorkstationFuelPreservation.PrefixUpdateTick(
            __instance, world, ___currentMeltTimesLeft, ___isModuleUsed, out __state);
    }

    [HarmonyPriority(Priority.High)]
    private static void Postfix(
        TileEntityWorkstation __instance,
        World world,
        float[] ___currentMeltTimesLeft,
        bool[] ___isModuleUsed,
        RebirthWorkstationFuelPreservation.UpdateState __state)
    {
        RebirthWorkstationFuelPreservation.PostfixUpdateTick(
            __instance, world, ___currentMeltTimesLeft, ___isModuleUsed, __state);
    }
}

[HarmonyPatch(typeof(TileEntityWorkstation), "HandleFuel")]
internal static class RebirthWorkstationHandleFuelPatch
{
    private static void Prefix(
        TileEntityWorkstation __instance,
        World _world,
        ref float _timePassed,
        float[] ___currentMeltTimesLeft,
        bool[] ___isModuleUsed)
    {
        RebirthWorkstationFuelPreservation.PrefixHandleFuel(
            __instance, _world, ___currentMeltTimesLeft, ___isModuleUsed, ref _timePassed);
    }
}

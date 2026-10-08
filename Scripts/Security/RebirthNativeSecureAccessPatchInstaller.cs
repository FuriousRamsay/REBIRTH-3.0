using HarmonyLib;
using Platform;

#nullable disable

/// <summary>
/// Extends the four concrete 3.1 native lock implementations with REBIRTH
/// ally/party authorization. Targets are explicit so a game update cannot make
/// the installer patch an unrelated same-named method discovered at runtime.
/// </summary>
public static class RebirthNativeSecureAccessPatchInstaller
{
    private const string HarmonyId = "rebirth.secureaccess.native.3.1";
    private static bool installed;
    public static bool IsInstalled { get { return installed; } }

    public static void Install()
    {
        if (installed)
            return;

        Harmony harmony = new Harmony(HarmonyId);
        try
        {
            RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthSecureAccessTileFeaturePatch));
            RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthSecureAccessVehiclePatch));
            RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthSecureAccessDronePatch));
            RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthSecureAccessVendingMachinePatch));
            installed = true;
            { if (RebirthLogSettings.RuntimeInstallLoggingEnabled) Log.Out("[REBIRTH SecureAccess] Extended four explicit 3.1 native lock implementations."); }
        }
        catch
        {
            harmony.UnpatchSelf();
            installed = false;
            throw;
        }
    }

    internal static void Apply(
        ILockable instance,
        PlatformUserIdentifierAbs userIdentifier,
        ref bool result)
    {
        // Preserve every native success exactly as returned by the game.
        if (result || instance == null || userIdentifier == null || !instance.IsLocked())
            return;

        PlatformUserIdentifierAbs owner = instance.GetOwner();
        string reason;
        if (RebirthSecureAccessPolicy.CanExpandAccess(
            userIdentifier,
            owner,
            RebirthSecureAccessPurpose.DirectInteraction,
            out reason))
        {
            result = true;
        }
    }
}

[HarmonyPatch(typeof(TEFeatureLockable), nameof(TEFeatureLockable.IsUserAllowed))]
internal static class RebirthSecureAccessTileFeaturePatch
{
    private static void Postfix(
        TEFeatureLockable __instance,
        PlatformUserIdentifierAbs _userIdentifier,
        ref bool __result)
    {
        RebirthNativeSecureAccessPatchInstaller.Apply(__instance, _userIdentifier, ref __result);
    }
}

[HarmonyPatch(typeof(EntityVehicle), nameof(EntityVehicle.IsUserAllowed))]
internal static class RebirthSecureAccessVehiclePatch
{
    private static void Postfix(
        EntityVehicle __instance,
        PlatformUserIdentifierAbs _userIdentifier,
        ref bool __result)
    {
        RebirthNativeSecureAccessPatchInstaller.Apply(__instance, _userIdentifier, ref __result);
    }
}

[HarmonyPatch(typeof(EntityDrone), nameof(EntityDrone.IsUserAllowed))]
internal static class RebirthSecureAccessDronePatch
{
    private static void Postfix(
        EntityDrone __instance,
        PlatformUserIdentifierAbs _userIdentifier,
        ref bool __result)
    {
        RebirthNativeSecureAccessPatchInstaller.Apply(__instance, _userIdentifier, ref __result);
    }
}

[HarmonyPatch(typeof(TileEntityVendingMachine), nameof(TileEntityVendingMachine.IsUserAllowed))]
internal static class RebirthSecureAccessVendingMachinePatch
{
    private static void Postfix(
        TileEntityVendingMachine __instance,
        PlatformUserIdentifierAbs _userIdentifier,
        ref bool __result)
    {
        RebirthNativeSecureAccessPatchInstaller.Apply(__instance, _userIdentifier, ref __result);
    }
}

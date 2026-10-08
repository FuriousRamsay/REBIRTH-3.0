using System;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Scripting;

#nullable disable

[Preserve]
public static class RebirthSleeperRespawnRuntimePolicy
{
    private static bool s_enabled;

    public static bool Enabled
    {
        get { return s_enabled; }
    }

    public static void SetEnabled(bool enabled)
    {
        s_enabled = enabled;
    }

    /// <summary>
    /// A cleared sleeper volume is allowed to perform its normal timed reset only when
    /// Sleeper Respawns is enabled. Initial activation is unaffected because an untouched
    /// volume has wasCleared == false.
    /// </summary>
    public static bool ShouldBlockRespawn(SleeperVolume volume)
    {
        return !s_enabled && volume != null && volume.wasCleared;
    }

    // Retained for compatibility with the original narrow CheckTrigger patch.
    public static bool ShouldBlockTrigger(SleeperVolume volume)
    {
        return ShouldBlockRespawn(volume);
    }
}

[Preserve]
public static class RebirthSleeperRespawnInstaller
{
    private static bool s_installed;

    public static void Install()
    {
        if (s_installed)
            return;

        Harmony harmony = new Harmony("rebirth.fresh.sleeperrespawns.3.1");
        int installedCount = 0;

        try
        {
            RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthSleeperRespawnCheckTriggerPatch));
            installedCount++;
        }
        catch (Exception ex)
        {
            Log.Error("[REBIRTH Sleeper Respawns] Failed to patch SleeperVolume.CheckTrigger(World, Vector3): "
                + ex.GetType().Name + ": " + ex.Message);
        }

        try
        {
            RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthSleeperRespawnUpdatePlayerTouchedPatch));
            installedCount++;
        }
        catch (Exception ex)
        {
            Log.Error("[REBIRTH Sleeper Respawns] Failed to patch SleeperVolume.UpdatePlayerTouched(World, EntityPlayer): "
                + ex.GetType().Name + ": " + ex.Message);
        }

        if (installedCount == 0)
            throw new InvalidOperationException("No Sleeper Respawns patches were installed.");

        s_installed = true;
        { if (RebirthLogSettings.RuntimeInstallLoggingEnabled) Log.Out("[REBIRTH Sleeper Respawns] Installed " + installedCount
            + "/2 runtime guards. enabled=" + RebirthSleeperRespawnRuntimePolicy.Enabled); }
    }
}

/// <summary>
/// Blocks the ordinary proximity-trigger path before it schedules a cleared volume for update.
/// </summary>
[Preserve]
[HarmonyPatch(
    typeof(SleeperVolume),
    nameof(SleeperVolume.CheckTrigger),
    new Type[] { typeof(World), typeof(Vector3) })]
public static class RebirthSleeperRespawnCheckTriggerPatch
{
    public static bool Prefix(SleeperVolume __instance, ref bool __result)
    {
        if (!RebirthSleeperRespawnRuntimePolicy.ShouldBlockRespawn(__instance))
            return true;

        __result = false;
        return false;
    }
}

/// <summary>
/// Authoritative guard for every normal sleeper reactivation path.
///
/// In 3.1, scripted sleeper triggers call OnTriggered(), which calls UpdatePlayerTouched()
/// directly and bypasses CheckTrigger(). Once the respawn timer has expired, the native method
/// calls Reset(), which clears wasCleared and repopulates the volume. Blocking this method while
/// a volume is still marked cleared prevents both proximity and scripted-trigger respawns while
/// leaving initial population and explicit quest/reset operations unchanged.
/// </summary>
[Preserve]
[HarmonyPatch(
    typeof(SleeperVolume),
    nameof(SleeperVolume.UpdatePlayerTouched),
    new Type[] { typeof(World), typeof(EntityPlayer) })]
public static class RebirthSleeperRespawnUpdatePlayerTouchedPatch
{
    public static bool Prefix(SleeperVolume __instance)
    {
        return !RebirthSleeperRespawnRuntimePolicy.ShouldBlockRespawn(__instance);
    }
}

/// <summary>
/// Independent bootstrap so an unrelated failure earlier in the shared RebirthFreshInit
/// sequence cannot prevent the sleeper-respawn policy from being installed.
/// </summary>
[Preserve]
public sealed class RebirthSleeperRespawnBootstrap : IModApi
{
    public void InitMod(Mod _modInstance)
    {
        try
        {
            RebirthSleeperRespawnInstaller.Install();
        }
        catch (Exception ex)
        {
            Log.Error("[REBIRTH Sleeper Respawns] Bootstrap failed: "
                + ex.GetType().Name + ": " + ex.Message);
        }
    }
}

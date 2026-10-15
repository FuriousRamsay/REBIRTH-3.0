#if DEBUG
using HarmonyLib;
using System;
using System.Text;

#nullable disable

/// <summary>
/// Explicit manual patch installer for harvest/salvage first-slice testing.
/// 
/// This intentionally does not call PatchAll.
/// It installs only named, audited/manual-test patch methods.
/// </summary>
public static class RebirthHarvestSystemsManualPatchInstaller
{
    public const string HarmonyId = "rebirth.fresh.harvest.systems.manual";

    private static readonly HarmonyLib.Harmony s_harmony = new HarmonyLib.Harmony(HarmonyId);

    private static bool s_cropPatchInstalled;
    private static bool s_attackHarvestPatchInstalled;
    private static bool s_stumpHarvestContextPatchInstalled;

    public static bool IsCropPatchInstalled { get { return s_cropPatchInstalled; } }
    public static bool IsAttackHarvestPatchInstalled { get { return s_attackHarvestPatchInstalled; } }
    public static bool IsStumpHarvestContextPatchInstalled { get { return s_stumpHarvestContextPatchInstalled; } }

    public static bool AnyInstalled
    {
        get { return s_cropPatchInstalled || s_attackHarvestPatchInstalled || s_stumpHarvestContextPatchInstalled; }
    }

    public static string InstallCropPatch()
    {
        s_cropPatchInstalled = false;
        RebirthCropActivationHarvestPolicy.Disable();
        return "[RebirthHarvestSystemsManualPatchInstaller] 3.0: crop patch skipped; old BlockCropsGrown target not present.";
    }

    public static string InstallAttackHarvestPatch()
    {
        if (s_attackHarvestPatchInstalled)
            return "[RebirthHarvestSystemsManualPatchInstaller] attack harvest patch already installed.";

        s_harmony.CreateClassProcessor(typeof(Harmony_RebirthAttackHarvestScaffoldPatch)).Patch();
        s_attackHarvestPatchInstalled = true;

        return "[RebirthHarvestSystemsManualPatchInstaller] attack harvest no-op scaffold installed explicitly.";
    }

    public static string InstallStumpHarvestContextPatch()
    {
        if (s_stumpHarvestContextPatchInstalled)
            return "[RebirthHarvestSystemsManualPatchInstaller] stump harvest context patch already installed.";

        s_harmony.CreateClassProcessor(typeof(Harmony_RebirthStumpHarvestContextScaffoldPatch)).Patch();
        s_stumpHarvestContextPatchInstalled = true;

        return "[RebirthHarvestSystemsManualPatchInstaller] stump harvest context no-op scaffold installed explicitly.";
    }

    public static string InstallAll()
    {
        StringBuilder sb = new StringBuilder(2048);
        sb.AppendLine(InstallCropPatch());
        sb.AppendLine(InstallAttackHarvestPatch());
        sb.AppendLine(InstallStumpHarvestContextPatch());
        return sb.ToString();
    }

    public static string UninstallAll()
    {
        s_harmony.UnpatchSelf();

        s_cropPatchInstalled = false;
        s_attackHarvestPatchInstalled = false;
        s_stumpHarvestContextPatchInstalled = false;

        RebirthCropActivationHarvestPolicy.Reset();
        RebirthAttackHarvestPolicy.Disable();
        RebirthAttackHarvestBonusPolicy.Disable();
        RebirthAttackHarvestCounters.Reset();
        RebirthStumpHarvestContextPolicy.Disable();
        RebirthStumpHarvestRewardPolicy.Disable();
        RebirthStumpHarvestContextStore.Clear();

        return "[RebirthHarvestSystemsManualPatchInstaller] uninstalled all harvest/salvage manual patches, disabled manual policies, and cleared stored stump context.";
    }

    public static string GetStatusReport()
    {
        return "[RebirthHarvestSystemsManualPatchInstaller] harmonyId: " + HarmonyId
            + "; cropPatchInstalled: " + s_cropPatchInstalled
            + "; attackHarvestPatchInstalled: " + s_attackHarvestPatchInstalled
            + "; stumpHarvestContextPatchInstalled: " + s_stumpHarvestContextPatchInstalled;
    }
}
#endif

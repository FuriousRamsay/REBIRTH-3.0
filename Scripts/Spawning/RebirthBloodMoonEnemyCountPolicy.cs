using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine.Scripting;

#nullable disable

/// <summary>
/// Makes the Blood Moon active-enemy limit follow the configured Blood Moon Count
/// instead of the hidden party limit and per-wave maxAlive value.
///
/// Blood Moon Count retains the game's per-player meaning:
/// configured count * members in the Blood Moon party.
/// </summary>
[Preserve]
public static class RebirthBloodMoonEnemyCountPolicy
{
    public static int GetConfiguredPartyActiveCount(AIDirectorBloodMoonParty party)
    {
        if (party == null || party.partySpawner == null || party.partySpawner.partyMembers == null)
            return 0;

        int configuredPerPlayer = Math.Max(0, AIDirectorBloodMoonComponent.BloodMoonEnemyCount);
        int partyMembers = Math.Max(1, party.partySpawner.partyMembers.Count);

        long requested = (long)configuredPerPlayer * partyMembers;
        return requested > int.MaxValue ? int.MaxValue : (int)requested;
    }

    /// <summary>
    /// Replacement for Utils.FastMin(waveMaxAlive, enemyActiveMax) in
    /// AIDirectorBloodMoonParty.Tick. The wave value is intentionally ignored so
    /// the active target is controlled by the Blood Moon Count setting.
    /// </summary>
    public static int UseConfiguredActiveCount(int waveMaxAlive, int configuredActiveCount)
    {
        return Math.Max(0, configuredActiveCount);
    }
}

[Preserve]
public static class RebirthBloodMoonEnemyCountInstaller
{
    private static bool s_installed;

    public static void Install()
    {
        if (s_installed)
            return;

        Harmony harmony = new Harmony("rebirth.bloodmoon.enemycount.settingsonly.3.1");
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthBloodMoonInitPartySettingsPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthBloodMoonTickSettingsPatch));

        s_installed = true;
        { if (RebirthLogSettings.RuntimeInstallLoggingEnabled) Log.Out("[REBIRTH Blood Moon Count] Settings-driven active enemy limit installed."); }
    }
}

/// <summary>
/// Independent bootstrap so this gameplay patch is not skipped if an unrelated
/// installer in the shared Rebirth bootstrap fails.
/// </summary>
[Preserve]
public sealed class RebirthBloodMoonEnemyCountBootstrap : IModApi
{
    public void InitMod(Mod _modInstance)
    {
        RebirthBloodMoonEnemyCountInstaller.Install();
    }
}

[Preserve]
[HarmonyPatch(typeof(AIDirectorBloodMoonParty), nameof(AIDirectorBloodMoonParty.InitParty))]
public static class RebirthBloodMoonInitPartySettingsPatch
{
    /// <summary>
    /// Reproduces the native initialization without the hidden fixed party cap.
    /// Removing the clamp also removes its compensating game-stage scaling.
    /// </summary>
    public static bool Prefix(AIDirectorBloodMoonParty __instance)
    {
        if (__instance == null || __instance.partySpawner == null)
            return true;

        int partyLevel = __instance.partySpawner.CalcPartyLevel();

        __instance.enemyActiveMax =
            RebirthBloodMoonEnemyCountPolicy.GetConfiguredPartyActiveCount(__instance);

        // Native 3.1 increases game-stage scaling when requested count exceeds 30.
        // With no hidden count clamp, there is nothing to compensate for.
        __instance.partySpawner.SetScaling(1f);
        __instance.partySpawner.SetPartyLevel(partyLevel);
        __instance.bonusLootSpawnCount = __instance.partySpawner.bonusLootEvery / 2;

        return false;
    }
}

[Preserve]
[HarmonyPatch(typeof(AIDirectorBloodMoonParty), nameof(AIDirectorBloodMoonParty.Tick))]
public static class RebirthBloodMoonTickSettingsPatch
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        MethodInfo nativeFastMin = AccessTools.Method(
            typeof(Utils),
            nameof(Utils.FastMin),
            new Type[] { typeof(int), typeof(int) });

        MethodInfo replacement = AccessTools.Method(
            typeof(RebirthBloodMoonEnemyCountPolicy),
            nameof(RebirthBloodMoonEnemyCountPolicy.UseConfiguredActiveCount),
            new Type[] { typeof(int), typeof(int) });

        FieldInfo enemyActiveMax = AccessTools.Field(
            typeof(AIDirectorBloodMoonParty),
            nameof(AIDirectorBloodMoonParty.enemyActiveMax));

        if (nativeFastMin == null || replacement == null || enemyActiveMax == null)
            throw new InvalidOperationException(
                "REBIRTH Blood Moon Count could not resolve the required 3.1 method signatures.");

        List<CodeInstruction> codes = new List<CodeInstruction>(instructions);
        int replacements = 0;

        for (int i = 1; i < codes.Count; i++)
        {
            if (!codes[i].Calls(nativeFastMin))
                continue;

            CodeInstruction prior = codes[i - 1];
            if (prior.opcode != OpCodes.Ldfld || !Equals(prior.operand, enemyActiveMax))
                continue;

            codes[i].opcode = OpCodes.Call;
            codes[i].operand = replacement;
            replacements++;
        }

        if (replacements != 1)
        {
            throw new InvalidOperationException(
                "REBIRTH Blood Moon Count expected exactly one active-limit clamp in " +
                "AIDirectorBloodMoonParty.Tick but found " + replacements + ".");
        }

        return codes;
    }
}

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Scripting;

#nullable disable

[Preserve]
public static class RebirthSleeperSpawnMultiplierRuntimePolicy
{
    private static int s_normalMultiplier = 1;
    private static int s_infestedMultiplier = 2;

    public static void SetMultipliers(int normal, int infested)
    {
        s_normalMultiplier = Mathf.Clamp(normal, 1, 4);
        s_infestedMultiplier = Mathf.Clamp(infested, 2, 6);
    }

    public static void AddScaledSpawnCount(SleeperVolume volume, string groupName, float nativeMin, float nativeMax)
    {
        if (volume == null) return;

        // UpdatePlayerTouched has already multiplied the authored min/max by the
        // native quest multiplier and POI difficulty scale. Divide out only the
        // native quest multiplier, then apply the selected REBIRTH multiplier.
        // This matches the 2.6 contract without replacing the complete native method.
        float factor = GetPopulationFactor(volume);
        volume.AddSpawnCount(groupName, nativeMin * factor, nativeMax * factor);

        // Do NOT clamp the count to spawnsAvailable.Count. SleeperVolume.FindSpawnIndex()
        // deliberately calls ResetSpawnsAvailable() when the list is exhausted, which
        // is how the 2.6 multiplier supported populations larger than the number of
        // unique authored sleeper blocks in a volume.
    }

    public static void RunScaledMinScript(
        MinScript script,
        SleeperVolume volume,
        EntityPlayer player,
        float nativeCountScale)
    {
        if (script == null) return;

        // MinScript spawn commands call SleeperVolume.AddSpawnCount later from
        // MinScript.Tick(). They therefore need the same corrected count scale as
        // the primary sleeper group. Without this replacement, scripted infested
        // groups would continue using the native quest multiplier instead of the
        // selected REBIRTH infested multiplier.
        float factor = volume == null ? 1f : GetPopulationFactor(volume);
        script.Run(volume, player, nativeCountScale * factor);
    }

    private static float GetPopulationFactor(SleeperVolume volume)
    {
        PrefabInstance prefab = volume.prefabInstance;
        if (prefab == null) return s_normalMultiplier;

        // Preserve the native special bandit population scale exactly.
        if (prefab.LastRefreshType.Test_AnySet(QuestEventManager.banditTag)) return 1f;

        float nativeQuestMultiplier = prefab.LastQuestClass == null ? 1f : prefab.LastQuestClass.SpawnMultiplier;
        if (nativeQuestMultiplier <= 0f) nativeQuestMultiplier = 1f;

        bool isInfested = prefab.LastRefreshType.Test_AnySet(QuestEventManager.infestedTag);
        int selectedMultiplier = isInfested ? s_infestedMultiplier : s_normalMultiplier;
        return selectedMultiplier / nativeQuestMultiplier;
    }
}

[Preserve]
public static class RebirthSleeperSpawnMultiplierInstaller
{
    private static bool s_installed;
    public static void Install()
    {
        if (s_installed) return;
        RebirthHarmonyBootstrap.PatchClassOnce(new Harmony("rebirth.fresh.sleepermultipliers.3.1"), typeof(RebirthSleeperSpawnMultiplierPatch));
        s_installed = true;
    }
}

[Preserve]
[HarmonyPatch(
    typeof(SleeperVolume),
    nameof(SleeperVolume.UpdatePlayerTouched),
    new Type[] { typeof(World), typeof(EntityPlayer) })]
public static class RebirthSleeperSpawnMultiplierPatch
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        MethodInfo native = AccessTools.Method(
            typeof(SleeperVolume),
            nameof(SleeperVolume.AddSpawnCount),
            new[] { typeof(string), typeof(float), typeof(float) });
        MethodInfo replacement = AccessTools.Method(
            typeof(RebirthSleeperSpawnMultiplierRuntimePolicy),
            nameof(RebirthSleeperSpawnMultiplierRuntimePolicy.AddScaledSpawnCount),
            new[] { typeof(SleeperVolume), typeof(string), typeof(float), typeof(float) });
        MethodInfo nativeMinScriptRun = AccessTools.Method(
            typeof(MinScript),
            nameof(MinScript.Run),
            new[] { typeof(SleeperVolume), typeof(EntityPlayer), typeof(float) });
        MethodInfo replacementMinScriptRun = AccessTools.Method(
            typeof(RebirthSleeperSpawnMultiplierRuntimePolicy),
            nameof(RebirthSleeperSpawnMultiplierRuntimePolicy.RunScaledMinScript),
            new[] { typeof(MinScript), typeof(SleeperVolume), typeof(EntityPlayer), typeof(float) });
        if (native == null || replacement == null || nativeMinScriptRun == null || replacementMinScriptRun == null)
            throw new InvalidOperationException("REBIRTH Sleeper Multiplier method signatures are unavailable.");

        int spawnCountReplacementCount = 0;
        int minScriptReplacementCount = 0;
        foreach (CodeInstruction instruction in instructions)
        {
            if (instruction.Calls(native))
            {
                instruction.opcode = OpCodes.Call;
                instruction.operand = replacement;
                spawnCountReplacementCount++;
            }
            else if (instruction.Calls(nativeMinScriptRun))
            {
                instruction.opcode = OpCodes.Call;
                instruction.operand = replacementMinScriptRun;
                minScriptReplacementCount++;
            }
            yield return instruction;
        }

        if (spawnCountReplacementCount != 1 || minScriptReplacementCount != 1)
            throw new InvalidOperationException(
                "REBIRTH Sleeper Multiplier expected one AddSpawnCount call and one MinScript.Run call in "
                + "SleeperVolume.UpdatePlayerTouched but found AddSpawnCount=" + spawnCountReplacementCount
                + " MinScript.Run=" + minScriptReplacementCount + ".");
    }
}

/// <summary>
/// Independent bootstrap so an unrelated failure in the shared Rebirth installer
/// cannot silently leave the UI option active while the runtime patch is absent.
/// </summary>
[Preserve]
public sealed class RebirthSleeperSpawnMultiplierModApi : IModApi
{
    public void InitMod(Mod modInstance)
    {
        try
        {
            RebirthSleeperSpawnMultiplierInstaller.Install();
            { if (RebirthLogSettings.RuntimeInstallLoggingEnabled) Log.Out("[REBIRTH Sleeper Multipliers] Runtime patch installed."); }
        }
        catch (Exception ex)
        {
            Log.Error("[REBIRTH Sleeper Multipliers] Install failed: "
                + ex.GetType().Name + ": " + ex.Message);
        }
    }
}

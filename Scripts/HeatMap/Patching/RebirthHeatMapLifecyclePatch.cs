using System;
using HarmonyLib;
using UnityEngine.Scripting;

#nullable disable

/// <summary>
/// Restores persistent heat accumulation across failed scout rolls/spawn attempts.
/// Base 2.6 and 3.1 both clear the heat region as soon as the threshold is checked,
/// even when the 20% scout roll fails. Rebirth retains the existing events until a
/// scout horde is actually queued, then applies the normal long cooldown and reset.
/// </summary>
[Preserve]
public static class RebirthHeatMapLifecycleInstaller
{
    private const string HarmonyId = "rebirth.heatmap.lifecycle.3.1";
    private static bool s_installed;

    public static string Install()
    {
        if (s_installed)
            return "[REBIRTH HeatMap] lifecycle patch already installed";

        try
        {
            Harmony harmony = new Harmony(HarmonyId);
            RebirthHarmonyBootstrap.PatchClassOnce(
                harmony,
                typeof(RebirthHeatMapCheckToSpawnPatch));

            s_installed = true;
            return "[REBIRTH HeatMap] installed persistent heat lifecycle patch";
        }
        catch (Exception ex)
        {
            string failure = "[REBIRTH HeatMap] lifecycle patch failed: "
                + ex.GetType().Name + ": " + ex.Message;
            Log.Error(failure);
            return failure;
        }
    }
}

[HarmonyPatch(typeof(AIDirectorChunkEventComponent), "CheckToSpawn", new Type[] { typeof(AIDirectorChunkData) })]
internal static class RebirthHeatMapCheckToSpawnPatch
{
    private sealed class WarningState { public float Next; public int Suppressed; public WarningState() { } }
    private static System.Runtime.CompilerServices.ConditionalWeakTable<AIDirectorChunkData, WarningState> warnings =
        new System.Runtime.CompilerServices.ConditionalWeakTable<AIDirectorChunkData, WarningState>();
    internal static void ResetWarnings()
    {
        warnings = new System.Runtime.CompilerServices.ConditionalWeakTable<AIDirectorChunkData, WarningState>();
    }
    private static void WarnMissingEvent(AIDirectorChunkData chunk)
    {
        WarningState state = warnings.GetOrCreateValue(chunk);
        float now = UnityEngine.Time.realtimeSinceStartup;
        if (state.Next > now) { state.Suppressed++; return; }
        Log.Warning("[REBIRTH HeatMap] threshold reached but no heat event was available; activity retained; repeated=" + state.Suppressed);
        state.Suppressed = 0;
        state.Next = now + 30f;
    }
    private const float ScoutThreshold = 25f;
    private const float ScoutChance = 0.20f;

    private static bool Prefix(
        AIDirectorChunkEventComponent __instance,
        AIDirectorChunkData _chunkData)
    {
        if (__instance == null || _chunkData == null)
            return false;

        if (!GameStats.GetBool(EnumGameStats.ZombieHordeMeter)
            || !GameStats.GetBool(EnumGameStats.IsSpawnEnemies)
            || _chunkData.ActivityLevel < ScoutThreshold)
        {
            return false;
        }

        AIDirectorChunkEvent bestEvent = FindBestEventWithoutReset(_chunkData);
        if (bestEvent == null)
        {
            WarnMissingEvent(_chunkData);
            return false;
        }

        // Do not consume accumulated heat when the scout chance fails.
        bool scoutRollPassed = __instance.Director != null
            && __instance.Director.random.RandomFloat < ScoutChance
            && !GameUtils.IsPlaytesting();

        if (!scoutRollPassed)
            return false;

        int spawnCountBefore = __instance.scoutSpawnList != null
            ? __instance.scoutSpawnList.Count
            : 0;

        __instance.SpawnScouts(bestEvent.Position.ToVector3());

        int spawnCountAfter = __instance.scoutSpawnList != null
            ? __instance.scoutSpawnList.Count
            : 0;

        // Spawn placement, player-distance restrictions, or another Rebirth policy
        // can reject the attempt. Keep all heat/events so the region can try again.
        if (spawnCountAfter <= spawnCountBefore)
        {
            if (RebirthDiagnosticPolicy.MayPrepare(RebirthLogSettings.HeatMapLoggingEnabled))
            Log.Out("[REBIRTH HeatMap] scout roll passed but no scout was queued; heat retained at "
                + _chunkData.ActivityLevel.ToString("F3"));
            return false;
        }

        // A real scout event now exists. Apply the same long regional/neighbor
        // cooldown used by vanilla successful scout spawns, then consume the heat.
        __instance.StartCooldownOnNeighbors(bestEvent.Position, true);
        _chunkData.FindBestEventAndReset();
        _chunkData.SetLongDelay();

        if (RebirthDiagnosticPolicy.MayPrepare(RebirthLogSettings.HeatMapLoggingEnabled))
        Log.Out("[REBIRTH HeatMap] scout queued; heat consumed and long cooldown started at "
            + bestEvent.Position);

        return false;
    }

    private static AIDirectorChunkEvent FindBestEventWithoutReset(
        AIDirectorChunkData chunkData)
    {
        int count = chunkData.EventCount;
        if (count <= 0)
            return null;

        AIDirectorChunkEvent best = chunkData.GetEvent(0);
        for (int i = 1; i < count; i++)
        {
            AIDirectorChunkEvent candidate = chunkData.GetEvent(i);
            if (candidate != null
                && (best == null || candidate.Value > best.Value))
            {
                best = candidate;
            }
        }

        return best;
    }
}

#if DEBUG
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Platform;
using UnityEngine;
using UnityEngine.Scripting;

#nullable disable

[Preserve]
public class ConsoleCmdRebirthLightFxAutoBenchFresh : ConsoleCmdAbstract
{
    private static RebirthLightFxAutoBenchRunner s_runner;

    public override bool IsExecuteOnClient { get { return true; } }

    public override string[] getCommands()
    {
        return new[] { "rblightbench", "rbfxbench" };
    }

    public override string getDescription()
    {
        return "Runs a focused automated FPS isolation suite for rendered lights and powered-light shadows.";
    }

    public override string getHelp()
    {
        return "Usage:\n"
             + "  rblightbench auto [phaseSeconds] [warmupSeconds]\n"
             + "  rblightbench stop\n"
             + "  rblightbench status\n"
             + "\n"
             + "Recommended:\n"
             + "  rblightbench auto 20 5\n"
             + "\n"
             + "Runs a compact sequence from the current camera/player position and restores lights at the end.\n"
             + "Phases: baseline, global rendered lights off, powered/player-placed shadows off, restored final.\n"
             + "Aliases: rblightbench, rbfxbench";
    }

    public override void Execute(List<string> _params, CommandSenderInfo _senderInfo)
    {
        string cmd = (_params == null || _params.Count == 0)
            ? "auto"
            : (_params[0] ?? string.Empty).Trim().ToLowerInvariant();

        if (cmd == "help" || cmd == "?")
        {
            Log.Out(getHelp());
            return;
        }

        if (cmd == "stop" || cmd == "cancel")
        {
            StopRunner(true);
            return;
        }

        if (cmd == "status")
        {
            Log.Out("[LightBench] running=" + (s_runner != null));
            return;
        }

        if (cmd != "auto" && cmd != "full" && cmd != "run" && cmd != "start")
        {
            Log.Out("[LightBench] Unknown subcommand: " + cmd);
            Log.Out(getHelp());
            return;
        }

        int phaseSeconds = ParseInt(_params, 1, 20, 5, 120);
        int warmupSeconds = ParseInt(_params, 2, 5, 0, 30);
        if (warmupSeconds >= phaseSeconds) warmupSeconds = Math.Max(0, phaseSeconds - 1);

        StopRunner(false);

        GameObject go = new GameObject("RebirthLightFxAutoBenchRunner");
        UnityEngine.Object.DontDestroyOnLoad(go);
        s_runner = go.AddComponent<RebirthLightFxAutoBenchRunner>();
        s_runner.Configure(phaseSeconds, warmupSeconds, OnRunnerFinished);
        Log.Out("[LightBench] Started focused rendered-light FPS suite phaseSeconds=" + phaseSeconds
            + " warmupSeconds=" + warmupSeconds
            + ". Keep camera/player still until the final summary is printed.");
        RebirthLightFxAutoBenchRunner.NotifyToolbelt("Light bench started. Keep camera still until completion message.");
    }

    private static int ParseInt(List<string> args, int index, int defaultValue, int min, int max)
    {
        if (args == null || args.Count <= index)
            return defaultValue;

        int value;
        if (!int.TryParse(args[index], out value))
            return defaultValue;

        if (value < min)
            return min;
        if (value > max)
            return max;
        return value;
    }

    private static void StopRunner(bool log)
    {
        if (s_runner == null)
        {
            if (log)
                Log.Out("[LightBench] No focused suite is running.");
            return;
        }

        try
        {
            s_runner.AbortAndRestore();
        }
        catch
        {
        }

        UnityEngine.Object.Destroy(s_runner.gameObject);
        s_runner = null;
        if (log)
        {
            Log.Out("[LightBench] Stopped focused suite and requested restore.");
            RebirthLightFxAutoBenchRunner.NotifyToolbelt("Light bench stopped and restore requested.");
        }
    }

    private static void OnRunnerFinished(RebirthLightFxAutoBenchRunner runner)
    {
        if (s_runner == runner)
            s_runner = null;
    }
}

public class RebirthLightFxAutoBenchRunner : MonoBehaviour
{
    private struct PhaseResult
    {
        public string Name;
        public double AvgFps;
        public double MinFps;
        public double MaxFps;
        public int Frames;
        public double Seconds;
        public int PoweredLightLods;
        public int PoweredLightsEnabled;
        public int PoweredLightsShadowCasting;
        public int AllLightLods;
        public int AllLightLodsEnabled;
        public int AllLightLodsShadowCasting;
        public long AreaCalls;
        public long PlantSyncs;
        public long LightEvaluateCalls;
        public double LightEvaluateMs;
        public long ThermalEvaluateCalls;
        public double ThermalEvaluateMs;
        public long LightProcessorRefreshLocalBlockCalls;
        public long LightProcessorSpreadBlockCalls;
        public long LightProcessorUnspreadBlockCalls;
        public long LightProcessorRefreshSunlightCalls;
        public long LightProcessorSpreadSunCalls;
        public long LightProcessorUnspreadSunCalls;
        public long ExposureInvalidationBlockChanges;
        public long ExposureInvalidationSkippedPlantLike;
    }

    private struct SceneCounts
    {
        public int PoweredLightLods;
        public int PoweredLightsEnabled;
        public int PoweredLightsShadowCasting;
        public int AllLightLods;
        public int AllLightLodsEnabled;
        public int AllLightLodsShadowCasting;
    }

    private readonly List<PhaseResult> s_results = new List<PhaseResult>(8);

    private int m_phaseSeconds;
    private int m_warmupSeconds;
    private Action<RebirthLightFxAutoBenchRunner> m_finishedCallback;
    private bool m_abort;
    private long m_startTimestamp;
    private bool m_samplingWasEnabled;
    private bool m_baselineCaptured;
    private bool m_baselineAllLightsOff;
    private bool m_baselinePoweredShadowDisabled;
    private readonly Dictionary<int, LightState> m_baselineLights = new Dictionary<int, LightState>();
    private const int PhaseTotal = 4;

    private struct LightState
    {
        public Light Light;
        public bool Enabled;
        public LightShadows Shadows;
        public float ShadowStrength;
    }

    public void Configure(int phaseSeconds, int warmupSeconds, Action<RebirthLightFxAutoBenchRunner> finishedCallback)
    {
        m_phaseSeconds = phaseSeconds;
        m_warmupSeconds = warmupSeconds;
        m_finishedCallback = finishedCallback;
        StartCoroutine(RunSuite());
    }

    public void AbortAndRestore()
    {
        m_abort = true;
        RestoreEverything();
    }

    private IEnumerator RunSuite()
    {
        m_startTimestamp = Stopwatch.GetTimestamp();

        CaptureBaseline();
        m_samplingWasEnabled = AdvancedFarmingPerfSnapshotService.SamplingEnabled;
        AdvancedFarmingPerfSnapshotService.SetSampling(true);

        Log.Out("[LightBench] === BEGIN focused rendered-light FPS suite ===");
        Log.Out("[LightBench] phaseSeconds=" + m_phaseSeconds + " warmupSeconds=" + m_warmupSeconds);
        Log.Out("[LightBench] Phases: baseline, global_lights_off, powered_shadows_off, restored_final.");
        NotifyToolbelt("Light bench: starting 4 phases. Do not move until completed.");

        yield return StartCoroutine(RunPhase("baseline_original", ApplyBaseline));
        if (m_abort) yield break;

        yield return StartCoroutine(RunPhase("global_lights_off", ApplyGlobalLightsOff));
        if (m_abort) yield break;

        yield return StartCoroutine(RunPhase("powered_shadows_off", ApplyPoweredShadowsOff));
        if (m_abort) yield break;

        yield return StartCoroutine(RunPhase("restored_final", ApplyBaseline));
        if (m_abort) yield break;

        RestoreEverything();
        RestoreSamplingState();

        PrintFinalSummary();
        Log.Out("[LightBench] === END focused rendered-light FPS suite ===");
        NotifyToolbelt("Light bench completed. Send the latest output_log_client zip.");

        if (m_finishedCallback != null)
            m_finishedCallback(this);
        UnityEngine.Object.Destroy(gameObject);
    }

    private IEnumerator RunPhase(string phaseName, Action apply)
    {
        RestoreEverything();
        if (apply != null)
            apply();

        int phaseNumber = s_results.Count + 1;
        Log.Out("[LightBench] PHASE START name=" + phaseName + " phase=" + phaseNumber + "/" + PhaseTotal
            + " warmupSeconds=" + m_warmupSeconds + " sampleSeconds=" + m_phaseSeconds);
        NotifyToolbelt("Light bench phase " + phaseNumber + "/" + PhaseTotal + " started: " + phaseName);

        float warmElapsed = 0f;
        while (warmElapsed < (float)m_warmupSeconds)
        {
            if (m_abort)
                yield break;
            warmElapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        // Re-apply once after warmup because vanilla LightLOD can rewrite rendered-light state.
        // Do not apply every frame; that would pollute the FPS sample.
        if (apply != null)
            apply();

        AdvancedFarmingPerfSnapshotService.SampleSnapshot afStart = AdvancedFarmingPerfSnapshotService.GetSnapshot();
        double elapsed = 0d;
        int frames = 0;
        double minFps = double.MaxValue;
        double maxFps = 0d;

        while (elapsed < (double)m_phaseSeconds)
        {
            if (m_abort)
                yield break;

            float dt = Time.unscaledDeltaTime;
            if (dt > 0.000001f)
            {
                double frameFps = 1.0d / (double)dt;
                if (frameFps < minFps)
                    minFps = frameFps;
                if (frameFps > maxFps)
                    maxFps = frameFps;
                elapsed += (double)dt;
                frames++;
            }

            yield return null;
        }

        AdvancedFarmingPerfSnapshotService.SampleSnapshot afEnd = AdvancedFarmingPerfSnapshotService.GetSnapshot();
        SceneCounts counts = CountScene();
        PhaseResult result = BuildPhaseResult(phaseName, frames, elapsed, minFps, maxFps, counts, afStart, afEnd);
        s_results.Add(result);
        LogPhaseResult(result);
        NotifyToolbelt("Light bench phase " + phaseNumber + "/" + PhaseTotal + " complete: " + phaseName + " avg " + FormatDouble(result.AvgFps) + " FPS.");
    }

    private PhaseResult BuildPhaseResult(
        string name,
        int frames,
        double elapsed,
        double minFps,
        double maxFps,
        SceneCounts counts,
        AdvancedFarmingPerfSnapshotService.SampleSnapshot afStart,
        AdvancedFarmingPerfSnapshotService.SampleSnapshot afEnd)
    {
        PhaseResult r = new PhaseResult();
        r.Name = name;
        r.Frames = frames;
        r.Seconds = elapsed;
        r.AvgFps = elapsed > 0d ? (double)frames / elapsed : 0d;
        r.MinFps = minFps == double.MaxValue ? 0d : minFps;
        r.MaxFps = maxFps;
        r.PoweredLightLods = counts.PoweredLightLods;
        r.PoweredLightsEnabled = counts.PoweredLightsEnabled;
        r.PoweredLightsShadowCasting = counts.PoweredLightsShadowCasting;
        r.AllLightLods = counts.AllLightLods;
        r.AllLightLodsEnabled = counts.AllLightLodsEnabled;
        r.AllLightLodsShadowCasting = counts.AllLightLodsShadowCasting;

        r.AreaCalls = afEnd.AreaCalls - afStart.AreaCalls;
        r.PlantSyncs = afEnd.PlantSyncs - afStart.PlantSyncs;
        r.LightEvaluateCalls = afEnd.LightEvaluateCalls - afStart.LightEvaluateCalls;
        r.LightEvaluateMs = TicksToMs(afEnd.LightEvaluateElapsedStopwatchTicks - afStart.LightEvaluateElapsedStopwatchTicks);
        r.ThermalEvaluateCalls = afEnd.ThermalEvaluateCalls - afStart.ThermalEvaluateCalls;
        r.ThermalEvaluateMs = TicksToMs(afEnd.ThermalEvaluateElapsedStopwatchTicks - afStart.ThermalEvaluateElapsedStopwatchTicks);
        r.LightProcessorRefreshLocalBlockCalls = afEnd.LightProcessorRefreshLocalBlockCalls - afStart.LightProcessorRefreshLocalBlockCalls;
        r.LightProcessorSpreadBlockCalls = afEnd.LightProcessorSpreadBlockCalls - afStart.LightProcessorSpreadBlockCalls;
        r.LightProcessorUnspreadBlockCalls = afEnd.LightProcessorUnspreadBlockCalls - afStart.LightProcessorUnspreadBlockCalls;
        r.LightProcessorRefreshSunlightCalls = afEnd.LightProcessorRefreshSunlightCalls - afStart.LightProcessorRefreshSunlightCalls;
        r.LightProcessorSpreadSunCalls = afEnd.LightProcessorSpreadSunCalls - afStart.LightProcessorSpreadSunCalls;
        r.LightProcessorUnspreadSunCalls = afEnd.LightProcessorUnspreadSunCalls - afStart.LightProcessorUnspreadSunCalls;
        r.ExposureInvalidationBlockChanges = afEnd.ExposureInvalidationBlockChanges - afStart.ExposureInvalidationBlockChanges;
        r.ExposureInvalidationSkippedPlantLike = afEnd.ExposureInvalidationSkippedPlantLike - afStart.ExposureInvalidationSkippedPlantLike;
        return r;
    }

    private void LogPhaseResult(PhaseResult r)
    {
        Log.Out("[LightBench] PHASE RESULT name=" + r.Name
            + " avgFps=" + FormatDouble(r.AvgFps)
            + " minFps=" + FormatDouble(r.MinFps)
            + " maxFps=" + FormatDouble(r.MaxFps)
            + " frames=" + r.Frames
            + " seconds=" + FormatDouble(r.Seconds)
            + " poweredLightLods=" + r.PoweredLightLods
            + " poweredLightsEnabled=" + r.PoweredLightsEnabled
            + " poweredLightsShadowCasting=" + r.PoweredLightsShadowCasting
            + " allLightLods=" + r.AllLightLods
            + " allLightLodsEnabled=" + r.AllLightLodsEnabled
            + " allLightLodsShadowCasting=" + r.AllLightLodsShadowCasting
            + " areaCalls=" + r.AreaCalls
            + " plantSyncs=" + r.PlantSyncs
            + " lightEvalCalls=" + r.LightEvaluateCalls
            + " lightEvalMs=" + FormatDouble(r.LightEvaluateMs)
            + " thermalEvalCalls=" + r.ThermalEvaluateCalls
            + " thermalEvalMs=" + FormatDouble(r.ThermalEvaluateMs)
            + " lpBlockRefresh=" + r.LightProcessorRefreshLocalBlockCalls
            + " lpBlockSpread=" + r.LightProcessorSpreadBlockCalls
            + " lpBlockUnspread=" + r.LightProcessorUnspreadBlockCalls
            + " lpSunRefresh=" + r.LightProcessorRefreshSunlightCalls
            + " lpSunSpread=" + r.LightProcessorSpreadSunCalls
            + " lpSunUnspread=" + r.LightProcessorUnspreadSunCalls
            + " exposureBlockChanges=" + r.ExposureInvalidationBlockChanges
            + " exposureSkippedPlantLike=" + r.ExposureInvalidationSkippedPlantLike);
    }

    private void PrintFinalSummary()
    {
        if (s_results.Count == 0)
            return;

        PhaseResult normal = s_results[0];
        StringBuilder sb = new StringBuilder(1024);
        sb.Append("[LightBench] FINAL SUMMARY baseline=").Append(FormatDouble(normal.AvgFps)).Append("fps");
        for (int i = 1; i < s_results.Count; i++)
        {
            PhaseResult r = s_results[i];
            sb.Append(" | ").Append(r.Name)
              .Append("=").Append(FormatDouble(r.AvgFps)).Append("fps")
              .Append(" delta=").Append(FormatDouble(r.AvgFps - normal.AvgFps));
        }
        Log.Out(sb.ToString());

        Log.Out("[LightBench] Read this as: global_lights_off isolates total rendered-light cost; powered_shadows_off isolates the shadow portion of powered/player-placed lights. Campfire FX and invalid local-light phases were removed in v115 because prior runs showed no useful signal.");
    }

    private void ApplyBaseline()
    {
        RestoreEverything();
    }

    private void CaptureBaseline()
    {
        m_baselineLights.Clear();
        m_baselineAllLightsOff = LightViewer.IsAllOff;
        m_baselinePoweredShadowDisabled = Harmony_RebirthPoweredLightShadowDiagnosticsPatch.DisablePoweredLightShadows;
        Light[] lights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
        for (int i=0;i<lights.Length;i++)
        {
            Light light=lights[i]; if (light==null) continue;
            m_baselineLights[light.GetInstanceID()] = new LightState { Light=light, Enabled=light.enabled, Shadows=light.shadows, ShadowStrength=light.shadowStrength };
        }
        m_baselineCaptured = true;
    }

    private void ApplyGlobalLightsOff()
    {
        SetGlobalLights(false);
    }

    private void ApplyPoweredShadowsOff()
    {
        SetGlobalLights(true);
        Harmony_RebirthPoweredLightShadowDiagnosticsPatch.SetDisabled(true);
    }

    private void RestoreEverything()
    {
        if (!m_baselineCaptured) return;
        Harmony_RebirthPoweredLightShadowDiagnosticsPatch.SetDisabled(m_baselinePoweredShadowDisabled);
        SetGlobalLights(!m_baselineAllLightsOff);
        foreach (KeyValuePair<int,LightState> pair in m_baselineLights)
        {
            LightState state=pair.Value;
            if (state.Light==null) continue;
            state.Light.enabled=state.Enabled;
            state.Light.shadows=state.Shadows;
            state.Light.shadowStrength=state.ShadowStrength;
        }
        RestoreSamplingState();
    }

    private void RestoreSamplingState()
    {
        AdvancedFarmingPerfSnapshotService.SetSampling(m_samplingWasEnabled);
    }

    private SceneCounts CountScene()
    {
        SceneCounts counts = new SceneCounts();
        LightLOD[] lightLods = UnityEngine.Object.FindObjectsByType<LightLOD>(FindObjectsSortMode.None);
        for (int i = 0; i < lightLods.Length; i++)
        {
            LightLOD lod = lightLods[i];
            if (lod == null)
                continue;

            counts.AllLightLods++;
            Light light = lod.GetLight();
            if (light != null)
            {
                if (light.enabled)
                    counts.AllLightLodsEnabled++;
                if (light.shadows != LightShadows.None)
                    counts.AllLightLodsShadowCasting++;
            }

            if (!IsPoweredBlockLight(lod))
                continue;

            counts.PoweredLightLods++;
            if (light != null)
            {
                if (light.enabled)
                    counts.PoweredLightsEnabled++;
                if (light.shadows != LightShadows.None)
                    counts.PoweredLightsShadowCasting++;
            }
        }

        return counts;
    }

    private static bool IsPoweredBlockLight(LightLOD lightLod)
    {
        if (lightLod == null || lightLod.bed == null)
            return false;

        Block block = lightLod.bed.blockValue.Block;
        return block is BlockPoweredLight;
    }

    private static void SetGlobalLights(bool enable)
    {
        if (Camera.main == null)
            return;

        LightViewer viewer = Camera.main.GetComponent<LightViewer>();
        if (viewer == null)
            viewer = Camera.main.gameObject.AddComponent<LightViewer>();

        if (enable)
        {
            LightViewer.IsAllOff = false;
            viewer.TurnOnAllLights();
        }
        else
        {
            LightViewer.IsAllOff = true;
            viewer.TurnOffAllLights();
        }
    }


    private void OnDestroy()
    {
        RestoreEverything();
    }

    public static void NotifyToolbelt(string message)
    {
        if (string.IsNullOrEmpty(message))
            return;

        try
        {
            if (GameManager.Instance == null || GameManager.Instance.World == null)
                return;

            EntityPlayerLocal player = GameManager.Instance.World.GetPrimaryPlayer();
            if (player == null)
                return;

            GameManager.ShowTooltip(player, message, true, false, 4f);
        }
        catch
        {
        }
    }

    private static double TicksToMs(long ticks)
    {
        return Stopwatch.Frequency > 0L ? (double)ticks * 1000.0d / (double)Stopwatch.Frequency : 0d;
    }

    private static string FormatDouble(double value)
    {
        return value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
    }
}
#endif

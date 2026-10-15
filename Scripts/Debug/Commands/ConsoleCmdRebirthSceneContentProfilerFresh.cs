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
public class ConsoleCmdRebirthSceneContentProfilerFresh : ConsoleCmdAbstract
{
    private static RebirthSceneContentProfilerRunner s_runner;

    public override bool IsExecuteOnClient { get { return true; } }

    public override string[] getCommands()
    {
        return new[] { "rbsceneperf", "rbfarmcontent", "rbscenefarm" };
    }

    public override string getDescription()
    {
        return "Runs a farm/scene content census and frame-pressure profiler around the local player.";
    }

    public override string getHelp()
    {
        return "Usage:\n"
             + "  rbsceneperf farm [radius] [durationSeconds] [intervalSeconds] [spikeMs] [severeMs] [warmupSeconds] [verticalRange]\n"
             + "  rbsceneperf census [radius] [verticalRange]\n"
             + "  rbsceneperf stop\n"
             + "  rbsceneperf status\n"
             + "\n"
             + "Recommended:\n"
             + "  rbsceneperf farm 64 360 15 25 40 5 24\n"
             + "\n"
             + "The farm command performs a one-time content/render census, then profiles frame/GC behavior without repeatedly scanning the scene."
             + " Toolbelt messages announce start, sampling, intervals, and completion.";
    }

    public override void Execute(List<string> _params, CommandSenderInfo _senderInfo)
    {
        string cmd = (_params == null || _params.Count == 0)
            ? "farm"
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
            Log.Out("[SceneContentProfiler] running=" + (s_runner != null));
            return;
        }

        if (cmd == "census" || cmd == "scan")
        {
            int censusRadius = ParseInt(_params, 1, 64, 4, 128);
            int censusVertical = ParseInt(_params, 2, 24, 4, 64);
            World world = GameManager.Instance != null ? GameManager.Instance.World : null;
            if (world == null)
            {
                Log.Out("[SceneContentProfiler] No world loaded.");
                return;
            }

            Vector3i center = ResolveCommandCenter(world, _senderInfo);
            RebirthSceneContentProfilerRunner.SceneCensus census = RebirthSceneContentProfilerRunner.BuildSceneCensus(world, center, censusRadius, censusVertical);
            RebirthSceneContentProfilerRunner.LogCensus("census", census);
            return;
        }

        if (cmd != "farm" && cmd != "auto" && cmd != "run" && cmd != "start")
        {
            Log.Out("[SceneContentProfiler] Unknown subcommand: " + cmd);
            Log.Out(getHelp());
            return;
        }

        int radius = ParseInt(_params, 1, 64, 4, 128);
        int durationSeconds = ParseInt(_params, 2, 360, 30, 900);
        int intervalSeconds = ParseInt(_params, 3, 15, 5, 60);
        double spikeMs = ParseDouble(_params, 4, 25d, 5d, 250d);
        double severeMs = ParseDouble(_params, 5, 40d, spikeMs, 500d);
        int warmupSeconds = ParseInt(_params, 6, 5, 0, 60);
        if (warmupSeconds >= durationSeconds) warmupSeconds = Math.Max(0, durationSeconds - 1);
        int verticalRange = ParseInt(_params, 7, 24, 4, 64);

        World runWorld = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (runWorld == null)
        {
            Log.Out("[SceneContentProfiler] No world loaded.");
            return;
        }

        Vector3i runCenter = ResolveCommandCenter(runWorld, _senderInfo);
        StopRunner(false);

        GameObject go = new GameObject("RebirthSceneContentProfilerRunner");
        UnityEngine.Object.DontDestroyOnLoad(go);
        s_runner = go.AddComponent<RebirthSceneContentProfilerRunner>();
        s_runner.Configure(runCenter, radius, verticalRange, durationSeconds, intervalSeconds, spikeMs, severeMs, warmupSeconds, OnRunnerFinished);

        Log.Out("[SceneContentProfiler] Started center=" + runCenter
            + " radius=" + radius
            + " verticalRange=" + verticalRange
            + " durationSeconds=" + durationSeconds
            + " intervalSeconds=" + intervalSeconds
            + " spikeMs=" + FormatDouble(spikeMs)
            + " severeMs=" + FormatDouble(severeMs)
            + " warmupSeconds=" + warmupSeconds
            + ". Keep camera/player still until completion.");
        RebirthSceneContentProfilerRunner.NotifyToolbelt("Scene/farm profiler started for " + durationSeconds + "s. Keep camera/player still.");
    }

    private static void StopRunner(bool log)
    {
        bool hadRunner = s_runner != null;
        if (s_runner != null)
        {
            try
            {
                s_runner.AbortAndRestore();
            }
            catch
            {
            }

            UnityEngine.Object.Destroy(s_runner.gameObject);
            s_runner = null;
        }

        if (log)
        {
            if (hadRunner)
            {
                Log.Out("[SceneContentProfiler] Stopped and restored sampling state.");
                RebirthSceneContentProfilerRunner.NotifyToolbelt("Scene/farm profiler stopped.");
            }
            else
            {
                Log.Out("[SceneContentProfiler] No run is active.");
            }
        }
    }

    private static void OnRunnerFinished(RebirthSceneContentProfilerRunner runner)
    {
        if (s_runner == runner)
            s_runner = null;
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

    private static double ParseDouble(List<string> args, int index, double defaultValue, double min, double max)
    {
        if (args == null || args.Count <= index)
            return defaultValue;

        double value;
        if (!double.TryParse(args[index], out value) || double.IsNaN(value) || double.IsInfinity(value))
            return defaultValue;

        if (value < min)
            return min;
        if (value > max)
            return max;
        return value;
    }

    private static Vector3i ResolveCommandCenter(World world, CommandSenderInfo senderInfo)
    {
        EntityPlayer player = null;

        if (senderInfo.RemoteClientInfo != null)
            player = world.GetEntity(senderInfo.RemoteClientInfo.entityId) as EntityPlayer;

        if (player == null && world.Players != null && world.Players.list != null && world.Players.list.Count > 0)
            player = world.Players.list[0];

        if (player != null)
            return World.worldToBlockPos(player.GetPosition());

        EntityPlayerLocal local = world.GetPrimaryPlayer();
        if (local != null)
            return World.worldToBlockPos(local.GetPosition());

        return Vector3i.zero;
    }

    private static string FormatDouble(double value)
    {
        return value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
    }
}

public class RebirthSceneContentProfilerRunner : MonoBehaviour
{
    public struct SceneCensus
    {
        public Vector3i Center;
        public int Radius;
        public int VerticalRange;
        public int BlocksScanned;
        public int NonAirBlocks;
        public int CropBlocks;
        public int CropTileEntities;
        public int FarmPlots;
        public int FarmPlotTileEntities;
        public int WaterTanks;
        public int WaterTankTileEntities;
        public int HeatSourceBlocks;
        public int CampfireBlocks;
        public int PoweredLightBlocks;
        public int DynamicCoverBlocks;
        public int CandidateTileEntityLookups;
        public int CandidateTileEntities;
        public int Renderers;
        public int RenderersEnabled;
        public int MeshRenderers;
        public int SkinnedRenderers;
        public int ParticleRenderers;
        public int ParticleSystems;
        public int ParticleSystemsPlaying;
        public int Lights;
        public int LightsEnabled;
        public int PointLights;
        public int SpotLights;
        public int DirectionalLights;
        public int ShadowCastingLights;
        public int LightLods;
        public int LightLodsEnabled;
        public int LightLodsShadowCasting;
        public int PoweredLightLods;
        public int PoweredLightLodsEnabled;
        public int PoweredLightLodsShadowCasting;
        public float LocalLightRangeSum;
        public float LocalLightIntensitySum;
        public string TopBlocks;
    }

    private struct SpikeRecord
    {
        public int Index;
        public double TimeSeconds;
        public double FrameMs;
        public double Fps;
        public long MemoryDeltaBytes;
        public int Gc0Delta;
        public int Gc1Delta;
        public int Gc2Delta;
        public AdvancedFarmingPerfSnapshotService.SampleSnapshot Before;
        public AdvancedFarmingPerfSnapshotService.SampleSnapshot After;
    }

    private const int MaxStoredSpikes = 32;

    [Flags]
    private enum CensusBlockFlags : byte
    {
        None = 0,
        Crop = 1,
        FarmPlot = 2,
        Water = 4,
        Heat = 8,
        Campfire = 16,
        PoweredLight = 32,
        DynamicCover = 64
    }

    private struct CensusBlockInfo
    {
        public string Name;
        public CensusBlockFlags Flags;
    }

    private static readonly Dictionary<int, CensusBlockInfo> s_censusBlockInfoByType = new Dictionary<int, CensusBlockInfo>(512);

    private Vector3i m_center;
    private int m_radius;
    private int m_verticalRange;
    private int m_durationSeconds;
    private int m_intervalSeconds;
    private double m_spikeMs;
    private double m_severeMs;
    private int m_warmupSeconds;
    private Action<RebirthSceneContentProfilerRunner> m_finishedCallback;
    private bool m_abort;
    private bool m_samplingWasEnabled;
    private bool m_warmupAnnounced;
    private SceneCensus m_startCensus;
    private AdvancedFarmingPerfSnapshotService.SampleSnapshot m_startSnapshot;
    private AdvancedFarmingPerfSnapshotService.SampleSnapshot m_intervalSnapshot;
    private AdvancedFarmingPerfSnapshotService.SampleSnapshot m_lastFrameSnapshot;
    private SpikeRecord[] m_spikes;
    private int m_spikeCount;
    private int m_totalSpikeCount;
    private int m_severeSpikeCount;
    private double m_elapsed;
    private double m_intervalElapsed;
    private double m_intervalFrameMsTotal;
    private double m_intervalFrameMsMax;
    private double m_intervalFrameMsMin;
    private int m_intervalFrames;
    private int m_intervalSpikeCount;
    private int m_intervalSevereCount;
    private long m_lastMemory;
    private long m_startMemory;
    private long m_intervalMemory;
    private int m_lastGc0;
    private int m_lastGc1;
    private int m_lastGc2;
    private int m_startGc0;
    private int m_startGc1;
    private int m_startGc2;
    private int m_intervalGc0;
    private int m_intervalGc1;
    private int m_intervalGc2;

    public void Configure(
        Vector3i center,
        int radius,
        int verticalRange,
        int durationSeconds,
        int intervalSeconds,
        double spikeMs,
        double severeMs,
        int warmupSeconds,
        Action<RebirthSceneContentProfilerRunner> finishedCallback)
    {
        m_center = center;
        m_radius = radius;
        m_verticalRange = verticalRange;
        m_durationSeconds = durationSeconds;
        m_intervalSeconds = intervalSeconds;
        m_spikeMs = spikeMs;
        m_severeMs = severeMs;
        m_warmupSeconds = warmupSeconds;
        m_finishedCallback = finishedCallback;
        m_spikes = new SpikeRecord[MaxStoredSpikes];
        StartCoroutine(RunProfile());
    }

    public void AbortAndRestore()
    {
        m_abort = true;
        RestoreSamplingState();
    }

    private IEnumerator RunProfile()
    {
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (world == null)
        {
            Log.Out("[SceneContentProfiler] No world loaded; aborting.");
            NotifyToolbelt("Scene/farm profiler aborted: no world loaded.");
            FinishAndDestroy();
            yield break;
        }

        NotifyToolbelt("Scene/farm profiler scanning content around player...");
        long censusTicksStart = Stopwatch.GetTimestamp();
        m_startCensus = BuildSceneCensus(world, m_center, m_radius, m_verticalRange);
        long censusTicks = Stopwatch.GetTimestamp() - censusTicksStart;
        Log.Out("[SceneContentProfiler] === BEGIN scene/content profiler ===");
        Log.Out("[SceneContentProfiler] censusElapsedMs=" + FormatDouble(TicksToMs(censusTicks)));
        LogCensus("start", m_startCensus);
        // Observational mode does not force a collection; GC counters/memory are sampled as-is.

        m_samplingWasEnabled = AdvancedFarmingPerfSnapshotService.SamplingEnabled;
        AdvancedFarmingPerfSnapshotService.SetSampling(true);
        m_startSnapshot = AdvancedFarmingPerfSnapshotService.GetSnapshot();
        m_intervalSnapshot = m_startSnapshot;
        m_lastFrameSnapshot = m_startSnapshot;
        m_lastMemory = GC.GetTotalMemory(false);
        m_startMemory = m_lastMemory;
        m_intervalMemory = m_lastMemory;
        m_lastGc0 = GC.CollectionCount(0);
        m_lastGc1 = GC.CollectionCount(1);
        m_lastGc2 = GC.CollectionCount(2);
        m_startGc0 = m_lastGc0;
        m_startGc1 = m_lastGc1;
        m_startGc2 = m_lastGc2;
        m_intervalGc0 = m_lastGc0;
        m_intervalGc1 = m_lastGc1;
        m_intervalGc2 = m_lastGc2;
        ResetIntervalFrameStats();

        Log.Out("[SceneContentProfiler] profile durationSeconds=" + m_durationSeconds
            + " intervalSeconds=" + m_intervalSeconds
            + " spikeMs=" + FormatDouble(m_spikeMs)
            + " severeMs=" + FormatDouble(m_severeMs)
            + " warmupSeconds=" + m_warmupSeconds
            + " samplingWasEnabled=" + m_samplingWasEnabled
            + " liveSpikeLogging=False");
        NotifyToolbelt("Scene/farm profiler running for " + m_durationSeconds + "s. Warmup=" + m_warmupSeconds + "s.");

        while (!m_abort && m_elapsed < (double)m_durationSeconds)
        {
            yield return null;

            float dt = Time.unscaledDeltaTime;
            if (dt <= 0.000001f)
                continue;

            double frameMs = (double)dt * 1000.0d;
            m_elapsed += (double)dt;
            m_intervalElapsed += (double)dt;
            m_intervalFrames++;
            m_intervalFrameMsTotal += frameMs;
            if (frameMs > m_intervalFrameMsMax)
                m_intervalFrameMsMax = frameMs;
            if (m_intervalFrameMsMin <= 0d || frameMs < m_intervalFrameMsMin)
                m_intervalFrameMsMin = frameMs;

            AdvancedFarmingPerfSnapshotService.SampleSnapshot now = AdvancedFarmingPerfSnapshotService.GetSnapshot();
            long memoryNow = GC.GetTotalMemory(false);
            long memoryDelta = memoryNow - m_lastMemory;
            int gc0Now = GC.CollectionCount(0);
            int gc1Now = GC.CollectionCount(1);
            int gc2Now = GC.CollectionCount(2);
            int gc0Delta = gc0Now - m_lastGc0;
            int gc1Delta = gc1Now - m_lastGc1;
            int gc2Delta = gc2Now - m_lastGc2;

            bool warmedUp = m_elapsed >= (double)m_warmupSeconds;
            if (warmedUp && !m_warmupAnnounced)
            {
                m_warmupAnnounced = true;
                m_intervalSnapshot = now;
                m_intervalMemory = memoryNow;
                m_intervalGc0 = gc0Now;
                m_intervalGc1 = gc1Now;
                m_intervalGc2 = gc2Now;
                ResetIntervalFrameStats();
                NotifyToolbelt("Scene/farm profiler warmup complete. Recording spikes now.");
            }

            if (warmedUp && frameMs >= m_spikeMs)
            {
                m_totalSpikeCount++;
                m_intervalSpikeCount++;
                if (frameMs >= m_severeMs)
                {
                    m_severeSpikeCount++;
                    m_intervalSevereCount++;
                }

                RecordSpike(m_totalSpikeCount, m_elapsed, frameMs, memoryDelta, gc0Delta, gc1Delta, gc2Delta, m_lastFrameSnapshot, now);
            }

            m_lastFrameSnapshot = now;
            m_lastMemory = memoryNow;
            m_lastGc0 = gc0Now;
            m_lastGc1 = gc1Now;
            m_lastGc2 = gc2Now;

            if (m_intervalElapsed >= (double)m_intervalSeconds)
            {
                LogInterval(now);
                m_intervalSnapshot = now;
                m_intervalMemory = GC.GetTotalMemory(false);
                m_intervalGc0 = GC.CollectionCount(0);
                m_intervalGc1 = GC.CollectionCount(1);
                m_intervalGc2 = GC.CollectionCount(2);
                ResetIntervalFrameStats();
            }
        }

        if (!m_abort)
        {
            AdvancedFarmingPerfSnapshotService.SampleSnapshot end = AdvancedFarmingPerfSnapshotService.GetSnapshot();
            if (m_intervalFrames > 0)
                LogInterval(end);

            LogFinal(end);

            World endWorld = GameManager.Instance != null ? GameManager.Instance.World : null;
            if (endWorld != null)
            {
                SceneCensus endCensus = BuildSceneCensus(endWorld, m_center, m_radius, m_verticalRange);
                LogCensus("end", endCensus);
            }

            NotifyToolbelt("Scene/farm profiler completed. Send the latest output_log_client zip.");
        }

        RestoreSamplingState();
        Log.Out("[SceneContentProfiler] === END scene/content profiler ===");
        FinishAndDestroy();
    }

    private void FinishAndDestroy()
    {
        if (m_finishedCallback != null)
            m_finishedCallback(this);
        UnityEngine.Object.Destroy(gameObject);
    }

    private void RestoreSamplingState()
    {
        AdvancedFarmingPerfSnapshotService.SetSampling(m_samplingWasEnabled);
    }

    private void OnDestroy()
    {
        RestoreSamplingState();
    }

    private void ResetIntervalFrameStats()
    {
        m_intervalElapsed = 0d;
        m_intervalFrameMsTotal = 0d;
        m_intervalFrameMsMax = 0d;
        m_intervalFrameMsMin = 0d;
        m_intervalFrames = 0;
        m_intervalSpikeCount = 0;
        m_intervalSevereCount = 0;
    }

    private void RecordSpike(
        int index,
        double elapsedSeconds,
        double frameMs,
        long memoryDelta,
        int gc0Delta,
        int gc1Delta,
        int gc2Delta,
        AdvancedFarmingPerfSnapshotService.SampleSnapshot before,
        AdvancedFarmingPerfSnapshotService.SampleSnapshot after)
    {
        SpikeRecord r = new SpikeRecord();
        r.Index = index;
        r.TimeSeconds = elapsedSeconds;
        r.FrameMs = frameMs;
        r.Fps = frameMs > 0d ? 1000.0d / frameMs : 0d;
        r.MemoryDeltaBytes = memoryDelta;
        r.Gc0Delta = gc0Delta;
        r.Gc1Delta = gc1Delta;
        r.Gc2Delta = gc2Delta;
        r.Before = before;
        r.After = after;

        if (m_spikeCount < m_spikes.Length)
        {
            m_spikes[m_spikeCount++] = r;
            return;
        }

        int lowest = 0;
        double lowestMs = m_spikes[0].FrameMs;
        for (int i = 1; i < m_spikes.Length; i++)
        {
            if (m_spikes[i].FrameMs < lowestMs)
            {
                lowest = i;
                lowestMs = m_spikes[i].FrameMs;
            }
        }

        if (r.FrameMs > lowestMs)
            m_spikes[lowest] = r;
    }

    private void LogInterval(AdvancedFarmingPerfSnapshotService.SampleSnapshot now)
    {
        double avgFrameMs = m_intervalFrames > 0 ? m_intervalFrameMsTotal / (double)m_intervalFrames : 0d;
        double avgFps = avgFrameMs > 0d ? 1000.0d / avgFrameMs : 0d;
        double minFps = m_intervalFrameMsMax > 0d ? 1000.0d / m_intervalFrameMsMax : 0d;
        double maxFps = m_intervalFrameMsMin > 0d ? 1000.0d / m_intervalFrameMsMin : 0d;
        AdvancedFarmingPerfSnapshotService.SampleSnapshot delta = Delta(now, m_intervalSnapshot);
        long memNow = GC.GetTotalMemory(false);
        int gc0 = GC.CollectionCount(0);
        int gc1 = GC.CollectionCount(1);
        int gc2 = GC.CollectionCount(2);

        Log.Out("[SceneContentProfiler] INTERVAL t=" + FormatDouble(m_elapsed)
            + " frames=" + m_intervalFrames
            + " avgFps=" + FormatDouble(avgFps)
            + " minFps=" + FormatDouble(minFps)
            + " maxFps=" + FormatDouble(maxFps)
            + " maxFrameMs=" + FormatDouble(m_intervalFrameMsMax)
            + " spikes=" + m_intervalSpikeCount
            + " severe=" + m_intervalSevereCount
            + " gc0=" + (gc0 - m_intervalGc0)
            + " gc1=" + (gc1 - m_intervalGc1)
            + " gc2=" + (gc2 - m_intervalGc2)
            + " memDeltaKB=" + BytesToKb(memNow - m_intervalMemory)
            + BuildAfDeltaSummary(delta));
        NotifyToolbelt("Scene/farm profiler " + FormatDouble(m_elapsed) + "s/" + m_durationSeconds + "s avg=" + FormatDouble(avgFps) + " min=" + FormatDouble(minFps) + " spikes=" + m_intervalSpikeCount);
    }

    private void LogFinal(AdvancedFarmingPerfSnapshotService.SampleSnapshot end)
    {
        AdvancedFarmingPerfSnapshotService.SampleSnapshot delta = Delta(end, m_startSnapshot);
        long memNow = GC.GetTotalMemory(false);
        int gc0 = GC.CollectionCount(0);
        int gc1 = GC.CollectionCount(1);
        int gc2 = GC.CollectionCount(2);

        Log.Out("[SceneContentProfiler] FINAL elapsed=" + FormatDouble(m_elapsed)
            + " totalSpikes=" + m_totalSpikeCount
            + " severeSpikes=" + m_severeSpikeCount
            + " gc0=" + (gc0 - m_startGc0)
            + " gc1=" + (gc1 - m_startGc1)
            + " gc2=" + (gc2 - m_startGc2)
            + " memDeltaKB=" + BytesToKb(memNow - m_startMemory)
            + BuildAfDeltaSummary(delta));

        SortSpikesDescending();
        int count = m_spikeCount < 12 ? m_spikeCount : 12;
        for (int i = 0; i < count; i++)
        {
            SpikeRecord r = m_spikes[i];
            AdvancedFarmingPerfSnapshotService.SampleSnapshot sd = Delta(r.After, r.Before);
            Log.Out("[SceneContentProfiler] TOP-SPIKE rank=" + (i + 1)
                + " index=" + r.Index
                + " t=" + FormatDouble(r.TimeSeconds)
                + " frameMs=" + FormatDouble(r.FrameMs)
                + " fps=" + FormatDouble(r.Fps)
                + " memDeltaKB=" + BytesToKb(r.MemoryDeltaBytes)
                + " gc0=" + r.Gc0Delta
                + " gc1=" + r.Gc1Delta
                + " gc2=" + r.Gc2Delta
                + BuildAfDeltaSummary(sd));
        }
    }

    private void SortSpikesDescending()
    {
        for (int i = 0; i < m_spikeCount - 1; i++)
        {
            int best = i;
            for (int j = i + 1; j < m_spikeCount; j++)
            {
                if (m_spikes[j].FrameMs > m_spikes[best].FrameMs)
                    best = j;
            }

            if (best != i)
            {
                SpikeRecord tmp = m_spikes[i];
                m_spikes[i] = m_spikes[best];
                m_spikes[best] = tmp;
            }
        }
    }


    private static CensusBlockInfo GetCensusBlockInfo(BlockValue bv)
    {
        CensusBlockInfo info;
        if (s_censusBlockInfoByType.TryGetValue(bv.type, out info))
            return info;

        Block block = bv.Block;
        string name = SafeBlockName(block);
        string lower = name.ToLowerInvariant();
        string typeName = block != null ? block.GetType().Name : string.Empty;

        CensusBlockFlags flags = CensusBlockFlags.None;
        if (IsCropLike(lower, typeName))
            flags |= CensusBlockFlags.Crop;
        if (IsFarmPlotLike(lower, typeName))
            flags |= CensusBlockFlags.FarmPlot;
        if (IsWaterProviderLike(lower, typeName))
            flags |= CensusBlockFlags.Water;
        if (IsHeatSourceLike(lower, typeName))
            flags |= CensusBlockFlags.Heat;
        if (lower.Contains("campfire") || typeName.IndexOf("Campfire", StringComparison.OrdinalIgnoreCase) >= 0)
            flags |= CensusBlockFlags.Campfire;
        if (IsPoweredLightLike(lower, typeName))
            flags |= CensusBlockFlags.PoweredLight;
        if (IsDynamicCoverLike(lower, typeName))
            flags |= CensusBlockFlags.DynamicCover;

        info = new CensusBlockInfo
        {
            Name = name,
            Flags = flags
        };
        s_censusBlockInfoByType[bv.type] = info;
        return info;
    }

    public static SceneCensus BuildSceneCensus(World world, Vector3i center, int radius, int verticalRange)
    {
        SceneCensus c = new SceneCensus();
        c.Center = center;
        c.Radius = radius;
        c.VerticalRange = verticalRange;

        Dictionary<string, int> topBlocks = new Dictionary<string, int>(128);
        int yMin = center.y - verticalRange;
        int yMax = center.y + verticalRange;
        if (yMin < 1)
            yMin = 1;
        if (yMax > 254)
            yMax = 254;

        for (int x = center.x - radius; x <= center.x + radius; x++)
        {
            for (int z = center.z - radius; z <= center.z + radius; z++)
            {
                for (int y = yMin; y <= yMax; y++)
                {
                    c.BlocksScanned++;
                    Vector3i pos = new Vector3i(x, y, z);
                    BlockValue bv = world.GetBlock(pos);
                    if (bv.isair)
                        continue;

                    c.NonAirBlocks++;
                    CensusBlockInfo info = GetCensusBlockInfo(bv);
                    AddTopBlock(topBlocks, info.Name);

                    bool crop = (info.Flags & CensusBlockFlags.Crop) != 0;
                    bool farmPlot = (info.Flags & CensusBlockFlags.FarmPlot) != 0;
                    bool water = (info.Flags & CensusBlockFlags.Water) != 0;
                    bool heat = (info.Flags & CensusBlockFlags.Heat) != 0;
                    bool campfire = (info.Flags & CensusBlockFlags.Campfire) != 0;
                    bool poweredLight = (info.Flags & CensusBlockFlags.PoweredLight) != 0;
                    bool dynamicCover = (info.Flags & CensusBlockFlags.DynamicCover) != 0;

                    if (crop)
                        c.CropBlocks++;
                    if (farmPlot)
                        c.FarmPlots++;
                    if (water)
                        c.WaterTanks++;
                    if (heat)
                        c.HeatSourceBlocks++;
                    if (campfire)
                        c.CampfireBlocks++;
                    if (poweredLight)
                        c.PoweredLightBlocks++;
                    if (dynamicCover)
                        c.DynamicCoverBlocks++;

                    if (crop || farmPlot || water || heat || poweredLight)
                    {
                        c.CandidateTileEntityLookups++;
                        TileEntity te = world.GetTileEntity(pos);
                        if (te != null)
                        {
                            c.CandidateTileEntities++;
                            if (te is TileEntityPlantGrowingRebirth)
                                c.CropTileEntities++;
                            if (te is TileEntityFarmPlotRebirth)
                                c.FarmPlotTileEntities++;
                            if (te is TileEntityWaterTankRebirth)
                                c.WaterTankTileEntities++;
                        }
                    }
                }
            }
        }

        c.TopBlocks = BuildTopBlockString(topBlocks, 12);
        FillUnitySceneCounts(ref c, center, radius);
        return c;
    }

    private static void FillUnitySceneCounts(ref SceneCensus c, Vector3i center, int radius)
    {
        Vector3 centerUnity = new Vector3(center.x + 0.5f, center.y + 0.5f, center.z + 0.5f) - Origin.position;
        float radiusSq = (float)radius * (float)radius;

        Renderer[] renderers = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer r = renderers[i];
            if (r == null)
                continue;
            if (!IsWithinRadius(r.bounds.center, centerUnity, radiusSq))
                continue;

            c.Renderers++;
            if (r.enabled && r.gameObject != null && r.gameObject.activeInHierarchy)
                c.RenderersEnabled++;
            if (r is MeshRenderer)
                c.MeshRenderers++;
            if (r is SkinnedMeshRenderer)
                c.SkinnedRenderers++;
            if (r is ParticleSystemRenderer)
                c.ParticleRenderers++;
        }

        ParticleSystem[] particleSystems = UnityEngine.Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None);
        for (int i = 0; i < particleSystems.Length; i++)
        {
            ParticleSystem ps = particleSystems[i];
            if (ps == null)
                continue;
            if (!IsWithinRadius(ps.transform.position, centerUnity, radiusSq))
                continue;

            c.ParticleSystems++;
            if (ps.isPlaying)
                c.ParticleSystemsPlaying++;
        }

        Light[] lights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
        for (int i = 0; i < lights.Length; i++)
        {
            Light light = lights[i];
            if (light == null)
                continue;

            if (light.type != LightType.Directional && !IsWithinRadius(light.transform.position, centerUnity, radiusSq))
                continue;

            c.Lights++;
            if (light.enabled && light.gameObject != null && light.gameObject.activeInHierarchy)
                c.LightsEnabled++;
            if (light.type == LightType.Point)
                c.PointLights++;
            else if (light.type == LightType.Spot)
                c.SpotLights++;
            else if (light.type == LightType.Directional)
                c.DirectionalLights++;
            if (light.shadows != LightShadows.None)
                c.ShadowCastingLights++;
            if (light.enabled)
            {
                c.LocalLightRangeSum += light.range;
                c.LocalLightIntensitySum += light.intensity;
            }
        }

        LightLOD[] lightLods = UnityEngine.Object.FindObjectsByType<LightLOD>(FindObjectsSortMode.None);
        for (int i = 0; i < lightLods.Length; i++)
        {
            LightLOD lod = lightLods[i];
            if (lod == null)
                continue;
            if (lod.transform != null && !IsWithinRadius(lod.transform.position, centerUnity, radiusSq))
                continue;

            c.LightLods++;
            Light light = lod.GetLight();
            if (light != null)
            {
                if (light.enabled)
                    c.LightLodsEnabled++;
                if (light.shadows != LightShadows.None)
                    c.LightLodsShadowCasting++;
            }

            if (IsPoweredBlockLight(lod))
            {
                c.PoweredLightLods++;
                if (light != null)
                {
                    if (light.enabled)
                        c.PoweredLightLodsEnabled++;
                    if (light.shadows != LightShadows.None)
                        c.PoweredLightLodsShadowCasting++;
                }
            }
        }
    }

    public static void LogCensus(string label, SceneCensus c)
    {
        Log.Out("[SceneContentProfiler] CENSUS label=" + label
            + " center=" + c.Center
            + " radius=" + c.Radius
            + " verticalRange=" + c.VerticalRange
            + " blocksScanned=" + c.BlocksScanned
            + " nonAir=" + c.NonAirBlocks
            + " cropBlocks=" + c.CropBlocks
            + " cropTE=" + c.CropTileEntities
            + " farmPlots=" + c.FarmPlots
            + " farmPlotTE=" + c.FarmPlotTileEntities
            + " waterTanks=" + c.WaterTanks
            + " waterTankTE=" + c.WaterTankTileEntities
            + " heatSources=" + c.HeatSourceBlocks
            + " campfires=" + c.CampfireBlocks
            + " poweredLightBlocks=" + c.PoweredLightBlocks
            + " dynamicCovers=" + c.DynamicCoverBlocks
            + " teLookups=" + c.CandidateTileEntityLookups
            + " candidateTE=" + c.CandidateTileEntities
            + " renderers=" + c.Renderers
            + " renderersEnabled=" + c.RenderersEnabled
            + " meshRenderers=" + c.MeshRenderers
            + " skinnedRenderers=" + c.SkinnedRenderers
            + " particleRenderers=" + c.ParticleRenderers
            + " particleSystems=" + c.ParticleSystems
            + " particlePlaying=" + c.ParticleSystemsPlaying
            + " lights=" + c.Lights
            + " lightsEnabled=" + c.LightsEnabled
            + " pointLights=" + c.PointLights
            + " spotLights=" + c.SpotLights
            + " directionalLights=" + c.DirectionalLights
            + " shadowLights=" + c.ShadowCastingLights
            + " lightLods=" + c.LightLods
            + " lightLodsEnabled=" + c.LightLodsEnabled
            + " lightLodsShadowCasting=" + c.LightLodsShadowCasting
            + " poweredLightLods=" + c.PoweredLightLods
            + " poweredLightLodsEnabled=" + c.PoweredLightLodsEnabled
            + " poweredLightLodsShadowCasting=" + c.PoweredLightLodsShadowCasting
            + " localLightRangeSum=" + FormatDouble(c.LocalLightRangeSum)
            + " localLightIntensitySum=" + FormatDouble(c.LocalLightIntensitySum));
        Log.Out("[SceneContentProfiler] CENSUS-TOP-BLOCKS label=" + label + " " + c.TopBlocks);
    }

    private static bool IsWithinRadius(Vector3 pos, Vector3 center, float radiusSq)
    {
        float dx = pos.x - center.x;
        float dy = pos.y - center.y;
        float dz = pos.z - center.z;
        return dx * dx + dy * dy + dz * dz <= radiusSq;
    }

    private static bool IsPoweredBlockLight(LightLOD lightLod)
    {
        if (lightLod == null || lightLod.bed == null)
            return false;

        Block block = lightLod.bed.blockValue.Block;
        return block is BlockPoweredLight;
    }

    private static bool IsCropLike(string lower, string typeName)
    {
        return lower.Contains("planted")
            || lower.Contains("crop")
            || typeName.IndexOf("PlantGrowing", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool IsFarmPlotLike(string lower, string typeName)
    {
        return lower.Contains("farmplot")
            || lower.Contains("farm_plot")
            || typeName.IndexOf("FarmPlot", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool IsWaterProviderLike(string lower, string typeName)
    {
        return lower.Contains("watertank")
            || lower.Contains("water_tank")
            || lower.Contains("dewcollector")
            || lower.Contains("dew_collector")
            || typeName.IndexOf("WaterTank", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool IsHeatSourceLike(string lower, string typeName)
    {
        return lower.Contains("campfire")
            || lower.Contains("forge")
            || lower.Contains("torch")
            || lower.Contains("candle")
            || lower.Contains("stove")
            || lower.Contains("burning")
            || typeName.IndexOf("Campfire", StringComparison.OrdinalIgnoreCase) >= 0
            || typeName.IndexOf("Workstation", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool IsPoweredLightLike(string lower, string typeName)
    {
        return lower.Contains("poweredlight")
            || lower.Contains("light") && lower.Contains("ceiling")
            || lower.Contains("spotlight")
            || typeName.IndexOf("PoweredLight", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool IsDynamicCoverLike(string lower, string typeName)
    {
        return lower.Contains("shutter")
            || lower.Contains("curtain")
            || lower.Contains("blind")
            || lower.Contains("drape")
            || lower.Contains("hatch")
            || lower.Contains("door")
            || lower.Contains("window")
            || lower.Contains("glass")
            || typeName.IndexOf("Door", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static string SafeBlockName(Block block)
    {
        if (block == null)
            return "<null>";

        try
        {
            string name = block.GetBlockName();
            return string.IsNullOrEmpty(name) ? block.GetType().Name : name;
        }
        catch
        {
            return block.GetType().Name;
        }
    }

    private static void AddTopBlock(Dictionary<string, int> map, string name)
    {
        if (string.IsNullOrEmpty(name))
            name = "<empty>";

        int count;
        if (map.TryGetValue(name, out count))
            map[name] = count + 1;
        else
            map[name] = 1;
    }

    private static string BuildTopBlockString(Dictionary<string, int> map, int max)
    {
        List<KeyValuePair<string, int>> list = new List<KeyValuePair<string, int>>(map);
        list.Sort(delegate (KeyValuePair<string, int> a, KeyValuePair<string, int> b)
        {
            int cmp = b.Value.CompareTo(a.Value);
            if (cmp != 0)
                return cmp;
            return string.Compare(a.Key, b.Key, StringComparison.Ordinal);
        });

        StringBuilder sb = new StringBuilder(512);
        int count = list.Count < max ? list.Count : max;
        for (int i = 0; i < count; i++)
        {
            if (i > 0)
                sb.Append(" | ");
            sb.Append(list[i].Key).Append("=").Append(list[i].Value);
        }

        if (count == 0)
            sb.Append("none");
        return sb.ToString();
    }

    private static AdvancedFarmingPerfSnapshotService.SampleSnapshot Delta(
        AdvancedFarmingPerfSnapshotService.SampleSnapshot a,
        AdvancedFarmingPerfSnapshotService.SampleSnapshot b)
    {
        AdvancedFarmingPerfSnapshotService.SampleSnapshot d = new AdvancedFarmingPerfSnapshotService.SampleSnapshot();
        d.AreaCalls = a.AreaCalls - b.AreaCalls;
        d.TotalElapsedStopwatchTicks = a.TotalElapsedStopwatchTicks - b.TotalElapsedStopwatchTicks;
        d.AreaMaintenanceCalls = a.AreaMaintenanceCalls - b.AreaMaintenanceCalls;
        d.AreaMaintenancePhaseElapsedStopwatchTicks = a.AreaMaintenancePhaseElapsedStopwatchTicks - b.AreaMaintenancePhaseElapsedStopwatchTicks;
        d.PlantSyncs = a.PlantSyncs - b.PlantSyncs;
        d.LightEvaluateCalls = a.LightEvaluateCalls - b.LightEvaluateCalls;
        d.LightEvaluateElapsedStopwatchTicks = a.LightEvaluateElapsedStopwatchTicks - b.LightEvaluateElapsedStopwatchTicks;
        d.ThermalEvaluateCalls = a.ThermalEvaluateCalls - b.ThermalEvaluateCalls;
        d.ThermalEvaluateElapsedStopwatchTicks = a.ThermalEvaluateElapsedStopwatchTicks - b.ThermalEvaluateElapsedStopwatchTicks;
        d.LightExposureSearches = a.LightExposureSearches - b.LightExposureSearches;
        d.ThermalExposureSearches = a.ThermalExposureSearches - b.ThermalExposureSearches;
        d.ThermalExposureNodes = a.ThermalExposureNodes - b.ThermalExposureNodes;
        d.WaterRegistryMaintenanceQueries = a.WaterRegistryMaintenanceQueries - b.WaterRegistryMaintenanceQueries;
        d.WaterRegistryMaintenanceCandidates = a.WaterRegistryMaintenanceCandidates - b.WaterRegistryMaintenanceCandidates;
        d.WaterRegistryMaintenanceTileEntityLookups = a.WaterRegistryMaintenanceTileEntityLookups - b.WaterRegistryMaintenanceTileEntityLookups;
        d.WaterRegistryResolveCacheHits = a.WaterRegistryResolveCacheHits - b.WaterRegistryResolveCacheHits;
        d.HeatRegistryQueries = a.HeatRegistryQueries - b.HeatRegistryQueries;
        d.HeatScanCalls = a.HeatScanCalls - b.HeatScanCalls;
        d.HeatScanPositions = a.HeatScanPositions - b.HeatScanPositions;
        d.SunRefreshQueued = a.SunRefreshQueued - b.SunRefreshQueued;
        d.SunRefreshRebuilds = a.SunRefreshRebuilds - b.SunRefreshRebuilds;
        d.LightProcessorRefreshSunlightCalls = a.LightProcessorRefreshSunlightCalls - b.LightProcessorRefreshSunlightCalls;
        d.LightProcessorRefreshLocalSunCalls = a.LightProcessorRefreshLocalSunCalls - b.LightProcessorRefreshLocalSunCalls;
        d.LightProcessorRefreshLocalBlockCalls = a.LightProcessorRefreshLocalBlockCalls - b.LightProcessorRefreshLocalBlockCalls;
        d.LightProcessorSpreadSunCalls = a.LightProcessorSpreadSunCalls - b.LightProcessorSpreadSunCalls;
        d.LightProcessorSpreadBlockCalls = a.LightProcessorSpreadBlockCalls - b.LightProcessorSpreadBlockCalls;
        d.ProcessAreaManagedPositiveBytes = a.ProcessAreaManagedPositiveBytes - b.ProcessAreaManagedPositiveBytes;
        d.AreaMaintenanceManagedPositiveBytes = a.AreaMaintenanceManagedPositiveBytes - b.AreaMaintenanceManagedPositiveBytes;
        d.LightEvaluateManagedPositiveBytes = a.LightEvaluateManagedPositiveBytes - b.LightEvaluateManagedPositiveBytes;
        d.ThermalEvaluateManagedPositiveBytes = a.ThermalEvaluateManagedPositiveBytes - b.ThermalEvaluateManagedPositiveBytes;
        d.WaterMaintenanceManagedPositiveBytes = a.WaterMaintenanceManagedPositiveBytes - b.WaterMaintenanceManagedPositiveBytes;
        return d;
    }

    private static string BuildAfDeltaSummary(AdvancedFarmingPerfSnapshotService.SampleSnapshot d)
    {
        return " areaCalls=" + d.AreaCalls
            + " areaMs=" + FormatDouble(TicksToMs(d.TotalElapsedStopwatchTicks))
            + " maintCalls=" + d.AreaMaintenanceCalls
            + " maintMs=" + FormatDouble(TicksToMs(d.AreaMaintenancePhaseElapsedStopwatchTicks))
            + " plantSyncs=" + d.PlantSyncs
            + " lightEvalCalls=" + d.LightEvaluateCalls
            + " lightEvalMs=" + FormatDouble(TicksToMs(d.LightEvaluateElapsedStopwatchTicks))
            + " thermalEvalCalls=" + d.ThermalEvaluateCalls
            + " thermalEvalMs=" + FormatDouble(TicksToMs(d.ThermalEvaluateElapsedStopwatchTicks))
            + " lightSearches=" + d.LightExposureSearches
            + " thermalSearches=" + d.ThermalExposureSearches
            + " thermalNodes=" + d.ThermalExposureNodes
            + " waterMaintQ=" + d.WaterRegistryMaintenanceQueries
            + " waterMaintCand=" + d.WaterRegistryMaintenanceCandidates
            + " waterMaintTE=" + d.WaterRegistryMaintenanceTileEntityLookups
            + " waterResolveHits=" + d.WaterRegistryResolveCacheHits
            + " heatRegistryQ=" + d.HeatRegistryQueries
            + " heatScanCalls=" + d.HeatScanCalls
            + " heatScanPos=" + d.HeatScanPositions
            + " sunQueued=" + d.SunRefreshQueued
            + " sunRebuilds=" + d.SunRefreshRebuilds
            + " lpSunRefresh=" + d.LightProcessorRefreshSunlightCalls
            + " lpLocalSun=" + d.LightProcessorRefreshLocalSunCalls
            + " lpLocalBlock=" + d.LightProcessorRefreshLocalBlockCalls
            + " lpSpreadSun=" + d.LightProcessorSpreadSunCalls
            + " lpSpreadBlock=" + d.LightProcessorSpreadBlockCalls
            + " managedAreaKB=" + BytesToKb(d.ProcessAreaManagedPositiveBytes)
            + " managedMaintKB=" + BytesToKb(d.AreaMaintenanceManagedPositiveBytes)
            + " managedLightKB=" + BytesToKb(d.LightEvaluateManagedPositiveBytes)
            + " managedThermalKB=" + BytesToKb(d.ThermalEvaluateManagedPositiveBytes)
            + " managedWaterMaintKB=" + BytesToKb(d.WaterMaintenanceManagedPositiveBytes);
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

    private static long BytesToKb(long bytes)
    {
        return bytes / 1024L;
    }
}
#endif

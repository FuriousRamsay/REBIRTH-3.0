#if DEBUG
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

#nullable disable

/// <summary>
/// Read-only paired client/server crop-growth diagnostic.
///
/// A client-side console command selects all nearby immature Advanced Farming crops, starts a
/// local sampling session, and sends the same positions to the authoritative server. Both peers
/// then log comparable snapshots from the main-thread GameUpdate pump. No crop, timer, water,
/// light, temperature, schedule, or tile-entity state is changed by this service.
/// </summary>
public static class AdvancedFarmingCropGrowthDiagnosticService
{
    public const int DefaultRadius = 8;
    public const int DefaultDurationSeconds = 60;
    public const int DefaultIntervalSeconds = 10;
    public const int MaxCrops = 64;

    private const int MinRadius = 1;
    private const int MaxRadius = 16;
    private const int MinDurationSeconds = 20;
    private const int MaxDurationSeconds = 180;
    private const int MinIntervalSeconds = 5;
    private const int MaxIntervalSeconds = 30;

    private sealed class CropState
    {
        public string InitialBlockName;
        public string PreviousBlockName;
        public ulong InitialAccumulatedTicks;
        public ulong PreviousAccumulatedTicks;
        public ulong InitialGameTimerTicks;
        public ulong PreviousGameTimerTicks;
        public int InitialLastProcessedSeconds;
        public int PreviousLastProcessedSeconds;
        public string PreviousHoverText;
        public bool EverBlockChanged;
        public bool EverAccumulatedChanged;
        public bool EverGameTimerChanged;
        public bool EverLastProcessedChanged;
        public bool EverHoverChanged;
    }

    private sealed class Session
    {
        public World World;
        public EntityPlayer Requester;
        public string Id;
        public string Role;
        public int RequestingEntityId;
        public Vector3i Center;
        public int Radius;
        public int DurationSeconds;
        public int IntervalSeconds;
        public float StartedRealtime;
        public float NextSampleRealtime;
        public int SampleIndex;
        public Vector3i[] CropPositions;
        public readonly Dictionary<Vector3i, CropState> CropStates = new Dictionary<Vector3i, CropState>();
    }

    private static readonly Dictionary<string, Session> s_sessions = new Dictionary<string, Session>(StringComparer.Ordinal);
    private static readonly List<string> s_finishedSessionIds = new List<string>(4);
    private static readonly Dictionary<string,float> s_lastStarts=new Dictionary<string,float>(StringComparer.Ordinal);
    public static long AdmissionRevision {get;private set;}

    public static int ClampRadius(int value)
    {
        if (value < MinRadius) return MinRadius;
        if (value > MaxRadius) return MaxRadius;
        return value;
    }

    public static int ClampDurationSeconds(int value)
    {
        if (value < MinDurationSeconds) return MinDurationSeconds;
        if (value > MaxDurationSeconds) return MaxDurationSeconds;
        return value;
    }

    public static int ClampIntervalSeconds(int value)
    {
        if (value < MinIntervalSeconds) return MinIntervalSeconds;
        if (value > MaxIntervalSeconds) return MaxIntervalSeconds;
        return value;
    }

    public static Vector3i[] FindNearbyImmatureCrops(World world, Vector3i center, int radius)
    {
        if (world == null)
            return new Vector3i[0];

        radius = ClampRadius(radius);
        List<CropCandidate> candidates = new List<CropCandidate>(32);

        for (int x = center.x - radius; x <= center.x + radius; x++)
        {
            for (int z = center.z - radius; z <= center.z + radius; z++)
            {
                for (int y = center.y - 5; y <= center.y + 6; y++)
                {
                    Vector3i pos = new Vector3i(x, y, z);
                    BlockValue blockValue = world.GetBlock(pos);
                    BlockPlantGrowingRebirth plant = blockValue.Block as BlockPlantGrowingRebirth;
                    if (plant == null || plant.IsFullyGrownCrop(blockValue))
                        continue;

                    int dx = pos.x - center.x;
                    int dy = pos.y - center.y;
                    int dz = pos.z - center.z;
                    candidates.Add(new CropCandidate(pos, dx * dx + dy * dy + dz * dz));
                }
            }
        }

        candidates.Sort(CropCandidateComparer.Instance);
        int count = Math.Min(MaxCrops, candidates.Count);
        Vector3i[] result = new Vector3i[count];
        for (int i = 0; i < count; i++)
            result[i] = candidates[i].Pos;
        return result;
    }

    public static string Start(
        World world,
        string sessionId,
        string role,
        int requestingEntityId,
        Vector3i center,
        int radius,
        int durationSeconds,
        int intervalSeconds,
        Vector3i[] cropPositions)
    {
        if (!ThreadManager.IsMainThread()||world == null||!ReferenceEquals(world,GameManager.Instance?.World)||requestingEntityId<=0)
            return "[AdvancedFarming GrowthTest] start rejected: world=null";
        var requester=world.GetEntity(requestingEntityId) as EntityPlayer;
        if(requester==null||requester.IsDead())return "[AdvancedFarming GrowthTest] start rejected: requester unavailable";

        if (string.IsNullOrEmpty(sessionId))
            sessionId = BuildSessionId(requestingEntityId);
        if(sessionId.Length>128)return "[AdvancedFarming GrowthTest] start rejected: label too long";
        foreach(char c in sessionId)if(!char.IsLetterOrDigit(c)&&c!='-'&&c!='_'&&c!='.')return "[AdvancedFarming GrowthTest] start rejected: invalid label";
        if(role!="CLIENT"&&role!="HOST"&&role!="SERVER")return "[AdvancedFarming GrowthTest] start rejected: invalid role";
        string requesterKey=requestingEntityId.ToString()+"|"+role;float now=Time.realtimeSinceStartup;
        if(s_lastStarts.TryGetValue(requesterKey,out var last)&&now-last<1f)return "[AdvancedFarming GrowthTest] start rejected: cooldown";
        if(s_lastStarts.Count>=256)foreach(var old in new List<string>(s_lastStarts.Keys))if(now-s_lastStarts[old]>=1f)s_lastStarts.Remove(old);
        if(!s_lastStarts.ContainsKey(requesterKey)&&s_lastStarts.Count>=256)return "[AdvancedFarming GrowthTest] start rejected: requester capacity";
        s_lastStarts[requesterKey]=now;

        radius = ClampRadius(radius);
        durationSeconds = ClampDurationSeconds(durationSeconds);
        intervalSeconds = ClampIntervalSeconds(intervalSeconds);
        cropPositions = SanitizePositions(center, radius, cropPositions);

        s_finishedSessionIds.Clear();
        foreach (KeyValuePair<string, Session> pair in s_sessions)
        {
            Session active = pair.Value;
            if (active.RequestingEntityId == requestingEntityId && string.Equals(active.Role, role, StringComparison.Ordinal))
                s_finishedSessionIds.Add(pair.Key);
        }
        for (int i = 0; i < s_finishedSessionIds.Count; i++)
        {
            Log.Out("[AdvancedFarming GrowthTest] replacing active session key=" + s_finishedSessionIds[i]);
            s_sessions.Remove(s_finishedSessionIds[i]);
        }
        s_finishedSessionIds.Clear();

        Session session = new Session
        {
            World=world,
            Requester=requester,
            Id = sessionId,
            Role = string.IsNullOrEmpty(role) ? "UNKNOWN" : role,
            RequestingEntityId = requestingEntityId,
            Center = center,
            Radius = radius,
            DurationSeconds = durationSeconds,
            IntervalSeconds = intervalSeconds,
            StartedRealtime = Time.realtimeSinceStartup,
            SampleIndex = 0,
            CropPositions = cropPositions
        };
        session.NextSampleRealtime = session.StartedRealtime + intervalSeconds;

        s_sessions[MakeSessionKey(session.Id, session.Role,requestingEntityId)] = session;
        AdmissionRevision++;

        Log.Out("[AdvancedFarming GrowthTest] START"
            + " session=" + session.Id
            + " role=" + session.Role
            + " requesterEntityId=" + requestingEntityId
            + " center=" + center
            + " radius=" + radius
            + " durationSeconds=" + durationSeconds
            + " intervalSeconds=" + intervalSeconds
            + " cropCount=" + cropPositions.Length
            + " worldRemote=" + world.IsRemote()
            + " advancedEnabled=" + AdvancedFarmingRuntimePolicy.Enabled
            + " growthSeconds=" + AdvancedFarmingRuntimePolicy.GetEffectiveGrowthSeconds()
            + " minimumNaturalSunlight=" + AdvancedFarmingRuntimePolicy.MinimumNaturalSunlight
            + " policyVersion=" + AdvancedFarmingRuntimePolicy.PolicyVersion);

        LogSample(world, session, 0f, false);

        if (ShouldNotifyLocalPlayer(session.Role))
            NotifyToolbelt("Crop growth test started for " + durationSeconds + " seconds. Stay near the crops; you do not need to keep aiming at them.");

        return "[AdvancedFarming GrowthTest] session=" + session.Id
            + " role=" + session.Role
            + " cropCount=" + cropPositions.Length
            + " started=true";
    }

    public static bool IsActive(string sessionId,string role,int requester)
    {
        return s_sessions.TryGetValue(MakeSessionKey(sessionId,role,requester),out var session)&&session.RequestingEntityId==requester&&
            ReferenceEquals(session.World,GameManager.Instance?.World)&&ReferenceEquals(session.Requester,session.World.GetEntity(requester))&&!session.Requester.IsDead();
    }
    public static void Pump(World world)
    {
        if (!ThreadManager.IsMainThread()||world == null || s_sessions.Count == 0)
            return;

        float now = Time.realtimeSinceStartup;
        s_finishedSessionIds.Clear();

        foreach (KeyValuePair<string, Session> pair in s_sessions)
        {
            Session session = pair.Value;
            if(!ReferenceEquals(session.World,world)||!ReferenceEquals(session.Requester,world.GetEntity(session.RequestingEntityId))||session.Requester.IsDead())
            {s_finishedSessionIds.Add(pair.Key);continue;}
            float elapsed = now - session.StartedRealtime;
            bool complete = elapsed >= session.DurationSeconds;

            if (now >= session.NextSampleRealtime || complete)
            {
                if (elapsed > session.DurationSeconds)
                    elapsed = session.DurationSeconds;

                session.SampleIndex++;
                LogSample(world, session, elapsed, complete);

                do
                {
                    session.NextSampleRealtime += session.IntervalSeconds;
                }
                while (session.NextSampleRealtime <= now);
            }

            if (complete)
            {
                LogCompletion(session);
                s_finishedSessionIds.Add(pair.Key);
            }
        }

        for (int i = 0; i < s_finishedSessionIds.Count; i++)
            s_sessions.Remove(s_finishedSessionIds[i]);
    }

    public static void Reset(string reason)
    {
        if (s_sessions.Count > 0)
            Log.Out("[AdvancedFarming GrowthTest] RESET activeSessions=" + s_sessions.Count + " reason=" + (reason ?? string.Empty));
        s_sessions.Clear();
        s_finishedSessionIds.Clear();
        s_lastStarts.Clear();
    }

    public static string BuildSessionId(int entityId)
    {
        return DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + "-" + entityId;
    }

    private static void LogSample(World world, Session session, float elapsedSeconds, bool finalSample)
    {
        AdvancedFarmingHoverTextService.ClearConditionCache();
        AdvancedFarmingLightService.ClearCache();
        AdvancedFarmingTemperatureService.ClearCache();

        int present = 0;
        int missing = 0;
        int eligible = 0;
        int blocked = 0;
        int noTileEntity = 0;
        int accumulatedChanged = 0;
        int hoverChanged = 0;
        int expectedBlockedButHoverNoReason = 0;
        int expectedEligibleButDisplayFrozen = 0;

        for (int i = 0; i < session.CropPositions.Length; i++)
        {
            Vector3i pos = session.CropPositions[i];
            CropSnapshot snapshot = BuildCropSnapshot(world, session, pos);
            if (!snapshot.Present)
            {
                missing++;
                Log.Out("[AdvancedFarming GrowthTest Crop]"
                    + " session=" + session.Id
                    + " role=" + session.Role
                    + " sample=" + session.SampleIndex
                    + " elapsedRealSeconds=" + elapsedSeconds.ToString("0.0")
                    + " pos=" + pos
                    + " present=false"
                    + " block=" + snapshot.BlockName);
                continue;
            }

            present++;
            if (!snapshot.HasTileEntity)
                noTileEntity++;
            if (snapshot.Eligible)
                eligible++;
            else
                blocked++;
            if (snapshot.AccumulatedChanged)
                accumulatedChanged++;
            if (snapshot.HoverChanged)
                hoverChanged++;
            if (snapshot.ExpectedBlocked && !snapshot.HoverShowsAnyBlockingReason)
                expectedBlockedButHoverNoReason++;
            if (session.SampleIndex > 0
                && snapshot.Eligible
                && !snapshot.BlockChangedSincePrevious
                && !snapshot.AccumulatedChangedSincePrevious
                && snapshot.GameTimerDeltaFromPrevious == 0L
                && snapshot.LastProcessedDeltaFromPrevious == 0
                && !snapshot.HoverChanged)
                expectedEligibleButDisplayFrozen++;

            Log.Out("[AdvancedFarming GrowthTest Crop]"
                + " session=" + session.Id
                + " role=" + session.Role
                + " sample=" + session.SampleIndex
                + " elapsedRealSeconds=" + elapsedSeconds.ToString("0.0")
                + " final=" + finalSample
                + " pos=" + pos
                + " block=" + snapshot.BlockName
                + " blockType=" + snapshot.BlockType
                + " blockClass=" + snapshot.BlockClass
                + " initialBlock=" + snapshot.InitialBlockName
                + " blockChanged=" + snapshot.BlockChanged
                + " blockChangedSincePrevious=" + snapshot.BlockChangedSincePrevious
                + " stage=" + snapshot.GrowthStage
                + " present=true"
                + " tileEntity=" + snapshot.HasTileEntity
                + " tileEntityRuntimeType=" + snapshot.TileEntityRuntimeType
                + " accumulatedTicks=" + snapshot.AccumulatedTicks
                + " accumulatedDeltaFromStart=" + snapshot.AccumulatedDeltaFromStart
                + " accumulatedDeltaFromPrevious=" + snapshot.AccumulatedDeltaFromPrevious
                + " gameTimerTicks=" + snapshot.GameTimerTicks
                + " gameTimerDeltaFromStart=" + snapshot.GameTimerDeltaFromStart
                + " gameTimerDeltaFromPrevious=" + snapshot.GameTimerDeltaFromPrevious
                + " lastProcessedSeconds=" + snapshot.LastProcessedSeconds
                + " lastProcessedDeltaFromStart=" + snapshot.LastProcessedDeltaFromStart
                + " lastProcessedDeltaFromPrevious=" + snapshot.LastProcessedDeltaFromPrevious
                + " nowTicks=" + snapshot.NowTicks
                + " nowWorldSeconds=" + snapshot.NowWorldSeconds
                + " watered=" + snapshot.Watered
                + " stageRateTicks=" + snapshot.StageRateTicks
                + " predictedDisplayAccumulatedTicks=" + snapshot.PredictedDisplayAccumulatedTicks
                + " predictedDisplayRemainingSeconds=" + snapshot.PredictedDisplayRemainingSeconds
                + " currentRuleMushroom=" + snapshot.IsMushroom
                + " lightPass=" + snapshot.LightPass
                + " localLightPass=" + snapshot.LocalLightPass
                + " authoritativeLightState=" + snapshot.AuthoritativeLightState
                + " sunRaw=" + snapshot.SunRaw
                + " blockRaw=" + snapshot.BlockRaw
                + " cropSun=" + snapshot.CropSun
                + " requiredLight=" + snapshot.RequiredLight
                + " requiredNaturalSunlight=" + snapshot.RequiredNaturalSunlight
                + " hasSun=" + snapshot.HasSun
                + " hasOpenSky=" + snapshot.HasOpenSky
                + " hasBlockLight=" + snapshot.HasBlockLight
                + " isNight=" + snapshot.IsNight
                + " baseTemperature=" + snapshot.BaseTemperature.ToString("0.00")
                + " temperatureSource=" + snapshot.BaseTemperatureSource
                + " enclosedAdjustment=" + snapshot.EnclosedBiomeAdjustment.ToString("0.00")
                + " heatBonus=" + snapshot.HeatBonus.ToString("0.00")
                + " temperature=" + snapshot.Temperature.ToString("0.00")
                + " minTemperature=" + snapshot.MinTemperature
                + " temperaturePass=" + snapshot.TemperaturePass
                + " biome=" + snapshot.Biome
                + " biomePass=" + snapshot.BiomePass
                + " depletion=" + snapshot.WaterDepletion
                + " waterPass=" + snapshot.WaterPass
                + " tankWater=" + snapshot.TankWater
                + " collectorWater=" + snapshot.CollectorWater
                + " plotWater=" + snapshot.PlotWater
                + " eligible=" + snapshot.Eligible
                + " blockerMask=" + snapshot.BlockerMask
                + " hoverShowsTimer=" + snapshot.HoverShowsTimer
                + " hoverShowsBlockingReason=" + snapshot.HoverShowsAnyBlockingReason
                + " hoverChanged=" + snapshot.HoverChanged
                + " hover=\"" + EscapeForLog(snapshot.HoverText) + "\"");
        }

        Log.Out("[AdvancedFarming GrowthTest Sample]"
            + " session=" + session.Id
            + " role=" + session.Role
            + " sample=" + session.SampleIndex
            + " elapsedRealSeconds=" + elapsedSeconds.ToString("0.0")
            + " final=" + finalSample
            + " requestedCrops=" + session.CropPositions.Length
            + " present=" + present
            + " missing=" + missing
            + " noTileEntity=" + noTileEntity
            + " eligible=" + eligible
            + " blocked=" + blocked
            + " accumulatedChangedFromStart=" + accumulatedChanged
            + " hoverChangedThisSample=" + hoverChanged
            + " expectedBlockedButHoverNoReason=" + expectedBlockedButHoverNoReason
            + " eligibleNoStateOrHoverChangeThisInterval=" + expectedEligibleButDisplayFrozen);
    }

    private static CropSnapshot BuildCropSnapshot(World world, Session session, Vector3i pos)
    {
        CropSnapshot result = new CropSnapshot();
        BlockValue blockValue = world.GetBlock(pos);
        BlockPlantGrowingRebirth plant = blockValue.Block as BlockPlantGrowingRebirth;
        result.BlockName = blockValue.Block != null ? blockValue.Block.GetBlockName() : "<null>";
        result.BlockType = blockValue.type;
        result.BlockClass = blockValue.Block != null ? blockValue.Block.GetType().FullName : "<null>";
        result.Present = plant != null;
        if (!result.Present)
            return result;

        result.GrowthStage = GetGrowthStage(result.BlockName);
        result.IsMushroom = result.BlockName.IndexOf("mushroom", StringComparison.OrdinalIgnoreCase) >= 0;
        result.IsNight = !world.IsDaytime();
        result.MinTemperature = plant.DiagnosticMinTemperature;
        result.NowTicks = GameTimer.Instance.ticks;
        result.NowWorldSeconds = RebirthUtilities.TotalGameSecondsPassed();
        result.StageRateTicks = (ulong)Math.Max(1, AdvancedFarmingRuntimePolicy.GetEffectiveGrowthSeconds() * 20);

        TileEntity rawTileEntity = world.GetTileEntity(pos);
        TileEntityPlantGrowingRebirth te = rawTileEntity as TileEntityPlantGrowingRebirth;
        result.TileEntityRuntimeType = rawTileEntity != null ? rawTileEntity.GetType().FullName : "<null>";
        result.HasTileEntity = te != null;
        if (te != null)
        {
            result.AccumulatedTicks = te.AccumulatedTicks;
            result.GameTimerTicks = te.GameTimerTicks;
            result.LastProcessedSeconds = te.LastProcessedWorldSeconds;
            result.Watered = te.bWatered;
        }

        AdvancedFarmingLightService.LightResult light = AdvancedFarmingLightService.Evaluate(
            world,
            pos,
            plant.DiagnosticLightLevelStay,
            plant.DiagnosticLightLevelGrow,
            result.IsMushroom,
            AdvancedFarmingRuntimePolicy.Enabled);

        result.SunRaw = light.SunLight;
        result.BlockRaw = light.BlockLight;
        result.CropSun = light.CropSunLight;
        result.RequiredLight = light.RequiredLightLevel;
        result.RequiredNaturalSunlight = light.RequiredNaturalSunlight;
        result.HasSun = light.HasSunLight;
        result.HasOpenSky = light.HasOpenSky;
        result.HasBlockLight = light.HasBlockLight;
        result.LocalLightPass = result.IsMushroom || (((result.HasSun || result.HasOpenSky) && !result.IsNight) || result.HasBlockLight);
        result.AuthoritativeLightState = te != null
            ? te.AuthoritativeLightState.ToString()
            : AdvancedFarmingAuthoritativeLightState.Unknown.ToString();
        result.LightPass = result.LocalLightPass;
        if (world.IsRemote() && !result.IsMushroom)
        {
            result.LightPass = te != null
                && te.AuthoritativeLightState == AdvancedFarmingAuthoritativeLightState.Pass;
        }

        AdvancedFarmingTemperatureService.TemperatureResult temp = AdvancedFarmingTemperatureService.Evaluate(
            world,
            pos,
            result.HasSun || result.HasOpenSky,
            AdvancedFarmingRuntimePolicy.Enabled);
        result.BaseTemperature = temp.BaseTemperature;
        result.BaseTemperatureSource = temp.BaseTemperatureSource ?? string.Empty;
        result.EnclosedBiomeAdjustment = temp.EnclosedBiomeAdjustment;
        result.HeatBonus = temp.HeatBonus;
        result.Temperature = temp.Temperature;
        result.TemperaturePass = !AdvancedFarmingRuntimePolicy.Enabled || result.IsMushroom || result.Temperature >= result.MinTemperature;
        result.Biome = temp.BiomeName ?? string.Empty;
        result.BiomePass = result.IsMushroom || !string.Equals(result.Biome, "wasteland", StringComparison.OrdinalIgnoreCase);

        result.WaterDepletion = RebirthUtilities.GetCropWaterDepletion(blockValue, result.Temperature);
        result.TankWater = -1;
        result.CollectorWater = -1;
        result.PlotWater = 0;
        if (!result.IsMushroom)
        {
            result.TankWater = RebirthUtilities.CheckForWater(world, pos, "FuriousRamsayWaterTank", 4, 1, 0);
            result.CollectorWater = RebirthUtilities.CheckForWater(world, pos, "FuriousRamsayDewCollector", 4, 1, 0);
            TileEntityFarmPlotRebirth plot = world.GetTileEntity(pos + Vector3i.down) as TileEntityFarmPlotRebirth;
            if (plot != null)
                result.PlotWater = plot.waterCount;
        }
        result.WaterPass = result.IsMushroom || result.Watered
            || Math.Max(result.TankWater, result.CollectorWater) >= result.WaterDepletion
            || result.PlotWater >= result.WaterDepletion;

        result.Eligible = result.HasTileEntity
            && !plant.IsFullyGrownCrop(blockValue)
            && (!AdvancedFarmingRuntimePolicy.Enabled || (result.LightPass && result.TemperaturePass && result.BiomePass && result.WaterPass));
        result.ExpectedBlocked = AdvancedFarmingRuntimePolicy.Enabled && !result.Eligible;
        result.BlockerMask = BuildBlockerMask(result);

        ulong displayAccumulated = AdvancedFarmingHoverTextService.GetPredictedDisplayAccumulatedTicksForDiagnostic(
            world,
            pos,
            blockValue.type,
            te,
            result.StageRateTicks,
            !result.Eligible);
        result.PredictedDisplayAccumulatedTicks = displayAccumulated;
        ulong remainingTicks = displayAccumulated >= result.StageRateTicks ? 0UL : result.StageRateTicks - displayAccumulated;
        result.PredictedDisplayRemainingSeconds = (remainingTicks + 19UL) / 20UL;

        EntityAlive focusingEntity = world.GetPrimaryPlayer();
        try
        {
            result.HoverText = plant.GetActivationText(world, blockValue, pos, focusingEntity) ?? string.Empty;
        }
        catch (Exception ex)
        {
            result.HoverText = "<exception:" + ex.GetType().Name + ":" + ex.Message + ">";
        }
        result.HoverShowsTimer = ContainsLocalized(result.HoverText, result.GrowthStage == 2 ? "ttNextCycle2" : "ttNextCycle1");
        result.HoverShowsAnyBlockingReason = ContainsLocalized(result.HoverText, "ttNeedLight")
            || ContainsLocalized(result.HoverText, "ttNeedWater")
            || ContainsLocalized(result.HoverText, "ttNoWaterInTank")
            || ContainsLocalized(result.HoverText, "ttNoGrowWasteland")
            || ContainsLocalized(result.HoverText, "ttNoGrowTooCold");

        if (!session.CropStates.TryGetValue(pos, out CropState state))
        {
            state = new CropState
            {
                InitialBlockName = result.BlockName,
                PreviousBlockName = result.BlockName,
                InitialAccumulatedTicks = result.AccumulatedTicks,
                PreviousAccumulatedTicks = result.AccumulatedTicks,
                InitialGameTimerTicks = result.GameTimerTicks,
                PreviousGameTimerTicks = result.GameTimerTicks,
                InitialLastProcessedSeconds = result.LastProcessedSeconds,
                PreviousLastProcessedSeconds = result.LastProcessedSeconds,
                PreviousHoverText = result.HoverText
            };
            session.CropStates[pos] = state;
        }

        result.InitialBlockName = state.InitialBlockName;
        result.BlockChanged = !string.Equals(result.BlockName, state.InitialBlockName, StringComparison.Ordinal);
        result.BlockChangedSincePrevious = !string.Equals(result.BlockName, state.PreviousBlockName, StringComparison.Ordinal);
        result.AccumulatedDeltaFromStart = SignedDelta(result.AccumulatedTicks, state.InitialAccumulatedTicks);
        result.AccumulatedDeltaFromPrevious = SignedDelta(result.AccumulatedTicks, state.PreviousAccumulatedTicks);
        result.GameTimerDeltaFromStart = SignedDelta(result.GameTimerTicks, state.InitialGameTimerTicks);
        result.GameTimerDeltaFromPrevious = SignedDelta(result.GameTimerTicks, state.PreviousGameTimerTicks);
        result.LastProcessedDeltaFromStart = result.LastProcessedSeconds - state.InitialLastProcessedSeconds;
        result.LastProcessedDeltaFromPrevious = result.LastProcessedSeconds - state.PreviousLastProcessedSeconds;
        result.HoverChanged = !string.Equals(result.HoverText, state.PreviousHoverText, StringComparison.Ordinal);
        result.AccumulatedChanged = result.AccumulatedTicks != state.InitialAccumulatedTicks;
        result.AccumulatedChangedSincePrevious = result.AccumulatedTicks != state.PreviousAccumulatedTicks;

        state.EverBlockChanged |= result.BlockChanged;
        state.EverAccumulatedChanged |= result.AccumulatedChanged;
        state.EverGameTimerChanged |= result.GameTimerTicks != state.InitialGameTimerTicks;
        state.EverLastProcessedChanged |= result.LastProcessedSeconds != state.InitialLastProcessedSeconds;
        state.EverHoverChanged |= !string.Equals(result.HoverText, state.PreviousHoverText, StringComparison.Ordinal);
        state.PreviousBlockName = result.BlockName;
        state.PreviousAccumulatedTicks = result.AccumulatedTicks;
        state.PreviousGameTimerTicks = result.GameTimerTicks;
        state.PreviousLastProcessedSeconds = result.LastProcessedSeconds;
        state.PreviousHoverText = result.HoverText;

        return result;
    }

    private static void LogCompletion(Session session)
    {
        int blockChanged = 0;
        int accumulatedChanged = 0;
        int gameTimerChanged = 0;
        int lastProcessedChanged = 0;
        int hoverChanged = 0;

        foreach (CropState state in session.CropStates.Values)
        {
            if (state.EverBlockChanged) blockChanged++;
            if (state.EverAccumulatedChanged) accumulatedChanged++;
            if (state.EverGameTimerChanged) gameTimerChanged++;
            if (state.EverLastProcessedChanged) lastProcessedChanged++;
            if (state.EverHoverChanged) hoverChanged++;
        }

        Log.Out("[AdvancedFarming GrowthTest] COMPLETE"
            + " session=" + session.Id
            + " role=" + session.Role
            + " durationSeconds=" + session.DurationSeconds
            + " requestedCrops=" + session.CropPositions.Length
            + " trackedCrops=" + session.CropStates.Count
            + " blockChanged=" + blockChanged
            + " accumulatedChanged=" + accumulatedChanged
            + " gameTimerChanged=" + gameTimerChanged
            + " lastProcessedChanged=" + lastProcessedChanged
            + " hoverChanged=" + hoverChanged
            + " instruction=Upload_the_matching_client_and_dedicated_server_logs");

        if (ShouldNotifyLocalPlayer(session.Role))
            NotifyToolbelt("Crop growth test complete. Send the matching client and dedicated-server logs.");
    }

    private static Vector3i[] SanitizePositions(Vector3i center, int radius, Vector3i[] positions)
    {
        if (positions == null || positions.Length == 0)
            return new Vector3i[0];

        List<Vector3i> result = new List<Vector3i>(Math.Min(MaxCrops, positions.Length));
        HashSet<Vector3i> seen = new HashSet<Vector3i>();
        int maxHorizontal = radius + 2;

        for (int i = 0; i < positions.Length && result.Count < MaxCrops; i++)
        {
            Vector3i pos = positions[i];
            if (Math.Abs(pos.x - center.x) > maxHorizontal
                || Math.Abs(pos.z - center.z) > maxHorizontal
                || Math.Abs(pos.y - center.y) > 8)
                continue;

            if (seen.Add(pos))
                result.Add(pos);
        }

        return result.ToArray();
    }

    private static bool ShouldNotifyLocalPlayer(string role)
    {
        // Remote clients use CLIENT. Single-player/P2P host sessions use HOST and also create a
        // SERVER sampling session in the same process; notify only HOST to avoid duplicate popups.
        return string.Equals(role, "CLIENT", StringComparison.Ordinal)
            || string.Equals(role, "HOST", StringComparison.Ordinal);
    }

    private static string MakeSessionKey(string sessionId, string role,int requester)
    {
        return requester.ToString()+"|"+(sessionId ?? string.Empty) + "|" + (role ?? string.Empty);
    }

    private static long SignedDelta(ulong current, ulong baseline)
    {
        if (current >= baseline)
        {
            ulong delta = current - baseline;
            return delta > long.MaxValue ? long.MaxValue : (long)delta;
        }

        ulong negative = baseline - current;
        return negative > (ulong)long.MaxValue ? long.MinValue : -(long)negative;
    }

    private static int GetGrowthStage(string blockName)
    {
        if (string.IsNullOrEmpty(blockName))
            return 0;
        if (blockName.IndexOf("3Harvest", StringComparison.OrdinalIgnoreCase) >= 0)
            return 3;
        if (blockName.EndsWith("2", StringComparison.OrdinalIgnoreCase))
            return 2;
        if (blockName.EndsWith("1", StringComparison.OrdinalIgnoreCase))
            return 1;
        return 0;
    }

    private static string BuildBlockerMask(CropSnapshot snapshot)
    {
        StringBuilder sb = new StringBuilder(32);
        if (!snapshot.LightPass) AppendMask(sb, "LIGHT");
        if (!snapshot.WaterPass) AppendMask(sb, "WATER");
        if (!snapshot.BiomePass) AppendMask(sb, "BIOME");
        if (!snapshot.TemperaturePass) AppendMask(sb, "TEMP");
        if (!snapshot.HasTileEntity) AppendMask(sb, "NO_TE");
        return sb.Length == 0 ? "NONE" : sb.ToString();
    }

    private static void AppendMask(StringBuilder sb, string value)
    {
        if (sb.Length > 0)
            sb.Append('|');
        sb.Append(value);
    }

    private static bool ContainsLocalized(string text, string key)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(key))
            return false;

        string localized = Localization.Get(key);
        if (!string.IsNullOrEmpty(localized) && text.IndexOf(localized, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;
        return text.IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static string EscapeForLog(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        string escaped = value.Replace("\\", "\\\\").Replace("\r", "\\r").Replace("\n", "\\n").Replace("\"", "\\\"");
        if (escaped.Length > 1200)
            escaped = escaped.Substring(0, 1200) + "<truncated>";
        return escaped;
    }

    private static void NotifyToolbelt(string message)
    {
        try
        {
            World world = GameManager.Instance != null ? GameManager.Instance.World : null;
            EntityPlayerLocal player = world != null ? world.GetPrimaryPlayer() : null;
            if (player != null)
                GameManager.ShowTooltip(player, message, true, false, 4f);
        }
        catch
        {
        }
    }

    private struct CropCandidate
    {
        public readonly Vector3i Pos;
        public readonly int DistanceSquared;

        public CropCandidate(Vector3i pos, int distanceSquared)
        {
            Pos = pos;
            DistanceSquared = distanceSquared;
        }
    }

    private sealed class CropCandidateComparer : IComparer<CropCandidate>
    {
        public static readonly CropCandidateComparer Instance = new CropCandidateComparer();

        public int Compare(CropCandidate a, CropCandidate b)
        {
            int result = a.DistanceSquared.CompareTo(b.DistanceSquared);
            if (result != 0) return result;
            result = a.Pos.x.CompareTo(b.Pos.x);
            if (result != 0) return result;
            result = a.Pos.y.CompareTo(b.Pos.y);
            if (result != 0) return result;
            return a.Pos.z.CompareTo(b.Pos.z);
        }
    }

    private sealed class CropSnapshot
    {
        public bool Present;
        public string BlockName;
        public int BlockType;
        public string BlockClass;
        public string InitialBlockName;
        public bool BlockChanged;
        public bool BlockChangedSincePrevious;
        public int GrowthStage;
        public bool HasTileEntity;
        public string TileEntityRuntimeType;
        public ulong AccumulatedTicks;
        public long AccumulatedDeltaFromStart;
        public long AccumulatedDeltaFromPrevious;
        public bool AccumulatedChanged;
        public bool AccumulatedChangedSincePrevious;
        public ulong GameTimerTicks;
        public long GameTimerDeltaFromStart;
        public long GameTimerDeltaFromPrevious;
        public int LastProcessedSeconds;
        public int LastProcessedDeltaFromStart;
        public int LastProcessedDeltaFromPrevious;
        public ulong NowTicks;
        public int NowWorldSeconds;
        public bool Watered;
        public ulong StageRateTicks;
        public ulong PredictedDisplayAccumulatedTicks;
        public ulong PredictedDisplayRemainingSeconds;
        public bool IsMushroom;
        public bool LightPass;
        public bool LocalLightPass;
        public string AuthoritativeLightState;
        public byte SunRaw;
        public byte BlockRaw;
        public byte CropSun;
        public int RequiredLight;
        public int RequiredNaturalSunlight;
        public bool HasSun;
        public bool HasOpenSky;
        public bool HasBlockLight;
        public bool IsNight;
        public float BaseTemperature;
        public string BaseTemperatureSource;
        public float EnclosedBiomeAdjustment;
        public float HeatBonus;
        public float Temperature;
        public int MinTemperature;
        public bool TemperaturePass;
        public string Biome;
        public bool BiomePass;
        public int WaterDepletion;
        public bool WaterPass;
        public int TankWater;
        public int CollectorWater;
        public int PlotWater;
        public bool Eligible;
        public bool ExpectedBlocked;
        public string BlockerMask;
        public string HoverText;
        public bool HoverShowsTimer;
        public bool HoverShowsAnyBlockingReason;
        public bool HoverChanged;
    }
}
#endif

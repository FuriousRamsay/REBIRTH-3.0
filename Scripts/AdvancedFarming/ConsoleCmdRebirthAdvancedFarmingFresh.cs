using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using UnityEngine;
using UnityEngine.Scripting;

#nullable disable

[Preserve]
public class ConsoleCmdRebirthAdvancedFarmingFresh : ConsoleCmdAbstract
{
    private static Timer s_autoBenchTimer;
    private static AdvancedFarmingPerfSnapshotService.SampleSnapshot s_autoBenchStartSnapshot;
    private static AdvancedFarmingPerfSnapshotService.SampleSnapshot s_autoBenchLastSnapshot;
    private static long s_autoBenchStartedTicks;
    private static int s_autoBenchDurationSeconds;
    private static int s_autoBenchIntervalSeconds;
    private static int s_autoBenchGrowthSeconds;
    private static int s_autoBenchIntervalIndex;
    private static bool s_autoBenchRunning;
    private static readonly object s_autoBenchLock = new object();

    private static Timer s_farmingSuiteTimer;
    private static AdvancedFarmingPerfSnapshotService.SampleSnapshot s_farmingSuiteStartSnapshot;
    private static AdvancedFarmingPerfSnapshotService.SampleSnapshot s_farmingSuiteLastSnapshot;
    private static long s_farmingSuiteStartedTicks;
    private static int[] s_farmingSuiteGrowthSeconds;
    private static int s_farmingSuiteIndex;
    private static int s_farmingSuiteDurationSeconds;
    private static int s_farmingSuiteIntervalSeconds;
    private static int s_farmingSuiteRadius;
    private static Vector3i s_farmingSuiteCenter;
    private static bool s_farmingSuiteRunning;
    private static readonly object s_farmingSuiteLock = new object();

    private static Timer s_temperatureTrackTimer;
    private static readonly object s_temperatureTrackLock = new object();
    private static Vector3i s_temperatureTrackPos;
    private static long s_temperatureTrackStartedTicks;
    private static int s_temperatureTrackDurationSeconds;
    private static int s_temperatureTrackIntervalSeconds;
    private static int s_temperatureTrackSampleIndex;
    private static bool s_temperatureTrackRunning;
    private static bool s_temperatureTrackFreshEachSample;
    private static bool s_temperatureTrackHasLast;
    private static float s_temperatureTrackLastFinalTemp;
    private static float s_temperatureTrackLastBaseTemp;
    private static float s_temperatureTrackLastWeatherPenalty;
    private static float s_temperatureTrackLastHeatBonus;
    private static float s_temperatureTrackLastEnclosedAdjust;

    public override bool IsExecuteOnClient { get { return false; } }

    public override string[] getCommands()
    {
        return new[] { "rbfarming", "rbfarm", "rbadvancedfarming" };
    }

    public override string getDescription()
    {
        return "Controls the REBIRTH fresh Advanced Farming implementation.";
    }

    public override string getHelp()
    {
        return "Usage:\n"
             + "  rbfarming status\n"
             + "  rbfarming install\n"
             + "  rbfarming uninstall\n"
             + "  rbfarming enable\n"
             + "  rbfarming disable\n"
             + "  rbfarming growthminutes <minutes>\n"
             + "  rbfarming growthseconds <seconds>\n"
             + "  rbfarming naturalsunlight <0-15|status>\n"
             + "  rbfarming rules\n"
             + "  rbfarming overridebiome <biome> <rule|clear>\n"
             + "  rbfarming probe [radius]\n"
             + "  rbfarming probehere <x> <y> <z>\n"
             + "  rbfarming perfreport [radius]\n"
             + "  rbfarming perfsample <start|stop|reset|report>\n"
             + "  rbfarming stuttertrace [start|stop|status] [durationSeconds] [intervalSeconds] [slowMs]\n"
             + "  rbfarming perfcheck [durationSeconds] [intervalSeconds] [slowMs]\n"
             + "  rbfarming blockpatchbypass <on|off|toggle|status>\n"
             + "  rbfarming poweredlightshadows <on|off|toggle|status>\n"
             + "  rbfarming anchordiag\n"
             + "  rbfarming catchupdiag [radius]\n"
             + "  rbfarming catchupdiag <x> <y> <z> [runplant|runarea] [backSeconds]\n"
             + "  rbfarming catchupdebug <on|off|status>\n"
             + "  rbfarming autobench <growthSeconds> <durationSeconds> <intervalSeconds>\n"
             + "  rbfarming autobench90\n"
             + "  rbfarming autobench10\n"
             + "  rbfarming autobenchcold90 [temperature]\n"
             + "  rbfarming autobenchcold10 [temperature]\n"
             + "  rbfarming temperature <value|clear|status>\n"
             + "  rbfarming heatcache <status|clear>\n"
             + "  rbfarming temptrack <x> <y> <z> [durationRealSeconds] [intervalRealSeconds] [fresh]\n"
             + "  rbfarming temptrack <stop|status>\n"
             + "  rbfarming watercache <status|clear>\n"
             + "  rbfarming waterverify [radius]\n"
             + "  rbfarming cropenv [radius] [fail|all|pass]\n"
             + "  rbfarming cropenv <x> <y> <z>\n"
             + "  rbfarming lightdebug [radius] [samples]\n"
             + "  rbfarming lightdebug <x> <y> <z> [samples]\n"
             + "  rbfarming lightcolumn <x> <y> <z>\n"
             + "  rbfarming blockforensics <x> <y> <z>\n"
             + "  rbfarming doorlightissue\n"
             + "    no-arg nearest-door rotation/open-state + cobblestone-structure crop light diagnostic\n"
             + "  rbfarming heattest <x> <y> <z>\n"
             + "  rbfarming heattestarea <x> <y> <z> [radius]\n"
             + "  rbfarming heatfulltest [temperature] [radius] [benchSeconds] [intervalSeconds]\n"
             + "  rbfarming farmsuite [cycles] [durationSeconds] [intervalSeconds] [radius] [temperature]\n"
             + "  rbfarming hovertrace <x> <y> <z> <start|stop|reset|report>\n"
#if DEBUG
             + "  rbfarming debug <hover|tileentity|saveload|water|bootstrap|catchup|all> <on|off|status>\n"
#endif
#if DEBUG
             + "  rbfarming testcropgrowth on|off|toggle|status\n"
#endif
             + "\n"
             + "Growth timing commands are server-authoritative.\n"
             + "TileEntity patch install is explicit and does not use PatchAll.\n"
#if !DEBUG
             + "DEBUG-only rapid-growth commands are not compiled into this RELEASE build.\n"
#endif
             ;
    }

    public override void Execute(List<string> _params, CommandSenderInfo _senderInfo)
    {
        string cmd = (_params == null || _params.Count == 0) ? "status" : (_params[0] ?? string.Empty).Trim().ToLowerInvariant();

        switch (cmd)
        {
            case "help":
            case "?":
                Log.Out(getHelp());
                return;

            case "status":
                Log.Out(AdvancedFarmingRuntimePolicy.Status());
                Log.Out(RebirthAdvancedFarmingPatchInstaller.Status());
                Log.Out(AdvancedFarmingDynamicLightOpacityService.BuildStatus());
                return;

            case "install":
                Log.Out(RebirthAdvancedFarmingPatchInstaller.Install());
                return;

            case "uninstall":
                Log.Out(RebirthAdvancedFarmingPatchInstaller.Uninstall());
                return;

            case "enable":
                AdvancedFarmingRuntimePolicy.SetEnabled(true);
                Log.Out(AdvancedFarmingRuntimePolicy.Status());
                return;

            case "disable":
                AdvancedFarmingRuntimePolicy.SetEnabled(false);
                Log.Out(AdvancedFarmingRuntimePolicy.Status());
                return;

            case "growthminutes":
                if (_params == null || _params.Count < 2 || !int.TryParse(_params[1], out int minutes))
                {
                    Log.Out("Usage: rbfarming growthminutes <minutes>");
                    return;
                }
                AdvancedFarmingRuntimePolicy.SetGrowthMinutes(minutes);
                AdvancedFarmingCatchupService.ResetSchedulingState();
                Log.Out(AdvancedFarmingRuntimePolicy.Status());
                return;

            case "growthseconds":
                if (_params == null || _params.Count < 2 || !int.TryParse(_params[1], out int seconds))
                {
                    Log.Out("Usage: rbfarming growthseconds <seconds>");
                    return;
                }
                AdvancedFarmingRuntimePolicy.SetGrowthSeconds(seconds);
                AdvancedFarmingCatchupService.ResetSchedulingState();
                Log.Out(AdvancedFarmingRuntimePolicy.Status());
                return;

            case "naturalsunlight":
            case "minimumsunlight":
            case "minsun":
                if (_params == null || _params.Count < 2 || _params[1].Equals("status", System.StringComparison.OrdinalIgnoreCase))
                {
                    Log.Out(AdvancedFarmingRuntimePolicy.Status());
                    return;
                }
                if (!int.TryParse(_params[1], out int minimumNaturalSunlight))
                {
                    Log.Out("Usage: rbfarming naturalsunlight <0-15|status>");
                    return;
                }
                AdvancedFarmingRuntimePolicy.SetMinimumNaturalSunlight(minimumNaturalSunlight);
                Log.Out(AdvancedFarmingRuntimePolicy.Status());
                return;

            case "rules":
                Log.Out(AdvancedFarmingRuleResolver.Describe());
                return;

            case "overridebiome":
                if (_params == null || _params.Count < 3)
                {
                    Log.Out("Usage: rbfarming overridebiome <biome> <rule|clear>");
                    return;
                }
                string biome = _params[1];
                string rule = _params[2];
                if (rule.Equals("clear", System.StringComparison.OrdinalIgnoreCase))
                    rule = null;
                AdvancedFarmingRuleResolver.SetBiomeOverride(biome, rule);
                Log.Out(AdvancedFarmingRuleResolver.Describe());
                return;

            case "probe":
                ExecuteProbe(_params, _senderInfo);
                return;

            case "probehere":
                ExecuteProbeHere(_params);
                return;

            case "perfreport":
            case "perf":
                ExecutePerfReport(_params, _senderInfo);
                return;

            case "perfsample":
                ExecutePerfSample(_params);
                return;

            case "perfcheck":
            case "farmperfcheck":
                ExecutePerfCheck(_params);
                return;

            case "stuttertrace":
            case "farmstutter":
            case "perftrace":
                ExecuteStutterTrace(_params);
                return;

            case "blockpatchbypass":
            case "blocklightbypass":
            case "lightblockbypass":
                ExecuteBlockLightPatchBypass(_params);
                return;

            case "poweredlightshadows":
            case "poweredlightshadow":
            case "lightshadows":
            case "shadowedpoweredlights":
                ExecutePoweredLightShadows(_params);
                return;

            case "anchordiag":
                Log.Out(AdvancedFarmingPerfSnapshotService.BuildAnchorDiagnosticsReport());
                return;

            case "catchupdiag":
            case "catchup":
            case "catchuptest":
                ExecuteCatchupDiagnostic(_params, _senderInfo);
                return;

            case "catchupdebug":
            case "catchuplog":
            case "catchuplogging":
                ExecuteCatchupDebug(_params);
                return;

            case "catchupwatch":
            case "catchupsession":
            case "catchuplive":
                Log.Out("[AdvancedFarming CatchupDebug] catchupwatch has been disabled in v86 because it mutated crop timing/growth state. Use: rbfarming catchupdebug <on|off|status>.");
                Log.Out(AdvancedFarmingCatchupService.BuildCatchupDebugStatus());
                return;

            case "temperature":
            case "temp":
                ExecuteTemperature(_params);
                return;

            case "heatcache":
            case "heatstatus":
                ExecuteHeatCache(_params);
                return;

            case "temptrack":
            case "temperaturetrack":
                ExecuteTemperatureTrack(_params);
                return;

            case "watercache":
            case "waterstatus":
            case "waterregistry":
                ExecuteWaterCache(_params);
                return;

            case "waterverify":
            case "verifywater":
            case "waterregistryverify":
                ExecuteWaterVerify(_params, _senderInfo);
                return;

            case "cropenv":
            case "lightdiag":
            case "tempdiag":
            case "envdiag":
                ExecuteCropEnvironmentDiagnostic(_params, _senderInfo);
                return;

            case "lightdebug":
            case "cropenvdebug":
            case "autolightdebug":
                ExecuteLightAutoDebug(_params, _senderInfo);
                return;

            case "lightcolumn":
            case "cropcolumn":
                ExecuteLightColumnDiagnostic(_params);
                return;

            case "blockforensics":
            case "doorforensics":
                ExecuteBlockForensics(_params);
                return;

            case "doorlightissue":
            case "doorlightdiag":
            case "doorlight":
            case "autodoorlight":
                ExecuteDoorLightIssueDiagnostic(_senderInfo);
                return;

            case "farmsuite":
            case "farmingsuite":
            case "farmtest":
                ExecuteFarmingSuite(_params, _senderInfo);
                return;

            case "heatfulltest":
            case "heatautotest":
            case "heattestauto":
                ExecuteHeatFullTest(_params, _senderInfo);
                return;

            case "heattest":
            case "testheat":
                ExecuteHeatTest(_params);
                return;

            case "heattestarea":
            case "testheatarea":
            case "heatcropstest":
                ExecuteHeatTestArea(_params);
                return;

            case "autobenchcold90":
            case "benchcold90":
                ExecuteColdAutoBench(_params, "5400", "60", "10");
                return;

            case "autobenchcold10":
            case "benchcold10":
                ExecuteColdAutoBench(_params, "10", "80", "1");
                return;

            case "autobench90":
            case "bench90":
                ExecuteAutoBench(new List<string> { "autobench", "5400", "60", "10" });
                return;

            case "autobench10":
            case "bench10":
                ExecuteAutoBench(new List<string> { "autobench", "10", "80", "1" });
                return;

            case "autobench":
            case "perfauto":
            case "bench":
                ExecuteAutoBench(_params);
                return;

            case "hovertrace":
                ExecuteHoverTrace(_params);
                return;


#if DEBUG
            case "debug":
                ExecuteDebug(_params);
                return;
#endif

#if DEBUG
            case "testcropgrowth":
            case "rapidgrowth":
                ExecuteTestCropGrowth(_params);
                return;
#endif

            default:
                Log.Out("Unknown rbfarming command: " + cmd);
                Log.Out(getHelp());
                return;
        }
    }


    private static void ExecuteBlockLightPatchBypass(List<string> args)
    {
        string action = (args != null && args.Count > 1) ? (args[1] ?? string.Empty).Trim().ToLowerInvariant() : "status";

        if (action == "on" || action == "enable" || action == "enabled" || action == "true" || action == "1")
            Harmony_RebirthAdvancedFarmingLightProcessorPatch.BypassBlockLightPatch = true;
        else if (action == "off" || action == "disable" || action == "disabled" || action == "false" || action == "0")
            Harmony_RebirthAdvancedFarmingLightProcessorPatch.BypassBlockLightPatch = false;
        else if (action == "toggle" || action == "switch")
            Harmony_RebirthAdvancedFarmingLightProcessorPatch.BypassBlockLightPatch = !Harmony_RebirthAdvancedFarmingLightProcessorPatch.BypassBlockLightPatch;
        else if (action != "status" && action != "report" && action != "state")
        {
            Log.Out("Usage: rbfarming blockpatchbypass <on|off|toggle|status>");
            return;
        }

        Log.Out("[AdvancedFarming] LightProcessor BLOCK bypass=" + Harmony_RebirthAdvancedFarmingLightProcessorPatch.BypassBlockLightPatch
            + " (diagnostic only; when enabled, powered/BLOCK light uses vanilla static opacity and dynamic covers can leak powered light). Toggle one powered light off/on after changing this to force a clean BLOCK relight for A/B testing.");
    }

    private static void ExecutePoweredLightShadows(List<string> args)
    {
        string action = (args != null && args.Count > 1) ? (args[1] ?? string.Empty).Trim().ToLowerInvariant() : "status";

        if (action == "off" || action == "disable" || action == "disabled" || action == "false" || action == "0")
        {
            Log.Out("[AdvancedFarming] " + Harmony_RebirthPoweredLightShadowDiagnosticsPatch.SetDisabled(true));
        }
        else if (action == "on" || action == "enable" || action == "enabled" || action == "true" || action == "1")
        {
            Log.Out("[AdvancedFarming] " + Harmony_RebirthPoweredLightShadowDiagnosticsPatch.SetDisabled(false));
        }
        else if (action == "toggle" || action == "switch")
        {
            Log.Out("[AdvancedFarming] " + Harmony_RebirthPoweredLightShadowDiagnosticsPatch.Toggle());
        }
        else if (action != "status" && action != "report" && action != "state")
        {
            Log.Out("Usage: rbfarming poweredlightshadows <on|off|toggle|status>");
            return;
        }

        Log.Out(Harmony_RebirthPoweredLightShadowDiagnosticsPatch.BuildStatus());
    }


    public static void ResetAutomationState(string reason)
    {
        lock (s_autoBenchLock)
        {
            if (s_autoBenchTimer != null)
            {
                s_autoBenchTimer.Dispose();
                s_autoBenchTimer = null;
            }

            s_autoBenchRunning = false;
        }

        lock (s_farmingSuiteLock)
        {
            if (s_farmingSuiteTimer != null)
            {
                s_farmingSuiteTimer.Dispose();
                s_farmingSuiteTimer = null;
            }

            s_farmingSuiteRunning = false;
        }

        AdvancedFarmingPerfSnapshotService.SetSampling(false);
    }



    private static void ExecuteCatchupDebug(List<string> args)
    {
        string action = (args != null && args.Count > 1) ? (args[1] ?? string.Empty).Trim().ToLowerInvariant() : "status";

        if (action == "on" || action == "enable" || action == "enabled" || action == "true" || action == "1" || action == "start")
        {
            Log.Out(AdvancedFarmingCatchupService.SetCatchupDebug(true));
            Log.Out("[AdvancedFarming CatchupDebug] Logging is now active. Reproduce the unload/reload/catch-up scenario normally; this command does not force catch-up or change crop timers.");
            return;
        }

        if (action == "off" || action == "disable" || action == "disabled" || action == "false" || action == "0" || action == "stop")
        {
            Log.Out(AdvancedFarmingCatchupService.SetCatchupDebug(false));
            return;
        }

        if (action == "status" || action == "report" || action == "state" || action == "diag")
        {
            Log.Out(AdvancedFarmingCatchupService.BuildCatchupDebugStatus());
            return;
        }

        Log.Out("Usage: rbfarming catchupdebug <on|off|status>");
        Log.Out(AdvancedFarmingCatchupService.BuildCatchupDebugStatus());
    }


    private static void ExecuteCatchupWatch(List<string> args, CommandSenderInfo senderInfo)
    {
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (world == null)
        {
            Log.Out("[AdvancedFarming CatchupWatch] No world loaded.");
            return;
        }

        string action = (args != null && args.Count > 1) ? (args[1] ?? string.Empty).Trim().ToLowerInvariant() : "status";

        if (action == "status" || action == "report" || action == "diag")
        {
            Log.Out(AdvancedFarmingCatchupService.BuildCatchupWatchStatus(world));
            return;
        }

        if (action == "finish" || action == "complete" || action == "end")
        {
            Log.Out(AdvancedFarmingCatchupService.FinishCatchupWatch(world, "manualFinish"));
            return;
        }

        if (action == "stop" || action == "cancel" || action == "off")
        {
            Log.Out(AdvancedFarmingCatchupService.StopCatchupWatch());
            return;
        }

        if (action != "start" && action != "begin" && action != "on")
        {
            Log.Out("Usage: rbfarming catchupwatch start <x> <y> <z> <growthSeconds> <waitSeconds> [label]");
            Log.Out("   or: rbfarming catchupwatch start <growthSeconds> <waitSeconds> [radius] [label]");
            Log.Out("   or: rbfarming catchupwatch <status|finish|stop>");
            return;
        }

        Vector3i pos;
        int growthSeconds;
        int waitSeconds;
        string label = null;

        if (TryParseVectorArgs(args, 2, out pos))
        {
            if (args == null || args.Count < 7 || !int.TryParse(args[5], out growthSeconds) || !int.TryParse(args[6], out waitSeconds))
            {
                Log.Out("Usage: rbfarming catchupwatch start <x> <y> <z> <growthSeconds> <waitSeconds> [label]");
                return;
            }

            if (args.Count > 7)
                label = args[7];
        }
        else
        {
            if (args == null || args.Count < 4 || !int.TryParse(args[2], out growthSeconds) || !int.TryParse(args[3], out waitSeconds))
            {
                Log.Out("Usage: rbfarming catchupwatch start <growthSeconds> <waitSeconds> [radius] [label]");
                return;
            }

            int radius = 8;
            if (args.Count > 4)
                int.TryParse(args[4], out radius);
            if (radius < 1)
                radius = 1;
            if (radius > 32)
                radius = 32;

            Vector3i center = ResolveCommandCenter(world, senderInfo);
            List<Vector3i> crops = ScanAdvancedFarmingCrops(world, center, radius, 3, 5);
            if (crops == null || crops.Count == 0)
            {
                Log.Out("[AdvancedFarming CatchupWatch] no crop found near center=" + center + " radius=" + radius + "; use explicit coordinates instead.");
                return;
            }

            pos = Vector3i.zero;
            bool foundGrowable = false;
            bool foundHarvestResettable = false;
            Vector3i harvestResettablePos = Vector3i.zero;
            int skippedHarvest = 0;
            int skippedNoTileEntity = 0;
            int resettableHarvest = 0;
            StringBuilder candidateSummary = new StringBuilder(1024);
            int summaryCount = 0;
            for (int i = 0; i < crops.Count; i++)
            {
                Vector3i candidate = crops[i];
                BlockValue candidateValue = world.GetBlock(candidate);
                string candidateName = candidateValue.Block != null ? candidateValue.Block.GetBlockName() : string.Empty;
                bool hasPlantTileEntity = world.GetTileEntity(candidate) is TileEntityPlantGrowingRebirth;
                bool isHarvest = IsHarvestStageCropName(candidateName);
                string seedStageName = ResolveSeedStageNameForHarvest(candidateName);
                bool canResetHarvest = isHarvest && !string.IsNullOrEmpty(seedStageName);

                if (summaryCount < 16)
                {
                    if (candidateSummary.Length > 0)
                        candidateSummary.Append(" | ");
                    candidateSummary.Append(candidate).Append(':').Append(candidateName)
                        .Append(":te=").Append(hasPlantTileEntity ? "Y" : "N")
                        .Append(":harvest=").Append(isHarvest ? "Y" : "N");
                    if (canResetHarvest)
                        candidateSummary.Append(":resetTo=").Append(seedStageName);
                    summaryCount++;
                }

                if (!hasPlantTileEntity)
                {
                    skippedNoTileEntity++;
                    continue;
                }

                if (isHarvest)
                {
                    skippedHarvest++;
                    if (canResetHarvest)
                    {
                        resettableHarvest++;
                        if (!foundHarvestResettable)
                        {
                            harvestResettablePos = candidate;
                            foundHarvestResettable = true;
                        }
                    }

                    continue;
                }

                pos = candidate;
                foundGrowable = true;
                break;
            }

            if (!foundGrowable && foundHarvestResettable)
            {
                pos = harvestResettablePos;
                foundGrowable = true;
                Log.Out("[AdvancedFarming CatchupWatch] no immature crop found near center=" + center
                    + " radius=" + radius
                    + "; using nearest resettable harvest-stage crop for a controlled catch-up test pos=" + pos
                    + " resetTo=" + ResolveSeedStageNameForHarvest((world.GetBlock(pos).Block != null ? world.GetBlock(pos).Block.GetBlockName() : string.Empty))
                    + " candidates=" + crops.Count
                    + " skippedHarvest=" + skippedHarvest
                    + " resettableHarvest=" + resettableHarvest
                    + " skippedNoTileEntity=" + skippedNoTileEntity
                    + " candidateSummary=" + candidateSummary.ToString());
            }

            if (!foundGrowable)
            {
                Log.Out("[AdvancedFarming CatchupWatch] no usable crop with a crop tile entity found near center=" + center
                    + " radius=" + radius
                    + " candidates=" + crops.Count
                    + " skippedHarvest=" + skippedHarvest
                    + " resettableHarvest=" + resettableHarvest
                    + " skippedNoTileEntity=" + skippedNoTileEntity
                    + " candidateSummary=" + candidateSummary.ToString()
                    + "; use explicit coordinates for the exact crop you want to test.");
                return;
            }

            if (args.Count > 5)
                label = args[5];
        }

        if (growthSeconds < 1)
            growthSeconds = 1;
        if (waitSeconds < 1)
            waitSeconds = 1;

        Log.Out(AdvancedFarmingCatchupService.StartCatchupWatch(world, pos, growthSeconds, waitSeconds, label));
    }


    private static void ExecuteCatchupDiagnostic(List<string> args, CommandSenderInfo senderInfo)
    {
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (world == null)
        {
            Log.Out("[AdvancedFarming CatchupDiag] No world loaded.");
            return;
        }

        Vector3i explicitPos;
        if (TryParseVectorArgs(args, 1, out explicitPos))
        {
            string mode = "report";
            int backSeconds = 0;

            if (args != null && args.Count > 4)
                mode = (args[4] ?? string.Empty).Trim().ToLowerInvariant();

            if (args != null && args.Count > 5)
                int.TryParse(args[5], out backSeconds);

            if (backSeconds < 0)
                backSeconds = 0;

            if (mode == "report" || mode == "status" || mode == "diag" || string.IsNullOrEmpty(mode))
            {
                Log.Out(AdvancedFarmingCatchupService.BuildCatchupDiagnostic(world, explicitPos));
                return;
            }

            if (mode == "run" || mode == "plant" || mode == "shared" || mode == "runplant")
            {
                Log.Out(AdvancedFarmingCatchupService.RunCatchupDiagnostic(world, explicitPos, backSeconds, false));
                return;
            }

            if (mode == "area" || mode == "anchor" || mode == "runarea")
            {
                Log.Out(AdvancedFarmingCatchupService.RunCatchupDiagnostic(world, explicitPos, backSeconds, true));
                return;
            }

            Log.Out("Usage: rbfarming catchupdiag <x> <y> <z> [runplant|runarea] [backSeconds]");
            return;
        }

        int radius = 8;
        if (args != null && args.Count > 1)
            int.TryParse(args[1], out radius);

        if (radius < 1)
            radius = 1;
        if (radius > 32)
            radius = 32;

        Vector3i center = ResolveCommandCenter(world, senderInfo);
        Log.Out("[AdvancedFarming CatchupDiag] scan center=" + center + " radius=" + radius + " max=40");

        int count = 0;
        for (int x = center.x - radius; x <= center.x + radius && count < 40; x++)
        {
            for (int z = center.z - radius; z <= center.z + radius && count < 40; z++)
            {
                for (int y = center.y - 3; y <= center.y + 5 && count < 40; y++)
                {
                    Vector3i pos = new Vector3i(x, y, z);
                    TileEntityPlantGrowingRebirth te = world.GetTileEntity(pos) as TileEntityPlantGrowingRebirth;
                    if (te == null)
                        continue;

                    Log.Out(AdvancedFarmingCatchupService.BuildCatchupDiagnostic(world, pos));
                    count++;
                }
            }
        }

        Log.Out("[AdvancedFarming CatchupDiag] scanCropCount=" + count);
    }


    private static void ExecuteProbe(List<string> args, CommandSenderInfo senderInfo)
    {
        int radius = 8;
        if (args != null && args.Count > 1)
            int.TryParse(args[1], out radius);

        if (radius < 1)
            radius = 1;
        if (radius > 32)
            radius = 32;

        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (world == null)
        {
            Log.Out("[AdvancedFarming Probe] No world loaded.");
            return;
        }

        Vector3i center = Vector3i.zero;
        EntityPlayer player = null;

        if (senderInfo.RemoteClientInfo != null)
            player = world.GetEntity(senderInfo.RemoteClientInfo.entityId) as EntityPlayer;

        if (player == null && world.Players != null && world.Players.list != null && world.Players.list.Count > 0)
            player = world.Players.list[0];

        if (player != null)
            center = World.worldToBlockPos(player.GetPosition());
        else
        {
            EntityPlayerLocal local = world.GetPrimaryPlayer();
            if (local != null)
                center = World.worldToBlockPos(local.GetPosition());
        }

        Log.Out("[AdvancedFarming Probe] center=" + center + " radius=" + radius + " " + AdvancedFarmingRuntimePolicy.Status());

        int count = 0;
        for (int x = center.x - radius; x <= center.x + radius; x++)
        {
            for (int z = center.z - radius; z <= center.z + radius; z++)
            {
                for (int y = center.y - 3; y <= center.y + 5; y++)
                {
                    Vector3i pos = new Vector3i(x, y, z);
                    TileEntityPlantGrowingRebirth te = world.GetTileEntity(pos) as TileEntityPlantGrowingRebirth;
                    if (te == null)
                        continue;

                    Log.Out(AdvancedFarmingCatchupService.DescribePlantTiming(world, pos, world.GetBlock(pos), te));
                    count++;
                }
            }
        }

        Log.Out("[AdvancedFarming Probe] cropCount=" + count);
    }

    private static void ExecuteProbeHere(List<string> args)
    {
        if (args == null || args.Count < 4)
        {
            Log.Out("Usage: rbfarming probehere <x> <y> <z>");
            return;
        }

        if (!int.TryParse(args[1], out int x) || !int.TryParse(args[2], out int y) || !int.TryParse(args[3], out int z))
        {
            Log.Out("Usage: rbfarming probehere <x> <y> <z>");
            return;
        }

        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (world == null)
        {
            Log.Out("[AdvancedFarming Probe] No world loaded.");
            return;
        }

        Vector3i pos = new Vector3i(x, y, z);
        TileEntityPlantGrowingRebirth te = world.GetTileEntity(pos) as TileEntityPlantGrowingRebirth;
        if (te == null)
        {
            Log.Out("[AdvancedFarming Probe] No crop tile entity at " + pos);
            return;
        }

        Log.Out(AdvancedFarmingCatchupService.DescribePlantTiming(world, pos, world.GetBlock(pos), te));
    }


    private static void ExecutePerfReport(List<string> args, CommandSenderInfo senderInfo)
    {
        int radius = 24;
        if (args != null && args.Count > 1)
            int.TryParse(args[1], out radius);

        if (radius < 1)
            radius = 1;
        if (radius > 64)
            radius = 64;

        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (world == null)
        {
            Log.Out("[AdvancedFarming Perf] No world loaded.");
            return;
        }

        Vector3i center = ResolveCommandCenter(world, senderInfo);
        Log.Out(AdvancedFarmingPerfSnapshotService.BuildReport(world, center, radius));
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


    private static void ExecutePerfSample(List<string> args)
    {
        string action = (args != null && args.Count > 1) ? (args[1] ?? string.Empty).Trim().ToLowerInvariant() : "report";

        if (action == "start" || action == "on")
        {
            Log.Out(AdvancedFarmingPerfSnapshotService.SetSampling(true));
            return;
        }

        if (action == "stop" || action == "off")
        {
            Log.Out(AdvancedFarmingPerfSnapshotService.SetSampling(false));
            return;
        }

        if (action == "reset")
        {
            AdvancedFarmingPerfSnapshotService.ResetSampling();
            Log.Out("[AdvancedFarming PerfSample] reset");
            return;
        }

        if (action == "report" || action == "status")
        {
            Log.Out(AdvancedFarmingPerfSnapshotService.BuildSamplingReport());
            return;
        }

        Log.Out("Usage: rbfarming perfsample <start|stop|reset|report>");
    }


    private static void ExecutePerfCheck(List<string> args)
    {
        int durationSeconds = 60;
        int intervalSeconds = 5;
        double slowMs = 4d;

        if (args != null && args.Count > 1)
            int.TryParse(args[1], out durationSeconds);
        if (args != null && args.Count > 2)
            int.TryParse(args[2], out intervalSeconds);
        if (args != null && args.Count > 3)
            double.TryParse(args[3], out slowMs);

        List<string> traceArgs = new List<string>();
        traceArgs.Add("stuttertrace");
        traceArgs.Add("start");
        traceArgs.Add(durationSeconds.ToString());
        traceArgs.Add(intervalSeconds.ToString());
        traceArgs.Add(slowMs.ToString("0.###"));

        ExecuteStutterTrace(traceArgs);
    }


    private static void ExecuteStutterTrace(List<string> args)
    {
        string action = (args != null && args.Count > 1) ? (args[1] ?? string.Empty).Trim().ToLowerInvariant() : "start";

        if (action == "stop" || action == "off")
        {
            Log.Out(AdvancedFarmingStutterTraceService.Stop());
            return;
        }

        if (action == "status" || action == "report")
        {
            Log.Out(AdvancedFarmingStutterTraceService.Status());
            return;
        }

        if (action == "start" || action == "on")
        {
            int durationSeconds = 60;
            int intervalSeconds = 5;
            double slowMs = 8d;

            if (args != null && args.Count > 2)
                int.TryParse(args[2], out durationSeconds);
            if (durationSeconds <= 0)
                durationSeconds = 60;

            if (args != null && args.Count > 3)
                int.TryParse(args[3], out intervalSeconds);
            if (intervalSeconds <= 0)
                intervalSeconds = 5;

            if (args != null && args.Count > 4)
                double.TryParse(args[4], out slowMs);
            if (slowMs <= 0d)
                slowMs = 8d;

            Log.Out(AdvancedFarmingStutterTraceService.Start(durationSeconds, intervalSeconds, slowMs));
            return;
        }

        Log.Out("Usage: rbfarming stuttertrace [start|stop|status] [durationSeconds] [intervalSeconds] [slowMs]");
    }




    private static void ExecuteTemperature(List<string> args)
    {
        if (args == null || args.Count < 2)
        {
            Log.Out("Usage: rbfarming temperature <value|clear|status>");
            Log.Out(AdvancedFarmingHeatQueryService.TemperatureStatus());
            return;
        }

        string action = (args[1] ?? string.Empty).Trim().ToLowerInvariant();
        if (action == "clear" || action == "off" || action == "reset")
        {
            Log.Out(AdvancedFarmingHeatQueryService.ClearTemperatureOverride());
            return;
        }

        if (action == "status")
        {
            Log.Out(AdvancedFarmingHeatQueryService.TemperatureStatus());
            return;
        }

        if (!float.TryParse(args[1], out float value))
        {
            Log.Out("Usage: rbfarming temperature <value|clear|status>");
            return;
        }

        Log.Out(AdvancedFarmingHeatQueryService.SetTemperatureOverride(value));
    }

    private static void ExecuteWaterCache(List<string> args)
    {
        string action = args != null && args.Count > 1 ? (args[1] ?? string.Empty).Trim().ToLowerInvariant() : "status";

        if (action == "clear" || action == "reset" || action == "invalidate")
        {
            Log.Out("[AdvancedFarming WaterRegistry] no cached water values/results to clear; provider positions remain registered.");
        }

        Log.Out(AdvancedFarmingWaterQueryService.BuildStatus());
    }


    private static void ExecuteWaterVerify(List<string> args, CommandSenderInfo senderInfo)
    {
        int radius = 12;
        if (args != null && args.Count > 1)
            int.TryParse(args[1], out radius);

        if (radius < 1)
            radius = 1;
        if (radius > 64)
            radius = 64;

        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (world == null)
        {
            Log.Out("[AdvancedFarming WaterVerify] No world loaded.");
            return;
        }

        Vector3i center = ResolveCommandCenter(world, senderInfo);
        LogWaterVerify(world, center, radius, "manual");
    }

    private static void LogFarmingSuitePatchValidation()
    {
        int installedMethods = RebirthAdvancedFarmingPatchInstaller.WorkstationHeatInstalledMethods;
        bool pass = installedMethods >= 1;
        Log.Out("[AdvancedFarming FarmSuite] heatPatchValidation workstationHeatPatchInstalledMethods=" + installedMethods + " pass=" + pass);
    }

    private static void LogWaterVerify(World world, Vector3i center, int radius, string label)
    {
        if (world == null)
            return;

        int minY = center.y - 5;
        int maxY = center.y + 5;
        int cropsChecked = 0;
        int registryMismatches = 0;
        int printedMismatches = 0;
        int maxMismatchPrints = 20;
        int storageFirstPriorityCandidates = 0;
        int storageFirstPriorityPass = 0;
        int storageFirstPriorityFail = 0;

        List<AdvancedFarmingWaterProviderRegistry.ProviderDebugEntry> scannedEntries = ScanWaterProviderEntries(world, center, radius, minY, maxY);
        List<AdvancedFarmingWaterProviderRegistry.ProviderDebugEntry> registryEntries = AdvancedFarmingWaterProviderRegistry.CollectProviderDebugEntriesInArea(world, center, radius, minY, maxY, true);
        ProviderScanCounts scanned = CountProviderEntries(scannedEntries);
        ProviderScanCounts registry = CountProviderEntries(registryEntries);
        int providerMissing = 0;
        int providerExtra = 0;
        LogProviderDiffs(label, registryEntries, scannedEntries, maxMismatchPrints, ref providerMissing, ref providerExtra);

        for (int x = center.x - radius; x <= center.x + radius; x++)
        {
            for (int z = center.z - radius; z <= center.z + radius; z++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    Vector3i pos = new Vector3i(x, y, z);
                    BlockValue blockValue = world.GetBlock(pos);
                    string blockName = blockValue.Block != null ? blockValue.Block.GetBlockName() : string.Empty;
                    if (!IsAdvancedFarmingCropName(blockName))
                        continue;

                    cropsChecked++;
                    int registryTank = RebirthUtilities.CheckForWater(world, pos, "FuriousRamsayWaterTank", 4, 1, 0);
                    int scanTank = RebirthUtilities.CheckForWaterUncached(world, pos, "FuriousRamsayWaterTank", 4, 1, 0);
                    int registryCollector = RebirthUtilities.CheckForWater(world, pos, "FuriousRamsayDewCollector", 4, 1, 0);
                    int scanCollector = RebirthUtilities.CheckForWaterUncached(world, pos, "FuriousRamsayDewCollector", 4, 1, 0);

                    if (registryTank != scanTank || registryCollector != scanCollector)
                    {
                        registryMismatches++;
                        if (printedMismatches < maxMismatchPrints)
                        {
                            Log.Out("[AdvancedFarming WaterVerify] mismatch label=" + label
                                + " pos=" + pos
                                + " registryTank=" + registryTank
                                + " scanTank=" + scanTank
                                + " registryCollector=" + registryCollector
                                + " scanCollector=" + scanCollector);
                            printedMismatches++;
                        }
                    }

                    TileEntityFarmPlotRebirth plot = world.GetTileEntity(pos + Vector3i.down) as TileEntityFarmPlotRebirth;
                    int plotWater = plot != null ? plot.waterCount : 0;
                    bool hasStorageWater = registryTank > 0 || registryCollector > 0;
                    if (plotWater > 0 && hasStorageWater)
                    {
                        storageFirstPriorityCandidates++;
                        AdvancedFarmingWaterProviderKind selectedKind = AdvancedFarmingWaterProviderRegistry.SelectConsumptionProvider(world, pos, 1);
                        if (selectedKind == AdvancedFarmingWaterProviderKind.WaterTank || selectedKind == AdvancedFarmingWaterProviderKind.DewCollector)
                            storageFirstPriorityPass++;
                        else
                            storageFirstPriorityFail++;
                    }
                }
            }
        }

        bool providerCountPass = providerMissing == 0
            && providerExtra == 0
            && registry.WaterTanks == scanned.WaterTanks
            && registry.DewCollectors == scanned.DewCollectors
            && registry.FarmPlots == scanned.FarmPlots;
        bool waterResultPass = registryMismatches == 0;
        bool storageFirstPass = storageFirstPriorityFail == 0;

        Log.Out("[AdvancedFarming WaterVerify] summary label=" + label
            + " center=" + center
            + " radius=" + radius
            + " cropsChecked=" + cropsChecked
            + " registryMismatches=" + registryMismatches
            + " printedMismatches=" + printedMismatches
            + " registryWaterTanks=" + registry.WaterTanks
            + " scannedWaterTanks=" + scanned.WaterTanks
            + " registryDewCollectors=" + registry.DewCollectors
            + " scannedDewCollectors=" + scanned.DewCollectors
            + " registryFarmPlots=" + registry.FarmPlots
            + " scannedFarmPlots=" + scanned.FarmPlots
            + " providerMissing=" + providerMissing
            + " providerExtra=" + providerExtra
            + " providerCountPass=" + providerCountPass
            + " waterResultPass=" + waterResultPass
            + " storageFirstPriorityCandidates=" + storageFirstPriorityCandidates
            + " storageFirstPriorityPass=" + storageFirstPriorityPass
            + " storageFirstPriorityFail=" + storageFirstPriorityFail
            + " storageFirstPriorityPassAll=" + storageFirstPass);

        Log.Out(AdvancedFarmingWaterProviderRegistry.BuildStatus());
    }

    private struct ProviderScanCounts
    {
        public int WaterTanks;
        public int DewCollectors;
        public int FarmPlots;
    }

    private static List<AdvancedFarmingWaterProviderRegistry.ProviderDebugEntry> ScanWaterProviderEntries(World world, Vector3i center, int radius, int minY, int maxY)
    {
        List<AdvancedFarmingWaterProviderRegistry.ProviderDebugEntry> entries = new List<AdvancedFarmingWaterProviderRegistry.ProviderDebugEntry>();
        for (int x = center.x - radius; x <= center.x + radius; x++)
        {
            for (int z = center.z - radius; z <= center.z + radius; z++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    Vector3i pos = new Vector3i(x, y, z);
                    TileEntity te = world.GetTileEntity(pos);
                    AdvancedFarmingWaterProviderKind kind = AdvancedFarmingWaterProviderKind.Unknown;

                    if (te is TileEntityFarmPlotRebirth)
                    {
                        kind = AdvancedFarmingWaterProviderKind.FarmPlot;
                    }
                    else if (te is TileEntityWaterTankRebirth)
                    {
                        BlockValue blockValueForKind = world.GetBlock(pos);
                        string blockNameForKind = blockValueForKind.Block != null ? blockValueForKind.Block.GetBlockName() : string.Empty;
                        kind = IsDewCollectorProviderName(blockNameForKind) ? AdvancedFarmingWaterProviderKind.DewCollector : AdvancedFarmingWaterProviderKind.WaterTank;
                    }

                    if (kind == AdvancedFarmingWaterProviderKind.Unknown)
                        continue;

                    BlockValue blockValue = world.GetBlock(pos);
                    string blockName = blockValue.Block != null ? blockValue.Block.GetBlockName() : string.Empty;
                    entries.Add(new AdvancedFarmingWaterProviderRegistry.ProviderDebugEntry
                    {
                        Pos = pos,
                        Kind = kind,
                        BlockType = blockValue.type,
                        BlockName = blockName
                    });
                }
            }
        }

        return entries;
    }

    private static ProviderScanCounts CountProviderEntries(List<AdvancedFarmingWaterProviderRegistry.ProviderDebugEntry> entries)
    {
        ProviderScanCounts counts = new ProviderScanCounts();
        if (entries == null)
            return counts;

        for (int i = 0; i < entries.Count; i++)
        {
            AdvancedFarmingWaterProviderKind kind = entries[i].Kind;
            if (kind == AdvancedFarmingWaterProviderKind.WaterTank)
                counts.WaterTanks++;
            else if (kind == AdvancedFarmingWaterProviderKind.DewCollector)
                counts.DewCollectors++;
            else if (kind == AdvancedFarmingWaterProviderKind.FarmPlot)
                counts.FarmPlots++;
        }

        return counts;
    }

    private static void LogProviderDiffs(string label, List<AdvancedFarmingWaterProviderRegistry.ProviderDebugEntry> registryEntries, List<AdvancedFarmingWaterProviderRegistry.ProviderDebugEntry> scannedEntries, int maxPrints, ref int missing, ref int extra)
    {
        int printed = 0;

        if (scannedEntries != null)
        {
            for (int i = 0; i < scannedEntries.Count; i++)
            {
                AdvancedFarmingWaterProviderRegistry.ProviderDebugEntry scanned = scannedEntries[i];
                if (FindProviderEntry(registryEntries, scanned.Pos, scanned.Kind) >= 0)
                    continue;

                missing++;
                if (printed < maxPrints)
                {
                    Log.Out("[AdvancedFarming WaterVerify] providerMissing label=" + label
                        + " pos=" + scanned.Pos
                        + " kind=" + scanned.Kind
                        + " block=" + scanned.BlockName);
                    printed++;
                }
            }
        }

        if (registryEntries != null)
        {
            for (int i = 0; i < registryEntries.Count; i++)
            {
                AdvancedFarmingWaterProviderRegistry.ProviderDebugEntry registry = registryEntries[i];
                if (FindProviderEntry(scannedEntries, registry.Pos, registry.Kind) >= 0)
                    continue;

                extra++;
                if (printed < maxPrints)
                {
                    Log.Out("[AdvancedFarming WaterVerify] providerExtra label=" + label
                        + " pos=" + registry.Pos
                        + " kind=" + registry.Kind
                        + " block=" + registry.BlockName);
                    printed++;
                }
            }
        }
    }

    private static int FindProviderEntry(List<AdvancedFarmingWaterProviderRegistry.ProviderDebugEntry> entries, Vector3i pos, AdvancedFarmingWaterProviderKind kind)
    {
        if (entries == null)
            return -1;

        for (int i = 0; i < entries.Count; i++)
        {
            AdvancedFarmingWaterProviderRegistry.ProviderDebugEntry entry = entries[i];
            if (entry.Kind == kind && entry.Pos.x == pos.x && entry.Pos.y == pos.y && entry.Pos.z == pos.z)
                return i;
        }

        return -1;
    }

    private static bool IsDewCollectorProviderName(string blockName)
    {
        return !string.IsNullOrEmpty(blockName) && blockName.IndexOf("DewCollector", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }


    private static void ExecuteTemperatureTrack(List<string> args)
    {
        if (args == null || args.Count < 2)
        {
            Log.Out("Usage: rbfarming temptrack <x> <y> <z> [durationRealSeconds] [intervalRealSeconds] [fresh]");
            Log.Out("Usage: rbfarming temptrack <stop|status>");
            return;
        }

        string sub = (args[1] ?? string.Empty).Trim().ToLowerInvariant();
        if (sub == "stop" || sub == "off" || sub == "cancel")
        {
            StopTemperatureTrack("stopped by command");
            return;
        }

        if (sub == "status")
        {
            lock (s_temperatureTrackLock)
            {
                Log.Out("[AdvancedFarming TempTrack] running=" + s_temperatureTrackRunning
                    + " pos=" + s_temperatureTrackPos
                    + " durationRealSeconds=" + s_temperatureTrackDurationSeconds
                    + " intervalRealSeconds=" + s_temperatureTrackIntervalSeconds
                    + " sampleIndex=" + s_temperatureTrackSampleIndex
                    + " freshEachSample=" + s_temperatureTrackFreshEachSample);
            }
            return;
        }

        Vector3i pos;
        if (!TryParseVectorArgs(args, 1, out pos))
        {
            Log.Out("Usage: rbfarming temptrack <x> <y> <z> [durationRealSeconds] [intervalRealSeconds] [fresh]");
            Log.Out("Example: rbfarming temptrack 123 45 678 180 2");
            Log.Out("Example fresh-cache comparison: rbfarming temptrack 123 45 678 180 2 fresh");
            return;
        }

        int durationSeconds = 180;
        int intervalSeconds = 2;
        if (args.Count > 4)
            int.TryParse(args[4], out durationSeconds);
        if (args.Count > 5)
            int.TryParse(args[5], out intervalSeconds);

        if (durationSeconds < 5)
            durationSeconds = 5;
        if (durationSeconds > 1800)
            durationSeconds = 1800;
        if (intervalSeconds < 1)
            intervalSeconds = 1;
        if (intervalSeconds > 60)
            intervalSeconds = 60;

        bool freshEachSample = false;
        if (args.Count > 6)
        {
            string flag = args[6] ?? string.Empty;
            freshEachSample = flag.Equals("fresh", StringComparison.OrdinalIgnoreCase)
                || flag.Equals("nocache", StringComparison.OrdinalIgnoreCase)
                || flag.Equals("clearcache", StringComparison.OrdinalIgnoreCase);
        }

        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (world == null)
        {
            Log.Out("[AdvancedFarming TempTrack] No world loaded.");
            return;
        }

        lock (s_temperatureTrackLock)
        {
            if (s_temperatureTrackTimer != null)
            {
                s_temperatureTrackTimer.Dispose();
                s_temperatureTrackTimer = null;
            }

            s_temperatureTrackPos = pos;
            s_temperatureTrackDurationSeconds = durationSeconds;
            s_temperatureTrackIntervalSeconds = intervalSeconds;
            s_temperatureTrackStartedTicks = DateTime.UtcNow.Ticks;
            s_temperatureTrackSampleIndex = 0;
            s_temperatureTrackRunning = true;
            s_temperatureTrackFreshEachSample = freshEachSample;
            s_temperatureTrackHasLast = false;
            s_temperatureTrackLastFinalTemp = 0f;
            s_temperatureTrackLastBaseTemp = 0f;
            s_temperatureTrackLastWeatherPenalty = 0f;
            s_temperatureTrackLastHeatBonus = 0f;
            s_temperatureTrackLastEnclosedAdjust = 0f;

            Log.Out("[AdvancedFarming TempTrack] started pos=" + pos
                + " durationRealSeconds=" + durationSeconds
                + " intervalRealSeconds=" + intervalSeconds
                + " freshEachSample=" + freshEachSample
                + " note=Logs WeatherManager temp, weather penalty, heat bonus, enclosed biome adjustment, day/night state, thermal path, and final crop temp.");

            LogTemperatureTrackSampleLocked("start");
            s_temperatureTrackTimer = new Timer(TemperatureTrackTick, null, intervalSeconds * 1000, intervalSeconds * 1000);
        }
    }

    private static void StopTemperatureTrack(string reason)
    {
        lock (s_temperatureTrackLock)
        {
            if (s_temperatureTrackTimer != null)
            {
                s_temperatureTrackTimer.Dispose();
                s_temperatureTrackTimer = null;
            }

            if (!s_temperatureTrackRunning)
            {
                Log.Out("[AdvancedFarming TempTrack] not running");
                return;
            }

            s_temperatureTrackRunning = false;
            Log.Out("[AdvancedFarming TempTrack] complete reason=" + reason
                + " samples=" + s_temperatureTrackSampleIndex
                + " pos=" + s_temperatureTrackPos);
        }
    }

    private static void TemperatureTrackTick(object state)
    {
        lock (s_temperatureTrackLock)
        {
            if (!s_temperatureTrackRunning)
                return;

            long elapsedTicks = DateTime.UtcNow.Ticks - s_temperatureTrackStartedTicks;
            int elapsedSeconds = (int)(elapsedTicks / TimeSpan.TicksPerSecond);
            if (elapsedSeconds < 0)
                elapsedSeconds = 0;

            LogTemperatureTrackSampleLocked("interval");

            if (elapsedSeconds >= s_temperatureTrackDurationSeconds)
                StopTemperatureTrack("duration reached");
        }
    }

    private static void LogTemperatureTrackSampleLocked(string label)
    {
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (world == null)
        {
            Log.Out("[AdvancedFarming TempTrack] sampleSkipped label=" + label + " reason=no world");
            return;
        }

        if (s_temperatureTrackFreshEachSample)
        {
            AdvancedFarmingLightService.ClearCache();
            AdvancedFarmingTemperatureService.ClearCache();
            AdvancedFarmingHeatQueryService.InvalidateAll("temptrack fresh sample");
        }

        s_temperatureTrackSampleIndex++;
        long elapsedTicks = DateTime.UtcNow.Ticks - s_temperatureTrackStartedTicks;
        int elapsedRealSeconds = (int)(elapsedTicks / TimeSpan.TicksPerSecond);
        if (elapsedRealSeconds < 0)
            elapsedRealSeconds = 0;

        Vector3i pos = s_temperatureTrackPos;
        BlockValue blockValue = world.GetBlock(pos);
        string blockName = blockValue.Block != null ? blockValue.Block.GetBlockName() : "<null>";
        bool isMushroom = blockName.IndexOf("mushroom", StringComparison.OrdinalIgnoreCase) >= 0;
        bool advancedFarming = AdvancedFarmingRuntimePolicy.Enabled;

        AdvancedFarmingLightService.LightResult light = AdvancedFarmingLightService.Evaluate(
            world,
            pos,
            3,
            3,
            isMushroom,
            advancedFarming);

        bool hasNaturalLight = light.HasSunLight || light.HasOpenSky;
        AdvancedFarmingTemperatureService.TemperatureResult temperature = AdvancedFarmingTemperatureService.Evaluate(
            world,
            pos,
            hasNaturalLight,
            advancedFarming);

        ulong worldTime = world.GetWorldTime();
        int day = GameUtils.WorldTimeToDays(worldTime);
        int hour = GameUtils.WorldTimeToHours(worldTime);
        int minute = GameUtils.WorldTimeToMinutes(worldTime);
        bool isDaytime = world.IsDaytime();
        int dawnHour = world.DawnHour;
        int duskHour = world.DuskHour;

        WeatherManager weather = WeatherManager.Instance;
        float weatherManagerTemp = weather != null ? weather.GetCurrentTemperatureValue() : 0f;
        float cloudPercent = weather != null ? weather.GetCurrentCloudThicknessPercent() : 0f;
        float rainPercent = weather != null ? weather.GetCurrentRainfallPercent() : 0f;
        float cloudPenalty = cloudPercent * 10f;
        float rainPenalty = rainPercent * 10f;
        float hotAdjust = AdvancedFarmingTemperatureService.GetEnclosedBiomeAdjustment(temperature.BiomeName, false);
        float coldAdjust = AdvancedFarmingTemperatureService.GetEnclosedBiomeAdjustment(temperature.BiomeName, true);
        bool uncachedCampfire = RebirthUtilities.CheckForHeatUncached(world, pos, "campfire", 6, 3, 1);
        bool uncachedWoodstove = RebirthUtilities.CheckForHeatUncached(world, pos, "cntWoodBurningStove", 10, 5, 1);

        string deltaFinal = s_temperatureTrackHasLast ? FormatSignedFloat(temperature.Temperature - s_temperatureTrackLastFinalTemp) : "n/a";
        string deltaBase = s_temperatureTrackHasLast ? FormatSignedFloat(temperature.BaseTemperature - s_temperatureTrackLastBaseTemp) : "n/a";
        string deltaPenalty = s_temperatureTrackHasLast ? FormatSignedFloat(temperature.WeatherPenalty - s_temperatureTrackLastWeatherPenalty) : "n/a";
        string deltaHeat = s_temperatureTrackHasLast ? FormatSignedFloat(temperature.HeatBonus - s_temperatureTrackLastHeatBonus) : "n/a";
        string deltaAdjust = s_temperatureTrackHasLast ? FormatSignedFloat(temperature.EnclosedBiomeAdjustment - s_temperatureTrackLastEnclosedAdjust) : "n/a";

        s_temperatureTrackHasLast = true;
        s_temperatureTrackLastFinalTemp = temperature.Temperature;
        s_temperatureTrackLastBaseTemp = temperature.BaseTemperature;
        s_temperatureTrackLastWeatherPenalty = temperature.WeatherPenalty;
        s_temperatureTrackLastHeatBonus = temperature.HeatBonus;
        s_temperatureTrackLastEnclosedAdjust = temperature.EnclosedBiomeAdjustment;

        Log.Out("[AdvancedFarming TempTrack] label=" + label
            + " sample=" + s_temperatureTrackSampleIndex
            + " elapsedRealSeconds=" + elapsedRealSeconds
            + " pos=" + pos
            + " block=" + blockName
            + " worldTime=" + GameUtils.WorldTimeToString(worldTime)
            + " worldTimeRaw=" + worldTime
            + " day=" + day
            + " hour=" + hour
            + " minute=" + minute
            + " dawnHour=" + dawnHour
            + " duskHour=" + duskHour
            + " isDaytime=" + isDaytime
            + " isNight=" + temperature.IsNight
            + " finalTemp=" + temperature.Temperature.ToString("0.##")
            + " deltaFinal=" + deltaFinal
            + " baseTemp=" + temperature.BaseTemperature.ToString("0.##")
            + " deltaBase=" + deltaBase
            + " weatherManagerTemp=" + weatherManagerTemp.ToString("0.##")
            + " weatherPenalty=" + temperature.WeatherPenalty.ToString("0.##")
            + " deltaWeatherPenalty=" + deltaPenalty
            + " cloudPercent=" + cloudPercent.ToString("0.###")
            + " rainPercent=" + rainPercent.ToString("0.###")
            + " cloudPenalty=" + cloudPenalty.ToString("0.##")
            + " rainPenalty=" + rainPenalty.ToString("0.##")
            + " tempOverride=" + temperature.TemperatureOverride
            + " biome=" + temperature.BiomeName
            + " enclosed=" + temperature.Enclosed
            + " enclosedBiomeAdjust=" + temperature.EnclosedBiomeAdjustment.ToString("0.##")
            + " deltaEnclosedAdjust=" + deltaAdjust
            + " enclosedHotAdjust=" + temperature.EnclosedBiomeHotAdjustment.ToString("0.##")
            + " enclosedColdAdjust=" + temperature.EnclosedBiomeColdAdjustment.ToString("0.##")
            + " enclosedColdBlend=" + temperature.EnclosedBiomeColdBlend.ToString("0.###")
            + " enclosedAdjustMode=" + temperature.EnclosedBiomeAdjustmentMode
            + " configuredHotMiddayAdjust=" + hotAdjust.ToString("0.##")
            + " configuredColdMidnightAdjust=" + coldAdjust.ToString("0.##")
            + " heatSource=" + temperature.HeatSource
            + " heatBonus=" + temperature.HeatBonus.ToString("0.##")
            + " deltaHeat=" + deltaHeat
            + " cachedCampfire=" + temperature.HasCampfireHeat
            + " cachedWoodstove=" + temperature.HasWoodStoveHeat
            + " uncachedCampfire=" + uncachedCampfire
            + " uncachedWoodstove=" + uncachedWoodstove
            + " hasNaturalLight=" + hasNaturalLight
            + " hasSun=" + light.HasSunLight
            + " hasOpenSky=" + light.HasOpenSky
            + " blockLight=" + light.BlockLight
            + " lightMode=" + light.NaturalLightMode
            + " lightCacheHit=" + light.CacheHit
            + " lightCacheAge=" + light.CacheAgeSeconds
            + " lightCacheLifetime=" + light.CacheLifetimeSeconds
            + " thermalOpenToExterior=" + temperature.ThermalOpenToExterior
            + " thermalMode=" + temperature.ThermalExposureMode
            + " thermalExitPos=" + temperature.ThermalExitPos
            + " thermalBlockingPos=" + temperature.ThermalBlockingPos
            + " thermalBlockingBlock=" + temperature.ThermalBlockingBlock
            + " thermalNodes=" + temperature.ThermalNodesVisited);
    }

    private static string FormatSignedFloat(float value)
    {
        if (value > 0.0005f)
            return "+" + value.ToString("0.##");
        if (value < -0.0005f)
            return value.ToString("0.##");
        return "0";
    }

    private static void ExecuteHeatCache(List<string> args)
    {
        string action = args != null && args.Count > 1 ? (args[1] ?? string.Empty).Trim().ToLowerInvariant() : "status";

        if (action == "clear" || action == "reset" || action == "invalidate")
        {
            AdvancedFarmingHeatQueryService.ClearRegisteredHeatSources();
            Log.Out("[AdvancedFarming HeatCache] invalidated");
        }

        Log.Out(AdvancedFarmingHeatQueryService.BuildStatus());
        Log.Out(AdvancedFarmingWaterQueryService.BuildStatus());
    }


    private static void ExecuteFarmingSuite(List<string> args, CommandSenderInfo senderInfo)
    {
        string cycleText = "5400,60,10";
        int durationSeconds = 60;
        int intervalSeconds = 10;
        int radius = 12;
        float temperature = 30f;

        if (args != null && args.Count > 1 && !string.IsNullOrEmpty(args[1]))
            cycleText = args[1];
        if (args != null && args.Count > 2)
            int.TryParse(args[2], out durationSeconds);
        if (args != null && args.Count > 3)
            int.TryParse(args[3], out intervalSeconds);
        if (args != null && args.Count > 4)
            int.TryParse(args[4], out radius);
        if (args != null && args.Count > 5)
            float.TryParse(args[5], out temperature);

        if (durationSeconds < 5)
            durationSeconds = 5;
        if (durationSeconds > 600)
            durationSeconds = 600;

        if (intervalSeconds < 1)
            intervalSeconds = 1;
        if (intervalSeconds > durationSeconds)
            intervalSeconds = durationSeconds;

        if (radius < 1)
            radius = 1;
        if (radius > 64)
            radius = 64;

        int[] cycles = ParseCycleList(cycleText);
        if (cycles == null || cycles.Length == 0)
        {
            Log.Out("Usage: rbfarming farmsuite [cycles] [durationSeconds] [intervalSeconds] [radius] [temperature]");
            Log.Out("Example: rbfarming farmsuite 5400,60,10 60 10 12 30");
            return;
        }

        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (world == null)
        {
            Log.Out("[AdvancedFarming FarmSuite] No world loaded.");
            return;
        }

        Vector3i center = ResolveCommandCenter(world, senderInfo);

        lock (s_farmingSuiteLock)
        {
            if (s_farmingSuiteTimer != null)
            {
                s_farmingSuiteTimer.Dispose();
                s_farmingSuiteTimer = null;
            }

            StopAutoBench("[AdvancedFarming AutoBench] stopped by FarmSuite");

            s_farmingSuiteGrowthSeconds = cycles;
            s_farmingSuiteIndex = 0;
            s_farmingSuiteDurationSeconds = durationSeconds;
            s_farmingSuiteIntervalSeconds = intervalSeconds;
            s_farmingSuiteRadius = radius;
            s_farmingSuiteCenter = center;
            s_farmingSuiteRunning = true;

            Log.Out("[AdvancedFarming FarmSuite] started cycles=" + FormatCycleList(cycles)
                + " durationSeconds=" + durationSeconds
                + " intervalSeconds=" + intervalSeconds
                + " radius=" + radius
                + " temperature=" + temperature.ToString("0.##")
                + " center=" + center);

            Log.Out(AdvancedFarmingHeatQueryService.SetTemperatureOverride(temperature));
            AdvancedFarmingHeatQueryService.InvalidateAll("farmsuite start");
            LogFarmingSuitePatchValidation();
            Log.Out(AdvancedFarmingWaterProviderRegistry.BuildStatus());

            StartFarmingSuitePhaseLocked();
        }
    }

    private static int[] ParseCycleList(string cycleText)
    {
        if (string.IsNullOrEmpty(cycleText))
            return new int[] { 5400, 60, 10 };

        string lowered = cycleText.Trim().ToLowerInvariant();
        if (lowered == "all" || lowered == "default")
            return new int[] { 5400, 60, 10 };

        string[] parts = lowered.Split(',');
        List<int> values = new List<int>();
        for (int i = 0; i < parts.Length; i++)
        {
            string token = (parts[i] ?? string.Empty).Trim();
            if (token == "90m" || token == "90min" || token == "90minutes")
                token = "5400";
            else if (token.EndsWith("m"))
            {
                string minutesText = token.Substring(0, token.Length - 1);
                if (int.TryParse(minutesText, out int minutes))
                    token = (minutes * 60).ToString();
            }
            else if (token.EndsWith("s"))
                token = token.Substring(0, token.Length - 1);

            if (int.TryParse(token, out int seconds) && seconds > 0)
                values.Add(seconds);
        }

        return values.ToArray();
    }

    private static string FormatCycleList(int[] cycles)
    {
        if (cycles == null || cycles.Length == 0)
            return string.Empty;

        string text = string.Empty;
        for (int i = 0; i < cycles.Length; i++)
        {
            if (i > 0)
                text += ",";
            text += cycles[i];
        }

        return text;
    }

    private static void StartFarmingSuitePhaseLocked()
    {
        if (s_farmingSuiteGrowthSeconds == null || s_farmingSuiteIndex >= s_farmingSuiteGrowthSeconds.Length)
        {
            AdvancedFarmingPerfSnapshotService.SetSampling(false);
            s_farmingSuiteRunning = false;
            if (s_farmingSuiteTimer != null)
            {
                s_farmingSuiteTimer.Dispose();
                s_farmingSuiteTimer = null;
            }

            Log.Out("[AdvancedFarming FarmSuite] complete cycles=" + FormatCycleList(s_farmingSuiteGrowthSeconds));
            return;
        }

        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (world == null)
        {
            AdvancedFarmingPerfSnapshotService.SetSampling(false);
            s_farmingSuiteRunning = false;
            Log.Out("[AdvancedFarming FarmSuite] stopped: no world loaded.");
            return;
        }

        int growthSeconds = s_farmingSuiteGrowthSeconds[s_farmingSuiteIndex];
        AdvancedFarmingRuntimePolicy.SetGrowthSeconds(growthSeconds);
        AdvancedFarmingHeatQueryService.InvalidateAll("farmsuite phase start");

        Log.Out("[AdvancedFarming FarmSuite] phaseStart index=" + (s_farmingSuiteIndex + 1)
            + "/" + s_farmingSuiteGrowthSeconds.Length
            + " growthSeconds=" + growthSeconds
            + " center=" + s_farmingSuiteCenter
            + " radius=" + s_farmingSuiteRadius);

        LogFarmingSuitePatchValidation();
        LogFarmingSystemScan(world, s_farmingSuiteCenter, s_farmingSuiteRadius, "phaseStart growthSeconds=" + growthSeconds);
        LogWaterVerify(world, s_farmingSuiteCenter, s_farmingSuiteRadius, "phaseStart growthSeconds=" + growthSeconds);

        AdvancedFarmingPerfSnapshotService.ResetSampling();
        AdvancedFarmingPerfSnapshotService.SetSampling(true);
        s_farmingSuiteStartedTicks = DateTime.UtcNow.Ticks;
        s_farmingSuiteStartSnapshot = AdvancedFarmingPerfSnapshotService.GetSnapshot();
        s_farmingSuiteLastSnapshot = s_farmingSuiteStartSnapshot;

        if (s_farmingSuiteTimer != null)
        {
            s_farmingSuiteTimer.Dispose();
            s_farmingSuiteTimer = null;
        }

        s_farmingSuiteTimer = new Timer(FarmingSuiteTick, null, s_farmingSuiteIntervalSeconds * 1000, s_farmingSuiteIntervalSeconds * 1000);
    }

    private static void FarmingSuiteTick(object state)
    {
        lock (s_farmingSuiteLock)
        {
            if (!s_farmingSuiteRunning)
                return;

            long elapsedTicks = DateTime.UtcNow.Ticks - s_farmingSuiteStartedTicks;
            int elapsedSeconds = (int)(elapsedTicks / TimeSpan.TicksPerSecond);
            if (elapsedSeconds < 0)
                elapsedSeconds = 0;

            int growthSeconds = s_farmingSuiteGrowthSeconds[s_farmingSuiteIndex];
            AdvancedFarmingPerfSnapshotService.SampleSnapshot now = AdvancedFarmingPerfSnapshotService.GetSnapshot();

            Log.Out(AdvancedFarmingPerfSnapshotService.BuildSamplingDeltaReport(
                "suite growthSeconds=" + growthSeconds + " elapsedSeconds=" + elapsedSeconds + " interval",
                s_farmingSuiteLastSnapshot,
                now));

            s_farmingSuiteLastSnapshot = now;

            if (elapsedSeconds >= s_farmingSuiteDurationSeconds)
            {
                Log.Out(AdvancedFarmingPerfSnapshotService.BuildSamplingDeltaReport(
                    "suite growthSeconds=" + growthSeconds + " total",
                    s_farmingSuiteStartSnapshot,
                    now));

                Log.Out(AdvancedFarmingPerfSnapshotService.BuildSamplingReport());
                AdvancedFarmingPerfSnapshotService.SetSampling(false);

                World verifyWorld = GameManager.Instance != null ? GameManager.Instance.World : null;
                if (verifyWorld != null)
                    LogWaterVerify(verifyWorld, s_farmingSuiteCenter, s_farmingSuiteRadius, "phaseComplete growthSeconds=" + growthSeconds);

                s_farmingSuiteIndex++;
                StartFarmingSuitePhaseLocked();
            }
        }
    }

    private static void LogFarmingSystemScan(World world, Vector3i center, int radius, string label)
    {
        int cropCount = 0;
        int heatedCrops = 0;
        int unheatedCrops = 0;
        int lightPassCrops = 0;
        int lightFailCrops = 0;
        int waterPassCrops = 0;
        int waterFailCrops = 0;
        int tempPassCrops = 0;
        int tempFailCrops = 0;
        int farmPlots = 0;
        int farmPlotWaterTotal = 0;
        int waterTanks = 0;
        int waterTankWaterTotal = 0;
        int heatSourcesRegistered = 0;
        int storageFirstPriorityCandidates = 0;
        int storageFirstPriorityPass = 0;
        int storageFirstPriorityFail = 0;
        int printedCrops = 0;
        int maxPrintedCrops = 20;

        bool isNight = GameManager.Instance != null && GameManager.Instance.World != null && !GameManager.Instance.World.IsDaytime();

        for (int x = center.x - radius; x <= center.x + radius; x++)
        {
            for (int z = center.z - radius; z <= center.z + radius; z++)
            {
                for (int y = center.y - 5; y <= center.y + 5; y++)
                {
                    Vector3i pos = new Vector3i(x, y, z);
                    BlockValue blockValue = world.GetBlock(pos);
                    string blockName = blockValue.Block != null ? blockValue.Block.GetBlockName() : string.Empty;

                    TileEntityFarmPlotRebirth plot = world.GetTileEntity(pos) as TileEntityFarmPlotRebirth;
                    if (plot != null)
                    {
                        farmPlots++;
                        farmPlotWaterTotal += plot.waterCount;
                    }

                    TileEntityWaterTankRebirth tank = world.GetTileEntity(pos) as TileEntityWaterTankRebirth;
                    if (tank != null)
                    {
                        waterTanks++;
                        waterTankWaterTotal += tank.waterCount;
                    }

                    if (!IsAdvancedFarmingCropName(blockName))
                        continue;

                    cropCount++;

                    AdvancedFarmingLightService.LightResult light = AdvancedFarmingLightService.Evaluate(
                        world,
                        pos,
                        3,
                        3,
                        false,
                        AdvancedFarmingRuntimePolicy.Enabled);

                    bool hasSunLight = light.HasSunLight;
                    bool hasBlockLight = light.HasBlockLight;
                    bool hasOpenSky = light.HasOpenSky;
                    bool lightPass = (((hasSunLight || hasOpenSky) && !isNight) || hasBlockLight);
                    if (lightPass)
                        lightPassCrops++;
                    else
                        lightFailCrops++;

                    AdvancedFarmingTemperatureService.TemperatureResult temperature = AdvancedFarmingTemperatureService.Evaluate(
                        world,
                        pos,
                        hasSunLight || hasOpenSky,
                        AdvancedFarmingRuntimePolicy.Enabled);
                    bool registryCampfire = temperature.HasCampfireHeat;
                    bool registryStove = temperature.HasWoodStoveHeat;
                    bool heatPass = registryCampfire || registryStove;
                    if (heatPass)
                    {
                        heatedCrops++;
                        heatSourcesRegistered++;
                    }
                    else
                        unheatedCrops++;

                    float temp = temperature.Temperature;

                    bool tempPass = temp >= 45f;
                    if (tempPass)
                        tempPassCrops++;
                    else
                        tempFailCrops++;

                    int waterInTank = RebirthUtilities.CheckForWater(world, pos, "FuriousRamsayWaterTank", 4, 1, 0);
                    int waterInCollector = RebirthUtilities.CheckForWater(world, pos, "FuriousRamsayDewCollector", 4, 1, 0);
                    TileEntityFarmPlotRebirth belowPlot = world.GetTileEntity(pos + Vector3i.down) as TileEntityFarmPlotRebirth;
                    int farmPlotWater = belowPlot != null ? belowPlot.waterCount : 0;
                    bool waterPass = waterInTank > 0 || waterInCollector > 0 || farmPlotWater > 0;
                    if (waterPass)
                        waterPassCrops++;
                    else
                        waterFailCrops++;

                    bool hasStorageWater = waterInTank > 0 || waterInCollector > 0;
                    if (farmPlotWater > 0 && hasStorageWater)
                    {
                        storageFirstPriorityCandidates++;
                        AdvancedFarmingWaterProviderKind selectedKind = AdvancedFarmingWaterProviderRegistry.SelectConsumptionProvider(world, pos, 1);
                        if (selectedKind == AdvancedFarmingWaterProviderKind.WaterTank || selectedKind == AdvancedFarmingWaterProviderKind.DewCollector)
                            storageFirstPriorityPass++;
                        else
                            storageFirstPriorityFail++;
                    }

                    if (printedCrops < maxPrintedCrops || !lightPass || !waterPass || !tempPass)
                    {
                        Log.Out("[AdvancedFarming FarmSuite] crop label=" + label
                            + " pos=" + pos
                            + " block=" + blockName
                            + " lightPass=" + lightPass
                            + " waterPass=" + waterPass
                            + " tempPass=" + tempPass
                            + " temp=" + temp.ToString("0.##")
                            + " biome=" + temperature.BiomeName
                            + " enclosed=" + temperature.Enclosed
                            + " enclosedBiomeAdjust=" + temperature.EnclosedBiomeAdjustment.ToString("0.##")
                            + " enclosedHotAdjust=" + temperature.EnclosedBiomeHotAdjustment.ToString("0.##")
                            + " enclosedColdAdjust=" + temperature.EnclosedBiomeColdAdjustment.ToString("0.##")
                            + " enclosedColdBlend=" + temperature.EnclosedBiomeColdBlend.ToString("0.###")
                            + " enclosedAdjustMode=" + temperature.EnclosedBiomeAdjustmentMode
                            + " heatSource=" + temperature.HeatSource
                            + " registryCampfire=" + registryCampfire
                            + " registryStove=" + registryStove
                            + " tankWater=" + waterInTank
                            + " collectorWater=" + waterInCollector
                            + " farmPlotWater=" + farmPlotWater);
                        printedCrops++;
                    }
                }
            }
        }

        Log.Out("[AdvancedFarming FarmSuite] systemScan label=" + label
            + " center=" + center
            + " radius=" + radius
            + " crops=" + cropCount
            + " farmPlots=" + farmPlots
            + " farmPlotWaterTotal=" + farmPlotWaterTotal
            + " waterTanks=" + waterTanks
            + " waterTankWaterTotal=" + waterTankWaterTotal
            + " heatedCrops=" + heatedCrops
            + " unheatedCrops=" + unheatedCrops
            + " lightPassCrops=" + lightPassCrops
            + " lightFailCrops=" + lightFailCrops
            + " waterPassCrops=" + waterPassCrops
            + " waterFailCrops=" + waterFailCrops
            + " tempPassCrops=" + tempPassCrops
            + " tempFailCrops=" + tempFailCrops
            + " heatChecksPositive=" + heatSourcesRegistered
            + " storageFirstPriorityCandidates=" + storageFirstPriorityCandidates
            + " storageFirstPriorityPass=" + storageFirstPriorityPass
            + " storageFirstPriorityFail=" + storageFirstPriorityFail);

        Log.Out(AdvancedFarmingHeatQueryService.BuildStatus());
        Log.Out(AdvancedFarmingWaterQueryService.BuildStatus());
    }


    
    private sealed class CropEnvironmentDiagnosticRow
    {
        public Vector3i Pos;
        public string Line;
        public bool LightPass;
        public bool NaturalPath;
        public bool Enclosed;
        public bool ThermalOpenToExterior;
        public string ThermalExposureMode;
        public byte CropSunLight;
        public int RequiredNaturalSunlight;
    }

    private static void ExecuteCropEnvironmentDiagnostic(List<string> args, CommandSenderInfo senderInfo)
    {
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (world == null)
        {
            Log.Out("[AdvancedFarming EnvDiag] No world loaded.");
            return;
        }

        AdvancedFarmingLightService.ClearCache();
        AdvancedFarmingTemperatureService.ClearCache();

        Vector3i explicitPos;
        if (TryParseVectorArgs(args, 1, out explicitPos))
        {
            Log.Out(RebirthAdvancedFarmingPatchInstaller.Status());
            Log.Out(AdvancedFarmingLightService.BuildStatus());
            LogCropEnvironmentDiagnostic(world, explicitPos, "explicit");
            return;
        }

        int radius = 8;
        string mode = "fail";
        if (args != null && args.Count > 1)
        {
            int parsedRadius;
            if (int.TryParse(args[1], out parsedRadius))
                radius = parsedRadius;
            else
                mode = args[1];
        }

        if (args != null && args.Count > 2 && !string.IsNullOrEmpty(args[2]))
            mode = args[2];

        if (radius < 1)
            radius = 1;
        if (radius > 32)
            radius = 32;

        bool printAll = string.Equals(mode, "all", StringComparison.OrdinalIgnoreCase);
        bool printPassOnly = string.Equals(mode, "pass", StringComparison.OrdinalIgnoreCase)
            || string.Equals(mode, "passes", StringComparison.OrdinalIgnoreCase);
        bool printFailOnly = string.Equals(mode, "fail", StringComparison.OrdinalIgnoreCase)
            || string.Equals(mode, "fails", StringComparison.OrdinalIgnoreCase)
            || string.Equals(mode, "failed", StringComparison.OrdinalIgnoreCase);

        Vector3i center = ResolveCommandCenter(world, senderInfo);
        Log.Out("[AdvancedFarming EnvDiag] center=" + center + " radius=" + radius + " mode=" + mode);
        Log.Out(RebirthAdvancedFarmingPatchInstaller.Status());
        Log.Out(AdvancedFarmingLightService.BuildStatus());

        List<CropEnvironmentDiagnosticRow> failing = new List<CropEnvironmentDiagnosticRow>(64);
        List<CropEnvironmentDiagnosticRow> passing = new List<CropEnvironmentDiagnosticRow>(64);

        int found = 0;
        int naturalPathFail = 0;
        int enclosed = 0;

        for (int x = center.x - radius; x <= center.x + radius; x++)
        {
            for (int z = center.z - radius; z <= center.z + radius; z++)
            {
                for (int y = center.y - 5; y <= center.y + 5; y++)
                {
                    Vector3i pos = new Vector3i(x, y, z);
                    BlockValue blockValue = world.GetBlock(pos);
                    string blockName = blockValue.Block != null ? blockValue.Block.GetBlockName() : string.Empty;
                    if (!IsAdvancedFarmingCropName(blockName))
                        continue;

                    found++;
                    CropEnvironmentDiagnosticRow row = BuildCropEnvironmentDiagnosticRow(world, pos, "scan");
                    if (!row.NaturalPath)
                        naturalPathFail++;
                    if (row.Enclosed)
                        enclosed++;

                    if (row.LightPass)
                        passing.Add(row);
                    else
                        failing.Add(row);
                }
            }
        }

        int printed = 0;
        int maxFailPrint = printAll ? int.MaxValue : 120;
        int maxPassPrint = printAll ? int.MaxValue : 24;

        if (!printPassOnly)
        {
            for (int i = 0; i < failing.Count && i < maxFailPrint; i++)
            {
                Log.Out(failing[i].Line);
                printed++;
            }
        }

        if (!printFailOnly)
        {
            for (int i = 0; i < passing.Count && i < maxPassPrint; i++)
            {
                Log.Out(passing[i].Line);
                printed++;
            }
        }
        else if (failing.Count == 0)
        {
            int samples = Math.Min(passing.Count, 12);
            for (int i = 0; i < samples; i++)
            {
                Log.Out(passing[i].Line);
                printed++;
            }
        }

        Log.Out("[AdvancedFarming EnvDiag] cropCount=" + found
            + " lightFail=" + failing.Count
            + " lightPass=" + passing.Count
            + " naturalPathFail=" + naturalPathFail
            + " enclosed=" + enclosed
            + " printed=" + printed
            + " mode=" + mode);

        if (failing.Count > printed && !printAll)
        {
            Log.Out("[AdvancedFarming EnvDiag] output truncated failing=" + failing.Count
                + " printed=" + printed
                + " rerun with: rbfarming cropenv " + radius + " all");
        }
    }

    private static void LogCropEnvironmentDiagnostic(World world, Vector3i pos, string label)
    {
        Log.Out(BuildCropEnvironmentDiagnosticRow(world, pos, label).Line);
    }

    private static CropEnvironmentDiagnosticRow BuildCropEnvironmentDiagnosticRow(World world, Vector3i pos, string label)
    {
        BlockValue blockValue = world.GetBlock(pos);
        string blockName = blockValue.Block != null ? blockValue.Block.GetBlockName() : "<null>";
        bool isMushroom = blockName.IndexOf("mushroom", System.StringComparison.OrdinalIgnoreCase) >= 0;
        bool advancedFarming = AdvancedFarmingRuntimePolicy.Enabled;

        AdvancedFarmingLightService.LightResult light = AdvancedFarmingLightService.Evaluate(
            world,
            pos,
            3,
            3,
            isMushroom,
            advancedFarming);

        bool isNight = world != null && !world.IsDaytime();
        bool lightPass = isMushroom || (((light.HasSunLight || light.HasOpenSky) && !isNight) || light.HasBlockLight);
        bool hasNaturalLight = light.HasSunLight || light.HasOpenSky;
        AdvancedFarmingTemperatureService.TemperatureResult temperature = AdvancedFarmingTemperatureService.Evaluate(
            world,
            pos,
            hasNaturalLight,
            advancedFarming);

        CropEnvironmentDiagnosticRow row = new CropEnvironmentDiagnosticRow();
        row.Pos = pos;
        row.LightPass = lightPass;
        row.NaturalPath = light.NaturalLightPathOpen;
        row.Enclosed = temperature.Enclosed;
        row.ThermalOpenToExterior = temperature.ThermalOpenToExterior;
        row.ThermalExposureMode = temperature.ThermalExposureMode;
        row.CropSunLight = light.CropSunLight;
        row.RequiredNaturalSunlight = light.RequiredNaturalSunlight;

        row.Line = "[AdvancedFarming EnvDiag] label=" + label
            + " pos=" + pos
            + " block=" + blockName
            + " isMushroom=" + isMushroom
            + " lightPass=" + lightPass
            + " isNight=" + isNight
            + " rawSun=" + light.RawHasSunLight
            + " rawOpenSky=" + light.RawHasOpenSky
            + " sun=" + light.SunLight
            + " cropSun=" + light.CropSunLight
            + " requiredLight=" + light.RequiredLightLevel
            + " requiredNaturalSunlight=" + light.RequiredNaturalSunlight
            + " naturalLightStrongEnough=" + light.NaturalLightStrongEnough
            + " vanillaAmountEnclosed=" + light.VanillaAmountEnclosed.ToString("0.###")
            + " vanillaEnclosed=" + light.VanillaEnclosed
            + " geometryBlockerOverride=" + light.GeometryBlockerOverride
            + " blockLight=" + light.BlockLight
            + " hasSun=" + light.HasSunLight
            + " hasOpenSky=" + light.HasOpenSky
            + " hasBlockLight=" + light.HasBlockLight
            + " naturalPath=" + light.NaturalLightPathOpen
            + " lightMode=" + light.NaturalLightMode
            + " lightCacheHit=" + light.CacheHit
            + " lightCacheAge=" + light.CacheAgeSeconds
            + " lightCacheLifetime=" + light.CacheLifetimeSeconds
            + " exposureNodes=" + light.ExposureNodesVisited
            + " transparentCandidates=" + light.ExposureTransparentCandidates
            + " transparentAir=" + light.ExposureAirTransparentCandidates
            + " transparentNonAir=" + light.ExposureNonAirTransparentCandidates
            + " exitPos=" + light.ExitPos
            + " blockerPos=" + light.BlockingPos
            + " blockerBlock=" + light.BlockingBlock
            + " temp=" + temperature.Temperature.ToString("0.##")
            + " baseTemp=" + temperature.BaseTemperature.ToString("0.##")
            + " weatherPenalty=" + temperature.WeatherPenalty.ToString("0.##")
            + " heatSource=" + temperature.HeatSource
            + " heatBonus=" + temperature.HeatBonus.ToString("0.##")
            + " biome=" + temperature.BiomeName
            + " enclosed=" + temperature.Enclosed
            + " thermalOpenToExterior=" + temperature.ThermalOpenToExterior
            + " thermalMode=" + temperature.ThermalExposureMode
            + " thermalExitPos=" + temperature.ThermalExitPos
            + " thermalBlockingPos=" + temperature.ThermalBlockingPos
            + " thermalBlockingBlock=" + temperature.ThermalBlockingBlock
            + " thermalNodes=" + temperature.ThermalNodesVisited
            + " enclosedBiomeAdjust=" + temperature.EnclosedBiomeAdjustment.ToString("0.##")
            + " enclosedHotAdjust=" + temperature.EnclosedBiomeHotAdjustment.ToString("0.##")
            + " enclosedColdAdjust=" + temperature.EnclosedBiomeColdAdjustment.ToString("0.##")
            + " enclosedColdBlend=" + temperature.EnclosedBiomeColdBlend.ToString("0.###")
            + " enclosedAdjustMode=" + temperature.EnclosedBiomeAdjustmentMode;

        return row;
    }

    private static bool TryParseVectorArgs(List<string> args, int startIndex, out Vector3i pos)
    {
        pos = Vector3i.zero;
        if (args == null || args.Count <= startIndex)
            return false;

        if (args.Count > startIndex + 2)
        {
            int x;
            int y;
            int z;
            if (int.TryParse(args[startIndex], out x)
                && int.TryParse(args[startIndex + 1], out y)
                && int.TryParse(args[startIndex + 2], out z))
            {
                pos = new Vector3i(x, y, z);
                return true;
            }
        }

        string single = args[startIndex];
        if (string.IsNullOrEmpty(single) || single.IndexOf(',') < 0)
            return false;

        string[] parts = single.Split(',');
        if (parts == null || parts.Length != 3)
            return false;

        int sx;
        int sy;
        int sz;
        if (!int.TryParse(parts[0].Trim(), out sx)
            || !int.TryParse(parts[1].Trim(), out sy)
            || !int.TryParse(parts[2].Trim(), out sz))
        {
            return false;
        }

        pos = new Vector3i(sx, sy, sz);
        return true;
    }


    private static void ExecuteLightAutoDebug(List<string> args, CommandSenderInfo senderInfo)
    {
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (world == null)
        {
            Log.Out("[AdvancedFarming LightDebug] No world loaded.");
            return;
        }

        AdvancedFarmingLightService.ClearCache();
        AdvancedFarmingTemperatureService.ClearCache();

        Vector3i explicitPos;
        if (TryParseVectorArgs(args, 1, out explicitPos))
        {
            int explicitSamples = 12;
            int sampleIndex = args != null && args.Count > 4 ? 4 : 2;
            if (args != null && args.Count > sampleIndex)
            {
                int parsedSamples;
                if (int.TryParse(args[sampleIndex], out parsedSamples))
                    explicitSamples = parsedSamples;
            }

            if (explicitSamples < 1)
                explicitSamples = 1;
            if (explicitSamples > 32)
                explicitSamples = 32;

            Log.Out("[AdvancedFarming LightDebug] explicit pos=" + explicitPos + " samples=" + explicitSamples);
            Log.Out(RebirthAdvancedFarmingPatchInstaller.Status());
            Log.Out(AdvancedFarmingLightService.BuildStatus());
            LogCropLightAutoDebug(world, explicitPos, "explicit", explicitSamples);
            return;
        }

        int radius = 10;
        int samples = 6;
        if (args != null && args.Count > 1)
        {
            int parsedRadius;
            if (int.TryParse(args[1], out parsedRadius))
                radius = parsedRadius;
        }
        if (args != null && args.Count > 2)
        {
            int parsedSamples;
            if (int.TryParse(args[2], out parsedSamples))
                samples = parsedSamples;
        }

        if (radius < 1)
            radius = 1;
        if (radius > 32)
            radius = 32;
        if (samples < 1)
            samples = 1;
        if (samples > 16)
            samples = 16;

        Vector3i center = ResolveCommandCenter(world, senderInfo);
        Log.Out("[AdvancedFarming LightDebug] center=" + center + " radius=" + radius + " samples=" + samples);
        Log.Out(RebirthAdvancedFarmingPatchInstaller.Status());
        Log.Out(AdvancedFarmingLightService.BuildStatus());

        List<CropEnvironmentDiagnosticRow> failing = new List<CropEnvironmentDiagnosticRow>(64);
        List<CropEnvironmentDiagnosticRow> passing = new List<CropEnvironmentDiagnosticRow>(64);
        int found = 0;
        int naturalPathFail = 0;
        int enclosed = 0;

        for (int x = center.x - radius; x <= center.x + radius; x++)
        {
            for (int z = center.z - radius; z <= center.z + radius; z++)
            {
                for (int y = center.y - 5; y <= center.y + 5; y++)
                {
                    Vector3i pos = new Vector3i(x, y, z);
                    BlockValue blockValue = world.GetBlock(pos);
                    string blockName = blockValue.Block != null ? blockValue.Block.GetBlockName() : string.Empty;
                    if (!IsAdvancedFarmingCropName(blockName))
                        continue;

                    found++;
                    CropEnvironmentDiagnosticRow row = BuildCropEnvironmentDiagnosticRow(world, pos, "scan");
                    if (!row.NaturalPath)
                        naturalPathFail++;
                    if (row.Enclosed)
                        enclosed++;

                    if (row.LightPass)
                        passing.Add(row);
                    else
                        failing.Add(row);
                }
            }
        }

        Log.Out("[AdvancedFarming LightDebug] summary cropCount=" + found
            + " lightFail=" + failing.Count
            + " lightPass=" + passing.Count
            + " naturalPathFail=" + naturalPathFail
            + " enclosed=" + enclosed);

        int debugged = 0;
        for (int i = 0; i < failing.Count && debugged < samples; i++)
        {
            LogCropLightAutoDebug(world, failing[i].Pos, "fail" + debugged, samples);
            debugged++;
        }

        if (debugged == 0)
        {
            int passSamples = Math.Min(samples, passing.Count);
            Log.Out("[AdvancedFarming LightDebug] no failing crops found; printing passing samples=" + passSamples);
            for (int i = 0; i < passSamples; i++)
            {
                LogCropLightAutoDebug(world, passing[i].Pos, "pass" + i, Math.Min(samples, 6));
            }
        }
    }

    private static void LogCropLightAutoDebug(World world, Vector3i cropPos, string label, int samples)
    {
        Log.Out(BuildCropEnvironmentDiagnosticRow(world, cropPos, label).Line);

        AdvancedFarmingLightService.LightSearchDiagnostic search = AdvancedFarmingLightService.BuildSearchDiagnostic(world, cropPos, samples, samples);
        Log.Out("[AdvancedFarming LightDebug] search label=" + label
            + " crop=" + cropPos
            + " start=" + search.StartPos
            + " startTransparent=" + search.StartTransparent
            + " startBlock=" + search.StartBlock
            + " radius=" + search.SearchHorizontalRadius
            + " up=" + search.SearchUp
            + " sourceColumns=" + search.ColumnsChecked
            + " openSources=" + search.OpenColumns
            + " blockedSources=" + search.BlockedColumns);

        if (search.OpenColumnSamples != null)
        {
            for (int i = 0; i < search.OpenColumnSamples.Count; i++)
            {
                AdvancedFarmingLightService.LightSearchColumnEntry entry = search.OpenColumnSamples[i];
                Log.Out("[AdvancedFarming LightDebug] openSource label=" + label
                    + " root=" + entry.RootPos
                    + " dx=" + entry.Dx
                    + " dz=" + entry.Dz
                    + " dist=" + entry.ChebyshevDistance
                    + " air=" + entry.AirCount
                    + " nonAirTransparent=" + entry.NonAirTransparentCount
                    + " nonAirOpaque=" + entry.NonAirOpaqueCount);
            }
        }

        if (search.BlockedColumnSamples != null)
        {
            for (int i = 0; i < search.BlockedColumnSamples.Count; i++)
            {
                AdvancedFarmingLightService.LightSearchColumnEntry entry = search.BlockedColumnSamples[i];
                Log.Out("[AdvancedFarming LightDebug] blockedSource label=" + label
                    + " root=" + entry.RootPos
                    + " dx=" + entry.Dx
                    + " dz=" + entry.Dz
                    + " dist=" + entry.ChebyshevDistance
                    + " firstBlockerPos=" + entry.FirstBlockerPos
                    + " firstBlockerBlock=" + entry.FirstBlockerBlock
                    + " air=" + entry.AirCount
                    + " nonAirTransparent=" + entry.NonAirTransparentCount
                    + " nonAirOpaque=" + entry.NonAirOpaqueCount);
            }
        }

        AdvancedFarmingLightService.LightColumnDiagnostic column = AdvancedFarmingLightService.BuildColumnDiagnostic(world, cropPos);
        LogLightColumnDiagnostic(column, "sourceProbeRoot=" + cropPos + " label=" + label, 24);
    }

    private static void LogLightColumnDiagnostic(AdvancedFarmingLightService.LightColumnDiagnostic diagnostic, string label, int maxEntries)
    {
        Log.Out("[AdvancedFarming LightColumn] " + label
            + " root=" + diagnostic.RootPos
            + " open=" + diagnostic.Open
            + " air=" + diagnostic.AirCount
            + " nonAirTransparent=" + diagnostic.NonAirTransparentCount
            + " nonAirOpaque=" + diagnostic.NonAirOpaqueCount
            + " firstBlockerPos=" + diagnostic.FirstBlockerPos
            + " firstBlockerBlock=" + diagnostic.FirstBlockerBlock);

        List<AdvancedFarmingLightService.LightColumnEntry> entries = diagnostic.Entries;
        if (entries == null || entries.Count == 0)
        {
            Log.Out("[AdvancedFarming LightColumn] " + label + " no non-air blocks above root.");
            return;
        }

        int printed = 0;
        for (int i = 0; i < entries.Count; i++)
        {
            AdvancedFarmingLightService.LightColumnEntry entry = entries[i];
            Log.Out("[AdvancedFarming LightColumn] " + label
                + " y=" + entry.Pos.y
                + " pos=" + entry.Pos
                + " block=" + entry.BlockName
                + " class=" + entry.ClassName
                + " isAir=" + entry.IsAir
                + " isWater=" + entry.IsWater
                + " isChild=" + entry.IsChild
                + " lightOpacity=" + entry.LightOpacity
                + " effectiveLightOpacity=" + entry.EffectiveLightOpacity
                + " shapeOpacityPolicy=" + entry.ShapeOpacityPolicy
                + " isSeeThrough=" + entry.IsSeeThrough
                + " isMovementBlocked=" + entry.IsMovementBlocked
                + " materialIsPlant=" + entry.MaterialIsPlant
                + " rule=" + entry.Rule
                + " doorDecision=" + entry.DoorDecision
                + " verdict=" + (entry.Transparent ? "TRANSPARENT" : "BLOCKER"));

            printed++;
            if (printed >= maxEntries)
            {
                Log.Out("[AdvancedFarming LightColumn] " + label + " output truncated entries=" + entries.Count + " printed=" + printed);
                break;
            }
        }
    }



    private static void ExecuteLightColumnDiagnostic(List<string> args)
    {
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (world == null)
        {
            Log.Out("[AdvancedFarming LightColumn] No world loaded.");
            return;
        }

        Vector3i pos;
        if (!TryParseVectorArgs(args, 1, out pos))
        {
            Log.Out("Usage: rbfarming lightcolumn <x> <y> <z>");
            return;
        }

        AdvancedFarmingLightService.ClearCache();
        AdvancedFarmingTemperatureService.ClearCache();
        AdvancedFarmingLightService.LightColumnDiagnostic diagnostic = AdvancedFarmingLightService.BuildColumnDiagnostic(world, pos);

        LogLightColumnDiagnostic(diagnostic, "explicit", 80);
    }

    private static void ExecuteBlockForensics(List<string> args)
    {
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (world == null)
        {
            Log.Out("[AdvancedFarming BlockForensics] No world loaded.");
            return;
        }

        Vector3i pos;
        if (!TryParseVectorArgs(args, 1, out pos))
        {
            Log.Out("Usage: rbfarming blockforensics <x> <y> <z>");
            return;
        }

        Log.Out(AdvancedFarmingLightService.BuildBlockForensics(world, pos));
    }

    private static void ExecuteDoorLightIssueDiagnostic(CommandSenderInfo senderInfo)
    {
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (world == null)
        {
            Log.Out("[AdvancedFarming DoorLightIssue] No world loaded.");
            return;
        }

        Vector3i center = ResolveCommandCenter(world, senderInfo);
        const int horizontalRadius = 24;
        const int below = 6;
        const int above = 12;
        const int maxDoorsToPrint = 64;
        const int maxCropsToPrint = 64;

        Log.Out("[AdvancedFarming DoorLightIssue] center=" + center
            + " scanRadius=" + horizontalRadius
            + " yRange=" + (center.y - below) + ".." + (center.y + above)
            + " noArgs=true mergedDoorLightPatch=v95_visualRefreshIgnoresFalseRenderingOn");
        Log.Out(RebirthAdvancedFarmingPatchInstaller.Status());
        Log.Out(AdvancedFarmingLightService.BuildStatus());
        Log.Out(AdvancedFarmingDynamicLightOpacityService.BuildStatus());

        AdvancedFarmingLightService.ClearCache();
        AdvancedFarmingTemperatureService.ClearCache();
        AdvancedFarmingDynamicLightOpacityService.ProcessAllPendingSunlightRefreshesForDiagnostics();

        List<Vector3i> doorPositions = ScanDoorLightCandidates(world, center, horizontalRadius, below, above);
        List<Vector3i> windowGlassPositions = ScanWindowGlassLightCandidates(world, center, horizontalRadius, below, above);
        List<Vector3i> allCropPositions = ScanAdvancedFarmingCrops(world, center, horizontalRadius, below, above);
        DoorLightStructureRegion structureRegion = DetectCobblestoneStructureRegion(world, center, horizontalRadius, below, above);
        List<Vector3i> cropPositions = FilterCropsToCobblestoneStructure(allCropPositions, structureRegion);
        if (cropPositions.Count == 0 && !structureRegion.Found)
            cropPositions = allCropPositions;

        LogDoorLightStructureFilterReport(world, center, structureRegion, allCropPositions, cropPositions, "beforeRefresh");
        LogDoorLightSurfaceAudit(world, center, structureRegion, doorPositions, cropPositions, "beforeRefresh", 96);
        LogWindowGlassLightTemperatureAudit(world, center, windowGlassPositions, cropPositions, structureRegion, "beforeRefresh", 64);
        LogDoorRotationStructureCropTest(world, center, doorPositions, cropPositions, structureRegion, "beforeRefresh");
        Log.Out("[AdvancedFarming DoorLightIssue] candidates beforeRefresh coverCandidates=" + doorPositions.Count
            + " crops=" + cropPositions.Count
            + " allCrops=" + allCropPositions.Count
            + " structureFiltered=" + structureRegion.Found);

        LogDoorLightCandidateReports(world, center, doorPositions, maxDoorsToPrint, "beforeRefresh");
        LogDoorLightFocusedCropReport(world, center, cropPositions, doorPositions, "beforeRefresh");
        LogDoorLightCropReports(world, cropPositions, doorPositions, maxCropsToPrint, "beforeRefresh");
        LogDoorLightStructureSummary(world, center, doorPositions, cropPositions, "beforeRefresh");

        // Force the same local SUN recompute the door-state hook is supposed to trigger.
        // The before/after comparison tells us whether the problem is bad geometry masks or
        // stale chunk SUN values after an open/close/rotation state change.
        AdvancedFarmingDynamicLightOpacityService.RefreshSunlightAround(center, horizontalRadius + 2);
        AdvancedFarmingDynamicLightOpacityService.ProcessAllPendingSunlightRefreshesForDiagnostics();
        AdvancedFarmingLightService.ClearCache();
        AdvancedFarmingTemperatureService.ClearCache();

        Log.Out("[AdvancedFarming DoorLightIssue] afterRefresh begin");
        LogDoorLightStructureFilterReport(world, center, structureRegion, allCropPositions, cropPositions, "afterRefresh");
        LogDoorLightSurfaceAudit(world, center, structureRegion, doorPositions, cropPositions, "afterRefresh", 96);
        LogWindowGlassLightTemperatureAudit(world, center, windowGlassPositions, cropPositions, structureRegion, "afterRefresh", 64);
        LogDoorRotationStructureCropTest(world, center, doorPositions, cropPositions, structureRegion, "afterRefresh");
        LogDoorLightCandidateReports(world, center, doorPositions, maxDoorsToPrint, "afterRefresh");
        LogDoorLightFocusedCropReport(world, center, cropPositions, doorPositions, "afterRefresh");
        LogDoorLightCropReports(world, cropPositions, doorPositions, maxCropsToPrint, "afterRefresh");
        LogDoorLightStructureSummary(world, center, doorPositions, cropPositions, "afterRefresh");

        Log.Out("[AdvancedFarming DoorLightIssue] renderRefreshProbe skipped reason=Reflection-based object-graph and private-member discovery was removed. Typed SUN/BLOCK, geometry, crop, window, door, and temperature diagnostics above remain active.");

        Log.Out("[AdvancedFarming DoorLightIssue] done doorsPrinted=" + Math.Min(doorPositions.Count, maxDoorsToPrint)
            + " cropsPrinted=" + Math.Min(cropPositions.Count, maxCropsToPrint)
            + " coverCandidates=" + doorPositions.Count
            + " windowGlassCandidates=" + windowGlassPositions.Count
            + " cropCandidates=" + cropPositions.Count
            + " allCropCandidates=" + allCropPositions.Count
            + " structureFiltered=" + structureRegion.Found);
    }


    private struct DoorLightSurfaceSample
    {
        public Vector3i Pos;
        public string Role;
        public string BlockName;
        public string ClassName;
        public bool IsAir;
        public bool IsDoor;
        public bool IsCrop;
        public int Sun;
        public int BlockLight;
        public int MaxSun3x3x3;
        public int MaxBlock3x3x3;
        public Vector3i MaxSunPos;
        public Vector3i MaxBlockPos;
        public byte Rotation;
        public byte Meta;
        public byte Meta2;
        public bool IsChild;
        public int LightOpacity;
    }

    private struct DoorLightSurfaceStats
    {
        public string Role;
        public int Count;
        public int LitCount;
        public int SunCount;
        public int BlockCount;
        public int MinSun;
        public int MaxSun;
        public int SumSun;
        public int MinBlock;
        public int MaxBlock;
        public int SumBlock;
        public int NeighborSunCount;
        public int NeighborBlockCount;

        public void Init(string role)
        {
            Role = role;
            Count = 0;
            LitCount = 0;
            SunCount = 0;
            BlockCount = 0;
            MinSun = 255;
            MaxSun = 0;
            SumSun = 0;
            MinBlock = 255;
            MaxBlock = 0;
            SumBlock = 0;
            NeighborSunCount = 0;
            NeighborBlockCount = 0;
        }

        public void Add(DoorLightSurfaceSample sample)
        {
            Count++;
            if (sample.Sun > 0 || sample.BlockLight > 0) LitCount++;
            if (sample.Sun > 0) SunCount++;
            if (sample.BlockLight > 0) BlockCount++;
            if (sample.Sun < MinSun) MinSun = sample.Sun;
            if (sample.Sun > MaxSun) MaxSun = sample.Sun;
            SumSun += sample.Sun;
            if (sample.BlockLight < MinBlock) MinBlock = sample.BlockLight;
            if (sample.BlockLight > MaxBlock) MaxBlock = sample.BlockLight;
            SumBlock += sample.BlockLight;
            if (sample.MaxSun3x3x3 > sample.Sun) NeighborSunCount++;
            if (sample.MaxBlock3x3x3 > sample.BlockLight) NeighborBlockCount++;
        }
    }

    private static void LogDoorLightSurfaceAudit(World world, Vector3i center, DoorLightStructureRegion region, List<Vector3i> doorPositions, List<Vector3i> cropPositions, string label, int maxSamplesToPrint)
    {
        if (world == null)
            return;

        if (!region.Found)
        {
            Log.Out("[AdvancedFarming DoorLightIssueSurfaceAudit] label=" + label
                + " center=" + center
                + " structureFound=false note=Cannot classify floor/wall/ceiling surfaces without a detected connected cobblestone structure.");
            return;
        }

        List<DoorLightSurfaceSample> samples = BuildDoorLightSurfaceSamples(world, center, region, doorPositions, cropPositions);
        Log.Out("[AdvancedFarming DoorLightIssueSurfaceAudit] label=" + label
            + " center=" + center
            + " structureBounds=" + region.Min + ".." + region.Max
            + " sampleCount=" + samples.Count
            + " maxPrinted=" + maxSamplesToPrint
            + " purpose=Compares actual SUN/BLOCK values on floor, wall, ceiling, cover, crop, and interior-air samples inside the detected cobblestone structure. Cover candidates include doors, trapdoors, hatches, shutters, curtains, drapes, blinds, and centered non-glass cover plates. If these are dark but visuals are bright, the issue is render/material/ambient rather than gameplay light.");

        LogDoorLightSurfaceRoleSummary(samples, label, "floor");
        LogDoorLightSurfaceRoleSummary(samples, label, "wall");
        LogDoorLightSurfaceRoleSummary(samples, label, "ceiling");
        LogDoorLightSurfaceRoleSummary(samples, label, "cover");
        LogDoorLightSurfaceRoleSummary(samples, label, "crop");
        LogDoorLightSurfaceRoleSummary(samples, label, "interiorAir");

        SortSurfaceSamplesForPrint(samples, center);
        int printed = 0;
        for (int i = 0; i < samples.Count && printed < maxSamplesToPrint; i++)
        {
            DoorLightSurfaceSample sample = samples[i];
            Log.Out("[AdvancedFarming DoorLightIssueSurfaceAuditSample] label=" + label
                + " role=" + sample.Role
                + " pos=" + sample.Pos
                + " block=" + sample.BlockName
                + " class=" + sample.ClassName
                + " isAir=" + sample.IsAir
                + " isDoor=" + sample.IsDoor
                + " isCrop=" + sample.IsCrop
                + " sun=" + sample.Sun
                + " blockLight=" + sample.BlockLight
                + " maxSun3x3x3=" + sample.MaxSun3x3x3
                + " maxSunPos=" + sample.MaxSunPos
                + " maxBlock3x3x3=" + sample.MaxBlock3x3x3
                + " maxBlockPos=" + sample.MaxBlockPos
                + " lightOpacity=" + sample.LightOpacity
                + " rot=" + sample.Rotation
                + " meta=" + sample.Meta
                + " meta2=" + sample.Meta2
                + " child=" + sample.IsChild
                + " note=Use sun/blockLight to separate real light-map brightness from model/material/ambient brightness.");
            printed++;
        }

        if (samples.Count > printed)
        {
            Log.Out("[AdvancedFarming DoorLightIssueSurfaceAudit] label=" + label
                + " outputTruncated=true samples=" + samples.Count
                + " printed=" + printed);
        }
    }

    private static List<DoorLightSurfaceSample> BuildDoorLightSurfaceSamples(World world, Vector3i center, DoorLightStructureRegion region, List<Vector3i> doorPositions, List<Vector3i> cropPositions)
    {
        List<DoorLightSurfaceSample> samples = new List<DoorLightSurfaceSample>(256);
        Dictionary<string, bool> added = new Dictionary<string, bool>(256);

        int minX = region.Min.x;
        int maxX = region.Max.x;
        int minZ = region.Min.z;
        int maxZ = region.Max.z;
        int interiorMinX = minX + 1;
        int interiorMaxX = maxX - 1;
        int interiorMinZ = minZ + 1;
        int interiorMaxZ = maxZ - 1;
        int minY = region.Min.y - 2;
        int maxY = region.Max.y + 1;

        if (interiorMinX > interiorMaxX || interiorMinZ > interiorMaxZ)
            return samples;

        int floorY = GuessStructureFloorY(world, center, region, cropPositions);
        int eyeY = center.y;
        if (eyeY < minY) eyeY = minY;
        if (eyeY > maxY) eyeY = maxY;
        int cropY = GuessCropY(cropPositions, center.y);

        for (int x = interiorMinX; x <= interiorMaxX; x++)
        {
            for (int z = interiorMinZ; z <= interiorMaxZ; z++)
            {
                AddSurfaceSample(samples, added, world, new Vector3i(x, floorY, z), "floor");
                AddSurfaceSample(samples, added, world, new Vector3i(x, floorY + 1, z), "interiorAir");
                AddSurfaceSample(samples, added, world, new Vector3i(x, cropY, z), "interiorAir");
                AddSurfaceSample(samples, added, world, new Vector3i(x, region.Max.y, z), "ceiling");
                AddSurfaceSample(samples, added, world, new Vector3i(x, region.Max.y - 1, z), "interiorAir");
            }
        }

        for (int y = region.Min.y; y <= region.Max.y; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                AddSurfaceSample(samples, added, world, new Vector3i(x, y, minZ), "wall");
                AddSurfaceSample(samples, added, world, new Vector3i(x, y, maxZ), "wall");
            }
            for (int z = minZ + 1; z <= maxZ - 1; z++)
            {
                AddSurfaceSample(samples, added, world, new Vector3i(minX, y, z), "wall");
                AddSurfaceSample(samples, added, world, new Vector3i(maxX, y, z), "wall");
            }
        }

        if (doorPositions != null)
        {
            for (int i = 0; i < doorPositions.Count; i++)
            {
                Vector3i pos = doorPositions[i];
                if (pos.x >= region.Min.x - 1 && pos.x <= region.Max.x + 1
                    && pos.z >= region.Min.z - 1 && pos.z <= region.Max.z + 1
                    && pos.y >= minY && pos.y <= maxY)
                    AddSurfaceSample(samples, added, world, pos, "cover");
            }
        }

        if (cropPositions != null)
        {
            for (int i = 0; i < cropPositions.Count; i++)
                AddSurfaceSample(samples, added, world, cropPositions[i], "crop");
        }

        return samples;
    }

    private static void AddSurfaceSample(List<DoorLightSurfaceSample> samples, Dictionary<string, bool> added, World world, Vector3i pos, string role)
    {
        if (samples == null || added == null || world == null)
            return;

        string key = role + ":" + BuildPositionKey(pos);
        if (added.ContainsKey(key))
            return;

        added[key] = true;
        samples.Add(BuildDoorLightSurfaceSample(world, pos, role));
    }

    private static DoorLightSurfaceSample BuildDoorLightSurfaceSample(World world, Vector3i pos, string role)
    {
        DoorLightSurfaceSample sample = new DoorLightSurfaceSample();
        sample.Pos = pos;
        sample.Role = role;

        ChunkCluster chunkCluster = world != null ? world.ChunkCache : null;
        BlockValue value = world != null ? world.GetBlock(pos) : BlockValue.Air;
        Block block = value.Block;
        sample.BlockName = GetDebugBlockName(value);
        sample.ClassName = block != null ? block.GetType().Name : "<null>";
        sample.IsAir = value.isair;
        sample.IsDoor = block != null && IsDoorLightCandidate(block);
        sample.IsCrop = block != null && IsAdvancedFarmingCropBlock(block);
        sample.Sun = GetLightSafe(chunkCluster, pos, Chunk.LIGHT_TYPE.SUN);
        sample.BlockLight = GetLightSafe(chunkCluster, pos, Chunk.LIGHT_TYPE.BLOCK);
        sample.MaxSun3x3x3 = sample.Sun;
        sample.MaxBlock3x3x3 = sample.BlockLight;
        sample.MaxSunPos = pos;
        sample.MaxBlockPos = pos;
        sample.Rotation = value.rotation;
        sample.Meta = value.meta;
        sample.Meta2 = value.meta2;
        sample.IsChild = value.ischild;
        sample.LightOpacity = block != null ? block.lightOpacity : 0;

        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dz = -1; dz <= 1; dz++)
                {
                    Vector3i p = pos + new Vector3i(dx, dy, dz);
                    int sun = GetLightSafe(chunkCluster, p, Chunk.LIGHT_TYPE.SUN);
                    int blockLight = GetLightSafe(chunkCluster, p, Chunk.LIGHT_TYPE.BLOCK);
                    if (sun > sample.MaxSun3x3x3)
                    {
                        sample.MaxSun3x3x3 = sun;
                        sample.MaxSunPos = p;
                    }
                    if (blockLight > sample.MaxBlock3x3x3)
                    {
                        sample.MaxBlock3x3x3 = blockLight;
                        sample.MaxBlockPos = p;
                    }
                }
            }
        }

        return sample;
    }

    private static void LogDoorLightSurfaceRoleSummary(List<DoorLightSurfaceSample> samples, string label, string role)
    {
        DoorLightSurfaceStats stats = new DoorLightSurfaceStats();
        stats.Init(role);
        if (samples != null)
        {
            for (int i = 0; i < samples.Count; i++)
            {
                if (samples[i].Role == role)
                    stats.Add(samples[i]);
            }
        }

        string avgSun = stats.Count > 0 ? ((float)stats.SumSun / stats.Count).ToString("0.00") : "<none>";
        string avgBlock = stats.Count > 0 ? ((float)stats.SumBlock / stats.Count).ToString("0.00") : "<none>";
        Log.Out("[AdvancedFarming DoorLightIssueSurfaceAuditSummary] label=" + label
            + " role=" + role
            + " count=" + stats.Count
            + " lit=" + stats.LitCount
            + " sunCells=" + stats.SunCount
            + " blockCells=" + stats.BlockCount
            + " minSun=" + (stats.Count > 0 ? stats.MinSun.ToString() : "<none>")
            + " maxSun=" + (stats.Count > 0 ? stats.MaxSun.ToString() : "<none>")
            + " avgSun=" + avgSun
            + " minBlock=" + (stats.Count > 0 ? stats.MinBlock.ToString() : "<none>")
            + " maxBlock=" + (stats.Count > 0 ? stats.MaxBlock.ToString() : "<none>")
            + " avgBlock=" + avgBlock
            + " neighborHasHigherSun=" + stats.NeighborSunCount
            + " neighborHasHigherBlock=" + stats.NeighborBlockCount);
    }

    private static int GuessStructureFloorY(World world, Vector3i center, DoorLightStructureRegion region, List<Vector3i> cropPositions)
    {
        if (cropPositions != null && cropPositions.Count > 0)
        {
            int bestY = cropPositions[0].y;
            for (int i = 1; i < cropPositions.Count; i++)
            {
                if (cropPositions[i].y < bestY)
                    bestY = cropPositions[i].y;
            }
            return bestY - 1;
        }

        int x = center.x;
        int z = center.z;
        if (x <= region.Min.x) x = region.Min.x + 1;
        if (x >= region.Max.x) x = region.Max.x - 1;
        if (z <= region.Min.z) z = region.Min.z + 1;
        if (z >= region.Max.z) z = region.Max.z - 1;

        for (int y = center.y; y >= region.Min.y - 3; y--)
        {
            BlockValue value = world.GetBlock(new Vector3i(x, y, z));
            if (!value.isair && value.Block != null)
                return y;
        }

        return center.y - 1;
    }

    private static int GuessCropY(List<Vector3i> cropPositions, int fallbackY)
    {
        if (cropPositions == null || cropPositions.Count == 0)
            return fallbackY;

        int bestY = cropPositions[0].y;
        for (int i = 1; i < cropPositions.Count; i++)
        {
            if (cropPositions[i].y < bestY)
                bestY = cropPositions[i].y;
        }
        return bestY;
    }

    private static bool IsAdvancedFarmingCropBlock(Block block)
    {
        if (block == null)
            return false;

        string blockName = block.GetBlockName() ?? string.Empty;
        return ContainsIgnoreCase(blockName, "farm")
            || ContainsIgnoreCase(blockName, "crop")
            || ContainsIgnoreCase(blockName, "plant")
            || ContainsIgnoreCase(blockName, "seed")
            || ContainsIgnoreCase(blockName, "mushroom");
    }

    private static void SortSurfaceSamplesForPrint(List<DoorLightSurfaceSample> samples, Vector3i center)
    {
        if (samples == null || samples.Count <= 1)
            return;

        samples.Sort(delegate (DoorLightSurfaceSample a, DoorLightSurfaceSample b)
        {
            int roleCompare = GetSurfaceRolePriority(a.Role).CompareTo(GetSurfaceRolePriority(b.Role));
            if (roleCompare != 0)
                return roleCompare;
            return DistanceSquared(a.Pos, center).CompareTo(DistanceSquared(b.Pos, center));
        });
    }

    private static int GetSurfaceRolePriority(string role)
    {
        if (role == "cover") return 0;
        if (role == "crop") return 1;
        if (role == "floor") return 2;
        if (role == "interiorAir") return 3;
        if (role == "wall") return 4;
        if (role == "ceiling") return 5;
        return 99;
    }


    private static void LogWindowGlassLightTemperatureAudit(World world, Vector3i center, List<Vector3i> windowGlassPositions, List<Vector3i> cropPositions, DoorLightStructureRegion structureRegion, string label, int maxWindowsToPrint)
    {
        int windowCount = windowGlassPositions != null ? windowGlassPositions.Count : 0;
        int cropCount = cropPositions != null ? cropPositions.Count : 0;
        Log.Out("[AdvancedFarming WindowGlassLightAudit] label=" + label
            + " center=" + center
            + " windowGlassCandidates=" + windowCount
            + " structureFound=" + structureRegion.Found
            + " structureBounds=" + (structureRegion.Found ? structureRegion.Min + ".." + structureRegion.Max : "<none>")
            + " structureCrops=" + cropCount
            + " purpose=First-class audit for intact/broken windows, glass, centered non-glass cover plates, and centered/window-shaped window/glass cover plates as natural-light and temperature enclosure candidates.");

        if (windowCount == 0)
        {
            Log.Out("[AdvancedFarming WindowGlassLightAudit] label=" + label
                + " noWindowGlassCandidates=true note=Current scan did not find window/glass blocks, centered non-glass cover plates, or centered/window-shaped window/glass cover plates, excluding curtain/drape/blind diagnostic covers, shutters, and door-like covers.");
            LogWindowGlassCropTemperatureLinks(world, center, windowGlassPositions, cropPositions, label, 96);
            return;
        }

        int count = Math.Min(windowCount, Math.Max(0, maxWindowsToPrint));
        for (int i = 0; i < count; i++)
        {
            Vector3i pos = windowGlassPositions[i];
            LogWindowGlassBlockAudit(world, center, pos, label, i);
        }

        if (windowCount > count)
        {
            Log.Out("[AdvancedFarming WindowGlassLightAudit] label=" + label
                + " outputTruncated windowGlassCandidates=" + windowCount
                + " printed=" + count);
        }

        LogWindowGlassCropTemperatureLinks(world, center, windowGlassPositions, cropPositions, label, 96);
    }

    private static void LogWindowGlassBlockAudit(World world, Vector3i center, Vector3i pos, string label, int index)
    {
        BlockValue value = world.GetBlock(pos);
        Block block = value.Block;
        string blockName = block != null ? block.GetBlockName() : "<null>";
        string className = block != null ? block.GetType().Name : "<null>";
        ChunkCluster chunkCluster = world != null ? world.ChunkCache : null;
        int sun = chunkCluster != null ? chunkCluster.GetLight(pos, Chunk.LIGHT_TYPE.SUN) : -1;
        int blockLight = chunkCluster != null ? chunkCluster.GetLight(pos, Chunk.LIGHT_TYPE.BLOCK) : -1;

        Log.Out("[AdvancedFarming WindowGlassLightAuditBlock] label=" + label
            + " index=" + index
            + " pos=" + pos
            + " distSq=" + DistanceSquared(pos, center)
            + " block=" + blockName
            + " class=" + className
            + " windowLike=" + IsWindowGlassLightCandidate(block)
            + " curtainBlind=" + IsCurtainBlindBlock(block)
            + " shutterLike=" + IsShutterLikeBlock(block)
            + " centeredNonGlass=" + IsCenteredNonGlassCoverBlock(block)
            + " centeredWindowGlass=" + IsCenteredWindowGlassCoverBlock(block)
            + " isDoorCandidate=" + IsDoorLightCandidate(block)
            + " isAir=" + value.isair
            + " child=" + value.ischild
            + " rot=" + value.rotation
            + " meta=" + value.meta
            + " meta2=" + value.meta2
            + " lightOpacity=" + (block != null ? block.lightOpacity.ToString() : "<null>")
            + " shapeOpacityPolicy=" + BuildGeneratedShapePolicySummary(block)
            + " isSeeThrough=" + SafeIsSeeThrough(world, pos, block, value)
            + " sun=" + sun
            + " blockLight=" + blockLight
            + " edgeUp=" + BuildEdgeOpacitySummary(chunkCluster, pos + Vector3i.up, pos)
            + " edgeDown=" + BuildEdgeOpacitySummary(chunkCluster, pos + Vector3i.down, pos)
            + " edgeNorth=" + BuildEdgeOpacitySummary(chunkCluster, pos + new Vector3i(0, 0, 1), pos)
            + " edgeSouth=" + BuildEdgeOpacitySummary(chunkCluster, pos + new Vector3i(0, 0, -1), pos)
            + " edgeEast=" + BuildEdgeOpacitySummary(chunkCluster, pos + Vector3i.right, pos)
            + " edgeWest=" + BuildEdgeOpacitySummary(chunkCluster, pos + Vector3i.left, pos));

        Log.Out(AdvancedFarmingLightService.BuildBlockForensics(world, pos));
        LogLightColumnDiagnostic(AdvancedFarmingLightService.BuildColumnDiagnostic(world, pos + Vector3i.down), "windowglass:" + label + ":belowWindow", 24);
    }

    private static void LogWindowGlassCropTemperatureLinks(World world, Vector3i center, List<Vector3i> windowGlassPositions, List<Vector3i> cropPositions, string label, int maxCropsToPrint)
    {
        if (cropPositions == null || cropPositions.Count == 0)
        {
            Log.Out("[AdvancedFarming WindowTemperatureLink] label=" + label
                + " structureCrops=0 note=No crop candidates available for window/glass temperature link.");
            return;
        }

        int lightPass = 0;
        int enclosed = 0;
        int naturalPath = 0;
        int printed = 0;
        int count = Math.Min(cropPositions.Count, Math.Max(0, maxCropsToPrint));

        for (int i = 0; i < cropPositions.Count; i++)
        {
            Vector3i cropPos = cropPositions[i];
            CropEnvironmentDiagnosticRow row = BuildCropEnvironmentDiagnosticRow(world, cropPos, "windowglass:" + label + ":crop");
            if (row.LightPass) lightPass++;
            if (row.Enclosed) enclosed++;
            if (row.NaturalPath) naturalPath++;

            if (printed >= count)
                continue;

            Vector3i nearestWindow = FindNearestPosition(windowGlassPositions, cropPos, out int nearestDistSq);
            Log.Out("[AdvancedFarming WindowTemperatureLink] label=" + label
                + " index=" + i
                + " cropPos=" + row.Pos
                + " lightPass=" + row.LightPass
                + " naturalPath=" + row.NaturalPath
                + " enclosed=" + row.Enclosed
                + " thermalOpenToExterior=" + row.ThermalOpenToExterior
                + " thermalMode=" + row.ThermalExposureMode
                + " cropSun=" + row.CropSunLight
                + " requiredNaturalSunlight=" + row.RequiredNaturalSunlight
                + " nearestWindowGlass=" + (nearestDistSq >= 0 ? nearestWindow.ToString() : "<none>")
                + " nearestWindowGlassDistSq=" + (nearestDistSq >= 0 ? nearestDistSq.ToString() : "<none>")
                + " note=Temperature.Enclosed now uses the thermal enclosure search. Normal intact glass/window seals temperature; broken/damaged glass/window can open temperature. Centered non-glass and centered/window-shaped glass/window covers follow their placement-specific pass/block light state for thermal openness.");
            printed++;
        }

        Log.Out("[AdvancedFarming WindowTemperatureSummary] label=" + label
            + " structureCrops=" + cropPositions.Count
            + " lightPass=" + lightPass
            + " lightFail=" + (cropPositions.Count - lightPass)
            + " naturalPath=" + naturalPath
            + " enclosed=" + enclosed
            + " windowGlassCandidates=" + (windowGlassPositions != null ? windowGlassPositions.Count : 0)
            + " printed=" + printed);
    }

    private static Vector3i FindNearestPosition(List<Vector3i> positions, Vector3i center, out int bestDistSq)
    {
        bestDistSq = -1;
        Vector3i best = Vector3i.zero;
        if (positions == null || positions.Count == 0)
            return best;

        for (int i = 0; i < positions.Count; i++)
        {
            int distSq = DistanceSquared(positions[i], center);
            if (bestDistSq < 0 || distSq < bestDistSq)
            {
                bestDistSq = distSq;
                best = positions[i];
            }
        }

        return best;
    }

    private static void LogDoorRotationStructureCropTest(World world, Vector3i center, List<Vector3i> doorPositions, List<Vector3i> cropPositions, DoorLightStructureRegion structureRegion, string label)
    {
        Vector3i nearestDoor = Vector3i.zero;
        bool hasDoor = doorPositions != null && doorPositions.Count > 0;
        if (hasDoor)
            nearestDoor = doorPositions[0];

        Log.Out("[AdvancedFarming DoorRotationTest] label=" + label
            + " center=" + center
            + " nearestDoorFound=" + hasDoor
            + " nearestDoor=" + (hasDoor ? nearestDoor.ToString() : "<none>")
            + " nearestDoorDistSq=" + (hasDoor ? DistanceSquared(nearestDoor, center).ToString() : "<none>")
            + " doorCandidates=" + (doorPositions != null ? doorPositions.Count : 0)
            + " structureFound=" + structureRegion.Found
            + " structureBounds=" + (structureRegion.Found ? structureRegion.Min + ".." + structureRegion.Max : "<none>")
            + " structureCropCount=" + (cropPositions != null ? cropPositions.Count : 0)
            + " purpose=single-row summary for one placed door rotation/open-state test");

        if (hasDoor)
        {
            Log.Out(AdvancedFarmingLightService.BuildDoorStateDiagnostic(world, nearestDoor, label + ":nearestDoor"));
            LogLightColumnDiagnostic(AdvancedFarmingLightService.BuildColumnDiagnostic(world, nearestDoor), "doorrotationtest:" + label + ":nearestDoorColumn", 18);
            LogLightColumnDiagnostic(AdvancedFarmingLightService.BuildColumnDiagnostic(world, nearestDoor + Vector3i.down), "doorrotationtest:" + label + ":belowNearestDoorColumn", 18);
        }

        LogDoorRotationStructureCropSummary(world, cropPositions, label);
        LogDoorRotationStructureCropSamples(world, cropPositions, label, 96);
    }

    private static void LogDoorRotationStructureCropSummary(World world, List<Vector3i> cropPositions, string label)
    {
        if (cropPositions == null || cropPositions.Count == 0)
        {
            Log.Out("[AdvancedFarming DoorRotationTestCropSummary] label=" + label
                + " structureCrops=0 lightPass=0 lightFail=0 naturalPath=0 enclosed=0 minCropSun=<none> maxCropSun=<none> avgCropSun=<none>");
            return;
        }

        int lightPass = 0;
        int naturalPath = 0;
        int enclosed = 0;
        int thermalOpen = 0;
        int minCropSun = 255;
        int maxCropSun = 0;
        int sumCropSun = 0;
        int minRequiredNatural = 255;
        int maxRequiredNatural = 0;
        int strongEnough = 0;

        for (int i = 0; i < cropPositions.Count; i++)
        {
            CropEnvironmentDiagnosticRow row = BuildCropEnvironmentDiagnosticRow(world, cropPositions[i], "doorrotationtest:" + label + ":summary");
            if (row.LightPass) lightPass++;
            if (row.NaturalPath) naturalPath++;
            if (row.Enclosed) enclosed++;
            if (row.ThermalOpenToExterior) thermalOpen++;
            if (row.CropSunLight < minCropSun) minCropSun = row.CropSunLight;
            if (row.CropSunLight > maxCropSun) maxCropSun = row.CropSunLight;
            sumCropSun += row.CropSunLight;
            if (row.RequiredNaturalSunlight < minRequiredNatural) minRequiredNatural = row.RequiredNaturalSunlight;
            if (row.RequiredNaturalSunlight > maxRequiredNatural) maxRequiredNatural = row.RequiredNaturalSunlight;
            if (row.CropSunLight >= row.RequiredNaturalSunlight) strongEnough++;
        }

        Log.Out("[AdvancedFarming DoorRotationTestCropSummary] label=" + label
            + " structureCrops=" + cropPositions.Count
            + " lightPass=" + lightPass
            + " lightFail=" + (cropPositions.Count - lightPass)
            + " naturalPath=" + naturalPath
            + " enclosed=" + enclosed
            + " thermalOpenToExterior=" + thermalOpen
            + " thermalSealed=" + (cropPositions.Count - thermalOpen)
            + " naturalLightStrongEnoughByCropSun=" + strongEnough
            + " minCropSun=" + minCropSun
            + " maxCropSun=" + maxCropSun
            + " avgCropSun=" + ((float)sumCropSun / (float)cropPositions.Count).ToString("0.###")
            + " minRequiredNaturalSunlight=" + minRequiredNatural
            + " maxRequiredNaturalSunlight=" + maxRequiredNatural);
    }

    private static void LogDoorRotationStructureCropSamples(World world, List<Vector3i> cropPositions, string label, int maxPrint)
    {
        if (cropPositions == null || cropPositions.Count == 0)
            return;

        int count = Math.Min(cropPositions.Count, Math.Max(0, maxPrint));
        for (int i = 0; i < count; i++)
        {
            CropEnvironmentDiagnosticRow row = BuildCropEnvironmentDiagnosticRow(world, cropPositions[i], "doorrotationtest:" + label + ":structureCrop");
            Log.Out("[AdvancedFarming DoorRotationTestCrop] label=" + label
                + " index=" + i
                + " pos=" + row.Pos
                + " lightPass=" + row.LightPass
                + " naturalPath=" + row.NaturalPath
                + " enclosed=" + row.Enclosed
                + " thermalOpenToExterior=" + row.ThermalOpenToExterior
                + " thermalMode=" + row.ThermalExposureMode
                + " cropSun=" + row.CropSunLight
                + " requiredNaturalSunlight=" + row.RequiredNaturalSunlight);
        }

        if (cropPositions.Count > count)
        {
            Log.Out("[AdvancedFarming DoorRotationTestCrop] label=" + label
                + " outputTruncated structureCrops=" + cropPositions.Count
                + " printed=" + count);
        }
    }
    private struct DoorLightStructureRegion
    {
        public bool Found;
        public Vector3i Min;
        public Vector3i Max;
        public int BlockCount;
        public int SeedDistanceSquared;
        public Vector3i Seed;
    }

    private static DoorLightStructureRegion DetectCobblestoneStructureRegion(World world, Vector3i center, int horizontalRadius, int below, int above)
    {
        DoorLightStructureRegion result = new DoorLightStructureRegion();
        if (world == null)
            return result;

        Dictionary<string, Vector3i> cobblestoneBlocks = new Dictionary<string, Vector3i>(256);
        int minY = center.y - below;
        int maxY = center.y + above;

        for (int x = center.x - horizontalRadius; x <= center.x + horizontalRadius; x++)
        {
            for (int z = center.z - horizontalRadius; z <= center.z + horizontalRadius; z++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    Vector3i pos = new Vector3i(x, y, z);
                    BlockValue value = world.GetBlock(pos);
                    if (value.isair || value.Block == null)
                        continue;

                    if (!IsCobblestoneStructureBlock(value.Block))
                        continue;

                    string key = BuildPositionKey(pos);
                    if (!cobblestoneBlocks.ContainsKey(key))
                        cobblestoneBlocks.Add(key, pos);
                }
            }
        }

        if (cobblestoneBlocks.Count == 0)
            return result;

        Vector3i seed = Vector3i.zero;
        int bestDistance = int.MaxValue;
        foreach (KeyValuePair<string, Vector3i> entry in cobblestoneBlocks)
        {
            int distance = DistanceSquared(entry.Value, center);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                seed = entry.Value;
            }
        }

        Dictionary<string, bool> visited = new Dictionary<string, bool>(cobblestoneBlocks.Count);
        Queue<Vector3i> queue = new Queue<Vector3i>();
        string seedKey = BuildPositionKey(seed);
        visited[seedKey] = true;
        queue.Enqueue(seed);

        Vector3i min = seed;
        Vector3i max = seed;
        int count = 0;

        while (queue.Count > 0)
        {
            Vector3i pos = queue.Dequeue();
            count++;
            if (pos.x < min.x) min.x = pos.x;
            if (pos.y < min.y) min.y = pos.y;
            if (pos.z < min.z) min.z = pos.z;
            if (pos.x > max.x) max.x = pos.x;
            if (pos.y > max.y) max.y = pos.y;
            if (pos.z > max.z) max.z = pos.z;

            EnqueueCobblestoneNeighbor(cobblestoneBlocks, visited, queue, pos + new Vector3i(1, 0, 0));
            EnqueueCobblestoneNeighbor(cobblestoneBlocks, visited, queue, pos + new Vector3i(-1, 0, 0));
            EnqueueCobblestoneNeighbor(cobblestoneBlocks, visited, queue, pos + new Vector3i(0, 1, 0));
            EnqueueCobblestoneNeighbor(cobblestoneBlocks, visited, queue, pos + new Vector3i(0, -1, 0));
            EnqueueCobblestoneNeighbor(cobblestoneBlocks, visited, queue, pos + new Vector3i(0, 0, 1));
            EnqueueCobblestoneNeighbor(cobblestoneBlocks, visited, queue, pos + new Vector3i(0, 0, -1));
        }

        result.Found = count >= 4;
        result.Min = min;
        result.Max = max;
        result.BlockCount = count;
        result.SeedDistanceSquared = bestDistance;
        result.Seed = seed;
        return result;
    }

    private static void EnqueueCobblestoneNeighbor(Dictionary<string, Vector3i> cobblestoneBlocks, Dictionary<string, bool> visited, Queue<Vector3i> queue, Vector3i pos)
    {
        string key = BuildPositionKey(pos);
        if (visited.ContainsKey(key))
            return;

        if (!cobblestoneBlocks.ContainsKey(key))
            return;

        visited[key] = true;
        queue.Enqueue(pos);
    }

    private static List<Vector3i> FilterCropsToCobblestoneStructure(List<Vector3i> cropPositions, DoorLightStructureRegion region)
    {
        List<Vector3i> filtered = new List<Vector3i>(cropPositions != null ? cropPositions.Count : 0);
        if (cropPositions == null || cropPositions.Count == 0)
            return filtered;

        if (!region.Found)
            return filtered;

        for (int i = 0; i < cropPositions.Count; i++)
        {
            Vector3i pos = cropPositions[i];
            if (IsPositionInsideCobblestoneStructure(pos, region))
                filtered.Add(pos);
        }

        SortPositionsByDistance(filtered, new Vector3i((region.Min.x + region.Max.x) / 2, (region.Min.y + region.Max.y) / 2, (region.Min.z + region.Max.z) / 2));
        return filtered;
    }

    private static bool IsPositionInsideCobblestoneStructure(Vector3i pos, DoorLightStructureRegion region)
    {
        if (!region.Found)
            return false;

        return pos.x > region.Min.x
            && pos.x < region.Max.x
            && pos.z > region.Min.z
            && pos.z < region.Max.z
            && pos.y >= region.Min.y - 2
            && pos.y <= region.Max.y + 1;
    }

    private static void LogDoorLightStructureFilterReport(World world, Vector3i center, DoorLightStructureRegion region, List<Vector3i> allCropPositions, List<Vector3i> filteredCropPositions, string label)
    {
        Log.Out("[AdvancedFarming DoorLightIssueStructureFilter] label=" + label
            + " center=" + center
            + " found=" + region.Found
            + " seed=" + region.Seed
            + " seedDistSq=" + region.SeedDistanceSquared
            + " cobblestoneBlocks=" + region.BlockCount
            + " bounds=" + (region.Found ? region.Min + ".." + region.Max : "<none>")
            + " interiorX=" + (region.Found ? (region.Min.x + 1).ToString() + ".." + (region.Max.x - 1).ToString() : "<none>")
            + " interiorZ=" + (region.Found ? (region.Min.z + 1).ToString() + ".." + (region.Max.z - 1).ToString() : "<none>")
            + " yRange=" + (region.Found ? (region.Min.y - 2).ToString() + ".." + (region.Max.y + 1).ToString() : "<none>")
            + " allCrops=" + (allCropPositions != null ? allCropPositions.Count : 0)
            + " structureCrops=" + (filteredCropPositions != null ? filteredCropPositions.Count : 0)
            + " note=DoorLightIssue crop reports are limited to crops inside this detected cobblestone structure when found.");

        if (region.Found && (filteredCropPositions == null || filteredCropPositions.Count == 0))
        {
            Log.Out("[AdvancedFarming DoorLightIssueStructureFilter] label=" + label
                + " warning=No advanced-farming crops were inside the detected cobblestone interior. Stand inside the structure before running the command or verify the structure walls are connected cobblestone blocks.");
        }
    }

    private static bool IsCobblestoneStructureBlock(Block block)
    {
        if (block == null)
            return false;

        string blockName = block.GetBlockName() ?? string.Empty;
        return ContainsIgnoreCase(blockName, "cobblestone")
            || ContainsIgnoreCase(blockName, "cobble");
    }

    private static string BuildPositionKey(Vector3i pos)
    {
        return pos.x.ToString() + ":" + pos.y.ToString() + ":" + pos.z.ToString();
    }

    private static List<Vector3i> ScanDoorLightCandidates(World world, Vector3i center, int horizontalRadius, int below, int above)
    {
        List<Vector3i> results = new List<Vector3i>(32);
        int minY = center.y - below;
        int maxY = center.y + above;

        for (int x = center.x - horizontalRadius; x <= center.x + horizontalRadius; x++)
        {
            for (int z = center.z - horizontalRadius; z <= center.z + horizontalRadius; z++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    Vector3i pos = new Vector3i(x, y, z);
                    BlockValue value = world.GetBlock(pos);
                    if (value.isair || value.Block == null)
                        continue;

                    if (IsDoorLightCandidate(value.Block))
                        results.Add(pos);
                }
            }
        }

        SortPositionsByDistance(results, center);
        return results;
    }

    private static List<Vector3i> ScanWindowGlassLightCandidates(World world, Vector3i center, int horizontalRadius, int below, int above)
    {
        List<Vector3i> results = new List<Vector3i>(32);
        int minY = center.y - below;
        int maxY = center.y + above;

        for (int x = center.x - horizontalRadius; x <= center.x + horizontalRadius; x++)
        {
            for (int z = center.z - horizontalRadius; z <= center.z + horizontalRadius; z++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    Vector3i pos = new Vector3i(x, y, z);
                    BlockValue value = world.GetBlock(pos);
                    if (value.isair || value.Block == null)
                        continue;

                    if (IsWindowGlassLightCandidate(value.Block) || IsCenteredNonGlassCoverBlock(value.Block))
                        results.Add(pos);
                }
            }
        }

        SortPositionsByDistance(results, center);
        return results;
    }

    private static List<Vector3i> ScanAdvancedFarmingCrops(World world, Vector3i center, int horizontalRadius, int below, int above)
    {
        List<Vector3i> results = new List<Vector3i>(32);
        int minY = center.y - below;
        int maxY = center.y + above;

        for (int x = center.x - horizontalRadius; x <= center.x + horizontalRadius; x++)
        {
            for (int z = center.z - horizontalRadius; z <= center.z + horizontalRadius; z++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    Vector3i pos = new Vector3i(x, y, z);
                    BlockValue value = world.GetBlock(pos);
                    string blockName = value.Block != null ? value.Block.GetBlockName() : string.Empty;
                    if (IsAdvancedFarmingCropName(blockName))
                        results.Add(pos);
                }
            }
        }

        SortPositionsByDistance(results, center);
        return results;
    }

    private static void LogDoorLightCandidateReports(World world, Vector3i center, List<Vector3i> positions, int maxPrint, string label)
    {
        if (positions == null || positions.Count == 0)
        {
            Log.Out("[AdvancedFarming DoorLightIssue] label=" + label + " no door/trapdoor/hatch/shutter/curtain/drape/blind cover candidates near player.");
            return;
        }

        int count = Math.Min(positions.Count, maxPrint);
        for (int i = 0; i < count; i++)
        {
            Vector3i pos = positions[i];
            Log.Out(AdvancedFarmingLightService.BuildBlockForensics(world, pos));
            LogDoorLightNeighborReport(world, center, pos, label);
            LogDoorVisualLightAudit(world, center, pos, label);
            LogLightColumnDiagnostic(AdvancedFarmingLightService.BuildColumnDiagnostic(world, pos + Vector3i.down), label + ":belowDoor", 24);
        }

        if (positions.Count > count)
        {
            Log.Out("[AdvancedFarming DoorLightIssue] label=" + label
                + " coverCandidateOutputTruncated candidates=" + positions.Count
                + " printed=" + count);
        }
    }


    private static void LogDoorLightFocusedCropReport(World world, Vector3i center, List<Vector3i> cropPositions, List<Vector3i> doorPositions, string label)
    {
        if (cropPositions == null || cropPositions.Count == 0)
        {
            Log.Out("[AdvancedFarming DoorLightIssueFocus] label=" + label
                + " center=" + center
                + " noCropCandidates=true");
            return;
        }

        Vector3i nearest = cropPositions[0];
        int nearestDistSq = DistanceSquared(nearest, center);
        for (int i = 1; i < cropPositions.Count; i++)
        {
            int distSq = DistanceSquared(cropPositions[i], center);
            if (distSq < nearestDistSq)
            {
                nearest = cropPositions[i];
                nearestDistSq = distSq;
            }
        }

        CropEnvironmentDiagnosticRow row = BuildCropEnvironmentDiagnosticRow(world, nearest, "doorlightissue:" + label + ":nearestCrop");
        Log.Out("[AdvancedFarming DoorLightIssueFocus] label=" + label
            + " center=" + center
            + " nearestCrop=" + nearest
            + " distSq=" + nearestDistSq
            + " lightPass=" + row.LightPass
            + " naturalPath=" + row.NaturalPath
            + " enclosed=" + row.Enclosed
            + " cropSun=" + row.CropSunLight
            + " requiredNaturalSunlight=" + row.RequiredNaturalSunlight
            + " note=This is the crop position closest to the player command center; stand on/near the bad crop before running the command.");
        Log.Out(row.Line);
        Log.Out(BuildDoorLightCropGrowthState(world, nearest, label));
        LogDoorLightCropSearchDetails(world, nearest, label + ":nearestCrop", 16, 16);
        LogDoorLightCropNearestDoors(world, nearest, doorPositions, label + ":nearestCrop", 8);
        LogLightColumnDiagnostic(AdvancedFarmingLightService.BuildColumnDiagnostic(world, nearest), "doorlightissue:" + label + ":nearestCropColumn", 24);

        int localRadius = 4;
        int localCount = 0;
        int localPass = 0;
        int localNaturalPath = 0;
        int localEnclosed = 0;
        int localDirectOpenSkyAtCrop = 0;
        int localPrinted = 0;
        for (int i = 0; i < cropPositions.Count; i++)
        {
            Vector3i pos = cropPositions[i];
            if (Math.Abs(pos.x - nearest.x) > localRadius || Math.Abs(pos.z - nearest.z) > localRadius || Math.Abs(pos.y - nearest.y) > 2)
                continue;

            CropEnvironmentDiagnosticRow local = BuildCropEnvironmentDiagnosticRow(world, pos, "doorlightissue:" + label + ":nearestCluster");
            localCount++;
            if (local.LightPass) localPass++;
            if (local.NaturalPath) localNaturalPath++;
            if (local.Enclosed) localEnclosed++;
            if (local.Line.IndexOf("lightMode=openColumnAtCrop", StringComparison.OrdinalIgnoreCase) >= 0) localDirectOpenSkyAtCrop++;

            if (localPrinted < 16)
            {
                Log.Out("[AdvancedFarming DoorLightIssueFocusCrop] label=" + label
                    + " pos=" + pos
                    + " lightPass=" + local.LightPass
                    + " naturalPath=" + local.NaturalPath
                    + " enclosed=" + local.Enclosed
                    + " cropSun=" + local.CropSunLight
                    + " requiredNaturalSunlight=" + local.RequiredNaturalSunlight);
                Log.Out(BuildDoorLightCropGrowthState(world, pos, label));
                localPrinted++;
            }
        }

        Log.Out("[AdvancedFarming DoorLightIssueFocusSummary] label=" + label
            + " nearestCrop=" + nearest
            + " localRadius=" + localRadius
            + " localCrops=" + localCount
            + " lightPass=" + localPass
            + " naturalPath=" + localNaturalPath
            + " enclosed=" + localEnclosed
            + " directOpenSkyAtCrop=" + localDirectOpenSkyAtCrop
            + " printed=" + localPrinted);
    }

    private static string BuildDoorLightCropGrowthState(World world, Vector3i cropPos, string label)
    {
        if (world == null)
        {
            return "[AdvancedFarming DoorLightIssueGrowth] label=" + label
                + " pos=" + cropPos
                + " noWorld=true";
        }

        TileEntityPlantGrowingRebirth te = world.GetTileEntity(cropPos) as TileEntityPlantGrowingRebirth;
        BlockValue blockValue = world.GetBlock(cropPos);
        string blockName = blockValue.Block != null ? blockValue.Block.GetBlockName() : "<null>";
        ulong nowTicks = GameTimer.Instance != null ? GameTimer.Instance.ticks : 0UL;
        int nowSeconds = RebirthUtilities.TotalGameSecondsPassed();
        if (te == null)
        {
            return "[AdvancedFarming DoorLightIssueGrowth] label=" + label
                + " pos=" + cropPos
                + " block=" + blockName
                + " hasPlantTE=false";
        }

        return "[AdvancedFarming DoorLightIssueGrowth] label=" + label
            + " pos=" + cropPos
            + " block=" + blockName
            + " hasPlantTE=true"
            + " accumulatedTicks=" + te.AccumulatedTicks
            + " gameTimerTicks=" + te.GameTimerTicks
            + " nowTicks=" + nowTicks
            + " tickDelta=" + (nowTicks >= te.GameTimerTicks ? nowTicks - te.GameTimerTicks : 0UL)
            + " lastProcessedWorldSeconds=" + te.LastProcessedWorldSeconds
            + " nowWorldSeconds=" + nowSeconds
            + " secondsSinceProcessed=" + (nowSeconds - te.LastProcessedWorldSeconds)
            + " watered=" + te.bWatered
            + " updating=" + te.bUpdating;
    }

    private static void LogDoorLightCropReports(World world, List<Vector3i> positions, List<Vector3i> doorPositions, int maxPrint, string label)
    {
        if (positions == null || positions.Count == 0)
        {
            Log.Out("[AdvancedFarming DoorLightIssue] label=" + label + " no advanced-farming crop candidates near player.");
            return;
        }

        int count = Math.Min(positions.Count, maxPrint);
        for (int i = 0; i < count; i++)
        {
            Vector3i cropPos = positions[i];
            Log.Out(BuildCropEnvironmentDiagnosticRow(world, cropPos, "doorlightissue:" + label).Line);
            LogDoorLightCropSearchDetails(world, cropPos, label, 12, 12);
            LogDoorLightCropNearestDoors(world, cropPos, doorPositions, label, 6);
            LogLightColumnDiagnostic(AdvancedFarmingLightService.BuildColumnDiagnostic(world, cropPos), "doorlightissue:" + label + ":cropColumn", 16);
        }

        if (positions.Count > count)
        {
            Log.Out("[AdvancedFarming DoorLightIssue] label=" + label
                + " cropOutputTruncated candidates=" + positions.Count
                + " printed=" + count);
        }
    }

    private static void LogDoorLightCropSearchDetails(World world, Vector3i cropPos, string label, int openSamples, int blockedSamples)
    {
        AdvancedFarmingLightService.LightSearchDiagnostic search = AdvancedFarmingLightService.BuildSearchDiagnostic(world, cropPos, openSamples, blockedSamples);
        Log.Out("[AdvancedFarming DoorLightIssueSearch] label=" + label
            + " plantPos=" + search.PlantPos
            + " startPos=" + search.StartPos
            + " startTransparent=" + search.StartTransparent
            + " startBlock=" + search.StartBlock
            + " columnsChecked=" + search.ColumnsChecked
            + " openColumns=" + search.OpenColumns
            + " blockedColumns=" + search.BlockedColumns
            + " radius=" + search.SearchHorizontalRadius
            + " up=" + search.SearchUp
            + " meaning=openColumnsAreWhyCropCanStillFindNaturalLight");

        if (search.OpenColumnSamples != null)
        {
            for (int i = 0; i < search.OpenColumnSamples.Count; i++)
            {
                AdvancedFarmingLightService.LightSearchColumnEntry entry = search.OpenColumnSamples[i];
                Log.Out("[AdvancedFarming DoorLightIssueOpenColumn] label=" + label
                    + " plantPos=" + cropPos
                    + " root=" + entry.RootPos
                    + " dx=" + entry.Dx
                    + " dz=" + entry.Dz
                    + " dist=" + entry.ChebyshevDistance
                    + " air=" + entry.AirCount
                    + " nonAirTransparent=" + entry.NonAirTransparentCount
                    + " nonAirOpaque=" + entry.NonAirOpaqueCount);
            }
        }

        if (search.BlockedColumnSamples != null)
        {
            for (int i = 0; i < search.BlockedColumnSamples.Count; i++)
            {
                AdvancedFarmingLightService.LightSearchColumnEntry entry = search.BlockedColumnSamples[i];
                Log.Out("[AdvancedFarming DoorLightIssueBlockedColumn] label=" + label
                    + " plantPos=" + cropPos
                    + " root=" + entry.RootPos
                    + " dx=" + entry.Dx
                    + " dz=" + entry.Dz
                    + " dist=" + entry.ChebyshevDistance
                    + " firstBlockerPos=" + entry.FirstBlockerPos
                    + " firstBlockerBlock=" + entry.FirstBlockerBlock
                    + " air=" + entry.AirCount
                    + " nonAirTransparent=" + entry.NonAirTransparentCount
                    + " nonAirOpaque=" + entry.NonAirOpaqueCount);
            }
        }
    }

    private static void LogDoorLightCropNearestDoors(World world, Vector3i cropPos, List<Vector3i> doorPositions, string label, int maxDoors)
    {
        if (doorPositions == null || doorPositions.Count == 0)
        {
            Log.Out("[AdvancedFarming DoorLightIssueCropDoorLinks] label=" + label
                + " crop=" + cropPos
                + " noDoorCandidates=true");
            return;
        }

        List<Vector3i> nearest = new List<Vector3i>(doorPositions);
        SortPositionsByDistance(nearest, cropPos);
        int count = Math.Min(nearest.Count, Math.Max(0, maxDoors));
        for (int i = 0; i < count; i++)
        {
            Vector3i doorPos = nearest[i];
            Vector3i delta = new Vector3i(doorPos.x - cropPos.x, doorPos.y - cropPos.y, doorPos.z - cropPos.z);
            BlockValue doorValue = world.GetBlock(doorPos);
            Log.Out("[AdvancedFarming DoorLightIssueCropDoorLink] label=" + label
                + " crop=" + cropPos
                + " doorRank=" + (i + 1)
                + " doorPos=" + doorPos
                + " delta=" + delta
                + " distSq=" + DistanceSquared(doorPos, cropPos)
                + " block=" + GetDebugBlockName(doorValue)
                + " rot=" + doorValue.rotation
                + " meta=" + doorValue.meta
                + " meta2=" + doorValue.meta2
                + " child=" + doorValue.ischild
                + " selfLight=" + BuildLightCellSummary(world != null ? world.ChunkCache : null, world, doorPos));
        }
    }

    private static void LogDoorLightStructureSummary(World world, Vector3i center, List<Vector3i> doorPositions, List<Vector3i> cropPositions, string label)
    {
        Vector3i doorMin;
        Vector3i doorMax;
        bool hasDoors = TryGetBounds(doorPositions, out doorMin, out doorMax);
        Vector3i cropMin;
        Vector3i cropMax;
        bool hasCrops = TryGetBounds(cropPositions, out cropMin, out cropMax);

        Log.Out("[AdvancedFarming DoorLightIssueStructure] label=" + label
            + " center=" + center
            + " doorCount=" + (doorPositions != null ? doorPositions.Count : 0)
            + " cropCount=" + (cropPositions != null ? cropPositions.Count : 0)
            + " doorBounds=" + (hasDoors ? doorMin + ".." + doorMax : "<none>")
            + " cropBounds=" + (hasCrops ? cropMin + ".." + cropMax : "<none>")
            + " note=Use OpenColumn rows to see which sky columns still justify crop light; use CropDoorLink rows to confirm both side doors were found.");

        if (cropPositions == null || cropPositions.Count == 0)
            return;

        int lit = 0;
        int naturalPath = 0;
        int enclosed = 0;
        int printed = 0;
        for (int i = 0; i < cropPositions.Count; i++)
        {
            CropEnvironmentDiagnosticRow row = BuildCropEnvironmentDiagnosticRow(world, cropPositions[i], "doorlightissue:" + label + ":summary");
            if (row.LightPass) lit++;
            if (row.NaturalPath) naturalPath++;
            if (row.Enclosed) enclosed++;
            if (printed < 8)
            {
                Log.Out("[AdvancedFarming DoorLightIssueCropSummarySample] label=" + label
                    + " pos=" + row.Pos
                    + " lightPass=" + row.LightPass
                    + " naturalPath=" + row.NaturalPath
                    + " enclosed=" + row.Enclosed
                    + " requiredNaturalSunlight=" + row.RequiredNaturalSunlight
                    + " cropSun=" + row.CropSunLight);
                printed++;
            }
        }

        Log.Out("[AdvancedFarming DoorLightIssueCropSummary] label=" + label
            + " crops=" + cropPositions.Count
            + " lightPass=" + lit
            + " naturalPath=" + naturalPath
            + " enclosed=" + enclosed);
    }

    private static bool TryGetBounds(List<Vector3i> positions, out Vector3i min, out Vector3i max)
    {
        min = Vector3i.zero;
        max = Vector3i.zero;
        if (positions == null || positions.Count == 0)
            return false;

        min = positions[0];
        max = positions[0];
        for (int i = 1; i < positions.Count; i++)
        {
            Vector3i pos = positions[i];
            if (pos.x < min.x) min.x = pos.x;
            if (pos.y < min.y) min.y = pos.y;
            if (pos.z < min.z) min.z = pos.z;
            if (pos.x > max.x) max.x = pos.x;
            if (pos.y > max.y) max.y = pos.y;
            if (pos.z > max.z) max.z = pos.z;
        }

        return true;
    }



    private static void LogDoorVisualLightAudit(World world, Vector3i center, Vector3i pos, string label)
    {
        if (world == null)
            return;

        ChunkCluster chunkCluster = world.ChunkCache;
        BlockValue value = world.GetBlock(pos);
        Block block = value.Block;
        string blockName = GetDebugBlockName(value);
        string className = block != null ? block.GetType().Name : "<null>";
        bool modelLike = ContainsIgnoreCase(className, "Model")
            || ContainsIgnoreCase(className, "Composite")
            || ContainsIgnoreCase(blockName, "door")
            || ContainsIgnoreCase(blockName, "hatch")
            || ContainsIgnoreCase(blockName, "trapdoor");

        int selfSun = GetLightSafe(chunkCluster, pos, Chunk.LIGHT_TYPE.SUN);
        int selfBlock = GetLightSafe(chunkCluster, pos, Chunk.LIGHT_TYPE.BLOCK);
        int maxSun = selfSun;
        int maxBlock = selfBlock;
        Vector3i maxSunPos = pos;
        Vector3i maxBlockPos = pos;
        int litCells = 0;
        int sunCells = 0;
        int blockCells = 0;

        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dz = -1; dz <= 1; dz++)
                {
                    Vector3i sample = pos + new Vector3i(dx, dy, dz);
                    int sun = GetLightSafe(chunkCluster, sample, Chunk.LIGHT_TYPE.SUN);
                    int blockLight = GetLightSafe(chunkCluster, sample, Chunk.LIGHT_TYPE.BLOCK);
                    if (sun > 0 || blockLight > 0)
                        litCells++;
                    if (sun > 0)
                        sunCells++;
                    if (blockLight > 0)
                        blockCells++;
                    if (sun > maxSun)
                    {
                        maxSun = sun;
                        maxSunPos = sample;
                    }
                    if (blockLight > maxBlock)
                    {
                        maxBlock = blockLight;
                        maxBlockPos = sample;
                    }
                }
            }
        }

        string visualClassification;
        if (selfSun <= 0 && selfBlock <= 0 && maxSun <= 0 && maxBlock <= 0)
            visualClassification = "noActualLightIn3x3x3_ifVisuallyBrightLikelyModelMaterialAmbientOrStaleRender";
        else if (selfSun <= 0 && selfBlock <= 0)
            visualClassification = "doorCellDarkButNeighborLightExists_possibleModelAmbientOrAdjacentLightBleed";
        else
            visualClassification = "doorCellHasActualLight";

        Log.Out("[AdvancedFarming DoorVisualLightAudit] label=" + label
            + " center=" + center
            + " pos=" + pos
            + " block=" + blockName
            + " class=" + className
            + " modelLike=" + modelLike
            + " rot=" + value.rotation
            + " meta=" + value.meta
            + " meta2=" + value.meta2
            + " child=" + value.ischild
            + " selfSun=" + selfSun
            + " selfBlock=" + selfBlock
            + " maxSun3x3x3=" + maxSun
            + " maxSunPos=" + maxSunPos
            + " maxBlock3x3x3=" + maxBlock
            + " maxBlockPos=" + maxBlockPos
            + " litCells3x3x3=" + litCells
            + " sunCells3x3x3=" + sunCells
            + " blockCells3x3x3=" + blockCells
            + " visualClassification=" + visualClassification
            + " note=Compares actual SUN/BLOCK light around the door against visual brightness; if actual light is zero but door looks bright, this points to render/material/ambient/stale-mesh lighting rather than crop-light leakage.");

        Log.Out("[AdvancedFarming DoorVisualLightFaces] label=" + label
            + " pos=" + pos
            + " self=" + BuildLightCellSummary(chunkCluster, world, pos)
            + " up=" + BuildLightCellSummary(chunkCluster, world, pos + Vector3i.up)
            + " down=" + BuildLightCellSummary(chunkCluster, world, pos + Vector3i.down)
            + " north=" + BuildLightCellSummary(chunkCluster, world, pos + new Vector3i(0, 0, 1))
            + " south=" + BuildLightCellSummary(chunkCluster, world, pos + new Vector3i(0, 0, -1))
            + " east=" + BuildLightCellSummary(chunkCluster, world, pos + new Vector3i(1, 0, 0))
            + " west=" + BuildLightCellSummary(chunkCluster, world, pos + new Vector3i(-1, 0, 0)));
    }


    private static string JoinLimited(List<string> values, int max)
    {
        if (values == null || values.Count == 0)
            return "<none>";

        int count = Math.Min(values.Count, max);
        string joined = string.Join(" | ", values.GetRange(0, count).ToArray());
        if (values.Count > count)
            joined += " | ...+" + (values.Count - count);
        return joined;
    }

    private static void LogDoorLightNeighborReport(World world, Vector3i center, Vector3i pos, string label)
    {
        ChunkCluster chunkCluster = world != null ? world.ChunkCache : null;
        BlockValue value = world.GetBlock(pos);
        Log.Out("[AdvancedFarming DoorLightIssueNeighbors] label=" + label
            + " center=" + center
            + " pos=" + pos
            + " block=" + GetDebugBlockName(value)
            + " self=" + BuildLightCellSummary(chunkCluster, world, pos)
            + " up=" + BuildLightCellSummary(chunkCluster, world, pos + Vector3i.up)
            + " down=" + BuildLightCellSummary(chunkCluster, world, pos + Vector3i.down)
            + " north=" + BuildLightCellSummary(chunkCluster, world, pos + new Vector3i(0, 0, 1))
            + " south=" + BuildLightCellSummary(chunkCluster, world, pos + new Vector3i(0, 0, -1))
            + " east=" + BuildLightCellSummary(chunkCluster, world, pos + new Vector3i(1, 0, 0))
            + " west=" + BuildLightCellSummary(chunkCluster, world, pos + new Vector3i(-1, 0, 0))
            + " edgeFromUp=" + BuildEdgeOpacitySummary(chunkCluster, pos + Vector3i.up, pos)
            + " edgeToDown=" + BuildEdgeOpacitySummary(chunkCluster, pos, pos + Vector3i.down)
            + " edgeFromNorth=" + BuildEdgeOpacitySummary(chunkCluster, pos + new Vector3i(0, 0, 1), pos)
            + " edgeFromSouth=" + BuildEdgeOpacitySummary(chunkCluster, pos + new Vector3i(0, 0, -1), pos)
            + " edgeFromEast=" + BuildEdgeOpacitySummary(chunkCluster, pos + new Vector3i(1, 0, 0), pos)
            + " edgeFromWest=" + BuildEdgeOpacitySummary(chunkCluster, pos + new Vector3i(-1, 0, 0), pos));
    }


    private static int GetLightSafe(ChunkCluster chunkCluster, Vector3i pos, Chunk.LIGHT_TYPE lightType)
    {
        if (chunkCluster == null)
            return -1;

        try
        {
            return chunkCluster.GetLight(pos, lightType);
        }
        catch
        {
            return -1;
        }
    }

    private static string BuildLightCellSummary(ChunkCluster chunkCluster, World world, Vector3i pos)
    {
        BlockValue value = world.GetBlock(pos);
        string sun = "?";
        string block = "?";
        if (chunkCluster != null)
        {
            try { sun = chunkCluster.GetLight(pos, Chunk.LIGHT_TYPE.SUN).ToString(); } catch { sun = "<threw>"; }
            try { block = chunkCluster.GetLight(pos, Chunk.LIGHT_TYPE.BLOCK).ToString(); } catch { block = "<threw>"; }
        }

        return "{" + pos
            + ":name=" + GetDebugBlockName(value)
            + ",sun=" + sun
            + ",block=" + block
            + ",rot=" + value.rotation
            + ",meta=" + value.meta
            + ",child=" + value.ischild
            + "}";
    }

    private static string BuildEdgeOpacitySummary(ChunkCluster chunkCluster, Vector3i fromPos, Vector3i toPos)
    {
        if (chunkCluster == null)
            return "{from=" + fromPos + ",to=" + toPos + ",opacity=<noChunkCluster>}";

        try
        {
            int opacity = AdvancedFarmingDynamicLightOpacityService.GetLightStepOpacity(chunkCluster, fromPos, toPos);
            return "{from=" + fromPos + ",to=" + toPos + ",opacity=" + opacity + "}";
        }
        catch (Exception ex)
        {
            return "{from=" + fromPos + ",to=" + toPos + ",opacity=<threw:" + ex.GetType().Name + ">}";
        }
    }

    private static bool IsCenteredNonGlassCoverBlock(Block block)
    {
        if (block == null)
            return false;

        if (IsCurtainBlindBlock(block) || IsShutterLikeBlock(block) || IsDoorLightCandidate(block))
            return false;

        string blockName = block.GetBlockName() ?? string.Empty;
        string className = block.GetType().Name ?? string.Empty;
        string tags = SafeGetBlockProperty(block, "Tags");
        string blockTag = SafeGetBlockProperty(block, "BlockTag");
        string place = SafeGetBlockProperty(block, "Place");
        string model = SafeGetBlockProperty(block, "Model");
        string shape = SafeGetBlockProperty(block, "Shape");
        string text = blockName + " " + className + " " + tags + " " + blockTag + " " + place + " " + model + " " + shape;

        if (ContainsIgnoreCase(text, "glass"))
            return false;

        return block.lightOpacity > 0
            && (ContainsIgnoreCase(text, "ctrplate") || ContainsIgnoreCase(text, "centered"));
    }


    private static bool IsCenteredWindowGlassCoverBlock(Block block)
    {
        if (block == null)
            return false;

        if (IsCurtainBlindBlock(block) || IsShutterLikeBlock(block) || IsDoorLightCandidate(block))
            return false;

        if (block.lightOpacity <= 0)
            return false;

        string blockName = block.GetBlockName() ?? string.Empty;
        string className = block.GetType().Name ?? string.Empty;
        string tags = SafeGetBlockProperty(block, "Tags");
        string blockTag = SafeGetBlockProperty(block, "BlockTag");
        string place = SafeGetBlockProperty(block, "Place");
        string model = SafeGetBlockProperty(block, "Model");
        string shape = SafeGetBlockProperty(block, "Shape");
        string shapeCategories = SafeGetBlockProperty(block, "ShapeCategories");
        string customIcon = SafeGetBlockProperty(block, "CustomIcon");
        string text = blockName + " " + className + " " + tags + " " + blockTag + " " + place + " " + model + " " + shape + " " + shapeCategories + " " + customIcon;

        bool windowOrGlass = ContainsIgnoreCase(text, "window") || ContainsIgnoreCase(text, "glass");
        if (!windowOrGlass)
            return false;

        bool explicitCentered = ContainsIgnoreCase(text, "ctrplate") || ContainsIgnoreCase(text, "centered");
        bool windowShapeAperture = ContainsIgnoreCase(shapeCategories, "windows")
            || ContainsIgnoreCase(model, "/window")
            || ContainsIgnoreCase(model, "\\window")
            || ContainsIgnoreCase(model, "window_")
            || ContainsIgnoreCase(customIcon, "shapewindow");

        return explicitCentered || windowShapeAperture;
    }

    private static string BuildGeneratedShapePolicySummary(Block block)
    {
        try
        {
            return AdvancedFarmingShapeOpacityPolicy.BuildDebugSummary(block);
        }
        catch (Exception ex)
        {
            return "<threw:" + ex.GetType().Name + ">";
        }
    }

    private static string SafeGetBlockProperty(Block block, string key)
    {
        if (block == null || block.Properties == null || block.Properties.Values == null)
            return string.Empty;

        try
        {
            return block.Properties.Values.ContainsKey(key) ? (block.Properties.Values[key] ?? string.Empty) : string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static bool IsWindowGlassLightCandidate(Block block)
    {
        if (block == null)
            return false;

        if (IsCurtainBlindBlock(block) || IsShutterLikeBlock(block) || IsDoorLightCandidate(block))
            return false;

        string blockName = block.GetBlockName() ?? string.Empty;
        string className = block.GetType().Name ?? string.Empty;
        string tagText = SafeGetBlockProperty(block, "Tags")
            + " " + SafeGetBlockProperty(block, "BlockTag")
            + " " + SafeGetBlockProperty(block, "Place");

        return ContainsIgnoreCase(blockName, "window")
            || ContainsIgnoreCase(blockName, "glass")
            || ContainsIgnoreCase(className, "window")
            || ContainsIgnoreCase(className, "glass")
            || ContainsIgnoreCase(tagText, "window")
            || ContainsIgnoreCase(tagText, "glass");
    }

    private static bool IsCurtainBlindBlock(Block block)
    {
        if (block == null)
            return false;

        string blockName = block.GetBlockName() ?? string.Empty;
        string className = block.GetType().Name ?? string.Empty;
        string tags = SafeGetBlockProperty(block, "Tags");
        string blockTag = SafeGetBlockProperty(block, "BlockTag");
        string text = blockName + " " + className + " " + tags + " " + blockTag;
        return ContainsIgnoreCase(text, "curtain")
            || ContainsIgnoreCase(text, "drape")
            || ContainsIgnoreCase(text, "blind");
    }

    private static bool IsShutterLikeBlock(Block block)
    {
        if (block == null)
            return false;

        string blockName = block.GetBlockName() ?? string.Empty;
        string className = block.GetType().Name ?? string.Empty;
        string tags = SafeGetBlockProperty(block, "Tags");
        string blockTag = SafeGetBlockProperty(block, "BlockTag");
        string place = SafeGetBlockProperty(block, "Place");
        string text = blockName + " " + className + " " + tags + " " + blockTag + " " + place;
        return ContainsIgnoreCase(text, "shutter");
    }

    private static string SafeIsSeeThrough(World world, Vector3i pos, Block block, BlockValue value)
    {
        if (block == null)
            return "<null>";

        try { return block.IsSeeThrough(world, pos, value).ToString(); }
        catch (Exception ex) { return "<threw:" + ex.GetType().Name + ">"; }
    }

    private static string SafeObjectToString(object value)
    {
        if (value == null)
            return string.Empty;

        try { return value.ToString() ?? string.Empty; }
        catch { return string.Empty; }
    }

    private static bool IsDoorLightCandidate(Block block)
    {
        if (block == null)
            return false;

        string blockName = block.GetBlockName() ?? string.Empty;
        string className = block.GetType().Name ?? string.Empty;
        string tags = SafeGetBlockProperty(block, "Tags");
        string blockTag = SafeGetBlockProperty(block, "BlockTag");
        string place = SafeGetBlockProperty(block, "Place");
        string text = blockName + " " + className + " " + tags + " " + blockTag + " " + place;

        return ContainsIgnoreCase(text, "door")
            || ContainsIgnoreCase(text, "trapdoor")
            || ContainsIgnoreCase(text, "hatch")
            || ContainsIgnoreCase(text, "shutter")
            || IsCurtainBlindBlock(block);
    }

    private static string GetDebugBlockName(BlockValue value)
    {
        Block block = value.Block;
        return block != null ? block.GetBlockName() : "<null>";
    }

    private static bool ContainsIgnoreCase(string text, string value)
    {
        return !string.IsNullOrEmpty(text)
            && !string.IsNullOrEmpty(value)
            && text.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static void SortPositionsByDistance(List<Vector3i> positions, Vector3i center)
    {
        if (positions == null || positions.Count <= 1)
            return;

        positions.Sort(delegate (Vector3i a, Vector3i b)
        {
            int da = DistanceSquared(a, center);
            int db = DistanceSquared(b, center);
            if (da != db)
                return da.CompareTo(db);
            if (a.y != b.y)
                return a.y.CompareTo(b.y);
            if (a.x != b.x)
                return a.x.CompareTo(b.x);
            return a.z.CompareTo(b.z);
        });
    }

    private static int DistanceSquared(Vector3i a, Vector3i b)
    {
        int dx = a.x - b.x;
        int dy = a.y - b.y;
        int dz = a.z - b.z;
        return dx * dx + dy * dy + dz * dz;
    }


    private static void ExecuteHeatFullTest(List<string> args, CommandSenderInfo senderInfo)
    {
        float temperature = 30f;
        int radius = 8;
        string benchSeconds = "60";
        string intervalSeconds = "10";

        if (args != null && args.Count > 1)
            float.TryParse(args[1], out temperature);
        if (args != null && args.Count > 2)
            int.TryParse(args[2], out radius);
        if (args != null && args.Count > 3)
            benchSeconds = args[3];
        if (args != null && args.Count > 4)
            intervalSeconds = args[4];

        if (radius < 1)
            radius = 1;
        if (radius > 32)
            radius = 32;

        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (world == null)
        {
            Log.Out("[AdvancedFarming HeatFullTest] No world loaded.");
            return;
        }

        Vector3i center = ResolveCommandCenter(world, senderInfo);
        Log.Out("[AdvancedFarming HeatFullTest] center=" + center + " temperature=" + temperature.ToString("0.##") + " radius=" + radius + " benchGrowthSeconds=5400 benchSeconds=" + benchSeconds + " intervalSeconds=" + intervalSeconds);

        Log.Out(AdvancedFarmingHeatQueryService.SetTemperatureOverride(temperature));
        AdvancedFarmingHeatQueryService.InvalidateAll("heatfulltest");
        Log.Out(AdvancedFarmingHeatQueryService.BuildStatus());

        int crops = 0;
        int heatedCrops = 0;
        int unheatedCrops = 0;
        int printed = 0;
        int maxPrint = 24;

        for (int x = center.x - radius; x <= center.x + radius; x++)
        {
            for (int z = center.z - radius; z <= center.z + radius; z++)
            {
                for (int y = center.y - 4; y <= center.y + 4; y++)
                {
                    Vector3i pos = new Vector3i(x, y, z);
                    BlockValue blockValue = world.GetBlock(pos);
                    string blockName = blockValue.Block != null ? blockValue.Block.GetBlockName() : string.Empty;

                    if (!IsAdvancedFarmingCropName(blockName))
                        continue;

                    crops++;
                    string line = BuildHeatTestLine(world, pos, "autoCrop");
                    bool heated = line.IndexOf("heatSource=none", System.StringComparison.OrdinalIgnoreCase) < 0;
                    if (heated)
                        heatedCrops++;
                    else
                        unheatedCrops++;

                    if (printed < maxPrint || heated)
                    {
                        Log.Out(line);
                        printed++;
                    }
                }
            }
        }

        Log.Out("[AdvancedFarming HeatFullTest] summary center=" + center
            + " radius=" + radius
            + " crops=" + crops
            + " heatedCrops=" + heatedCrops
            + " unheatedCrops=" + unheatedCrops
            + " printedCropLines=" + printed);

        Log.Out(AdvancedFarmingHeatQueryService.BuildStatus());

        ExecuteAutoBench(new List<string> { "autobench", "5400", benchSeconds, intervalSeconds });
    }


    private static void ExecuteHeatTest(List<string> args)
    {
        if (args == null || args.Count < 4)
        {
            Log.Out("Usage: rbfarming heattest <x> <y> <z>");
            return;
        }

        if (!int.TryParse(args[1], out int x) || !int.TryParse(args[2], out int y) || !int.TryParse(args[3], out int z))
        {
            Log.Out("Usage: rbfarming heattest <x> <y> <z>");
            return;
        }

        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (world == null)
        {
            Log.Out("[AdvancedFarming HeatTest] No world loaded.");
            return;
        }

        Vector3i pos = new Vector3i(x, y, z);
        Log.Out(BuildHeatTestLine(world, pos, "direct"));

        BlockValue blockValue = world.GetBlock(pos);
        string blockName = blockValue.Block != null ? blockValue.Block.GetBlockName() : string.Empty;
        if (string.Equals(blockName, "air", System.StringComparison.OrdinalIgnoreCase))
        {
            Vector3i below = pos + Vector3i.down;
            BlockValue belowValue = world.GetBlock(below);
            string belowName = belowValue.Block != null ? belowValue.Block.GetBlockName() : string.Empty;
            if (IsAdvancedFarmingCropName(belowName))
                Log.Out(BuildHeatTestLine(world, below, "belowCropHint"));
        }

        Log.Out(AdvancedFarmingHeatQueryService.BuildStatus());
    }

    private static void ExecuteHeatTestArea(List<string> args)
    {
        if (args == null || args.Count < 4)
        {
            Log.Out("Usage: rbfarming heattestarea <x> <y> <z> [radius]");
            return;
        }

        if (!int.TryParse(args[1], out int x) || !int.TryParse(args[2], out int y) || !int.TryParse(args[3], out int z))
        {
            Log.Out("Usage: rbfarming heattestarea <x> <y> <z> [radius]");
            return;
        }

        int radius = 4;
        if (args.Count > 4)
            int.TryParse(args[4], out radius);

        if (radius < 1)
            radius = 1;
        if (radius > 12)
            radius = 12;

        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (world == null)
        {
            Log.Out("[AdvancedFarming HeatTestArea] No world loaded.");
            return;
        }

        Vector3i center = new Vector3i(x, y, z);
        int found = 0;
        Log.Out("[AdvancedFarming HeatTestArea] center=" + center + " radius=" + radius);

        for (int px = center.x - radius; px <= center.x + radius; px++)
        {
            for (int pz = center.z - radius; pz <= center.z + radius; pz++)
            {
                for (int py = center.y - 2; py <= center.y + 2; py++)
                {
                    Vector3i pos = new Vector3i(px, py, pz);
                    BlockValue blockValue = world.GetBlock(pos);
                    string blockName = blockValue.Block != null ? blockValue.Block.GetBlockName() : string.Empty;
                    if (!IsAdvancedFarmingCropName(blockName))
                        continue;

                    found++;
                    Log.Out(BuildHeatTestLine(world, pos, "crop"));
                }
            }
        }

        Log.Out("[AdvancedFarming HeatTestArea] cropCount=" + found);
        Log.Out(AdvancedFarmingHeatQueryService.BuildStatus());
    }

    private static string BuildHeatTestLine(World world, Vector3i pos, string label)
    {
        BlockValue blockValue = world.GetBlock(pos);
        string blockName = blockValue.Block != null ? blockValue.Block.GetBlockName() : "<null>";

        AdvancedFarmingLightService.LightResult light = AdvancedFarmingLightService.Evaluate(
            world,
            pos,
            3,
            3,
            false,
            AdvancedFarmingRuntimePolicy.Enabled);
        AdvancedFarmingTemperatureService.TemperatureResult temperature = AdvancedFarmingTemperatureService.Evaluate(
            world,
            pos,
            light.HasSunLight || light.HasOpenSky,
            AdvancedFarmingRuntimePolicy.Enabled);

        return "[AdvancedFarming HeatTest] label=" + label
            + " pos=" + pos
            + " block=" + blockName
            + " temperatureOverride=" + temperature.TemperatureOverride
            + " baseTemp=" + temperature.BaseTemperature.ToString("0.##")
            + " weatherPenalty=" + temperature.WeatherPenalty.ToString("0.##")
            + " registryCampfire=" + temperature.HasCampfireHeat
            + " registryStove=" + temperature.HasWoodStoveHeat
            + " heatSource=" + temperature.HeatSource
            + " heatBonus=" + temperature.HeatBonus.ToString("0.##")
            + " biome=" + temperature.BiomeName
            + " enclosed=" + temperature.Enclosed
            + " thermalOpenToExterior=" + temperature.ThermalOpenToExterior
            + " thermalMode=" + temperature.ThermalExposureMode
            + " thermalExitPos=" + temperature.ThermalExitPos
            + " thermalBlockingPos=" + temperature.ThermalBlockingPos
            + " thermalBlockingBlock=" + temperature.ThermalBlockingBlock
            + " thermalNodes=" + temperature.ThermalNodesVisited
            + " enclosedBiomeAdjust=" + temperature.EnclosedBiomeAdjustment.ToString("0.##")
            + " enclosedHotAdjust=" + temperature.EnclosedBiomeHotAdjustment.ToString("0.##")
            + " enclosedColdAdjust=" + temperature.EnclosedBiomeColdAdjustment.ToString("0.##")
            + " enclosedColdBlend=" + temperature.EnclosedBiomeColdBlend.ToString("0.###")
            + " enclosedAdjustMode=" + temperature.EnclosedBiomeAdjustmentMode
            + " adjustedTemp=" + temperature.Temperature.ToString("0.##")
            + " required=45";
    }

    private static string ResolveSeedStageNameForHarvest(string blockName)
    {
        if (string.IsNullOrEmpty(blockName))
            return string.Empty;

        string lower = blockName.ToLowerInvariant();
        int harvestIndex = lower.IndexOf("3harvest", System.StringComparison.Ordinal);
        if (harvestIndex < 0)
            return string.Empty;

        string candidate = blockName.Substring(0, harvestIndex) + "1";
        if (Block.GetBlockValue(candidate).isair)
            return string.Empty;

        return candidate;
    }

    private static bool IsHarvestStageCropName(string blockName)
    {
        return !string.IsNullOrEmpty(blockName)
            && blockName.IndexOf("3Harvest", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool IsAdvancedFarmingCropName(string blockName)
    {
        if (string.IsNullOrEmpty(blockName))
            return false;

        if (string.Equals(blockName, "air", System.StringComparison.OrdinalIgnoreCase))
            return false;

        return blockName.IndexOf("planted", System.StringComparison.OrdinalIgnoreCase) >= 0
            || blockName.IndexOf("crop", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static void ExecuteColdAutoBench(List<string> args, string growthSeconds, string durationSeconds, string intervalSeconds)
    {
        float temperature = 0f;
        if (args != null && args.Count > 1)
            float.TryParse(args[1], out temperature);

        Log.Out(AdvancedFarmingHeatQueryService.SetTemperatureOverride(temperature));
        ExecuteAutoBench(new List<string> { "autobench", growthSeconds, durationSeconds, intervalSeconds });
    }


    private static void ExecuteAutoBench(List<string> args)
    {
        if (args == null || args.Count < 2)
        {
            Log.Out("Usage: rbfarming autobench <growthSeconds> <durationSeconds> <intervalSeconds>");
            Log.Out("Examples:");
            Log.Out("  rbfarming autobench 5400 60 10");
            Log.Out("  rbfarming autobench 10 80 1");
            return;
        }

        string action = (args[1] ?? string.Empty).Trim().ToLowerInvariant();
        if (action == "stop" || action == "off" || action == "cancel")
        {
            StopAutoBench("[AdvancedFarming AutoBench] stopped");
            return;
        }

        if (!int.TryParse(args[1], out int growthSeconds) || growthSeconds < 1)
        {
            Log.Out("Usage: rbfarming autobench <growthSeconds> <durationSeconds> <intervalSeconds>");
            return;
        }

        int durationSeconds = 60;
        int intervalSeconds = growthSeconds <= 15 ? 1 : 10;

        if (args.Count > 2)
            int.TryParse(args[2], out durationSeconds);
        if (args.Count > 3)
            int.TryParse(args[3], out intervalSeconds);

        if (durationSeconds < 5)
            durationSeconds = 5;
        if (durationSeconds > 600)
            durationSeconds = 600;

        if (intervalSeconds < 1)
            intervalSeconds = 1;
        if (intervalSeconds > durationSeconds)
            intervalSeconds = durationSeconds;

        lock (s_autoBenchLock)
        {
            if (s_autoBenchTimer != null)
            {
                s_autoBenchTimer.Dispose();
                s_autoBenchTimer = null;
            }

            AdvancedFarmingRuntimePolicy.SetGrowthSeconds(growthSeconds);
            AdvancedFarmingPerfSnapshotService.ResetSampling();
            AdvancedFarmingPerfSnapshotService.SetSampling(true);

            s_autoBenchGrowthSeconds = growthSeconds;
            s_autoBenchDurationSeconds = durationSeconds;
            s_autoBenchIntervalSeconds = intervalSeconds;
            s_autoBenchIntervalIndex = 0;
            s_autoBenchStartedTicks = DateTime.UtcNow.Ticks;
            s_autoBenchStartSnapshot = AdvancedFarmingPerfSnapshotService.GetSnapshot();
            s_autoBenchLastSnapshot = s_autoBenchStartSnapshot;
            s_autoBenchRunning = true;

            Log.Out("[AdvancedFarming AutoBench] started growthSeconds=" + growthSeconds
                + " durationSeconds=" + durationSeconds
                + " intervalSeconds=" + intervalSeconds
                + " policy=" + AdvancedFarmingRuntimePolicy.Status());

            s_autoBenchTimer = new Timer(AutoBenchTick, null, intervalSeconds * 1000, intervalSeconds * 1000);
        }
    }

    private static void AutoBenchTick(object state)
    {
        lock (s_autoBenchLock)
        {
            if (!s_autoBenchRunning)
                return;

            long elapsedTicks = DateTime.UtcNow.Ticks - s_autoBenchStartedTicks;
            int elapsedSeconds = (int)(elapsedTicks / TimeSpan.TicksPerSecond);
            if (elapsedSeconds < 0)
                elapsedSeconds = 0;

            AdvancedFarmingPerfSnapshotService.SampleSnapshot now = AdvancedFarmingPerfSnapshotService.GetSnapshot();
            s_autoBenchIntervalIndex++;

            Log.Out(AdvancedFarmingPerfSnapshotService.BuildSamplingDeltaReport(
                "growthSeconds=" + s_autoBenchGrowthSeconds + " intervalIndex=" + s_autoBenchIntervalIndex + " elapsedSeconds=" + elapsedSeconds + " interval",
                s_autoBenchLastSnapshot,
                now));

            s_autoBenchLastSnapshot = now;

            if (elapsedSeconds >= s_autoBenchDurationSeconds)
            {
                Log.Out(AdvancedFarmingPerfSnapshotService.BuildSamplingDeltaReport(
                    "growthSeconds=" + s_autoBenchGrowthSeconds + " total",
                    s_autoBenchStartSnapshot,
                    now));

                Log.Out(AdvancedFarmingPerfSnapshotService.BuildSamplingReport());
                AdvancedFarmingPerfSnapshotService.SetSampling(false);

                if (s_autoBenchTimer != null)
                {
                    s_autoBenchTimer.Dispose();
                    s_autoBenchTimer = null;
                }

                s_autoBenchRunning = false;
                Log.Out("[AdvancedFarming AutoBench] complete growthSeconds=" + s_autoBenchGrowthSeconds);
            }
        }
    }

    private static void StopAutoBench(string message)
    {
        lock (s_autoBenchLock)
        {
            if (s_autoBenchTimer != null)
            {
                s_autoBenchTimer.Dispose();
                s_autoBenchTimer = null;
            }

            s_autoBenchRunning = false;
            AdvancedFarmingPerfSnapshotService.SetSampling(false);
            Log.Out(message);
        }
    }


    private static void ExecuteHoverTrace(List<string> args)
    {
        if (args == null || args.Count < 2)
        {
            Log.Out("Usage: rbfarming hovertrace <x> <y> <z> <start|stop|reset|report>");
            Log.Out(AdvancedFarmingHoverTextService.BuildHoverTraceReport());
            return;
        }

        string action = (args[1] ?? string.Empty).Trim().ToLowerInvariant();

        if (action == "report" || action == "status")
        {
            Log.Out(AdvancedFarmingHoverTextService.BuildHoverTraceReport());
            return;
        }

        if (action == "stop" || action == "off")
        {
            Log.Out(AdvancedFarmingHoverTextService.StopHoverTrace());
            return;
        }

        if (action == "reset")
        {
            Log.Out(AdvancedFarmingHoverTextService.ResetHoverTrace());
            return;
        }

        if (args.Count >= 5)
        {
            if (!int.TryParse(args[1], out int x) || !int.TryParse(args[2], out int y) || !int.TryParse(args[3], out int z))
            {
                Log.Out("Usage: rbfarming hovertrace <x> <y> <z> <start|stop|reset|report>");
                return;
            }

            action = (args[4] ?? string.Empty).Trim().ToLowerInvariant();
            Vector3i pos = new Vector3i(x, y, z);

            if (action == "start" || action == "on")
            {
                Log.Out(AdvancedFarmingHoverTextService.StartHoverTrace(pos));
                return;
            }

            if (action == "reset")
            {
                AdvancedFarmingHoverTextService.StartHoverTrace(pos);
                Log.Out(AdvancedFarmingHoverTextService.ResetHoverTrace());
                return;
            }
        }

        Log.Out("Usage: rbfarming hovertrace <x> <y> <z> <start|stop|reset|report>");
    }


#if DEBUG

    private static void ExecuteDebug(List<string> args)
    {
        if (args == null || args.Count < 2)
        {
            Log.Out("Usage: rbfarming debug <hover|tileentity|saveload|water|bootstrap|catchup|all> <on|off|status>");
            Log.Out(AdvancedFarmingDebug.Status());
            return;
        }

        string channel = args[1];
        string value = args.Count > 2 ? (args[2] ?? string.Empty).Trim().ToLowerInvariant() : "status";

        if (value == "on" || value == "true" || value == "1")
            AdvancedFarmingDebug.Set(channel, true);
        else if (value == "off" || value == "false" || value == "0")
            AdvancedFarmingDebug.Set(channel, false);
        else if (value != "status")
        {
            Log.Out("Usage: rbfarming debug <hover|tileentity|saveload|water|bootstrap|catchup|all> <on|off|status>");
            return;
        }

        Log.Out(AdvancedFarmingDebug.Status());
    }

    private static void ExecuteTestCropGrowth(List<string> args)
    {
        string value = (args != null && args.Count > 1) ? (args[1] ?? string.Empty).Trim().ToLowerInvariant() : "status";

        if (value == "on" || value == "true" || value == "1")
            RebirthVariables.testCropGrowth = true;
        else if (value == "off" || value == "false" || value == "0")
            RebirthVariables.testCropGrowth = false;
        else if (value == "toggle")
            RebirthVariables.testCropGrowth = !RebirthVariables.testCropGrowth;
        else if (value != "status")
        {
            Log.Out("Usage: rbfarming testcropgrowth on|off|toggle|status");
            return;
        }

        Log.Out("[AdvancedFarming DEBUG] testCropGrowth=" + RebirthVariables.testCropGrowth + " effectiveGrowthSeconds=" + AdvancedFarmingRuntimePolicy.GetEffectiveGrowthSeconds());
    }
#endif
}

using System;
using System.Collections.Generic;
using UnityEngine.Scripting;

#nullable disable

namespace Rebirth.Bootstrap
{
    public enum RebirthBootstrapInstallState
    {
        NotStarted,
        Installing,
        Complete,
        Partial,
        Failed
    }

    /// <summary>
    /// REBIRTH bootstrap using explicit, feature-owned Harmony installers. Successful steps are
    /// remembered independently so a retry resumes at the missing/failed integration instead of
    /// multiplying already-installed hooks. Assembly-wide PatchAll remains intentionally disabled.
    /// </summary>
    [Preserve]
    public sealed class RebirthFreshInit : IModApi
    {
        private static readonly object Sync = new object();
        private static readonly Dictionary<string, RebirthBootstrapInstallState> Steps =
            new Dictionary<string, RebirthBootstrapInstallState>(StringComparer.Ordinal);
        private static RebirthBootstrapInstallState overallState = RebirthBootstrapInstallState.NotStarted;
        private static string lastFailure = string.Empty;
        private static readonly HarmonyLib.Harmony PerfHarmony =
            new HarmonyLib.Harmony("rebirth.performance.3.1");
        private static readonly HarmonyLib.Harmony DensityHarmony =
            new HarmonyLib.Harmony("rebirth.biome-decoration-density.3.1");

        public static RebirthBootstrapInstallState State { get { lock (Sync) return overallState; } }
        public static string LastFailure { get { lock (Sync) return lastFailure; } }

        public void InitMod(Mod _modInstance)
        {
            lock (Sync)
            {
                if (overallState == RebirthBootstrapInstallState.Complete || overallState == RebirthBootstrapInstallState.Installing)
                    return;
                overallState = RebirthBootstrapInstallState.Installing;
                lastFailure = string.Empty;
            }

            try
            {
                RunStepOnce("harmony-core", delegate { RebirthHarmonyBootstrap.EnsurePatched(); });
                RunStepOnce("server-only-xbl-cleanup", delegate
                {
                    if (!RebirthUnusedServerOnlyXblCleanupInstaller.Install())
                        Log.Warning("[REBIRTH][XBL Cleanup] Native cleanup retained: " +
                            RebirthUnusedServerOnlyXblCleanupInstaller.LastRefusal);
                });
                RunStepOnce("perf-fasttags", delegate { RebirthHarmonyBootstrap.PatchClassOnce(PerfHarmony, typeof(RebirthFastTagsUnionPatch)); });
                RunStepOnce("vehicle-hud-slot-guard", delegate { RebirthHarmonyBootstrap.PatchClassOnce(PerfHarmony, typeof(RebirthHudStatBarSlotGuardPatch)); });
                RunStepOnce("reflection-rate-at-speed", delegate { RebirthHarmonyBootstrap.PatchClassOnce(PerfHarmony, typeof(RebirthReflectionRateAtSpeedPatch)); });
                RunStepOnce("gamesense-option", delegate { RebirthHarmonyBootstrap.PatchClassOnce(PerfHarmony, typeof(RebirthGameSenseOptionPatch)); });
                RunStepOnce("gamesense-dedupe", delegate { Log.Out(RebirthGameSenseDedupePatch.Install(PerfHarmony)); });
                RunStepOnce("density-hooks", delegate
                {
                    RebirthHarmonyBootstrap.PatchClassOnce(DensityHarmony, typeof(Rebirth.WorldDecorations.Density.AddDecoBlockPatch));
                    RebirthHarmonyBootstrap.PatchClassOnce(DensityHarmony, typeof(Rebirth.WorldDecorations.Density.WorldBiomesReadXmlDensityPatch));
                });
                RunStepOnce("npc-prepared-original-event-gate", RebirthNpcPreparedEventGateInstaller.Install);
                RunStepOnce("module-registry", RebirthModuleRegistry.EnsureRegistered);
                RunStepOnce("npc-lifecycle", RebirthNpcLifecycle.EnsureRegistered);
                RunStepOnce("npc-foundation", RebirthNpcFoundationService.Initialize);
                RunStepOnce("patch-registry", RebirthPatchRegistry.EnsureRegistered);
                RunStepOnce("native-controls", RebirthNativeControls.Install);
                RunStepOnce("survivor", delegate
                {
                    string report = RebirthSurvivorInstaller.Install(_modInstance);
                    if (RebirthLogSettings.RuntimeInstallLoggingEnabled) Log.Out(report);
                    if (!RebirthSurvivorInstaller.IsInstalled) throw new InvalidOperationException(report);
                });
                RunStepOnce("metabolism", delegate { string report = RebirthMetabolismInstaller.Install(); if (RebirthLogSettings.RuntimeInstallLoggingEnabled) Log.Out(report); });
                RunStepOnce("cooking", RebirthCookingBatch.Install);
                RunStepOnce("crafting-window-lookup", RebirthCraftingWindowLookup.Install);
                RunStepOnce("inventory-info-window-lookup", RebirthInventoryInfoWindowLookup.Install);
                RunStepOnce("ui-input-style-subscriptions", RebirthInputStyleSubscriptions.Install);
                RunStepOnce("toolbelt-capacity", delegate { string report = RebirthToolbeltCapacityInstaller.Install(); if (RebirthLogSettings.RuntimeInstallLoggingEnabled) Log.Out(report); });
                RunStepOnce("poi-sense", delegate { string report = RebirthPoiSenseRuntimeIntegration.Install(); if (RebirthLogSettings.RuntimeInstallLoggingEnabled) Log.Out(report); });
                RunStepOnce("spawn-composition-service", RebirthSpawnCompositionService.Initialize);
                RunStepOnce("spawn-composition-runtime", delegate { string report = RebirthSpawnCompositionRuntimeIntegration.Install(); if (RebirthLogSettings.RuntimeInstallLoggingEnabled) Log.Out(report); });
                RunStepOnce("vehicle-restoration", RebirthVehicleRestorationInstaller.Install);
                RunStepOnce("vehicle-respawn", delegate { string report = RebirthVehicleRespawnPatchInstaller.Install(); if (RebirthLogSettings.RuntimeInstallLoggingEnabled) Log.Out(report); });
                RunStepOnce("fire", delegate { string report = RebirthFirePatchInstaller.Install(); if (RebirthFirePatchInstaller.FailedMethods > 0) Log.Warning(report); else if (RebirthLogSettings.RuntimeInstallLoggingEnabled) Log.Out(report); });
                RunStepOnce("heat-map", delegate { string report = RebirthHeatMapLifecycleInstaller.Install(); if (RebirthLogSettings.RuntimeInstallLoggingEnabled) Log.Out(report); });
                RunStepOnce("held-item-animator", delegate
                {
                    string report = RebirthCustomHeldItemAnimatorPreserver.Install();
                    if (RebirthLogSettings.RuntimeInstallLoggingEnabled) Log.Out(report);
                    if (!RebirthCustomHeldItemAnimatorPreserver.IsInstalled) throw new InvalidOperationException(report);
                });
                RunStepOnce("tree-replant", RebirthAutoReplantTreesInstaller.Install);
                RunStepOnce("path-smoothing", RebirthHybridPathSmoothingInstaller.Install);
                RunStepOnce("sleeper-respawn", RebirthSleeperRespawnInstaller.Install);
                RunStepOnce("sleeper-multiplier", RebirthSleeperSpawnMultiplierInstaller.Install);
                RunStepOnce("console-popup", RebirthConsolePopupInstaller.Install);
                RunStepOnce("trader-placement", RebirthTraderPlacementExceptionsInstaller.Install);
                RunStepOnce("trader-loot", RebirthTraderLootPolicyInstaller.Install);
                RunStepOnce("trader-jobs", RebirthTraderJobsInstaller.Install);
                RunStepOnce("workstation-fuel", delegate
                {
                    string report = RebirthWorkstationFuelPreservationInstaller.Install();
                    if (RebirthLogSettings.RuntimeInstallLoggingEnabled) Log.Out(report);
                    if (!RebirthWorkstationFuelPreservationInstaller.IsInstalled) throw new InvalidOperationException(report);
                });
                RunStepOnce("weather-fog", delegate { string report = RebirthWeatherFogPatchInstaller.Install(); if (RebirthLogSettings.RuntimeInstallLoggingEnabled) Log.Out(report); });
                RunStepOnce("uniform-atmosphere", delegate { string report = RebirthUniformAtmospherePatchInstaller.Install(); if (RebirthLogSettings.RuntimeInstallLoggingEnabled) Log.Out(report); });
                RunStepOnce("pitch-black", delegate { string report = RebirthPitchBlackPatchInstaller.Install(); if (RebirthLogSettings.RuntimeInstallLoggingEnabled) Log.Out(report); });

                lock (Sync) overallState = RebirthBootstrapInstallState.Complete;
                { if (RebirthLogSettings.RuntimeInstallLoggingEnabled) Log.Out("[REBIRTH Fresh] Bootstrap initialized. modules="
                    + RebirthModuleRegistry.GetModulesSnapshot().Length
                    + " patchEntries=" + RebirthPatchRegistry.GetEntriesSnapshot().Length
                    + " harmonyMode=targeted-installers state=Complete"); }
            }
            catch (Exception ex)
            {
                lock (Sync)
                {
                    overallState = HasCompletedStep() ? RebirthBootstrapInstallState.Partial : RebirthBootstrapInstallState.Failed;
                    lastFailure = ex.GetType().Name + ": " + ex.Message;
                }
                Log.Error("[REBIRTH Fresh] bootstrap " + overallState + ": " + ex.GetType().Name + ": " + ex.Message);
            }
        }

        private static void RunStepOnce(string name, Action action)
        {
            RebirthBootstrapInstallState state;
            lock (Sync)
            {
                if (Steps.TryGetValue(name, out state) && state == RebirthBootstrapInstallState.Complete) return;
                Steps[name] = RebirthBootstrapInstallState.Installing;
            }
            try
            {
                action();
                lock (Sync) Steps[name] = RebirthBootstrapInstallState.Complete;
            }
            catch
            {
                lock (Sync) Steps[name] = RebirthBootstrapInstallState.Failed;
                throw;
            }
        }

        private static bool HasCompletedStep()
        {
            foreach (KeyValuePair<string, RebirthBootstrapInstallState> pair in Steps)
                if (pair.Value == RebirthBootstrapInstallState.Complete) return true;
            return false;
        }
    }
}


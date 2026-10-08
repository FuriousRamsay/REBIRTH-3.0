using System;
using System.Collections.Generic;

#nullable disable

public static class RebirthSurvivorInstaller
{
    private static bool installed;
    private static string lastReport = "[REBIRTH Survivor] not installed";

    public static bool IsInstalled { get { return installed; } }
    public static string LastReport { get { return lastReport; } }

    public static string Install(Mod modInstance)
    {
        if (modInstance != null && !string.IsNullOrEmpty(modInstance.Path))
            RebirthSurvivorDefinitionLoader.SetPreferredModRoot(modInstance.Path);
        return Install();
    }

    public static string Install()
    {
        if (installed && RebirthSurvivorDefinitionRegistry.IsReady && RebirthBackgroundBonusRegistry.IsReady) return lastReport;

        List<string> runtimeErrors = new List<string>();

        // Load and validate all Survivor/progression authority before installing any player-facing
        // Main Menu hooks. The Main Menu must never expose Progression Explorer while its graph
        // is still unavailable.
        RebirthSurvivorDefinitionBundle bundle = RebirthSurvivorDefinitionRegistry.Bundle;
        if (bundle == null)
        {
            try
            {
                string configRoot = RebirthSurvivorDefinitionLoader.ResolveConfigRoot();
                if (RebirthLogSettings.RuntimeInstallLoggingEnabled) Log.Out("[REBIRTH Survivor] loading definitions from '" + configRoot + "'.");
                bundle = RebirthSurvivorDefinitionLoader.Load(configRoot);
                string hash = RebirthSurvivorDefinitionVersion.ComputeSemanticHash(bundle);
                string version = RebirthSurvivorDefinitionVersion.BuildVersionLabel(hash);
                RebirthSurvivorAuthoringReport report = RebirthSurvivorAuthoringValidator.Validate(bundle);
                if (!report.IsValid)
                {
                    // Authoring validation is diagnostic, not a runtime kill-switch.
                    // A stale validator expectation must never prevent the Survivor definitions
                    // from being installed, because doing so leaves the XML-authored Main Menu
                    // buttons visible but prevents their Harmony wiring from ever being installed.
                    //
                    // Keep the full validation report in the log so release-blocking authoring
                    // defects remain visible, but continue with the loaded bundle. Runtime
                    // subsystems perform their own safety/availability checks where required.
                    Log.Error(report.BuildText(hash, version));
                    Log.Warning("[REBIRTH Survivor] authoring validation reported errors; continuing runtime installation so Survivor Profiles and Progression Explorer are not disabled by diagnostic drift.");
                }

                // The Diet/Mood food catalogue is required creator data. Preload it synchronously from
                // REBIRTH authoring during InitMod so the main-menu creator can never open against an
                // uninitialized ItemClass.list and require a leave/re-open retry. ItemClass is only used
                // later as optional presentation enrichment.
                RebirthDietFoodCatalogue.PreloadAuthored(bundle.ConfigRoot);

                RebirthSurvivorDefinitionRegistry.Install(bundle);
                // Main-menu Survivor creation happens without a loaded world, so console commands
                // cannot be used to configure automatic diagnostics. Load the mod-local read-only
                // logging gates as soon as the authoritative config root is known.
                RebirthLogSettings.LoadFromConfigRoot(bundle.ConfigRoot);
                if (RebirthLogSettings.RuntimeInstallLoggingEnabled) Log.Out("[REBIRTH Survivor] definition registry installed backgrounds=" + bundle.Backgrounds.Count
                    + " traits=" + bundle.Traits.Count + " diets=" + bundle.Diets.Count
                    + " root='" + bundle.ConfigRoot + "'.");
            }
            catch (Exception ex)
            {
                RebirthSurvivorDefinitionRegistry.Clear();
                RebirthBackgroundBonusRegistry.Clear();
                installed = false;
                lastReport = "[REBIRTH Survivor] definition install FAILED: " + ex.GetType().Name + ": " + ex.Message;
                Log.Error(lastReport);
                return lastReport;
            }
        }

        // From this point forward the validated definitions are authoritative and must remain
        // available to the creator even if a later runtime integration step fails. A subsystem
        // failure blocks world-character confirmation, but it must never erase Backgrounds,
        // Traits, or Diets from the UI.
        RunStep("mode", delegate
        {
            RebirthSurvivorMode.ApplyFromSandbox(RebirthSandboxOptionManager.Current.PlayerProgression);
            return "mode applied";
        }, runtimeErrors);
        string backgroundBonusReport = RunStep("backgroundBonuses", delegate
        {
            return RebirthBackgroundBonusRegistry.Install(bundle.ConfigRoot);
        }, runtimeErrors);
        RunStep("profileStore", delegate
        {
            RebirthSurvivorProfileStore.Initialize();
            return "profile store initialized";
        }, runtimeErrors);

        string persistenceReport = RunStep("persistence", RebirthSurvivorPersistenceLifecycle.Install, runtimeErrors);
        string creationReport = RunStep("creation", RebirthSurvivorCreationService.Install, runtimeErrors);
        string holdReport = RunStep("hold", RebirthCharacterCreationHoldService.Install, runtimeErrors);
        RunStep("libraryCursorReservation", RebirthBackpackLibraryCursorGuard.Install, runtimeErrors);
        RunStep("gearRecoveryCapture",delegate
        {
            if(!RebirthGearRecoveryEntityCapture.Install(new HarmonyLib.Harmony("rebirth.gear.recovery.capture.v1")))
                throw new InvalidOperationException("Recovery item capture constructor unavailable.");
            return "compact recovery item capture installed";
        },runtimeErrors);
        string networkReport = RunStep("network", RebirthSurvivorNetworkService.Install, runtimeErrors);
        string firstEntryUiReport = RunStep("firstEntryUi", RebirthSurvivorFirstEntryUiService.Install, runtimeErrors);
        string progressionReport = RunStep("progression", RebirthSurvivorProgressionInstaller.Install, runtimeErrors);
        string conditionReport = RunStep("condition", RebirthSurvivorConditionService.Install, runtimeErrors);
        string statisticsReport = RunStep("statistics", RebirthStatisticsService.Install, runtimeErrors);
        string partyIdentityReport = RunStep("partyIdentity", RebirthPartyIdentityService.Install, runtimeErrors);

        // UI hooks are installed last so XUi/Main Menu can only expose the Explorer after the
        // canonical graph has been built from validated Survivor + Capability authority.
        string uiReport = RunStep("ui", RebirthSurvivorUiInstaller.Install, runtimeErrors);

        if (runtimeErrors.Count != 0)
        {
            installed = false;
            lastReport = "[REBIRTH Survivor] definitions ready but runtime install INCOMPLETE: "
                + string.Join(" | ", runtimeErrors.ToArray())
                + " backgrounds=" + bundle.Backgrounds.Count
                + " traits=" + bundle.Traits.Count
                + " diets=" + bundle.Diets.Count
                + " version=" + RebirthSurvivorDefinitionRegistry.DefinitionVersion
                + " hash=" + RebirthSurvivorDefinitionRegistry.SemanticHash;
            Log.Error(lastReport);
            return lastReport;
        }

        installed = true;
        lastReport = "[REBIRTH Survivor] definitions ready backgrounds=" + bundle.Backgrounds.Count
            + " traits=" + bundle.Traits.Count
            + " diets=" + bundle.Diets.Count
            + " skills=" + bundle.Progression.Skills.Count
            + " skillKnowledge=" + bundle.Progression.Skills.Count
            + " legacyKnowledge=" + bundle.Progression.Knowledge.Count
            + " support=" + bundle.SupportProfiles.Count
            + " localProfiles=" + RebirthSurvivorProfileStore.GetProfilesSnapshot().Length
            + " profileIssues=" + RebirthSurvivorProfileStore.GetIssuesSnapshot().Length
            + " persistence=" + persistenceReport
            + " creation=" + creationReport
            + " hold=" + holdReport
            + " network=" + networkReport
            + " firstEntryUi=" + firstEntryUiReport
            + " progression=" + progressionReport
            + " condition=" + conditionReport
            + " statistics=" + statisticsReport
            + " partyIdentity=" + partyIdentityReport
            + " backgroundBonuses=" + backgroundBonusReport
            + " ui=" + uiReport
            + " version=" + RebirthSurvivorDefinitionRegistry.DefinitionVersion
            + " hash=" + RebirthSurvivorDefinitionRegistry.SemanticHash;
        return lastReport;
    }

    private static string RunStep(string name, Func<string> step, List<string> errors)
    {
        { if (RebirthLogSettings.RuntimeInstallLoggingEnabled) RebirthLogSettings.TraceRuntimeInstall("BEGIN step=" + name); }
        try
        {
            string result = step != null ? (step() ?? string.Empty) : string.Empty;
            { if (RebirthLogSettings.RuntimeInstallLoggingEnabled) RebirthLogSettings.TraceRuntimeInstall("PASS step=" + name + " result=" + result); }
            return result;
        }
        catch (Exception ex)
        {
            string error = name + "=" + ex.GetType().Name + ": " + ex.Message;
            errors.Add(error);
            Log.Error("[REBIRTH Survivor][RuntimeInstall] FAIL step=" + name + " harmonyLastClass=" + RebirthHarmonyBootstrap.LastPatchClass + " harmonyLastError=" + RebirthHarmonyBootstrap.LastError);
            Log.Error("[REBIRTH Survivor][RuntimeInstall] exception=" + ex.ToString());
            Exception inner = ex.InnerException;
            int depth = 0;
            while (inner != null && depth < 8)
            {
                Log.Error("[REBIRTH Survivor][RuntimeInstall] inner[" + depth + "]=" + inner.ToString());
                inner = inner.InnerException;
                depth++;
            }
            return "FAILED(" + name + ")";
        }
    }
}

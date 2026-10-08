using System;
using System.Collections.Generic;
using Platform;
using UnityEngine.Scripting;

#nullable disable

[Preserve]
public class ConsoleCmdRebirthSys : ConsoleCmdAbstract
{
    public override bool IsExecuteOnClient { get { return true; } }

    public override string[] getCommands()
    {
        return new[] { "rbsysfresh", "rbsys2", "rbarch" };
    }

    public override string getDescription()
    {
        return "Prints read-only REBIRTH fresh-architecture foundation reports.";
    }

    public override string getHelp()
    {
        return "Usage:\n"
             + "  rbsysfresh modules [category-or-id]\n"
             + "  rbsysfresh budget\n"
             + "  rbsysfresh cost [maxRows]\n"
             + "  rbsysfresh hotmethods\n"
             + "  rbsysfresh patchowners\n"
             + "  rbsysfresh conflicts\n"
             + "  rbsysfresh evidence\n"
             + "  rbsysfresh leaks\n"
             + "  rbsysfresh options [filter]\n"
             + "  rbsysfresh xui\n"
             + "  rbsysfresh external\n"
             + "  rbsysfresh xml\n"
             + "  rbsysfresh xmlfiles [filter]\n"
             + "  rbsysfresh xmlcleanup\n"
             + "  rbsysfresh entities [filter]\n"
             + "  rbsysfresh spawns [filter]\n"
             + "  rbsysfresh spawnrules\n"
             + "  rbsysfresh player\n"
             + "  rbsysfresh playersafety\n"
             + "  rbsysfresh damage\n"
             + "  rbsysfresh damagepolicies\n"
             + "  rbsysfresh damagesafety\n"
#if DEBUG
             + "  rbsysfresh pathing\n"
             + "  rbsysfresh pathbudget\n"
             + "  rbsysfresh astarrisk\n"
             + "  rbsysfresh pathingsafety\n"
#endif
             + "  rbsysfresh items\n"
             + "  rbsysfresh itemrules\n"
             + "  rbsysfresh itemsafety\n"
             + "  rbsysfresh paint\n"
             + "  rbsysfresh paintrules\n"
             + "  rbsysfresh paintsafety\n"
             + "  rbsysfresh caves\n"
             + "  rbsysfresh caverules\n"
             + "  rbsysfresh cavesafety\n"
             + "  rbsysfresh toggles [filter]\n"
             + "  rbsysfresh impact\n"
             + "  rbsysfresh togglepolicy [filter]\n"
             + "  rbsysfresh togglesafety\n"
             + "  rbsysfresh perfbaseline\n"
             + "  rbsysfresh perfdryrun off|on <filter>\n"
             + "  rbsysfresh perfsweep\n"
             + "  rbsysfresh perfsafety\n"
             + "  rbsysfresh state [filter]\n"
             + "  rbsysfresh stateon <filter>\n"
             + "  rbsysfresh stateoff <filter>\n"
             + "  rbsysfresh statereset [filter]\n"
             + "  rbsysfresh manifest [filter]\n"
             + "  rbsysfresh manifesthot\n"
             + "  rbsysfresh manifestmeasure\n"
             + "  rbsysfresh manifestowners\n"
             + "  rbsysfresh patchadapters [filter]\n"
             + "  rbsysfresh patchexcluded\n"
             + "  rbsysfresh patchmeasure\n"
             + "  rbsysfresh patchsafety\n"
             + "  rbsysfresh profiles [filter]\n"
             + "  rbsysfresh profilerules\n"
             + "  rbsysfresh profilepatchcompat\n"
             + "  rbsysfresh profilesafety\n"
             + "  rbsysfresh buildselect <profile>\n"
             + "  rbsysfresh buildstatus\n"
             + "  rbsysfresh buildmanifest [profile]\n"
             + "  rbsysfresh buildreset\n"
             + "  rbsysfresh buildscripts [filter]\n"
             + "  rbsysfresh buildscriptprofile [profile]\n"
             + "  rbsysfresh buildscriptfiles\n"
             + "  rbsysfresh buildscriptsafety\n"
             + "  rbsysfresh buildfiles\n"
             + "  rbsysfresh buildfilessafety\n"
             + "  rbsysfresh scenarioledger [filter]\n"
             + "  rbsysfresh scenariohot\n"
             + "  rbsysfresh scenariodisambiguate\n"
             + "  rbsysfresh scenarioimpact\n"
             + "  rbsysfresh scenarioledgersafety\n"
             + "  rbsysfresh scenariocontracts [filter]\n"
             + "  rbsysfresh scenariodetail <scenario>\n"
             + "  rbsysfresh scenariocombinations\n"
             + "  rbsysfresh scenarioauthority\n"
             + "  rbsysfresh scenariosafety\n"
             + "  rbsysfresh scenariobindings [filter]\n"
             + "  rbsysfresh scenariobinding <scenario>\n"
             + "  rbsysfresh scenariomodule <module>\n"
             + "  rbsysfresh scenarioguardrails\n"
             + "  rbsysfresh scenariobindingssafety\n"
             + "  rbsysfresh scenarioxmlownership [filter]\n"
             + "  rbsysfresh scenarioxml <scenario>\n"
             + "  rbsysfresh scenarioxmllayer <layer>\n"
             + "  rbsysfresh scenarioxmldisambiguate\n"
             + "  rbsysfresh scenarioxmlsafety\n"
             + "  rbsysfresh scenariopolicies [filter]\n"
             + "  rbsysfresh scenariohotgates\n"
             + "  rbsysfresh scenariopolicyauthority\n"
             + "  rbsysfresh scenariopolicyinvalidation\n"
             + "  rbsysfresh scenariopolicysafety\n"
             + "  rbsysfresh scenarioperfsets [filter]\n"
             + "  rbsysfresh scenarioperfmetrics [filter]\n"
             + "  rbsysfresh scenarioperfmatrix\n"
             + "  rbsysfresh scenarioperfblocked\n"
             + "  rbsysfresh scenarioperfsafety\n"
             + "  rbsysfresh scenariomanagers [filter]\n"
             + "  rbsysfresh scenariomanager <scenario>\n"
             + "  rbsysfresh scenariomanagerauthority\n"
             + "  rbsysfresh scenariomanagerlifecycle\n"
             + "  rbsysfresh scenariomanagerblocked\n"
             + "  rbsysfresh scenariomanagersafety\n"
             + "  rbsysfresh features [filter]\n"
             + "  rbsysfresh featurehot\n"
             + "  rbsysfresh featurenetwork\n"
             + "  rbsysfresh featurepersistence\n"
             + "  rbsysfresh featurescenario\n"
             + "  rbsysfresh featureblocked\n"
             + "  rbsysfresh featuresafety\n"
             + "  rbsysfresh featuredeps [filter]\n"
             + "  rbsysfresh featuredepsupstream\n"
             + "  rbsysfresh featuredepsguardrails\n"
             + "  rbsysfresh featuredepsblocked\n"
             + "  rbsysfresh featuredepssafety\n"
             + "  rbsysfresh xmlcleanupplan [filter]\n"
             + "  rbsysfresh xmlrootincludes\n"
             + "  rbsysfresh xmlcleanupplansafety\n"
             + "  rbsysfresh migrationorder [filter]\n"
             + "  rbsysfresh migrationwave <filter>\n"
             + "  rbsysfresh migrationblocked\n"
             + "  rbsysfresh migrationnext\n"
             + "  rbsysfresh migrationordersafety\n"
             + "  rbsysfresh featuretests [filter]\n"
             + "  rbsysfresh featuretestcritical\n"
             + "  rbsysfresh featuretesthot\n"
             + "  rbsysfresh featureteststateful\n"
             + "  rbsysfresh featuretestblocked\n"
             + "  rbsysfresh featuretestsafety\n"
             + "  rbsysfresh netpersist [filter]\n"
             + "  rbsysfresh authoritycontracts\n"
             + "  rbsysfresh persistencecontracts\n"
             + "  rbsysfresh netpersistblocked\n"
             + "  rbsysfresh netpersistsafety\n"
             + "  rbsysfresh buildverify [filter]\n"
             + "  rbsysfresh buildverifyrelease\n"
             + "  rbsysfresh buildverifyprofiles\n"
             + "  rbsysfresh buildverifyhot\n"
             + "  rbsysfresh buildverifysafety\n"
             + "  rbsysfresh harmonystrategy [filter]\n"
             + "  rbsysfresh harmonyhot\n"
             + "  rbsysfresh harmonyreflection\n"
             + "  rbsysfresh harmonypatchtypes\n"
             + "  rbsysfresh harmonyblocked\n"
             + "  rbsysfresh harmonysafety\n"
             + "  rbsysfresh migrationready [filter]\n"
             + "  rbsysfresh migrationrecommended\n"
             + "  rbsysfresh migrationreadyblocked\n"
             + "  rbsysfresh migrationreadysafety\n"
             + "  rbsysfresh runtimepolicy\n"
             + "  rbsysfresh runtimepolicydetail\n"
             + "  rbsysfresh runtimepolicyreset\n"
             + "  rbsysfresh runtimepolicysafety\n"
             + "  rbsysfresh diaggates\n"
             + "  rbsysfresh diaggaterules\n"
             + "  rbsysfresh diaggateexample\n"
             + "  rbsysfresh diaggatesafety\n"
             + "  rbsysfresh profilecompare [filter]\n"
             + "  rbsysfresh profilecomparerelease\n"
             + "  rbsysfresh profilecompareprofiling\n"
             + "  rbsysfresh profilecomparesafety\n"
             + "  rbsysfresh runtimetoggles [filter]\n"
             + "  rbsysfresh runtimetogglesreset\n"
             + "  rbsysfresh runtimetogglessafety\n"
             + "  rbsysfresh hotflags\n"
             + "  rbsysfresh hotflagslist\n"
             + "  rbsysfresh hotflagsintended\n"
             + "  rbsysfresh hotflagssafety\n"
             + "  rbsysfresh runtimehealth\n"
             + "  rbsysfresh runtimehealthfull\n"
             + "  rbsysfresh runtimehealthblockers\n"
             + "  rbsysfresh runtimehealthnext\n"
             + "  rbsysfresh runtimehealthsafety\n"
             + "  rbsysfresh harvestgate [filter]\n"
             + "  rbsysfresh harvestgatecritical\n"
             + "  rbsysfresh harvestgateserver\n"
             + "  rbsysfresh harvestgateperf\n"
             + "  rbsysfresh harvestgateready\n"
             + "  rbsysfresh harvestgatesafety\n"
             + "  rbsysfresh harvestpolicy\n"
             + "  rbsysfresh harvestpolicydetail\n"
             + "  rbsysfresh harvestpolicyrequirements\n"
             + "  rbsysfresh harvestpolicyreset\n"
             + "  rbsysfresh harvestpolicysafety\n"
             + "  rbsysfresh consolidation [filter]\n"
             + "  rbsysfresh consolidationtarget\n"
             + "  rbsysfresh consolidationsafety\n"
             + "  rbsysfresh firstslice [filter]\n"
             + "  rbsysfresh firstsliceblocked\n"
             + "  rbsysfresh firstslicerecommended\n"
             + "  rbsysfresh firstslicesafety\n"
             + "  rbsysfresh patchevidence [filter]\n"
             + "  rbsysfresh patchevidencerequired\n"
             + "  rbsysfresh patchevidenceblocked\n"
             + "  rbsysfresh patchevidencesafety\n"
             + "  rbsysfresh sourceintake [filter]\n"
             + "  rbsysfresh sourceintakemarkdown\n"
             + "  rbsysfresh sourceintakesafety\n"
             + "  rbsysfresh firstsliceaudit [filter]\n"
             + "  rbsysfresh firstsliceauditblockers\n"
             + "  rbsysfresh firstsliceauditsafety\n"
             + "  rbsysfresh firstslicenogo [filter]\n"
             + "  rbsysfresh firstslicenogoblockers\n"
             + "  rbsysfresh firstslicenogosafety\n"
             + "  rbsysfresh harvestadapter\n"
             + "  rbsysfresh harvestadapterpreview [amount]\n"
             + "  rbsysfresh harvestadapterfuture\n"
             + "  rbsysfresh harvestadaptersafety\n"
             + "  rbsysfresh smoketests [filter]\n"
             + "  rbsysfresh smoketestscritical\n"
             + "  rbsysfresh smoketestsorder\n"
             + "  rbsysfresh smoketestssafety\n"
             + "  rbsysfresh milestone [filter]\n"
             + "  rbsysfresh milestonenext\n"
             + "  rbsysfresh milestonesafety\n"
             + "  rbsysfresh sourceaudit [filter]\n"
             + "  rbsysfresh sourceauditrequired\n"
             + "  rbsysfresh sourceauditnogo\n"
             + "  rbsysfresh sourceauditnext\n"
             + "  rbsysfresh sourceauditsafety\n"
             + "  rbsysfresh harvestsourceaudit [filter]\n"
             + "  rbsysfresh harvestsourceauditselected\n"
             + "  rbsysfresh harvestsourceauditrejected\n"
             + "  rbsysfresh harvestsourceauditnext\n"
             + "  rbsysfresh harvestsourceauditsafety\n"
             + "  rbsysfresh cropharvest\n"
             + "  rbsysfresh cropharvestdetail\n"
             + "  rbsysfresh cropharvestpreview [count]\n"
             + "  rbsysfresh cropharvestclassify <blockName>\n"
             + "  rbsysfresh cropharvesttable [filter]\n"
             + "  rbsysfresh cropharvestflow\n"
             + "  rbsysfresh cropharvestsafety\n"
             + "  rbsysfresh cropharvestenabletest <wildBonusPercent> <grownBonusPercent> [downgradePlayerGrown]\n"
             + "  rbsysfresh cropharvestdisable\n"
             + "\n"
             + "Aliases: rbsysfresh, rbsys2, rbarch\n"
             + "\n"
             + "This fresh-project command is read-only. It does not toggle gameplay systems,\n"
             + "install patches, or register scheduler callbacks.\n";
    }

    public override void Execute(List<string> _params, CommandSenderInfo _senderInfo)
    {
        if (_params == null || _params.Count == 0)
        {
            Log.Out(getHelp());
            Log.Out(RebirthModuleRegistry.GetSummary());
            return;
        }

        string cmd = (_params[0] ?? string.Empty).Trim().ToLowerInvariant();

        switch (cmd)
        {
            case "help":
            case "?":
                Log.Out(getHelp());
                return;

            case "status":
            case "summary":
                Log.Out(RebirthModuleRegistry.GetSummary());
                return;

            case "modules":
            case "module":
            case "list":
                string filter = _params.Count > 1 ? _params[1] : null;
                Log.Out(RebirthModuleRegistry.GetList(filter));
                return;

            case "budget":
            case "budgets":
                Log.Out(RebirthModuleRegistry.GetBudgetReport());
                return;

            case "cost":
            case "costs":
                int maxRows = 25;
                if (_params.Count > 1)
                    int.TryParse(_params[1], out maxRows);
                Log.Out(RebirthModuleCostStats.GetCostReport(maxRows));
                return;

            case "leaks":
            case "active":
                Log.Out(RebirthModuleRegistry.GetLeakReport());
                return;

            case "hotmethods":
            case "hot":
            case "dispatchers":
                Log.Out(RebirthArchitectureEvidenceRegistry.GetHotMethodsReport());
                return;

            case "patchowners":
            case "owners":
            case "patches":
                Log.Out(RebirthArchitectureEvidenceRegistry.GetPatchOwnersReport());
                return;

            case "conflicts":
            case "collision":
            case "collisions":
                Log.Out(RebirthArchitectureEvidenceRegistry.GetConflictsReport());
                return;

            case "patchregistry":
            case "registry":
                Log.Out(RebirthPatchRegistry.GetReport());
                return;

            case "bootstrap":
                Log.Out("[REBIRTH Fresh] bootstrap loaded. No Harmony patches installed. modules="
                    + RebirthModuleRegistry.GetModulesSnapshot().Length
                    + " patchEntries=" + RebirthPatchRegistry.GetEntriesSnapshot().Length);
                return;


            case "options":
            case "option":
            case "optionmigration":
                string optionFilter = _params.Count > 1 ? _params[1] : null;
                Log.Out(RebirthOptionMigrationRegistry.GetReport(optionFilter));
                return;

            case "xui":
            case "xuimigration":
                Log.Out(RebirthXUiMigrationRegistry.GetReport());
                return;

            case "external":
            case "externals":
            case "absorption":
                Log.Out(RebirthExternalProjectRegistry.GetReport());
                return;

            case "evidence":
            case "ledger":
                Log.Out(RebirthArchitectureEvidenceRegistry.GetEvidenceReport());
                return;


            case "xml":
            case "xmllayers":
            case "layers":
                Log.Out(RebirthXmlLayerRegistry.GetLayerReport());
                return;

            case "xmlfiles":
            case "xmlfile":
                string xmlFilter = _params.Count > 1 ? _params[1] : null;
                Log.Out(RebirthXmlLayerRegistry.GetFileFamilyReport(xmlFilter));
                return;

            case "xmlcleanup":
            case "cleanupxml":
                Log.Out(RebirthXmlLayerRegistry.GetCleanupReport());
                return;


            case "entities":
            case "entityclasses":
                string entityFilter = _params.Count > 1 ? _params[1] : null;
                Log.Out(RebirthEntityClassRegistry.GetReport(entityFilter));
                return;

            case "spawns":
            case "spawn":
            case "spawngroups":
                string spawnFilter = _params.Count > 1 ? _params[1] : null;
                Log.Out(RebirthSpawnRegistry.GetGroupsReport(spawnFilter));
                return;

            case "spawnrules":
            case "spawningrules":
                Log.Out(RebirthSpawnRegistry.GetRulesReport());
                return;


            case "player":
            case "playermovement":
            case "movement":
                Log.Out(RebirthPlayerMovementPipeline.GetStageReport());
                return;

            case "playersafety":
            case "movementsafety":
                Log.Out(RebirthPlayerMovementPipeline.GetSafetyReport());
                return;


            case "damage":
            case "damagestages":
                Log.Out(RebirthDamagePipeline.GetStageReport());
                return;

            case "damagepolicies":
            case "combatpolicies":
                Log.Out(RebirthDamagePipeline.GetPolicyReport());
                return;

            case "damagesafety":
            case "combatsafety":
                Log.Out(RebirthDamagePipeline.GetSafetyReport());
                return;


#if DEBUG
            case "pathing":
            case "path":
            case "pathstages":
                Log.Out(RebirthPathingPipeline.GetTenantReport());
                return;

            case "pathbudget":
            case "localroutebudget":
                Log.Out(RebirthPathingPipeline.GetBudgetReport());
                return;

            case "astarrisk":
            case "astar":
                Log.Out(RebirthPathingPipeline.GetAStarRiskReport());
                return;

            case "pathingsafety":
            case "pathsafety":
                Log.Out(RebirthPathingPipeline.GetSafetyReport());
                return;


#endif

            case "items":
            case "itemstats":
            case "stats":
                Log.Out(RebirthItemStatPipeline.GetTenantReport());
                return;

            case "itemrules":
            case "statrules":
                Log.Out(RebirthItemStatPipeline.GetRuleReport());
                return;

            case "itemsafety":
            case "statsafety":
                Log.Out(RebirthItemStatPipeline.GetSafetyReport());
                return;


            case "paint":
            case "render":
            case "renderface":
                Log.Out(RebirthPaintPipeline.GetTenantReport());
                return;

            case "paintrules":
            case "renderrules":
                Log.Out(RebirthPaintPipeline.GetRuleReport());
                return;

            case "paintsafety":
            case "rendersafety":
                Log.Out(RebirthPaintPipeline.GetSafetyReport());
                return;


            case "caves":
            case "worldcaves":
            case "worldgen":
                Log.Out(RebirthCavePipeline.GetTenantReport());
                return;

            case "caverules":
            case "worldgenrules":
                Log.Out(RebirthCavePipeline.GetRuleReport());
                return;

            case "cavesafety":
            case "worldgensafety":
                Log.Out(RebirthCavePipeline.GetSafetyReport());
                return;


            case "toggles":
            case "togglecontracts":
                string toggleFilter = _params.Count > 1 ? _params[1] : null;
                Log.Out(RebirthModuleToggleRegistry.GetListReport(toggleFilter));
                return;

            case "impact":
            case "perfimpact":
            case "performanceimpact":
                Log.Out(RebirthModuleToggleRegistry.GetImpactReport());
                return;

            case "togglepolicy":
            case "togglepolicies":
                string policyFilter = _params.Count > 1 ? _params[1] : null;
                Log.Out(RebirthModuleToggleRegistry.GetPolicyReport(policyFilter));
                return;

            case "togglesafety":
                Log.Out(RebirthModuleToggleRegistry.GetSafetyReport());
                return;


            case "perfbaseline":
                Log.Out(RebirthPerformanceDryRunPlanner.GetBaselinePlanReport());
                return;

            case "perfdryrun":
                if (_params.Count < 3)
                {
                    Log.Out("[RebirthPerfDryRun] Usage: rbsysfresh perfdryrun off|on <module-filter>");
                    return;
                }
                string perfState = (_params[1] ?? string.Empty).Trim().ToLowerInvariant();
                bool perfEnable;
                if (perfState == "on" || perfState == "enable" || perfState == "enabled")
                    perfEnable = true;
                else if (perfState == "off" || perfState == "disable" || perfState == "disabled")
                    perfEnable = false;
                else
                {
                    Log.Out("[RebirthPerfDryRun] Unknown state: " + perfState + ". Use on/off.");
                    return;
                }
                Log.Out(RebirthPerformanceDryRunPlanner.GetDryRunReport(perfEnable, _params[2]));
                return;

            case "perfsweep":
                Log.Out(RebirthPerformanceDryRunPlanner.GetSweepPlanReport());
                return;

            case "perfsafety":
                Log.Out(RebirthPerformanceDryRunPlanner.GetSafetyReport());
                return;


            case "state":
            case "togglestate":
                string stateFilter = _params.Count > 1 ? _params[1] : null;
                Log.Out(RebirthModuleToggleStateRegistry.GetStatusReport(stateFilter));
                return;

            case "stateon":
                if (_params.Count < 2)
                {
                    Log.Out("[RebirthToggleState] Usage: rbsysfresh stateon <module-filter>");
                    return;
                }
                Log.Out(RebirthModuleToggleStateRegistry.SetShadowState(_params[1], true, "rbsysfresh shadow enable"));
                return;

            case "stateoff":
                if (_params.Count < 2)
                {
                    Log.Out("[RebirthToggleState] Usage: rbsysfresh stateoff <module-filter>");
                    return;
                }
                Log.Out(RebirthModuleToggleStateRegistry.SetShadowState(_params[1], false, "rbsysfresh shadow disable"));
                return;

            case "statereset":
                string resetFilter = _params.Count > 1 ? _params[1] : null;
                Log.Out(RebirthModuleToggleStateRegistry.ResetShadowState(resetFilter));
                return;


            case "manifest":
            case "modulemanifest":
                string manifestFilter = _params.Count > 1 ? _params[1] : null;
                Log.Out(RebirthModuleManifestRegistry.GetManifestReport(manifestFilter));
                return;

            case "manifesthot":
            case "modulehot":
                Log.Out(RebirthModuleManifestRegistry.GetHotPathManifestReport());
                return;

            case "manifestmeasure":
            case "modulemeasure":
                Log.Out(RebirthModuleManifestRegistry.GetMeasurementManifestReport());
                return;

            case "manifestowners":
            case "moduleowners":
                Log.Out(RebirthModuleManifestRegistry.GetOwnershipSummaryReport());
                return;


            case "patchadapters":
            case "patchadapter":
                string patchFilter = _params.Count > 1 ? _params[1] : null;
                Log.Out(RebirthPatchAdapterPlanRegistry.GetReport(patchFilter));
                return;

            case "patchexcluded":
            case "excludedpatches":
                Log.Out(RebirthPatchAdapterPlanRegistry.GetExcludedReport());
                return;

            case "patchmeasure":
            case "patchmeasurement":
                Log.Out(RebirthPatchAdapterPlanRegistry.GetMeasurementReport());
                return;

            case "patchsafety":
                Log.Out(RebirthPatchAdapterPlanRegistry.GetSafetyReport());
                return;


            case "profiles":
            case "buildprofiles":
                string profileFilter = _params.Count > 1 ? _params[1] : null;
                Log.Out(RebirthBuildProfileRegistry.GetProfileReport(profileFilter));
                return;

            case "profilerules":
            case "buildprofilerules":
                Log.Out(RebirthBuildProfileRegistry.GetRuleReport());
                return;

            case "profilepatchcompat":
            case "buildprofilepatchcompat":
                Log.Out(RebirthBuildProfileRegistry.GetPatchAdapterCompatibilityReport());
                return;

            case "profilesafety":
            case "buildprofilesafety":
                Log.Out(RebirthBuildProfileRegistry.GetSafetyReport());
                return;


            case "buildselect":
                if (_params.Count < 2)
                {
                    Log.Out("[RebirthBuildProfileSelection] Usage: rbsysfresh buildselect release|profiling|stripped|devdiagnostics|compataudit");
                    return;
                }
                Log.Out(RebirthBuildProfileSelectionRegistry.SetProfile(_params[1]));
                return;

            case "buildstatus":
                Log.Out(RebirthBuildProfileSelectionRegistry.GetStatusReport());
                return;

            case "buildmanifest":
                string buildProfile = _params.Count > 1 ? _params[1] : null;
                Log.Out(RebirthBuildProfileSelectionRegistry.GetDryRunManifestReport(buildProfile));
                return;

            case "buildreset":
                Log.Out(RebirthBuildProfileSelectionRegistry.Reset());
                return;


            case "buildscripts":
            case "buildscript":
                string scriptFilter = _params.Count > 1 ? _params[1] : null;
                Log.Out(RebirthBuildScriptTemplateRegistry.GetTemplateReport(scriptFilter));
                return;

            case "buildscriptprofile":
                string scriptProfile = _params.Count > 1 ? _params[1] : null;
                Log.Out(RebirthBuildScriptTemplateRegistry.GetProfileTemplateReport(scriptProfile));
                return;

            case "buildscriptfiles":
                Log.Out(RebirthBuildScriptTemplateRegistry.GetFutureFileNameReport());
                return;

            case "buildscriptsafety":
                Log.Out(RebirthBuildScriptTemplateRegistry.GetSafetyReport());
                return;


            case "buildfiles":
            case "buildscriptfilesgenerated":
                Log.Out(RebirthBuildScriptFileRegistry.GetReport());
                return;

            case "buildfilessafety":
                Log.Out(RebirthBuildScriptFileRegistry.GetSafetyReport());
                return;


            case "scenarioledger":
            case "scenariosurfaces":
                string scenarioLedgerFilter = _params.Count > 1 ? _params[1] : null;
                Log.Out(RebirthScenarioSurfaceLedger.GetSurfaceReport(scenarioLedgerFilter));
                return;

            case "scenariohot":
            case "scenariohotpaths":
                Log.Out(RebirthScenarioSurfaceLedger.GetHotPathReport());
                return;

            case "scenariodisambiguate":
            case "scenariodisambiguation":
                Log.Out(RebirthScenarioSurfaceLedger.GetDisambiguationReport());
                return;

            case "scenarioimpact":
                Log.Out(RebirthScenarioSurfaceLedger.GetArchitectureImpactReport());
                return;

            case "scenarioledgersafety":
                Log.Out(RebirthScenarioSurfaceLedger.GetSafetyReport());
                return;


            case "scenariocontracts":
            case "scenarios":
                string scenarioContractFilter = _params.Count > 1 ? _params[1] : null;
                Log.Out(RebirthScenarioContractRegistry.GetContractReport(scenarioContractFilter));
                return;

            case "scenariodetail":
                if (_params.Count < 2)
                {
                    Log.Out("[RebirthScenarioContracts] Usage: rbsysfresh scenariodetail <scenario>");
                    return;
                }
                Log.Out(RebirthScenarioContractRegistry.GetDetailReport(_params[1]));
                return;

            case "scenariocombinations":
                Log.Out(RebirthScenarioContractRegistry.GetCombinationReport());
                return;

            case "scenarioauthority":
                Log.Out(RebirthScenarioContractRegistry.GetAuthorityReport());
                return;

            case "scenariosafety":
                Log.Out(RebirthScenarioContractRegistry.GetSafetyReport());
                return;


            case "scenariobindings":
                string scenarioBindingFilter = _params.Count > 1 ? _params[1] : null;
                Log.Out(RebirthScenarioModuleBindingRegistry.GetBindingReport(scenarioBindingFilter));
                return;

            case "scenariobinding":
                if (_params.Count < 2)
                {
                    Log.Out("[RebirthScenarioBindings] Usage: rbsysfresh scenariobinding <scenario>");
                    return;
                }
                Log.Out(RebirthScenarioModuleBindingRegistry.GetScenarioReport(_params[1]));
                return;

            case "scenariomodule":
                if (_params.Count < 2)
                {
                    Log.Out("[RebirthScenarioBindings] Usage: rbsysfresh scenariomodule <module>");
                    return;
                }
                Log.Out(RebirthScenarioModuleBindingRegistry.GetModuleReport(_params[1]));
                return;

            case "scenarioguardrails":
                Log.Out(RebirthScenarioModuleBindingRegistry.GetGuardrailReport());
                return;

            case "scenariobindingssafety":
                Log.Out(RebirthScenarioModuleBindingRegistry.GetSafetyReport());
                return;


            case "scenarioxmlownership":
                string scenarioXmlFilter = _params.Count > 1 ? _params[1] : null;
                Log.Out(RebirthScenarioXmlOwnershipRegistry.GetOwnershipReport(scenarioXmlFilter));
                return;

            case "scenarioxml":
                if (_params.Count < 2)
                {
                    Log.Out("[RebirthScenarioXml] Usage: rbsysfresh scenarioxml <scenario>");
                    return;
                }
                Log.Out(RebirthScenarioXmlOwnershipRegistry.GetScenarioReport(_params[1]));
                return;

            case "scenarioxmllayer":
                if (_params.Count < 2)
                {
                    Log.Out("[RebirthScenarioXml] Usage: rbsysfresh scenarioxmllayer <layer>");
                    return;
                }
                Log.Out(RebirthScenarioXmlOwnershipRegistry.GetLayerReport(_params[1]));
                return;

            case "scenarioxmldisambiguate":
                Log.Out(RebirthScenarioXmlOwnershipRegistry.GetDisambiguationReport());
                return;

            case "scenarioxmlsafety":
                Log.Out(RebirthScenarioXmlOwnershipRegistry.GetSafetyReport());
                return;


            case "scenariopolicies":
                string scenarioPolicyFilter = _params.Count > 1 ? _params[1] : null;
                Log.Out(RebirthScenarioRuntimePolicyCacheRegistry.GetSnapshotReport(scenarioPolicyFilter));
                return;

            case "scenariohotgates":
                Log.Out(RebirthScenarioRuntimePolicyCacheRegistry.GetHotPathGateReport());
                return;

            case "scenariopolicyauthority":
                Log.Out(RebirthScenarioRuntimePolicyCacheRegistry.GetAuthorityReport());
                return;

            case "scenariopolicyinvalidation":
                Log.Out(RebirthScenarioRuntimePolicyCacheRegistry.GetInvalidationReport());
                return;

            case "scenariopolicysafety":
                Log.Out(RebirthScenarioRuntimePolicyCacheRegistry.GetSafetyReport());
                return;


            case "scenarioperfsets":
                string scenarioPerfSetFilter = _params.Count > 1 ? _params[1] : null;
                Log.Out(RebirthScenarioPerformanceContractRegistry.GetSetReport(scenarioPerfSetFilter));
                return;

            case "scenarioperfmetrics":
                string scenarioPerfMetricFilter = _params.Count > 1 ? _params[1] : null;
                Log.Out(RebirthScenarioPerformanceContractRegistry.GetMetricReport(scenarioPerfMetricFilter));
                return;

            case "scenarioperfmatrix":
                Log.Out(RebirthScenarioPerformanceContractRegistry.GetMatrixReport());
                return;

            case "scenarioperfblocked":
                Log.Out(RebirthScenarioPerformanceContractRegistry.GetBlockedReport());
                return;

            case "scenarioperfsafety":
                Log.Out(RebirthScenarioPerformanceContractRegistry.GetSafetyReport());
                return;


            case "scenariomanagers":
                string scenarioManagerFilter = _params.Count > 1 ? _params[1] : null;
                Log.Out(RebirthScenarioManagerBoundaryRegistry.GetBoundaryReport(scenarioManagerFilter));
                return;

            case "scenariomanager":
                if (_params.Count < 2)
                {
                    Log.Out("[RebirthScenarioManagers] Usage: rbsysfresh scenariomanager <scenario>");
                    return;
                }
                Log.Out(RebirthScenarioManagerBoundaryRegistry.GetScenarioReport(_params[1]));
                return;

            case "scenariomanagerauthority":
                Log.Out(RebirthScenarioManagerBoundaryRegistry.GetAuthorityReport());
                return;

            case "scenariomanagerlifecycle":
                Log.Out(RebirthScenarioManagerBoundaryRegistry.GetLifecycleReport());
                return;

            case "scenariomanagerblocked":
                Log.Out(RebirthScenarioManagerBoundaryRegistry.GetBlockedReport());
                return;

            case "scenariomanagersafety":
                Log.Out(RebirthScenarioManagerBoundaryRegistry.GetSafetyReport());
                return;


            case "features":
                string featureFilter = _params.Count > 1 ? _params[1] : null;
                Log.Out(RebirthFeatureDomainRegistry.GetDomainReport(featureFilter));
                return;

            case "featurehot":
                Log.Out(RebirthFeatureDomainRegistry.GetHotPathReport());
                return;

            case "featurenetwork":
            case "featureauthority":
                Log.Out(RebirthFeatureDomainRegistry.GetNetworkReport());
                return;

            case "featurepersistence":
            case "featuresaveload":
                Log.Out(RebirthFeatureDomainRegistry.GetPersistenceReport());
                return;

            case "featurescenario":
                Log.Out(RebirthFeatureDomainRegistry.GetScenarioReport());
                return;

            case "featureblocked":
                Log.Out(RebirthFeatureDomainRegistry.GetBlockedReport());
                return;

            case "featuresafety":
                Log.Out(RebirthFeatureDomainRegistry.GetSafetyReport());
                return;


            case "featuredeps":
                string featureDepsFilter = _params.Count > 1 ? _params[1] : null;
                Log.Out(RebirthFeatureDependencyGraphRegistry.GetDependencyReport(featureDepsFilter));
                return;

            case "featuredepsupstream":
                Log.Out(RebirthFeatureDependencyGraphRegistry.GetUpstreamReport());
                return;

            case "featuredepsguardrails":
                Log.Out(RebirthFeatureDependencyGraphRegistry.GetGuardrailReport());
                return;

            case "featuredepsblocked":
                Log.Out(RebirthFeatureDependencyGraphRegistry.GetBlockedReport());
                return;

            case "featuredepssafety":
                Log.Out(RebirthFeatureDependencyGraphRegistry.GetSafetyReport());
                return;


            case "xmlcleanupplan":
                string xmlCleanupPlanFilter = _params.Count > 1 ? _params[1] : null;
                Log.Out(RebirthXmlCleanupPlanRegistry.GetPlanReport(xmlCleanupPlanFilter));
                return;

            case "xmlrootincludes":
                Log.Out(RebirthXmlCleanupPlanRegistry.GetRootIncludeReport());
                return;

            case "xmlcleanupplansafety":
                Log.Out(RebirthXmlCleanupPlanRegistry.GetSafetyReport());
                return;


            case "migrationorder":
                string migrationOrderFilter = _params.Count > 1 ? _params[1] : null;
                Log.Out(RebirthFeatureMigrationOrderRegistry.GetOrderReport(migrationOrderFilter));
                return;

            case "migrationwave":
                if (_params.Count < 2)
                {
                    Log.Out("[RebirthMigrationOrder] Usage: rbsysfresh migrationwave <filter>");
                    return;
                }
                Log.Out(RebirthFeatureMigrationOrderRegistry.GetWaveReport(_params[1]));
                return;

            case "migrationblocked":
                Log.Out(RebirthFeatureMigrationOrderRegistry.GetBlockedReport());
                return;

            case "migrationnext":
                Log.Out(RebirthFeatureMigrationOrderRegistry.GetNextPlanningReport());
                return;

            case "migrationordersafety":
                Log.Out(RebirthFeatureMigrationOrderRegistry.GetSafetyReport());
                return;


            case "featuretests":
                string featureTestsFilter = _params.Count > 1 ? _params[1] : null;
                Log.Out(RebirthFeatureTestMatrixRegistry.GetTestReport(featureTestsFilter));
                return;

            case "featuretestcritical":
                Log.Out(RebirthFeatureTestMatrixRegistry.GetCriticalReport());
                return;

            case "featuretesthot":
                Log.Out(RebirthFeatureTestMatrixRegistry.GetHotPathReport());
                return;

            case "featureteststateful":
                Log.Out(RebirthFeatureTestMatrixRegistry.GetStatefulReport());
                return;

            case "featuretestblocked":
                Log.Out(RebirthFeatureTestMatrixRegistry.GetBlockedReport());
                return;

            case "featuretestsafety":
                Log.Out(RebirthFeatureTestMatrixRegistry.GetSafetyReport());
                return;


            case "netpersist":
                string netPersistFilter = _params.Count > 1 ? _params[1] : null;
                Log.Out(RebirthNetworkPersistenceContractRegistry.GetContractReport(netPersistFilter));
                return;

            case "authoritycontracts":
                Log.Out(RebirthNetworkPersistenceContractRegistry.GetAuthorityReport());
                return;

            case "persistencecontracts":
                Log.Out(RebirthNetworkPersistenceContractRegistry.GetPersistenceReport());
                return;

            case "netpersistblocked":
                Log.Out(RebirthNetworkPersistenceContractRegistry.GetBlockedReport());
                return;

            case "netpersistsafety":
                Log.Out(RebirthNetworkPersistenceContractRegistry.GetSafetyReport());
                return;


            case "buildverify":
                string buildVerifyFilter = _params.Count > 1 ? _params[1] : null;
                Log.Out(RebirthBuildVerificationRegistry.GetGateReport(buildVerifyFilter));
                return;

            case "buildverifyrelease":
                Log.Out(RebirthBuildVerificationRegistry.GetReleaseReport());
                return;

            case "buildverifyprofiles":
                Log.Out(RebirthBuildVerificationRegistry.GetProfilingReport());
                return;

            case "buildverifyhot":
                Log.Out(RebirthBuildVerificationRegistry.GetHotPathReport());
                return;

            case "buildverifysafety":
                Log.Out(RebirthBuildVerificationRegistry.GetSafetyReport());
                return;


            case "harmonystrategy":
                string harmonyStrategyFilter = _params.Count > 1 ? _params[1] : null;
                Log.Out(RebirthHarmonyPatchStrategyRegistry.GetStrategyReport(harmonyStrategyFilter));
                return;

            case "harmonyhot":
                Log.Out(RebirthHarmonyPatchStrategyRegistry.GetHotPathReport());
                return;

            case "harmonyreflection":
                Log.Out(RebirthHarmonyPatchStrategyRegistry.GetReflectionReport());
                return;

            case "harmonypatchtypes":
                Log.Out(RebirthHarmonyPatchStrategyRegistry.GetPatchTypeReport());
                return;

            case "harmonyblocked":
                Log.Out(RebirthHarmonyPatchStrategyRegistry.GetBlockedReport());
                return;

            case "harmonysafety":
                Log.Out(RebirthHarmonyPatchStrategyRegistry.GetSafetyReport());
                return;


            case "migrationready":
                string migrationReadyFilter = _params.Count > 1 ? _params[1] : null;
                Log.Out(RebirthMigrationReadinessRegistry.GetSliceReport(migrationReadyFilter));
                return;

            case "migrationrecommended":
                Log.Out(RebirthMigrationReadinessRegistry.GetRecommendedReport());
                return;

            case "migrationreadyblocked":
                Log.Out(RebirthMigrationReadinessRegistry.GetBlockedReport());
                return;

            case "migrationreadysafety":
                Log.Out(RebirthMigrationReadinessRegistry.GetSafetyReport());
                return;


            case "runtimepolicy":
                Log.Out(RebirthRuntimeScenarioPolicyCache.GetSummaryReport());
                return;

            case "runtimepolicydetail":
                Log.Out(RebirthRuntimeScenarioPolicyCache.GetDetailReport());
                return;

            case "runtimepolicyreset":
                RebirthRuntimeScenarioPolicyCache.ResetToExplicitDefaults("rbsysfresh manual reset");
                Log.Out(RebirthRuntimeScenarioPolicyCache.GetSummaryReport());
                return;

            case "runtimepolicysafety":
                Log.Out(RebirthRuntimeScenarioPolicyCache.GetSafetyReport());
                return;


            case "diaggates":
                Log.Out(RebirthDiagnosticGates.GetSummaryReport());
                return;

            case "diaggaterules":
                Log.Out(RebirthDiagnosticGates.GetRulesReport());
                return;

            case "diaggateexample":
                Log.Out(RebirthDiagnosticGates.GetExampleReport());
                return;

            case "diaggatesafety":
                Log.Out(RebirthDiagnosticGates.GetSafetyReport());
                return;


            case "profilecompare":
                string profileCompareFilter = _params.Count > 1 ? _params[1] : null;
                Log.Out(RebirthBuildProfileComparisonReport.GetComparisonReport(profileCompareFilter));
                return;

            case "profilecomparerelease":
                Log.Out(RebirthBuildProfileComparisonReport.GetReleaseReport());
                return;

            case "profilecompareprofiling":
                Log.Out(RebirthBuildProfileComparisonReport.GetProfilingReport());
                return;

            case "profilecomparesafety":
                Log.Out(RebirthBuildProfileComparisonReport.GetSafetyReport());
                return;


            case "runtimetoggles":
                string runtimeToggleFilter = _params.Count > 1 ? _params[1] : null;
                Log.Out(RebirthRuntimeFeatureToggleState.GetToggleReport(runtimeToggleFilter));
                return;

            case "runtimetogglesreset":
                RebirthRuntimeFeatureToggleState.ResetToExplicitDefaults();
                Log.Out(RebirthRuntimeFeatureToggleState.GetSummaryReport());
                return;

            case "runtimetogglessafety":
                Log.Out(RebirthRuntimeFeatureToggleState.GetSafetyReport());
                return;


            case "hotflags":
                Log.Out(RebirthHotPathFeatureFlags.GetSummaryReport());
                return;

            case "hotflagslist":
                Log.Out(RebirthHotPathFeatureFlags.GetFlagsReport());
                return;

            case "hotflagsintended":
                Log.Out(RebirthHotPathFeatureFlags.GetIntendedUseReport());
                return;

            case "hotflagssafety":
                Log.Out(RebirthHotPathFeatureFlags.GetSafetyReport());
                return;


            case "runtimehealth":
                Log.Out(RebirthRuntimeArchitectureHealth.GetSummaryReport());
                return;

            case "runtimehealthfull":
                Log.Out(RebirthRuntimeArchitectureHealth.GetFullReport());
                return;

            case "runtimehealthblockers":
                Log.Out(RebirthRuntimeArchitectureHealth.GetBlockersReport());
                return;

            case "runtimehealthnext":
                Log.Out(RebirthRuntimeArchitectureHealth.GetRecommendedNextReport());
                return;

            case "runtimehealthsafety":
                Log.Out(RebirthRuntimeArchitectureHealth.GetSafetyReport());
                return;


            case "harvestgate":
                string harvestGateFilter = _params.Count > 1 ? _params[1] : null;
                Log.Out(RebirthHarvestSalvageTestGateRegistry.GetTestReport(harvestGateFilter));
                return;

            case "harvestgatecritical":
                Log.Out(RebirthHarvestSalvageTestGateRegistry.GetCriticalReport());
                return;

            case "harvestgateserver":
                Log.Out(RebirthHarvestSalvageTestGateRegistry.GetServerReport());
                return;

            case "harvestgateperf":
                Log.Out(RebirthHarvestSalvageTestGateRegistry.GetPerformanceReport());
                return;

            case "harvestgateready":
                Log.Out(RebirthHarvestSalvageTestGateRegistry.GetMigrationReadinessReport());
                return;

            case "harvestgatesafety":
                Log.Out(RebirthHarvestSalvageTestGateRegistry.GetSafetyReport());
                return;


            case "harvestpolicy":
                Log.Out(RebirthHarvestSalvageRuntimePolicy.GetSummaryReport());
                return;

            case "harvestpolicydetail":
                Log.Out(RebirthHarvestSalvageRuntimePolicy.GetDetailReport());
                return;

            case "harvestpolicyrequirements":
                Log.Out(RebirthHarvestSalvageRuntimePolicy.GetFuturePatchRequirementsReport());
                return;

            case "harvestpolicyreset":
                RebirthHarvestSalvageRuntimePolicy.ResetToExplicitDefaults("rbsysfresh manual reset");
                Log.Out(RebirthHarvestSalvageRuntimePolicy.GetSummaryReport());
                return;

            case "harvestpolicysafety":
                Log.Out(RebirthHarvestSalvageRuntimePolicy.GetSafetyReport());
                return;


            case "consolidation":
                string consolidationFilter = _params.Count > 1 ? _params[1] : null;
                Log.Out(RebirthProjectConsolidationRegistry.GetPlanReport(consolidationFilter));
                return;

            case "consolidationtarget":
                Log.Out(RebirthProjectConsolidationRegistry.GetTargetShapeReport());
                return;

            case "consolidationsafety":
                Log.Out(RebirthProjectConsolidationRegistry.GetSafetyReport());
                return;


            case "firstslice":
                string firstSliceFilter = _params.Count > 1 ? _params[1] : null;
                Log.Out(RebirthFirstGameplaySliceContractRegistry.GetContractReport(firstSliceFilter));
                return;

            case "firstsliceblocked":
                Log.Out(RebirthFirstGameplaySliceContractRegistry.GetBlockedReport());
                return;

            case "firstslicerecommended":
                Log.Out(RebirthFirstGameplaySliceContractRegistry.GetRecommendedReport());
                return;

            case "firstslicesafety":
                Log.Out(RebirthFirstGameplaySliceContractRegistry.GetSafetyReport());
                return;


            case "patchevidence":
                string patchEvidenceFilter = _params.Count > 1 ? _params[1] : null;
                Log.Out(RebirthPatchTargetEvidenceLedger.GetEvidenceReport(patchEvidenceFilter));
                return;

            case "patchevidencerequired":
                Log.Out(RebirthPatchTargetEvidenceLedger.GetRequiredReport());
                return;

            case "patchevidenceblocked":
                Log.Out(RebirthPatchTargetEvidenceLedger.GetBlockedReport());
                return;

            case "patchevidencesafety":
                Log.Out(RebirthPatchTargetEvidenceLedger.GetSafetyReport());
                return;


            case "sourceintake":
                string sourceIntakeFilter = _params.Count > 1 ? _params[1] : null;
                Log.Out(RebirthSourceEvidenceIntakeTemplate.GetTemplateReport(sourceIntakeFilter));
                return;

            case "sourceintakemarkdown":
                Log.Out(RebirthSourceEvidenceIntakeTemplate.GetBlankMarkdownTemplate());
                return;

            case "sourceintakesafety":
                Log.Out(RebirthSourceEvidenceIntakeTemplate.GetSafetyReport());
                return;


            case "firstsliceaudit":
                string firstSliceAuditFilter = _params.Count > 1 ? _params[1] : null;
                Log.Out(RebirthFirstSliceAuditChecklistRegistry.GetChecklistReport(firstSliceAuditFilter));
                return;

            case "firstsliceauditblockers":
                Log.Out(RebirthFirstSliceAuditChecklistRegistry.GetBlockersReport());
                return;

            case "firstsliceauditsafety":
                Log.Out(RebirthFirstSliceAuditChecklistRegistry.GetSafetyReport());
                return;


            case "firstslicenogo":
                string firstSliceNoGoFilter = _params.Count > 1 ? _params[1] : null;
                Log.Out(RebirthFirstSliceNoGoGate.GetReasonsReport(firstSliceNoGoFilter));
                return;

            case "firstslicenogoblockers":
                Log.Out(RebirthFirstSliceNoGoGate.GetBlockersReport());
                return;

            case "firstslicenogosafety":
                Log.Out(RebirthFirstSliceNoGoGate.GetSafetyReport());
                return;


            case "harvestadapter":
                Log.Out(RebirthHarvestSalvageImplementationAdapter.GetSummaryReport());
                return;

            case "harvestadapterpreview":
                int harvestAdapterAmount = 10;
                if (_params.Count > 1)
                {
                    int parsedAmount;
                    if (int.TryParse(_params[1], out parsedAmount))
                        harvestAdapterAmount = parsedAmount;
                }

                Log.Out(RebirthHarvestSalvageImplementationAdapter.GetPreviewReport(harvestAdapterAmount));
                return;

            case "harvestadapterfuture":
                Log.Out(RebirthHarvestSalvageImplementationAdapter.GetFutureUseReport());
                return;

            case "harvestadaptersafety":
                Log.Out(RebirthHarvestSalvageImplementationAdapter.GetSafetyReport());
                return;


            case "smoketests":
                string smokeTestFilter = _params.Count > 1 ? _params[1] : null;
                Log.Out(RebirthCommandSmokeTestMatrix.GetMatrixReport(smokeTestFilter));
                return;

            case "smoketestscritical":
                Log.Out(RebirthCommandSmokeTestMatrix.GetCriticalReport());
                return;

            case "smoketestsorder":
                Log.Out(RebirthCommandSmokeTestMatrix.GetRecommendedOrderReport());
                return;

            case "smoketestssafety":
                Log.Out(RebirthCommandSmokeTestMatrix.GetSafetyReport());
                return;


            case "milestone":
                string milestoneFilter = _params.Count > 1 ? _params[1] : null;
                Log.Out(RebirthMilestonePlanRegistry.GetPlanReport(milestoneFilter));
                return;

            case "milestonenext":
                Log.Out(RebirthMilestonePlanRegistry.GetNextReport());
                return;

            case "milestonesafety":
                Log.Out(RebirthMilestonePlanRegistry.GetSafetyReport());
                return;


            case "sourceaudit":
                string sourceAuditFilter = _params.Count > 1 ? _params[1] : null;
                Log.Out(RebirthSourceAuditWorkspaceRegistry.GetFilesReport(sourceAuditFilter));
                return;

            case "sourceauditrequired":
                Log.Out(RebirthSourceAuditWorkspaceRegistry.GetRequiredFilesReport());
                return;

            case "sourceauditnogo":
                Log.Out(RebirthSourceAuditWorkspaceRegistry.GetNoGoReport());
                return;

            case "sourceauditnext":
                Log.Out(RebirthSourceAuditWorkspaceRegistry.GetNextReport());
                return;

            case "sourceauditsafety":
                Log.Out(RebirthSourceAuditWorkspaceRegistry.GetSafetyReport());
                return;


            case "harvestsourceaudit":
                string harvestSourceAuditFilter = _params.Count > 1 ? _params[1] : null;
                Log.Out(RebirthHarvestSalvageActualSourceAuditRegistry.GetCandidateReport(harvestSourceAuditFilter));
                return;

            case "harvestsourceauditselected":
                Log.Out(RebirthHarvestSalvageActualSourceAuditRegistry.GetSelectedReport());
                return;

            case "harvestsourceauditrejected":
                Log.Out(RebirthHarvestSalvageActualSourceAuditRegistry.GetRejectedReport());
                return;

            case "harvestsourceauditnext":
                Log.Out(RebirthHarvestSalvageActualSourceAuditRegistry.GetNextReport());
                return;

            case "harvestsourceauditsafety":
                Log.Out(RebirthHarvestSalvageActualSourceAuditRegistry.GetSafetyReport());
                return;


            case "cropharvest":
                Log.Out(RebirthCropActivationHarvestPolicy.GetSummaryReport());
                return;

            case "cropharvestdetail":
                Log.Out(RebirthCropActivationHarvestPolicy.GetDetailReport());
                return;

            case "cropharvestpreview":
                int cropHarvestPreviewCount = 10;
                if (_params.Count > 1)
                {
                    int parsedCropHarvestCount;
                    if (int.TryParse(_params[1], out parsedCropHarvestCount))
                        cropHarvestPreviewCount = parsedCropHarvestCount;
                }

                Log.Out(RebirthCropActivationHarvestPolicy.GetPreviewReport(cropHarvestPreviewCount));
                return;

            case "cropharvestclassify":
                string cropHarvestBlockName = _params.Count > 1 ? _params[1] : null;
                Log.Out(RebirthCropActivationHarvestClassifier.GetPreviewReport(cropHarvestBlockName));
                return;

            case "cropharvestsafety":
                Log.Out(RebirthCropActivationHarvestPolicy.GetSafetyReport());
                Log.Out(RebirthCropActivationHarvestClassifier.GetSafetyReport());
                return;



            case "cropharvesttable":
                string rbsysCropTableFilter = _params.Count > 1 ? _params[1] : null;
                Log.Out(RebirthCropActivationHarvestGeneratedCropTable.GetListReport(rbsysCropTableFilter));
                return;


            case "cropharvestflow":
                Log.Out(RebirthCropActivationHarvestFlow.GetAwardBeforeMutationReport());
                return;

            case "cropharvestenabletest":
                int rbsysCropWildPercent = 0;
                int rbsysCropGrownPercent = 0;
                bool rbsysCropDowngradePlayerGrown = true;

                if (_params.Count > 1)
                    int.TryParse(_params[1], out rbsysCropWildPercent);

                if (_params.Count > 2)
                    int.TryParse(_params[2], out rbsysCropGrownPercent);

                if (_params.Count > 3)
                    bool.TryParse(_params[3], out rbsysCropDowngradePlayerGrown);

                RebirthCropActivationHarvestPolicy.EnableManualTest(rbsysCropWildPercent, rbsysCropGrownPercent, rbsysCropDowngradePlayerGrown);
                Log.Out("[RebirthCropActivationHarvestPolicy] manual test enabled. This state is not saved.");
                Log.Out(RebirthCropActivationHarvestPolicy.GetSummaryReport());
                return;

            case "cropharvestdisable":
                RebirthCropActivationHarvestPolicy.Reset();
                Log.Out("[RebirthCropActivationHarvestPolicy] reset to disabled defaults.");
                return;


            case "harvestsystems":
                Log.Out(RebirthHarvestSystemsPolicy.GetDetailReport());
                return;

            case "harvestsystemsinstallcrop":
                Log.Out(RebirthHarvestSystemsManualPatchInstaller.InstallCropPatch());
                return;

            case "harvestsystemsinstallall":
                Log.Out(RebirthHarvestSystemsManualPatchInstaller.InstallAll());
                return;

            case "harvestsystemsrollback":
                Log.Out(RebirthHarvestSystemsManualPatchInstaller.UninstallAll());
                return;


            case "harvestsystemsenablestumpcontext":
                bool rbsysRequireVehicle = true;
                if (_params.Count > 1)
                    bool.TryParse(_params[1], out rbsysRequireVehicle);

                Log.Out(RebirthHarvestSystemsManualPatchInstaller.InstallStumpHarvestContextPatch());
                RebirthStumpHarvestContextPolicy.EnableManualTest(rbsysRequireVehicle);
                Log.Out(RebirthStumpHarvestContextPolicy.GetSummaryReport());
                return;

            case "harvestsystemsstumpcontext":
                Log.Out(RebirthStumpHarvestContextPolicy.GetSummaryReport());
                Log.Out(RebirthStumpHarvestContextStore.GetListReport());
                return;


            case "harvestsystemsenablestumprewardpreview":
                RebirthStumpHarvestRewardPolicy.EnablePreviewOnly(false, true);
                Log.Out(RebirthStumpHarvestRewardPolicy.GetSummaryReport());
                return;


            case "harvestsystemsenablestumpreward":
                RebirthStumpHarvestRewardPolicy.EnableManualTest("resourceHoney", 1, true, true, true);
                Log.Out(RebirthStumpHarvestRewardPolicy.GetSummaryReport());
                return;


            case "harvestsystemsattackaudit":
                Log.Out(RebirthAttackHarvestTargetAudit.GetReport());
                return;

            case "harvestsystemsenableattackobserve":
                Log.Out(RebirthHarvestSystemsManualPatchInstaller.InstallAttackHarvestPatch());
                RebirthAttackHarvestPolicy.EnableObserveOnly();
                Log.Out(RebirthAttackHarvestPolicy.GetSummaryReport());
                return;

            case "harvestsystemsattackcounters":
                Log.Out(RebirthAttackHarvestCounters.GetDetailReport());
                return;


            case "harvestsystemsenableattackbonus":
                int rbsysDestroyBonus = 0;
                int rbsysHarvestBonus = 0;
                if (_params.Count > 1)
                    int.TryParse(_params[1], out rbsysDestroyBonus);
                if (_params.Count > 2)
                    int.TryParse(_params[2], out rbsysHarvestBonus);

                Log.Out(RebirthHarvestSystemsManualPatchInstaller.InstallAttackHarvestPatch());
                RebirthAttackHarvestPolicy.EnableReplacementMode(false);
                RebirthAttackHarvestBonusPolicy.EnableManualTest(rbsysDestroyBonus, rbsysHarvestBonus, true, true);
                Log.Out(RebirthAttackHarvestBonusPolicy.GetSummaryReport());
                return;

            default:
                Log.Out("[RebirthArchitecture] Unknown subcommand: " + cmd);
                Log.Out(getHelp());
                return;
        }
    }
}

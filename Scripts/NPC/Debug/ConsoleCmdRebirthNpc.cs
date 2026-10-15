#if DEBUG
using System;
using System.Collections.Generic;
using UnityEngine.Scripting;

#nullable disable

[Preserve]
public sealed class ConsoleCmdRebirthNpc : ConsoleCmdAbstract
{
    public override bool IsExecuteOnClient => true;

    public override string[] getCommands()
    {
        return new[] { "rbnpc", "rebirthnpc" };
    }

    public override string getDescription()
    {
        return "REBIRTH NPC framework status, authoring validation and gated diagnostics.";
    }

    public override string getHelp()
    {
        return "Usage:\n"
             + "  rbnpc status\n"
             + "  rbnpc validate\n"
             + "  rbnpc profiles\n"
             + "  rbnpc lifecycle\n"
             + "  rbnpc network\n"
             + "  rbnpc actors\n"
             + "  rbnpc transitions\n"
             + "  rbnpc commands\n"
             + "  rbnpc commandpolicy\n"
             + "  rbnpc leases\n"
             + "  rbnpc activities\n"
             + "  rbnpc activitypolicy\n"
             + "  rbnpc recoveries\n"
             + "  rbnpc controllers\n"
             + "  rbnpc stationary\n"
             + "  rbnpc navigation\n"
             + "  rbnpc follow\n"
             + "  rbnpc guardnav\n"
             + "  rbnpc patrol\n"
             + "  rbnpc work\n"
             + "  rbnpc workdomain\n"
             + "  rbnpc travel\n"
             + "  rbnpc mission\n"
             + "  rbnpc persistence\n"
             + "  rbnpc identities\n"
             + "  rbnpc identityconflicts\n"
             + "  rbnpc schemas\n"
             + "  rbnpc persistencehealth\n"
             + "  rbnpc persistencemanifest\n"
             + "  rbnpc inventory\n"
             + "  rbnpc itemintegrity\n"
             + "  rbnpc farmlogistics\n"
             + "  rbnpc productionresources|dutymedical\n"
             + "  rbnpc decision\n"
             + "  rbnpc social\n"
             + "  rbnpc interact\n"
             + "  rbnpc simulation\n"
             + "  rbnpc compatibility\n"
             + "  rbnpc adapters\n"
             + "  rbnpc legacymigration\n"
             + "  rbnpc externalcapabilities\n"
             + "  rbnpc scale\n"
             + "  rbnpc scalebenchmark [entities] [queries]\n"
             + "  rbnpc performance\n"
             + "  rbnpc administration\n"
             + "  rbnpc conformance [ACIP-##]\n"
             + "  rbnpc progression [selftest]\n"
             + "  rbnpc debug on|off\n"
             + "\nThe command is read-only except for enabling or disabling diagnostics.\n";
    }

    public override void Execute(List<string> parameters, CommandSenderInfo senderInfo)
    {
        string operation = parameters == null || parameters.Count == 0
            ? "status"
            : (parameters[0] ?? string.Empty).Trim().ToLowerInvariant();

        switch (operation)
        {
            case "status":
                Log.Out(RebirthNpcDiagnostics.GetStatus());
                return;
            case "validate":
                Log.Out(RebirthNpcAuthoringValidator.ValidateLoadedEntityClasses().ToReport());
                return;
            case "conformance":
                Log.Out(RebirthNpcConformanceControlPlane.GetReport(parameters.Count > 1 ? parameters[1] : "ACIP-00"));
                return;
            case "progression":
                Log.Out(parameters.Count > 1 && string.Equals(parameters[1], "selftest", StringComparison.OrdinalIgnoreCase) ? RebirthNpcProgressionQualificationService.RunSelfTest() : RebirthNpcProgressionService.GetReport()+"\n"+RebirthNpcProgressionPersistenceStore.GetReport());
                return;
            case "profiles":
                Log.Out(RebirthNpcDiagnostics.GetProfilesReport());
                return;
            case "lifecycle":
                Log.Out(RebirthNpcLifecycle.GetReport());
                return;
            case "lifecyclequal":
            case "lifecyclequalification":
                Log.Out(RebirthNpcLifecycleQualificationService.GetReport());
                return;
            case "network":
                Log.Out(RebirthNpcNetworkProtocol.GetReport());
                return;
            case "compatibility":
                RebirthNpcCompatibilityQualificationService.Audit(true);
                Log.Out(RebirthNpcCompatibilityQualificationService.GetReport());
                return;
            case "adapters":
                RebirthNpcBaseGameAdapterRegistry.Refresh();
                Log.Out(RebirthNpcBaseGameAdapterRegistry.GetReport());
                return;
            case "legacymigration":
                Log.Out(RebirthNpcLegacyMigrationRegistry.GetReport());
                return;
            case "externalcapabilities":
                Log.Out(RebirthNpcThirdPartyCapabilityRegistry.GetReport());
                return;
            case "actors":
                if (!RebirthNpcDiagnostics.Enabled)
                {
                    Log.Out("[REBIRTH NPC] actor diagnostics are disabled. Use: rbnpc debug on");
                    return;
                }
                Log.Out(RebirthNpcDiagnostics.GetActorsReport());
                return;
            case "transitions":
                Log.Out(RebirthNpcDiagnostics.GetTransitionReport());
                return;
            case "commandpolicy":
                Log.Out(RebirthNpcCommandPolicy.GetReport());
                return;
            case "leases":
                if (!RebirthNpcDiagnostics.Enabled)
                {
                    Log.Out("[REBIRTH NPC] execution lease diagnostics are disabled. Use: rbnpc debug on");
                    return;
                }
                Log.Out(RebirthNpcExecutionLeaseDiagnostics.GetReport());
                return;
            case "activities":
                if (!RebirthNpcDiagnostics.Enabled)
                {
                    Log.Out("[REBIRTH NPC] activity diagnostics are disabled. Use: rbnpc debug on");
                    return;
                }
                Log.Out(RebirthNpcActivityDiagnostics.GetReport());
                return;
            case "activitypolicy":
                Log.Out(RebirthNpcActivityResultPolicy.GetReport());
                return;
            case "recoveries":
                if (!RebirthNpcDiagnostics.Enabled)
                {
                    Log.Out("[REBIRTH NPC] activity recovery diagnostics are disabled. Use: rbnpc debug on");
                    return;
                }
                Log.Out(RebirthNpcActivityRecoveryDiagnostics.GetReport());
                return;
            case "controllers":
                Log.Out(RebirthNpcActivityDispatchDiagnostics.GetReport());
                return;
            case "stationary":
                if (!RebirthNpcDiagnostics.Enabled)
                {
                    Log.Out("[REBIRTH NPC] stationary activity diagnostics are disabled. Use: rbnpc debug on");
                    return;
                }
                Log.Out(RebirthNpcStationaryActivityDiagnostics.GetReport());
                return;
            case "navigation":
                if (!RebirthNpcDiagnostics.Enabled)
                {
                    Log.Out("[REBIRTH NPC] navigation diagnostics are disabled. Use: rbnpc debug on");
                    return;
                }
                Log.Out(RebirthNpcNavigationRuntimeAdapter.GetReport());
                return;
            case "follow":
                if (!RebirthNpcDiagnostics.Enabled)
                {
                    Log.Out("[REBIRTH NPC] follow diagnostics are disabled. Use: rbnpc debug on");
                    return;
                }
                Log.Out(RebirthNpcFollowDiagnostics.GetReport());
                return;
            case "guardnav":
                if (!RebirthNpcDiagnostics.Enabled)
                {
                    Log.Out("[REBIRTH NPC] Guard navigation diagnostics are disabled. Use: rbnpc debug on");
                    return;
                }
                Log.Out(RebirthNpcGuardNavigationDiagnostics.GetReport());
                return;
            case "patrol":
                if (!RebirthNpcDiagnostics.Enabled)
                {
                    Log.Out("[REBIRTH NPC] Patrol diagnostics are disabled. Use: rbnpc debug on");
                    return;
                }
                Log.Out(RebirthNpcPatrolDiagnostics.GetReport());
                return;
            case "work":
                if (!RebirthNpcDiagnostics.Enabled)
                {
                    Log.Out("[REBIRTH NPC] Work diagnostics are disabled. Use: rbnpc debug on");
                    return;
                }
                Log.Out(RebirthNpcWorkDiagnostics.GetReport());
                Log.Out(RebirthNpcWorkDefinitionRegistry.Count == 0 ? "[REBIRTH NPC Work Definitions] registered=0" : "[REBIRTH NPC Work Definitions] registered=" + RebirthNpcWorkDefinitionRegistry.Count);
                Log.Out(RebirthNpcWorkCapabilityService.GetReport());
                Log.Out(RebirthNpcWorkAssignmentService.GetReport());
                Log.Out(RebirthNpcWorkReservationService.GetReport());
                Log.Out(RebirthNpcWorkAssignmentCoordinator.GetReport());
                Log.Out(RebirthNpcWorkTargetAdapterRegistry.GetReport());
                Log.Out(RebirthNpcConcreteWorkAdapterRegistry.GetReport() + "\n" + RebirthNpcAdvancedFarmingHarvestAdapter.GetReport() + "\n" + RebirthNpcAdvancedFarmingPlantingWateringAdapter.GetReport() + "\n" + RebirthNpcFarmingWorkPackageService.GetReport());
                Log.Out(RebirthNpcHaulingPlanRegistry.GetReport());
                Log.Out(RebirthNpcSettlementInventoryEndpointRegistry.GetReport());
                Log.Out(RebirthNpcSettlementLogisticsPackageService.GetReport());
                Log.Out(RebirthNpcWorkstationExecutionService.GetReport() + "\n" +
                    RebirthNpcConstructionWorkPackageService.GetReport() + "\n" +
                    RebirthNpcBlockConstructionAdapter.GetReport() + "\n" +
                    RebirthNpcMiningWorkPackageService.GetReport() + "\n" +
                    RebirthNpcBlockMiningAdapter.GetReport() + "\n" +
                    RebirthNpcScavengingWorkPackageService.GetReport() + "\n" +
                    RebirthNpcContainerScavengingAdapter.GetReport() + "\n" +
                    RebirthNpcGuardDutyWorkPackageService.GetReport() + "\n" +
                    RebirthNpcGuardPostWorkAdapter.GetReport() + "\n" +
                    RebirthNpcMedicalTreatmentWorkPackageService.GetReport() + "\n" +
                    RebirthNpcMedicalTreatmentAdapter.GetReport());
                Log.Out(RebirthNpcPhysicalResourceEndpointRegistry.GetReport());
                Log.Out(RebirthNpcWorkExecutionTelemetry.GetReport());
                Log.Out(RebirthNpcWorkNavigationService.GetReport());
                Log.Out(RebirthNpcWorkOutcomeService.GetReport());
                Log.Out(RebirthNpcWorkOutcomePersistenceStore.GetReport());
                Log.Out(RebirthNpcWorkCheckpointPersistenceStore.GetReport());
                return;
            case "workdomain":
                if (!RebirthNpcDiagnostics.Enabled)
                {
                    Log.Out("[REBIRTH NPC] work-domain diagnostics are disabled. Use: rbnpc debug on");
                    return;
                }
                Log.Out("[REBIRTH NPC Work Definitions] registered=" + RebirthNpcWorkDefinitionRegistry.Count);
                Log.Out(RebirthNpcWorkCapabilityService.GetReport());
                Log.Out(RebirthNpcWorkAssignmentService.GetReport());
                Log.Out(RebirthNpcWorkReservationService.GetReport());
                Log.Out(RebirthNpcWorkAssignmentCoordinator.GetReport());
                Log.Out(RebirthNpcWorkTargetAdapterRegistry.GetReport());
                Log.Out(RebirthNpcConcreteWorkAdapterRegistry.GetReport() + "\n" + RebirthNpcAdvancedFarmingHarvestAdapter.GetReport() + "\n" + RebirthNpcAdvancedFarmingPlantingWateringAdapter.GetReport() + "\n" + RebirthNpcFarmingWorkPackageService.GetReport());
                Log.Out(RebirthNpcHaulingPlanRegistry.GetReport());
                Log.Out(RebirthNpcSettlementInventoryEndpointRegistry.GetReport());
                Log.Out(RebirthNpcSettlementLogisticsPackageService.GetReport());
                Log.Out(RebirthNpcWorkstationExecutionService.GetReport() + "\n" +
                    RebirthNpcConstructionWorkPackageService.GetReport() + "\n" +
                    RebirthNpcBlockConstructionAdapter.GetReport() + "\n" +
                    RebirthNpcMiningWorkPackageService.GetReport() + "\n" +
                    RebirthNpcBlockMiningAdapter.GetReport() + "\n" +
                    RebirthNpcScavengingWorkPackageService.GetReport() + "\n" +
                    RebirthNpcContainerScavengingAdapter.GetReport() + "\n" +
                    RebirthNpcGuardDutyWorkPackageService.GetReport() + "\n" +
                    RebirthNpcGuardPostWorkAdapter.GetReport() + "\n" +
                    RebirthNpcMedicalTreatmentWorkPackageService.GetReport() + "\n" +
                    RebirthNpcMedicalTreatmentAdapter.GetReport());
                Log.Out(RebirthNpcPhysicalResourceEndpointRegistry.GetReport());
                return;
            case "travel":
                if (!RebirthNpcDiagnostics.Enabled)
                {
                    Log.Out("[REBIRTH NPC] Travel diagnostics are disabled. Use: rbnpc debug on");
                    return;
                }
                Log.Out(RebirthNpcTravelDiagnostics.GetReport());
                return;
            case "mission":
                if (!RebirthNpcDiagnostics.Enabled)
                {
                    Log.Out("[REBIRTH NPC] Mission diagnostics are disabled. Use: rbnpc debug on");
                    return;
                }
                Log.Out(RebirthNpcMissionDiagnostics.GetReport());
                return;
            case "persistence":
                if (!RebirthNpcDiagnostics.Enabled)
                {
                    Log.Out("[REBIRTH NPC] persistence diagnostics are disabled. Use: rbnpc debug on");
                    return;
                }
                Log.Out(RebirthNpcExecutionPersistenceStore.GetReport());
                return;
            case "identities":
                if (!RebirthNpcDiagnostics.Enabled)
                {
                    Log.Out("[REBIRTH NPC] identity diagnostics are disabled. Use: rbnpc debug on");
                    return;
                }
                Log.Out(RebirthNpcStableIdentityStore.GetReport());
                return;
            case "schemas":
                if (!RebirthNpcDiagnostics.Enabled)
                {
                    Log.Out("[REBIRTH NPC] persistence schema diagnostics are disabled. Use: rbnpc debug on");
                    return;
                }
                Log.Out(RebirthNpcPersistenceSchemaTelemetry.GetReport());
                return;
            case "persistencehealth":
                if (!RebirthNpcDiagnostics.Enabled)
                {
                    Log.Out("[REBIRTH NPC] persistence health diagnostics are disabled. Use: rbnpc debug on");
                    return;
                }
                Log.Out(RebirthNpcPersistenceSchemaTelemetry.GetHealthReport());
                return;
            case "persistencemanifest":
                if (!RebirthNpcDiagnostics.Enabled)
                {
                    Log.Out("[REBIRTH NPC] persistence manifest diagnostics are disabled. Use: rbnpc debug on");
                    return;
                }
                Log.Out(RebirthNpcPersistenceCoordinator.GetReport());
                return;
            case "inventory":
                if (!RebirthNpcDiagnostics.Enabled)
                {
                    Log.Out("[REBIRTH NPC] inventory diagnostics are disabled. Use: rbnpc debug on");
                    return;
                }
                Log.Out(RebirthNpcInventoryTransactionService.GetReport());
                Log.Out(RebirthNpcInventoryAuthorityService.GetReport());
                Log.Out(RebirthNpcExternalInventoryTransferCoordinator.GetReport());
                Log.Out(RebirthNpcInventoryDispositionService.GetReport());
                Log.Out(RebirthNpcInventoryPersistenceStore.GetReport());
                Log.Out(RebirthNpcEquipmentService.GetReport());
                Log.Out(RebirthNpcNativeEquipmentBridge.GetReport());
                Log.Out(RebirthNpcEquipmentPersistenceStore.GetReport());
                return;
            case "itemintegrity":
                Log.Out(RebirthNpcItemIntegrityService.GetReport());
                return;
            case "farmlogistics":
                Log.Out(RebirthNpcFarmingLogisticsQualificationService.GetReport());
                Log.Out(RebirthNpcFarmingWorkPackageService.GetReport());
                Log.Out(RebirthNpcSettlementLogisticsPackageService.GetReport());
                Log.Out(RebirthNpcSettlementInventoryEndpointRegistry.GetReport());
                return;
            case "productionresources":
                RebirthNpcProductionResourceQualificationService.Audit();
                Log.Out(RebirthNpcProductionResourceQualificationService.GetReport());
                Log.Out(RebirthNpcWorkstationExecutionService.GetReport());
                Log.Out(RebirthNpcConstructionWorkPackageService.GetReport());
                Log.Out(RebirthNpcMiningWorkPackageService.GetReport());
                Log.Out(RebirthNpcScavengingWorkPackageService.GetReport());
                return;
            case "dutymedical":
                RebirthNpcDutyMedicalQualificationService.Audit();
                Log.Out(RebirthNpcDutyMedicalQualificationService.GetReport());
                Log.Out(RebirthNpcGuardDutyWorkPackageService.GetReport());
                Log.Out(RebirthNpcGuardPostWorkAdapter.GetReport());
                Log.Out(RebirthNpcMedicalTreatmentWorkPackageService.GetReport());
                Log.Out(RebirthNpcMedicalTreatmentAdapter.GetReport());
                return;
            case "decision":
                if (!RebirthNpcDiagnostics.Enabled)
                {
                    Log.Out("[REBIRTH NPC] decision diagnostics are disabled. Use: rbnpc debug on");
                    return;
                }
                Log.Out(RebirthNpcDecisionEngine.GetReport());
                return;
            case "interact":
                {
                    EntityPlayerLocal player = GameManager.Instance != null && GameManager.Instance.World != null
                        ? GameManager.Instance.World.GetPrimaryPlayer() : null;
                    string interactionError = "No local player is available.";
                    if (player == null || player.PlayerUI == null ||
                        !RebirthNpcInteractionUiService.OpenNearest(player.PlayerUI.xui, out interactionError))
                        Log.Out("[REBIRTH NPC] " + interactionError);
                    return;
                }
            case "scale":
                if (!RebirthNpcDiagnostics.Enabled)
                {
                    Log.Out("[REBIRTH NPC] scale diagnostics are disabled. Use: rbnpc debug on");
                    return;
                }
                Log.Out(RebirthNpcScaleHardeningService.GetReport());
                Log.Out(RebirthNpcScaleBenchmarkService.GetReport());
                return;
            case "scalebenchmark":
                if (!RebirthNpcDiagnostics.Enabled)
                {
                    Log.Out("[REBIRTH NPC] scale benchmark is disabled. Use: rbnpc debug on");
                    return;
                }
                int benchmarkEntities = ParsePositive(parameters, 1, 2000);
                int benchmarkQueries = ParsePositive(parameters, 2, 500);
                RebirthNpcScaleBenchmarkService.Run(benchmarkEntities, benchmarkQueries);
                Log.Out(RebirthNpcScaleBenchmarkService.GetReport());
                Log.Out(RebirthNpcScaleHardeningService.GetReport());
                return;
            case "administration":
                Log.Out(RebirthNpcAdministrationSupportService.GetReport());
                return;
            case "performance":
                if (!RebirthNpcDiagnostics.Enabled)
                {
                    Log.Out("[REBIRTH NPC] performance diagnostics are disabled. Use: rbnpc debug on");
                    return;
                }
                RebirthNpcPerformanceQualificationService.Capture();
                Log.Out(RebirthNpcPerformanceQualificationService.GetReport());
                return;
            case "releasequalification":
                if (!RebirthNpcDiagnostics.Enabled)
                {
                    Log.Out("[REBIRTH NPC] release qualification diagnostics are disabled. Use: rbnpc debug on");
                    return;
                }
                Log.Out(RebirthNpcReleaseQualificationService.RunStaticQualification());
                return;
            case "faction":
                if (!RebirthNpcDiagnostics.Enabled)
                {
                    Log.Out("[REBIRTH NPC] faction diagnostics are disabled. Use: rbnpc debug on");
                    return;
                }
                Log.Out(RebirthNpcFactionGameplayService.GetReport());
                return;
            case "social":
                if (!RebirthNpcDiagnostics.Enabled)
                {
                    Log.Out("[REBIRTH NPC] social diagnostics are disabled. Use: rbnpc debug on");
                    return;
                }
                Log.Out(RebirthNpcSocialService.GetReport());
                Log.Out(RebirthNpcSocialGameplayEventProducers.GetReport());
                Log.Out(RebirthNpcInteractionSessionService.GetReport());
                Log.Out(RebirthNpcInteractionCommandRouter.GetReport());
                Log.Out(RebirthNpcInteractionPresentationService.GetReport());
                Log.Out(RebirthNpcInteractionMutationHandlers.GetReport());
                Log.Out(RebirthNpcTradeService.GetReport());
                Log.Out(RebirthNpcDialogueService.GetReport());
                Log.Out(RebirthNpcHireService.GetReport());
                return;
            case "simulation":
                if (!RebirthNpcDiagnostics.Enabled)
                {
                    Log.Out("[REBIRTH NPC] simulation diagnostics are disabled. Use: rbnpc debug on");
                    return;
                }
                Log.Out(RebirthNpcSettlementSimulation.GetReport());
                Log.Out(RebirthNpcSettlementOperations.GetReport());
                Log.Out(RebirthNpcSettlementPersistenceStore.GetReport());
                return;
            case "identityconflicts":
                if (!RebirthNpcDiagnostics.Enabled)
                {
                    Log.Out("[REBIRTH NPC] identity conflict diagnostics are disabled. Use: rbnpc debug on");
                    return;
                }
                Log.Out(RebirthNpcStableIdentityStore.GetConflictReport());
                return;
            case "commands":
                if (!RebirthNpcDiagnostics.Enabled)
                {
                    Log.Out("[REBIRTH NPC] command diagnostics are disabled. Use: rbnpc debug on");
                    return;
                }
                Log.Out(RebirthNpcCommandDiagnostics.GetReport());
                return;
            case "debug":
                if (parameters.Count < 2)
                {
                    Log.Out("[REBIRTH NPC] debug=" + RebirthNpcDiagnostics.Enabled);
                    return;
                }
                string value = parameters[1] ?? string.Empty;
                if (value.Equals("on", StringComparison.OrdinalIgnoreCase) || value.Equals("true", StringComparison.OrdinalIgnoreCase))
                    RebirthNpcDiagnostics.Enabled = true;
                else if (value.Equals("off", StringComparison.OrdinalIgnoreCase) || value.Equals("false", StringComparison.OrdinalIgnoreCase))
                    RebirthNpcDiagnostics.Enabled = false;
                else
                {
                    Log.Out("Usage: rbnpc debug on|off");
                    return;
                }
                Log.Out("[REBIRTH NPC] debug=" + RebirthNpcDiagnostics.Enabled);
                return;
            case "help":
            case "?":
                Log.Out(getHelp());
                return;
            default:
                Log.Out("[REBIRTH NPC] unknown subcommand: " + operation);
                Log.Out(getHelp());
                return;
        }
    }
    private static int ParsePositive(List<string> parameters, int index, int fallback)
    {
        if (parameters == null || index < 0 || index >= parameters.Count) return fallback;
        int value;
        return int.TryParse(parameters[index], out value) && value > 0 ? value : fallback;
    }

}
#endif

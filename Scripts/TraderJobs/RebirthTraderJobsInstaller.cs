using HarmonyLib;

#nullable disable

/// <summary>
/// Explicit installer for REBIRTH trader-job patches. The project intentionally does
/// not use assembly-wide PatchAll, so trader features must own their Harmony install.
/// </summary>
public static class RebirthTraderJobsInstaller
{
    private static bool installed;

    public static void Install()
    {
        if (installed)
            return;

        installed = true;
        Harmony harmony = new Harmony("rebirth.trader-jobs.3.1");
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthTraderIntroductionAssignmentPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthTraderWorkspaceStockPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthTraderQuantitySelectionPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthTierVehicleDisplayPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthTierVehicleItemPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthTraderJobsToNextTierPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthEntityTraderExpandedJobGenerationPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthEntityTraderJobListingPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthTraderClientOfferSnapshotPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthTraderPoiCompleteQuestPersistencePatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthTraderDeathStartsGraceImmediatelyPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthTraderQuestDeathGracePatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthTraderDeathDisconnectFailureGuardPatch));
        // Boundary patches do NOT start grace. They only preserve a death/disconnect
        // timer that already exists; ordinary walk-outs still fail immediately.
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthTraderExistingGracePoiBoundaryPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthTraderExistingGraceStayWithinPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthTraderExistingGraceStayWithinStatusPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthTraderRallyStartsNewJobGracePatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthTraderGracePoiReservationServerPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthTraderReconnectPreserveSharedPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthTraderReconnectStartQuestPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthTraderLocalWorldExitGracePatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthTraderDisconnectCapturePatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthTraderReconnectSyncPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthTraderOfflinePartyCompletionPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthTraderQuestGraceCompassUpdatePatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthTraderQuestGraceCompassBindingPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthTraderQuestOfferAcceptPreflightPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthTraderQuestOfferReturnToListPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthTraderProgressionDayCreditPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthTraderQuestLimitBuffSuppressPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthTraderCompletionCreditSafetyPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthQuestJournalAcceptedTraderJobLimitPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthFetchPositionRoutingPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthFetchServerStorageIdentityPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthFetchSetupIdentityPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthFetchCountIdentityPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthFetchContainerItemIdentityPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthInfestedQuestClassInitPatch));

        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthSilentQuestRemovePatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthSilentSharedQuestByOwnerPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthSilentSharedQuestForOwnerPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthSilentSharedPoiOutsidePatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthSilentSharedStayWithinOutsidePatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthSilentShareTooltipPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthSilentAcceptSharedQuestPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthSilentFailAllSharedQuestsPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthSilentSharedQuestNetworkPatch));

        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthTraderResponseClickPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthTraderRandomFetchServerPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthTraderRandomClientListArrivedPatch));
        #if DEBUG
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthTraderQuestOfferTracePatch));
        RebirthSpecialOfferDiagnostics.Install(harmony);
        #endif

        { if (RebirthLogSettings.RuntimeInstallLoggingEnabled) Log.Out("[REBIRTH] Trader jobs installed: expanded per-tier listings, repeat-POI policy, concurrent/daily acceptance limits, guaranteed progression credit, multi-fetch routing/identity, infested presentation, class-level Jobs click/direct-accept bindings, random refresh, completion history, 2.6-hard listing composition caps, silent cancel/shared notifications, death/disconnect-only five-minute recovery with death-time deadlines."); }
    }
}

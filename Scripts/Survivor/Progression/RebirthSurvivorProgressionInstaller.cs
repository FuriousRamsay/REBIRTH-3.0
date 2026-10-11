using HarmonyLib;

#nullable disable

public static class RebirthSurvivorProgressionInstaller
{
    private static readonly Harmony HarmonyInstance=new Harmony("rebirth.survivor.progression.3.1");
    private static bool installed;

    public static string Install()
    {
        if(installed)return "[REBIRTH Survivor Progression] already installed";
        string config=RebirthProgressionRuntimeConfig.Load();
        // The ready policy must reach native unlock calls before optional installers can fail.
        RebirthRecipeUnlockPolicyInstallation.EnsureInstalled(HarmonyInstance);
        RebirthStationObservationDispatcher.Install();
        RebirthSelfMedicalPracticeAwards.Install();
        string training=RebirthSkillTrainingProfileRegistry.Load();
        string phase8=RebirthPhase8WorldOutputTrainingService.Load();
        string phase9=RebirthPhase9TechnicalTrainingService.Load();
        string equipmentRepairTraining=RebirthEquipmentRepairTrainingService.Install();
        string theory=RebirthTheoryProgressionService.Load();
        string theoryReveal=RebirthTheoryRevealService.Load();
        string reachability=RebirthSkillReachabilityValidator.LoadAndValidate();

        // Static authority must be ready before any optional/risky Harmony installation.
        // This keeps the Progression Explorer navigable even if a later gameplay hook fails
        // against a changed game build.
        string advancedDisciplines=RebirthAdvancedDisciplineRegistry.Load();
        string blackMagicTargets=RebirthBlackMagicTargetClassifier.LoadDefinitions();
        string progressionGraph=RebirthProgressionGraphRegistry.BuildFromCurrentAuthority();

        RebirthHarmonyBootstrap.PatchClassOnce(HarmonyInstance, typeof(RebirthStationToolAvailabilityPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(HarmonyInstance, typeof(RebirthStationOpenQueueToolHoldPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(HarmonyInstance, typeof(RebirthStationNativePublicationHoldPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(HarmonyInstance, typeof(RebirthStationNativeRewardHoldPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(HarmonyInstance, typeof(RebirthStationInputSerializedPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(HarmonyInstance, typeof(RebirthStationQueueSerializedPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(HarmonyInstance, typeof(RebirthStationSnapshotWritePatch));
        RebirthHarmonyBootstrap.PatchClassOnce(HarmonyInstance, typeof(RebirthStationRegionPublishedPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(HarmonyInstance, typeof(RebirthStationSnapshotUpdatePatch));
        RebirthHarmonyBootstrap.PatchClassOnce(HarmonyInstance, typeof(RebirthStationChunkSerializedPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(HarmonyInstance, typeof(RebirthStationSnapshotResetPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(HarmonyInstance, typeof(RebirthNativeBackpackMagazinePatches));
        RebirthHarmonyBootstrap.PatchClassOnce(HarmonyInstance, typeof(RebirthSurvivorBackpackCapacityPatches));
        RebirthHarmonyBootstrap.PatchClassOnce(HarmonyInstance, typeof(RebirthNativeBackpackEquipmentPatches));
        RebirthSkillAwardService.ClearRuntimeAntiRepeat();
        RebirthAnimalHandlingService.ClearRuntime();
        string waveA=RebirthSkillWaveAService.Install();
        string resourceField=RebirthResourceFieldSkillService.Install();
        string weaponFamilies=RebirthWeaponFamilySkillService.Install();
        string serviceCrafting=RebirthServiceCraftSkillService.Install();
        RebirthPersonalCraftCompletionService.Install(HarmonyInstance);
        RebirthWorkstationCraftCompletion.Install(HarmonyInstance);
        string complexSkills=RebirthComplexSkillSystemService.Install();
        string infrastructure=RebirthInfrastructureWorkService.Install(HarmonyInstance);
        string placedProvenance=RebirthPlacedWorkmanshipService.Install(HarmonyInstance);
        string repairSignatures=RebirthRepairSignatureService.Install(HarmonyInstance);
        string workmanshipSignatures=RebirthWorkmanshipSignatureService.Install(HarmonyInstance);
        string resourceSignatures=RebirthResourceSignatureService.Install(HarmonyInstance);
        string oreSense=RebirthOreSenseService.Install();
        string chunkISignatures=RebirthCombatPatrolTrackingSignatureService.Install(HarmonyInstance);
        string chunkJSignatures=RebirthFoodFarmingButcherySignatureService.Install(HarmonyInstance);
        string chunkKDrinks=RebirthDrinkPreparationSignatureService.Install(HarmonyInstance);
        string chunkMTeaching=RebirthTeachingService.Install();
        string chunkNSalvage=RebirthScavengerSalvageProfileService.Install(HarmonyInstance);
        string blackMagic=RebirthBlackMagicService.Install(HarmonyInstance);
        string rage=RebirthRageService.Install();
        string wildAffinity=RebirthWildAffinityService.Install(HarmonyInstance);
        string beastmaster=RebirthBeastmasterService.Install(HarmonyInstance);
        string specialPanthers=RebirthSpecialPantherService.Install();
        RebirthHarmonyBootstrap.PatchClassOnce(HarmonyInstance,typeof(RebirthSurvivorCombatSkillPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(HarmonyInstance,typeof(RebirthProgressionPointMessaging));
        RebirthHarmonyBootstrap.PatchClassOnce(HarmonyInstance,typeof(RebirthChallengeCategoryPresentation));
        RebirthHarmonyBootstrap.PatchClassOnce(HarmonyInstance,typeof(RebirthLooseStoneReturnRoutingPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(HarmonyInstance,typeof(RebirthRangedProfileWeaponStatePatch));
        RebirthHarmonyBootstrap.PatchClassOnce(HarmonyInstance,typeof(RebirthRangedProfileEquipmentStatePatch));
        RebirthHarmonyBootstrap.PatchClassOnce(HarmonyInstance,typeof(RebirthRangedReloadStartTimingPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(HarmonyInstance,typeof(RebirthRangedReloadCompleteTimingPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(HarmonyInstance,typeof(RebirthSurvivorHarvestSkillPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(HarmonyInstance,typeof(RebirthPhase8HarvestCollectedOutputPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(HarmonyInstance,typeof(RebirthSurvivorResourceHarvestEffectContextPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(HarmonyInstance,typeof(RebirthSurvivorWorkstationKnowledgeAuthorityPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(HarmonyInstance,typeof(RebirthSurvivorOpenWorkstationQueueUpdatePatch));
        RebirthHarmonyBootstrap.PatchClassOnce(HarmonyInstance,typeof(RebirthSurvivorOpenWorkstationQueueOutputPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(HarmonyInstance,typeof(RebirthSurvivorWorkstationSkillCompletionPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(HarmonyInstance,typeof(RebirthSurvivorTreatOtherSkillPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(HarmonyInstance,typeof(RebirthSelfMedicalInstantEffectPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(HarmonyInstance,typeof(RebirthSelfMedicalConsumeEffectPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(HarmonyInstance,typeof(RebirthSelfMedicalNewBuffAddedPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(HarmonyInstance,typeof(RebirthSelfMedicalNewBuffStartPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(HarmonyInstance,typeof(RebirthSelfMedicalReliableReceive));
        RebirthHarmonyBootstrap.PatchClassOnce(HarmonyInstance,typeof(RebirthSelfMedicalStatsReceive));
        RebirthHarmonyBootstrap.PatchClassOnce(HarmonyInstance,typeof(RebirthSelfMedicalStatsScalarWrite));
        RebirthHarmonyBootstrap.PatchClassOnce(HarmonyInstance,typeof(RebirthSelfMedicalReplicaBuffStarter));
        RebirthHarmonyBootstrap.PatchClassOnce(HarmonyInstance,typeof(RebirthSelfMedicalNativeRestoreBootstrap));
        RebirthHarmonyBootstrap.PatchClassOnce(HarmonyInstance,typeof(RebirthSelfMedicalReplicaEntityTeardown));
        RebirthHarmonyBootstrap.PatchClassOnce(HarmonyInstance,typeof(RebirthSelfMedicalReplicaOwnerTeardown));
        RebirthHarmonyBootstrap.PatchClassOnce(HarmonyInstance,typeof(RebirthSelfMedicalReplicaWorldTeardown));
        RebirthHarmonyBootstrap.PatchClassOnce(HarmonyInstance,typeof(RebirthSelfMedicalReplicaDisconnect));
        RebirthHarmonyBootstrap.PatchClassOnce(HarmonyInstance,typeof(RebirthSelfMedicalReplicaServerDisconnect));
        RebirthHarmonyBootstrap.PatchClassOnce(HarmonyInstance,typeof(RebirthSelfMedicalReplicaClientsClear));
        RebirthHarmonyBootstrap.PatchClassOnce(HarmonyInstance,typeof(RebirthSelfMedicalReplicaMapTeardown));
        RebirthHarmonyBootstrap.PatchClassOnce(HarmonyInstance,typeof(RebirthSelfMedicalSimpleSpawnBootstrap));
        RebirthHarmonyBootstrap.PatchClassOnce(HarmonyInstance,typeof(RebirthSelfMedicalSteamSpawnBootstrap));
        RebirthHarmonyBootstrap.PatchClassOnce(HarmonyInstance,typeof(RebirthSelfMedicalPendingShouldProcess));
        RebirthHarmonyBootstrap.PatchClassOnce(HarmonyInstance,typeof(RebirthSelfMedicalPendingHandleSkipped));
        RebirthHarmonyBootstrap.PatchClassOnce(HarmonyInstance,typeof(RebirthMedicalInstantPracticePatch));
        RebirthHarmonyBootstrap.PatchClassOnce(HarmonyInstance,typeof(RebirthMedicalHeldPracticePatch));
        RebirthHarmonyBootstrap.PatchClassOnce(HarmonyInstance,typeof(RebirthMedicalRecoveryPracticePatch));
        RebirthHarmonyBootstrap.PatchClassOnce(HarmonyInstance,typeof(RebirthMedicalFatigueReliefPracticePatch));
        RebirthHarmonyBootstrap.PatchClassOnce(HarmonyInstance,typeof(RebirthMedicalDelayedClinicalPracticePatch));
        RebirthHarmonyBootstrap.PatchClassOnce(HarmonyInstance,typeof(RebirthSelfMedicalPracticeDeathPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(HarmonyInstance,typeof(RebirthExternalTreatmentEvidenceDeathPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(HarmonyInstance,typeof(RebirthSurvivorEquipmentRepairSkillPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(HarmonyInstance,typeof(RebirthSurvivorServiceCraftTimePatch));
        RebirthHarmonyBootstrap.PatchClassOnce(HarmonyInstance,typeof(RebirthSurvivorServiceRepairQueuePatch));
        RebirthHarmonyBootstrap.PatchClassOnce(HarmonyInstance,typeof(RebirthRepairSignatureQueuePatch));
        RebirthHarmonyBootstrap.PatchClassOnce(HarmonyInstance,typeof(RebirthRepairSignatureCompletionPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(HarmonyInstance,typeof(RebirthSkillWaveALockServerPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(HarmonyInstance,typeof(RebirthSkillWaveANetLockServerPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(HarmonyInstance,typeof(RebirthSkillWaveALockSuccessPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(HarmonyInstance,typeof(RebirthCompositeLockpickStartPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(HarmonyInstance,typeof(RebirthCompositeLockpickSuccessPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(HarmonyInstance,typeof(RebirthTheorySoloLockpickReleasePatch));
        RebirthHarmonyBootstrap.PatchClassOnce(HarmonyInstance,typeof(RebirthTheorySoloLockpickBlockTransitionPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(HarmonyInstance,typeof(RebirthTheorySoloLockpickCommitPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(HarmonyInstance,typeof(RebirthTheorySoloLockpickShowUiPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(HarmonyInstance,typeof(RebirthTheorySoloLockpickTimerPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(HarmonyInstance,typeof(RebirthTheorySoloPeerCombatPacketPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(HarmonyInstance,typeof(RebirthTheorySoloPeerCombatCorePatch));
        RebirthHarmonyBootstrap.PatchClassOnce(HarmonyInstance,typeof(RebirthTheorySoloCombatKillPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(HarmonyInstance,typeof(RebirthSkillWaveABuyPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(HarmonyInstance,typeof(RebirthSkillWaveASellPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(HarmonyInstance,typeof(RebirthTradingSpecialStockLabelPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(HarmonyInstance,typeof(RebirthSurvivorExplosiveBlockSkillPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(HarmonyInstance,typeof(RebirthSurvivorDroneStunSkillPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(HarmonyInstance,typeof(RebirthProvenanceItemStackPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(HarmonyInstance,typeof(RebirthTreeHarvestSettlementPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(HarmonyInstance,typeof(RebirthTreeDestroyedSettlementPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(HarmonyInstance,typeof(RebirthLoggerCuttingCostPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(HarmonyInstance,typeof(RebirthTreeDamageEvidencePatch));
        installed=true;
        return "[REBIRTH Survivor Progression] installed "+config+" "+training+" "+phase8+" "+phase9+" "+equipmentRepairTraining+" "+theory+" "+theoryReveal+" "+reachability+" "+waveA+" "+resourceField+" "+weaponFamilies+" "+serviceCrafting+" "+complexSkills+" "+infrastructure+" "+placedProvenance+" "+repairSignatures+" "+workmanshipSignatures+" "+resourceSignatures+" "+oreSense+" "+chunkISignatures+" "+chunkJSignatures+" "+chunkKDrinks+" "+chunkMTeaching+" "+chunkNSalvage+" "+advancedDisciplines+" "+blackMagicTargets+" "+blackMagic+" "+rage+" "+wildAffinity+" "+beastmaster+" "+specialPanthers+" "+progressionGraph;
    }
}




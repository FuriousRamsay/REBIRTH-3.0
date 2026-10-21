using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml;

// Preparation only. No caller may treat this primary-domain subset as a complete
// lifecycle bundle: ancillary controllers/financial/native handles still require
// their own typed adapters or positive empty preflight before atomic transfer.
internal sealed class RebirthNpcSaveQuarantinePreparation
{
    internal const long MaximumEvidenceBytes=64L*1024*1024;
    private readonly RebirthNpcPersistenceFile.QuarantineWriterFence fence;
    private readonly RebirthNpcSaveQuarantineCapture.Snapshot capture;
    private readonly RebirthNpcSaveQuarantineDomain[] domains;
    internal RebirthNpcSaveQuarantineDomain[] CopyQualifiedDomains()
    {if(!IsCurrent)throw new InvalidOperationException("Original typed preparation changed");return (RebirthNpcSaveQuarantineDomain[])domains.Clone();}
    internal static int ExpectedPrimaryDomainCount=>Required.Length;
    private static readonly string[] Required={"inventory-service","inventory-binding","equipment-service","equipment-binding","social-service","faction-service","social-binding","progression-service","progression-binding","advanced-service","advanced-binding","economy-service","economy-binding","combat-service","combat-binding","settlement-service","settlement-binding","execution-persistence","execution-leases","work-telemetry","work-checkpoint-binding","work-outcome-service","work-outcome-binding","stable-identity","aggregate","world-integration","native-equipment-projection","recovery-projection-mailbox","runtime-registry","recovery-driver","lifecycle-gameplay","identity-audit","timeline","progression-client-cache","progression-projection-revisions","command-gateway","activity-recovery","remote-command-routing","activity-runtime","decision-runtime","decision-signals","work-navigation-empty","dialogue-conversations","trade-quotes","dog-storage-empty","dog-native-observers","dog-runtime-cache","dog-ghost-retries","dog-capacity-projection","dog-auto-loot-callbacks","companion-behavior","companion-item-locks","companion-snapshot-callbacks","dog-navigation-markers","follow-controller-empty","travel-controller-empty","guard-controller-empty","patrol-controller-empty","stationary-controller-empty","mission-controller-empty","work-controller-empty","work-resolution-empty","work-targets-empty","work-executor-health","navigation-runtime-empty","spatial-index","path-budget","network-batches-empty","relationship-batches-empty","serialization-pool-idle","replication-performance","retention-delegates-empty","work-reservations-empty","work-assignment-controller-empty","workstation-jobs-empty","network-baseline","network-epoch","work-assignment-records","equipment-projection-receipts","guard-packages-empty","medical-packages-empty","guard-native-empty","medical-native-empty"};
    private RebirthNpcSaveQuarantinePreparation(RebirthNpcPersistenceFile.QuarantineWriterFence writer,RebirthNpcSaveQuarantineCapture.Snapshot observed,List<RebirthNpcSaveQuarantineDomain> prepared)
    {fence=writer;capture=observed;domains=prepared.ToArray();}
    internal bool IsCurrent
    {get{if(!fence.IsCurrent||!capture.IsUnchangedOriginal(out _))return false;foreach(var domain in domains)if(!domain.IsCurrent)return false;return true;}}
    internal static bool TryPrepare(RebirthNpcPersistenceFile.QuarantineWriterFence fence,RebirthNpcSaveQuarantineCapture.Snapshot capture,out RebirthNpcSaveQuarantinePreparation prepared,out string error)
    {
        prepared=null;error="Original primary domain preparation refused";
        try
        {
            if(fence==null||capture==null||!ReferenceEquals(fence.Scope,capture.Scope)||!fence.IsCurrent||!capture.IsUnchangedOriginal(out error))return false;
            error="Typed original domain preparation refused; original state retained";
            var scope=fence.Scope;var domains=new List<RebirthNpcSaveQuarantineDomain>();using(var budget=new RebirthNpcSaveQuarantineDomain.EvidenceBudget(MaximumEvidenceBytes))
            {
                if(!RebirthNpcInventoryPersistenceStore.TryPrepareQuarantine(scope,domains)||!RebirthNpcEquipmentPersistenceStore.TryPrepareQuarantine(scope,domains)||!RebirthNpcSocialPersistenceStore.TryPrepareQuarantine(scope,domains)||!RebirthNpcProgressionPersistenceStore.TryPrepareQuarantine(scope,domains)||!RebirthNpcAdvancedProgressionPersistenceStore.TryPrepareQuarantine(scope,domains)||!RebirthNpcEconomyPersistenceStore.TryPrepareQuarantine(scope,domains)||!RebirthNpcCombatPersistenceStore.TryPrepareQuarantine(scope,domains)||!RebirthNpcSettlementPersistenceStore.TryPrepareQuarantine(scope,domains)||!RebirthNpcExecutionPersistenceStore.TryPrepareQuarantine(scope,domains))return false;
                domains.Add(RebirthNpcExecutionLeaseRegistry.PrepareLeaseQuarantine(scope));domains.Add(RebirthNpcCommandGateway.PrepareCommandQuarantine(scope));domains.Add(RebirthNpcActivityRecoveryRegistry.PrepareActivityRecoveryQuarantine(scope));domains.Add(RebirthNpcActivityRegistry.PrepareActivityQuarantine(scope));domains.Add(RebirthNpcDecisionEngine.PrepareDecisionQuarantine(scope));domains.Add(RebirthNpcDecisionSignalStore.PrepareSignalsQuarantine(scope));domains.Add(RebirthNpcWorkNavigationService.PrepareEmptyNavigationQuarantine(scope));domains.Add(RebirthNpcDialogueService.PrepareDialogueQuarantine(scope));domains.Add(RebirthNpcTradeService.PrepareTradeQuarantine(scope));domains.Add(RebirthDogStorageService.PrepareEmptyStorageQuarantine(scope));domains.Add(RebirthDogChunkObserverService.PrepareDogObserverQuarantine(scope));domains.Add(RebirthDogRuntimeService.PrepareDogRuntimeQuarantine(scope));domains.Add(RebirthDogLifecycleService.PrepareGhostRetryQuarantine(scope));domains.Add(RebirthDogCapacitySyncService.PrepareCapacityQuarantine(scope.World,capture.NetworkEpoch,0));domains.Add(RebirthDogAutoLootBridge.PrepareAutoLootQuarantine(scope.World,capture.NetworkEpoch,0));domains.Add(RebirthCompanionBehaviorService.PrepareBehaviorQuarantine(scope));domains.Add(RebirthCompanionInventoryLockService.PrepareItemLocksQuarantine(scope));domains.Add(RebirthCompanionSnapshotService.PrepareSnapshotQuarantine(scope.World,capture.NetworkEpoch,0));domains.Add(RebirthDogNavigationMarkerService.PrepareMarkerQuarantine(scope.World,capture.NetworkEpoch,0));domains.Add(RebirthNpcFollowRuntime.PrepareEmptyControllerQuarantine(scope));domains.Add(RebirthNpcTravelRuntime.PrepareEmptyControllerQuarantine(scope));domains.Add(RebirthNpcGuardNavigationRuntime.PrepareEmptyControllerQuarantine(scope));domains.Add(RebirthNpcPatrolRuntime.PrepareEmptyControllerQuarantine(scope));domains.Add(RebirthNpcStationaryActivityRuntime.PrepareEmptyControllerQuarantine(scope));domains.Add(RebirthNpcMissionRuntime.PrepareEmptyControllerQuarantine(scope));domains.Add(RebirthNpcSpatialIndex.PrepareSpatialQuarantine(scope));domains.Add(RebirthNpcPathBudgetCoordinator.PreparePathQuarantine(scope));domains.Add(RebirthNpcNetworkBatcher.PrepareBatchQuarantine(scope));domains.Add(RebirthNpcRelationshipBatcher.PrepareRelationshipBatchQuarantine(scope));domains.Add(RebirthNpcSerializationBufferPool.PreparePoolQuarantine(scope));domains.Add(RebirthNpcReplicationPerformanceService.PreparePerformanceQuarantine(scope));domains.Add(RebirthNpcRetentionCoordinator.PrepareRetentionQuarantine(scope));domains.Add(RebirthNpcNetworkBaselineService.PrepareBaselineQuarantine(scope));domains.Add(RebirthNpcNetworkEpoch.PrepareEpochQuarantine(scope));domains.Add(RebirthNpcEquipmentProjectionReceipts.PrepareReceiptQuarantine(scope));domains.Add(RebirthNpcGuardDutyWorkPackageService.PrepareHeldQuarantine(scope));domains.Add(RebirthNpcMedicalTreatmentWorkPackageService.PrepareHeldQuarantine(scope));domains.Add(RebirthNpcGuardPostWorkAdapter.PrepareHeldQuarantine(scope));domains.Add(RebirthNpcMedicalTreatmentAdapter.PrepareHeldQuarantine(scope));domains.Add(RebirthNpcWorkAssignmentService.PrepareAssignmentRecordsQuarantine(scope));domains.Add(RebirthNpcNavigationRuntimeAdapter.PrepareEmptyNavigationRuntimeQuarantine(scope));domains.Add(RebirthNpcWorkReservationService.PrepareReservationQuarantine(scope));domains.Add(RebirthNpcWorkAssignmentCoordinator.PrepareAssignmentCoordinatorQuarantine(scope));domains.Add(RebirthNpcWorkstationExecutionService.PrepareWorkstationQuarantine(scope));domains.Add(RebirthNpcWorkRuntime.PrepareEmptyControllerQuarantine(scope));domains.Add(RebirthNpcWorkExecutorRegistry.PrepareEmptyResolutionQuarantine(scope));domains.Add(RebirthNpcWorkTargetRegistry.PrepareEmptyTargetQuarantine(scope));domains.Add(RebirthNpcWorkExecutorHealthRegistry.PrepareHealthQuarantine(scope));domains.Add(RebirthNpcRemoteCommandService.PrepareRemoteCommandQuarantine(scope));domains.Add(RebirthNpcPersistenceTimelineService.PrepareTimelineQuarantine(scope));domains.Add(RebirthNpcProgressionProjectionRevisionService.PrepareProgressionRevisionQuarantine(scope.World));domains.Add(RebirthNpcProgressionClientCache.PrepareProgressionCacheQuarantine(scope.World,capture.NetworkEpoch,0));
                if(!RebirthNpcWorkCheckpointPersistenceStore.TryPrepareQuarantine(scope,domains)||!RebirthNpcWorkOutcomePersistenceStore.TryPrepareQuarantine(scope,domains)||!RebirthNpcStableIdentityStore.TryPrepareQuarantine(scope,domains)||!RebirthNpcAggregatePersistenceStore.TryPrepareQuarantine(scope,domains)||!RebirthNpcWorldIntegrationService.TryPrepareQuarantine(scope,domains))return false;
                domains.Add(RebirthNpcNativeEquipmentBridge.PrepareNativeProjectionQuarantine(scope.World,scope.Directory));domains.Add(RebirthNpcRecoveryProjectionNetwork.PrepareProjectionMailboxQuarantine(scope.World,scope.Directory));domains.Add(RebirthNpcRuntimeRegistry.PrepareRuntimeQuarantine(scope.World,scope.Directory));domains.Add(RebirthNpcLiveRecoveryRuntime.PrepareRecoveryDriverQuarantine(scope.World,scope.Directory));if(!RebirthNpcLifecycleGameplayService.TryPrepareQuarantine(scope,domains)||!RebirthNpcIdentityAuditLedger.TryPrepareQuarantine(scope,domains))return false;
            }
            var names=new HashSet<string>(StringComparer.Ordinal);foreach(var domain in domains)if(domain==null||!names.Add(domain.Name))throw new InvalidOperationException("Duplicate or missing typed original domain");
            if(names.Count!=Required.Length)throw new InvalidOperationException("Primary typed domain membership incomplete");foreach(string name in Required)if(!names.Contains(name))throw new InvalidOperationException("Required typed original domain omitted");
            var next=new RebirthNpcSaveQuarantinePreparation(fence,capture,domains);if(!next.IsCurrent)return false;prepared=next;error=string.Empty;return true;
        }
        catch(Exception exception){error="Original primary domain preparation retained: "+exception.GetType().Name;return false;}
    }
    internal XmlDocument CopyEvidence()
    {
        if(!IsCurrent)throw new InvalidOperationException("Original domain preparation changed");var scope=capture.Scope;var doc=new XmlDocument{XmlResolver=null};var root=doc.CreateElement("npcQuarantinePrimaryPreparation");doc.AppendChild(root);root.SetAttribute("version","1");root.SetAttribute("directory",scope.Directory);root.SetAttribute("fingerprint",scope.Fingerprint);root.SetAttribute("generation",scope.Generation.ToString(CultureInfo.InvariantCulture));root.SetAttribute("completeLifecycleBundle","0");foreach(var domain in domains)root.AppendChild(domain.CopyEvidence(doc));return doc;
    }
    internal static bool IsDetachedServerMembership(RebirthNpcSaveQuarantineDomain[] domains)
    {
        if(domains==null||domains.Length!=Required.Length)return false;
        var names=new HashSet<string>(StringComparer.Ordinal);
        foreach(var domain in domains)if(domain==null||!domain.IsDetached||!names.Add(domain.Name))return false;
        foreach(string name in Required)if(!names.Contains(name))return false;
        return true;
    }
    internal static bool IsExactServerCoverage(HashSet<string> names)
    {
        if(names==null||names.Count!=Required.Length)return false;
        foreach(string name in Required)if(!names.Contains(name))return false;
        return true;
    }
}


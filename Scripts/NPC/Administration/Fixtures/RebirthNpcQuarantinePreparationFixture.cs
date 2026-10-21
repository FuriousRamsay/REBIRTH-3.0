#if DEBUG
using System;
using System.IO;

// PREPARED NOT RUN. Uses the actual current typed adapters/writer fence and
// retained native actor writer. Never calls Detach/Rollback or creates a receipt.
internal static class RebirthNpcQuarantinePreparationFixture
{
    internal static void VerifyPrimaryPreparation(string confirmation)
    {
        if(!RebirthNpcQualificationMutationGuard.Allows(confirmation))throw new InvalidOperationException(RebirthNpcQualificationMutationGuard.Refused);
        RebirthNpcEquipmentProjectionReceipts.VerifyActualReceiptQueries(confirmation);
        Require(!RebirthNpcPersistenceFile.HasQuarantineWriterFence);
        var world=GameManager.Instance.World;string directory=GameIO.GetSaveGameDir();
        string timelineBefore=RebirthNpcPersistenceTimelineService.GetReport();
        Require(RebirthNpcSaveQuarantineCapture.RememberCurrent(out _));var capture=RebirthNpcSaveQuarantineCapture.Find(world,directory);Require(capture!=null);
        Require(RebirthNpcPersistenceFile.TryFenceQuarantine(capture.Scope,out var fence,out _));
        using(fence)
        {
            Require(fence.IsCurrent&&RebirthNpcPersistenceCoordinator.IsRecoveryInstalling);
            Require(RebirthNpcSaveQuarantinePreparation.TryPrepare(fence,capture,out var prepared,out _));
            var evidence=prepared.CopyEvidence();Require(evidence.DocumentElement.GetAttribute("completeLifecycleBundle")=="0");Require(evidence.DocumentElement.SelectNodes("domain").Count==RebirthNpcSaveQuarantinePreparation.ExpectedPrimaryDomainCount);
            Require(evidence.DocumentElement.SelectNodes("domain[@id='timeline']/timeline").Count==1);
            Require(evidence.DocumentElement.SelectNodes("domain[@id='command-gateway']/commands").Count==1);
            Require(evidence.DocumentElement.SelectNodes("domain[@id='activity-recovery']/activityRecovery").Count==1);
            Require(evidence.DocumentElement.SelectNodes("domain[@id='remote-command-routing']/remoteCommandRouting").Count==1);
            Require(evidence.DocumentElement.SelectNodes("domain[@id='activity-runtime']/activities").Count==1);
            Require(evidence.DocumentElement.SelectNodes("domain[@id='decision-runtime']/decision").Count==1);
            Require(evidence.DocumentElement.SelectNodes("domain[@id='decision-signals']/signals").Count==1);
            Require(evidence.DocumentElement.SelectNodes("domain[@id='work-navigation-empty']/navigation[@emptyOnly='1']").Count==1);
            Require(evidence.DocumentElement.SelectNodes("domain[@id='dialogue-conversations']/conversations").Count==1);
            Require(evidence.DocumentElement.SelectNodes("domain[@id='trade-quotes']/tradeQuotes").Count==1);
            Require(evidence.DocumentElement.SelectNodes("domain[@id='dog-storage-empty']/dogStorage[@emptyOnly='1']").Count==1);
            Require(evidence.DocumentElement.SelectNodes("domain[@id='dog-native-observers']/dogObservers").Count==1);
            Require(evidence.DocumentElement.SelectNodes("domain[@id='dog-runtime-cache']/dogRuntime").Count==1);
            Require(evidence.DocumentElement.SelectNodes("domain[@id='dog-ghost-retries']/ghostRetries").Count==1);
            Require(evidence.DocumentElement.SelectNodes("domain[@id='dog-capacity-projection']/dogCapacity").Count==1);
            Require(evidence.DocumentElement.SelectNodes("domain[@id='dog-auto-loot-callbacks']/autoLootCallbacks").Count==1);
            Require(evidence.DocumentElement.SelectNodes("domain[@id='companion-behavior']/companionBehavior").Count==1);
            Require(evidence.DocumentElement.SelectNodes("domain[@id='companion-item-locks']/companionItemLocks").Count==1);
            Require(evidence.DocumentElement.SelectNodes("domain[@id='companion-snapshot-callbacks']/companionSnapshots").Count==1);
            Require(evidence.DocumentElement.SelectNodes("domain[@id='dog-navigation-markers']/dogMarkers").Count==1);
            Require(evidence.DocumentElement.SelectNodes("domain[@id='follow-controller-empty']/controllerEmpty[@emptyOnly='1']").Count==1);
            Require(evidence.DocumentElement.SelectNodes("domain[@id='travel-controller-empty']/controllerEmpty[@emptyOnly='1']").Count==1);
            Require(evidence.DocumentElement.SelectNodes("domain[@id='guard-controller-empty']/controllerEmpty[@emptyOnly='1']").Count==1);
            Require(evidence.DocumentElement.SelectNodes("domain[@id='patrol-controller-empty']/controllerEmpty[@emptyOnly='1']").Count==1);
            Require(evidence.DocumentElement.SelectNodes("domain[@id='stationary-controller-empty']/controllerEmpty[@emptyOnly='1']").Count==1);
            Require(evidence.DocumentElement.SelectNodes("domain[@id='mission-controller-empty']/controllerEmpty[@emptyOnly='1']").Count==1);
            Require(evidence.DocumentElement.SelectNodes("domain[@id='work-controller-empty']/controllerEmpty[@emptyOnly='1']").Count==1);
            Require(evidence.DocumentElement.SelectNodes("domain[@id='work-resolution-empty']/workResolution[@emptyOnly='1']").Count==1);
            Require(evidence.DocumentElement.SelectNodes("domain[@id='work-targets-empty']/workTargets[@emptyOnly='1']").Count==1);
            Require(evidence.DocumentElement.SelectNodes("domain[@id='work-executor-health']/workHealth[@generationMeaning='executor-registration']").Count==1);
            Require(evidence.DocumentElement.SelectNodes("domain[@id='navigation-runtime-empty']/controllerEmpty[@emptyOnly='1']").Count==1);
            foreach(string domain in new[]{"spatial-index","path-budget","network-batches-empty","relationship-batches-empty","serialization-pool-idle","replication-performance","retention-delegates-empty","work-reservations-empty","work-assignment-controller-empty","workstation-jobs-empty","network-baseline","network-epoch","work-assignment-records","equipment-projection-receipts","guard-packages-empty","medical-packages-empty","guard-native-empty","medical-native-empty"})Require(evidence.DocumentElement.SelectNodes("domain[@id='"+domain+"']").Count==1);
            Require(RebirthNpcPersistenceTimelineService.GetReport()==timelineBefore);
            Require(prepared.IsCurrent&&fence.IsCurrent&&capture.IsUnchangedOriginal(out _));
        }
        Require(!RebirthNpcPersistenceFile.HasQuarantineWriterFence&&ReferenceEquals(GameManager.Instance.World,world)&&RebirthNpcPersistenceTimelineService.GetReport()==timelineBefore);
    }
    internal static void VerifyForeignNativeWriter(string confirmation,EntityRebirthNPC originalActor)
    {
        if(!RebirthNpcQualificationMutationGuard.Allows(confirmation))throw new InvalidOperationException(RebirthNpcQualificationMutationGuard.Refused);
        Require(originalActor?.world!=null&&!ReferenceEquals(originalActor.world,GameManager.Instance.World)&&ReferenceEquals(originalActor.world.GetEntity(originalActor.entityId),originalActor)&&originalActor.NativeIdentityReadable);
        var originalState=originalActor.RebirthRuntimeState;var stable=originalActor.NativeStableIdentity;var observed=originalActor.ObservedNativeStableIdentity;
        using(var stream=new MemoryStream())using(var writer=MemoryPools.poolBinaryWriter.AllocSync(_bReset:false))
        {
            writer.SetBaseStream(stream);bool rejected=false;
            try{originalActor.Write(writer,StreamModeWrite.Persistency);}
            catch(InvalidOperationException ex){rejected=ex.Message=="Retained original native NPC body cannot be rewritten with offworld legacy identity framing";}
            Require(rejected&&stream.Position==0&&stream.Length==0&&ReferenceEquals(originalActor.RebirthRuntimeState,originalState)&&originalActor.NativeStableIdentity==stable&&originalActor.ObservedNativeStableIdentity==observed);
        }
    }
    private static void Require(bool condition){if(!condition)throw new InvalidOperationException("Actual NPC quarantine preparation fixture failed; no transfer authority issued");}
}

#endif

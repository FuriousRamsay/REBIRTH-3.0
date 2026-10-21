#if DEBUG
using System;
using System.Collections.Generic;

// Explicit native console entrypoint; no parent Bridge dispatcher registration.
// Never exposes the service-swapping offline generation fixtures in a live game.
public sealed class ConsoleCmdRebirthNpcRecoveryFixture:ConsoleCmdAbstract
{
    public override bool IsExecuteOnClient=>false;
    public override int DefaultPermissionLevel=>0;
    public override string[] getCommands()=>new[]{"rbnpcrecoveryfixture"};
    public override string getDescription()=>"Disposable-only actual NPC recovery codecs, native framing and durable-boundary fixture control (DEBUG).";
    public override string getHelp()=>"rbnpcrecoveryfixture <codecs|native entityId|quarantineprepare|missionendretained|workheld|assertcachefenced|begin exactFile|arm exactFile boundary|release|status> confirm=CodexTest\nActual boundaries: "+RebirthNpcRecoveryFaults.AvailablePoints+"\nServer-local authoritative disposable CodexTest only; game guard stays ON. quarantineprepare observes primary adapters only and never transfers/clears a domain. Inventory/equipment service-generation fixtures remain isolated-offline-only.";
    public override void Execute(List<string> args,CommandSenderInfo sender)
    {
        if(sender.RemoteClientInfo!=null||args==null||args.Count<2||args[args.Count-1]!="confirm=CodexTest"||!RebirthNpcQualificationMutationGuard.Allows("CodexTest"))
        {Log.Out("[REBIRTH NPC Recovery Fixture] result=FAIL refused; explicit server-local CodexTest confirmation required.");return;}
        try
        {
            string op=args[0];
            if(op=="assertcachefenced"&&args.Count==2)
            {RebirthNpcRecoveryCacheStepFixture.VerifyActualWriterFence();Log.Out("[REBIRTH NPC Recovery Fixture] result=PASS actual group2 ordinary writer refusal");return;}
            if(op=="status"&&args.Count==2)
            {Log.Out("[REBIRTH NPC Recovery Fixture] state="+RebirthNpcRecoveryFaults.Capture()+" "+RebirthNpcLiveRecoveryRuntime.GetReport());return;}
            if(op=="quarantineprepare"&&args.Count==2)
            {RebirthNpcQuarantinePreparationFixture.VerifyPrimaryPreparation("CodexTest");Log.Out("[REBIRTH NPC Recovery Fixture] result=PASS primary typed preparation only; lifecycle transfer NOT RUN; no domain detached");return;}
            if(op=="missionendretained"&&args.Count==2)
            {RebirthNpcMissionEndRetentionFixture.Verify("CodexTest");Log.Out("[REBIRTH NPC Recovery Fixture] result=PASS actual retained Mission End retirement refusal; no callback replay");return;}
            if(op=="workheld"&&args.Count==2)
            {RebirthNpcGuardDutyWorkPackageService.VerifyActualHeldPackage("CodexTest");RebirthNpcMedicalTreatmentWorkPackageService.VerifyActualHeldPackage("CodexTest");RebirthNpcGuardPostWorkAdapter.VerifyActualHeldPackage("CodexTest");RebirthNpcMedicalTreatmentAdapter.VerifyActualHeldPackage("CodexTest");RebirthNpcWorkAssignmentService.VerifyActualHeldAssignmentRecords("CodexTest");RebirthNpcWorkTargetRegistry.VerifyActualHeldTargets("CodexTest");RebirthNpcWorkReservationService.VerifyActualHeldReservations("CodexTest");RebirthNpcWorkstationExecutionService.VerifyActualHeldWorkstations("CodexTest");Log.Out("[REBIRTH NPC Recovery Fixture] result=PASS current held Work target state unchanged; active-original coverage requires actual claims");return;}
            if(op=="arm"&&args.Count==4)
            {RebirthNpcRecoveryFaults.Arm("CodexTest",args[1],args[2]);Log.Out("[REBIRTH NPC Recovery Fixture] armed actual boundary "+args[2]);return;}
            if(op=="release"&&args.Count==2)
            {RebirthNpcRecoveryFaults.Release("CodexTest");Log.Out("[REBIRTH NPC Recovery Fixture] released recovery advancement; durable original unchanged");return;}
            if(op=="begin"&&args.Count==3)
            {Log.Out("[REBIRTH NPC Recovery Fixture] "+RebirthNpcLiveRecoveryFixture.Begin("CodexTest",args[1]));return;}
            if(op=="native"&&args.Count==3&&int.TryParse(args[1],out int entity)&&GameManager.Instance.World.GetEntity(entity) is EntityRebirthNPC actor)
            {RebirthNpcNativeEcdBoundFixture.Verify("CodexTest",actor);Log.Out("[REBIRTH NPC Recovery Fixture] result=PASS actual native ECD framing only");return;}
            if(op=="codecs"&&args.Count==2)
            {
                RebirthNpcRecoveryCacheStepFixture.Verify();RebirthNpcEpochAllocatorFixture.Verify();RebirthNpcBaselineV2Fixture.Verify();RebirthNpcRecoveryProtocolFixture.Verify();RebirthNpcRecoveryGroupSchemaFixture.Verify();RebirthNpcNativeStableIdentityFixture.Verify();
                RebirthNpcRecoveryIntegrityFixture.Verify();RebirthNpcRecoveryCustodyImageFixture.Verify();RebirthNpcRecoveryPublicationFixture.Verify();RebirthNpcRecoveryStagingFixture.Verify();
                RebirthNpcInteractionGenerationFixture.Verify();
                RebirthNpcOriginalStoreSchemaFixture.Verify();
                RebirthNpcProgressionRecoveryFixture.Verify();RebirthDogCapacityRecoveryFixture.Verify();RebirthCompanionSnapshotRecoveryFixture.Verify();RebirthNpcObservedGenerationFixture.Verify("CodexTest");
                RebirthNpcTradeService.VerifyDetachedQuoteRetention();
                Log.Out("[REBIRTH NPC Recovery Fixture] result=PASS actual detached codecs/schema/publication invariants only; no live recovery/topology claim");return;
            }
            Log.Out("[REBIRTH NPC Recovery Fixture] result=FAIL unsupported invocation; "+getHelp());
        }
        catch(Exception ex){Log.Out("[REBIRTH NPC Recovery Fixture] result=FAIL "+ex.GetType().Name+": "+ex.Message);}
    }
}
#endif

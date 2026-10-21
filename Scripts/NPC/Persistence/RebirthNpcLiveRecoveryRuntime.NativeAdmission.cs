using System;
using System.Runtime.CompilerServices;

internal static partial class RebirthNpcLiveRecoveryRuntime
{
    private sealed class NativeAdmissionActivation
    {internal bool Invoked,Returned,Complete;}
    private static readonly ConditionalWeakTable<EntityRebirthNPC,NativeAdmissionActivation> nativeAdmissionActivations=
        new ConditionalWeakTable<EntityRebirthNPC,NativeAdmissionActivation>();
    internal static bool TryResumeAdmittedNativeActivation(RebirthNpcRuntimeNativeAdoption.NativeReentry source)
    {
        if(!ThreadManager.IsMainThread()||source?.Current()!=true||RebirthNpcRuntimeNativeAdoption.IsIncomingHeld(source.Actor)||
            !RebirthNpcSaveQuarantineBundle.CanReleaseServerRuntimeNativeReentry(source)||current!=null&&!current.Installed||
            current==null&&RebirthNpcPersistenceCoordinator.IsRecoveryInstalling)return false;
        var actor=source.Actor;
        if(!deferred.TryGetValue(actor,out var world)||!ReferenceEquals(world,source.World))return false;
        var progress=nativeAdmissionActivations.GetValue(actor,_=>new NativeAdmissionActivation());
        if(progress.Complete)return true;
        if(!actor.TryCompleteDeferredDomainRegistration()||!actor.NativeIdentityReadable)return false;
        if(!deferredProjected.Contains(actor))
        {
            bool recoveredExecution=current?.Target=="RebirthNpcExecutionState.xml"&&RebirthNpcExecutionLeaseRegistry.TryGetActive(actor.entityId,out _);
            var runtime=actor.RebirthRuntimeState;var order=runtime.Order;var guard=runtime.GuardPosition;bool hasGuard=runtime.HasGuardPosition;
            if(!RebirthNpcAggregatePersistenceStore.TryGet(runtime.StableId,out var record)||
                !RebirthNpcRecoveryRuntimeProjection.TryPrepareCurrentNative(actor,record,out var projection,out _)||!projection.Install())return false;
            if(recoveredExecution){runtime.Order=order;runtime.GuardPosition=guard;runtime.HasGuardPosition=hasGuard;runtime.MarkPersisted();}
            deferredProjected.Add(actor);
        }
        if(!progress.Invoked)
        {
            progress.Invoked=true;
            // Only custom deferred activation runs. The game's native Add,
            // Spawn, read and earlier controller callback are never replayed.
            actor.ResumeRecoveryNativeActivation();progress.Returned=true;
        }
        if(!progress.Returned||!source.Current()||RebirthNpcExecutionPersistenceStore.HasUnrestoredOrdinary(actor.RebirthRuntimeState.StableId,actor.entityId))return false;
        deferred.Remove(actor);deferredProjected.Remove(actor);progress.Complete=true;
        RebirthNpcRuntimeNativeAdoption.CompleteNativeActivation(actor);return true;
    }
}

using System;

// Runtime continuation fence only; never execution/payment admission or native
// reconstruction. Actual lease/activity CAS remains in each existing controller.
internal sealed class RebirthNpcControllerOriginalScope
{
    internal readonly World World;
    internal readonly EntityRebirthNPC Actor;
    internal readonly RebirthNpcRuntimeState Runtime;
    internal readonly RebirthNpcStableId StableId;
    internal readonly ulong Generation;
    internal readonly Entity Subject;
    internal readonly int SubjectId;
    private RebirthNpcControllerOriginalScope(World world,EntityRebirthNPC actor,ulong generation,int subject)
    {World=world;Actor=actor;Runtime=actor.RebirthRuntimeState;StableId=Runtime.StableId;Generation=generation;SubjectId=subject;Subject=subject>=0?world.GetEntity(subject):null;}
    internal static bool CanTick(World originalWorld)
    {return ThreadManager.IsMainThread()&&originalWorld?.IsRemote()==false&&ReferenceEquals(originalWorld,GameManager.Instance?.World)&&SingletonMonoBehaviour<ConnectionManager>.Instance?.IsServer==true&&!RebirthNpcPersistenceCoordinator.IsRecoveryInstalling;}
    internal static RebirthNpcControllerOriginalScope CaptureRetainedNavigation(RebirthNpcSaveQuarantineScope scope,EntityRebirthNPC actor,RebirthNpcRuntimeState runtime)
    {
        if(!ThreadManager.IsMainThread()||scope==null||actor==null||runtime==null||runtime.StableId.IsEmpty||
            !ReferenceEquals(actor.world,scope.World)||!ReferenceEquals(actor.RebirthRuntimeState,runtime)||!actor.NativeIdentityReadable||
            !actor.ObservedNativeStableIdentity.IsEmpty&&actor.ObservedNativeStableIdentity!=runtime.StableId)return null;
        // Read-only native owner capture; no context/lease/activity is created.
        return new RebirthNpcControllerOriginalScope(scope.World,actor,scope.Generation,-1);
    }
    internal static bool TryCapture(RebirthNpcControllerContext context,ref World boundWorld,ref ulong boundGeneration,bool empty,out RebirthNpcControllerOriginalScope original)
    {
        original=null;var world=GameManager.Instance?.World;var actor=context.Npc;
        if(actor?.RebirthRuntimeState!=null&&RebirthNpcRetainedControllerCustody.BlocksOwner(actor.RebirthRuntimeState.StableId))return false;
        if(actor?.RebirthRuntimeState!=null&&RebirthNpcRetainedWorkPackages.BlocksOwner(actor.RebirthRuntimeState.StableId))return false;
        if(!CanTick(world)||actor==null||context.Lease==null||context.Lease.LeaseId==0||context.ActivityId==0||context.Lease.TargetEntityId!=actor.entityId||!ReferenceEquals(actor.world,world)||!ReferenceEquals(world.GetEntity(actor.entityId),actor)||!actor.NativeIdentityReadable||actor.RebirthRuntimeState==null||actor.RebirthRuntimeState.StableId.IsEmpty||(boundWorld!=null&&!ReferenceEquals(boundWorld,world))||(boundWorld==null&&!empty))return false;
        var observed=actor.ObservedNativeStableIdentity;if(!observed.IsEmpty&&observed!=actor.RebirthRuntimeState.StableId)return false;
        ulong generation=RebirthNpcRecoveryGeneration.RequireCurrent();
        if(boundWorld!=null&&boundGeneration!=generation&&!empty)return false;
        boundWorld=world;boundGeneration=generation;original=new RebirthNpcControllerOriginalScope(world,actor,generation,context.Lease.SubjectEntityId);return true;
    }
    internal bool Current(bool requireSubject=false)
    {
        if(!CanTick(World)||!ReferenceEquals(Actor.world,World)||!ReferenceEquals(World.GetEntity(Actor.entityId),Actor)||!Actor.NativeIdentityReadable||!ReferenceEquals(Actor.RebirthRuntimeState,Runtime)||Runtime.StableId!=StableId||!RebirthNpcRecoveryGeneration.TryReadObservedGeneration(World,out ulong generation)||generation!=Generation)return false;
        var observed=Actor.ObservedNativeStableIdentity;if(!observed.IsEmpty&&observed!=StableId)return false;
        return !requireSubject||(Subject!=null&&ReferenceEquals(Subject.world,World)&&ReferenceEquals(World.GetEntity(SubjectId),Subject));
    }
}

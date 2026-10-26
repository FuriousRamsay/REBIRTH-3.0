using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

// A selected, non-mesh damaged block, one native material, whole repair only.
// Source review must qualify this purpose independently of generic Work.
internal static class RebirthNpcSingleRepairWork
{
    internal const string DefinitionId="rebirth.structure.repair-one";
    internal const bool ExecutionQualified=false;
    internal static bool IsDefinition(string definition)=>definition==DefinitionId;
    internal static void Register()=>RebirthNpcWorkDefinitionRegistry.Register(new RebirthNpcWorkDefinition(DefinitionId,
        RebirthNpcWorkKind.Crafting,RebirthNpcWorkCapability.Crafting,325,1,TimeSpan.FromSeconds(90),false),true);
    private static bool Shape(string definition,string target,bool hasPosition,Vector3 position)
    {
        if(!ExecutionQualified||!IsDefinition(definition)||!hasPosition||target==null||target.Length>256||
            float.IsNaN(position.x)||float.IsNaN(position.y)||float.IsNaN(position.z)||
            float.IsInfinity(position.x)||float.IsInfinity(position.y)||float.IsInfinity(position.z))return false;
        var parts=target.Split(':');return parts.Length==3&&parts[0]=="desk"&&parts[1].Length>0&&parts[2]=="Structure"&&
            position==new Vector3i(Mathf.FloorToInt(position.x),Mathf.FloorToInt(position.y),Mathf.FloorToInt(position.z)).ToVector3();
    }
    internal static bool Allows(RebirthNpcWorkAssignment a)=>a!=null&&Shape(a.DefinitionId,a.TargetKey,a.HasTargetPosition,a.TargetPosition);
    internal static bool Allows(RebirthNpcWorkAssignmentRequest a)=>a!=null&&Shape(a.DefinitionId,a.TargetKey,a.HasTargetPosition,a.TargetPosition);
    internal static bool Allows(RebirthNpcEmbodiedWorkIntent i)=>i!=null&&i.Path=="concrete"&&i.PrimaryId!=0&&i.SecondaryId!=0&&
        i.ExecutorId=="rebirth.work.crafting-workstation"&&i.Actor is EntityRebirthHumanoidNPC&&i.MatchesOriginal()&&
        RebirthNpcWorkAssignmentService.TryGet(i.PrimaryId,out var a)&&Allows(a)&&a.NpcId==i.StableId&&
        RebirthNpcWorkReservationService.TryGetForAssignment(i.PrimaryId,out var r)&&r.ReservationId==i.SecondaryId;
    internal static Vector3i Position(RebirthNpcWorkAssignment a)=>new Vector3i(Mathf.FloorToInt(a.TargetPosition.x),Mathf.FloorToInt(a.TargetPosition.y),Mathf.FloorToInt(a.TargetPosition.z));
    internal static string TargetId(RebirthNpcWorkAssignment a)=>"block:"+Position(a).x+":"+Position(a).y+":"+Position(a).z;
    internal static bool Authorized(RebirthNpcWorkAssignment a,out long revision)
    {
        revision=0;var world=GameManager.Instance?.World;
        if(!Allows(a)||world?.IsRemote()!=false||
            !RebirthNpcDeskVolumeWorkTargetAdapter.ContainsStructurePoint(a,a.TargetPosition+new Vector3(.5f,.5f,.5f),out revision))return false;
        var owner=RebirthNpcInteractionMutationHandlers.FindActorPlayer(a.ActorId);
        var persistent=owner==null?null:GameManager.Instance.GetPersistentPlayerList()?.GetPlayerDataFromEntityID(owner.entityId);
        if(owner==null||owner.IsDead()||persistent==null||!ReferenceEquals(owner.world,world)||!ReferenceEquals(world.GetEntity(owner.entityId),owner)||
            world.IsWithinTraderArea(Position(a)))return false;
        var claim=world.GetLandClaimOwner(Position(a),persistent);return claim==EnumLandClaimOwner.None||claim==EnumLandClaimOwner.Self;
    }
    internal static bool PotentiallyReady(RebirthNpcWorkAssignment a,EntityRebirthHumanoidNPC npc)
    {
        if(!Authorized(a,out _)||npc?.bag?.ItemGrid==null||npc.IsDead()||npc.Hand==null||npc.Hand.IsBusy||npc.GetAttackTarget()!=null)return false;
        if(RebirthNpcStructureRepairOriginal.HasNativePending(a.NpcId))return true;
        var block=npc.world.GetBlock(Position(a));
        return RebirthNpcSingleRepairPolicy.TryObserve(npc,Position(a),block,false,out _);
    }
    internal static RebirthNpcWorkAssignmentResult Assign(string session,string savedStructure,Vector3i block,
        out RebirthNpcWorkAssignment assignment,out string detail)
    {
        assignment=null;detail="Native single-block repair is held for source review; general Structure repair remains unavailable.";
        if(!ExecutionQualified)return RebirthNpcWorkAssignmentResult.PermissionDenied;
        if(RebirthNpcInteractionSessionService.Authorize(session,RebirthNpcInteractionAction.AssignWork,out var c)!=RebirthNpcInteractionResult.Allowed||c==null)
            return RebirthNpcWorkAssignmentResult.PermissionDenied;
        var probe=new RebirthNpcWorkAssignment{DefinitionId=DefinitionId,TargetKey=savedStructure,HasTargetPosition=true,
            TargetPosition=block.ToVector3(),ActorId=c.ActorId,NpcId=c.NpcId};
        if(!Authorized(probe,out _))return RebirthNpcWorkAssignmentResult.PermissionDenied;
        return RebirthNpcWorkAssignmentService.Create(new RebirthNpcWorkAssignmentRequest{SessionKey=session,DefinitionId=DefinitionId,
            TargetKey=savedStructure,HasTargetPosition=true,TargetPosition=block.ToVector3(),ExpectedRuntimeRevision=c.RuntimeRevision},out assignment,out detail);
    }
    internal static bool AllowsOutcome(RebirthNpcWorkOutcomeRecord outcome)=>RebirthNpcStructureRepairOriginal.AllowsNativeOutcome(outcome);
}

public sealed class RebirthNpcSingleRepairTargetAdapter:IRebirthNpcWorkTargetAdapter
{
    public string AdapterId=>"rebirth.target.explicit-structure-repair";
    public int Priority=>410;
    public bool CanResolve(RebirthNpcWorkAssignment a,RebirthNpcWorkDefinition d)=>RebirthNpcSingleRepairWork.Allows(a);
    public bool TryResolve(RebirthNpcWorkAssignment a,RebirthNpcWorkDefinition d,out RebirthNpcWorkTargetSnapshot target,out string detail)
    {
        target=null;detail="Authorized selected damaged block in saved Structure is unavailable.";
        if(!RebirthNpcSingleRepairWork.Authorized(a,out long revision))return false;
        target=new RebirthNpcWorkTargetSnapshot{AdapterId=AdapterId,StableTargetId=RebirthNpcSingleRepairWork.TargetId(a),
            TargetRevision=revision.ToString(CultureInfo.InvariantCulture),Position=a.TargetPosition,IsLoaded=true,IsAvailable=true,
            InteractionRange=RebirthNpcWorkRuntime.WorkRadius,Detail="Selected block inside saved Structure volume."};detail=target.Detail;return true;
    }
    public bool Validate(RebirthNpcConcreteWorkContext c,out RebirthNpcWorkFailureCategory failure,out string detail)
    {
        failure=RebirthNpcWorkFailureCategory.AuthorizationDenied;detail="Structure, original block position or saved revision changed.";
        if(c?.Assignment==null||c.Target==null||!RebirthNpcSingleRepairWork.Authorized(c.Assignment,out long revision)||
            c.Target.StableTargetId!=RebirthNpcSingleRepairWork.TargetId(c.Assignment)||c.Target.Position!=c.Assignment.TargetPosition||
            c.Target.TargetRevision!=revision.ToString(CultureInfo.InvariantCulture))return false;
        failure=RebirthNpcWorkFailureCategory.None;return true;
    }
    public bool IsNpcInRange(RebirthNpcConcreteWorkContext c,out string detail)
    {
        detail="Worker must reach the selected damaged block.";
        return RebirthNpcWorkNavigationService.TryResolveNpc(c.Assignment.NpcId,out var npc)&&
            (npc.position-c.Assignment.TargetPosition).sqrMagnitude<=RebirthNpcWorkRuntime.WorkRadiusSquared;
    }
}

public sealed class RebirthNpcSingleRepairWorkAdapter:IRebirthNpcCraftingWorkAdapter
{
    public string AdapterId=>"rebirth.structure.physical-repair-one";
    public int Priority=>820;
    public bool CanHandle(RebirthNpcConcreteWorkContext c)=>RebirthNpcSingleRepairWork.Allows(c?.Assignment);
    private static RebirthNpcWorkMutationResult Hold(string detail)=>new RebirthNpcWorkMutationResult{
        Disposition=RebirthNpcWorkExecutionDisposition.Suspend,FailureCategory=RebirthNpcWorkFailureCategory.PersistenceRecovery,Detail=detail};
    public RebirthNpcWorkMutationResult BeginOperation(RebirthNpcConcreteWorkContext c)=>CanHandle(c)?
        RebirthNpcWorkMutationResult.Continue(0f,"Selected damaged block accepted; awaiting physical arrival."):Hold("Repair purpose is release-held.");
    public RebirthNpcWorkMutationResult TickOperation(RebirthNpcConcreteWorkContext c)
    {
        if(!CanHandle(c)||RebirthNpcEmbodiedWorkOwner.CurrentDispatchIntent is not RebirthNpcEmbodiedWorkIntent intent||
            !RebirthNpcEmbodiedWorkOwner.IsDispatching(intent)||!RebirthNpcSingleRepairWork.Allows(intent)||
            intent.Actor is not EntityRebirthHumanoidNPC npc||!RebirthNpcSingleRepairWork.Authorized(c.Assignment,out _)||
            (npc.position-c.Assignment.TargetPosition).sqrMagnitude>RebirthNpcWorkRuntime.WorkRadiusSquared)return Hold("Original EAI, Structure authorization or selected block arrival is unavailable.");
        if(!RebirthNpcStructureRepairOriginal.HasNativePending(c.Assignment.NpcId))
        {
            var target=RebirthNpcSingleRepairWork.Position(c.Assignment);
            // Persistent, change-only report of what the worker lacks (material, stack, tool).
            if(RebirthP9RepairShortfall.EvaluateSingle(npc,c.Assignment.AssignmentId,target,out var shortfall))
            {
                RebirthP9RepairShortfall.Record(npc,c.Assignment.AssignmentId,shortfall);
                return new RebirthNpcWorkMutationResult{Disposition=RebirthNpcWorkExecutionDisposition.Retry,
                    FailureCategory=RebirthNpcWorkFailureCategory.MissingInput,Detail="Repair material or tool is short; see the saved shortfall report."};
            }
            // Concealed block: stand where the exact native first-hit ray reaches it.
            string approachDetail=string.Empty;
            var approach=npc.world.GetBlock(target).damage>0?RebirthP9RepairWorkPosition.Approach(npc,c.Assignment.AssignmentId,target,out approachDetail):RebirthP9RepairApproach.Visible;
            if(approach==RebirthP9RepairApproach.Moving)return RebirthNpcWorkMutationResult.Continue(0f,approachDetail);
            if(approach==RebirthP9RepairApproach.Unreachable)return Hold(approachDetail);
        }
        return RebirthNpcStructureRepairOriginal.AdvanceNativeOperation(c,out var result)?result:
            Hold("Original material debit or native repair publication is retained; uncertain effects are not replayed.");
    }
    public void CancelOperation(RebirthNpcConcreteWorkContext c,string reason)
    {
        if(c?.Assignment!=null)RebirthP9RepairShortfall.Clear(c.Assignment.NpcId,c.Assignment.AssignmentId);
    }
}

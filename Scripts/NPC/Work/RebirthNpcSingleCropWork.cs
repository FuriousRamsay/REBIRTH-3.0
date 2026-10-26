using System;
using System.Globalization;
using UnityEngine;

// Explicit single-crop action inside a saved Farm volume. Whole-volume
// scheduling, seed placement and watering do not acquire this purpose.
internal static class RebirthNpcSingleCropWork
{
    internal const string DefinitionId="rebirth.farming.harvest-one";
    internal static bool IsDefinition(string definition)=>definition==DefinitionId;
    internal static void Register()=>RebirthNpcWorkDefinitionRegistry.Register(new RebirthNpcWorkDefinition(DefinitionId,
        RebirthNpcWorkKind.Farming,RebirthNpcWorkCapability.Farming|RebirthNpcWorkCapability.Harvesting,320,1,TimeSpan.FromSeconds(60),false),true);
    internal static bool Shape(string definition,string key,bool hasPosition,Vector3 position)
    {
        if(!IsDefinition(definition)||!hasPosition||key==null||key.Length>256||float.IsNaN(position.x)||float.IsNaN(position.y)||float.IsNaN(position.z))return false;
        var parts=key.Split(':');return parts.Length==3&&parts[0]=="desk"&&parts[1].Length>0&&parts[2]=="Farm"&&
            position==new Vector3i(Mathf.FloorToInt(position.x),Mathf.FloorToInt(position.y),Mathf.FloorToInt(position.z)).ToVector3();
    }
    internal static bool Allows(RebirthNpcWorkAssignment a)=>a!=null&&Shape(a.DefinitionId,a.TargetKey,a.HasTargetPosition,a.TargetPosition);
    internal static bool Allows(RebirthNpcWorkAssignmentRequest a)=>a!=null&&Shape(a.DefinitionId,a.TargetKey,a.HasTargetPosition,a.TargetPosition);
    internal static bool Allows(RebirthNpcEmbodiedWorkIntent intent)=>intent!=null&&intent.Path=="concrete"&&intent.PrimaryId!=0&&intent.SecondaryId!=0&&
        intent.ExecutorId=="rebirth.work.crafting-workstation"&&intent.Actor is EntityRebirthHumanoidNPC&&intent.MatchesOriginal()&&
        RebirthNpcWorkAssignmentService.TryGet(intent.PrimaryId,out var assignment)&&Allows(assignment)&&assignment.NpcId==intent.StableId&&
        RebirthNpcWorkReservationService.TryGetForAssignment(intent.PrimaryId,out var reservation)&&reservation.ReservationId==intent.SecondaryId;
    internal static Vector3i Position(RebirthNpcWorkAssignment a)=>new Vector3i(Mathf.FloorToInt(a.TargetPosition.x),Mathf.FloorToInt(a.TargetPosition.y),Mathf.FloorToInt(a.TargetPosition.z));
    internal static string TargetId(RebirthNpcWorkAssignment a)=>"block:"+Position(a).x.ToString(CultureInfo.InvariantCulture)+":"+
        Position(a).y.ToString(CultureInfo.InvariantCulture)+":"+Position(a).z.ToString(CultureInfo.InvariantCulture);
    internal static bool Authorized(RebirthNpcWorkAssignment a,out long revision)
    {
        revision=0;
        var world=GameManager.Instance?.World;
        if(!Allows(a)||world?.IsRemote()!=false||
            !RebirthNpcDeskVolumeWorkTargetAdapter.ContainsFarmPoint(a,a.TargetPosition+new Vector3(.5f,.5f,.5f),out revision))return false;
        var owner=RebirthNpcInteractionMutationHandlers.FindActorPlayer(a.ActorId);
        var persistent=owner==null?null:GameManager.Instance?.GetPersistentPlayerList()?.GetPlayerDataFromEntityID(owner.entityId);
        var position=Position(a);
        if(owner==null||owner.IsDead()||persistent==null||!ReferenceEquals(owner.world,world)||
            !ReferenceEquals(world.GetEntity(owner.entityId),owner)||world.IsWithinTraderArea(position))return false;
        var claim=world.GetLandClaimOwner(position,persistent);
        return (claim==EnumLandClaimOwner.None||claim==EnumLandClaimOwner.Self)&&world.CanPickupBlockAt(position,persistent);
    }
    internal static bool Mature(RebirthNpcWorkAssignment a)
    {
        if(!AdvancedFarmingRuntimePolicy.Enabled||!Authorized(a,out _))return false;
        var block=GameManager.Instance.World.GetBlock(Position(a));
        return block.Block is BlockPlantGrowingRebirth crop&&crop.IsFullyGrownCrop(block)&&RebirthUtilities.IsPlayerGrownHarvestCrop(block)&&
            RebirthNpcAdvancedFarmingHarvestAdapter.TryResolveHarvestItem(block,out _)&&!RebirthUtilities.HarvestDestination(block,true).isair;
    }
    internal static bool PotentiallyReady(RebirthNpcWorkAssignment a,EntityRebirthHumanoidNPC npc)
    {
        if(!Authorized(a,out _)||npc.bag?.ItemGrid==null||npc.GetAttackTarget()!=null||npc.Hand==null||npc.Hand.IsBusy)return false;
        var operation=RebirthNpcConcreteWorkExecutorBase.DeriveOperationId(a.AssignmentId);
        if(RebirthNpcFarmOriginal.TryReadSaved(a.NpcId,operation,out var retained))return
            (string)retained.Attribute("definition")==DefinitionId&&(string)retained.Attribute("target")==a.TargetKey&&
            (long?)retained.Attribute("startedTicks")==a.CreatedUtcTicks&&
            (int?)retained.Attribute("x")==Position(a).x&&(int?)retained.Attribute("y")==Position(a).y&&(int?)retained.Attribute("z")==Position(a).z;
        if(!Mature(a)||!RebirthNpcWorkOutcomeService.CanAdmitExactOutcomeIdentity(a))return false;
        for(int i=0;i<npc.bag.SlotCount;i++)if(npc.bag.ItemGrid[i]==null||npc.bag.ItemGrid[i].IsEmpty())return true;
        return false;
    }
    internal static RebirthNpcWorkAssignmentResult Assign(string session,string savedFarmTarget,Vector3i crop,
        out RebirthNpcWorkAssignment assignment,out string detail)
    {
        assignment=null;detail="An authorized mature player-grown crop inside the saved Farm target is required.";
        if(RebirthNpcInteractionSessionService.Authorize(session,RebirthNpcInteractionAction.AssignWork,out var context)!=RebirthNpcInteractionResult.Allowed||context==null)
            return RebirthNpcWorkAssignmentResult.PermissionDenied;
        var request=new RebirthNpcWorkAssignmentRequest{SessionKey=session,DefinitionId=DefinitionId,TargetKey=savedFarmTarget,
            HasTargetPosition=true,TargetPosition=crop.ToVector3(),ExpectedRuntimeRevision=context.RuntimeRevision};
        var probe=new RebirthNpcWorkAssignment{DefinitionId=DefinitionId,TargetKey=savedFarmTarget,HasTargetPosition=true,
            TargetPosition=crop.ToVector3(),ActorId=context.ActorId,NpcId=context.NpcId};
        if(!Mature(probe))return RebirthNpcWorkAssignmentResult.PermissionDenied;
        return RebirthNpcWorkAssignmentService.Create(request,out assignment,out detail);
    }
    internal static bool AllowsOutcome(RebirthNpcWorkOutcomeRecord outcome)=>outcome!=null&&IsDefinition(outcome.DefinitionId)&&
        RebirthNpcFarmOriginal.AllowsOutcome(outcome)&&RebirthNpcWorkAssignmentService.TryGet(outcome.AssignmentId,out var a)&&Allows(a)&&
        a.NpcId==outcome.NpcId&&RebirthNpcFarmOriginal.TryReadSaved(a.NpcId,outcome.OutcomeId,out var row)&&
        outcome.OutcomeId==RebirthNpcConcreteWorkExecutorBase.DeriveOperationId(a.AssignmentId)&&
        (string)row.Attribute("definition")==DefinitionId&&(string)row.Attribute("target")==a.TargetKey&&
        outcome.StartedUtcTicks==a.CreatedUtcTicks&&(long?)row.Attribute("startedTicks")==a.CreatedUtcTicks&&
        (int?)row.Attribute("x")==Position(a).x&&(int?)row.Attribute("y")==Position(a).y&&(int?)row.Attribute("z")==Position(a).z;
}

public sealed class RebirthNpcSingleCropTargetAdapter:IRebirthNpcWorkTargetAdapter
{
    public string AdapterId=>"rebirth.target.explicit-farm-crop";
    public int Priority=>400;
    public bool CanResolve(RebirthNpcWorkAssignment a,RebirthNpcWorkDefinition d)=>RebirthNpcSingleCropWork.Allows(a);
    public bool TryResolve(RebirthNpcWorkAssignment a,RebirthNpcWorkDefinition d,out RebirthNpcWorkTargetSnapshot target,out string detail)
    {
        target=null;detail="Saved Farm and authorized crop position are unavailable.";
        if(!RebirthNpcSingleCropWork.Authorized(a,out long revision))return false;
        target=new RebirthNpcWorkTargetSnapshot{AdapterId=AdapterId,StableTargetId=RebirthNpcSingleCropWork.TargetId(a),
            TargetRevision=revision.ToString(CultureInfo.InvariantCulture),Position=a.TargetPosition,IsLoaded=true,IsAvailable=true,
            InteractionRange=RebirthNpcWorkRuntime.WorkRadius,Detail="Explicit crop inside the saved Farm volume."};detail=target.Detail;return true;
    }
    public bool Validate(RebirthNpcConcreteWorkContext c,out RebirthNpcWorkFailureCategory failure,out string detail)
    {
        failure=RebirthNpcWorkFailureCategory.AuthorizationDenied;detail="Farm target permission or saved revision changed.";
        if(c?.Assignment==null||c.Target==null||!RebirthNpcSingleCropWork.Authorized(c.Assignment,out long revision)||
            c.Target.StableTargetId!=RebirthNpcSingleCropWork.TargetId(c.Assignment)||!c.Target.Position.Equals(c.Assignment.TargetPosition)||
            c.Target.TargetRevision!=revision.ToString(CultureInfo.InvariantCulture))return false;
        failure=RebirthNpcWorkFailureCategory.None;detail="Saved Farm and exact crop position remain authorized.";return true;
    }
    public bool IsNpcInRange(RebirthNpcConcreteWorkContext c,out string detail)
    {
        detail="Worker must reach the exact crop.";if(!RebirthNpcWorkNavigationService.TryResolveNpc(c.Assignment.NpcId,out var npc))return false;
        bool current=(npc.position-c.Assignment.TargetPosition).sqrMagnitude<=RebirthNpcWorkRuntime.WorkRadiusSquared;
        if(current)detail="Worker reached the exact crop.";return current;
    }
}

public sealed class RebirthNpcSingleCropWorkAdapter:IRebirthNpcCraftingWorkAdapter
{
    public string AdapterId=>"rebirth.farming.physical-one-crop";
    public int Priority=>810;
    public bool CanHandle(RebirthNpcConcreteWorkContext c)=>RebirthNpcSingleCropWork.Allows(c?.Assignment);
    private static RebirthNpcWorkMutationResult Hold(string detail)=>new RebirthNpcWorkMutationResult{
        Disposition=RebirthNpcWorkExecutionDisposition.Suspend,FailureCategory=RebirthNpcWorkFailureCategory.MissingInput,Detail=detail};
    public RebirthNpcWorkMutationResult BeginOperation(RebirthNpcConcreteWorkContext c)=>CanHandle(c)?
        RebirthNpcWorkMutationResult.Continue(0f,"Explicit mature crop accepted; awaiting physical arrival."):Hold("Saved one-crop assignment is unavailable.");
    public RebirthNpcWorkMutationResult TickOperation(RebirthNpcConcreteWorkContext c)
    {
        if(!CanHandle(c)||RebirthNpcEmbodiedWorkOwner.CurrentDispatchIntent is not RebirthNpcEmbodiedWorkIntent intent||
            !RebirthNpcEmbodiedWorkOwner.IsDispatching(intent)||!RebirthNpcSingleCropWork.Allows(intent)||
            intent.Actor is not EntityRebirthHumanoidNPC npc||npc.GetAttackTarget()!=null||npc.Hand==null||npc.Hand.IsBusy||
            !RebirthNpcSingleCropWork.Authorized(c.Assignment,out _)||
            (npc.position-c.Assignment.TargetPosition).sqrMagnitude>RebirthNpcWorkRuntime.WorkRadiusSquared)return Hold("Original harvest EAI, Farm permission or exact crop arrival is unavailable.");
        if(!RebirthNpcFarmOriginal.TryReadSaved(c.Assignment.NpcId,c.OperationId,out var row))
            return RebirthNpcFarmOriginal.TryPrepare(c,out _,out string detail)?RebirthNpcWorkMutationResult.Continue(0f,detail):Hold(detail);
        var position=RebirthNpcSingleCropWork.Position(c.Assignment);
        if((string)row.Attribute("definition")!=RebirthNpcSingleCropWork.DefinitionId||
            (string)row.Attribute("assignment")!=c.Assignment.AssignmentId.ToString(CultureInfo.InvariantCulture)||
            (long?)row.Attribute("startedTicks")!=c.Assignment.CreatedUtcTicks||
            (string)row.Attribute("target")!=c.Assignment.TargetKey||
            (int?)row.Attribute("x")!=position.x||(int?)row.Attribute("y")!=position.y||(int?)row.Attribute("z")!=position.z)
            return Hold("Saved Farm original belongs to another purpose or crop.");
        string phase=(string)row.Attribute("phase");
        if(phase=="prepared")
            return RebirthNpcFarmOriginal.TryAdmitNativeCredit(npc,c.OperationId)&&RebirthNpcFarmOriginal.TryCommitBlock(c)?
                RebirthNpcWorkMutationResult.Continue(0f,"Actual harvest/replant mutation observed; native publication is settling."):
                Hold("Exact crop source or saved native bag-credit admission is settling.");
        if(phase=="block-attempted")
            return RebirthNpcFarmOriginal.TryPublishBlock(c.Assignment.NpcId,c.OperationId)?
                RebirthNpcWorkMutationResult.Continue(0f,"Saved native crop publication observed."):Hold("Harvest effect is awaiting positive native publication; unknown effects are not replayed.");
        if(phase=="block-published"||phase=="bag-attempted")
            return RebirthNpcFarmOriginal.TryCreditBag(npc,c.OperationId)?RebirthNpcWorkMutationResult.Continue(0f,"Exact native harvest credit saved."):
                Hold("Native bag credit is settling; a lookalike item cannot satisfy the saved credit identity.");
        if((phase=="bag-published"||phase=="completed")&&RebirthNpcFarmOriginal.TryComplete(c.Assignment.NpcId,c.OperationId))
            return new RebirthNpcWorkMutationResult{Disposition=RebirthNpcWorkExecutionDisposition.Completed,MutationCommitted=true,
                Detail="One player-grown crop harvested and replanted with saved native block and worker-item receipts."};
        return Hold("Saved crop original remains retained for exact settlement.");
    }
    public void CancelOperation(RebirthNpcConcreteWorkContext c,string reason){}
}

using System;
using System.Globalization;
using UnityEngine;

// The only Work definition admitted while the generic Work release is held.
// A typed assignment is never authority to mutate: the adapter still proves
// the source incarnation, owner, physical arrival and durable native custody.
internal static class RebirthNpcLiteratureWorkGate
{
    internal static bool InfrastructureEnabled=>true;
    private sealed class Capability : IRebirthNpcWorkCapabilityProvider
    {
        public int Priority=>1000;
        public RebirthNpcWorkCapability Resolve(RebirthNpcStableId id)
        {
            var world=GameManager.Instance?.World;
            return world?.IsRemote()==false&&RebirthNpcRuntimeRegistry.TryGetEntityId(id,out int entity)&&
                world.GetEntity(entity) is EntityRebirthHumanoidNPC npc&&!npc.IsDead()&&
                npc.RebirthRuntimeState?.OwnershipKind==RebirthNpcOwnershipKind.Player&&
                npc.RebirthRuntimeState.Presence==RebirthNpcPresenceState.Active
                ?RebirthNpcWorkCapability.Crafting|RebirthNpcWorkCapability.Farming|RebirthNpcWorkCapability.Harvesting:RebirthNpcWorkCapability.None;
        }
        public bool CanPerform(RebirthNpcWorkCapabilityContext context,out string denialReason)
        {
            denialReason="Literature requires the current owned survivor.";
            if(context==null)return false;
            var required=RebirthNpcSingleCropWork.IsDefinition(context.DefinitionId)?
                RebirthNpcWorkCapability.Farming|RebirthNpcWorkCapability.Harvesting:RebirthNpcWorkCapability.Crafting;
            if(!RebirthNpcQualifiedWorkGate.AllowsDefinition(context.DefinitionId)||context.Required!=required||
                (Resolve(context.NpcId)&required)!=required)return false;
            var world=GameManager.Instance?.World;
            if(world==null||!RebirthNpcRuntimeRegistry.TryGetEntityId(context.NpcId,out int entity)||
                world.GetEntity(entity) is not EntityRebirthHumanoidNPC npc||
                !string.Equals(npc.RebirthRuntimeState.OwnerId,context.ActorId,StringComparison.OrdinalIgnoreCase))return false;
            denialReason=string.Empty;return true;
        }
    }
    private static readonly Capability LiteratureCapability=new Capability();
    internal static void RegisterCapability()=>RebirthNpcWorkCapabilityService.Register(LiteratureCapability);
    internal static bool AllowsDefinition(string definition)=>
        string.Equals(definition,RebirthNpcLiteratureWorkAdapter.DefinitionId,StringComparison.Ordinal);
    internal static bool IsLiterature(string definition,string key,bool hasPosition,Vector3 position)
    {
        if(!AllowsDefinition(definition)||
            !hasPosition||string.IsNullOrEmpty(key)||!key.StartsWith("literature:S@",StringComparison.Ordinal))return false;
        var parts=key.Split(':');
        if(parts.Length!=3||parts[0]!="literature"||
            !int.TryParse(parts[2],NumberStyles.None,CultureInfo.InvariantCulture,out int slot)||slot<0||slot>255)return false;
        var source=RebirthNpcPhysicalResourceEndpointRegistry.Ensure(parts[1],out _);
        return source?.Kind==RemoteResourceSourceKind.StaticContainer&&
            Mathf.FloorToInt(position.x)==source.Position.x&&
            Mathf.FloorToInt(position.y)==source.Position.y&&
            Mathf.FloorToInt(position.z)==source.Position.z;
    }
    internal static bool Allows(RebirthNpcWorkAssignmentRequest request)=>request!=null&&
        IsLiterature(request.DefinitionId,request.TargetKey,request.HasTargetPosition,request.TargetPosition);
    internal static bool Allows(RebirthNpcWorkAssignment assignment)=>assignment!=null&&
        IsLiterature(assignment.DefinitionId,assignment.TargetKey,assignment.HasTargetPosition,assignment.TargetPosition);
    internal static string TargetId(RebirthNpcWorkAssignment assignment)=>assignment==null?null:
        "block:"+Mathf.FloorToInt(assignment.TargetPosition.x).ToString(CultureInfo.InvariantCulture)+":"+
        Mathf.FloorToInt(assignment.TargetPosition.y).ToString(CultureInfo.InvariantCulture)+":"+
        Mathf.FloorToInt(assignment.TargetPosition.z).ToString(CultureInfo.InvariantCulture);
    internal static bool AllowsOutcome(RebirthNpcWorkOutcomeRecord outcome)
    {
        if(outcome==null||!AllowsDefinition(outcome.DefinitionId)||outcome.State!=RebirthNpcWorkOutcomeState.Completed||
            !RebirthNpcWorkAssignmentService.TryGet(outcome.AssignmentId,out var assignment)||!Allows(assignment)||
            assignment.NpcId!=outcome.NpcId||assignment.CreatedUtcTicks!=outcome.StartedUtcTicks||
            outcome.TargetId!=TargetId(assignment)||outcome.Detail==null||
            !outcome.Detail.StartsWith("Saved physical literature read marker: ",StringComparison.Ordinal))return false;
        string witness=outcome.DurableWitness;
        const string prefix="saved-recipe-read:",separator="|operation:";
        if(witness==null||!witness.StartsWith(prefix,StringComparison.Ordinal))return false;
        int split=witness.IndexOf(separator,StringComparison.Ordinal);
        if(split<=prefix.Length||!Guid.TryParseExact(witness.Substring(split+separator.Length),"N",out var operation)||
            operation!=RebirthNpcConcreteWorkExecutorBase.DeriveOperationId(assignment.AssignmentId)||
            !RebirthNpcWorkNativeTransferOriginal.TryReadSaved(outcome.NpcId,operation,out var original)||
            original.Phase<4||original.Npc!=outcome.NpcId||original.Owner!=assignment.ActorId||
            !RebirthHumanProgression.TryGet(outcome.NpcId,out var record))return false;
        var target=assignment.TargetKey.Split(':');
        if(target.Length!=3||!int.TryParse(target[2],NumberStyles.None,CultureInfo.InvariantCulture,out int slot)||
            original.SourceSlot!=slot||original.Source!=RemoteResourceIdentity.Static(
                new Vector3i(Mathf.FloorToInt(assignment.TargetPosition.x),Mathf.FloorToInt(assignment.TargetPosition.y),
                    Mathf.FloorToInt(assignment.TargetPosition.z))))return false;
        string marker=witness.Substring(prefix.Length,split-prefix.Length);
        return RebirthNpcLiteratureWorkAdapter.Book(original,out string knowledge)&&
            marker==RebirthLiteratureService.RecipeReadMarker(knowledge)&&
            outcome.Detail=="Saved physical literature read marker: "+knowledge&&
            record?.Progression?.KnowledgeIds?.Contains(marker)==true;
    }
    internal static bool AllowsReturnedBookContinuity(RebirthNpcEmbodiedWorkIntent intent,EntityRebirthHumanoidNPC npc)
    {
        if(intent==null||npc==null||intent.Path!="concrete"||intent.PrimaryId==0||
            npc.inventory==null||npc.bag==null||intent.OriginalSelectedSlot<0||
            intent.OriginalSelectedSlot>=npc.inventory.SlotCount||npc.inventory.SelectedSlot!=intent.OriginalSelectedSlot||
            RebirthHumanEquipment.Frame(npc.inventory.GetItem(intent.OriginalSelectedSlot))!=intent.OriginalSelectedFrame||
            !RebirthNpcWorkAssignmentService.TryGet(intent.PrimaryId,out var assignment)||!Allows(assignment)||
            assignment.NpcId!=intent.StableId)return false;
        var operation=RebirthNpcConcreteWorkExecutorBase.DeriveOperationId(intent.PrimaryId);
        if(!RebirthNpcWorkNativeTransferOriginal.TryReadSaved(intent.StableId,operation,out var original)||
            original.Phase<4||original.Npc!=intent.StableId||original.Owner!=assignment.ActorId||
            original.BagSlot<0||original.BagSlot>=npc.bag.SlotCount||
            RebirthHumanEquipment.Frame(npc.bag.ItemGrid[original.BagSlot])!=original.Frame||
            !RebirthNpcLiteratureWorkAdapter.Book(original,out string knowledge)||
            !RebirthHumanProgression.TryGet(intent.StableId,out var record))return false;
        return record?.Progression?.KnowledgeIds?.Contains(RebirthLiteratureService.RecipeReadMarker(knowledge))==true;
    }
    internal static bool HasSavedCompletion(RebirthNpcWorkAssignment assignment)=>Allows(assignment)&&
        RebirthNpcWorkOutcomeService.HasCommittedLiterature(assignment.AssignmentId,assignment.NpcId);
    internal static bool Allows(RebirthNpcEmbodiedWorkIntent intent)
    {
        if(intent==null||intent.Path!="concrete"||intent.PrimaryId==0||intent.SecondaryId==0||
            intent.Actor is not EntityRebirthHumanoidNPC||!intent.MatchesOriginal()||
            !RebirthNpcWorkAssignmentService.TryGet(intent.PrimaryId,out var assignment)||
            !Allows(assignment)||assignment.NpcId!=intent.StableId||
            !string.Equals(intent.ExecutorId,"rebirth.work.crafting-workstation",StringComparison.Ordinal))return false;
        return RebirthNpcWorkReservationService.TryGetForAssignment(intent.PrimaryId,out var reservation)&&
            reservation.ReservationId==intent.SecondaryId;
    }
}

using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

// A narrow post-arrival Work consumer of the saved B05 custody protocol. The
// concrete executor has already validated the original assignment and target
// range before TickOperation. BeginOperation does not touch physical items.
public sealed class RebirthNpcLiteratureWorkAdapter : IRebirthNpcCraftingWorkAdapter
{
    internal const string DefinitionId="rebirth.crafting.read-literature";
    private const string Prefix="literature:";
    public string AdapterId=>"rebirth.crafting.physical-literature";
    public int Priority=>800;
    internal static void RegisterDefinition()=>RebirthNpcWorkDefinitionRegistry.Register(
        new RebirthNpcWorkDefinition(DefinitionId,RebirthNpcWorkKind.Crafting,
            RebirthNpcWorkCapability.Crafting,280,1,TimeSpan.FromSeconds(60),false),true);
    public bool CanHandle(RebirthNpcConcreteWorkContext context)=>context?.Definition?.DefinitionId==DefinitionId&&
        context.Assignment?.TargetKey?.StartsWith(Prefix,StringComparison.Ordinal)==true;
    private static bool Parse(RebirthNpcConcreteWorkContext context,out RebirthNpcPhysicalResourceEndpoint source,out int slot)
    {
        source=null;slot=-1;
        if(context?.Assignment==null||context.Definition?.DefinitionId!=DefinitionId||
            !context.Assignment.HasTargetPosition)return false;
        string[] parts=(context.Assignment.TargetKey??string.Empty).Split(':');
        if(parts.Length!=3||parts[0]!="literature"||
            !int.TryParse(parts[2],NumberStyles.None,CultureInfo.InvariantCulture,out slot)||slot<0||slot>255)return false;
        source=RebirthNpcPhysicalResourceEndpointRegistry.Ensure(parts[1],out _);
        if(source?.Kind!=RemoteResourceSourceKind.StaticContainer)return false;
        var p=context.Assignment.TargetPosition;
        return Mathf.FloorToInt(p.x)==source.Position.x&&Mathf.FloorToInt(p.y)==source.Position.y&&
            Mathf.FloorToInt(p.z)==source.Position.z;
    }
    private static bool MarkerSaved(RebirthNpcStableId id,string knowledge)
        =>!string.IsNullOrEmpty(knowledge)&&RebirthHumanProgression.TryGet(id,out var record)&&
            record.Progression.KnowledgeIds.Contains(RebirthLiteratureService.RecipeReadMarker(knowledge));
    private static Guid CompletionId(RebirthNpcConcreteWorkContext context,string knowledge)
    {
        // The native transfer operation is assignment-scoped; the saved read
        // marker makes this one specific literature completion permanent.
        string source="literature-read|"+context.Assignment.NpcId+"|"+
            context.OperationId.ToString("N")+"|"+knowledge;
        using var sha=SHA256.Create();byte[] hash=sha.ComputeHash(Encoding.UTF8.GetBytes(source));
        byte[] bytes=new byte[16];Array.Copy(hash,bytes,16);return new Guid(bytes);
    }
    internal static bool Book(RebirthNpcWorkNativeTransferOriginal.Record original,out string knowledge)
    {
        knowledge=null;
        try
        {
            var stack=RebirthHumanEquipment.Decode(original.Frame);
            return stack?.itemValue?.ItemClass!=null&&
                RebirthProgressionRuntimeConfig.TryGetLiterature(stack.itemValue.ItemClass.GetItemName(),out var definition)&&
                definition!=null&&string.Equals(definition.Kind,"discovery",StringComparison.OrdinalIgnoreCase)&&
                !string.IsNullOrEmpty(knowledge=definition.KnowledgeId);
        }
        catch{return false;}
    }
    private static RebirthNpcWorkMutationResult Retry(string detail)=>new RebirthNpcWorkMutationResult{
        Disposition=RebirthNpcWorkExecutionDisposition.Suspend,
        FailureCategory=RebirthNpcWorkFailureCategory.TargetUnavailable,Detail=detail};
    private static RebirthNpcWorkMutationResult Fail(string detail)=>new RebirthNpcWorkMutationResult{
        Disposition=RebirthNpcWorkExecutionDisposition.Failed,
        FailureCategory=RebirthNpcWorkFailureCategory.InvalidTarget,Detail=detail};
    public RebirthNpcWorkMutationResult BeginOperation(RebirthNpcConcreteWorkContext context)
    {
        if(!RebirthNpcLiteratureWorkGate.Allows(context?.Assignment)||!Parse(context,out var source,out int slot)||
            RebirthNpcEmbodiedWorkOwner.CurrentDispatchIntent is not RebirthNpcEmbodiedWorkIntent intent||
            !RebirthNpcEmbodiedWorkOwner.IsDispatching(intent)||intent.StableId!=context.Assignment.NpcId)
            return Fail("Saved literature Work target or original EAI custody is unavailable.");
        return RebirthNpcWorkMutationResult.Continue(0f,"Literature target accepted; awaiting physical arrival.");
    }
    public RebirthNpcWorkMutationResult TickOperation(RebirthNpcConcreteWorkContext context)
    {
        if(!RebirthNpcLiteratureWorkGate.Allows(context?.Assignment)||
            !Parse(context,out var source,out int slot)||
            RebirthNpcEmbodiedWorkOwner.CurrentDispatchIntent is not RebirthNpcEmbodiedWorkIntent intent||
            !RebirthNpcEmbodiedWorkOwner.IsDispatching(intent)||intent.StableId!=context.Assignment.NpcId)
            return Fail("Original literature work custody is unavailable.");
        var world=GameManager.Instance?.World;
        if(world?.IsRemote()!=false||!RebirthNpcRuntimeRegistry.TryGetEntityId(context.Assignment.NpcId,out int entity)||
            world.GetEntity(entity) is not EntityRebirthHumanoidNPC npc||npc.IsDead()||
            !ReferenceEquals(intent.Actor,npc)||
            (npc.position-source.Position.ToVector3()).sqrMagnitude>
                RebirthCompanionService.StorageDistance*RebirthCompanionService.StorageDistance)
            return Retry("Reader has not reached the saved literature source.");
        var owner=RebirthNpcInteractionMutationHandlers.FindActorPlayer(context.Assignment.ActorId);
        if(owner==null||owner.IsDead())return Retry("Authenticated companion owner is unavailable.");
        var id=context.Assignment.NpcId;
        if(!RebirthNpcWorkNativeTransferOriginal.TryReadSaved(id,context.OperationId,out var original))
        {
            int bag=-1;
            for(int i=0;i<npc.bag.SlotCount;i++)
                if(npc.bag.ItemGrid[i]==null||npc.bag.ItemGrid[i].IsEmpty()){bag=i;break;}
            if(bag<0||!source.TryObserveExactStackForNpc(owner,npc,slot,bag,out var observed)||
                observed.Stack?.itemValue?.ItemClass==null||
                !RebirthProgressionRuntimeConfig.TryGetLiterature(observed.Stack.itemValue.ItemClass.GetItemName(),out var book)||
                book==null||!string.Equals(book.Kind,"discovery",StringComparison.OrdinalIgnoreCase)||
                string.IsNullOrEmpty(book.KnowledgeId))return Retry("Exact physical discovery book or destination is unavailable.");
            if(MarkerSaved(id,book.KnowledgeId))return Fail("This companion has already read the selected literature.");
            if(!RebirthNpcWorkNativeTransfer.TryStart(owner,npc,source,slot,bag,context.OperationId,out _))
                return Retry("Saved literature transfer could not start.");
            return RebirthNpcWorkMutationResult.Continue(0f,"Exact literature transfer prepared.");
        }
        if(original.Npc!=id||original.SourceSlot!=slot||original.Operation!=context.OperationId||
            original.Source!=new StaticContainerResourceSource(world,source.Position).StableId||
            !Book(original,out string knowledge))return Fail("Saved literature original does not match this assignment.");
        if(!RebirthNpcWorkNativeTransfer.TryAdvance(owner,npc,original.Operation))
            return Retry("Physical literature transfer is awaiting durable custody.");
        if(!RebirthNpcWorkNativeTransferOriginal.TryReadSaved(id,original.Operation,out original)||original.Phase<4)
            return RebirthNpcWorkMutationResult.Continue(0f,"Physical literature transfer is settling.");
        if(MarkerSaved(id,knowledge))
        {
            // The reader has already checkpointed the actual knowledge marker.
            // Retry this same receipt until the outcome journal checkpoints too.
            var outcome=new RebirthNpcWorkOutcomeRecord{
                OutcomeId=CompletionId(context,knowledge),AssignmentId=context.Assignment.AssignmentId,
                NpcId=id,DefinitionId=DefinitionId,TargetId=RebirthNpcLiteratureWorkGate.TargetId(context.Assignment),
                DurableWitness="saved-recipe-read:"+RebirthLiteratureService.RecipeReadMarker(knowledge)+
                    "|operation:"+context.OperationId.ToString("N"),
                StartedUtcTicks=context.Assignment.CreatedUtcTicks,CompletedUtcTicks=DateTime.UtcNow.Ticks,
                State=RebirthNpcWorkOutcomeState.Completed,FailureCategory=RebirthNpcWorkFailureCategory.None,
                Detail="Saved physical literature read marker: "+knowledge};
            if(!RebirthNpcWorkOutcomeService.TryRecordCommitted(outcome))
                return Retry("Saved literature completion is awaiting its durable outcome receipt.");
            return new RebirthNpcWorkMutationResult{
                Disposition=RebirthNpcWorkExecutionDisposition.Completed,MutationCommitted=true,
                Detail="Physical literature study saved."};
        }
        if(RebirthHumanRecipeReading.TryStatus(id,out var reading))
        {
            if(reading.KnowledgeId!=knowledge)return Retry("Another physical literature session is active.");
            return RebirthNpcWorkMutationResult.Continue(0f,"Companion is reading the held book.");
        }
        if(RebirthHumanRecipeReading.IsPreparing(id))
            return RebirthNpcWorkMutationResult.Continue(0f,"Companion is raising the held book.");
        if(!RebirthHumanRecipeReading.TryBeginFromWorkBag(owner,npc,original.BagSlot,intent,out _))
            return Retry("Native held-book study is awaiting an idle hand.");
        return RebirthNpcWorkMutationResult.Continue(0f,"Companion began physical literature study.");
    }
    public void CancelOperation(RebirthNpcConcreteWorkContext context,string reason)
    {
        // Generic Work cancellation has no proven settlement. Its executor
        // retains the original and deliberately does not call this method.
    }
}

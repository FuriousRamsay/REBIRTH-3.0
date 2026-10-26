using System;
using System.Globalization;
using UnityEngine;

// One explicit neighboring named-container move. Category routing, merging and
// arbitrary-distance hauling are separate purposes, never inferred here.
internal static class RebirthNpcStorageOrganizationWork
{
    internal const string DefinitionId="rebirth.work.storage-organization-one";
    internal enum Mode:byte{Near=0,Far=1,Deposit=2,Sort=3,Merge=4,SortMerge=5,FarMerge=6}
    internal static bool IsMergeMode(Mode mode)=>mode==Mode.Merge||mode==Mode.SortMerge||mode==Mode.FarMerge;
    // FarMerge = leg-aware distant hauling whose destination cell already holds the identical payload (unit-by-unit merge of a
    // multi-unit stack). Navigation (P6 MultiLeg) must treat it exactly like Far; see the doc request list.
    internal static bool IsFarMode(Mode mode)=>mode==Mode.Far||mode==Mode.FarMerge;
    internal static bool IsSameContainerMode(Mode mode)=>mode==Mode.Sort||mode==Mode.SortMerge;
    // Shared-custody readiness gates (see P10_B09 doc, "Merge / partial / sort custody design"). All stay false until the
    // shared originals conserve exact ItemValue and counts across merge, partial and restart; admission refuses these modes.
    // Sort needs RebirthNpcWorkNativeCreditOriginal.Valid to accept Source==Destination (same container, distinct cells).
    internal static bool SortCustodyReady=>RebirthNpcB09GateOverride.SortOpen;
    internal static bool MergeCustodyReady=>RebirthNpcB09GateOverride.MergeOpen;
    internal static bool PartialCustodyReady=>RebirthNpcB09GateOverride.PartialOpen;
    // Far = leg-aware distant hauling between loaded owned containers: the worker first reaches the source,
    // and only after the saved source debit (transfer original phase 4) travels to the destination.
    // Deposit = credit of a finished-work bag stack (saved P7 pickup receipt) into one empty owned cell.
    internal const float FarMaxSeparation=96f;
    internal const string DepositDetail="One finished work output deposited into the assigned named container; no item produced or consumed here.";
    internal sealed class Target
    {
        internal RebirthNpcPhysicalResourceEndpoint Source,Destination;
        internal int SourceSlot,DestinationSlot;internal string SourceIncarnation,DestinationIncarnation;
        internal Mode Mode;internal Guid PickupOperation;
    }
    internal static bool IsDefinition(string id)=>id==DefinitionId;
    internal static void Register()
    {
        RebirthNpcWorkDefinitionRegistry.Register(new RebirthNpcWorkDefinition(
            DefinitionId,RebirthNpcWorkKind.Crafting,RebirthNpcWorkCapability.Crafting,265,1,TimeSpan.FromSeconds(60),false),true);
        RebirthP1B13Policy.RelocationWitness=IsRelocationWitness;
    }
    internal static bool Parse(string definition,string key,bool hasPosition,Vector3 position,out Target target)
    {
        target=null;
        try
        {
            if(!IsDefinition(definition)||!hasPosition||key==null||key.Length>512)return false;
            var fields=key.Split(':');
            if(fields.Length==5&&fields[0]=="storage-deposit")
            {
                if(!ParseSlot(fields[2],out int depositSlot)||!Guid.TryParseExact(fields[3],"N",out var depositInc)||depositInc==Guid.Empty||
                    !Guid.TryParseExact(fields[4],"N",out var pickup)||pickup==Guid.Empty)return false;
                var depositEndpoint=RebirthNpcPhysicalResourceEndpointRegistry.Ensure(fields[1],out _);
                if(depositEndpoint?.Kind!=RemoteResourceSourceKind.StaticContainer||position!=depositEndpoint.Position.ToVector3())return false;
                target=new Target{Destination=depositEndpoint,DestinationSlot=depositSlot,DestinationIncarnation=fields[3],Mode=Mode.Deposit,PickupOperation=pickup};
                return true;
            }
            bool sortMerge=fields.Length==7&&fields[0]=="storage-sortmerge";
            if(fields.Length==7&&(fields[0]=="storage-sort"||sortMerge))
            {
                // One container, two distinct cells: a sequence of single-unit moves via the worker's saved bag cell
                // (sortmerge = the destination cell already holds the identical payload; needs the merge credit as well).
                if(!SortCustodyReady||(sortMerge&&!MergeCustodyReady)||!ParseSlot(fields[2],out int fromCell)||!ParseSlot(fields[4],out int toCell)||fromCell==toCell||fields[1]!=fields[3]||fields[5]!=fields[6]||
                    !Guid.TryParseExact(fields[5],"N",out var sortInc)||sortInc==Guid.Empty)return false;
                var sortEndpoint=RebirthNpcPhysicalResourceEndpointRegistry.Ensure(fields[1],out _);
                if(sortEndpoint?.Kind!=RemoteResourceSourceKind.StaticContainer||position!=sortEndpoint.Position.ToVector3())return false;
                target=new Target{Source=sortEndpoint,Destination=sortEndpoint,SourceSlot=fromCell,DestinationSlot=toCell,
                    SourceIncarnation=fields[5],DestinationIncarnation=fields[6],Mode=sortMerge?Mode.SortMerge:Mode.Sort};
                return true;
            }
            bool far=fields.Length==7&&fields[0]=="storage-far";
            bool merge=fields.Length==7&&fields[0]=="storage-merge"&&MergeCustodyReady;
            bool farMerge=fields.Length==7&&fields[0]=="storage-farmerge"&&MergeCustodyReady;
            if(fields.Length!=7||(fields[0]!="storage-one"&&!far&&!merge&&!farMerge)||
                !ParseSlot(fields[2],out int sourceSlot)||!ParseSlot(fields[4],out int destinationSlot)||
                !Guid.TryParseExact(fields[5],"N",out var sourceId)||sourceId==Guid.Empty||
                !Guid.TryParseExact(fields[6],"N",out var destinationId)||destinationId==Guid.Empty)return false;
            var source=RebirthNpcPhysicalResourceEndpointRegistry.Ensure(fields[1],out _);
            var destination=RebirthNpcPhysicalResourceEndpointRegistry.Ensure(fields[3],out _);
            float separation=far||farMerge?FarMaxSeparation:RebirthCompanionService.StorageDistance-RebirthNpcWorkRuntime.WorkRadius;
            if(source?.Kind!=RemoteResourceSourceKind.StaticContainer||destination?.Kind!=RemoteResourceSourceKind.StaticContainer||
                source.Position==destination.Position||position!=source.Position.ToVector3()||separation<=0f||
                (source.Position.ToVector3()-destination.Position.ToVector3()).sqrMagnitude>separation*separation)return false;
            target=new Target{Source=source,Destination=destination,SourceSlot=sourceSlot,DestinationSlot=destinationSlot,
                SourceIncarnation=fields[5],DestinationIncarnation=fields[6],Mode=far?Mode.Far:farMerge?Mode.FarMerge:merge?Mode.Merge:Mode.Near};return true;
        }
        catch{return false;}
    }
    private static bool ParseSlot(string text,out int slot)=>int.TryParse(text,NumberStyles.None,CultureInfo.InvariantCulture,out slot)&&slot>=0&&slot<=255;
    internal static bool Allows(RebirthNpcWorkAssignment a)=>a!=null&&Parse(a.DefinitionId,a.TargetKey,a.HasTargetPosition,a.TargetPosition,out _);
    internal static bool Allows(RebirthNpcWorkAssignmentRequest a)=>a!=null&&Parse(a.DefinitionId,a.TargetKey,a.HasTargetPosition,a.TargetPosition,out _);
    internal static bool Allows(RebirthNpcEmbodiedWorkIntent intent)=>intent!=null&&intent.Path=="concrete"&&intent.PrimaryId!=0&&intent.SecondaryId!=0&&
        intent.ExecutorId=="rebirth.work.crafting-workstation"&&intent.Actor is EntityRebirthHumanoidNPC&&intent.MatchesOriginal()&&
        RebirthNpcWorkAssignmentService.TryGet(intent.PrimaryId,out var a)&&Allows(a)&&a.NpcId==intent.StableId&&
        RebirthNpcWorkReservationService.TryGetForAssignment(a.AssignmentId,out var reservation)&&reservation.ReservationId==intent.SecondaryId;
    internal static string TargetId(RebirthNpcWorkAssignment a)=>"block:"+Mathf.FloorToInt(a.TargetPosition.x).ToString(CultureInfo.InvariantCulture)+":"+
        Mathf.FloorToInt(a.TargetPosition.y).ToString(CultureInfo.InvariantCulture)+":"+Mathf.FloorToInt(a.TargetPosition.z).ToString(CultureInfo.InvariantCulture);
    internal static Guid CreditOperation(ulong assignment)
    {var bytes=RebirthNpcConcreteWorkExecutorBase.DeriveOperationId(assignment).ToByteArray();bytes[15]^=0x43;return new Guid(bytes);}
    internal static bool Named(Vector3i position,EntityPlayer owner,string incarnation,bool ignoreBusy=false)
    {
        var world=GameManager.Instance?.World;
        if(world?.IsRemote()!=false||owner==null||owner.IsDead()||!ReferenceEquals(owner.world,world)||
            !ReferenceEquals(world.GetEntity(owner.entityId),owner)||world.GetTileEntity(position) is not TileEntityComposite tile)return false;
        var feature=tile.GetFeature<TEFeatureRebirthContainerName>();
        var source=new StaticContainerResourceSource(world,position);
        return feature!=null&&feature.Incarnation==incarnation&&!string.IsNullOrWhiteSpace(feature.CustomName)&&
            source.IsLoaded&&!source.IsExcluded&&source.IsActivated&&(ignoreBusy||!source.IsBusy)&&source.IsAuthorized(owner,true,out _);
    }
    internal static bool Current(Target target,EntityPlayer owner)=>(target.Mode==Mode.Deposit||Named(target.Source.Position,owner,target.SourceIncarnation))&&
        Named(target.Destination.Position,owner,target.DestinationIncarnation);
    // The leg is a pure function of saved rows: destination only after the saved source debit (phase 4).
    // A crash anywhere resumes at the leg the durable state implies; no new persistence is introduced.
    internal static Vector3 LegPosition(RebirthNpcWorkAssignment a,Target target)
    {
        if(target.Mode==Mode.Deposit)return target.Destination.Position.ToVector3();
        if(IsFarMode(target.Mode)&&RebirthNpcWorkNativeTransferOriginal.TryReadSaved(a.NpcId,RebirthNpcConcreteWorkExecutorBase.DeriveOperationId(a.AssignmentId),out var row)&&
            row.Phase>=4&&MatchesSource(a,target,row))return target.Destination.Position.ToVector3();
        return target.Source!=null?target.Source.Position.ToVector3():a.TargetPosition;
    }
    internal static RebirthNpcWorkAssignmentResult AssignDeposit(string session,Guid pickupOperation,Vector3i destination,int destinationSlot,
        out RebirthNpcWorkAssignment assignment,out string detail)
    {
        assignment=null;detail="A finished-work receipt and an owned named container with an empty cell are required.";
        if(RebirthNpcInteractionSessionService.Authorize(session,RebirthNpcInteractionAction.AssignWork,out var context)!=RebirthNpcInteractionResult.Allowed||context==null)
            return RebirthNpcWorkAssignmentResult.PermissionDenied;
        var to=(GameManager.Instance?.World?.GetTileEntity(destination) as TileEntityComposite)?.GetFeature<TEFeatureRebirthContainerName>();
        if(to==null||pickupOperation==Guid.Empty||!RebirthNpcStationQueueOriginal.TryRead(context.NpcId,pickupOperation,out var receipt)||
            (string)receipt.Attribute("phase")!="completed")return RebirthNpcWorkAssignmentResult.InvalidRequest;
        string key="storage-deposit:"+Token(destination)+":"+destinationSlot.ToString(CultureInfo.InvariantCulture)+":"+to.Incarnation+":"+pickupOperation.ToString("N");
        if(!Parse(DefinitionId,key,true,destination.ToVector3(),out var target)||!Current(target,RebirthNpcInteractionMutationHandlers.FindActorPlayer(context.ActorId))||
            !DestinationVacant(target))return RebirthNpcWorkAssignmentResult.PermissionDenied;
        return RebirthNpcWorkAssignmentService.Create(new RebirthNpcWorkAssignmentRequest{SessionKey=session,DefinitionId=DefinitionId,
            TargetKey=key,TargetPosition=destination.ToVector3(),HasTargetPosition=true,ExpectedRuntimeRevision=context.RuntimeRevision},out assignment,out detail);
    }
    internal static bool MatchesDeposit(RebirthNpcWorkAssignment a,Target target,RebirthNpcWorkNativeCreditOriginal.Record credit)=>credit!=null&&
        target.Mode==Mode.Deposit&&credit.Npc==a.NpcId&&credit.Operation==CreditOperation(a.AssignmentId)&&credit.SourceOperation==target.PickupOperation&&
        credit.Owner==RebirthStablePlayerIdentity.ComputeStorageKey(a.ActorId)&&credit.Destination==RemoteResourceIdentity.Static(target.Destination.Position)&&
        credit.Incarnation==target.DestinationIncarnation&&credit.DestinationSlot==target.DestinationSlot;
    internal static bool DestinationVacant(Target target)
    {
        var destination=new StaticContainerResourceSource(GameManager.Instance?.World,target.Destination.Position);
        var slots=destination.Slots;var locks=destination.SlotLocks;
        return slots!=null&&locks!=null&&target.DestinationSlot<slots.Length&&target.DestinationSlot<locks.Length&&
            !locks[target.DestinationSlot]&&(slots[target.DestinationSlot]==null||slots[target.DestinationSlot].IsEmpty());
    }
    // Merge destination: occupied by the identical exact payload with room for the one item the transfer removes.
    internal static bool DestinationMergeable(Target target)
    {
        var world=GameManager.Instance?.World;
        var from=new StaticContainerResourceSource(world,target.Source.Position);
        var to=new StaticContainerResourceSource(world,target.Destination.Position);
        var fromSlots=from.Slots;var toSlots=to.Slots;var locks=to.SlotLocks;
        if(fromSlots==null||toSlots==null||locks==null||target.SourceSlot>=fromSlots.Length||target.DestinationSlot>=toSlots.Length||
            target.DestinationSlot>=locks.Length||locks[target.DestinationSlot]||fromSlots[target.SourceSlot]==null||fromSlots[target.SourceSlot].IsEmpty())return false;
        var one=fromSlots[target.SourceSlot].Clone();one.count=1;
        return RebirthNpcPhysicalResourceEndpoint.MergeTarget(toSlots[target.DestinationSlot],RebirthHumanEquipment.Frame(one));
    }
    internal static bool DestinationReady(Target target)=>IsMergeMode(target.Mode)?DestinationMergeable(target):DestinationVacant(target);
    // Exact saved relocation witness: the single-item move (or deposit) of this outcome, proven by the saved originals.
    internal struct RelocationWitness
    {
        internal Guid Operation;internal Mode Kind;internal bool HasSource;
        internal Vector3i SourcePosition,DestinationPosition;internal int SourceSlot,DestinationSlot,Count;
        internal string ItemFrame;internal long CompletedUtcTicks;
    }
    internal static bool TryReadRelocationWitness(RebirthNpcWorkOutcomeRecord outcome,out RelocationWitness witness)
    {
        witness=default(RelocationWitness);
        try
        {
            if(!AllowsOutcome(outcome)||!RebirthNpcWorkAssignmentService.TryGet(outcome.AssignmentId,out var a)||
                !Parse(a.DefinitionId,a.TargetKey,a.HasTargetPosition,a.TargetPosition,out var target)||
                !RebirthNpcWorkNativeCreditOriginal.TryReadSaved(a.NpcId,CreditOperation(a.AssignmentId),out var credit)||
                !RebirthNpcWorkNativeCredit.HasSavedCompletion(credit))return false;
            var moved=RebirthHumanEquipment.Decode(credit.Frame);
            if(moved==null||moved.IsEmpty())return false;
            witness=new RelocationWitness{Operation=credit.Operation,Kind=target.Mode,HasSource=target.Source!=null,
                SourcePosition=target.Source!=null?target.Source.Position:default(Vector3i),SourceSlot=target.SourceSlot,
                DestinationPosition=target.Destination.Position,DestinationSlot=target.DestinationSlot,
                ItemFrame=credit.Frame,Count=moved.count,CompletedUtcTicks=outcome.CompletedUtcTicks};
            return true;
        }
        catch{return false;}
    }
    // Assigned to RebirthP1B13Policy.RelocationWitness in Register(): true only for a saved, read-back relocation.
    internal static bool IsRelocationWitness(RebirthNpcWorkOutcomeRecord outcome)=>TryReadRelocationWitness(outcome,out _);
    private static bool StampAllows(RebirthNpcWorkAssignment a,RebirthNpcWorkOutcomeRecord outcome)=>
        !RebirthNpcB09CompletionStamp.TryRead(a.NpcId,CreditOperation(a.AssignmentId),out long saved)||saved==outcome.CompletedUtcTicks;
    private static string Token(Vector3i p)=>"S@"+p.x.ToString(CultureInfo.InvariantCulture)+","+p.y.ToString(CultureInfo.InvariantCulture)+","+p.z.ToString(CultureInfo.InvariantCulture);
    internal static RebirthNpcWorkAssignmentResult Assign(string session,Vector3i source,int sourceSlot,Vector3i destination,int destinationSlot,
        out RebirthNpcWorkAssignment assignment,out string detail,bool far=false,bool merge=false)
    {
        assignment=null;detail="Two owned nearby named containers and exact source/destination cells are required.";
        if(RebirthNpcInteractionSessionService.Authorize(session,RebirthNpcInteractionAction.AssignWork,out var context)!=RebirthNpcInteractionResult.Allowed||context==null)
            return RebirthNpcWorkAssignmentResult.PermissionDenied;
        var world=GameManager.Instance?.World;
        var from=(world?.GetTileEntity(source) as TileEntityComposite)?.GetFeature<TEFeatureRebirthContainerName>();
        var to=(world?.GetTileEntity(destination) as TileEntityComposite)?.GetFeature<TEFeatureRebirthContainerName>();
        if(from==null||to==null)return RebirthNpcWorkAssignmentResult.InvalidRequest;
        string key=(far&&merge?"storage-farmerge:":far?"storage-far:":merge?"storage-merge:":"storage-one:")+Token(source)+":"+sourceSlot.ToString(CultureInfo.InvariantCulture)+":"+Token(destination)+":"+
            destinationSlot.ToString(CultureInfo.InvariantCulture)+":"+from.Incarnation+":"+to.Incarnation;
        if(!Parse(DefinitionId,key,true,source.ToVector3(),out var target)||
            !Current(target,RebirthNpcInteractionMutationHandlers.FindActorPlayer(context.ActorId))||(merge&&!DestinationMergeable(target)))return RebirthNpcWorkAssignmentResult.PermissionDenied;
        return RebirthNpcWorkAssignmentService.Create(new RebirthNpcWorkAssignmentRequest{SessionKey=session,DefinitionId=DefinitionId,
            TargetKey=key,TargetPosition=source.ToVector3(),HasTargetPosition=true,ExpectedRuntimeRevision=context.RuntimeRevision},out assignment,out detail);
    }
    // Same-container sort step. Refused until the shared credit original accepts Source==Destination.
    internal static RebirthNpcWorkAssignmentResult AssignSort(string session,Vector3i container,int fromCell,int toCell,
        out RebirthNpcWorkAssignment assignment,out string detail,bool merge=false)
    {
        assignment=null;detail="In-container sorting is held until the sort/merge custody gates are proven.";
        if(!SortCustodyReady||(merge&&!MergeCustodyReady))return RebirthNpcWorkAssignmentResult.InvalidRequest;
        if(RebirthNpcInteractionSessionService.Authorize(session,RebirthNpcInteractionAction.AssignWork,out var context)!=RebirthNpcInteractionResult.Allowed||context==null)
            return RebirthNpcWorkAssignmentResult.PermissionDenied;
        var feature=(GameManager.Instance?.World?.GetTileEntity(container) as TileEntityComposite)?.GetFeature<TEFeatureRebirthContainerName>();
        if(feature==null)return RebirthNpcWorkAssignmentResult.InvalidRequest;
        string key=(merge?"storage-sortmerge:":"storage-sort:")+Token(container)+":"+fromCell.ToString(CultureInfo.InvariantCulture)+":"+Token(container)+":"+
            toCell.ToString(CultureInfo.InvariantCulture)+":"+feature.Incarnation+":"+feature.Incarnation;
        if(!Parse(DefinitionId,key,true,container.ToVector3(),out var target)||
            !Current(target,RebirthNpcInteractionMutationHandlers.FindActorPlayer(context.ActorId))||!DestinationReady(target))return RebirthNpcWorkAssignmentResult.PermissionDenied;
        return RebirthNpcWorkAssignmentService.Create(new RebirthNpcWorkAssignmentRequest{SessionKey=session,DefinitionId=DefinitionId,
            TargetKey=key,TargetPosition=container.ToVector3(),HasTargetPosition=true,ExpectedRuntimeRevision=context.RuntimeRevision},out assignment,out detail);
    }
    internal static bool MatchesSource(RebirthNpcWorkAssignment a,Target target,RebirthNpcWorkNativeTransferOriginal.Record row)=>row!=null&&
        row.Npc==a.NpcId&&row.Owner==RebirthStablePlayerIdentity.ComputeStorageKey(a.ActorId)&&row.Operation==RebirthNpcConcreteWorkExecutorBase.DeriveOperationId(a.AssignmentId)&&
        row.Source==RemoteResourceIdentity.Static(target.Source.Position)&&row.Incarnation==target.SourceIncarnation&&row.SourceSlot==target.SourceSlot;
    internal static bool MatchesCredit(RebirthNpcWorkAssignment a,Target target,RebirthNpcWorkNativeCreditOriginal.Record credit,
        RebirthNpcWorkNativeTransferOriginal.Record source)=>MatchesSource(a,target,source)&&credit!=null&&credit.WriteAhead&&credit.Npc==a.NpcId&&
        credit.Operation==CreditOperation(a.AssignmentId)&&credit.SourceOperation==source.Operation&&credit.Owner==source.Owner&&
        credit.Source==source.Source&&credit.Frame==source.Frame&&credit.BagSlot==source.BagSlot&&
        credit.Destination==RemoteResourceIdentity.Static(target.Destination.Position)&&credit.Incarnation==target.DestinationIncarnation&&credit.DestinationSlot==target.DestinationSlot&&credit.Merge==IsMergeMode(target.Mode);
    internal static bool PotentiallyReady(RebirthNpcWorkAssignment a,EntityRebirthHumanoidNPC npc)
    {
        if(!Allows(a)||!Parse(a.DefinitionId,a.TargetKey,a.HasTargetPosition,a.TargetPosition,out var target)||
            !Current(target,RebirthNpcInteractionMutationHandlers.FindActorPlayer(a.ActorId)))return false;
        var sourceOperation=RebirthNpcConcreteWorkExecutorBase.DeriveOperationId(a.AssignmentId);
        if(target.Mode==Mode.Deposit)
            return RebirthNpcWorkNativeCreditOriginal.TryReadSaved(a.NpcId,CreditOperation(a.AssignmentId),out var saved)?MatchesDeposit(a,target,saved):
                DestinationVacant(target)&&RebirthNpcStationQueueOriginal.TryRead(a.NpcId,target.PickupOperation,out var receipt)&&(string)receipt.Attribute("phase")=="completed";
        if(RebirthNpcWorkNativeTransferOriginal.TryReadSaved(a.NpcId,sourceOperation,out var source))return MatchesSource(a,target,source);
        if(!DestinationReady(target))return false;
        var owner=RebirthNpcInteractionMutationHandlers.FindActorPlayer(a.ActorId);
        for(int bag=0;bag<npc.bag.SlotCount;bag++)
            if((npc.bag.ItemGrid[bag]==null||npc.bag.ItemGrid[bag].IsEmpty())&&
                target.Source.TryProbeSourceSlotForNpc(owner,npc,target.SourceSlot,out _))return true;
        return false;
    }
    internal static bool AllowsOutcome(RebirthNpcWorkOutcomeRecord outcome)
    {
        return outcome!=null&&IsDefinition(outcome.DefinitionId)&&outcome.State==RebirthNpcWorkOutcomeState.Completed&&
            RebirthNpcWorkAssignmentService.TryGet(outcome.AssignmentId,out var a)&&Allows(a)&&a.NpcId==outcome.NpcId&&StampAllows(a,outcome)&&
            Parse(a.DefinitionId,a.TargetKey,a.HasTargetPosition,a.TargetPosition,out var target)&&
            (target.Mode==Mode.Deposit?
                RebirthNpcWorkNativeCreditOriginal.TryReadSaved(a.NpcId,CreditOperation(a.AssignmentId),out var deposit)&&MatchesDeposit(a,target,deposit)&&
                RebirthNpcWorkNativeCredit.HasSavedCompletion(deposit)&&outcome.OutcomeId==deposit.Operation&&outcome.TargetId==TargetId(a)&&
                outcome.StartedUtcTicks==a.CreatedUtcTicks&&outcome.DurableWitness=="named-storage-deposit:"+target.PickupOperation.ToString("N")+":"+deposit.Binding+":"+deposit.CreditProof&&
                outcome.Detail==DepositDetail&&outcome.Produced==null&&outcome.Consumed==null:
            RebirthNpcWorkNativeTransferOriginal.TryReadSaved(a.NpcId,RebirthNpcConcreteWorkExecutorBase.DeriveOperationId(a.AssignmentId),out var source)&&source.Phase==4&&
            RebirthNpcWorkNativeCreditOriginal.TryReadSaved(a.NpcId,CreditOperation(a.AssignmentId),out var credit)&&MatchesCredit(a,target,credit,source)&&
            RebirthNpcWorkNativeCredit.HasSavedCompletion(credit)&&outcome.OutcomeId==credit.Operation&&outcome.TargetId==TargetId(a)&&
            outcome.StartedUtcTicks==a.CreatedUtcTicks&&outcome.DurableWitness=="named-storage-move:"+source.Binding+":"+credit.Binding+":"+credit.CreditProof&&
            outcome.Detail=="One existing item moved between the assigned named containers; no item produced or consumed."&&outcome.Produced==null&&outcome.Consumed==null);
    }
}

public sealed class RebirthNpcStorageOrganizationWorkAdapter:IRebirthNpcCraftingWorkAdapter
{
    public string AdapterId=>"rebirth.storage.physical-one-item";
    public int Priority=>780;
    public bool CanHandle(RebirthNpcConcreteWorkContext c)=>RebirthNpcStorageOrganizationWork.Allows(c?.Assignment);
    private static RebirthNpcWorkMutationResult Hold(string detail)=>new RebirthNpcWorkMutationResult{
        Disposition=RebirthNpcWorkExecutionDisposition.Suspend,FailureCategory=RebirthNpcWorkFailureCategory.InventoryUnavailable,Detail=detail};
    public RebirthNpcWorkMutationResult BeginOperation(RebirthNpcConcreteWorkContext c)=>CanHandle(c)?
        RebirthNpcWorkMutationResult.Continue(0f,"Exact named-container move accepted; awaiting physical arrival."):Hold("Saved named-container assignment is unavailable.");
    public RebirthNpcWorkMutationResult TickOperation(RebirthNpcConcreteWorkContext c)
    {
        if(!CanHandle(c)||!RebirthNpcStorageOrganizationWork.Parse(c.Assignment.DefinitionId,c.Assignment.TargetKey,c.Assignment.HasTargetPosition,c.Assignment.TargetPosition,out var target)||
            RebirthNpcEmbodiedWorkOwner.CurrentDispatchIntent is not RebirthNpcEmbodiedWorkIntent intent||!RebirthNpcEmbodiedWorkOwner.IsDispatching(intent)||
            !RebirthNpcStorageOrganizationWork.Allows(intent)||intent.Actor is not EntityRebirthHumanoidNPC npc||npc.bag?.ItemGrid==null||
            npc.GetAttackTarget()!=null||npc.Hand==null||npc.Hand.IsBusy)return Hold("Original storage EAI custody is unavailable.");
        var a=c.Assignment;var owner=RebirthNpcInteractionMutationHandlers.FindActorPlayer(a.ActorId);
        if(!RebirthNpcStorageOrganizationWork.Current(target,owner)||
            (npc.position-RebirthNpcStorageOrganizationWork.LegPosition(a,target)).sqrMagnitude>RebirthNpcWorkRuntime.WorkRadiusSquared)
            return Hold("Named storage identity, access or physical worker arrival is unavailable.");
        if(target.Mode==RebirthNpcStorageOrganizationWork.Mode.Deposit)return TickDeposit(c,target,owner,npc);
        if(!RebirthNpcWorkNativeTransferOriginal.TryReadSaved(a.NpcId,c.OperationId,out var source))
        {
            if(!RebirthNpcStorageOrganizationWork.DestinationReady(target))return Hold("Assigned destination cell is full or locked; source remains untouched.");
            for(int bag=0;bag<npc.bag.SlotCount;bag++)
            {
                if(npc.bag.ItemGrid[bag]!=null&&!npc.bag.ItemGrid[bag].IsEmpty())continue;
                if(!target.Source.TryObserveExactStackForNpc(owner,npc,target.SourceSlot,bag,out _))continue;
                return RebirthNpcWorkNativeTransfer.TryStart(owner,npc,target.Source,target.SourceSlot,bag,c.OperationId,out _)?
                    RebirthNpcWorkMutationResult.Continue(0f,"Exact source-to-worker transfer prepared."):Hold("Source custody is awaiting its saved original.");
            }
            return Hold("Exact source item or empty worker cell is unavailable.");
        }
        if(!RebirthNpcStorageOrganizationWork.MatchesSource(a,target,source))return Hold("Saved source original differs from this assignment.");
        var creditOperation=RebirthNpcStorageOrganizationWork.CreditOperation(a.AssignmentId);
        if(!RebirthNpcWorkNativeCreditOriginal.TryReadSaved(a.NpcId,creditOperation,out var credit))
        {
            // A far worker only leaves the source after phase 4 is saved, so it must not re-advance the source leg from the destination.
            if(source.Phase!=4&&(!RebirthNpcWorkNativeTransfer.TryAdvance(owner,npc,c.OperationId)||
                !RebirthNpcWorkNativeTransferOriginal.TryReadSaved(a.NpcId,c.OperationId,out source)||source.Phase!=4))
                return Hold("Source debit and saved native worker credit are settling.");
            // Reachable only in the transition tick: the range gate above ran against the source leg before TryAdvance saved phase 4;
            // every later tick is gated by LegPosition (now the destination), so it never reaches here while still far.
            if(RebirthNpcStorageOrganizationWork.IsFarMode(target.Mode)&&
                (npc.position-target.Destination.Position.ToVector3()).sqrMagnitude>RebirthNpcWorkRuntime.WorkRadiusSquared)
                return RebirthNpcWorkMutationResult.Continue(0f,"Source debit saved; carrying the original item to the destination container.");
            return RebirthNpcWorkNativeCredit.TryStart(owner,npc,c.OperationId,target.Destination,source.BagSlot,target.DestinationSlot,creditOperation,RebirthNpcStorageOrganizationWork.IsMergeMode(target.Mode),RebirthNpcStorageOrganizationWork.IsSameContainerMode(target.Mode))?
                RebirthNpcWorkMutationResult.Continue(0f,"Exact worker-to-destination original prepared."):Hold("Destination access or exact empty cell is blocked; original item stays with worker.");
        }
        if(!RebirthNpcStorageOrganizationWork.MatchesCredit(a,target,credit,source)||!RebirthNpcWorkNativeCredit.TryAdvance(owner,npc,creditOperation)||
            !RebirthNpcWorkNativeCreditOriginal.TryReadSaved(a.NpcId,creditOperation,out credit)||!RebirthNpcWorkNativeCredit.HasSavedCompletion(credit))
            return Hold("Original destination write/publication and idempotent worker clear are settling.");
        if(!RebirthNpcB09CompletionStamp.GetOrMint(a.NpcId,credit.Operation,a.CreatedUtcTicks+1,out long completedTicks))return Hold("Saved completion time is settling.");
        var outcome=new RebirthNpcWorkOutcomeRecord{OutcomeId=credit.Operation,AssignmentId=a.AssignmentId,NpcId=a.NpcId,
            DefinitionId=RebirthNpcStorageOrganizationWork.DefinitionId,TargetId=RebirthNpcStorageOrganizationWork.TargetId(a),
            DurableWitness="named-storage-move:"+source.Binding+":"+credit.Binding+":"+credit.CreditProof,
            StartedUtcTicks=a.CreatedUtcTicks,CompletedUtcTicks=completedTicks,State=RebirthNpcWorkOutcomeState.Completed,
            FailureCategory=RebirthNpcWorkFailureCategory.None,Detail="One existing item moved between the assigned named containers; no item produced or consumed."};
        return RebirthNpcWorkOutcomeService.TryRecordCommitted(outcome)?new RebirthNpcWorkMutationResult{
            Disposition=RebirthNpcWorkExecutionDisposition.Completed,MutationCommitted=true,Detail=outcome.Detail}:Hold("Exact native move is awaiting its committed outcome.");
    }
    // Finished-work deposit: P6's bag-source credit chain (write-ahead lease, saved proof, saved bag clear).
    private static RebirthNpcWorkMutationResult TickDeposit(RebirthNpcConcreteWorkContext c,RebirthNpcStorageOrganizationWork.Target target,
        EntityPlayer owner,EntityRebirthHumanoidNPC npc)
    {
        var a=c.Assignment;var creditOperation=RebirthNpcStorageOrganizationWork.CreditOperation(a.AssignmentId);
        if(!RebirthNpcWorkNativeCreditOriginal.TryReadSaved(a.NpcId,creditOperation,out var credit))
        {
            if(!RebirthNpcStorageOrganizationWork.DestinationVacant(target))return Hold("Assigned destination cell is full or locked; the finished output stays with the worker.");
            return RebirthNpcWorkNativeCredit.TryStartFromPickupReceipt(owner,npc,target.PickupOperation,target.Destination,target.DestinationSlot,creditOperation)?
                RebirthNpcWorkMutationResult.Continue(0f,"Exact finished-output credit prepared."):Hold("Finished-output receipt or destination access is not ready; output stays with the worker.");
        }
        if(!RebirthNpcStorageOrganizationWork.MatchesDeposit(a,target,credit)||!RebirthNpcWorkNativeCredit.TryAdvance(owner,npc,creditOperation)||
            !RebirthNpcWorkNativeCreditOriginal.TryReadSaved(a.NpcId,creditOperation,out credit)||!RebirthNpcWorkNativeCredit.HasSavedCompletion(credit))
            return Hold("Original destination write/publication and idempotent worker clear are settling.");
        if(!RebirthNpcB09CompletionStamp.GetOrMint(a.NpcId,credit.Operation,a.CreatedUtcTicks+1,out long completedTicks))return Hold("Saved completion time is settling.");
        var outcome=new RebirthNpcWorkOutcomeRecord{OutcomeId=credit.Operation,AssignmentId=a.AssignmentId,NpcId=a.NpcId,
            DefinitionId=RebirthNpcStorageOrganizationWork.DefinitionId,TargetId=RebirthNpcStorageOrganizationWork.TargetId(a),
            DurableWitness="named-storage-deposit:"+target.PickupOperation.ToString("N")+":"+credit.Binding+":"+credit.CreditProof,
            StartedUtcTicks=a.CreatedUtcTicks,CompletedUtcTicks=completedTicks,State=RebirthNpcWorkOutcomeState.Completed,
            FailureCategory=RebirthNpcWorkFailureCategory.None,Detail=RebirthNpcStorageOrganizationWork.DepositDetail};
        return RebirthNpcWorkOutcomeService.TryRecordCommitted(outcome)?new RebirthNpcWorkMutationResult{
            Disposition=RebirthNpcWorkExecutionDisposition.Completed,MutationCommitted=true,Detail=outcome.Detail}:Hold("Exact deposit is awaiting its committed outcome.");
    }
    public void CancelOperation(RebirthNpcConcreteWorkContext c,string reason){}
}

// Leg-aware target adapter for Far (distant) storage moves: the travel target is the source until the
// saved source debit, then the destination. Registered at higher priority than the generic position adapter.
public sealed class RebirthNpcStorageLegTargetAdapter:IRebirthNpcWorkTargetAdapter
{
    public string AdapterId=>"rebirth.target.storage-leg";
    public int Priority=>410;
    private static bool Leg(RebirthNpcWorkAssignment a,out RebirthNpcStorageOrganizationWork.Target target)
    {
        target=null;
        return a!=null&&RebirthNpcStorageOrganizationWork.Parse(a.DefinitionId,a.TargetKey,a.HasTargetPosition,a.TargetPosition,out target)&&
            RebirthNpcStorageOrganizationWork.IsFarMode(target.Mode);
    }
    public bool CanResolve(RebirthNpcWorkAssignment a,RebirthNpcWorkDefinition d)=>Leg(a,out _);
    public bool TryResolve(RebirthNpcWorkAssignment a,RebirthNpcWorkDefinition d,out RebirthNpcWorkTargetSnapshot target,out string detail)
    {
        target=null;detail="Saved storage legs are unavailable.";
        if(!Leg(a,out var t))return false; // ownership and loaded state are judged by Validate (retryable vs denied)
        // Revision is constant: the leg flips on a saved row, which must not read as a changed target.
        target=new RebirthNpcWorkTargetSnapshot{AdapterId=AdapterId,StableTargetId=RebirthNpcStorageOrganizationWork.TargetId(a),TargetRevision="storage-leg-1",
            Position=RebirthNpcStorageOrganizationWork.LegPosition(a,t),IsLoaded=true,IsAvailable=true,InteractionRange=RebirthNpcWorkRuntime.WorkRadius,
            Detail="Named container for the current storage leg."};
        detail=target.Detail;return true;
    }
    // Unloaded or momentarily busy containers are retryable (item stays in saved worker custody);
    // only a real ownership/name/incarnation failure is AuthorizationDenied.
    private static bool Reachable(RebirthNpcStorageOrganizationWork.Target t)
    {
        var world=GameManager.Instance?.World;
        var src=new StaticContainerResourceSource(world,t.Source.Position);
        var dst=new StaticContainerResourceSource(world,t.Destination.Position);
        return src.IsLoaded&&dst.IsLoaded&&!src.IsBusy&&!dst.IsBusy;
    }
    public bool Validate(RebirthNpcConcreteWorkContext c,out RebirthNpcWorkFailureCategory failure,out string detail)
    {
        failure=RebirthNpcWorkFailureCategory.InvalidTarget;detail="Saved storage target is invalid.";
        if(c?.Assignment==null||c.Target==null||!Leg(c.Assignment,out var t))return false;
        if(!Reachable(t))
        {failure=RebirthNpcWorkFailureCategory.TargetUnavailable;detail="A named container is unloaded or in use; the worker keeps any carried item and retries.";return false;}
        failure=RebirthNpcWorkFailureCategory.AuthorizationDenied;detail="Named storage identity or access changed.";
        if(!RebirthNpcStorageOrganizationWork.Current(t,RebirthNpcInteractionMutationHandlers.FindActorPlayer(c.Assignment.ActorId)))return false;
        failure=RebirthNpcWorkFailureCategory.None;detail="Named containers remain owned and loaded.";return true;
    }
    public bool IsNpcInRange(RebirthNpcConcreteWorkContext c,out string detail)
    {
        detail="Worker must reach the current storage leg.";
        if(!Leg(c.Assignment,out var t)||!RebirthNpcWorkNavigationService.TryResolveNpc(c.Assignment.NpcId,out var npc))return false;
        bool current=(npc.position-RebirthNpcStorageOrganizationWork.LegPosition(c.Assignment,t)).sqrMagnitude<=RebirthNpcWorkRuntime.WorkRadiusSquared;
        if(current)detail="Worker reached the current storage leg.";return current;
    }
}

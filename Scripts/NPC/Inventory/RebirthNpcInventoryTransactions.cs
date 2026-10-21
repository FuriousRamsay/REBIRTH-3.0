using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Linq;
using System.Xml.Linq;

#nullable disable

public enum RebirthNpcInventoryTransactionResult : byte
{
    Applied = 0,
    Replayed = 1,
    InvalidRequest = 2,
    CapabilityDenied = 3,
    RevisionConflict = 4,
    InsufficientQuantity = 5,
    ReservationConflict = 6,
    NotFound = 7,
    AuthorityDenied = 8
}



public enum RebirthNpcInventoryTransferResult : byte
{
    Applied = 0,
    Replayed = 1,
    InvalidRequest = 2,
    CapabilityDenied = 3,
    RevisionConflict = 4,
    InsufficientQuantity = 5,
    ReservationConflict = 6,
    NotFound = 7,
    SameInventory = 8,
    AuthorityDenied = 9
}

public sealed class RebirthNpcInventoryTransfer
{
    public Guid TransactionId { get; }
    public RebirthNpcStableId SourceNpcId { get; }
    public RebirthNpcStableId DestinationNpcId { get; }
    public uint ExpectedSourceRevision { get; }
    public uint ExpectedDestinationRevision { get; }
    public string ItemKey { get; }
    public int Quantity { get; }
    public string AuthorityKey { get; }

    public RebirthNpcInventoryTransfer(Guid transactionId, RebirthNpcStableId sourceNpcId,
        RebirthNpcStableId destinationNpcId, uint expectedSourceRevision, uint expectedDestinationRevision,
        string itemKey, int quantity, string authorityKey)
    {
        TransactionId = transactionId;
        SourceNpcId = sourceNpcId;
        DestinationNpcId = destinationNpcId;
        ExpectedSourceRevision = expectedSourceRevision;
        ExpectedDestinationRevision = expectedDestinationRevision;
        ItemKey = (itemKey ?? string.Empty).Trim();
        Quantity = quantity;
        AuthorityKey = (authorityKey ?? string.Empty).Trim();
    }
}

public sealed class RebirthNpcInventoryMutation
{
    public string ItemKey { get; }
    public int QuantityDelta { get; }

    public RebirthNpcInventoryMutation(string itemKey, int quantityDelta)
    {
        ItemKey = (itemKey ?? string.Empty).Trim();
        QuantityDelta = quantityDelta;
    }
}

public sealed class RebirthNpcInventoryTransaction
{
    public Guid TransactionId { get; }
    public RebirthNpcStableId NpcId { get; }
    public uint ExpectedRevision { get; }
    public string AuthorityKey { get; }
    public IReadOnlyList<RebirthNpcInventoryMutation> Mutations { get; }

    public RebirthNpcInventoryTransaction(Guid transactionId, RebirthNpcStableId npcId, uint expectedRevision,
        string authorityKey, IReadOnlyList<RebirthNpcInventoryMutation> mutations)
    {
        TransactionId = transactionId;
        NpcId = npcId;
        ExpectedRevision = expectedRevision;
        AuthorityKey = (authorityKey ?? string.Empty).Trim();
        Mutations = mutations;
    }
}

public sealed class RebirthNpcInventorySnapshot
{
    internal Dictionary<Guid,string> LogisticsEffects;
    internal RebirthNpcNativeStackSet NativeStacks;
    public RebirthNpcStableId NpcId { get; internal set; }
    public uint Revision { get; internal set; }
    public Dictionary<string, int> Quantities { get; internal set; }
    public Dictionary<string, int> Reservations { get; internal set; }
}

public sealed class RebirthNpcInventoryPersistentRecord
{
    internal Dictionary<Guid,string> LogisticsEffects;
    internal RebirthNpcNativeStackSet NativeStacks;
    public RebirthNpcStableId NpcId { get; internal set; }
    public uint Revision { get; internal set; }
    public Dictionary<string, int> Quantities { get; internal set; }
    public Dictionary<string, int> Reservations { get; internal set; }
    public Guid[] ReplayJournal { get; internal set; }
}

public static partial class RebirthNpcInventoryTransactionService
{
    private sealed class InventoryState
    {
        internal readonly Dictionary<Guid,string> LogisticsEffects=new Dictionary<Guid,string>();
        internal RebirthNpcNativeStackSet NativeStacks;
        public uint Revision;
        public readonly Dictionary<string, int> Quantities = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, int> Reservations = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        public readonly HashSet<Guid> AppliedTransactions = new HashSet<Guid>();
        public readonly Queue<Guid> AppliedTransactionOrder = new Queue<Guid>();
    }

    private const int MaxReplayTransactionsPerInventory = 256;
    private static readonly object Sync = new object();
    private static Dictionary<RebirthNpcStableId, InventoryState> States = new Dictionary<RebirthNpcStableId, InventoryState>();
    private static object generationEpoch=new object();
    internal static object RecoveryEpoch{get{lock(Sync)return generationEpoch;}}
    private static long applied;
    private static long replayed;
    private static long rejected;

    public static RebirthNpcInventoryTransactionResult Apply(RebirthNpcInventoryTransaction transaction, out uint resultingRevision)
        =>ApplyCore(transaction,RebirthNpcInventoryAuthorityOperations.Mutate,out resultingRevision);

    // Only the external transfer coordinator uses this logical leg, after its
    // external reservation is prepared. Transfer never grants general Mutate.
    internal static RebirthNpcInventoryTransactionResult ApplyExternalTransferMutation(RebirthNpcExternalTransferRequest request,out uint resultingRevision)
    {
        resultingRevision=0;
        if(request==null||request.Quantity<=0||string.IsNullOrEmpty(request.EndpointId)||
            !Enum.IsDefined(typeof(RebirthNpcExternalTransferDirection),request.Direction))return RebirthNpcInventoryTransactionResult.InvalidRequest;
        int delta=request.Direction==RebirthNpcExternalTransferDirection.ExternalToNpc?request.Quantity:-request.Quantity;
        var transaction=new RebirthNpcInventoryTransaction(request.TransactionId,request.NpcId,request.ExpectedNpcRevision,request.AuthorityKey,
            new[]{new RebirthNpcInventoryMutation(request.ItemKey,delta)});
        return ApplyCore(transaction,RebirthNpcInventoryAuthorityOperations.Transfer,out resultingRevision);
    }

    // Logistics alone executes this original two-stage plan. Public Mutate and
    // ordinary Transfer semantics remain unchanged. Final file observation still
    // belongs to the logistics dispatcher after this runtime mutation returns.
    internal static bool CanAdmitLogisticsOriginalPlan(EntityPlayer actor,RebirthLogisticsTransferJournal.Record original,RebirthNpcStableId id)
    {
        if((RebirthNpcTradeService.BlocksOwnerPreparedNpc(id)||RebirthNpcMetabolismCustody.Blocks(id)||RemoteResourceTransactions.ActionBlocksNpc(id)))return false;
        try
        {
            if(!ThreadManager.IsMainThread()||actor?.world?.IsRemote()!=false||actor.IsDead()||original==null||id.IsEmpty||
                !ReferenceEquals(GameManager.Instance?.World,actor.world)||!ReferenceEquals(actor.world.GetEntity(actor.entityId),actor)||actor.Died!=original.Deaths||
                !RebirthOriginalActionScope.Matches(actor,original.Owner,original.Creation)||RebirthLogisticsTransferJournal.WorldKey(actor.world)!=original.World||
                !RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var owner)||owner.StorageKey!=original.Owner||
                !RebirthLogisticsTransferJournal.TryRead(original.Owner,original.Operation,out var retained)||retained.Phase!=0||retained.Rejecting||
                RebirthLogisticsTransferJournal.Encode(original)!=RebirthLogisticsTransferJournal.Encode(retained)||
                !RebirthNpcRuntimeRegistry.TryGetEntityId(id,out int entityId)||!(actor.world.GetEntity(entityId) is EntityRebirthNPC npc)||npc.IsDead()||
                !RemoteNpcResourceSource.IsAuthorized(npc,actor,false,out _)||RebirthNpcInteractionSessionService.IsInventoryAccessing(id)||
                (npc.position-actor.position).sqrMagnitude>LogisticsPreviewService.CompanionRadiusForPlanning*LogisticsPreviewService.CompanionRadiusForPlanning)return false;
            if(original.Action==QuickStackRadialAction.CompanionInventoryPull)
            {
                var user=GameManager.Instance.GetPersistentPlayerList()?.GetPlayerDataFromEntityID(actor.entityId)?.PrimaryId;
                if(!RebirthCompanionService.TryResolveInventoryTransferTarget(actor.world,actor.entityId,user,original.Target,out var target)||target.IsDog||target.StableId!=id.ToString())return false;
            }
            RebirthNpcInventoryPersistenceStore.EnsureLoaded();if(!RebirthNpcInventoryPersistenceStore.IsTransferLoaded)return false;
            var leg=retained.Npcs.SingleOrDefault(value=>value.StableId==id.ToString());if(leg==null||leg.Attempted!=0||leg.Published!=0)return false;
            lock(Sync)
            {
                var state=GetOrCreate(id);
                if(state.Revision!=leg.Revision||(state.NativeStacks?.Write().ToString(SaveOptions.DisableFormatting)??string.Empty)!=(leg.NativeBefore??string.Empty)||
                    state.AppliedTransactions.Contains(leg.DebitId)||state.AppliedTransactions.Contains(leg.CreditId))return false;
                var known=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach(var entry in leg.Entries)
                {
                    known.Add(entry.Key);state.Quantities.TryGetValue(entry.Key,out int count);state.Reservations.TryGetValue(entry.Key,out int reserved);
                    if(count!=entry.Before||reserved!=entry.Reserved||(state.NativeStacks?.GetQuantity(entry.Key)??0)!=entry.Custodied||
                        RebirthCompanionInventoryLockService.IsNpcItemLocked(id,entry.Key)!=entry.Locked)return false;
                }
                if(state.Quantities.Keys.Any(key=>!known.Contains(key))||state.Reservations.Keys.Any(key=>!known.Contains(key)))return false;
            }
            return RebirthNpcOriginalEffectAdmission.TryReserveLogistics(actor,retained,out var lease)&&lease.Current();
        }
        catch{return false;}
    }
    private static bool HasSavedLogisticsDebit(RebirthLogisticsTransferJournal.Record original,RebirthLogisticsTransferJournal.NpcLeg leg,RebirthNpcStableId id)
        =>RebirthNpcInventoryPersistenceStore.TryReadSavedTransferSnapshot(id,out var saved)&&ValidatePersistentRecord(saved,out _)&&
            saved.LogisticsEffects!=null&&saved.LogisticsEffects.TryGetValue(leg.DebitId,out var receipt)&&
            RebirthLogisticsNpcEffectReceipt.MatchesLogistics(original,leg,false,receipt);
    internal static RebirthNpcInventoryTransactionResult ApplyLogisticsOriginalPlan(EntityPlayer actor,
        RebirthLogisticsTransferJournal.Record original,RebirthNpcStableId npcId,bool credit,string authorityKey,out uint resultingRevision,bool preflightOnly=false)
    {
        resultingRevision=0;
        if((RebirthNpcTradeService.BlocksOwnerPreparedNpc(npcId)||RebirthNpcMetabolismCustody.Blocks(npcId)||RemoteResourceTransactions.ActionBlocksNpc(npcId)))return RebirthNpcInventoryTransactionResult.ReservationConflict;
        if(!ThreadManager.IsMainThread()||actor?.world?.IsRemote()!=false||actor.IsDead()||original==null||npcId.IsEmpty||
            !ReferenceEquals(GameManager.Instance?.World,actor.world)||!ReferenceEquals(actor.world.GetEntity(actor.entityId),actor)||
            !RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var identity)||identity.StorageKey!=original.Owner||
            !(RebirthLogisticsDeathSettlement.ActorCurrent(actor,original)||RebirthLogisticsDeathSettlement.MatchesPaidSourceContinuation(actor,original))||
            !RebirthLogisticsTransferJournal.TryRead(original.Owner,original.Operation,out var retained)||retained.Phase!=1||retained.Outcome!=0||retained.Legs.Any(endpoint=>endpoint.Restored)||string.IsNullOrEmpty(retained.OwnerDebitProof))return RebirthNpcInventoryTransactionResult.InvalidRequest;
        RebirthLogisticsTransferJournal.NpcLeg leg;
        try
        {
            if(RebirthLogisticsTransferJournal.Encode(retained)!=RebirthLogisticsTransferJournal.Encode(original))return RebirthNpcInventoryTransactionResult.InvalidRequest;
            leg=retained.Npcs.SingleOrDefault(candidate=>candidate.StableId==npcId.ToString());
            if(leg==null||leg.Revision>uint.MaxValue-2||(!preflightOnly&&leg.Attempted!=(credit?2:1))||!RebirthNpcOriginalEffectAdmission.IsReservedLogistics(retained,npcId)||credit&&!preflightOnly&&(leg.Published<1||retained.Legs.Any(endpoint=>endpoint.Published<1)||retained.Npcs.Any(endpoint=>endpoint.Published<1)))return RebirthNpcInventoryTransactionResult.InvalidRequest;
        }
        catch{return RebirthNpcInventoryTransactionResult.InvalidRequest;}
        if(!RebirthNpcInventoryAuthorityService.Validate(authorityKey,RebirthNpcInventoryAuthorityOperations.Transfer,npcId))return RebirthNpcInventoryTransactionResult.AuthorityDenied;
        if(!RebirthNpcRuntimeRegistry.TryGetEntityId(npcId,out int entityId)||!(actor.world.GetEntity(entityId) is EntityRebirthNPC npc)||
            npc.IsDead()||!RemoteNpcResourceSource.IsAuthorized(npc,actor,false,out _)||RebirthNpcInteractionSessionService.IsInventoryAccessing(npcId)||
            (npc.position-actor.position).sqrMagnitude>LogisticsPreviewService.CompanionRadiusForPlanning*LogisticsPreviewService.CompanionRadiusForPlanning)return RebirthNpcInventoryTransactionResult.NotFound;
        if(original.Action==QuickStackRadialAction.CompanionInventoryPull)
        {
            var persistent=GameManager.Instance.GetPersistentPlayerList()?.GetPlayerDataFromEntityID(actor.entityId);
            if(persistent?.PrimaryId==null||!RebirthCompanionService.TryResolveInventoryTransferTarget(actor.world,actor.entityId,persistent.PrimaryId,original.Target,out var target)||
                target.StableId!=npcId.ToString()||target.IsDog)return RebirthNpcInventoryTransactionResult.AuthorityDenied;
        }
        RebirthNpcInventoryPersistenceStore.EnsureLoaded();if(!RebirthNpcInventoryPersistenceStore.IsTransferLoaded)return RebirthNpcInventoryTransactionResult.NotFound;
        bool mutationStarted=false;
        try
        {
            RebirthNpcNativeStackSet beforeNative=null;
            if(!string.IsNullOrEmpty(leg.NativeBefore)&&!RebirthNpcNativeStackSet.TryRead(XElement.Parse(leg.NativeBefore),out beforeNative))return RebirthNpcInventoryTransactionResult.InvalidRequest;
            var nativeRecords=new List<RebirthNpcNativeStackRecord>();
            if(beforeNative!=null)foreach(var node in beforeNative.Write().Elements())
            {if(!RebirthNpcNativeStackRecord.TryRead(node,out var item))return RebirthNpcInventoryTransactionResult.InvalidRequest;nativeRecords.Add(item);}
            if(credit)foreach(var item in leg.NativeCredits)
            {
                if(!RebirthNpcNativeStackRecord.TryCapture(item.StackId,npcId,item.Stack,out var captured)||!string.Equals(captured.ItemKey,item.Key,StringComparison.OrdinalIgnoreCase))return RebirthNpcInventoryTransactionResult.InvalidRequest;
                nativeRecords.Add(captured);
            }
            if(preflightOnly&&!credit)
            {
                var finalRecords=new List<RebirthNpcNativeStackRecord>(nativeRecords);
                foreach(var item in leg.NativeCredits)
                {if(!RebirthNpcNativeStackRecord.TryCapture(item.StackId,npcId,item.Stack,out var captured))return RebirthNpcInventoryTransactionResult.InvalidRequest;finalRecords.Add(captured);}
                if(finalRecords.Count>0&&(!RebirthNpcNativeStackSet.TryCreate(npcId,leg.Revision+2,finalRecords,out var finalNative)||
                    !finalNative.FitsQuantities(leg.Entries.Where(entry=>entry.After>0).ToDictionary(entry=>entry.Key,entry=>entry.After,StringComparer.OrdinalIgnoreCase))))return RebirthNpcInventoryTransactionResult.InvalidRequest;
            }
            uint expected=leg.Revision+(credit?1U:0U),next=expected+1;
            RebirthNpcNativeStackSet proposedNative=null;
            if((beforeNative!=null||nativeRecords.Count>0)&&!RebirthNpcNativeStackSet.TryCreate(npcId,next,nativeRecords,out proposedNative))return RebirthNpcInventoryTransactionResult.InvalidRequest;
            var proposed=new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase);
            foreach(var entry in leg.Entries)
            {
                int count=credit?entry.After:Math.Min(entry.Before,entry.After-entry.PendingNative);
                if(count<(long)entry.Reserved+entry.Custodied||entry.Locked&&count<entry.Before)return RebirthNpcInventoryTransactionResult.ReservationConflict;
                if(count>0)proposed.Add(entry.Key,count);
            }
            if(proposedNative!=null&&!proposedNative.FitsQuantities(proposed))return RebirthNpcInventoryTransactionResult.ReservationConflict;
            Guid transaction=credit?leg.CreditId:leg.DebitId;
            string effect=RebirthLogisticsNpcEffectReceipt.ExpectedLogistics(retained,leg,credit);
            lock(Sync)
            {
                var state=GetOrCreate(npcId);resultingRevision=state.Revision;
                if(state.LogisticsEffects.TryGetValue(transaction,out var appliedEffect))
                    return RebirthLogisticsNpcEffectReceipt.MatchesLogistics(retained,leg,credit,appliedEffect)&&RebirthLogisticsNpcEffectReceipt.Validate(npcId,state.Revision,state.LogisticsEffects)?RebirthNpcInventoryTransactionResult.Replayed:RebirthNpcInventoryTransactionResult.RevisionConflict;
                // A remembered generic transaction GUID is not this typed
                // actual-effect witness. Retain such an unknown original.
                if(state.AppliedTransactions.Contains(transaction)||state.LogisticsEffects.Count>=RebirthLogisticsNpcEffectReceipt.MaximumReceipts||
                    !RebirthLogisticsNpcEffectReceipt.Validate(npcId,state.Revision,state.LogisticsEffects))return RebirthNpcInventoryTransactionResult.InvalidRequest;
                if(credit&&state.Revision>expected)
                {
                    // Original debit is already positively saved. Later lawful
                    // work is not a replacement baseline for another debit;
                    // add only this original's retained positive credit delta.
                    if(state.Revision==uint.MaxValue||!state.LogisticsEffects.TryGetValue(leg.DebitId,out var debitReceipt)||
                        !RebirthLogisticsNpcEffectReceipt.MatchesLogistics(retained,leg,false,debitReceipt)||!HasSavedLogisticsDebit(retained,leg,npcId)||
                        state.NativeStacks!=null&&(state.NativeStacks.Owner!=npcId.ToString()||state.NativeStacks.Revision!=state.Revision||!state.NativeStacks.FitsQuantities(state.Quantities)))return RebirthNpcInventoryTransactionResult.RevisionConflict;
                    var continued=new Dictionary<string,int>(state.Quantities,StringComparer.OrdinalIgnoreCase);
                    foreach(var entry in leg.Entries)
                    {
                        int delta=entry.After-Math.Min(entry.Before,entry.After-entry.PendingNative);if(delta<0)return RebirthNpcInventoryTransactionResult.InvalidRequest;
                        continued.TryGetValue(entry.Key,out int current);long total=(long)current+delta;
                        if(total>int.MaxValue||delta>0&&current==0&&RebirthCompanionInventoryLockService.IsNpcItemLocked(npcId,entry.Key))return RebirthNpcInventoryTransactionResult.ReservationConflict;
                        if(total>0)continued[entry.Key]=(int)total;
                    }
                    var preserved=new List<RebirthNpcNativeStackRecord>();var stackIds=new HashSet<Guid>();
                    if(state.NativeStacks!=null)foreach(var node in state.NativeStacks.Write().Elements())
                    {if(!RebirthNpcNativeStackRecord.TryRead(node,out var record)||!stackIds.Add(record.StackId))return RebirthNpcInventoryTransactionResult.InvalidRequest;preserved.Add(record);}
                    foreach(var item in leg.NativeCredits)
                    {if(!stackIds.Add(item.StackId)||!RebirthNpcNativeStackRecord.TryCapture(item.StackId,npcId,item.Stack,out var record)||!string.Equals(record.ItemKey,item.Key,StringComparison.OrdinalIgnoreCase))return RebirthNpcInventoryTransactionResult.InvalidRequest;preserved.Add(record);}
                    uint continuedRevision=state.Revision+1;RebirthNpcNativeStackSet continuedNative=null;
                    if((state.NativeStacks!=null||preserved.Count>0)&&(!RebirthNpcNativeStackSet.TryCreate(npcId,continuedRevision,preserved,out continuedNative)||!continuedNative.FitsQuantities(continued)))return RebirthNpcInventoryTransactionResult.ReservationConflict;
                    foreach(var reservation in state.Reservations)
                    {continued.TryGetValue(reservation.Key,out int total);if(reservation.Value<0||total<(long)reservation.Value+(continuedNative?.GetQuantity(reservation.Key)??0))return RebirthNpcInventoryTransactionResult.ReservationConflict;}
                    string continuedEffect=RebirthLogisticsNpcEffectReceipt.ExpectedLogistics(retained,leg,true,continuedRevision);
                    if(!ReferenceEquals(GameManager.Instance?.World,actor.world)||!ReferenceEquals(actor.world.GetEntity(actor.entityId),actor)||!(RebirthLogisticsDeathSettlement.ActorCurrent(actor,original)||RebirthLogisticsDeathSettlement.MatchesPaidSourceContinuation(actor,original))||
                        RebirthCamperCharacterAccess.CurrentCreation(actor)!=original.Creation||!ReferenceEquals(actor.world.GetEntity(entityId),npc)||npc.IsDead()||
                        !RebirthNpcOriginalEffectAdmission.IsReservedLogistics(retained,npcId)||!RebirthNpcInventoryAuthorityService.Validate(authorityKey,RebirthNpcInventoryAuthorityOperations.Transfer,npcId))return RebirthNpcInventoryTransactionResult.AuthorityDenied;
                    if(preflightOnly)return RebirthNpcInventoryTransactionResult.Applied;
                    generationEpoch=new object();mutationStarted=true;state.Quantities.Clear();foreach(var pair in continued)state.Quantities.Add(pair.Key,pair.Value);
                    state.NativeStacks=continuedNative;state.Revision=continuedRevision;RememberTransaction(state,transaction);state.LogisticsEffects.Add(transaction,continuedEffect);resultingRevision=continuedRevision;
                    Interlocked.Increment(ref applied);generationEpoch=new object();RebirthNpcInventoryPersistenceStore.MarkDirty();return RebirthNpcInventoryTransactionResult.Applied;
                }
                bool replay=false;
                if(state.Revision!=(replay?next:expected))return RebirthNpcInventoryTransactionResult.RevisionConflict;
                string expectedNative;
                if(replay)expectedNative=proposedNative?.Write().ToString(SaveOptions.DisableFormatting)??string.Empty;
                else expectedNative=beforeNative?.WithRevision(expected).Write().ToString(SaveOptions.DisableFormatting)??string.Empty;
                if((state.NativeStacks?.Write().ToString(SaveOptions.DisableFormatting)??string.Empty)!=expectedNative)return RebirthNpcInventoryTransactionResult.RevisionConflict;
                var known=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach(var entry in leg.Entries)
                {
                    known.Add(entry.Key);state.Quantities.TryGetValue(entry.Key,out int count);state.Reservations.TryGetValue(entry.Key,out int reserved);
                    int wanted=replay?(proposed.TryGetValue(entry.Key,out int post)?post:0):(credit?Math.Min(entry.Before,entry.After-entry.PendingNative):entry.Before);
                    if(count!=wanted||reserved!=entry.Reserved||RebirthCompanionInventoryLockService.IsNpcItemLocked(npcId,entry.Key)!=entry.Locked)return RebirthNpcInventoryTransactionResult.RevisionConflict;
                }
                if(state.Quantities.Keys.Any(key=>!known.Contains(key))||state.Reservations.Keys.Any(key=>!known.Contains(key)))return RebirthNpcInventoryTransactionResult.RevisionConflict;
                if(replay)return RebirthNpcInventoryTransactionResult.Replayed;
                if(!ReferenceEquals(GameManager.Instance?.World,actor.world)||!ReferenceEquals(actor.world.GetEntity(actor.entityId),actor)||!(RebirthLogisticsDeathSettlement.ActorCurrent(actor,original)||RebirthLogisticsDeathSettlement.MatchesPaidSourceContinuation(actor,original))||
                    RebirthCamperCharacterAccess.CurrentCreation(actor)!=original.Creation||!ReferenceEquals(actor.world.GetEntity(entityId),npc)||npc.IsDead()||
                    !RebirthNpcInventoryAuthorityService.Validate(authorityKey,RebirthNpcInventoryAuthorityOperations.Transfer,npcId))return RebirthNpcInventoryTransactionResult.AuthorityDenied;
                if(preflightOnly)return RebirthNpcInventoryTransactionResult.Applied;
                generationEpoch=new object();mutationStarted=true;
                generationEpoch=new object();state.Quantities.Clear();foreach(var pair in proposed)state.Quantities.Add(pair.Key,pair.Value);
                state.NativeStacks=proposedNative;state.Revision=next;RememberTransaction(state,transaction);resultingRevision=next;
                state.LogisticsEffects.Add(transaction,effect);
                Interlocked.Increment(ref applied);generationEpoch=new object();RebirthNpcInventoryPersistenceStore.MarkDirty();return RebirthNpcInventoryTransactionResult.Applied;
            }
        }
        catch(Exception error){if(mutationStarted)throw new InvalidOperationException("Original logistics NPC mutation outcome requires retained reconciliation.",error);return RebirthNpcInventoryTransactionResult.InvalidRequest;}
    }

    // Fetch uses its own retained original and existing Mutate lease. A DTO,
    // packet image, or remembered transaction GUID alone is never authority.
    private static bool RemoteOriginalCurrent(EntityPlayer actor,RebirthRemoteResourceNpcEffectPlan plan)
    {
        if(!ThreadManager.IsMainThread()||actor?.world?.IsRemote()!=false||actor.IsDead()||plan==null||plan.DogStorage||
            !ReferenceEquals(GameManager.Instance?.World,actor.world)||!ReferenceEquals(actor.world.GetEntity(actor.entityId),actor)||
            !RemoteResourcesRuntimePolicy.Enabled||!plan.IsCurrent()||!RebirthNpcRuntimeRegistry.TryGetEntityId(plan.NpcId,out int entityId)||
            !(actor.world.GetEntity(entityId) is EntityRebirthNPC npc)||npc.IsDead()||
            RebirthNpcInteractionSessionService.IsInventoryAccessing(plan.NpcId)||!RemoteNpcResourceSource.IsAuthorized(npc,actor,false,out _)||
            (npc.position-actor.position).sqrMagnitude>RemoteResourcesRuntimePolicy.Radius*RemoteResourcesRuntimePolicy.Radius)return false;
        return actor.Died==plan.Deaths&&RebirthCamperCharacterAccess.CurrentCreation(actor)==plan.Creation&&
            RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var identity)&&identity.StorageKey==plan.Owner;
    }
    private static bool RemoteOriginalBefore(InventoryState state,RebirthRemoteResourceNpcEffectPlan plan)
    {
        if(state.Revision!=plan.Revision||(state.NativeStacks?.Write().ToString(SaveOptions.DisableFormatting)??string.Empty)!=plan.BeforeNative||
            state.Quantities.Count!=plan.BeforeQuantities.Count||state.Reservations.Count!=plan.Reservations.Count)return false;
        foreach(var pair in plan.BeforeQuantities)
        {
            if(!state.Quantities.TryGetValue(pair.Key,out int before)||before!=pair.Value)return false;
            int after=plan.AfterQuantities.TryGetValue(pair.Key,out int count)?count:0;
            state.Reservations.TryGetValue(pair.Key,out int reserved);
            if(after>before||after<(long)reserved+(state.NativeStacks?.GetQuantity(pair.Key)??0)||
                after!=before&&RebirthCompanionInventoryLockService.IsNpcItemLocked(plan.NpcId,pair.Key))return false;
        }
        return plan.AfterQuantities.Keys.All(plan.BeforeQuantities.ContainsKey)&&
            plan.Reservations.All(pair=>state.Reservations.TryGetValue(pair.Key,out int count)&&count==pair.Value);
    }
    internal static bool CanAdmitRemoteResourceOriginalPlan(EntityPlayer actor,Guid operation,string sourceId)
    {
        try
        {
            RebirthNpcInventoryPersistenceStore.EnsureLoaded();
            if(!RebirthNpcInventoryPersistenceStore.IsTransferLoaded||
                !RebirthRemoteResourceDurableOutcome.TryReadNpcEffectPlan(actor,operation,sourceId,out var plan)||(RebirthNpcTradeService.BlocksOwnerPreparedNpc(plan.NpcId)||RebirthNpcMetabolismCustody.Blocks(plan.NpcId)||RemoteResourceTransactions.ActionBlocksNpc(plan.NpcId))||
                !RemoteOriginalCurrent(actor,plan)||!PruneSavedTerminalLogisticsEffects(plan.NpcId))return false;
            lock(Sync)
            {
                var state=GetOrCreate(plan.NpcId);
                string effect=RebirthLogisticsNpcEffectReceipt.ExpectedRemoteResource(plan);
                if(state.LogisticsEffects.TryGetValue(plan.MutationId,out var saved))
                    return saved==effect&&RebirthLogisticsNpcEffectReceipt.Validate(plan.NpcId,state.Revision,state.LogisticsEffects);
                if(state.AppliedTransactions.Contains(plan.MutationId)||!RemoteOriginalBefore(state,plan))return false;
            }
            return RebirthNpcOriginalEffectAdmission.TryReserveRemoteResource(actor,operation,sourceId,out var lease)&&lease.Current()&&plan.IsCurrent();
        }
        catch{return false;}
    }
    internal static RebirthNpcInventoryTransactionResult ApplyRemoteResourceOriginalPlan(EntityPlayer actor,Guid operation,string sourceId,string authorityKey,out uint resultingRevision)
    {
        resultingRevision=0;bool started=false;
        try
        {
            RebirthNpcInventoryPersistenceStore.EnsureLoaded();
            if(!RebirthNpcInventoryPersistenceStore.IsTransferLoaded||
                !RebirthRemoteResourceDurableOutcome.TryReadNpcEffectPlan(actor,operation,sourceId,out var plan)||(RebirthNpcTradeService.BlocksOwnerPreparedNpc(plan.NpcId)||RebirthNpcMetabolismCustody.Blocks(plan.NpcId)||RemoteResourceTransactions.ActionBlocksNpc(plan.NpcId))||plan.Attempted!=1||
                !RemoteOriginalCurrent(actor,plan)||!RebirthNpcOriginalEffectAdmission.IsReservedRemoteResource(plan))return RebirthNpcInventoryTransactionResult.InvalidRequest;
            if(!RebirthNpcInventoryAuthorityService.Validate(authorityKey,RebirthNpcInventoryAuthorityOperations.Mutate,plan.NpcId))return RebirthNpcInventoryTransactionResult.AuthorityDenied;
            RebirthNpcNativeStackSet afterNative=null;
            if(!string.IsNullOrEmpty(plan.AfterNative)&&!RebirthNpcNativeStackSet.TryRead(XElement.Parse(plan.AfterNative),out afterNative))return RebirthNpcInventoryTransactionResult.InvalidRequest;
            string effect=RebirthLogisticsNpcEffectReceipt.ExpectedRemoteResource(plan);
            lock(Sync)
            {
                var state=GetOrCreate(plan.NpcId);resultingRevision=state.Revision;
                if(state.LogisticsEffects.TryGetValue(plan.MutationId,out var saved))
                    return saved==effect&&RebirthLogisticsNpcEffectReceipt.Validate(plan.NpcId,state.Revision,state.LogisticsEffects)?RebirthNpcInventoryTransactionResult.Replayed:RebirthNpcInventoryTransactionResult.RevisionConflict;
                if(state.AppliedTransactions.Contains(plan.MutationId)||state.LogisticsEffects.Count>=RebirthLogisticsNpcEffectReceipt.MaximumReceipts||
                    !RemoteOriginalBefore(state,plan))return RebirthNpcInventoryTransactionResult.RevisionConflict;
                if(!RemoteOriginalCurrent(actor,plan)||!RebirthNpcOriginalEffectAdmission.IsReservedRemoteResource(plan)||
                    !RebirthNpcInventoryAuthorityService.Validate(authorityKey,RebirthNpcInventoryAuthorityOperations.Mutate,plan.NpcId))return RebirthNpcInventoryTransactionResult.AuthorityDenied;
                generationEpoch=new object();started=true;
                generationEpoch=new object();state.Quantities.Clear();foreach(var pair in plan.AfterQuantities)if(pair.Value>0)state.Quantities.Add(pair.Key,pair.Value);
                state.NativeStacks=afterNative;state.Revision=plan.Revision+1;RememberTransaction(state,plan.MutationId);
                state.LogisticsEffects.Add(plan.MutationId,effect);resultingRevision=state.Revision;
                Interlocked.Increment(ref applied);generationEpoch=new object();RebirthNpcInventoryPersistenceStore.MarkDirty();return RebirthNpcInventoryTransactionResult.Applied;
            }
        }
        catch(Exception error){if(started)throw new InvalidOperationException("Original fetch NPC effect requires retained reconciliation.",error);return RebirthNpcInventoryTransactionResult.InvalidRequest;}
    }

    private static RebirthNpcInventoryTransactionResult ApplyCore(RebirthNpcInventoryTransaction transaction,RebirthNpcInventoryAuthorityOperations purpose,out uint resultingRevision)
    {
        resultingRevision = 0;
        if(RebirthNpcPersistenceCoordinator.IsRecoveryInstalling)return RebirthNpcInventoryTransactionResult.AuthorityDenied;
        RebirthNpcInventoryPersistenceStore.EnsureLoaded();
        if (!Validate(transaction))
        {
            Interlocked.Increment(ref rejected);
            return RebirthNpcInventoryTransactionResult.InvalidRequest;
        }
        if(RebirthNpcTradeService.BlocksOwnerPreparedNpc(transaction.NpcId)||RebirthNpcMetabolismCustody.Blocks(transaction.NpcId)||RemoteResourceTransactions.ActionBlocksNpc(transaction.NpcId)&&!RemoteResourceTransactions.ActionAllowsNpc(transaction))return RebirthNpcInventoryTransactionResult.ReservationConflict;
        if (!RebirthNpcInventoryAuthorityService.Validate(transaction.AuthorityKey,
            purpose, transaction.NpcId))
        {
            Interlocked.Increment(ref rejected);
            return RebirthNpcInventoryTransactionResult.AuthorityDenied;
        }

        int entityId;
        RebirthNpcRuntimeState runtime;
        if (!RebirthNpcRuntimeRegistry.TryGetEntityId(transaction.NpcId, out entityId) ||
            !RebirthNpcRuntimeRegistry.TryGet(entityId, out runtime))
        {
            Interlocked.Increment(ref rejected);
            return RebirthNpcInventoryTransactionResult.NotFound;
        }
        RebirthNpcProfile profile;
        if (!RebirthNpcProfileRegistry.TryResolve(runtime.ProfileId, out profile) || !profile.Has(RebirthNpcCapabilities.Inventory))
        {
            Interlocked.Increment(ref rejected);
            return RebirthNpcInventoryTransactionResult.CapabilityDenied;
        }

        lock (Sync)
        {
            InventoryState state = GetOrCreate(transaction.NpcId);
            resultingRevision = state.Revision;
            if (state.AppliedTransactions.Contains(transaction.TransactionId))
            {
                Interlocked.Increment(ref replayed);
                return RebirthNpcInventoryTransactionResult.Replayed;
            }
            if (transaction.ExpectedRevision != state.Revision || state.Revision == uint.MaxValue)
            {
                Interlocked.Increment(ref rejected);
                return RebirthNpcInventoryTransactionResult.RevisionConflict;
            }

            Dictionary<string, int> proposed = new Dictionary<string, int>(state.Quantities, StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < transaction.Mutations.Count; i++)
            {
                RebirthNpcInventoryMutation mutation = transaction.Mutations[i];
                int current;
                proposed.TryGetValue(mutation.ItemKey, out current);
                long nextLong = (long)current + mutation.QuantityDelta;
                if (nextLong < 0 || nextLong > int.MaxValue)
                {
                    Interlocked.Increment(ref rejected);
                    return RebirthNpcInventoryTransactionResult.InsufficientQuantity;
                }
                int reserved;
                state.Reservations.TryGetValue(mutation.ItemKey, out reserved);
                if (nextLong < (long)reserved+(state.NativeStacks?.GetQuantity(mutation.ItemKey)??0))
                {
                    Interlocked.Increment(ref rejected);
                    return RebirthNpcInventoryTransactionResult.ReservationConflict;
                }
                proposed[mutation.ItemKey] = (int)nextLong;
            }

            if(state.NativeStacks!=null&&!state.NativeStacks.FitsQuantities(proposed))return RebirthNpcInventoryTransactionResult.ReservationConflict;
            generationEpoch=new object();state.Quantities.Clear();
            foreach (KeyValuePair<string, int> pair in proposed)
                if (pair.Value > 0) state.Quantities[pair.Key] = pair.Value;
            RememberTransaction(state, transaction.TransactionId);
            state.Revision++;
            if(state.NativeStacks!=null)state.NativeStacks=state.NativeStacks.WithRevision(state.Revision);
            resultingRevision = state.Revision;
            Interlocked.Increment(ref applied);
            generationEpoch=new object();RebirthNpcInventoryPersistenceStore.MarkDirty();
            return RebirthNpcInventoryTransactionResult.Applied;
        }
    }

    public static RebirthNpcInventoryTransferResult ApplyTransfer(RebirthNpcInventoryTransfer transfer,
        out uint sourceRevision, out uint destinationRevision)
    {
        sourceRevision = 0; destinationRevision = 0;
        if(RebirthNpcPersistenceCoordinator.IsRecoveryInstalling)return RebirthNpcInventoryTransferResult.AuthorityDenied;
        RebirthNpcInventoryPersistenceStore.EnsureLoaded();
        if (transfer == null || transfer.TransactionId == Guid.Empty || transfer.SourceNpcId.IsEmpty ||
            transfer.DestinationNpcId.IsEmpty || transfer.ItemKey.Length == 0 || transfer.Quantity <= 0 ||
            transfer.AuthorityKey.Length == 0)
            return RebirthNpcInventoryTransferResult.InvalidRequest;
        if (transfer.SourceNpcId == transfer.DestinationNpcId)
            return RebirthNpcInventoryTransferResult.SameInventory;
        if((RebirthNpcTradeService.BlocksOwnerPreparedNpc(transfer.SourceNpcId)||RebirthNpcMetabolismCustody.Blocks(transfer.SourceNpcId)||RemoteResourceTransactions.ActionBlocksNpc(transfer.SourceNpcId))||(RebirthNpcTradeService.BlocksOwnerPreparedNpc(transfer.DestinationNpcId)||RebirthNpcMetabolismCustody.Blocks(transfer.DestinationNpcId)||RemoteResourceTransactions.ActionBlocksNpc(transfer.DestinationNpcId)))return RebirthNpcInventoryTransferResult.ReservationConflict;
        if (!RebirthNpcInventoryAuthorityService.Validate(transfer.AuthorityKey,
            RebirthNpcInventoryAuthorityOperations.Transfer, transfer.SourceNpcId, transfer.DestinationNpcId))
            return RebirthNpcInventoryTransferResult.AuthorityDenied;
        if (!HasInventoryCapability(transfer.SourceNpcId) || !HasInventoryCapability(transfer.DestinationNpcId))
            return RebirthNpcInventoryTransferResult.CapabilityDenied;

        lock (Sync)
        {
            InventoryState source = GetOrCreate(transfer.SourceNpcId);
            InventoryState destination = GetOrCreate(transfer.DestinationNpcId);
            sourceRevision = source.Revision; destinationRevision = destination.Revision;
            if (source.AppliedTransactions.Contains(transfer.TransactionId) &&
                destination.AppliedTransactions.Contains(transfer.TransactionId))
                return RebirthNpcInventoryTransferResult.Replayed;
            if (source.Revision != transfer.ExpectedSourceRevision || destination.Revision != transfer.ExpectedDestinationRevision ||
                source.Revision == uint.MaxValue || destination.Revision == uint.MaxValue)
                return RebirthNpcInventoryTransferResult.RevisionConflict;
            int sourceQuantity; source.Quantities.TryGetValue(transfer.ItemKey, out sourceQuantity);
            int reserved; source.Reservations.TryGetValue(transfer.ItemKey, out reserved);
            if (sourceQuantity < transfer.Quantity) return RebirthNpcInventoryTransferResult.InsufficientQuantity;
            if ((long)sourceQuantity - transfer.Quantity < (long)reserved+(source.NativeStacks?.GetQuantity(transfer.ItemKey)??0)) return RebirthNpcInventoryTransferResult.ReservationConflict;
            int destinationQuantity; destination.Quantities.TryGetValue(transfer.ItemKey, out destinationQuantity);
            if ((long)destinationQuantity + transfer.Quantity > int.MaxValue)
                return RebirthNpcInventoryTransferResult.InvalidRequest;

            generationEpoch=new object();int remaining = sourceQuantity - transfer.Quantity;
            if(source.NativeStacks!=null&&remaining<source.NativeStacks.GetQuantity(transfer.ItemKey))return RebirthNpcInventoryTransferResult.ReservationConflict;
            if (remaining == 0) source.Quantities.Remove(transfer.ItemKey);
            else source.Quantities[transfer.ItemKey] = remaining;
            destination.Quantities[transfer.ItemKey] = destinationQuantity + transfer.Quantity;
            RememberTransaction(source, transfer.TransactionId);
            RememberTransaction(destination, transfer.TransactionId);
            source.Revision++; destination.Revision++;
            if(source.NativeStacks!=null)source.NativeStacks=source.NativeStacks.WithRevision(source.Revision);
            if(destination.NativeStacks!=null)destination.NativeStacks=destination.NativeStacks.WithRevision(destination.Revision);
            sourceRevision = source.Revision; destinationRevision = destination.Revision;
            Interlocked.Increment(ref applied);
            generationEpoch=new object();RebirthNpcInventoryPersistenceStore.MarkDirty();
            return RebirthNpcInventoryTransferResult.Applied;
        }
    }

    public static bool TryReserve(RebirthNpcStableId npcId, string itemKey, int quantity)
    {
        if((RebirthNpcTradeService.BlocksOwnerPreparedNpc(npcId)||RebirthNpcMetabolismCustody.Blocks(npcId)||RemoteResourceTransactions.ActionBlocksNpc(npcId)))return false;
        if(RebirthNpcPersistenceCoordinator.IsRecoveryInstalling)return false;
        RebirthNpcInventoryPersistenceStore.EnsureLoaded();
        itemKey = (itemKey ?? string.Empty).Trim();
        if (npcId.IsEmpty || itemKey.Length == 0 || quantity <= 0) return false;
        lock (Sync)
        {
            InventoryState state = GetOrCreate(npcId);
            int current;
            state.Quantities.TryGetValue(itemKey, out current);
            int reserved;
            state.Reservations.TryGetValue(itemKey, out reserved);
            if ((long)reserved + quantity > current-(state.NativeStacks?.GetQuantity(itemKey)??0)) return false;
            generationEpoch=new object();state.Reservations[itemKey] = reserved + quantity;
            generationEpoch=new object();RebirthNpcInventoryPersistenceStore.MarkDirty();
            return true;
        }
    }

    public static void ReleaseReservation(RebirthNpcStableId npcId, string itemKey, int quantity)
    {
        RebirthNpcInventoryPersistenceStore.EnsureLoaded();
        itemKey = (itemKey ?? string.Empty).Trim();
        if (npcId.IsEmpty || itemKey.Length == 0 || quantity <= 0) return;
        lock (Sync)
        {
            InventoryState state;
            if (!States.TryGetValue(npcId, out state)) return;
            int reserved;
            if (!state.Reservations.TryGetValue(itemKey, out reserved)) return;
            generationEpoch=new object();reserved -= quantity;
            if (reserved > 0) state.Reservations[itemKey] = reserved;
            else state.Reservations.Remove(itemKey);
            generationEpoch=new object();RebirthNpcInventoryPersistenceStore.MarkDirty();
        }
    }

    // Revision preflight does not need copies of quantities or reservations.
    public static uint GetRevision(RebirthNpcStableId npcId)
    {
        RebirthNpcInventoryPersistenceStore.EnsureLoaded();
        lock (Sync)
        {
            InventoryState state;
            return States.TryGetValue(npcId, out state) ? state.Revision : 0U;
        }
    }

    // Cached read only: checkpoint reconstruction must not create an inventory owner.
    internal static bool TryGetExistingNativeCustody(RebirthNpcStableId npcId, out RebirthNpcNativeStackSet custody, out uint revision)
    {
        lock (Sync)
        {
            custody=null; revision=0;
            if(!States.TryGetValue(npcId,out var state)) return false;
            custody=state.NativeStacks; revision=state.Revision; return true;
        }
    }
    public static RebirthNpcInventorySnapshot GetSnapshot(RebirthNpcStableId npcId)
    {
        RebirthNpcInventoryPersistenceStore.EnsureLoaded();
        lock (Sync)
        {
            InventoryState state = GetOrCreate(npcId);
            return new RebirthNpcInventorySnapshot
            {
                NativeStacks = state.NativeStacks,
                LogisticsEffects = new Dictionary<Guid,string>(state.LogisticsEffects),
                NpcId = npcId,
                Revision = state.Revision,
                Quantities = new Dictionary<string, int>(state.Quantities, StringComparer.OrdinalIgnoreCase),
                Reservations = new Dictionary<string, int>(state.Reservations, StringComparer.OrdinalIgnoreCase)
            };
        }
    }
    // Call before original owner/source effects. Preparation reserves/checks
    // both original phase receipts; a prepared journal is not an effect.
    internal static bool CanAdmitLogisticsOriginalEffects(RebirthNpcStableId id,Guid debit,Guid credit)
        =>CanAdmitOriginalEffectIds(id,new[]{debit,credit});
    internal static bool CanAdmitOriginalEffectIds(RebirthNpcStableId id,IReadOnlyList<Guid> effects)
    {
        if(!ThreadManager.IsMainThread()||GameManager.Instance?.World?.IsRemote()!=false||id.IsEmpty||effects==null||effects.Count<1||effects.Count>2||effects.Any(value=>value==Guid.Empty)||effects.Distinct().Count()!=effects.Count||!RebirthNpcInventoryPersistenceStore.IsTransferLoaded)return false;
        lock(Sync)
        {
            var state=GetOrCreate(id);int needed=effects.Count(value=>!state.LogisticsEffects.ContainsKey(value));
            return RebirthLogisticsNpcEffectReceipt.Validate(id,state.Revision,state.LogisticsEffects)&&state.LogisticsEffects.Count+needed<=RebirthLogisticsNpcEffectReceipt.MaximumReceipts;
        }
    }
    internal static bool PruneSavedTerminalLogisticsEffects(RebirthNpcStableId id)
    {
        if(!ThreadManager.IsMainThread()||GameManager.Instance?.World?.IsRemote()!=false||id.IsEmpty||!RebirthNpcInventoryPersistenceStore.IsTransferLoaded)return false;
        lock(Sync)
        {
            var state=GetOrCreate(id);if(!RebirthLogisticsNpcEffectReceipt.Validate(id,state.Revision,state.LogisticsEffects))return false;
            var expired=state.LogisticsEffects.Where(pair=>RebirthLogisticsNpcEffectReceipt.IsSavedTerminalLogistics(pair.Value)||
                RebirthRemoteResourceDurableOutcome.IsSavedTerminalEffectReceipt(pair.Value)).Select(pair=>pair.Key).ToArray();
            if(expired.Length>0){generationEpoch=new object();foreach(var key in expired)state.LogisticsEffects.Remove(key);RebirthNpcInventoryPersistenceStore.MarkDirty();}return true;
        }
    }


    public static RebirthNpcInventoryPersistentRecord[] CapturePersistentRecords()
    {
        lock (Sync)
        {
            RebirthNpcInventoryPersistentRecord[] records = new RebirthNpcInventoryPersistentRecord[States.Count];
            int index = 0;
            foreach (KeyValuePair<RebirthNpcStableId, InventoryState> pair in States)
            {
                records[index++] = new RebirthNpcInventoryPersistentRecord
                {
                    NativeStacks = pair.Value.NativeStacks,
                    LogisticsEffects = new Dictionary<Guid,string>(pair.Value.LogisticsEffects),
                    NpcId = pair.Key,
                    Revision = pair.Value.Revision,
                    Quantities = new Dictionary<string, int>(pair.Value.Quantities, StringComparer.OrdinalIgnoreCase),
                    Reservations = new Dictionary<string, int>(pair.Value.Reservations, StringComparer.OrdinalIgnoreCase),
                    ReplayJournal = pair.Value.AppliedTransactionOrder.ToArray()
                };
            }
            Array.Sort(records, (left, right) => string.CompareOrdinal(left.NpcId.ToString(), right.NpcId.ToString()));
            return records;
        }
    }

    public static bool ValidatePersistentRecord(RebirthNpcInventoryPersistentRecord record, out string error)
    {
        error = string.Empty;
        if (record == null || record.NpcId.IsEmpty)
        {
            error = "missing stable identity";
            return false;
        }
        if (record.Quantities == null || record.Reservations == null || record.ReplayJournal == null)
        {
            error = "missing inventory collections";
            return false;
        }
        if(record.NativeStacks!=null&&(record.NativeStacks.Owner!=record.NpcId.ToString()||
            record.NativeStacks.Revision!=record.Revision||!record.NativeStacks.FitsQuantities(record.Quantities)))
        {error="native stack ownership/revision/quantity mismatch";return false;}
        if(!RebirthLogisticsNpcEffectReceipt.Validate(record.NpcId,record.Revision,record.LogisticsEffects))
        {error="invalid original inventory effect receipt";return false;}
        foreach (KeyValuePair<string, int> pair in record.Quantities)
        {
            if (string.IsNullOrWhiteSpace(pair.Key) || pair.Value <= 0)
            {
                error = "invalid quantity entry";
                return false;
            }
        }
        foreach (KeyValuePair<string, int> pair in record.Reservations)
        {
            int quantity;
            if (string.IsNullOrWhiteSpace(pair.Key) || pair.Value <= 0 ||
                !record.Quantities.TryGetValue(pair.Key, out quantity) || pair.Value > quantity-(record.NativeStacks?.GetQuantity(pair.Key)??0))
            {
                error = "invalid reservation entry";
                return false;
            }
        }
        if (record.ReplayJournal.Length > MaxReplayTransactionsPerInventory)
        {
            error = "replay journal exceeds bound";
            return false;
        }
        HashSet<Guid> replayIds = new HashSet<Guid>();
        for (int i = 0; i < record.ReplayJournal.Length; i++)
            if (record.ReplayJournal[i] == Guid.Empty || !replayIds.Add(record.ReplayJournal[i]))
            {
                error = "invalid replay journal";
                return false;
            }

        return true;
    }

    public static bool TryRestorePersistentRecord(RebirthNpcInventoryPersistentRecord record, out string error)
    {
        if (!ValidatePersistentRecord(record, out error)) return false;
        lock (Sync)
        {
            InventoryState state = new InventoryState { Revision = record.Revision,NativeStacks=record.NativeStacks };
            if(record.NativeStacks!=null&&(record.NativeStacks.Owner!=record.NpcId.ToString()||
            record.NativeStacks.Revision!=record.Revision||!record.NativeStacks.FitsQuantities(record.Quantities)))
        {error="native stack ownership/revision/quantity mismatch";return false;}
        foreach (KeyValuePair<string, int> pair in record.Quantities) state.Quantities[pair.Key] = pair.Value;
            foreach (KeyValuePair<string, int> pair in record.Reservations) state.Reservations[pair.Key] = pair.Value;
            for (int i = 0; i < record.ReplayJournal.Length; i++) RememberTransaction(state, record.ReplayJournal[i]);
            foreach(var effect in record.LogisticsEffects??new Dictionary<Guid,string>())state.LogisticsEffects.Add(effect.Key,effect.Value);
            States[record.NpcId] = state;generationEpoch=new object();
        }
        return true;
    }

    internal sealed class PreparedGeneration
    {
        private Func<bool> commit;private readonly Func<bool> current;
        internal PreparedGeneration(Func<bool> commit,Func<bool> current){this.commit=commit;this.current=current;}
        internal bool IsCurrent=>commit!=null&&current();
        internal bool Commit(){var apply=Interlocked.Exchange(ref commit,null);return apply!=null&&apply();}
    }
    internal static bool TryPreparePersistentRecords(IEnumerable<RebirthNpcInventoryPersistentRecord> records,out PreparedGeneration generation,out string error)
    {
        generation=null;error=string.Empty;if(records==null){error="missing inventory generation";return false;}
        object expectedEpoch;lock(Sync)expectedEpoch=generationEpoch;var next=new Dictionary<RebirthNpcStableId,InventoryState>();
        foreach(var record in records)
        {
            if(!ValidatePersistentRecord(record,out error))return false;
            if(next.ContainsKey(record.NpcId)){error="duplicate inventory identity";return false;}
            RebirthNpcNativeStackSet native=null;
            if(record.NativeStacks!=null&&!RebirthNpcNativeStackSet.TryRead(record.NativeStacks.Write(),out native)){error="native custody copy failed";return false;}
            var state=new InventoryState{Revision=record.Revision,NativeStacks=native};
            foreach(var pair in record.Quantities)state.Quantities.Add(pair.Key,pair.Value);
            foreach(var pair in record.Reservations)state.Reservations.Add(pair.Key,pair.Value);
            foreach(var id in record.ReplayJournal){state.AppliedTransactions.Add(id);state.AppliedTransactionOrder.Enqueue(id);}
            foreach(var pair in record.LogisticsEffects??new Dictionary<Guid,string>())state.LogisticsEffects.Add(pair.Key,pair.Value);
            next.Add(record.NpcId,state);
        }
        generation=new PreparedGeneration(()=>{lock(Sync){if(!ReferenceEquals(expectedEpoch,generationEpoch))return false;States=next;generationEpoch=new object();return true;}},()=>{lock(Sync)return ReferenceEquals(expectedEpoch,generationEpoch);});return true;
    }
    internal static bool TryReplacePersistentRecords(IEnumerable<RebirthNpcInventoryPersistentRecord> records,out string error)
    {if(!TryPreparePersistentRecords(records,out var candidate,out error))return false;if(candidate.Commit())return true;error="inventory generation changed during preparation";return false;}
    public static void ClearRuntimeState()
    {
        lock (Sync){States.Clear();generationEpoch=new object();}
    }

    public static string GetReport()
    {
        lock (Sync)
        {
            int itemKinds = 0;
            int reservations = 0;
            foreach (InventoryState state in States.Values)
            {
                itemKinds += state.Quantities.Count;
                reservations += state.Reservations.Count;
            }
            return "[REBIRTH NPC Inventory] inventories=" + States.Count
                + " itemKinds=" + itemKinds
                + " reservations=" + reservations
                + " applied=" + applied
                + " replayed=" + replayed
                + " rejected=" + rejected;
        }
    }



    private static bool HasInventoryCapability(RebirthNpcStableId npcId)
    {
        int entityId; RebirthNpcRuntimeState runtime; RebirthNpcProfile profile;
        return RebirthNpcRuntimeRegistry.TryGetEntityId(npcId, out entityId) &&
            RebirthNpcRuntimeRegistry.TryGet(entityId, out runtime) &&
            RebirthNpcProfileRegistry.TryResolve(runtime.ProfileId, out profile) &&
            profile.Has(RebirthNpcCapabilities.Inventory);
    }

    private static void RememberTransaction(InventoryState state, Guid transactionId)
    {
        if (!state.AppliedTransactions.Add(transactionId)) return;
        state.AppliedTransactionOrder.Enqueue(transactionId);
        while (state.AppliedTransactionOrder.Count > MaxReplayTransactionsPerInventory)
        {
            Guid expired = state.AppliedTransactionOrder.Dequeue();
            state.AppliedTransactions.Remove(expired);
        }
    }

    private static InventoryState GetOrCreate(RebirthNpcStableId npcId)
    {
        InventoryState state;
        if (!States.TryGetValue(npcId, out state))
        {
            state = new InventoryState();
            States.Add(npcId, state);generationEpoch=new object();
        }
        return state;
    }

    private static bool Validate(RebirthNpcInventoryTransaction transaction)
    {
        if (transaction == null || transaction.TransactionId == Guid.Empty || transaction.NpcId.IsEmpty ||
            string.IsNullOrWhiteSpace(transaction.AuthorityKey) || transaction.Mutations == null || transaction.Mutations.Count == 0)
            return false;
        HashSet<string> keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < transaction.Mutations.Count; i++)
        {
            RebirthNpcInventoryMutation mutation = transaction.Mutations[i];
            if (mutation == null || string.IsNullOrWhiteSpace(mutation.ItemKey) || mutation.QuantityDelta == 0 || !keys.Add(mutation.ItemKey))
                return false;
        }
        return true;
    }
}

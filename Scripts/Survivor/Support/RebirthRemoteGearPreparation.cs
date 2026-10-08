using System;

// Preparation only. A caller must not send or apply this offer unless this method
// succeeds. Owner receipts, gear commit and recovery reconciliation remain separate.
public static class RebirthRemoteGearPreparation
{
    public static bool TryPrepare(EntityPlayer player, ClientInfo sender, string creation,
        int type, ushort seed, long revision, out RebirthGearTransferState offer)
        => TryPrepare(player, sender, creation, type, seed, revision, Guid.NewGuid(), out offer);

    // Original intent identity survives final-file preparation. Pending transactions
    // are reconciled via TryReplay, never rebuilt as another inventory proposal.
    public static bool TryPrepare(EntityPlayer player, ClientInfo sender, string creation,
        int type, ushort seed, long revision, Guid originalTransaction, out RebirthGearTransferState offer)
        => TryPrepareCore(player,sender,creation,type,seed,revision,originalTransaction,null,null,null,out offer);

    public static bool TryPrepare(EntityPlayer player,ClientInfo sender,RebirthGearPreparationIntent intent,out RebirthGearTransferState offer)
    {
        offer=null;
        return intent!=null && TryPrepareCore(player,sender,intent.CreationId,intent.ItemType,intent.ItemSeed,
            intent.ExpectedRevision,intent.TransactionId,intent,null,null,out offer);
    }
    // Native owner upload must already be saved on the server. The marker is
    // data only until bound to this exact current authenticated saved world/owner.
    public static bool TryPrepareBound(EntityPlayer player,ClientInfo sender,string originalMarker,out RebirthGearTransferState offer)
    {
        offer=null;
        try
        {
            if(player==null||sender==null||!ThreadManager.IsMainThread()||!RebirthGearPreparationMarker.TryRead(originalMarker,1f,out var savedWorld,out _,out var intent))return false;
            var game=GameManager.Instance;var world=game?.World;var state=world?.worldState;
            var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;
            if(world==null||world.IsRemote()||state==null||!Guid.TryParse(state.Guid,out var nativeWorld)||nativeWorld!=savedWorld||
                manager==null||!manager.IsServer||manager.Clients==null||!ReferenceEquals(manager.Clients.ForEntityId(player.entityId),sender)||
                !RebirthRemoteGearInventorySource.TryResolve(player,sender,intent.CreationId,out var record)||
                !RebirthWorldCharacterService.TryGetIdentity(player,out var identity)||identity==null||
                !RebirthStablePlayerIdentity.TryFromClientInfo(sender,out var senderIdentity)||
                identity.StorageKey!=senderIdentity.StorageKey||identity.CanonicalId!=senderIdentity.CanonicalId)return false;
            string owner=identity.CanonicalId,key=identity.StorageKey,native=state.Guid;
            Func<bool> originalCurrent=()=>ThreadManager.IsMainThread()&&ReferenceEquals(game,GameManager.Instance)&&
                ReferenceEquals(game.World,world)&&!world.IsRemote()&&ReferenceEquals(world.worldState,state)&&state.Guid==native&&
                ReferenceEquals(manager,SingletonMonoBehaviour<ConnectionManager>.Instance)&&manager.IsServer&&manager.Clients!=null&&
                ReferenceEquals(manager.Clients.ForEntityId(player.entityId),sender)&&
                RebirthRemoteGearInventorySource.TryResolve(player,sender,intent.CreationId,out var live)&&ReferenceEquals(live,record)&&
                RebirthWorldCharacterRepository.IsCurrentCachedRecord(record)&&
                RebirthWorldCharacterService.TryGetIdentity(player,out var currentIdentity)&&currentIdentity!=null&&currentIdentity.CanonicalId==owner&&
                currentIdentity.StorageKey==key&&RebirthGearPreparationPlayerFileWitness.HasOriginal(identity,savedWorld,originalMarker,intent);
            if(!originalCurrent())return false;
            string digest;
            using(var hash=System.Security.Cryptography.SHA256.Create())
                digest=BitConverter.ToString(hash.ComputeHash(System.Text.Encoding.UTF8.GetBytes(originalMarker))).Replace("-",string.Empty).ToLowerInvariant();
            return TryPrepareCore(player,sender,intent.CreationId,intent.ItemType,intent.ItemSeed,intent.ExpectedRevision,
                intent.TransactionId,intent,digest,originalCurrent,out offer);
        }
        catch {return false;} // Any saved original pending intent remains retained.
    }
    private static bool TryPrepareCore(EntityPlayer player,ClientInfo sender,string creation,int type,ushort seed,
        long revision,Guid originalTransaction,RebirthGearPreparationIntent intent,string requestDigest,Func<bool> originalCurrent,out RebirthGearTransferState offer)
    {
        offer = null;
        if(originalCurrent!=null&&!originalCurrent())return false;
        RebirthGearTransferState candidate;
        bool built=intent==null
            ? RebirthRemoteGearEquipPlan.TryBuild(player,sender,creation,type,seed,revision,originalTransaction,out candidate)
            : RebirthRemoteGearEquipPlan.TryBuild(player,sender,intent,out candidate);
        if(built&&requestDigest!=null){if(!candidate.TryBindPreparationRequest(requestDigest,out var bound))return false;candidate=bound;}
        if (!built ||
            !RebirthWorldCharacterService.TryGet(player, out var record) ||
            !RebirthWorldCharacterService.TryGetIdentity(player, out var identity) ||
            identity == null || record == null || identity.StorageKey != record.StablePlayerKey ||
            !RebirthWorldCharacterRepository.IsCurrentCachedRecord(record) ||
            !candidate.TryGetPlan(out var plan)) return false;

        string ownerKey = identity.StorageKey;
        bool saved = RebirthGearTransferJournal.Prepare(record.Support, candidate, creation, () =>
        {
            // Recheck the live binding and native per-connection preimage immediately
            // before saving; never substitute the remote entity's inventory mirror.
            if (!Current(player, sender, creation, record, ownerKey, candidate, plan, intent, originalCurrent)) return false;
            RebirthWorldCharacterService.MarkDirty(record, "remote-gear-preparation");
            try { RebirthWorldCharacterRepository.SaveIfDirty(identity, "remote-gear-preparation"); }
            catch { /* An uncertain write is resolved by reading the final file below. */ }
            return RebirthWorldCharacterRepository.HasSavedGearTransfer(identity, candidate,
                RebirthGearTransferPhase.Prepared) &&
                Current(player, sender, creation, record, ownerKey, candidate, plan, intent, originalCurrent);
        });
        if (!saved) return false;
        offer = candidate;
        return true;
    }

    // Replay only the retained prepared transaction. Inventory may already contain
    // its postimage; the owner's durable receipt must decide application/rejection.
    // This does not authorize a new plan or resend a later custody stage.
    public static bool TryReplay(EntityPlayer player,ClientInfo sender,string creation,out RebirthGearTransferState offer)
        =>TryReplayAtPhase(player,sender,creation,RebirthGearTransferPhase.Prepared,out offer,out _);

    private static bool TryReplayAtPhase(EntityPlayer player,ClientInfo sender,string creation,
        RebirthGearTransferPhase? required,out RebirthGearTransferState offer,out RebirthGearTransferPhase phase)
    {
        offer=null;phase=RebirthGearTransferPhase.Prepared;
        if(!RebirthRemoteGearInventorySource.TryResolve(player,sender,creation,out var record)||record?.Support==null||
            !RebirthWorldCharacterRepository.IsCurrentCachedRecord(record)||
            !RebirthWorldCharacterService.TryGetIdentity(player,out var identity)||identity==null||
            identity.StorageKey!=record.StablePlayerKey)return false;
        var retainedPhase=record.Support.GearTransferPhase;
        if((required.HasValue&&retainedPhase!=required.Value)||
            (retainedPhase!=RebirthGearTransferPhase.Prepared&&retainedPhase!=RebirthGearTransferPhase.OwnerApplied&&
             retainedPhase!=RebirthGearTransferPhase.GearCommitted))return false;
        string owner=identity.StorageKey;var candidate=record.Support.PendingGearTransfer;
        if(candidate==null||!RebirthWorldCharacterRepository.HasSavedGearTransfer(identity,candidate,retainedPhase)||
            !RebirthRemoteGearInventorySource.TryResolve(player,sender,creation,out var current)||!ReferenceEquals(record,current)||
            !RebirthWorldCharacterRepository.IsCurrentCachedRecord(record)||
            !RebirthWorldCharacterService.TryGetIdentity(player,out var currentIdentity)||currentIdentity==null||
            currentIdentity.StorageKey!=owner||record.StablePlayerKey!=owner||
            !ReferenceEquals(record.Support.PendingGearTransfer,candidate)||
            !RebirthGearTransferSavedWitness.Matches(record.Support,record.Origin?.CreationId,candidate,retainedPhase)||
            !RebirthGearTransferState.TryRead(candidate.ToXml(),out var copy))return false;
        offer=copy;phase=retainedPhase;return true;
    }
    // Replay authenticates the retained original request, not the current inventory:
    // an owner may already have applied it before a response was lost.
    public static bool TryReplayBound(EntityPlayer player,ClientInfo sender,string originalMarker,out RebirthGearTransferState offer)
        =>TryReplayBoundCore(player,sender,originalMarker,true,out offer,out _);

    // Retained phase lookup only. Does not confirm owner receipt, advance custody,
    // publish recovery or authorize an inventory write.
    public static bool TryReplayBoundRetained(EntityPlayer player,ClientInfo sender,string originalMarker,
        out RebirthGearTransferState offer,out RebirthGearTransferPhase phase)
        =>TryReplayBoundCore(player,sender,originalMarker,false,out offer,out phase);

    private static bool TryReplayBoundCore(EntityPlayer player,ClientInfo sender,string originalMarker,bool preparedOnly,
        out RebirthGearTransferState offer,out RebirthGearTransferPhase phase)
    {
        offer=null;phase=RebirthGearTransferPhase.Prepared;
        try
        {
            if(player==null||sender==null||!ThreadManager.IsMainThread()||
                !RebirthGearPreparationMarker.TryRead(originalMarker,1f,out var savedWorld,out _,out var intent))return false;
            var game=GameManager.Instance;var world=game?.World;var state=world?.worldState;
            var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;
            if(world==null||state==null||manager==null)return false;
            Func<bool> current=()=>ThreadManager.IsMainThread()&&ReferenceEquals(game,GameManager.Instance)&&
                ReferenceEquals(game.World,world)&&!world.IsRemote()&&ReferenceEquals(world.worldState,state)&&
                Guid.TryParse(state.Guid,out var guid)&&guid==savedWorld&&
                ReferenceEquals(manager,SingletonMonoBehaviour<ConnectionManager>.Instance)&&manager.IsServer&&manager.Clients!=null&&
                ReferenceEquals(manager.Clients.ForEntityId(player.entityId),sender)&&
                RebirthWorldCharacterService.TryGetIdentity(player,out var owner)&&owner!=null&&
                RebirthStablePlayerIdentity.TryFromClientInfo(sender,out var authenticated)&&authenticated!=null&&
                owner.CanonicalId==authenticated.CanonicalId&&owner.StorageKey==authenticated.StorageKey;
            if(!current()||!RebirthRemoteGearInventorySource.TryResolve(player,sender,intent.CreationId,out var originalRecord)||
                !TryReplayAtPhase(player,sender,intent.CreationId,preparedOnly?(RebirthGearTransferPhase?)RebirthGearTransferPhase.Prepared:null,out var candidate,out var retainedPhase)||
                candidate.TransactionId!=intent.TransactionId.ToString("N")||candidate.ExpectedRevision!=intent.ExpectedRevision)return false;
            string digest;
            using(var hash=System.Security.Cryptography.SHA256.Create())
                digest=BitConverter.ToString(hash.ComputeHash(System.Text.Encoding.UTF8.GetBytes(originalMarker))).Replace("-",string.Empty).ToLowerInvariant();
            if(candidate.PreparationRequestDigest!=digest||!current()||
                !RebirthRemoteGearInventorySource.TryResolve(player,sender,intent.CreationId,out var finalRecord)||
                !ReferenceEquals(finalRecord,originalRecord)||!RebirthWorldCharacterRepository.IsCurrentCachedRecord(finalRecord)||
                !RebirthGearTransferSavedWitness.Matches(finalRecord.Support,finalRecord.Origin?.CreationId,candidate,retainedPhase))return false;
            offer=candidate;phase=retainedPhase;return true;
        }
        catch {return false;}
    }
    private static bool Current(EntityPlayer player, ClientInfo sender, string creation,
        RebirthWorldCharacterRecord expected, string key, RebirthGearTransferState candidate, RebirthGearInventoryPlan plan, RebirthGearPreparationIntent intent, Func<bool> originalCurrent)
    {
        return expected != null && expected.StablePlayerKey == key &&
            RebirthWorldCharacterRepository.IsCurrentCachedRecord(expected) &&
            RebirthWorldCharacterService.TryGet(player, out var current) &&
            ReferenceEquals(current, expected) &&
            RebirthWorldCharacterService.TryGetIdentity(player, out var identity) &&
            identity != null && identity.StorageKey == key &&
            RebirthRemoteGearInventorySource.TryCapture(player, sender, creation, out var snapshot) &&
            ReferenceEquals(expected.Support?.PendingGearTransfer, candidate) &&
            expected.Support.GearTransferPhase == RebirthGearTransferPhase.Prepared &&
            RebirthGearTransferJournal.Matches(expected.Support, candidate, creation) &&
            plan.MatchesBefore(snapshot.Bag, snapshot.Belt, plan.GearBefore,
                snapshot.Bag.Length, snapshot.Belt.Length) && (intent==null || intent.MatchesInventory(snapshot)) && (originalCurrent==null || originalCurrent());
    }
}
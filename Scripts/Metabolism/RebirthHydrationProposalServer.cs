using System;
using System.Collections.Generic;

// Builds a description from native per-peer upload, never EntityPlayer's bag
// mirror. Source custody is separate from the later owner's saved grant.
internal static partial class RebirthHydrationProposalServer
{
    internal sealed class Proposal
    {
        internal readonly EntityPlayer Player;internal readonly ClientInfo Peer;
        internal readonly RebirthHydrationTransferOriginal Original;
        internal readonly RebirthRemoteResourceFragments.Body Body;
        private readonly bool sourceIsBag,returning;private readonly int sourceIndex;
        private RebirthHydrationServerSourceHold.Lease lease;
        internal RebirthHydrationServerSourceHold.Lease Lease=>lease;
        private Proposal(EntityPlayer player,ClientInfo peer,RebirthHydrationTransferOriginal original,bool bag,int index,bool returning,
            RebirthRemoteResourceFragments.Body body)
        {Player=player;Peer=peer;Original=original;sourceIsBag=bag;sourceIndex=index;this.returning=returning;Body=body;}
        internal bool Current()=>Scope(Player,Peer,Original.Operation,out var world,out var owner,out var creation)&&
            Original.Matches(world,owner,creation,Player.Died)&&(lease==null||lease.Current());
        internal static bool TryPrepare(EntityPlayer player,ClientInfo peer,Guid operation,bool sourceIsBag,int sourceIndex,bool returning,bool[] lockedBag,out Proposal proposal)
        {
            proposal=null;
            try
            {
                if(!Scope(player,peer,operation,out var world,out var owner,out var creation)||
                    (returning?sourceIndex!=-1||!sourceIsBag:sourceIndex<0))return false;
                if(proposals.TryGetValue(operation,out var retained))
                {
                    if(!retained.Current())
                    {
                        if(!RebirthHydrationTransferJournal.TryReadPending(player,out var saved,out _)||saved?.Digest!=retained.Original.Digest||saved.Operation!=operation||
                            !TryReenterProposal(player,peer,saved,sourceIsBag,sourceIndex,returning,lockedBag,out proposal))return false;
                        proposals[operation]=proposal;return true;
                    }
                    if(!ReferenceEquals(retained.Player,player)||!ReferenceEquals(retained.Peer,peer)||retained.sourceIsBag!=sourceIsBag||
                        retained.sourceIndex!=sourceIndex||retained.returning!=returning||!LocksMatch(lockedBag,retained.Original.BagLocks)||!retained.Current())return false;
                    if(retained.lease==null&&!RebirthHydrationServerSourceHold.Lease.TryAcquire(player,retained.Original,out retained.lease))return false;
                    if(!retained.Current())return false;proposal=retained;return true;
                }
                if(proposals.Count>=64||!RebirthHydrationTransferJournal.TryReadPending(player,out var cold,out _))return false;
                if(cold!=null)
                {
                    if(cold.Operation==operation)
                    {
                        if(!TryReenterProposal(player,peer,cold,sourceIsBag,sourceIndex,returning,lockedBag,out proposal))return false;
                        proposals.Add(operation,proposal);return true;
                    }
                    // A request lost before the client received its original
                    // never entered owner grant publication. Positive Prepared
                    // WAL + saved untouched source may retire that description.
                    if(!RebirthHydrationTransferJournal.HasPrepared(player,cold)||
                        !RebirthHydrationServerSourceHold.Lease.TryReenter(player,cold,out var untouched,true)||
                        !RebirthHydrationTransferJournal.PreparedRejection.TryPublish(untouched,out var rejection)||
                        !RebirthHydrationServerSourceHold.TryReleasePreparedRejection(rejection))return false;
                    if(proposals.TryGetValue(cold.Operation,out var old)&&old.Original.Digest==cold.Digest)proposals.Remove(cold.Operation);
                }
                if(
                    !RebirthMetabolismStateRepository.TryGet(player,out var state)||
                    !RebirthHydrationTransferScope.MatchesState(creation,state))return false;
                var uploaded=peer.latestPlayerData;
                if(uploaded==null||uploaded.deaths!=player.Died)return false;
                var bag=RebirthGearPlayerDataInventory.ReadSlots(uploaded,true);var belt=RebirthGearPlayerDataInventory.ReadSlots(uploaded,false);
                if(bag==null||belt==null)return false;
                int owned=RebirthToolbeltCapacity.GetOwnedSlotCount(player,belt.Length);
                if(!RebirthGearInventorySnapshot.TryCapture(bag,belt,owned,out var snapshot))return false;
                var item=state.HydrationSlotItem;int count=item==null||item.IsEmpty()?0:item.count;
                var hydration=new RebirthGearInventoryPlan.Stack{Count=count,ItemData=count==0?string.Empty:RebirthNativeItemCodec.Encode(item.itemValue)};
                if(!RebirthHydrationTransferPlan.TryCreate(snapshot.Bag,snapshot.Belt,lockedBag,owned,hydration,sourceIsBag,returning?-1:sourceIndex,out var plan)||
                    !RebirthHydrationTransferOriginal.TryCreate(operation,world,owner,creation,player.Died,state.Revision,owned,lockedBag,plan,out var original))return false;
                var body=RebirthHydrationOriginalTransport.Create(original);
                if(!ReferenceEquals(peer.latestPlayerData,uploaded)||!Scope(player,peer,operation,out var finalWorld,out var finalOwner,out var finalCreation)||
                    !original.Matches(finalWorld,finalOwner,finalCreation,player.Died)||
                    !RebirthMetabolismStateRepository.TryGet(player,out var latest)||!ReferenceEquals(latest,state)||latest.Revision!=original.Revision)return false;
                var next=new Proposal(player,peer,original,sourceIsBag,sourceIndex,returning,body);proposals.Add(operation,next);
                if(!RebirthHydrationServerSourceHold.Lease.TryAcquire(player,original,out next.lease)||!next.Current())return false;
                proposal=next;return true;
            }
            catch{return false;}
        }
        private static bool TryReenterProposal(EntityPlayer player,ClientInfo peer,RebirthHydrationTransferOriginal original,
            bool sourceIsBag,int sourceIndex,bool returning,bool[] lockedBag,out Proposal proposal)
        {
            proposal=null;
            if(!Scope(player,peer,original.Operation,out var world,out var owner,out var creation)||
                !original.Matches(world,owner,creation,player.Died)||original.Equip==returning||!LocksMatch(lockedBag,original.BagLocks)||
                !original.TryGetPlan(out var plan))return false;
            var change=plan.Changes[0];
            if(!returning&&(change.IsBag!=sourceIsBag||change.Index!=sourceIndex))return false;
            if(!RebirthHydrationServerSourceHold.Lease.TryReenter(player,original,out var lease))return false;
            var next=new Proposal(player,peer,original,sourceIsBag,sourceIndex,returning,RebirthHydrationOriginalTransport.Create(original)){lease=lease};
            if(!next.Current()||!RebirthHydrationServerSourceHold.Lease.SavedReentry(lease))return false;
            proposal=next;return true;
        }
    }
    private static readonly Dictionary<Guid,Proposal> proposals=new Dictionary<Guid,Proposal>();
    internal static bool TryGet(EntityPlayer player,ClientInfo peer,Guid operation,string digest,out Proposal proposal)
    {
        proposal=null;
        if(player==null||peer==null||!proposals.TryGetValue(operation,out var retained)||!ReferenceEquals(retained.Player,player)||
            !ReferenceEquals(retained.Peer,peer)||retained.Original.Digest!=digest||retained.Lease==null||!retained.Current()||!retained.Lease.Current())return false;
        proposal=retained;return true;
    }
    private static bool LocksMatch(bool[] requested,string saved)
    {if(requested!=null&&requested.Length!=saved.Length)return false;for(int i=0;i<saved.Length;i++)if((requested!=null&&requested[i])!=(saved[i]=='1'))return false;return true;}
    private static bool Scope(EntityPlayer player,ClientInfo peer,Guid operation,out Guid world,out string owner,out string creation)
    {
        world=Guid.Empty;owner=creation=null;var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;
        if(!ThreadManager.IsMainThread()||operation==Guid.Empty||player is EntityPlayerLocal||player?.world==null||player.world.IsRemote()||
            manager?.IsServer!=true||!ReferenceEquals(player.world,GameManager.Instance?.World)||!ReferenceEquals(player.world.GetEntity(player.entityId),player)||
            player.IsDead()||!player.IsSpawned()||peer==null||!peer.loginDone||peer.disconnecting||!ReferenceEquals(manager.Clients?.ForEntityId(player.entityId),peer)||
            !Guid.TryParse(player.world.Guid,out world)||world==Guid.Empty||!RebirthHydrationTransferScope.TryCapture(player,out creation)||
            !RebirthStablePlayerIdentity.TryFromClientInfo(peer,out var identity)||!RebirthStablePlayerIdentity.TryResolveServerEntity(player,out var canonical)||identity.StorageKey!=canonical.StorageKey)return false;
        owner=identity.StorageKey;return true;
    }
}

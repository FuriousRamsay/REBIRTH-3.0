using System;
using System.Collections.Generic;

// Warm user intent only. No native inventory bypass is acquired until the full
// authenticated server original matches this intent and the actual owner grid.
internal static partial class RebirthHydrationProposalClient
{
    internal sealed partial class Intent
    {
        internal readonly EntityPlayerLocal Player;internal readonly Guid Operation;
        internal readonly bool SourceIsBag,Returning;internal readonly int SourceIndex;
        private readonly World world;private readonly GameManager game;private readonly ConnectionManager manager;private readonly object peer;
        private readonly string creation,owner,locks;private readonly int deaths,owned;
        private readonly RebirthGearInventoryPlan.Stack source;
        private RebirthHydrationTransferOriginal original;private bool receiving,awaitingReentry,originalSaved;
        private Intent previous;
        internal RebirthHydrationTransferOriginal Original=>original;
        internal string Creation=>creation;
        internal int Deaths=>deaths;
        internal string LockBitmap=>locks;
        private Intent(EntityPlayerLocal player,bool bag,int index,bool returning,RebirthGearInventorySnapshot snapshot,int owned,string creation,string owner)
        {
            Player=player;Operation=Guid.NewGuid();SourceIsBag=bag;SourceIndex=index;Returning=returning;
            world=player.world;game=GameManager.Instance;manager=SingletonMonoBehaviour<ConnectionManager>.Instance;peer=manager.connectionToServer[0];
            this.creation=creation;this.owner=owner;deaths=player.Died;this.owned=owned;
            var bits=new char[snapshot.Bag.Length];var native=player.bag.LockedSlots;
            for(int i=0;i<bits.Length;i++)bits[i]=native!=null&&i<native.Length&&native[i]?'1':'0';locks=new string(bits);
            if(!returning){var cell=(bag?snapshot.Bag:snapshot.Belt)[index];source=new RebirthGearInventoryPlan.Stack{Count=cell.Count,ItemData=cell.ItemData};}
        }
        private Intent(EntityPlayerLocal player,RebirthHydrationTransferOriginal saved,RebirthGearInventorySnapshot snapshot,int owned,string creation,string owner)
            :this(player,saved.Equip&&saved.TryGetPlan(out var plan)?plan.Changes[0].IsBag:true,
                saved.Equip&&saved.TryGetPlan(out var sourcePlan)?sourcePlan.Changes[0].Index:-1,!saved.Equip,snapshot,owned,creation,owner)
        {
            Operation=saved.Operation;original=saved;originalSaved=true;awaitingReentry=true;
            if(saved.Equip&&saved.TryGetPlan(out var before))source=new RebirthGearInventoryPlan.Stack{
                Count=before.Changes[0].Before.Count,ItemData=before.Changes[0].Before.ItemData};
        }
        internal bool Current()=>Resolve(Player,out var currentPeer,out var currentCreation,out var currentOwner)&&
            ReferenceEquals(world,Player.world)&&ReferenceEquals(game,GameManager.Instance)&&ReferenceEquals(manager,SingletonMonoBehaviour<ConnectionManager>.Instance)&&
            ReferenceEquals(peer,currentPeer)&&creation==currentCreation&&owner==currentOwner&&deaths==Player.Died;
        internal static bool TryBegin(EntityPlayerLocal player,bool sourceIsBag,int sourceIndex,bool returning,out Intent intent)
        {
            intent=null;
            try
            {
                if(!Resolve(player,out _,out var creation,out var owner))return false;
                if(intents.TryGetValue(player,out var retained)&&retained.Current())
                {intent=retained;return true;}
                if(!RebirthHydrationClientOriginals.TryRead(player,out var cold))return false;
                if(cold!=null)return TryRecover(player,cold,out intent);
                if(intents.Count>=64||(returning?sourceIndex!=-1||!sourceIsBag:sourceIndex<0)||
                    RebirthHydrationOwnerHold.HoldsMetabolismOwner(player)||!RebirthRemoteResourceOwnerAction.CanPrepareNativeOriginal(player))return false;
                var bag=player.bag?.ItemGrid?.items;var belt=player.inventory?.ItemGrid?.items;if(bag==null||belt==null)return false;
                int owned=RebirthToolbeltCapacity.GetOwnedSlotCount(player,belt.Length);
                if(!RebirthGearInventorySnapshot.TryCapture(bag,belt,owned,out var snapshot))return false;
                if(!returning)
                {
                    if(sourceIndex>=(sourceIsBag?snapshot.Bag.Length:owned))return false;
                    var cell=(sourceIsBag?snapshot.Bag:snapshot.Belt)[sourceIndex];
                    if(cell.Count<1||!RebirthNativeItemCodec.TryDecode(cell.ItemData,out var item)||
                        !RebirthConsumableResolver.TryResolve(item,out var definition)||definition==null||!definition.IsDrink||!definition.HydrationEquippable)return false;
                    if(sourceIsBag&&player.bag.LockedSlots!=null&&sourceIndex<player.bag.LockedSlots.Length&&player.bag.LockedSlots[sourceIndex])return false;
                }
                var next=new Intent(player,sourceIsBag,sourceIndex,returning,snapshot,owned,creation,owner);
                if(!next.Current())return false;
                // An intro never delivered a complete original and acquired no
                // native custody. Preserve its description while replacing only
                // the revoked request; host Prepared rejection owns its close.
                if(retained!=null)
                {
                    if(retained.Current()||retained.Original!=null)return false;
                    int count=0;for(var cursor=retained;cursor!=null;cursor=cursor.previous)if(++count>=64)return false;
                    next.previous=retained;
                }
                intents[player]=next;intent=next;return true;
            }
            catch{return false;}
        }
        internal bool Receive(RebirthHydrationTransferOriginal candidate)
        {
            if(receiving)return false;receiving=true;
            try
            {
                if(candidate==null||!Current()||candidate.Operation!=Operation||candidate.Owner!=owner||candidate.Creation!=creation||candidate.Deaths!=deaths||
                    !Guid.TryParse(GamePrefs.GetString(EnumGamePrefs.GameGuidClient),out var nativeWorld)||candidate.World!=nativeWorld||
                    candidate.Equip==Returning||candidate.OwnedBelt!=owned||candidate.BagLocks!=locks||!candidate.TryGetPlan(out var plan))return false;
                var change=plan.Changes[0];
                if(!Returning&&(change.IsBag!=SourceIsBag||change.Index!=SourceIndex||change.Before.Count!=source.Count||change.Before.ItemData!=source.ItemData))return false;
                if(original!=null&&original.Digest!=candidate.Digest)return false;
                original=candidate; // Retain rejected description even if native Before changed.
                if(!RebirthHydrationClientOriginals.Prepare(Player,candidate))return false;
                originalSaved=true;
                if(awaitingReentry)
                {
                    if(!RebirthHydrationOwnerHold.TryReenterRemote(Player,candidate)||!Current())return false;
                    awaitingReentry=false;return true;
                }
                if(RebirthHydrationOwnerHold.Matches(Player,candidate)&&RebirthHydrationOwnerCheckpoint.HasSaved(Player,candidate,RebirthGearOwnerReceipt.Applied))return Current();
                if(!RebirthHydrationOwnerHold.TryAcquire(Player,candidate))return false;
                return RebirthHydrationOwnerCheckpoint.TryPersist(Player,original,RebirthGearOwnerReceipt.Applying)&&Current();
            }
            catch{return false;}
            finally{receiving=false;}
        }
        internal static bool TryRecover(EntityPlayerLocal player,RebirthHydrationTransferOriginal saved,out Intent intent)
        {
            intent=null;
            if(!Resolve(player,out _,out var creation,out var owner)||saved==null||saved.Creation!=creation||saved.Owner!=owner||saved.Deaths!=player.Died||
                !Guid.TryParse(GamePrefs.GetString(EnumGamePrefs.GameGuidClient),out var world)||saved.World!=world||
                !saved.TryGetPlan(out _))return false;
            Intent prior=null;
            if(intents.TryGetValue(player,out prior))
            {
                if(prior.Current()||prior.Original?.Digest!=saved.Digest)return false;
                int count=0;for(var cursor=prior;cursor!=null;cursor=cursor.previous)if(++count>=64)return false;
            }
            else if(intents.Count>=64)return false;
            var bag=player.bag?.ItemGrid?.items;var belt=player.inventory?.ItemGrid?.items;if(bag==null||belt==null)return false;
            int owned=RebirthToolbeltCapacity.GetOwnedSlotCount(player,belt.Length);
            if(owned!=saved.OwnedBelt||!RebirthGearInventorySnapshot.TryCapture(bag,belt,owned,out var snapshot))return false;
            var next=new Intent(player,saved,snapshot,owned,creation,owner);
            if(next.locks!=saved.BagLocks||!next.Current())return false;
            next.previous=prior;intents[player]=next;intent=next;return true;
        }
    }
    private static readonly Dictionary<EntityPlayerLocal,Intent> intents=new Dictionary<EntityPlayerLocal,Intent>();
    internal static bool TryGetCurrent(EntityPlayerLocal player,out Intent intent)
    {
        intent=null;
        if(player==null||!intents.TryGetValue(player,out var retained)||!retained.Current())return false;
        intent=retained;return true;
    }
    private static bool Resolve(EntityPlayerLocal player,out object peer,out string creation,out string owner)
    {
        peer=null;creation=owner=null;var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;
        if(!ThreadManager.IsMainThread()||player?.world?.IsRemote()!=true||manager==null||manager.IsServer||player.IsDead()||!player.IsSpawned()||
            !ReferenceEquals(player.world,GameManager.Instance?.World)||!ReferenceEquals(player.world.GetPrimaryPlayer(),player)||!ReferenceEquals(player.world.GetEntity(player.entityId),player)||
            manager.connectionToServer==null||manager.connectionToServer.Length==0||manager.connectionToServer[0]==null||manager.connectionToServer[0].IsDisconnected()||
            !RebirthHydrationTransferScope.TryCapture(player,out creation)||
            !RebirthStablePlayerIdentity.TryFromLocalPlatform(out var identity))return false;
        peer=manager.connectionToServer[0];owner=identity.StorageKey;return true;
    }
}

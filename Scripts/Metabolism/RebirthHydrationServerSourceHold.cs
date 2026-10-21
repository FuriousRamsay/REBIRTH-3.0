using System;
using System.Collections.Generic;
using System.IO;

// Remote server-side hydration source custody. This does not authorize or
// perform any native client inventory mutation, mirror replacement or replay.
internal static partial class RebirthHydrationServerSourceHold
{
    internal sealed class Lease
    {
        internal readonly EntityPlayer Player;internal readonly RebirthHydrationTransferOriginal Original;
        internal readonly RebirthMetabolismState State;
        internal bool Reentered {get;private set;}
        private readonly World world;private readonly GameManager game;private readonly ConnectionManager manager;
        private readonly object peer,transport;private readonly string save;
        internal World HeldWorld=>world;
        private Lease(EntityPlayer player,RebirthHydrationTransferOriginal original,RebirthMetabolismState state,object peer)
        {Player=player;Original=original;State=state;world=player.world;game=GameManager.Instance;manager=SingletonMonoBehaviour<ConnectionManager>.Instance;this.peer=peer;
            var client=peer as ClientInfo;transport=client?.netConnection!=null&&client.netConnection.Length>0?client.netConnection[0]:null;save=SavePath();}
        internal bool Current()
        {
            try{return leases.TryGetValue(Player,out var current)&&ReferenceEquals(current,this)&&
                ReferenceEquals(world,Player.world)&&ReferenceEquals(game,GameManager.Instance)&&
                ReferenceEquals(manager,SingletonMonoBehaviour<ConnectionManager>.Instance)&&save==SavePath()&&
                Scope(Player,Original,out var now)&&ReferenceEquals(now,peer)&&now is ClientInfo client&&transport!=null&&
                ReferenceEquals(client.netConnection[0],transport)&&Source(Player,Original,false,out var state)&&ReferenceEquals(state,State);}
            catch{return false;}
        }
        internal static bool TryAcquire(EntityPlayer player,RebirthHydrationTransferOriginal original,out Lease lease)
        {
            lease=null;
            try
            {
                if(player==null||original==null)return false;
                if(leases.TryGetValue(player,out var retained))
                {
                    if(retained.Original.Digest!=original.Digest||!retained.Current()||
                        !RebirthHydrationTransferJournal.TryPrepare(player,original,out _))return false;
                    if(!retained.Current()||!RebirthMetabolismStateRepository.TrySaveRemoteHydrationBefore(retained)||!retained.Current())return false;lease=retained;return true;
                }
                if(leases.Count>=64||HoldsPlayer(player)||!Scope(player,original,out var peer)||
                    RebirthBackpackLibraryReservation.BlocksResourceUse(player)||!Source(player,original,true,out var state))return false;
                var next=new Lease(player,original,state,peer);
                if(!Scope(player,original,out var latest)||!ReferenceEquals(latest,peer)||!Source(player,original,true,out var final)||!ReferenceEquals(final,state))return false;
                // Retain custody before a durable publication can become unknown.
                leases.Add(player,next);
                if(!next.Current()||!RebirthHydrationTransferJournal.TryPrepare(player,original,out _)||!next.Current()||
                    !RebirthMetabolismStateRepository.TrySaveRemoteHydrationBefore(next)||!next.Current())return false;
                lease=next;return true;
            }
            catch{return false;}
        }
        internal static bool TryReenter(EntityPlayer player,RebirthHydrationTransferOriginal original,out Lease lease,bool untouchedOnly=false)
        {
            lease=null;
            try
            {
                if(player==null||original==null||!Scope(player,original,out var peer)||
                    !RebirthHydrationTransferJournal.TryReadPending(player,out var saved,out _)||saved?.Digest!=original.Digest||
                    !Source(player,original,false,out var state))return false;
                leases.TryGetValue(player,out var existing);
                if(existing!=null&&existing.Current())
                {
                    if(existing.Original.Digest!=original.Digest||!existing.Current()||
                        !(untouchedOnly?RebirthHydrationTransferJournal.HasPrepared(player,original)&&RebirthMetabolismStateRepository.HasSavedRemoteHydrationBefore(existing):existing.Reentered&&SavedReentry(existing)))return false;
                    lease=existing;return true;
                }
                if(existing!=null&&existing.Original.Digest!=original.Digest||leases.Count+reenteredRemoteLeases.Count>=64||RebirthBackpackLibraryReservation.BlocksResourceUse(player))return false;
                foreach(var retained in leases.Values)
                    if(retained.Original.Owner==original.Owner&&retained.Original.World==original.World&&
                        (retained.Original.Digest!=original.Digest||retained.Current()))return false;
                var next=new Lease(player,original,state,peer){Reentered=true};
                if(existing!=null)reenteredRemoteLeases.Add(existing);
                leases[player]=next; // Hold before native final-file observations.
                if(!next.Current()||!(untouchedOnly?RebirthHydrationTransferJournal.HasPrepared(player,original)&&RebirthMetabolismStateRepository.HasSavedRemoteHydrationBefore(next):SavedReentry(next)))return false;
                lease=next;return true;
            }
            catch{return false;}
        }
        internal static bool SavedReentry(Lease lease)
        {
            if(lease==null||!lease.Reentered||!lease.Current())return false;
            bool ownerBefore=RebirthHydrationRemoteOwnerWitness.HasSavedBefore(lease);
            bool ownerAfter=RebirthHydrationRemoteOwnerWitness.HasApplied(lease);
            bool sourceBefore=RebirthMetabolismStateRepository.HasSavedRemoteHydrationBefore(lease);
            if(!ownerBefore&&!ownerAfter||!sourceBefore&&(!ownerAfter||!RebirthMetabolismStateRepository.HasSavedRemoteHydrationAfter(lease)))return false;
            // An Applied native result must belong to an entered original.
            return (!ownerAfter||RebirthHydrationTransferJournal.HasAttempted(lease.Player,lease.Original))&&lease.Current();
        }
    }
    private static readonly Dictionary<EntityPlayer,Lease> leases=new Dictionary<EntityPlayer,Lease>();
    private static readonly List<Lease> reenteredRemoteLeases=new List<Lease>();
    internal static bool HoldsPlayer(EntityPlayer player)
    {
        if(player==null)return false;
        if(leases.ContainsKey(player))return true;
        bool identified=RebirthStablePlayerIdentity.TryResolveServerEntity(player,out var identity)&&identity!=null;
        foreach(var lease in leases.Values)
            if(ReferenceEquals(lease.HeldWorld,player.world)&&
                (identified?identity.StorageKey==lease.Original.Owner:player.entityId==lease.Player.entityId))return true;
        return false;
    }
    internal static bool HoldsWorld(World world)
    {if(world==null)return false;foreach(var lease in leases.Values)if(ReferenceEquals(lease.HeldWorld,world))return true;foreach(var lease in reenteredRemoteLeases)if(ReferenceEquals(lease.HeldWorld,world))return true;return false;}
    private static bool Scope(EntityPlayer player,RebirthHydrationTransferOriginal original,out object peer)
    {
        peer=null;var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;
        if(!ThreadManager.IsMainThread()||manager?.IsServer!=true||player is EntityPlayerLocal||player?.world==null||
            player.world.IsRemote()||!ReferenceEquals(player.world,GameManager.Instance?.World)||player.IsDead()||!player.IsSpawned()||
            !ReferenceEquals(player.world.GetEntity(player.entityId),player)||!Guid.TryParse(player.world.Guid,out var world)||
            !RebirthHydrationTransferScope.TryCapture(player,out var creation))return false;
        var client=manager.Clients?.ForEntityId(player.entityId);
        if(client==null||!client.loginDone||client.disconnecting||client.netConnection==null||client.netConnection.Length==0||client.netConnection[0]==null||client.netConnection[0].IsDisconnected()||
            !RebirthStablePlayerIdentity.TryFromClientInfo(client,out var identity)||
            !RebirthStablePlayerIdentity.TryResolveServerEntity(player,out var canonical)||identity.StorageKey!=canonical.StorageKey||
            !original.Matches(world,canonical.StorageKey,creation,player.Died))return false;
        peer=client;return true;
    }
    private static bool Source(EntityPlayer player,RebirthHydrationTransferOriginal original,bool initial,out RebirthMetabolismState state)
    {
        state=null;
        if(!RebirthMetabolismStateRepository.TryGet(player,out state)||!RebirthHydrationTransferScope.MatchesState(original.Creation,state)||
            initial&&state.Revision!=original.Revision||!original.TryGetPlan(out var plan))return false;
        var item=state.HydrationSlotItem;int count=item==null||item.IsEmpty()?0:item.count;
        var receipt=state.HydrationTransferReceipt;
        if(receipt!=null&&receipt.Operation==original.Operation&&receipt.Digest!=original.Digest)return false;
        bool settled=receipt?.Digest==original.Digest;
        if(initial&&settled)return false;
        var expected=settled?plan.GearAfter:plan.GearBefore;
        if(count!=expected.Count||(count==0?string.Empty:RebirthNativeItemCodec.Encode(item.itemValue))!=expected.ItemData)return false;
        var container=original.Equip?plan.GearAfter:plan.GearBefore;
        if(!RebirthNativeItemCodec.TryDecode(container.ItemData,out var value)||!RebirthConsumableResolver.TryResolve(value,out var definition)||
            definition==null||!definition.IsDrink||!definition.HydrationEquippable)return false;
        if(original.Equip&&RebirthSurvivorMode.IsEnabledForCurrentWorld())
        {
            if(!RebirthWorldCharacterService.TryGet(player,out var character)||character?.Support==null)return false;
            if(character.Support.EquippedGearBySlot.TryGetValue(RebirthSurvivorGearService.SupportSlotId,out var gear)&&!string.IsNullOrEmpty(gear))return false;
        }
        return true;
    }
    private static string SavePath()
    {string path=GameIO.GetSaveGameDir();if(string.IsNullOrWhiteSpace(path))throw new InvalidDataException("Hydration source save unavailable");return Path.GetFullPath(path);}
}

using System;
using System.IO;

// Private observed native owner result. It authorizes no inventory/source write
// and cannot adopt or release a warm hold. Used only for cold metadata closure.
internal sealed partial class RebirthHydrationColdOwnerWitness
{
    internal readonly EntityPlayerLocal Player;internal readonly RebirthHydrationTransferOriginal Original;
    private readonly World world;private readonly GameManager game;private readonly ConnectionManager manager;
    private readonly object bag,belt;private readonly string save,playerData;
    private RebirthHydrationColdOwnerWitness(EntityPlayerLocal player,RebirthHydrationTransferOriginal original)
    {Player=player;Original=original;world=player.world;game=GameManager.Instance;manager=SingletonMonoBehaviour<ConnectionManager>.Instance;
        bag=player.bag;belt=player.inventory;save=Path.GetFullPath(GameIO.GetSaveGameDir());playerData=Path.GetFullPath(GameIO.GetPlayerDataDir());}
    internal static bool TryObserve(EntityPlayerLocal player,RebirthHydrationTransferOriginal original,out RebirthHydrationColdOwnerWitness witness)
    {
        witness=null;
        try{if(player==null||original==null)return false;var next=new RebirthHydrationColdOwnerWitness(player,original);
            if(!next.Current())return false;witness=next;return true;}
        catch{return false;}
    }
    internal bool Current()=>CurrentCore(false);
    private bool CurrentCore(bool recoveryHeld)
    {
        try
        {
            if(!Scope(recoveryHeld)||!LiveReceipts()||!Original.TryGetPlan(out var plan)||!RebirthStablePlayerIdentity.TryFromLocalPlatform(out var owner)||
                !RebirthGearNativePlayerFile.TryRead(owner,out var file)||file.deaths!=Original.Deaths||
                file.buffData==null||file.buffData.Length<1||file.buffData.Length>1024*1024)return false;
            byte[] buffs=file.buffData.ToArray();
            if(!RebirthGearReceiptReader.Contains(buffs,Original.Operation.ToString("N"),RebirthGearOwnerReceipt.Applied)||
                !RebirthGearReceiptReader.TryReadHydrationStage(buffs,Original.Operation,Original.Digest,out var hydration)||hydration!=1f||
                !RebirthGearReceiptReader.TryReadHydrationRefusedStage(buffs,Original.Operation,Original.Digest,out var refused)||refused!=0f)return false;
            var savedBag=RebirthGearPlayerDataInventory.ReadSlots(file,true);var savedBelt=RebirthGearPlayerDataInventory.ReadSlots(file,false);
            return RebirthGearInventorySnapshot.TryCapture(savedBag,savedBelt,Original.OwnedBelt,out var saved)&&
                plan.MatchesAppliedInventory(saved.Bag,saved.Belt)&&
                RebirthGearInventorySnapshot.TryCapture(Player.bag?.ItemGrid?.items,Player.inventory?.ItemGrid?.items,Original.OwnedBelt,out var live)&&
                plan.MatchesAppliedInventory(live.Bag,live.Belt)&&LiveReceipts()&&Scope(recoveryHeld);
        }
        catch{return false;}
    }
    private bool LiveReceipts()=>Player.Buffs!=null&&
        Player.Buffs.GetCustomVar("rbGear_"+Original.Operation.ToString("N"))==1f&&
        Player.Buffs.GetCustomVar(RebirthHydrationOwnerCheckpoint.Marker(Original))==1f&&
        Player.Buffs.GetCustomVar("rbHydrationRefused_"+Original.Operation.ToString("N")+"_"+Original.Digest)==0f;
    private bool Scope(bool recoveryHeld)
    {
        if(!ThreadManager.IsMainThread()||Original==null||world==null||world.IsRemote()||manager?.IsServer!=true||
            !ReferenceEquals(game,GameManager.Instance)||!ReferenceEquals(world,game.World)||!ReferenceEquals(Player.world,world)||
            !ReferenceEquals(manager,SingletonMonoBehaviour<ConnectionManager>.Instance)||!ReferenceEquals(world.GetPrimaryPlayer(),Player)||
            !ReferenceEquals(world.GetEntity(Player.entityId),Player)||!ReferenceEquals(Player.bag,bag)||!ReferenceEquals(Player.inventory,belt)||
            Player.IsDead()||!Player.IsSpawned()||RebirthHydrationTransferGrant.HasIssued(Original.Operation)||
            RebirthHydrationRemoteGrant.HasIssued(Original.Operation)||RebirthHydrationRemoteClientGrant.HasIssued(Original.Operation)||
            (recoveryHeld?!RebirthHydrationOwnerHold.Matches(Player,Original):RebirthHydrationOwnerHold.HoldsMetabolismOwner(Player))||RebirthHydrationServerSourceHold.HoldsPlayer(Player)||
            !SamePath(save,GameIO.GetSaveGameDir())||!SamePath(playerData,GameIO.GetPlayerDataDir())||
            !RebirthHydrationTransferScope.TryObserve(Player,out var scope)||!Guid.TryParse(world.Guid,out var worldId)||
            !RebirthStablePlayerIdentity.TryFromLocalPlatform(out var owner)||
            !RebirthStablePlayerIdentity.TryResolveServerEntity(Player,out var canonical)||owner.StorageKey!=canonical.StorageKey||
            game.getPersistentPlayerID(null)?.CombinedString!=owner.CanonicalId||!Original.Matches(worldId,owner.StorageKey,scope,Player.Died))return false;
        var nativeBag=Player.bag?.ItemGrid?.items;var nativeBelt=Player.inventory?.ItemGrid?.items;
        if(nativeBag==null||nativeBelt==null||Original.BagLocks.Length!=nativeBag.Length||
            RebirthToolbeltCapacity.GetOwnedSlotCount(Player,nativeBelt.Length)!=Original.OwnedBelt)return false;
        for(int i=0;i<nativeBag.Length;i++)
            if((Player.bag.LockedSlots!=null&&i<Player.bag.LockedSlots.Length&&Player.bag.LockedSlots[i])!=(Original.BagLocks[i]=='1'))return false;
        return true;
    }
    private static bool SamePath(string first,string second)=>!string.IsNullOrWhiteSpace(second)&&string.Equals(first,Path.GetFullPath(second),
        Environment.OSVersion.Platform==PlatformID.Win32NT?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal);
}

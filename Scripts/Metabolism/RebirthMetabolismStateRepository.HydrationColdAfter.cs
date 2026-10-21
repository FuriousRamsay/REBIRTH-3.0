using System;
using System.IO;

public static partial class RebirthMetabolismStateRepository
{
    // Read-only cold committed source witness. It cannot create a grant,
    // mutate physiology, replay an owner setter or release native custody.
    internal static bool HasSavedColdLocalHydrationAfter(EntityPlayerLocal player,RebirthHydrationTransferOriginal original)
    {
        try
        {
            if(!ColdLocalHydrationScope(player,original)||!TryGet(player,out var state)||!ColdHydrationAfterRow(state,original))return false;
            var game=GameManager.Instance;var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;
            var world=player.world;var bag=player.bag;var belt=player.inventory;
            lock(Sync)
            {
                if(!ScopeCurrentLocked()||!ReferenceEquals(scopeWorld,world))return false;
                string id=GetStablePlayerId(player),path=scopePath;int generation=loadGeneration;
                if(!States.TryGetValue(id,out var current)||!ReferenceEquals(current,state))return false;
                var info=new FileInfo(path);
                if(!info.Exists||info.Length<1||info.Length>MaximumStoreBytes||(info.Attributes&FileAttributes.ReparsePoint)!=0)return false;
                var saved=ReadCandidate(path);
                return saved.TryGetValue(id,out var row)&&ColdHydrationAfterRow(row,original)&&
                    generation==loadGeneration&&path==scopePath&&ScopeCurrentLocked()&&ReferenceEquals(scopeWorld,world)&&
                    ReferenceEquals(GameManager.Instance,game)&&ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance,manager)&&
                    ReferenceEquals(player.world,world)&&ReferenceEquals(player.bag,bag)&&ReferenceEquals(player.inventory,belt)&&
                    ColdLocalHydrationScope(player,original)&&States.TryGetValue(id,out current)&&ReferenceEquals(current,state)&&
                    ColdHydrationAfterRow(state,original);
            }
        }
        catch{return false;}
    }
    private static bool ColdLocalHydrationScope(EntityPlayerLocal player,RebirthHydrationTransferOriginal original)
    {
        if(!ThreadManager.IsMainThread()||!serverAuthority||original==null||player?.world?.IsRemote()!=false||
            SingletonMonoBehaviour<ConnectionManager>.Instance?.IsServer!=true||!ReferenceEquals(player.world,GameManager.Instance?.World)||
            !ReferenceEquals(player.world.GetPrimaryPlayer(),player)||!ReferenceEquals(player.world.GetEntity(player.entityId),player)||
            player.IsDead()||!player.IsSpawned()||!Guid.TryParse(player.world.Guid,out var world)||
            !RebirthHydrationTransferScope.TryObserve(player,out var scope)||
            !RebirthStablePlayerIdentity.TryFromLocalPlatform(out var owner)||
            !RebirthStablePlayerIdentity.TryResolveServerEntity(player,out var canonical)||canonical.StorageKey!=owner.StorageKey||
            GameManager.Instance.getPersistentPlayerID(null)?.CombinedString!=owner.CanonicalId)return false;
        return original.Matches(world,owner.StorageKey,scope,player.Died);
    }
    private static bool ColdHydrationAfterRow(RebirthMetabolismState state,RebirthHydrationTransferOriginal original)
    {
        if(state==null||!RebirthHydrationTransferScope.MatchesState(original.Creation,state)||
            state.HydrationTransferReceipt?.Operation!=original.Operation||state.HydrationTransferReceipt.Digest!=original.Digest||
            !original.TryGetPlan(out var plan))return false;
        var item=state.HydrationSlotItem;int count=item==null||item.IsEmpty()?0:item.count;
        return count==plan.GearAfter.Count&&(count==0?string.Empty:RebirthNativeItemCodec.Encode(item.itemValue))==plan.GearAfter.ItemData;
    }
}

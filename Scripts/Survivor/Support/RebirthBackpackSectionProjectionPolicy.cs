using System;
// TOOLS ONLY UNAPPLIED: display scope, never transfer authorization.
public static class RebirthBackpackSectionProjectionPolicy
{
    private sealed class LocalScope
    { public object Game,World,Manager,Origin,Support;public RebirthWorldCharacterRecord Record;public int PlayerId;public string Creation,Item;public long Revision; }
    private static bool TryLocal(EntityPlayerLocal player,out LocalScope scope)
    {
        scope=null;var game=GameManager.Instance;var world=player?.world;int playerId=player!=null?player.entityId:-1;var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;
        if(player==null||world==null||game==null||world.IsRemote()||!ReferenceEquals(world,game.World)||!ReferenceEquals(world.GetPrimaryPlayer(),player)||
            !ReferenceEquals(world.GetEntity(playerId),player)||!ThreadManager.IsMainThread()||manager==null||!manager.IsServer||!player.IsSpawned()||player.IsDead()||
            !RebirthSurvivorMode.IsEnabledForCurrentWorld()||!RebirthWorldCharacterRepository.IsServerAuthority||!RebirthWorldCharacterService.TryGet(player,out var record)||
            record==null||!record.IsComplete||record.Support==null||record.Origin==null||!RebirthWorldCharacterRepository.IsCurrentCachedRecord(record))return false;
        string creation=record.Origin.CreationId;string item;record.Support.EquippedGearBySlot.TryGetValue(RebirthSurvivorGearService.BackpackSlotId,out item);
        if(player.entityId!=playerId||!RebirthSurvivorRequestScope.Matches(creation,creation)||!ReferenceEquals(GameManager.Instance,game)||!ReferenceEquals(game.World,world)||!ReferenceEquals(player.world,world)||!ReferenceEquals(world.GetPrimaryPlayer(),player)||!ReferenceEquals(world.GetEntity(playerId),player)||!ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance,manager)||!manager.IsServer)return false;
        scope=new LocalScope{Game=game,World=world,Manager=manager,Record=record,Origin=record.Origin,Support=record.Support,PlayerId=playerId,Creation=creation,Item=item,Revision=record.Support.GearRevision};return true;
    }
    private static bool Same(LocalScope a,LocalScope b)=>ReferenceEquals(a.Game,b.Game)&&ReferenceEquals(a.World,b.World)&&ReferenceEquals(a.Manager,b.Manager)&&ReferenceEquals(a.Record,b.Record)&&ReferenceEquals(a.Origin,b.Origin)&&ReferenceEquals(a.Support,b.Support)&&a.PlayerId==b.PlayerId&&a.Revision==b.Revision&&string.Equals(a.Creation,b.Creation,StringComparison.Ordinal)&&string.Equals(a.Item,b.Item,StringComparison.Ordinal);
    public static string GetCreationId(EntityPlayerLocal player)
    {
        if(player?.world==null)return string.Empty;
        if(player.world.IsRemote())return RebirthSurvivorClientState.GetProjectedCreationId(player);
        return TryLocal(player,out var first)&&TryLocal(player,out var last)&&Same(first,last)?first.Creation:string.Empty;
    }
    private static bool Matches(EntityPlayerLocal player,string creation,string item,long revision,int capacity,bool sale)
    {
        if(player==null||player.world==null||GameManager.Instance==null||!ReferenceEquals(player.world,GameManager.Instance.World)||!ReferenceEquals(player.world.GetPrimaryPlayer(),player)||string.IsNullOrEmpty(item)||revision<0)return false;
        if(!player.world.IsRemote())return TryLocal(player,out var first)&&RebirthSurvivorRequestScope.Matches(creation,first.Creation)&&first.Revision==revision&&string.Equals(item,first.Item,StringComparison.Ordinal)&&
            capacity==(sale?RebirthBackpackSellStashPolicy.CapacityForBackpack(item):RebirthBackpackLibraryPolicy.CapacityForBackpack(item))&&TryLocal(player,out var last)&&Same(first,last);
        var header=RebirthSurvivorClientState.GetOwnerHeader();
        if(!header.Available||!header.RebirthModeEnabled||!header.HasCharacter||header.GearRevision<0||header.GearRevision!=revision)return false;
        if(!RebirthSurvivorRequestScope.Matches(creation,RebirthSurvivorClientState.GetProjectedCreationId(player))||!string.Equals(item,RebirthSurvivorClientState.GetProjectedGearItem(player,RebirthSurvivorGearService.BackpackSlotId),StringComparison.Ordinal))return false;
        if(capacity!=(sale?RebirthBackpackSellStashPolicy.CapacityForBackpack(item):RebirthBackpackLibraryPolicy.CapacityForBackpack(item)))return false;
        return header.Equals(RebirthSurvivorClientState.GetOwnerHeader());
    }
    public static bool Matches(EntityPlayerLocal player,RebirthBackpackLibraryView view)=>view!=null&&Matches(player,view.CreationId,view.BackpackItemId,view.GearRevision,view.Capacity,false);
    public static bool Matches(EntityPlayerLocal player,RebirthBackpackSellStashView view)=>view!=null&&Matches(player,view.CreationId,view.BackpackItemId,view.GearRevision,view.Capacity,true);
}

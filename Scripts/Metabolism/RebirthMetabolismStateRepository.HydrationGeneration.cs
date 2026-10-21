using System;
using System.Collections.Generic;
using System.IO;

public static partial class RebirthMetabolismStateRepository
{
    private sealed class HydrationGenerationWitness
    {internal RebirthMetabolismState State;internal World World;internal string Path,Value;internal int Generation;}
    private static readonly Dictionary<string,HydrationGenerationWitness> HydrationGenerations=new Dictionary<string,HydrationGenerationWitness>(StringComparer.Ordinal);
    internal static bool TryEnsureHydrationGeneration(EntityPlayer player,out string value)
    {
        value=null;
        try
        {
            if(!GenerationScope(player,out var peer))return false;
            var game=GameManager.Instance;var world=player.world;var state=GetOrCreate(player);if(state==null)return false;
            string id,path;int generation;string proposed;
            lock(Sync)
            {
                if(!ScopeCurrentLocked()||!ReferenceEquals(scopeWorld,world)||!States.TryGetValue(id=GetStablePlayerId(player),out var current)||!ReferenceEquals(state,current))return false;
                path=scopePath;generation=loadGeneration;
                if(state.HydrationGenerationId==string.Empty)
                {
                    if(RebirthNpcMetabolismCustody.HoldsPlayer(player)||RebirthHydrationOwnerHold.HoldsMetabolismOwner(player as EntityPlayerLocal)||RebirthHydrationServerSourceHold.HoldsPlayer(player))return false;
                    state.HydrationGenerationId=RebirthHydrationGeneration.Create();state.Touch();
                }
                proposed=state.HydrationGenerationId;if(!RebirthHydrationGeneration.IsCanonical(proposed))return false;
                if(HydrationGenerations.TryGetValue(id,out var known)&&ReferenceEquals(known.State,state)&&ReferenceEquals(known.World,world)&&
                    known.Path==path&&known.Generation==generation)
                {
                    if(known.Value!=proposed)return false;
                    if(!GenerationScope(player,out var currentPeer)||!ReferenceEquals(peer,currentPeer)||!ReferenceEquals(game,GameManager.Instance))return false;
                    value=proposed;return true;
                }
            }
            if(!SavedGeneration(path,id,proposed)&&(!TrySave("native-hydration-generation",out _)||!SavedGeneration(path,id,proposed)))return false;
            lock(Sync)
            {
                if(!ScopeCurrentLocked()||generation!=loadGeneration||path!=scopePath||!ReferenceEquals(scopeWorld,world)||
                    !States.TryGetValue(id,out var current)||!ReferenceEquals(current,state)||current.HydrationGenerationId!=proposed||
                    !GenerationScope(player,out var currentPeer)||!ReferenceEquals(currentPeer,peer)||!ReferenceEquals(game,GameManager.Instance))return false;
                HydrationGenerations[id]=new HydrationGenerationWitness{State=state,World=world,Path=path,Generation=generation,Value=proposed};
                value=proposed;return true;
            }
        }
        catch{return false;}
    }
    private static bool SavedGeneration(string path,string id,string value)
    {
        try
        {
            var file=new FileInfo(path);if(!file.Exists||file.Length<1||file.Length>MaximumStoreBytes||(file.Attributes&FileAttributes.ReparsePoint)!=0)return false;
            var saved=ReadCandidate(path);return saved.TryGetValue(id,out var row)&&row.HydrationGenerationId==value;
        }
        catch{return false;}
    }
    private static bool GenerationScope(EntityPlayer player,out object peer)
    {
        peer=null;var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;
        if(!ThreadManager.IsMainThread()||!serverAuthority||manager?.IsServer!=true||RebirthSurvivorMode.IsEnabledForCurrentWorld()||
            player?.world==null||player.world.IsRemote()||!ReferenceEquals(player.world,GameManager.Instance?.World)||
            !ReferenceEquals(player.world.GetEntity(player.entityId),player)||player.IsDead()||!player.IsSpawned()||
            !RebirthStablePlayerIdentity.TryResolveServerEntity(player,out var canonical))return false;
        if(player is EntityPlayerLocal)
            return ReferenceEquals(player.world.GetPrimaryPlayer(),player)&&RebirthStablePlayerIdentity.TryFromLocalPlatform(out var owner)&&
                owner.StorageKey==canonical.StorageKey&&GameManager.Instance.getPersistentPlayerID(null)?.CombinedString==owner.CanonicalId;
        var client=manager.Clients?.ForEntityId(player.entityId);
        if(client==null||!client.loginDone||client.disconnecting||!RebirthStablePlayerIdentity.TryFromClientInfo(client,out var identity)||identity.StorageKey!=canonical.StorageKey)return false;
        peer=client;return true;
    }
}

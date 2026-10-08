using System;
using System.IO;
using System.Xml.Linq;

// Continuation of an already accepted SAME refusal; creates no transaction or offer.
internal static class RebirthGearPreparationRefusalFlow
{
    internal static bool TryAdvance(EntityPlayerLocal player,object session)
    {
        bool relevant=false;
        try
        {
            if(!RebirthGearPreparationRefusalClient.TryGetCurrent(player,session,out var refusal)||
                !RebirthGearPreparationMarker.TryRead(refusal.OriginalMarker,1f,out var savedWorld,out var owned,out var intent)||
                !RebirthGearOwnerReservation.MatchesUnpreparedIntent(player,session,intent))return false;
            relevant=true;
            var game=GameManager.Instance;var world=player.world;var state=world?.worldState;string stateGuid=state?.Guid;
            var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;
            if(!RebirthStablePlayerIdentity.TryFromLocalPlatform(out var owner)||owner==null)return true;
            string root=Path.GetFullPath(GameIO.GetPlayerDataDir()),clientWorld=GamePrefs.GetString(EnumGamePrefs.GameGuidClient);
            if(!Guid.TryParse(clientWorld,out var clientGuid)||clientGuid!=savedWorld)return true;
            Func<bool> current=()=>ThreadManager.IsMainThread()&&ReferenceEquals(game,GameManager.Instance)&&game!=null&&
                ReferenceEquals(game.World,world)&&world!=null&&world.IsRemote()&&ReferenceEquals(player.world,world)&&
                state!=null&&ReferenceEquals(world.worldState,state)&&state.Guid==stateGuid&&
                ReferenceEquals(world.GetPrimaryPlayer(),player)&&ReferenceEquals(world.GetEntity(player.entityId),player)&&
                player.IsSpawned()&&!player.IsDead()&&player.Buffs!=null&&RebirthSurvivorMode.IsEnabledForCurrentWorld()&&
                !RebirthCharacterCreationHoldService.IsHeld(player)&&RebirthGearOwnerReservation.MatchesUnpreparedIntent(player,session,intent)&&
                ReferenceEquals(manager,SingletonMonoBehaviour<ConnectionManager>.Instance)&&manager!=null&&!manager.IsServer&&
                manager.connectionToServer!=null&&manager.connectionToServer.Length>0&&manager.connectionToServer[0]!=null&&
                ReferenceEquals(manager.connectionToServer[0],session)&&!manager.connectionToServer[0].IsDisconnected()&&
                GamePrefs.GetString(EnumGamePrefs.GameGuidClient)==clientWorld&&Path.GetFullPath(GameIO.GetPlayerDataDir())==root&&
                game.getPersistentPlayerID(null)?.CombinedString==owner.CanonicalId&&
                RebirthGearPreparationRefusalClient.TryGetCurrent(player,session,out var accepted)&&XNode.DeepEquals(accepted.Write(),refusal.Write())&&
                RebirthGearInventorySnapshot.TryCapture(player.bag?.ItemGrid?.items,player.inventory?.ItemGrid?.items,owned,out var live)&&intent.MatchesInventory(live);
            if(!current())return true;
            if(RebirthGearPreparationRefusalClient.TryGetAcknowledged(player,session,out var ack)&&XNode.DeepEquals(ack.Write(),refusal.Write()))
            {
                if(!RebirthGearPreparationRefusalOwnerCheckpoint.TryPersistRetirement(player,session,refusal)||!current())return true;
                bool released=RebirthGearOwnerReservation.ReleaseRefusedOriginal(player,session,intent,()=>
                    current()&&RebirthGearPreparationRefusalClient.TryGetAcknowledged(player,session,out var finalAck)&&
                    XNode.DeepEquals(finalAck.Write(),refusal.Write())&&
                    player.Buffs.GetCustomVar("rbGear_"+intent.TransactionId.ToString("N"))==-1f&&
                    RebirthGearPreparationRefusalPlayerFileWitness.HasRetired(owner,refusal)&&current()&&
                    HasRetiredLiveState(player,intent));
                if(released&&ReferenceEquals(game,GameManager.Instance)&&ReferenceEquals(game.World,world)&&
                    ReferenceEquals(world.GetPrimaryPlayer(),player))RebirthBackpackLibraryClientViews.Request(world,player.entityId);
                return true;
            }
            NetPackageManager.GetPackageId(typeof(NetPackagePlayerData));
            NetPackageManager.GetPackageId(typeof(NetPackageRebirthGearPreparationRequest));
            NetPackageManager.GetPackageId(typeof(NetPackageRebirthGearPreparationRefusal));
            if(!current()||!RebirthGearPreparationRefusalOwnerCheckpoint.TryPersist(player,session,refusal)||!current())return true;
            var snapshot=NetPackageManager.GetPackage<NetPackagePlayerData>().Setup(player);
            if(!current())return true;
            manager.SendToServer(snapshot);
            if(!current())return true;
            manager.SendToServer(NetPackageManager.GetPackage<NetPackageRebirthGearPreparationRequest>().Setup(player.entityId,refusal.OriginalMarker));
            return true;
        }
        catch{return relevant;} // Only an accepted held original owns this continuation.
    }
    private static bool HasRetiredLiveState(EntityPlayerLocal player,RebirthGearPreparationIntent intent)
    {
        if(player?.Buffs==null||intent==null||
            player.Buffs.GetCustomVar("rbGear_"+intent.TransactionId.ToString("N"))!=-1f)return false;
        foreach(string key in player.Buffs.CVars.Keys)
            if(key.StartsWith("_rbgearintent_",StringComparison.OrdinalIgnoreCase))return false;
        return true;
    }
}
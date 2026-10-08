using System;
using System.IO;

// Saves original intent under a continuously held owner inventory. Returning true
// requires exact final native file + original preimage, never the native save call.
internal static class RebirthGearPreparationOwnerCheckpoint
{
    internal static bool TryPersist(EntityPlayerLocal player,object session,RebirthGearPreparationIntent intent,Guid expectedWorld)
    {
        if(player==null||session==null||intent==null||expectedWorld==Guid.Empty||!ThreadManager.IsMainThread()||
            !RebirthStablePlayerIdentity.TryFromLocalPlatform(out var identity))return false;
        var game=GameManager.Instance;var world=player.world;var state=world?.worldState;
        var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;
        string root,clientWorld;
        try {root=Path.GetFullPath(GameIO.GetPlayerDataDir());clientWorld=GamePrefs.GetString(EnumGamePrefs.GameGuidClient);if(!Guid.TryParse(clientWorld,out var savedWorld)||savedWorld!=expectedWorld)return false;}catch{return false;}
        Func<bool> current=()=>ThreadManager.IsMainThread()&&ReferenceEquals(game,GameManager.Instance)&&game!=null&&
            ReferenceEquals(game.World,world)&&ReferenceEquals(player.world,world)&&world!=null&&world.IsRemote()&&
            ReferenceEquals(world.worldState,state)&&state!=null&&GamePrefs.GetString(EnumGamePrefs.GameGuidClient)==clientWorld&&
            ReferenceEquals(world.GetPrimaryPlayer(),player)&&ReferenceEquals(world.GetEntity(player.entityId),player)&&
            player.IsSpawned()&&!player.IsDead()&&player.Buffs!=null&&RebirthSurvivorMode.IsEnabledForCurrentWorld()&&
            !RebirthCharacterCreationHoldService.IsHeld(player)&&RebirthGearOwnerReservation.MatchesIntent(player,session,intent)&&
            RebirthSurvivorRequestScope.Matches(intent.CreationId,RebirthSurvivorClientState.GetProjectedCreationId(player))&&
            ReferenceEquals(manager,SingletonMonoBehaviour<ConnectionManager>.Instance)&&manager!=null&&!manager.IsServer&&
            manager.connectionToServer!=null&&manager.connectionToServer.Length>0&&manager.connectionToServer[0]!=null&&
            ReferenceEquals(manager.connectionToServer[0],session)&&!manager.connectionToServer[0].IsDisconnected()&&
            game.getPersistentPlayerID(null)?.CombinedString==identity.CanonicalId&&Path.GetFullPath(GameIO.GetPlayerDataDir())==root;
        try
        {
            if(!current())return false;
            var belt=player.inventory?.ItemGrid?.items;
            if(belt==null||!RebirthGearInventorySnapshot.TryCapture(player.bag?.ItemGrid?.items,belt,
                RebirthToolbeltCapacity.GetOwnedSlotCount(player,belt.Length),out var snapshot)||!intent.MatchesInventory(snapshot)||
                !RebirthGearPreparationMarker.TryEncode(expectedWorld,snapshot.OwnedBeltSlots,intent,out var marker))return false;
            // Never use original-preparation saving as a phase downgrade after Apply.
            if(player.Buffs.GetCustomVar("rbGear_"+intent.TransactionId.ToString("N"))!=0f)return false;
            foreach(string key in player.Buffs.CVars.Keys)
            {
                float value=player.Buffs.GetCustomVar(key);
                if(!key.StartsWith("_rbgearintent_",StringComparison.OrdinalIgnoreCase)||value==0f)continue;
                if(key!=marker||value!=1f)return false;
            }
            if(!current())return false;
            // Reload may retain the exact saved marker while live CVars lag.
            // Restore that same marker before any upload; never create another ID.
            if(player.Buffs.GetCustomVar(marker)!=1f)player.Buffs.SetCustomVar(marker,1f,false);
            if(!current())return false;
            if(RebirthGearPreparationPlayerFileWitness.HasOriginal(identity,expectedWorld,marker,intent))return current();
            game.SaveLocalPlayerData();
            return current()&&RebirthGearPreparationPlayerFileWitness.HasOriginal(identity,expectedWorld,marker,intent)&&current();
        }
        catch {return false;} // Unknown native write retains original marker and hold.
    }
}
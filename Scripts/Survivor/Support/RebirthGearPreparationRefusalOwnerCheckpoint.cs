using System;
using System.IO;
using System.Xml.Linq;

// Saves only rejection/marker retirement for the accepted SAME unprepared original.
// Unknown native save keeps custody held; never move items or synthesize an offer.
internal static class RebirthGearPreparationRefusalOwnerCheckpoint
{
    internal static bool TryPersist(EntityPlayerLocal player,object session,RebirthGearPreparationRefusal refusal)
        =>TryPersistCore(player,session,refusal,false);
    internal static bool TryPersistRetirement(EntityPlayerLocal player,object session,RebirthGearPreparationRefusal refusal)
        =>TryPersistCore(player,session,refusal,true);
    private static bool TryPersistCore(EntityPlayerLocal player,object session,RebirthGearPreparationRefusal refusal,bool retiring)
    {
        try
        {
            if(player==null||session==null||refusal==null||!ThreadManager.IsMainThread()||
                !RebirthStablePlayerIdentity.TryFromLocalPlatform(out var owner)||owner==null||
                !RebirthGearPreparationMarker.TryRead(refusal.OriginalMarker,1f,out var savedWorld,out var owned,out var intent)||
                savedWorld!=refusal.SavedWorld)return false;
            var game=GameManager.Instance;var world=player.world;var state=world?.worldState;
            string stateGuid=state?.Guid;
            var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;
            string root=Path.GetFullPath(GameIO.GetPlayerDataDir()),clientWorld=GamePrefs.GetString(EnumGamePrefs.GameGuidClient);
            if(!Guid.TryParse(clientWorld,out var nativeWorld)||nativeWorld!=savedWorld)return false;
            string marker=refusal.OriginalMarker,receipt="rbGear_"+intent.TransactionId.ToString("N");
            Func<bool> current=()=>ThreadManager.IsMainThread()&&ReferenceEquals(game,GameManager.Instance)&&game!=null&&
                ReferenceEquals(game.World,world)&&ReferenceEquals(player.world,world)&&world!=null&&world.IsRemote()&&
                state!=null&&ReferenceEquals(world.worldState,state)&&state.Guid==stateGuid&&
                GamePrefs.GetString(EnumGamePrefs.GameGuidClient)==clientWorld&&Path.GetFullPath(GameIO.GetPlayerDataDir())==root&&
                ReferenceEquals(world.GetPrimaryPlayer(),player)&&ReferenceEquals(world.GetEntity(player.entityId),player)&&
                player.IsSpawned()&&!player.IsDead()&&player.Buffs!=null&&RebirthSurvivorMode.IsEnabledForCurrentWorld()&&
                !RebirthCharacterCreationHoldService.IsHeld(player)&&RebirthGearOwnerReservation.MatchesUnpreparedIntent(player,session,intent)&&
                ReferenceEquals(manager,SingletonMonoBehaviour<ConnectionManager>.Instance)&&manager!=null&&!manager.IsServer&&
                manager.connectionToServer!=null&&manager.connectionToServer.Length>0&&manager.connectionToServer[0]!=null&&
                ReferenceEquals(manager.connectionToServer[0],session)&&!manager.connectionToServer[0].IsDisconnected()&&
                game.getPersistentPlayerID(null)?.CombinedString==owner.CanonicalId&&
                RebirthGearPreparationRefusalClient.TryGetCurrent(player,session,out var accepted)&&XNode.DeepEquals(accepted.Write(),refusal.Write())&&
                (!retiring||RebirthGearPreparationRefusalClient.TryGetAcknowledged(player,session,out var ack)&&XNode.DeepEquals(ack.Write(),refusal.Write()))&&
                RebirthGearInventorySnapshot.TryCapture(player.bag?.ItemGrid?.items,player.inventory?.ItemGrid?.items,owned,out var live)&&intent.MatchesInventory(live);
            if(!current())return false;
            bool hasMarker=false;
            foreach(string key in player.Buffs.CVars.Keys)
            {
                if(!key.StartsWith("_rbgearintent_",StringComparison.OrdinalIgnoreCase))continue;
                if(key!=marker||player.Buffs.GetCustomVar(key)!=1f)return false;
                hasMarker=true;
            }
            float phase=player.Buffs.GetCustomVar(receipt);
            if(phase!=0f&&phase!=-1f)return false;
            if(!hasMarker&&(!retiring||phase!=-1f))return false;
            Func<bool> savedEndpoint=()=>retiring
                ?RebirthGearPreparationRefusalPlayerFileWitness.HasRetired(owner,refusal)
                :RebirthGearPreparationRefusalPlayerFileWitness.HasRejectedOriginal(owner,refusal);
            if(savedEndpoint())
            {
                if(!current())return false;
                if(phase!=-1f)player.Buffs.SetCustomVar(receipt,-1f,false);
                if(retiring&&hasMarker)player.Buffs.RemoveCustomVar(marker);
                return current();
            }
            if(!RebirthGearPreparationPlayerFileWitness.TryReadOriginalPhase(owner,savedWorld,out var savedMarker,out var savedIntent,out var savedImage,out var savedPhase)||
                savedMarker!=marker||(retiring?savedPhase!=-1f:savedPhase!=0f&&savedPhase!=-1f)||
                !XNode.DeepEquals(savedIntent.Write(),intent.Write())||!intent.MatchesInventory(savedImage)||!current())return false;
            if(phase!=-1f)player.Buffs.SetCustomVar(receipt,-1f,false);
            // Keep the original marker through rejected save and server ACK for cold restore.
            if(retiring&&hasMarker)player.Buffs.RemoveCustomVar(marker);
            if(!current())return false;
            game.SaveLocalPlayerData();
            return current()&&savedEndpoint()&&current();
        }
        catch{return false;}
    }
}
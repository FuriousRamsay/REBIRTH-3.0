using System;
using System.IO;
using System.Xml.Linq;

// Original bound terminal only. No item writes or hold release. An uncertain
// native save keeps the original hold; retry saves the same absence checkpoint.
internal static class RebirthGearTerminalOwnerCheckpoint
{
    internal static bool HasSavedRetirement(EntityPlayerLocal player,RebirthGearTransferState offer,bool applied)
    {
        try
        {
            return offer?.PreparationRequestDigest!=null&&ThreadManager.IsMainThread()&&
                RebirthStablePlayerIdentity.TryFromLocalPlatform(out var owner)&&
                GameManager.Instance?.getPersistentPlayerID(null)?.CombinedString==owner.CanonicalId&&
                RebirthGearTerminalClient.TryGetCurrent(player,out var marker,out var terminal)&&
                terminal.Applied==applied&&terminal.TransactionId==offer.TransactionId&&terminal.CreationId==offer.CreationId&&
                terminal.GearRevision==offer.ExpectedRevision+1&&terminal.PreparationRequestDigest==offer.PreparationRequestDigest&&
                terminal.MatchesOriginalMarker(marker)&&RebirthGearPlayerFileWitness.HasRetired(owner,offer,applied);
        }
        catch{return false;}
    }
    internal static bool TryPersist(EntityPlayerLocal player,object session,RebirthGearTransferState offer)
    {
        if(player==null||session==null||offer?.PreparationRequestDigest==null||!ThreadManager.IsMainThread()||
            !RebirthStablePlayerIdentity.TryFromLocalPlatform(out var owner)||
            !RebirthGearTerminalClient.TryGetCurrent(player,out var marker,out var terminal)||
            !terminal.MatchesOriginalMarker(marker)||terminal.TransactionId!=offer.TransactionId||
            terminal.CreationId!=offer.CreationId||terminal.GearRevision!=offer.ExpectedRevision+1||
            terminal.PreparationRequestDigest!=offer.PreparationRequestDigest||!offer.TryGetPlan(out var plan))return false;
        var game=GameManager.Instance;var world=player.world;var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;
        string root,worldKey;
        try {root=Path.GetFullPath(GameIO.GetPlayerDataDir());worldKey=GamePrefs.GetString(EnumGamePrefs.GameGuidClient);}catch{return false;}
        Func<bool> current=()=>ThreadManager.IsMainThread()&&ReferenceEquals(game,GameManager.Instance)&&
            ReferenceEquals(game?.World,world)&&ReferenceEquals(player.world,world)&&world!=null&&world.IsRemote()&&
            ReferenceEquals(world.GetPrimaryPlayer(),player)&&ReferenceEquals(world.GetEntity(player.entityId),player)&&
            player.IsSpawned()&&!player.IsDead()&&player.Buffs!=null&&
            RebirthGearOwnerReservation.Matches(player,session,offer)&&
            ReferenceEquals(manager,SingletonMonoBehaviour<ConnectionManager>.Instance)&&manager!=null&&!manager.IsServer&&
            manager.connectionToServer!=null&&manager.connectionToServer.Length>0&&ReferenceEquals(manager.connectionToServer[0],session)&&
            manager.connectionToServer[0]!=null&&!manager.connectionToServer[0].IsDisconnected()&&
            GamePrefs.GetString(EnumGamePrefs.GameGuidClient)==worldKey&&Path.GetFullPath(GameIO.GetPlayerDataDir())==root&&
            game.getPersistentPlayerID(null)?.CombinedString==owner.CanonicalId&&
            RebirthGearTerminalClient.TryGetCurrent(player,out var cachedMarker,out var cachedTerminal)&&cachedMarker==marker&&
            XNode.DeepEquals(cachedTerminal.Write(),terminal.Write());
        try
        {
            if(!current()||player.Buffs.GetCustomVar("rbGear_"+offer.TransactionId)!=(terminal.Applied?1f:-1f)||
                !RebirthGearInventorySnapshot.TryCapture(player.bag?.ItemGrid?.items,player.inventory?.ItemGrid?.items,4,out var live)||
                !(terminal.Applied?plan.MatchesAppliedInventory(live.Bag,live.Belt):
                    plan.MatchesBefore(live.Bag,live.Belt,plan.GearBefore,live.Bag.Length,live.Belt.Length)))return false;
            bool hasMarker=false;
            foreach(string key in player.Buffs.CVars.Keys)
            {
                if(!key.StartsWith("_rbgearintent_",StringComparison.OrdinalIgnoreCase))continue;
                if(key!=marker||player.Buffs.GetCustomVar(key)!=1f)return false;
                hasMarker=true;
            }
            // First attempt requires same original saved receipt/postimage; a retry
            // can recognize the exact already-retired file without a second move.
            if(RebirthGearPlayerFileWitness.HasRetired(owner,offer,terminal.Applied))
            {
                if(hasMarker){if(!current())return false;player.Buffs.RemoveCustomVar(marker);}
                return current();
            }
            if(!(terminal.Applied?RebirthGearPlayerFileWitness.HasApplied(owner,offer):
                RebirthGearPlayerFileWitness.HasRejected(owner,offer))||!current())return false;
            if(hasMarker)player.Buffs.RemoveCustomVar(marker);
            if(!current())return false;
            game.SaveLocalPlayerData();
            return current()&&RebirthGearPlayerFileWitness.HasRetired(owner,offer,terminal.Applied)&&current();
        }
        catch{return false;}
    }
}
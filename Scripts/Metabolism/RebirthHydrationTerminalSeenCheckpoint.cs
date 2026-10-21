using System;
using System.IO;

// Persist terminal reception while the exact original native hold still exists.
// This marker describes a received server terminal, not financial replay rights.
internal static class RebirthHydrationTerminalSeenCheckpoint
{
    internal static string Marker(RebirthHydrationTransferOriginal original)
        =>"rbHydrationSeen_"+original.Operation.ToString("N")+"_"+original.Digest;
    internal static bool TryPersist(RebirthHydrationRemoteClientTerminal terminal)
    {
        try
        {
            var grant=terminal?.Grant;
            if(grant==null||!terminal.CurrentFor(grant)||!RebirthHydrationOwnerCheckpoint.HasSaved(grant.Player,grant.Original,RebirthGearOwnerReceipt.Applied))return false;
            var player=grant.Player;string marker=Marker(grant.Original);
            float previous=player.Buffs.GetCustomVar(marker);if(previous!=0f&&previous!=1f)return false;
            if(previous==1f&&HasSaved(grant))return true;
            player.Buffs.SetCustomVar(marker,1f,true);
            if(!terminal.CurrentFor(grant)||player.Buffs.GetCustomVar(marker)!=1f)return false;
            GameManager.Instance.SaveLocalPlayerData();
            return HasSaved(grant);
        }
        catch{return false;}
    }
    internal static bool HasSaved(RebirthHydrationRemoteClientGrant grant)
    {
        try
        {
            if(grant==null||!grant.Current()||!grant.Original.TryGetPlan(out var plan)||
                !RebirthStablePlayerIdentity.TryFromLocalPlatform(out var owner)||owner.StorageKey!=grant.Original.Owner||
                !RebirthGearNativePlayerFile.TryRead(owner,out var file)||file.deaths!=grant.Original.Deaths||
                file.buffData==null||file.buffData.Length<1||file.buffData.Length>1024*1024)return false;
            var original=grant.Original;byte[] buffs=file.buffData.ToArray();
            if(!RebirthGearReceiptReader.TryReadHydrationSeenStage(buffs,original.Operation,original.Digest,out var seen)||seen!=1f||
                !RebirthGearReceiptReader.TryReadHydrationStage(buffs,original.Operation,original.Digest,out var hydration)||hydration!=1f||
                !RebirthGearReceiptReader.Contains(buffs,original.Operation.ToString("N"),RebirthGearOwnerReceipt.Applied))return false;
            var bag=RebirthGearPlayerDataInventory.ReadSlots(file,true);var belt=RebirthGearPlayerDataInventory.ReadSlots(file,false);
            return RebirthGearInventorySnapshot.TryCapture(bag,belt,original.OwnedBelt,out var saved)&&
                plan.MatchesAppliedInventory(saved.Bag,saved.Belt)&&grant.Current();
        }
        catch{return false;}
    }
    // Historical acknowledgment evidence only. No grant/hold acquisition or
    // inventory replay is permitted by this observation after owner release.
    internal static bool HasSavedHistorical(EntityPlayerLocal player,RebirthHydrationTransferOriginal original)
    {
        try
        {
            if(!HistoricalScope(player,original,out var peer)||!RebirthStablePlayerIdentity.TryFromLocalPlatform(out var owner))return false;
            var game=GameManager.Instance;var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;var world=player.world;
            string path=PlayerPath();
            if(!RebirthGearNativePlayerFile.TryRead(owner,out var file)||file.deaths!=original.Deaths||
                file.buffData==null||file.buffData.Length<1||file.buffData.Length>1024*1024)return false;
            byte[] buffs=file.buffData.ToArray();
            return RebirthGearReceiptReader.TryReadHydrationSeenStage(buffs,original.Operation,original.Digest,out var seen)&&seen==1f&&
                RebirthGearReceiptReader.TryReadHydrationStage(buffs,original.Operation,original.Digest,out var hydration)&&hydration==1f&&
                RebirthGearReceiptReader.Contains(buffs,original.Operation.ToString("N"),RebirthGearOwnerReceipt.Applied)&&
                ReferenceEquals(game,GameManager.Instance)&&ReferenceEquals(manager,SingletonMonoBehaviour<ConnectionManager>.Instance)&&
                ReferenceEquals(world,player.world)&&path==PlayerPath()&&HistoricalScope(player,original,out var current)&&ReferenceEquals(peer,current);
        }
        catch{return false;}
    }
    private static bool HistoricalScope(EntityPlayerLocal player,RebirthHydrationTransferOriginal original,out object peer)
    {
        peer=null;var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;
        if(!ThreadManager.IsMainThread()||original==null||player?.world?.IsRemote()!=true||manager==null||manager.IsServer||
            !ReferenceEquals(player.world,GameManager.Instance?.World)||!ReferenceEquals(player.world.GetPrimaryPlayer(),player)||
            !ReferenceEquals(player.world.GetEntity(player.entityId),player)||player.IsDead()||!player.IsSpawned()||
            !RebirthHydrationTransferScope.TryCapture(player,out var creation)||
            !RebirthStablePlayerIdentity.TryFromLocalPlatform(out var owner)||
            !Guid.TryParse(GamePrefs.GetString(EnumGamePrefs.GameGuidClient),out var world)||!original.Matches(world,owner.StorageKey,creation,player.Died))return false;
        var peers=manager.connectionToServer;
        if(peers==null||peers.Length==0||peers[0]==null||peers[0].IsDisconnected())return false;
        peer=peers[0];return true;
    }
    private static string PlayerPath()
    {string path=GameIO.GetPlayerDataDir();if(string.IsNullOrWhiteSpace(path))throw new InvalidDataException("Native hydration owner path unavailable");return Path.GetFullPath(path);}
}

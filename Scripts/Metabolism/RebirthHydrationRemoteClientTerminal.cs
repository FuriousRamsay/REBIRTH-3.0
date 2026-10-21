using System;
using System.Collections.Generic;

// Only the current authenticated ToClient terminal receiver constructs this
// warm release capability. Receipt parsing alone never releases native custody.
internal sealed partial class RebirthHydrationRemoteClientTerminal
{
    private sealed class Seen
    {
        internal EntityPlayerLocal Player;internal World World;internal GameManager Game;internal ConnectionManager Manager;
        internal object Peer;internal RebirthHydrationTransferOriginal Original;internal bool Released;
    }
    private static readonly Dictionary<Guid,Seen> seen=new Dictionary<Guid,Seen>();
    internal readonly RebirthHydrationRemoteClientGrant Grant;
    private RebirthHydrationRemoteClientTerminal(RebirthHydrationRemoteClientGrant grant){Grant=grant;}
    internal static bool TryRestoreHistorical(EntityPlayerLocal player,RebirthHydrationTransferOriginal original)
    {
        try
        {
            if(player==null||original==null||!RebirthHydrationTerminalSeenCheckpoint.HasSavedHistorical(player,original))return false;
            if(TryGetAcknowledgment(player,original.Operation,original.Digest,out _))return RebirthHydrationOwnerHold.RetireHistoricalRemote(player,original);
            if(seen.TryGetValue(original.Operation,out var old)&&old.Original.Digest!=original.Digest||seen.Count>=64&&!seen.ContainsKey(original.Operation))return false;
            var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;
            var next=new Seen{Player=player,World=player.world,Game=GameManager.Instance,Manager=manager,Peer=manager.connectionToServer[0],Original=original,Released=true};
            if(!RebirthHydrationTerminalSeenCheckpoint.HasSavedHistorical(player,original))return false;
            seen[original.Operation]=next;
            return TryGetAcknowledgment(player,original.Operation,original.Digest,out _)&&RebirthHydrationOwnerHold.RetireHistoricalRemote(player,original);
        }
        catch{return false;}
    }
    internal bool CurrentFor(RebirthHydrationRemoteClientGrant grant)
    {
        try
        {
            if(!ReferenceEquals(Grant,grant)||!grant.Current()||!grant.Original.TryGetPlan(out var plan)||
                !RebirthHydrationOwnerCheckpoint.HasSaved(grant.Player,grant.Original,RebirthGearOwnerReceipt.Applied)||
                !RebirthGearInventorySnapshot.TryCapture(grant.Player.bag?.ItemGrid?.items,grant.Player.inventory?.ItemGrid?.items,
                    grant.Original.OwnedBelt,out var live))return false;
            return plan.MatchesAppliedInventory(live.Bag,live.Belt)&&grant.Current();
        }
        catch{return false;}
    }
    internal static bool Receive(World world,int playerId,Guid operation,string digest)
    {
        try
        {
            if(!ThreadManager.IsMainThread()||world?.IsRemote()!=true||!ReferenceEquals(world,GameManager.Instance?.World))return false;
            var player=world.GetPrimaryPlayer();
            if(player==null||player.entityId!=playerId)return false;
            if(TryGetAcknowledgment(player,operation,digest,out _))return true;
            if(player==null||player.entityId!=playerId||!RebirthHydrationRemoteClientGrant.TryGet(player,out var grant)||
                grant.Original.Operation!=operation||grant.Original.Digest!=digest||!grant.Current()||
                !RebirthHydrationOwnerCheckpoint.TryPersist(player,grant.Original,RebirthGearOwnerReceipt.Applied))return false;
            var terminal=new RebirthHydrationRemoteClientTerminal(grant);
            if(!terminal.CurrentFor(grant)||!RebirthHydrationTerminalSeenCheckpoint.TryPersist(terminal))return false;
            if(!seen.TryGetValue(operation,out var retained))
            {
                var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;
                if(seen.Count>=64)return false;
                retained=new Seen{Player=player,World=world,Game=GameManager.Instance,Manager=manager,Peer=manager.connectionToServer[0],Original=grant.Original};
                seen.Add(operation,retained);
            }
            if(!ReferenceEquals(retained.Player,player)||retained.Original.Digest!=digest||retained.Released||
                !RebirthHydrationOwnerHold.TryReleaseRemoteClient(terminal))return false;
            retained.Released=true;return true;
        }
        catch{return false;}
    }
    // Warm duplicate/acknowledgment description only. Cold recovery requires
    // independent saved terminal-seen proof and is not inferred from this cache.
    internal static bool TryGetAcknowledgment(EntityPlayerLocal player,Guid operation,string digest,out RebirthHydrationTransferOriginal original)
    {
        original=null;
        try
        {
            if(!ThreadManager.IsMainThread()||player==null||!seen.TryGetValue(operation,out var retained)||!retained.Released||
                !ReferenceEquals(player,retained.Player)||retained.Original.Digest!=digest||!ReferenceEquals(player.world,retained.World)||
                !ReferenceEquals(GameManager.Instance,retained.Game)||!ReferenceEquals(GameManager.Instance?.World,retained.World)||
                !retained.World.IsRemote()||!ReferenceEquals(retained.World.GetPrimaryPlayer(),player)||!ReferenceEquals(retained.World.GetEntity(player.entityId),player)||
                player.IsDead()||!player.IsSpawned()||player.Died!=retained.Original.Deaths||
                !ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance,retained.Manager)||retained.Manager.IsServer||
                retained.Manager.connectionToServer==null||retained.Manager.connectionToServer.Length==0||
                !ReferenceEquals(retained.Manager.connectionToServer[0],retained.Peer)||retained.Manager.connectionToServer[0].IsDisconnected()||
                !RebirthHydrationTransferScope.TryCapture(player,out var creation)||!RebirthSurvivorRequestScope.Matches(creation,retained.Original.Creation)||
                !RebirthStablePlayerIdentity.TryFromLocalPlatform(out var owner)||owner.StorageKey!=retained.Original.Owner)return false;
            original=retained.Original;return true;
        }
        catch{return false;}
    }
}

using System;
using System.Collections.Generic;

// Constructed only by the authenticated server-to-client packet receiver and
// the exact already-held, saved original. Receiving never applies inventory.
internal sealed partial class RebirthHydrationRemoteClientGrant
{
    private static readonly Dictionary<Guid,RebirthHydrationRemoteClientGrant> issued=new Dictionary<Guid,RebirthHydrationRemoteClientGrant>();
    internal static bool HasIssued(Guid operation)=>issued.ContainsKey(operation);
    internal readonly EntityPlayerLocal Player;internal readonly RebirthHydrationTransferOriginal Original;
    private readonly World world;private readonly ConnectionManager manager;private readonly object peer;
    private RebirthHydrationRemoteClientGrant(EntityPlayerLocal player,RebirthHydrationTransferOriginal original,object peer)
    {Player=player;Original=original;world=player.world;manager=SingletonMonoBehaviour<ConnectionManager>.Instance;this.peer=peer;}
    internal bool Current()
    {
        try{return issued.TryGetValue(Original.Operation,out var grant)&&ReferenceEquals(grant,this)&&
            Resolve(world,Player.entityId,out var player,out var currentPeer)&&ReferenceEquals(player,Player)&&ReferenceEquals(currentPeer,peer)&&
            ReferenceEquals(manager,SingletonMonoBehaviour<ConnectionManager>.Instance)&&RebirthHydrationOwnerHold.Matches(Player,Original);}
        catch{return false;}
    }
    internal static bool Receive(World world,int playerId,Guid operation,string digest)
    {
        try
        {
            if(operation==Guid.Empty||!Resolve(world,playerId,out var player,out var peer)||
                !RebirthHydrationOwnerHold.TryGetRemoteOriginal(player,out var original)||original.Operation!=operation||original.Digest!=digest)return false;
            if(issued.TryGetValue(operation,out var retained))
            {
                if(ReferenceEquals(retained.Player,player)&&retained.Original.Digest==digest&&retained.Current())return true;
                if(retained.Original.Digest!=digest||retained.Current())return false;
                // The new native owner is independently saved under this exact
                // original. Retire only the revoked warm capability below.
                if(!RebirthHydrationOwnerCheckpoint.HasSavedBefore(player,original)&&
                    !RebirthHydrationOwnerCheckpoint.HasSaved(player,original,RebirthGearOwnerReceipt.Applied))return false;
                issued.Remove(operation);
            }
            if(issued.Count>=64||player.Buffs==null||player.Buffs.GetCustomVar("rbHydrationRefused_"+operation.ToString("N")+"_"+digest)!=0f||
                !RebirthHydrationOwnerCheckpoint.HasSavedBefore(player,original)&&!RebirthHydrationOwnerCheckpoint.HasSaved(player,original,RebirthGearOwnerReceipt.Applied)||
                !Resolve(world,playerId,out var latest,out var latestPeer)||!ReferenceEquals(latest,player)||!ReferenceEquals(latestPeer,peer)||
                !RebirthHydrationOwnerHold.Matches(player,original))return false;
            var next=new RebirthHydrationRemoteClientGrant(player,original,peer);issued.Add(operation,next);return next.Current();
        }
        catch{return false;}
    }
    internal static bool TryGet(EntityPlayerLocal player,out RebirthHydrationRemoteClientGrant grant)
    {
        grant=null;
        if(!RebirthHydrationOwnerHold.TryGetRemoteOriginal(player,out var original)||!issued.TryGetValue(original.Operation,out var retained)||
            !ReferenceEquals(retained.Player,player)||!retained.Current())return false;
        grant=retained;return true;
    }
    private static bool Resolve(World world,int id,out EntityPlayerLocal player,out object peer)
    {
        player=null;peer=null;var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;
        if(!ThreadManager.IsMainThread()||world==null||!world.IsRemote()||!ReferenceEquals(GameManager.Instance?.World,world)||manager==null||manager.IsServer)return false;
        player=world.GetPrimaryPlayer();var peers=manager.connectionToServer;
        if(player==null||player.entityId!=id||!ReferenceEquals(player.world,world)||!ReferenceEquals(world.GetEntity(id),player)||
            player.IsDead()||!player.IsSpawned()||peers==null||peers.Length==0||peers[0]==null||peers[0].IsDisconnected())return false;
        peer=peers[0];return true;
    }
}

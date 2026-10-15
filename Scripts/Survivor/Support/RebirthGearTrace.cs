using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Newtonsoft.Json.Linq;

// Explicit diagnostic ledger only. No inventory/blob/token/path logging and no
// gameplay authority. All normal production calls are inert while disabled.
internal static class RebirthGearTrace
{
    private static readonly object sync=new object();
    private static readonly Queue<JObject> events=new Queue<JObject>();
    private static bool enabled;
    private static long sequence;
    internal static bool Enabled {get{lock(sync)return enabled;}}
    internal static void Configure(bool value,bool clear)
    {lock(sync){enabled=value;if(clear)events.Clear();}}
    internal static JObject Snapshot()
    {lock(sync){var rows=new JArray();foreach(var row in events)rows.Add(row.DeepClone());return new JObject{["enabled"]=enabled,["lastSequence"]=sequence,["events"]=rows};}}
    internal static void Note(EntityPlayer player,string transaction,string creation,string stage,bool? success=null,ClientInfo sender=null,JObject witness=null)
    {
        lock(sync)if(!enabled)return;
        try
        {
            player=player??GameManager.Instance?.World?.GetPrimaryPlayer();
            var world=player?.world;var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;
            var peer=manager?.connectionToServer;
            object connection=sender??(peer!=null&&peer.Length>0?(object)peer[0]:null);
            var local=player as EntityPlayerLocal;
            string projected=player==null?null:RebirthSurvivorClientState.GetProjectedCreationId(player);
            string owner=null;
            if(sender!=null&&RebirthStablePlayerIdentity.TryFromClientInfo(sender,out var remoteIdentity))owner=remoteIdentity?.StorageKey;
            else if(local!=null&&RebirthStablePlayerIdentity.TryFromLocalPlatform(out var identity))owner=identity?.StorageKey;
            var row=new JObject{["utc"]=DateTime.UtcNow.ToString("o"),["stage"]=stage,
                ["transactionId"]=transaction,["creationId"]=creation,["projectedCreationId"]=projected,
                ["ownerEntityId"]=player?.entityId??-1,["ownerKey"]=owner,
                ["worldKey"]=world?.worldState?.Guid,
                ["savedWorldKey"]=GamePrefs.GetString(EnumGamePrefs.GameGuidClient),
                ["worldScope"]=world==null?0:RuntimeHelpers.GetHashCode(world),
                ["sessionScope"]=connection==null?0:RuntimeHelpers.GetHashCode(connection),
                ["remote"]=world?.IsRemote()??false,
                ["currentOwner"]=player!=null&&ReferenceEquals(GameManager.Instance?.World,world)&&
                    ReferenceEquals(world.GetPrimaryPlayer(),player)&&ReferenceEquals(world.GetEntity(player.entityId),player),
                ["currentEntity"]=player!=null&&ReferenceEquals(GameManager.Instance?.World,world)&&ReferenceEquals(world.GetEntity(player.entityId),player),
                ["authenticatedSender"]=sender!=null&&manager?.Clients!=null&&player!=null&&ReferenceEquals(manager.Clients.ForEntityId(player.entityId),sender),
                ["gearHeld"]=RebirthGearOwnerReservation.IsHeld(local),
                ["libraryHeld"]=local!=null&&RebirthBackpackLibraryReservation.IsHeld(local),
                ["resourcePending"]=local!=null&&RemoteResourceClientTransactionCoordinator.HasPendingInventoryOperation(local)};
            if(success.HasValue)row["success"]=success.Value;
            if(witness!=null)row["savedOwnerMismatch"]=witness.DeepClone();
            if(player?.Buffs!=null&&Guid.TryParseExact(transaction,"N",out var tx))
                row["receipt"]=player.Buffs.GetCustomVar("rbGear_"+tx.ToString("N"));
            lock(sync){if(!enabled)return;row["sequence"]=++sequence;events.Enqueue(row);while(events.Count>192)events.Dequeue();}
        }
        catch{ } // Observation cannot change or prevent original transfer behavior.
    }
    internal static void Note(RebirthGearTransferState offer,string stage,bool? success=null)
        =>Note(null,offer?.TransactionId,offer?.CreationId,stage,success);
    internal static void Witness(RebirthGearTransferState offer,string checkpoint,string predicate,string section,
        int actualShape=-1,int expectedShape=-1,int index=-1,string actualHash=null,string expectedHash=null,string savedOwnerKey=null)
    {
        if(!Enabled)return;
        Note(null,offer?.TransactionId,offer?.CreationId,"saved-owner."+checkpoint,false,null,
            new JObject{["savedOwnerKey"]=savedOwnerKey,["predicate"]=predicate,["section"]=section,["actualShape"]=actualShape,
                ["expectedShape"]=expectedShape,["index"]=index,["actualHash"]=actualHash,["expectedHash"]=expectedHash});
    }
}


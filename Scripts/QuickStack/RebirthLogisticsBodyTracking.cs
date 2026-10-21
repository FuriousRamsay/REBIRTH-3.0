using System;
using System.Linq;

// Native same-body tracking prerequisite. Never AddDroppedBackpack: it can
// evict a different protected body at the native three-entry limit.
internal static class RebirthLogisticsBodyTracking
{
    internal sealed class Entry
    {
        internal readonly int Entity;
        internal readonly Vector3i Position;
        internal readonly uint Timestamp;
        internal readonly string Owner;
        internal readonly PersistentPlayerData Row;
        internal Entry(string owner,PersistentPlayerData row,PersistentPlayerData.ProtectedBackpack value)
        {Owner=owner;Row=row;Entity=value.EntityID;Position=value.Position;Timestamp=value.Timestamp;}
    }
    internal static bool TryObserve(EntityPlayer actor,ClientInfo sender,string owner,EntityBackpack exactBody,out Entry entry)
    {
        entry=null;
        try
        {
            if(!RebirthLogisticsBodyReferenceWitness.TryCurrentNativeOwner(actor,sender,owner,out _)||exactBody==null||
                !ReferenceEquals(exactBody.world,actor.world)||!ReferenceEquals(actor.world.GetEntity(exactBody.entityId),exactBody))return false;
            var row=GameManager.Instance.GetPersistentPlayerList().GetPlayerData(sender.InternalId);
            if(row.backpacksByID==null||row.backpacksByID.Count>3||!row.backpacksByID.TryGetValue(exactBody.entityId,out var value)||value.EntityID!=exactBody.entityId)return false;
            // Exactly the current primary record owns this native tracking ID.
            if(GameManager.Instance.GetPersistentPlayerList().Players.Values.Count(p=>p.backpacksByID!=null&&p.backpacksByID.ContainsKey(exactBody.entityId))!=1)return false;
            entry=new Entry(owner,row,value);return true;
        }
        catch{return false;}
    }
    internal static bool IsUnchanged(Entry original,EntityPlayer actor,ClientInfo sender,EntityBackpack exactBody)
    {
        return original!=null&&TryObserve(actor,sender,original.Owner,exactBody,out var current)&&ReferenceEquals(original.Row,current.Row)&&
            current.Entity==original.Entity&&current.Timestamp==original.Timestamp&&current.Position.Equals(original.Position);
    }
    // Called only after a separately proven same-body native reference repair.
    // Reuses native notification WITHOUT inserting/removing/reordering timestamps.
    internal static bool NotifyUnchanged(Entry original,EntityPlayer actor,ClientInfo sender,EntityBackpack exactBody)
    {
        if(!IsUnchanged(original,actor,sender,exactBody)||exactBody.RefPlayerId!=actor.entityId||!RebirthLogisticsOriginalTransport.CanSendNative(sender.netConnection?[0]))return false;
        original.Row.RefreshSortedBackpacksList();
        if(!IsUnchanged(original,actor,sender,exactBody)||original.Row.backpacksSortedByTimestamp.Count!=original.Row.backpacksByID.Count||
            original.Row.backpacksSortedByTimestamp.Any(value=>!original.Row.backpacksByID.TryGetValue(value.EntityID,out var entry)||entry.Timestamp!=value.Timestamp||!entry.Position.Equals(value.Position)))return false;
        var positions=original.Row.GetDroppedBackpackPositions();if(positions.Count>3||positions.Count!=original.Row.backpacksByID.Count)return false;
        SingletonMonoBehaviour<ConnectionManager>.Instance.SendPackage(NetPackageManager.GetPackage<NetPackagePlayerSetBackpackPosition>().Setup(actor.entityId,positions),_onlyClientsAttachedToAnEntity:false,actor.entityId);
        return true;
    }
}

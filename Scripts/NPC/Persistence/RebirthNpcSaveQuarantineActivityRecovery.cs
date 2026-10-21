using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml;

public static partial class RebirthNpcActivityRecoveryRegistry
{
    private static World activityRecoveryWorld;
    private static bool ActivityRecoveryScope()
    {
        var world=GameManager.Instance?.World;
        if(!ThreadManager.IsMainThread()||world?.IsRemote()!=false||SingletonMonoBehaviour<ConnectionManager>.Instance?.IsServer!=true||RebirthNpcPersistenceCoordinator.IsRecoveryInstalling)return false;
        lock(Sync){if(activityRecoveryWorld!=null&&!ReferenceEquals(activityRecoveryWorld,world))return false;if(activityRecoveryWorld==null)activityRecoveryWorld=world;return true;}
    }
    internal static bool CanRetireEmptyActivityRecovery
    {get{lock(Sync)return ThreadManager.IsMainThread()&&PendingByEntity.Count==0&&ByRecoveryId.Count==0&&HistoryOrder.Count==0&&AttemptsByLease.Count==0&&PendingOrder.Count==0&&PendingNodes.Count==0&&recoveryCursor==null;}}
    internal static void AppendQuarantineIds(HashSet<RebirthNpcStableId> ids)
    {
        lock(Sync)foreach(var ticket in PendingByEntity.Values)
        {if(ticket==null||activityRecoveryWorld?.GetEntity(ticket.TargetEntityId) is not EntityRebirthNPC npc||npc.RebirthRuntimeState==null)throw new InvalidOperationException("Activity original native identity unavailable");RebirthNpcSaveQuarantineIds.Add(ids,npc.RebirthRuntimeState.StableId);}
    }
    internal static RebirthNpcSaveQuarantineDomain PrepareActivityRecoveryQuarantine(RebirthNpcSaveQuarantineScope scope)
    {
        lock(Sync)
        {
            if(!ThreadManager.IsMainThread()||scope==null||(activityRecoveryWorld!=null&&!ReferenceEquals(activityRecoveryWorld,scope.World))||(activityRecoveryWorld==null&&!CanRetireEmptyActivityRecovery))throw new InvalidOperationException("Activity original binding unavailable");
            var pending=PendingByEntity;var records=ByRecoveryId;var history=HistoryOrder;var attempts=AttemptsByLease;var order=PendingOrder;var nodes=PendingNodes;var cursor=recoveryCursor;var world=activityRecoveryWorld;ulong allocator=nextRecoveryId;
            var nextPending=new Dictionary<int,RebirthNpcActivityRecoveryTicket>();var nextRecords=new Dictionary<ulong,RebirthNpcActivityRecoveryTicket>();var nextHistory=new Queue<ulong>();var nextAttempts=new Dictionary<ulong,int>();var nextOrder=new LinkedList<ulong>();var nextNodes=new Dictionary<ulong,LinkedListNode<ulong>>();var doc=ActivityRecoveryImage();
            return new RebirthNpcSaveQuarantineDomain("activity-recovery",doc,
                ()=>{lock(Sync)return ReferenceEquals(PendingByEntity,pending)&&ReferenceEquals(ByRecoveryId,records)&&ReferenceEquals(HistoryOrder,history)&&ReferenceEquals(AttemptsByLease,attempts)&&ReferenceEquals(PendingOrder,order)&&ReferenceEquals(PendingNodes,nodes)&&ReferenceEquals(recoveryCursor,cursor)&&ReferenceEquals(activityRecoveryWorld,world)&&nextRecoveryId==allocator&&ActivityRecoveryImage().OuterXml==doc.OuterXml;},
                ()=>{lock(Sync){PendingByEntity=nextPending;ByRecoveryId=nextRecords;HistoryOrder=nextHistory;AttemptsByLease=nextAttempts;PendingOrder=nextOrder;PendingNodes=nextNodes;recoveryCursor=null;activityRecoveryWorld=null;/* allocator stays process-monotone */}},
                ()=>{lock(Sync){PendingByEntity=pending;ByRecoveryId=records;HistoryOrder=history;AttemptsByLease=attempts;PendingOrder=order;PendingNodes=nodes;recoveryCursor=cursor;activityRecoveryWorld=world;nextRecoveryId=allocator;}});
        }
    }
    private static XmlDocument ActivityRecoveryImage()
    {
        if(ByRecoveryId.Count>8192||PendingByEntity.Count>8192||AttemptsByLease.Count>8192||PendingOrder.Count>8192||HistoryOrder.Count>8192||PendingNodes.Count!=PendingOrder.Count)throw new InvalidOperationException("Activity retention bound or index unavailable");
        long characters=0;foreach(var ticket in ByRecoveryId.Values){if(ticket==null)throw new InvalidOperationException("Activity original missing");characters+=(ticket.Detail?.Length??0)+(long)(ticket.ControllerId?.Length??0);if(characters>4L*1024*1024)throw new InvalidOperationException("Activity text retention bound exceeded");}
        var doc=new XmlDocument{XmlResolver=null};var root=doc.CreateElement("activityRecovery");doc.AppendChild(root);Attr(root,"next",nextRecoveryId);Attr(root,"cursor",recoveryCursor?.Value??0);
        var keys=new List<ulong>(ByRecoveryId.Keys);keys.Sort();foreach(ulong id in keys)
        {
            var t=ByRecoveryId[id];if(t.RecoveryId!=id)throw new InvalidOperationException("Activity key mismatch");var row=doc.CreateElement("ticket");root.AppendChild(row);Attr(row,"id",id);Attr(row,"lease",t.LeaseId);Attr(row,"target",t.TargetEntityId);Attr(row,"order",(byte)t.Order);Attr(row,"outcome",(byte)t.Outcome);Attr(row,"status",(byte)t.Status);Attr(row,"attempt",t.Attempt);Attr(row,"maximum",t.MaximumAttempts);Text(row,"controller",t.ControllerId);Text(row,"detail",t.Detail);Attr(row,"created",t.CreatedUtcTicks);Attr(row,"notBefore",t.NotBeforeUtcTicks);Attr(row,"expires",t.ExpiresUtcTicks);Attr(row,"updated",t.UpdatedUtcTicks);
        }
        var entities=new List<int>(PendingByEntity.Keys);entities.Sort();foreach(int id in entities){var ticket=PendingByEntity[id];if(ticket==null||ticket.TargetEntityId!=id||!ByRecoveryId.TryGetValue(ticket.RecoveryId,out var record)||!ReferenceEquals(ticket,record))throw new InvalidOperationException("Activity pending index mismatch");var row=doc.CreateElement("pending");root.AppendChild(row);Attr(row,"entity",id);Attr(row,"ticket",ticket.RecoveryId);}
        var leases=new List<ulong>(AttemptsByLease.Keys);leases.Sort();foreach(ulong id in leases){var row=doc.CreateElement("attempts");root.AppendChild(row);Attr(row,"lease",id);Attr(row,"count",AttemptsByLease[id]);}
        foreach(ulong id in PendingOrder){if(!PendingNodes.TryGetValue(id,out var node)||node.List!=PendingOrder||node.Value!=id)throw new InvalidOperationException("Activity traversal index mismatch");var row=doc.CreateElement("order");root.AppendChild(row);Attr(row,"id",id);}
        if(recoveryCursor!=null&&recoveryCursor.List!=PendingOrder)throw new InvalidOperationException("Activity cursor foreign");foreach(ulong id in HistoryOrder){var row=doc.CreateElement("history");root.AppendChild(row);Attr(row,"id",id);}return doc;
    }
    private static void Attr(XmlElement row,string name,IFormattable value)=>row.SetAttribute(name,value.ToString(null,CultureInfo.InvariantCulture));
    private static void Text(XmlElement row,string name,string value)
    {if((value?.Length??0)>65536)throw new InvalidOperationException("Activity row text exceeds retention bound");row.SetAttribute(name+"Null",value==null?"1":"0");row.SetAttribute(name,value??string.Empty);}
}

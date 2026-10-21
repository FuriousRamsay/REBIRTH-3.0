using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml;

public static partial class RebirthNpcActivityRegistry
{
    private static World activityWorld;
    private static bool ActivityOriginalScope()
    {
        var world=GameManager.Instance?.World;if(!ThreadManager.IsMainThread()||world?.IsRemote()!=false||SingletonMonoBehaviour<ConnectionManager>.Instance?.IsServer!=true||RebirthNpcPersistenceCoordinator.IsRecoveryInstalling)return false;
        lock(Sync){if(activityWorld!=null&&!ReferenceEquals(activityWorld,world))return false;if(activityWorld==null)activityWorld=world;return true;}
    }
    internal static bool CanRetireEmptyActivities
    {get{lock(Sync)return ThreadManager.IsMainThread()&&ActiveByEntity.Count==0&&ByActivityId.Count==0&&HistoryOrder.Count==0;}}
    internal static bool TryRetireEmptyActivityObservation()
    {lock(Sync){if(!CanRetireEmptyActivities)return false;activityWorld=null;nextWatchdogUtcTicks=0;return true;}}
    internal static void AppendQuarantineIds(HashSet<RebirthNpcStableId> ids)
    {
        lock(Sync)foreach(var activity in ActiveByEntity.Values)
        {if(activity==null||activityWorld?.GetEntity(activity.TargetEntityId) is not EntityRebirthNPC npc||npc.RebirthRuntimeState==null)throw new InvalidOperationException("Activity native identity unavailable");RebirthNpcSaveQuarantineIds.Add(ids,npc.RebirthRuntimeState.StableId);}
    }
    internal static RebirthNpcSaveQuarantineDomain PrepareActivityQuarantine(RebirthNpcSaveQuarantineScope scope)
    {
        lock(Sync)
        {
            if(!ThreadManager.IsMainThread()||scope==null||(activityWorld!=null&&!ReferenceEquals(activityWorld,scope.World))||(activityWorld==null&&!CanRetireEmptyActivities))throw new InvalidOperationException("Original activity binding unavailable");
            var active=ActiveByEntity;var records=ByActivityId;var history=HistoryOrder;var world=activityWorld;ulong allocator=nextActivityId;long watchdog=nextWatchdogUtcTicks;var doc=ActivityImage();
            var nextActive=new Dictionary<int,RebirthNpcActivityRecord>();var nextRecords=new Dictionary<ulong,RebirthNpcActivityRecord>();var nextHistory=new Queue<ulong>();
            return new RebirthNpcSaveQuarantineDomain("activity-runtime",doc,
                ()=>{lock(Sync)return ReferenceEquals(ActiveByEntity,active)&&ReferenceEquals(ByActivityId,records)&&ReferenceEquals(HistoryOrder,history)&&ReferenceEquals(activityWorld,world)&&nextActivityId==allocator&&nextWatchdogUtcTicks==watchdog&&ActivityImage().OuterXml==doc.OuterXml;},
                ()=>{lock(Sync){ActiveByEntity=nextActive;ByActivityId=nextRecords;HistoryOrder=nextHistory;activityWorld=null;nextWatchdogUtcTicks=0;}},
                ()=>{lock(Sync){ActiveByEntity=active;ByActivityId=records;HistoryOrder=history;activityWorld=world;nextActivityId=allocator;nextWatchdogUtcTicks=watchdog;}});
        }
    }
    private static XmlDocument ActivityImage()
    {
        if(ActiveByEntity.Count>8192||ByActivityId.Count>8448||HistoryOrder.Count>8192)throw new InvalidOperationException("Activity retention bound exceeded");long characters=0;
        foreach(var record in ByActivityId.Values){if(record==null)throw new InvalidOperationException("Activity original missing");characters+=(record.Detail?.Length??0)+(long)(record.ControllerId?.Length??0);if(characters>4L*1024*1024)throw new InvalidOperationException("Activity text retention bound exceeded");}
        var doc=new XmlDocument{XmlResolver=null};var root=doc.CreateElement("activities");doc.AppendChild(root);Attr(root,"next",nextActivityId);Attr(root,"watchdog",nextWatchdogUtcTicks);
        var keys=new List<ulong>(ByActivityId.Keys);keys.Sort();foreach(ulong id in keys)
        {var r=ByActivityId[id];if(r.ActivityId!=id)throw new InvalidOperationException("Activity original key mismatch");var row=doc.CreateElement("record");root.AppendChild(row);Attr(row,"id",r.ActivityId);Attr(row,"lease",r.LeaseId);Attr(row,"entity",r.TargetEntityId);Text(row,"controller",r.ControllerId);Attr(row,"order",(byte)r.Order);Attr(row,"revision",r.AcceptedRevision);Attr(row,"status",(byte)r.Status);Text(row,"detail",r.Detail);Attr(row,"started",r.StartedUtcTicks);Attr(row,"heartbeat",r.LastHeartbeatUtcTicks);Attr(row,"deadline",r.HeartbeatDeadlineUtcTicks);Attr(row,"updated",r.UpdatedUtcTicks);}
        var entities=new List<int>(ActiveByEntity.Keys);entities.Sort();foreach(int id in entities){var value=ActiveByEntity[id];if(value==null||value.TargetEntityId!=id||!ByActivityId.TryGetValue(value.ActivityId,out var original)||!ReferenceEquals(value,original))throw new InvalidOperationException("Activity native index mismatch");var row=doc.CreateElement("active");root.AppendChild(row);Attr(row,"entity",id);Attr(row,"activity",value.ActivityId);}
        foreach(ulong id in HistoryOrder){var row=doc.CreateElement("history");root.AppendChild(row);Attr(row,"id",id);}return doc;
    }
    private static void Attr(XmlElement row,string name,IFormattable value)=>row.SetAttribute(name,value.ToString(null,CultureInfo.InvariantCulture));
    private static void Text(XmlElement row,string name,string value)
    {if((value?.Length??0)>65536)throw new InvalidOperationException("Activity row text retention bound exceeded");row.SetAttribute(name+"Null",value==null?"1":"0");row.SetAttribute(name,value??string.Empty);}
}

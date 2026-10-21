using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml;

public static partial class RebirthNpcCommandGateway
{
    private static World commandWorld;
    private static bool TryCommandScope(out string error)
    {
        error="Original NPC command scope unavailable";var world=GameManager.Instance?.World;
        if(!ThreadManager.IsMainThread()||world?.IsRemote()!=false||SingletonMonoBehaviour<ConnectionManager>.Instance?.IsServer!=true)return false;
        lock(Sync){if(commandWorld!=null&&!ReferenceEquals(commandWorld,world))return false;if(commandWorld==null)commandWorld=world;}
        error=string.Empty;return true;
    }
    internal static bool CanRetireEmptyCommands
    {
        get{lock(Sync){if(!ThreadManager.IsMainThread()||pendingCount!=0||Records.Count!=0||RetryByDue.Count!=0||HistoryOrder.Count!=0)return false;foreach(var queue in PendingByPriority)if(queue.Count!=0)return false;return true;}}
    }
    internal static void AppendQuarantineIds(HashSet<RebirthNpcStableId> ids)
    {
        lock(Sync)foreach(var record in Records.Values)
        {
            if(record==null)throw new InvalidOperationException("Command original missing");
            if(IsTerminal(record.Status))continue;
            if(commandWorld==null||commandWorld.GetEntity(record.Request.TargetEntityId) is not EntityRebirthNPC npc||npc.RebirthRuntimeState==null)throw new InvalidOperationException("Pending command native identity unavailable");
            RebirthNpcSaveQuarantineIds.Add(ids,npc.RebirthRuntimeState.StableId);
        }
    }
    internal static RebirthNpcSaveQuarantineDomain PrepareCommandQuarantine(RebirthNpcSaveQuarantineScope scope)
    {
        lock(Sync)
        {
            if(!ThreadManager.IsMainThread()||scope==null||(commandWorld!=null&&!ReferenceEquals(commandWorld,scope.World))||(commandWorld==null&&!CanRetireEmptyCommands))throw new InvalidOperationException("Original command binding unavailable");
            var records=Records;var queues=PendingByPriority;var retries=RetryByDue;var history=HistoryOrder;var world=commandWorld;int count=pendingCount;ulong allocator=nextCommandId;
            var nextRecords=new Dictionary<ulong,RebirthNpcCommandRecord>();var nextQueues=new[]{new Queue<ulong>(),new Queue<ulong>(),new Queue<ulong>(),new Queue<ulong>()};var nextRetries=new SortedDictionary<long,Queue<ulong>>();var nextHistory=new Queue<ulong>();
            var actors=new Dictionary<int,EntityRebirthNPC>();foreach(var record in records.Values)if(!IsTerminal(record.Status))
            {if(world?.GetEntity(record.Request.TargetEntityId) is not EntityRebirthNPC npc)throw new InvalidOperationException("Original queued actor unavailable");actors[record.Request.TargetEntityId]=npc;}
            var doc=CommandQuarantineImage();
            return new RebirthNpcSaveQuarantineDomain("command-gateway",doc,
                ()=>{lock(Sync){if(!ReferenceEquals(Records,records)||!ReferenceEquals(PendingByPriority,queues)||!ReferenceEquals(RetryByDue,retries)||!ReferenceEquals(HistoryOrder,history)||!ReferenceEquals(commandWorld,world)||pendingCount!=count||nextCommandId!=allocator||CommandQuarantineImage().OuterXml!=doc.OuterXml)return false;foreach(var pair in actors)if(!ReferenceEquals(world.GetEntity(pair.Key),pair.Value))return false;return true;}},
                ()=>{lock(Sync){Records=nextRecords;PendingByPriority=nextQueues;RetryByDue=nextRetries;HistoryOrder=nextHistory;pendingCount=0;commandWorld=null;/* allocator remains process-monotone */}},
                ()=>{lock(Sync){Records=records;PendingByPriority=queues;RetryByDue=retries;HistoryOrder=history;pendingCount=count;commandWorld=world;nextCommandId=allocator;}});
        }
    }
    private static XmlDocument CommandQuarantineImage()
    {
        if(Records.Count>MaxQueuedCommands+MaxHistoryRecords||PendingByPriority.Length!=4||RetryByDue.Count>MaxQueuedCommands)throw new InvalidOperationException("Command retention bound exceeded");
        long tokens=HistoryOrder.Count,characters=0;foreach(var queue in PendingByPriority)tokens+=queue.Count;foreach(var queue in RetryByDue.Values)tokens+=queue.Count;
        if(tokens>8192)throw new InvalidOperationException("Command whole token retention bound exceeded");foreach(var record in Records.Values){if(record==null)throw new InvalidOperationException("Command original missing");characters+=(record.Detail?.Length??0)+(long)(record.Request.IssuerId?.Length??0);if(characters>4L*1024*1024)throw new InvalidOperationException("Command whole text retention bound exceeded");}
        var doc=new XmlDocument{XmlResolver=null};var root=doc.CreateElement("commands");doc.AppendChild(root);Set(root,"pending",pendingCount);Set(root,"next",nextCommandId);
        var keys=new List<ulong>(Records.Keys);keys.Sort();foreach(ulong key in keys)
        {
            var record=Records[key];if(record==null||record.Request.CommandId!=key)throw new InvalidOperationException("Command key mismatch");var request=record.Request;
            if(float.IsNaN(request.TargetPosition.x)||float.IsNaN(request.TargetPosition.y)||float.IsNaN(request.TargetPosition.z)||float.IsInfinity(request.TargetPosition.x)||float.IsInfinity(request.TargetPosition.y)||float.IsInfinity(request.TargetPosition.z))throw new InvalidOperationException("Command vector nonfinite");
            var row=doc.CreateElement("record");root.AppendChild(row);Set(row,"id",key);Set(row,"target",request.TargetEntityId);Set(row,"subject",request.SubjectEntityId);Text(row,"issuer",request.IssuerId);Set(row,"kind",(byte)request.Kind);Set(row,"origin",(byte)request.Origin);Set(row,"priority",(byte)request.Priority);Set(row,"revision",request.ExpectedRevision);Set(row,"expires",request.ExpiresUtcTicks);Set(row,"hasPosition",request.HasTargetPosition?1:0);row.SetAttribute("x",request.TargetPosition.x.ToString("R",CultureInfo.InvariantCulture));row.SetAttribute("y",request.TargetPosition.y.ToString("R",CultureInfo.InvariantCulture));row.SetAttribute("z",request.TargetPosition.z.ToString("R",CultureInfo.InvariantCulture));Set(row,"status",(byte)record.Status);Text(row,"detail",record.Detail);Set(row,"submitted",record.SubmittedUtcTicks);Set(row,"updated",record.UpdatedUtcTicks);Set(row,"attempt",record.AttemptCount);Set(row,"retry",record.NextAttemptUtcTicks);Set(row,"failure",(byte)record.LastFailureKind);
        }
        for(int i=0;i<PendingByPriority.Length;i++){var row=doc.CreateElement("priority");root.AppendChild(row);Set(row,"id",i);QueueImage(doc,row,PendingByPriority[i]);}
        foreach(var pair in RetryByDue){var row=doc.CreateElement("retry");root.AppendChild(row);Set(row,"due",pair.Key);QueueImage(doc,row,pair.Value);}
        var history=doc.CreateElement("history");root.AppendChild(history);QueueImage(doc,history,HistoryOrder);return doc;
    }
    private static void QueueImage(XmlDocument doc,XmlElement parent,Queue<ulong> queue)
    {if(queue==null||queue.Count>8192)throw new InvalidOperationException("Command token retention unavailable");foreach(ulong id in queue){var token=doc.CreateElement("token");parent.AppendChild(token);Set(token,"id",id);}}
    private static void Set(XmlElement row,string name,IFormattable value)=>row.SetAttribute(name,value.ToString(null,CultureInfo.InvariantCulture));
    private static void Text(XmlElement row,string name,string value)
    {if((value?.Length??0)>65536)throw new InvalidOperationException("Command text retention bound exceeded");row.SetAttribute(name+"Null",value==null?"1":"0");row.SetAttribute(name,value??string.Empty);}
}

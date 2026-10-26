using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml;

public static partial class RebirthNpcWorkAssignmentService
{
    private static RebirthNpcAncillaryOriginalScope assignmentRecordScope;
    private static bool UseAssignmentScope(){if(!RebirthNpcWorkReleaseGate.Enabled)return false;lock(Sync)return RebirthNpcAncillaryOriginalScope.Use(ref assignmentRecordScope,ById.Count==0&&ByNpc.Count==0&&HistoryOrder.Count==0);}
    // Admission is called only after the exact literature request predicate.
    // All generic mutation callers continue to use the held scope above.
    private static bool UseLiteratureAssignmentCreationScope(){lock(Sync)return RebirthNpcAncillaryOriginalScope.Use(ref assignmentRecordScope,ById.Count==0&&ByNpc.Count==0&&HistoryOrder.Count==0);}
    private static bool UseLiteratureAssignmentScope(ulong assignmentId)
    {
        if(!TryGet(assignmentId,out var assignment)||!RebirthNpcQualifiedWorkGate.Allows(assignment))return false;
        lock(Sync)return RebirthNpcAncillaryOriginalScope.Use(ref assignmentRecordScope,ById.Count==0&&ByNpc.Count==0&&HistoryOrder.Count==0);
    }
    private static bool AssignmentOriginalCurrent(RebirthNpcAncillaryOriginalScope original)=>original!=null&&ReferenceEquals(original,assignmentRecordScope)&&original.Current();
    private static bool ReadAssignmentScope(){lock(Sync)return ThreadManager.IsMainThread()&&(ById.Count==0&&ByNpc.Count==0&&HistoryOrder.Count==0||AssignmentOriginalCurrent(assignmentRecordScope));}
    internal static bool CanRetireEmptyAssignmentRecords{get{lock(Sync)return ThreadManager.IsMainThread()&&ById.Count==0&&ByNpc.Count==0&&HistoryOrder.Count==0&&RebirthNpcWorkCallbackObservation.IsIdle;}}
    internal static bool TryRetireEmptyAssignmentObservation(){lock(Sync){if(!CanRetireEmptyAssignmentRecords)return false;assignmentRecordScope=null;return true;}}
    internal static bool CanBeginFreshAssignmentGeneration(World world)
    {lock(Sync){if(!ThreadManager.IsMainThread()||(assignmentRecordScope==null&&ById.Count!=0)||(assignmentRecordScope!=null&&!ReferenceEquals(assignmentRecordScope.World,world)))return false;foreach(var row in ById.Values)if(row==null||!IsTerminal(row.Status))return false;return true;}}
    internal static void AppendQuarantineIds(HashSet<RebirthNpcStableId> ids)
    {lock(Sync){if(ById.Count>8192)throw new InvalidOperationException("Assignment retention bound");foreach(var row in ById.Values){if(row==null)throw new InvalidOperationException("Assignment original missing");RebirthNpcSaveQuarantineIds.Add(ids,row.NpcId);}foreach(var id in ByNpc.Keys)RebirthNpcSaveQuarantineIds.Add(ids,id);}}
    private static string Text(string value,ref long text){if(value!=null&&(value.Length>4096||(text+=value.Length)>1024*1024))throw new InvalidOperationException("Assignment string retention bound");return value??string.Empty;}
    private static string F(float value){if(float.IsNaN(value)||float.IsInfinity(value))throw new InvalidOperationException("Assignment vector nonfinite");return value.ToString("R",CultureInfo.InvariantCulture);}
    private static XmlDocument AssignmentImage()
    {
        if(ById.Count>8192||ByNpc.Count>8192||HistoryOrder.Count>MaxHistory)throw new InvalidOperationException("Assignment retention bound");
        var doc=RebirthNpcAncillaryOriginalScope.Image("workAssignments",assignmentRecordScope,new[]{created,cancelled,completed,failed,suspended,rejected});var root=doc.DocumentElement;root.SetAttribute("nextId",nextAssignmentId.ToString(CultureInfo.InvariantCulture));
        long text=0;var ids=new List<ulong>(ById.Keys);ids.Sort();foreach(ulong id in ids)
        {
            var a=ById[id];if(a==null||id==0||a.AssignmentId!=id||a.NpcId.IsEmpty||a.Revision==0||!Enum.IsDefined(typeof(RebirthNpcWorkAssignmentStatus),a.Status)||(nextAssignmentId!=0&&id>=nextAssignmentId))throw new InvalidOperationException("Assignment original corrupt");
            var e=doc.CreateElement("assignment");root.AppendChild(e);e.SetAttribute("id",id.ToString(CultureInfo.InvariantCulture));e.SetAttribute("npc",a.NpcId.ToString());e.SetAttribute("definition",Text(a.DefinitionId,ref text));e.SetAttribute("actor",Text(a.ActorId,ref text));e.SetAttribute("target",Text(a.TargetKey,ref text));e.SetAttribute("hasPosition",a.HasTargetPosition?"1":"0");e.SetAttribute("x",F(a.TargetPosition.x));e.SetAttribute("y",F(a.TargetPosition.y));e.SetAttribute("z",F(a.TargetPosition.z));e.SetAttribute("status",((byte)a.Status).ToString(CultureInfo.InvariantCulture));e.SetAttribute("revision",a.Revision.ToString(CultureInfo.InvariantCulture));e.SetAttribute("priority",a.Priority.ToString(CultureInfo.InvariantCulture));e.SetAttribute("detail",Text(a.Detail,ref text));e.SetAttribute("created",a.CreatedUtcTicks.ToString(CultureInfo.InvariantCulture));e.SetAttribute("updated",a.UpdatedUtcTicks.ToString(CultureInfo.InvariantCulture));
            // Preserve null distinctly from empty strings and absent schedule.
            e.SetAttribute("nulls",(a.DefinitionId==null?"1":"0")+(a.ActorId==null?"1":"0")+(a.TargetKey==null?"1":"0")+(a.Detail==null?"1":"0"));
            if(a.Schedule!=null){var schedule=doc.CreateElement("schedule");schedule.SetAttribute("start",a.Schedule.StartMinuteInclusive.ToString(CultureInfo.InvariantCulture));schedule.SetAttribute("end",a.Schedule.EndMinuteExclusive.ToString(CultureInfo.InvariantCulture));schedule.SetAttribute("days",a.Schedule.DayMask.ToString(CultureInfo.InvariantCulture));e.AppendChild(schedule);}
        }
        var owners=new List<RebirthNpcStableId>(ByNpc.Keys);owners.Sort((a,b)=>string.CompareOrdinal(a.ToString(),b.ToString()));var indexed=new HashSet<ulong>();foreach(var owner in owners){var list=ByNpc[owner];if(owner.IsEmpty||list==null||list.Count>8192)throw new InvalidOperationException("Assignment owner index corrupt");var e=doc.CreateElement("owner");e.SetAttribute("npc",owner.ToString());root.AppendChild(e);foreach(ulong id in list){if(!indexed.Add(id)||!ById.TryGetValue(id,out var row)||row.NpcId!=owner)throw new InvalidOperationException("Assignment owner index mismatch");var link=doc.CreateElement("id");link.InnerText=id.ToString(CultureInfo.InvariantCulture);e.AppendChild(link);}}
        if(indexed.Count!=ById.Count)throw new InvalidOperationException("Assignment owner index incomplete");var seen=new HashSet<ulong>();foreach(ulong id in HistoryOrder){if(!seen.Add(id)||!ById.TryGetValue(id,out var row)||!IsTerminal(row.Status))throw new InvalidOperationException("Assignment terminal history corrupt");var e=doc.CreateElement("history");e.InnerText=id.ToString(CultureInfo.InvariantCulture);root.AppendChild(e);}return doc;
    }
    internal static RebirthNpcSaveQuarantineDomain PrepareAssignmentRecordsQuarantine(RebirthNpcSaveQuarantineScope scope)
    {
        lock(Sync)
        {
            RebirthNpcAncillaryOriginalScope.RequireScope(scope,assignmentRecordScope);if(!RebirthNpcWorkCallbackObservation.IsIdle||(assignmentRecordScope==null&&(ById.Count!=0||ByNpc.Count!=0||HistoryOrder.Count!=0)))throw new InvalidOperationException("Original assignment scope unknown");
            var records=ById;var owners=ByNpc;var history=HistoryOrder;var original=assignmentRecordScope;var refs=new Dictionary<ulong,RebirthNpcWorkAssignment>(ById);var doc=AssignmentImage();string image=doc.OuterXml;ulong nextId=nextAssignmentId;long a=created,b=cancelled,c=completed,d=failed,e=suspended,f=rejected;var empty=new Dictionary<ulong,RebirthNpcWorkAssignment>();var emptyOwners=new Dictionary<RebirthNpcStableId,List<ulong>>();var emptyHistory=new Queue<ulong>();
            return new RebirthNpcSaveQuarantineDomain("work-assignment-records",doc,
                ()=>{lock(Sync){if(!ThreadManager.IsMainThread()||!RebirthNpcWorkCallbackObservation.IsIdle||!ReferenceEquals(ById,records)||!ReferenceEquals(ByNpc,owners)||!ReferenceEquals(HistoryOrder,history)||!ReferenceEquals(assignmentRecordScope,original)||ById.Count!=refs.Count)return false;foreach(var row in refs)if(!ById.TryGetValue(row.Key,out var current)||!ReferenceEquals(current,row.Value))return false;return AssignmentImage().OuterXml==image;}},
                ()=>{lock(Sync){ById=empty;ByNpc=emptyOwners;HistoryOrder=emptyHistory;assignmentRecordScope=null;nextAssignmentId=1;created=cancelled=completed=failed=suspended=rejected=0;}},
                ()=>{lock(Sync){ById=records;ByNpc=owners;HistoryOrder=history;assignmentRecordScope=original;nextAssignmentId=nextId;created=a;cancelled=b;completed=c;failed=d;suspended=e;rejected=f;}});
        }
    }
}

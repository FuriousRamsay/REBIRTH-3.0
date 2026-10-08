using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;

/// <summary>Original-task admission domain. Not connected to a native queue until enqueue custody is proved.</summary>
public sealed class RebirthTheorySoloOriginalTaskLedger
{
    public const int MaximumActiveTasks=128;
    public long Issued { get; private set; }
    private readonly Dictionary<long,Task> active=new Dictionary<long,Task>();
    public sealed class Task
    {
        public long Ordinal;
        public string Creation,Lease,Subject,Kind,RecipeBinding,RequestId,BeforeInventoryHash,AfterInventoryHash;
        public int Portions,Completed,OriginalActor;
        public double Difficulty;
        public bool Committed,Held,Published;
        public Task Clone() { return (Task)MemberwiseClone(); }
    }
    public Task Prepare(string requestId,int originalActor,string kind,string recipeBinding,string beforeInventoryHash,string afterInventoryHash,string creation,string subject,int portions,double difficulty)
    {
        Guid owner;
        if(!Guid.TryParseExact(requestId,"N",out var request)||request==Guid.Empty||requestId!=request.ToString("N")||originalActor<0||(kind!="personal"&&kind!="workstation")||!Binding(recipeBinding)||!Binding(beforeInventoryHash)||!Binding(afterInventoryHash)||!Guid.TryParseExact(creation,"N",out owner)||owner==Guid.Empty||creation!=owner.ToString("N")||String.IsNullOrEmpty(subject)||subject.Length>96||portions<1||portions>10000||Double.IsNaN(difficulty)||Double.IsInfinity(difficulty)||difficulty<0||difficulty>100)return null;
        var existing=active.Values.FirstOrDefault(t=>t.RequestId==requestId);
        if(existing!=null)return existing.OriginalActor==originalActor&&existing.Kind==kind&&existing.RecipeBinding==recipeBinding&&existing.BeforeInventoryHash==beforeInventoryHash&&existing.AfterInventoryHash==afterInventoryHash&&existing.Creation==creation&&existing.Subject==subject&&existing.Portions==portions&&existing.Difficulty==difficulty?existing.Clone():null;
        if(active.Count>=MaximumActiveTasks||Issued==Int64.MaxValue)return null;
        var task=new Task{RequestId=requestId,BeforeInventoryHash=beforeInventoryHash,AfterInventoryHash=afterInventoryHash,OriginalActor=originalActor,Kind=kind,RecipeBinding=recipeBinding,Ordinal=++Issued,Creation=creation,Lease=Guid.NewGuid().ToString("N"),Subject=subject,Portions=portions,Difficulty=difficulty};
        active.Add(task.Ordinal,task);return task.Clone();
    }
    private static bool Binding(string value)=>value!=null&&value.Length==64&&value.All(c=>c>='0'&&c<='9'||c>='a'&&c<='f');
    public bool TryGet(long ordinal,string creation,string lease,out Task proof)
    {
        proof=null;Task task;if(!active.TryGetValue(ordinal,out task)||task.Creation!=creation||task.Lease!=lease)return false;
        proof=task.Clone();return true;
    }
    private Task Exact(Task proof)
    {
        Task task;
        return proof!=null&&active.TryGetValue(proof.Ordinal,out task)&&task.RequestId==proof.RequestId&&task.OriginalActor==proof.OriginalActor&&task.Kind==proof.Kind&&task.RecipeBinding==proof.RecipeBinding&&task.BeforeInventoryHash==proof.BeforeInventoryHash&&task.AfterInventoryHash==proof.AfterInventoryHash&&task.Creation==proof.Creation&&task.Lease==proof.Lease&&task.Subject==proof.Subject&&task.Portions==proof.Portions&&task.Difficulty==proof.Difficulty?task:null;
    }
    // Callers must persist each mutation and prove its exact final-file witness before exposing continuation.
    public bool Publish(Task proof) { var task=Exact(proof);if(task==null||task.Held||task.Published)return false;task.Published=true;return true; }
    public bool Commit(Task proof) { var task=Exact(proof);if(task==null||task.Held||!task.Published||task.Committed)return false;task.Committed=true;return true; }
    public bool Hold(Task proof) { var task=Exact(proof);if(task==null)return false;task.Held=true;return true; }
    public bool Retire(Task proof) { var task=Exact(proof);return task!=null&&active.Remove(task.Ordinal); }
    public bool Complete(Task proof,int expectedNextPortion)
    {
        var task=Exact(proof);
        if(task==null||!task.Committed||task.Held||expectedNextPortion!=task.Completed+1||expectedNextPortion>task.Portions)return false;
        task.Completed=expectedNextPortion;
        if(task.Completed==task.Portions)active.Remove(task.Ordinal);
        return true;
    }
    public XElement Write(string creation)
    {
        var node=new XElement("originalTasks",new XAttribute("version",1),new XAttribute("creation",creation),new XAttribute("issued",Issued));
        foreach(var task in active.Values.OrderBy(t=>t.Ordinal))node.Add(new XElement("task",new XAttribute("before",task.BeforeInventoryHash),new XAttribute("after",task.AfterInventoryHash),new XAttribute("request",task.RequestId),new XAttribute("published",task.Published?1:0),new XAttribute("actor",task.OriginalActor),new XAttribute("kind",task.Kind),new XAttribute("recipe",task.RecipeBinding),new XAttribute("ordinal",task.Ordinal),new XAttribute("creation",task.Creation),new XAttribute("lease",task.Lease),new XAttribute("subject",task.Subject),new XAttribute("portions",task.Portions),new XAttribute("completed",task.Completed),new XAttribute("difficulty",task.Difficulty.ToString("R",CultureInfo.InvariantCulture)),new XAttribute("committed",task.Committed?1:0),new XAttribute("held",task.Held?1:0)));
        return node;
    }
    public static bool TryRead(XElement node,string creation,out RebirthTheorySoloOriginalTaskLedger ledger)
    {
        ledger=null;
        try
        {
            Guid owner;
            if(node==null||node.Name!="originalTasks"||node.Attributes().Count()!=3||(string)node.Attribute("version")!="1"||(string)node.Attribute("creation")!=creation||!Guid.TryParseExact(creation,"N",out owner)||owner==Guid.Empty||creation!=owner.ToString("N"))return false;
            var next=new RebirthTheorySoloOriginalTaskLedger{Issued=(long)node.Attribute("issued")};
            if(next.Issued<0||node.Elements().Count()>MaximumActiveTasks)return false;
            foreach(var row in node.Elements())
            {
                if(row.Name!="task"||row.HasElements||row.Attributes().Count()!=16)return false;
                string published=(string)row.Attribute("published"),committed=(string)row.Attribute("committed"),held=(string)row.Attribute("held");
                var task=new Task{BeforeInventoryHash=(string)row.Attribute("before"),AfterInventoryHash=(string)row.Attribute("after"),RequestId=(string)row.Attribute("request"),Published=published=="1",OriginalActor=(int)row.Attribute("actor"),Kind=(string)row.Attribute("kind"),RecipeBinding=(string)row.Attribute("recipe"),Ordinal=(long)row.Attribute("ordinal"),Creation=(string)row.Attribute("creation"),Lease=(string)row.Attribute("lease"),Subject=(string)row.Attribute("subject"),Portions=(int)row.Attribute("portions"),Completed=(int)row.Attribute("completed"),Difficulty=(double)row.Attribute("difficulty"),Committed=committed=="1",Held=held=="1"};
                Guid lease,originalRequest;
                if(!Guid.TryParseExact(task.RequestId,"N",out originalRequest)||originalRequest==Guid.Empty||task.RequestId!=originalRequest.ToString("N")||next.active.Values.Any(t=>t.RequestId==task.RequestId)||task.OriginalActor<0||(task.Kind!="personal"&&task.Kind!="workstation")||!Binding(task.RecipeBinding)||!Binding(task.BeforeInventoryHash)||!Binding(task.AfterInventoryHash)||task.Ordinal<1||task.Ordinal>next.Issued||task.Creation!=creation||!Guid.TryParseExact(task.Lease,"N",out lease)||lease==Guid.Empty||task.Lease!=lease.ToString("N")||String.IsNullOrEmpty(task.Subject)||task.Subject.Length>96||task.Portions<1||task.Portions>10000||task.Completed<0||task.Completed>=task.Portions||Double.IsNaN(task.Difficulty)||Double.IsInfinity(task.Difficulty)||task.Difficulty<0||task.Difficulty>100||(published!="0"&&published!="1")||task.Committed&&!task.Published||(committed!="0"&&committed!="1")||(held!="0"&&held!="1")||!task.Committed&&task.Completed!=0||next.active.ContainsKey(task.Ordinal))return false;
                next.active.Add(task.Ordinal,task);
            }
            if(!XNode.DeepEquals(node,next.Write(creation)))return false;
            ledger=next;return true;
        }
        catch{return false;}
    }
    public RebirthTheorySoloOriginalTaskLedger Clone()
    {
        var copy=new RebirthTheorySoloOriginalTaskLedger{Issued=Issued};
        foreach(var pair in active)copy.active.Add(pair.Key,pair.Value.Clone());return copy;
    }
}

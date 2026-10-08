using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

internal enum RebirthPoiResetCaller { Quest=1, ManualPoi=2, NaturalRespawn=3, ScriptRestart=4, Event=5, Region=6, Administration=7 }
// Original expected reset envelope, saved before any native mutation. Completion must
// positively witness every declared copy/regeneration/volume/trigger under this receipt.
internal sealed class RebirthPoiResetPlan
{
    public readonly Guid Transaction;
    public readonly RebirthPoiResetCaller Caller;
    public readonly string Manifest;
    public readonly IReadOnlyList<long> Chunks;
    public readonly IReadOnlyList<int> Volumes,Triggers;
    public readonly int ConservativeCharacters;
    public readonly IReadOnlyList<RebirthPoiAuthoredResetExpectation> Authored;
    public bool IsAuthored {get{return Authored.Count>0;} }
    public bool RequiresTriggerRefresh {get{return Caller==RebirthPoiResetCaller.Quest||Caller==RebirthPoiResetCaller.ManualPoi||Caller==RebirthPoiResetCaller.Event&&Chunks.Count>0;} }
    public RebirthPoiResetPlan(Guid transaction,RebirthPoiResetCaller caller,string manifest,IEnumerable<long> chunks,IEnumerable<int> volumes,IEnumerable<int> triggers,IEnumerable<RebirthPoiAuthoredResetExpectation> authored=null)
    {
        if(transaction==Guid.Empty||!Enum.IsDefined(typeof(RebirthPoiResetCaller),caller)||manifest==null||manifest.Length!=64||manifest.Any(c=>!(c>='0'&&c<='9'||c>='a'&&c<='f'))||chunks==null||volumes==null||triggers==null)throw new ArgumentException("Invalid native reset envelope.");
        var a=(authored??Enumerable.Empty<RebirthPoiAuthoredResetExpectation>()).ToArray();
        if(a.Length>8192||a.Any(e=>e==null)||a.Select(e=>e.Key).Distinct().Count()!=a.Length||a.GroupBy(e=>e.Kind).Any(g=>g.Count()>4096)||a.Select(e=>((int)e.Kind).ToString()+":"+e.Descriptor).Distinct().Count()!=a.Length)throw new ArgumentException("Invalid original authored expectation set.");
        Authored=Array.AsReadOnly(a.OrderBy(e=>(int)e.Kind).ThenBy(e=>e.Index).ToArray());
        long[] c=chunks.ToArray();int[] v=volumes.ToArray(),t=triggers.ToArray();
        if(c.Length>65536||v.Length<1&&a.Length==0||v.Length>4096||t.Length>4096||c.Distinct().Count()!=c.Length||v.Distinct().Count()!=v.Length||t.Distinct().Count()!=t.Length||v.Any(i=>i<0)||t.Any(i=>i<0)||caller==RebirthPoiResetCaller.Quest&&c.Length==0||caller==RebirthPoiResetCaller.ManualPoi&&c.Length==0)throw new ArgumentException("Invalid expected reset effects.");
        Array.Sort(c);Array.Sort(v);Array.Sort(t);Transaction=transaction;Caller=caller;Manifest=manifest;Chunks=Array.AsReadOnly(c);Volumes=Array.AsReadOnly(v);Triggers=Array.AsReadOnly(t);ConservativeCharacters=512+32*c.Length+24*(v.Length+t.Length)+192*a.Length;
        if(IsAuthored&&(v.Except(a.Where(e=>e.Kind==RebirthPoiAuthoredResetKind.Sleeper&&e.OriginalNativeId.HasValue).Select(e=>e.OriginalNativeId.Value)).Any()||t.Except(a.Where(e=>e.Kind==RebirthPoiAuthoredResetKind.Trigger&&e.OriginalNativeId.HasValue).Select(e=>e.OriginalNativeId.Value)).Any()))throw new ArgumentException("Legacy runtime IDs are not bound by authored intent.");
    }
    public string Canonical {get{return Write(this).ToString(SaveOptions.DisableFormatting);} }
    internal static XElement Write(RebirthPoiResetPlan plan)
    {
        var node=new XElement("plan",new XAttribute("version",plan.IsAuthored?2:1),new XAttribute("id",plan.Transaction.ToString("N")),new XAttribute("caller",(int)plan.Caller),new XAttribute("manifest",plan.Manifest));
        foreach(long id in plan.Chunks)node.Add(new XElement("chunk",new XAttribute("id",id)));
        foreach(int id in plan.Volumes)node.Add(new XElement("volume",new XAttribute("id",id)));
        foreach(int id in plan.Triggers)node.Add(new XElement("trigger",new XAttribute("id",id)));
        foreach(var expectation in plan.Authored)node.Add(expectation.Write());
        return node;
    }
    internal static RebirthPoiResetPlan Read(XElement node)
    {
        RebirthPoiClearanceCodec.Shape(node,"plan","version,id,caller,manifest","chunk,volume,trigger,authored");
        int version=RebirthPoiClearanceCodec.Int(node,"version");if(version!=1&&version!=2||version==1&&node.Elements("authored").Any()||version==2&&!node.Elements("authored").Any())throw new FormatException();
        foreach(var child in node.Elements().Where(c=>c.Name.LocalName!="authored"))RebirthPoiClearanceCodec.Shape(child,child.Name.LocalName,"id","");
        return new RebirthPoiResetPlan(RebirthPoiClearanceCodec.Id(node,"id"),(RebirthPoiResetCaller)RebirthPoiClearanceCodec.Int(node,"caller"),RebirthPoiClearanceCodec.Text(node,"manifest"),node.Elements("chunk").Select(c=>RebirthPoiClearanceCodec.Number(c,"id")),node.Elements("volume").Select(c=>RebirthPoiClearanceCodec.Int(c,"id")),node.Elements("trigger").Select(c=>RebirthPoiClearanceCodec.Int(c,"id")),node.Elements("authored").Select(RebirthPoiAuthoredResetExpectation.Read));
    }
}


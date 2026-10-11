using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

internal enum RebirthPoiResetCaller { Quest=1, ManualPoi=2, NaturalRespawn=3, ScriptRestart=4, Event=5, Region=6, Administration=7 }
internal enum RebirthPoiResetChunkEffect { Rebuilt=1, Removed=2 }
// Original expected reset envelope, saved before any native mutation. Completion must
// positively witness every declared copy/regeneration/volume/trigger under this receipt.
internal sealed class RebirthPoiResetPlan
{
    public readonly RebirthPoiResetChunkEffect ChunkEffect;
    public readonly Guid Transaction;
    public readonly RebirthPoiResetCaller Caller;
    public readonly string Manifest;
    public readonly IReadOnlyList<long> Chunks;
    public readonly IReadOnlyList<int> Volumes,Triggers;
    public readonly int ConservativeCharacters;
    public readonly IReadOnlyList<RebirthPoiAuthoredResetExpectation> Authored;
    public bool IsAuthored {get;private set;}
    public bool RequiresTriggerRefresh {get{return ChunkEffect==RebirthPoiResetChunkEffect.Rebuilt&&(Caller==RebirthPoiResetCaller.Quest||Caller==RebirthPoiResetCaller.ManualPoi||Caller==RebirthPoiResetCaller.Event&&Chunks.Count>0);} }
    public RebirthPoiResetPlan(Guid transaction,RebirthPoiResetCaller caller,string manifest,IEnumerable<long> chunks,IEnumerable<int> volumes,IEnumerable<int> triggers,IEnumerable<RebirthPoiAuthoredResetExpectation> authored=null,bool authoredEnvelope=false,RebirthPoiResetChunkEffect chunkEffect=RebirthPoiResetChunkEffect.Rebuilt)
    {
        if(transaction==Guid.Empty||!Enum.IsDefined(typeof(RebirthPoiResetCaller),caller)||manifest==null||manifest.Length!=64||manifest.Any(c=>!(c>='0'&&c<='9'||c>='a'&&c<='f'))||chunks==null||volumes==null||triggers==null)throw new ArgumentException("Invalid native reset envelope.");
        if(!Enum.IsDefined(typeof(RebirthPoiResetChunkEffect),chunkEffect)||chunkEffect==RebirthPoiResetChunkEffect.Removed&&(caller!=RebirthPoiResetCaller.Region&&caller!=RebirthPoiResetCaller.Administration))throw new ArgumentException("Invalid reset chunk effect owner.");
        ChunkEffect=chunkEffect;
        var a=(authored??Enumerable.Empty<RebirthPoiAuthoredResetExpectation>()).ToArray();
        if(a.Length>8192||a.Any(e=>e==null)||a.Select(e=>e.Key).Distinct().Count()!=a.Length||a.GroupBy(e=>e.Kind).Any(g=>g.Count()>4096)||a.Select(e=>((int)e.Kind).ToString()+":"+e.Descriptor).Distinct().Count()!=a.Length)throw new ArgumentException("Invalid original authored expectation set.");
        IsAuthored=authoredEnvelope||a.Length>0;Authored=Array.AsReadOnly(a.OrderBy(e=>(int)e.Kind).ThenBy(e=>e.Index).ToArray());
        if(chunkEffect==RebirthPoiResetChunkEffect.Removed&&!IsAuthored)throw new ArgumentException("Deletion requires an explicit authored envelope.");
        long[] c=chunks.ToArray();int[] v=volumes.ToArray(),t=triggers.ToArray();
        if(chunkEffect==RebirthPoiResetChunkEffect.Removed&&c.Length==0||c.Length>65536||v.Length<1&&!IsAuthored||IsAuthored&&a.Length==0&&c.Length==0||v.Length>4096||t.Length>4096||c.Distinct().Count()!=c.Length||v.Distinct().Count()!=v.Length||t.Distinct().Count()!=t.Length||v.Any(i=>i<0)||t.Any(i=>i<0)||caller==RebirthPoiResetCaller.Quest&&c.Length==0||caller==RebirthPoiResetCaller.ManualPoi&&c.Length==0)throw new ArgumentException("Invalid expected reset effects.");
        Array.Sort(c);Array.Sort(v);Array.Sort(t);Transaction=transaction;Caller=caller;Manifest=manifest;Chunks=Array.AsReadOnly(c);Volumes=Array.AsReadOnly(v);Triggers=Array.AsReadOnly(t);ConservativeCharacters=512+32*c.Length+24*(v.Length+t.Length)+192*a.Length;
        if(IsAuthored&&(v.Except(a.Where(e=>e.Kind==RebirthPoiAuthoredResetKind.Sleeper&&e.OriginalNativeId.HasValue).Select(e=>e.OriginalNativeId.Value)).Any()||t.Except(a.Where(e=>e.Kind==RebirthPoiAuthoredResetKind.Trigger&&e.OriginalNativeId.HasValue).Select(e=>e.OriginalNativeId.Value)).Any()))throw new ArgumentException("Legacy runtime IDs are not bound by authored intent.");
    }
    public string Canonical {get{return Write(this).ToString(SaveOptions.DisableFormatting);} }
    internal static XElement Write(RebirthPoiResetPlan plan)
    {
        var node=new XElement("plan",new XAttribute("version",plan.ChunkEffect==RebirthPoiResetChunkEffect.Removed?4:plan.IsAuthored?(plan.Authored.Count==0?3:2):1),new XAttribute("id",plan.Transaction.ToString("N")),new XAttribute("caller",(int)plan.Caller),new XAttribute("manifest",plan.Manifest));
        if(plan.ChunkEffect==RebirthPoiResetChunkEffect.Removed)node.Add(new XAttribute("chunkEffect",(int)plan.ChunkEffect));
        foreach(long id in plan.Chunks)node.Add(new XElement("chunk",new XAttribute("id",id)));
        foreach(int id in plan.Volumes)node.Add(new XElement("volume",new XAttribute("id",id)));
        foreach(int id in plan.Triggers)node.Add(new XElement("trigger",new XAttribute("id",id)));
        foreach(var expectation in plan.Authored)node.Add(expectation.Write());
        return node;
    }
    internal static RebirthPoiResetPlan Read(XElement node)
    {
        bool deletion=(string)node.Attribute("version")=="4";
        RebirthPoiClearanceCodec.Shape(node,"plan",deletion?"version,id,caller,manifest,chunkEffect":"version,id,caller,manifest","chunk,volume,trigger,authored");
        if(deletion&&RebirthPoiClearanceCodec.Int(node,"chunkEffect")!=(int)RebirthPoiResetChunkEffect.Removed)throw new FormatException();
        int version=RebirthPoiClearanceCodec.Int(node,"version");if(version!=1&&version!=2&&version!=3&&version!=4||(version==1||version==3)&&node.Elements("authored").Any()||version==2&&!node.Elements("authored").Any())throw new FormatException();
        foreach(var child in node.Elements().Where(c=>c.Name.LocalName!="authored"))RebirthPoiClearanceCodec.Shape(child,child.Name.LocalName,"id","");
        return new RebirthPoiResetPlan(RebirthPoiClearanceCodec.Id(node,"id"),(RebirthPoiResetCaller)RebirthPoiClearanceCodec.Int(node,"caller"),RebirthPoiClearanceCodec.Text(node,"manifest"),node.Elements("chunk").Select(c=>RebirthPoiClearanceCodec.Number(c,"id")),node.Elements("volume").Select(c=>RebirthPoiClearanceCodec.Int(c,"id")),node.Elements("trigger").Select(c=>RebirthPoiClearanceCodec.Int(c,"id")),node.Elements("authored").Select(RebirthPoiAuthoredResetExpectation.Read),version==3||version==4,deletion?RebirthPoiResetChunkEffect.Removed:RebirthPoiResetChunkEffect.Rebuilt);
    }
}


using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;

internal enum RebirthPoiAuthoredResetKind { Sleeper=1, Trigger=2 }
// Authored identity is distinct from an ephemeral world runtime ID. Constructor
// validation is an adapter assertion boundary, never proof of native registration.
internal sealed class RebirthPoiAuthoredResetExpectation
{
    public readonly RebirthPoiAuthoredResetKind Kind;
    public readonly int Index;
    public readonly string Descriptor;
    public readonly int? OriginalNativeId;
    public readonly bool Combat;
    public string Key {get{return ((int)Kind).ToString(CultureInfo.InvariantCulture)+":"+Index.ToString(CultureInfo.InvariantCulture);} }
    public RebirthPoiAuthoredResetExpectation(RebirthPoiAuthoredResetKind kind,int index,string descriptor,int? originalNativeId,bool combat)
    {
        if(!Enum.IsDefined(typeof(RebirthPoiAuthoredResetKind),kind)||index<0||index>=4096||descriptor==null||descriptor.Length!=64||descriptor.Any(c=>!(c>='0'&&c<='9'||c>='a'&&c<='f'))||originalNativeId.HasValue&&originalNativeId.Value<0||kind==RebirthPoiAuthoredResetKind.Trigger&&combat)throw new ArgumentException("Invalid authored reset expectation.");
        Kind=kind;Index=index;Descriptor=descriptor;OriginalNativeId=originalNativeId;Combat=combat;
    }
    internal XElement Write()
    {
        var node=new XElement("authored",new XAttribute("kind",(int)Kind),new XAttribute("index",Index),new XAttribute("descriptor",Descriptor),new XAttribute("combat",Combat?1:0));
        if(OriginalNativeId.HasValue)node.Add(new XAttribute("native",OriginalNativeId.Value));return node;
    }
    internal static RebirthPoiAuthoredResetExpectation Read(XElement node)
    {
        RebirthPoiClearanceCodec.Shape(node,"authored",node.Attribute("native")==null?"kind,index,descriptor,combat":"kind,index,descriptor,combat,native","");int combat=RebirthPoiClearanceCodec.Int(node,"combat");if(combat!=0&&combat!=1)throw new FormatException();
        return new RebirthPoiAuthoredResetExpectation((RebirthPoiAuthoredResetKind)RebirthPoiClearanceCodec.Int(node,"kind"),RebirthPoiClearanceCodec.Int(node,"index"),RebirthPoiClearanceCodec.Text(node,"descriptor"),node.Attribute("native")==null?(int?)null:RebirthPoiClearanceCodec.Int(node,"native"),combat==1);
    }
}

internal sealed class RebirthPoiAuthoredRuntimeBinding
{
    public readonly string ExpectationKey,Descriptor;
    public readonly int NativeId;
    public readonly Guid Receipt;
    public RebirthPoiAuthoredRuntimeBinding(string expectationKey,string descriptor,int nativeId,Guid receipt)
    {if(string.IsNullOrEmpty(expectationKey)||expectationKey.Length>16||descriptor==null||descriptor.Length!=64||descriptor.Any(c=>!(c>='0'&&c<='9'||c>='a'&&c<='f'))||nativeId<0||receipt==Guid.Empty)throw new ArgumentException("Invalid original runtime binding receipt.");ExpectationKey=expectationKey;Descriptor=descriptor;NativeId=nativeId;Receipt=receipt;}
}

// All original authored targets must have one unique, producer-verified binding.
// This evidence token must only follow the actual native copy/registration route.
internal sealed class RebirthPoiAuthoredResetBindings
{
    public readonly RebirthPoiResetPlan OriginalPlan;
    public readonly long OriginalEpoch;
    public int ConservativeCharacters {get{return OriginalPlan.ConservativeCharacters+512+192*Bindings.Count+512*PriorActors.Count;} }
    public readonly IReadOnlyDictionary<string,RebirthPoiAuthoredRuntimeBinding> Bindings;
    public readonly IReadOnlyDictionary<Guid,RebirthPoiResetActorOutcome> PriorActors;
    public RebirthPoiAuthoredResetBindings(RebirthPoiResetPlan originalPlan,IEnumerable<RebirthPoiAuthoredRuntimeBinding> bindings,long originalEpoch,IEnumerable<RebirthPoiResetActorOutcome> priorActors=null)
    {
        if(originalPlan==null||bindings==null||originalEpoch<0||originalEpoch==long.MaxValue)throw new ArgumentException("Authored reset bindings require original authored intent.");
        var map=new Dictionary<string,RebirthPoiAuthoredRuntimeBinding>(StringComparer.Ordinal);var native=new HashSet<string>(StringComparer.Ordinal);var receipts=new HashSet<Guid>();
        foreach(var binding in bindings)
        {
            if(binding==null||map.Count>=8192||map.ContainsKey(binding.ExpectationKey))throw new ArgumentException("Duplicate or excessive native binding.");
            var expected=originalPlan.Authored.FirstOrDefault(e=>e.Key==binding.ExpectationKey);
            string kind;
            if(originalPlan.IsAuthored){if(expected==null||expected.Descriptor!=binding.Descriptor||expected.OriginalNativeId.HasValue&&expected.OriginalNativeId.Value!=binding.NativeId)throw new ArgumentException("Foreign or changed original authored binding.");kind=((int)expected.Kind).ToString(CultureInfo.InvariantCulture);}
            else {kind=binding.ExpectationKey.StartsWith("1:",StringComparison.Ordinal)?"1":binding.ExpectationKey.StartsWith("2:",StringComparison.Ordinal)?"2":null;if(kind==null||binding.ExpectationKey!=kind+":"+binding.NativeId.ToString(CultureInfo.InvariantCulture)||!(kind=="1"?originalPlan.Volumes:originalPlan.Triggers).Contains(binding.NativeId))throw new ArgumentException("Legacy native-ID binding differs from original intent.");}
            if(!native.Add(kind+":"+binding.NativeId.ToString(CultureInfo.InvariantCulture))||!receipts.Add(binding.Receipt))throw new ArgumentException("Aliased native binding receipt.");
            map.Add(binding.ExpectationKey,binding);
        }
        if(map.Count!=(originalPlan.IsAuthored?originalPlan.Authored.Count:originalPlan.Volumes.Count+originalPlan.Triggers.Count))throw new ArgumentException("Incomplete authored generation binding.");
        var prior=new Dictionary<Guid,RebirthPoiResetActorOutcome>();foreach(var outcome in priorActors??Enumerable.Empty<RebirthPoiResetActorOutcome>()){if(outcome==null||prior.Count>=8192||outcome.SourceEpoch!=originalEpoch||prior.ContainsKey(outcome.Token)||!map.Values.Any(b=>b.ExpectationKey.StartsWith("1:",StringComparison.Ordinal)&&b.Descriptor==outcome.Descriptor)||!receipts.Add(outcome.Receipt))throw new ArgumentException("Invalid prior actor disposition set.");prior.Add(outcome.Token,outcome);}PriorActors=new ReadOnlyDictionary<Guid,RebirthPoiResetActorOutcome>(prior);
        OriginalEpoch=originalEpoch;OriginalPlan=originalPlan;Bindings=new ReadOnlyDictionary<string,RebirthPoiAuthoredRuntimeBinding>(map);
    }
    public string Canonical {get{return new XElement("authoredReset",new XAttribute("epoch",OriginalEpoch),RebirthPoiResetPlan.Write(OriginalPlan),Write()).ToString(SaveOptions.DisableFormatting);} }
    internal XElement Write()
    {
        var root=new XElement("bindings",new XAttribute("version",1),new XAttribute("id",OriginalPlan.Transaction.ToString("N")));
        foreach(var binding in Bindings.Values.OrderBy(b=>b.ExpectationKey,StringComparer.Ordinal))root.Add(new XElement("binding",new XAttribute("key",binding.ExpectationKey),new XAttribute("descriptor",binding.Descriptor),new XAttribute("native",binding.NativeId),new XAttribute("receipt",binding.Receipt.ToString("N"))));foreach(var outcome in PriorActors.Values.OrderBy(a=>a.Token))root.Add(outcome.Write());return root;
    }
    internal static RebirthPoiAuthoredResetBindings Read(XElement root,RebirthPoiResetPlan plan,long originalEpoch)
    {
        RebirthPoiClearanceCodec.Shape(root,"bindings","version,id","binding,priorActor");if(RebirthPoiClearanceCodec.Int(root,"version")!=1||RebirthPoiClearanceCodec.Id(root,"id")!=plan.Transaction)throw new FormatException();
        var bindings=new List<RebirthPoiAuthoredRuntimeBinding>();foreach(var node in root.Elements("binding")){RebirthPoiClearanceCodec.Shape(node,"binding","key,descriptor,native,receipt","");bindings.Add(new RebirthPoiAuthoredRuntimeBinding(RebirthPoiClearanceCodec.Text(node,"key"),RebirthPoiClearanceCodec.Text(node,"descriptor"),RebirthPoiClearanceCodec.Int(node,"native"),RebirthPoiClearanceCodec.Id(node,"receipt")));}return new RebirthPoiAuthoredResetBindings(plan,bindings,originalEpoch,root.Elements("priorActor").Select(RebirthPoiResetActorOutcome.Read));
    }
}
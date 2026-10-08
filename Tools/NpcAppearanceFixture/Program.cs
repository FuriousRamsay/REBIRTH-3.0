using System;
using System.Linq;
using System.Xml.Linq;
// Only the existing selector enum is doubled; all descriptor/codec/resolution code is linked production source.
public enum RebirthHumanNpcModelPipeline : byte { Auto=0, Custom=1, SDCS=2 }
static class Program
{
    static int passed;
    static void Check(bool condition, string label) { if(!condition) throw new Exception(label); passed++; }
    static RebirthNpcResolvedAppearance Make(string race="catalogue-race-A",int seed=77)
    {
        Check(RebirthNpcResolvedAppearance.TryCreate(RebirthHumanNpcModelPipeline.SDCS,seed,"authored-base","catalogue-v1","generator-v1","native-rig",true,race,2,"eye-id","hair-id","colour-id","","","beard-id",out var d),"valid actual choices"); return d;
    }
    static void Invalid(XElement node,string label) { Check(!RebirthNpcResolvedAppearance.TryRead(node,out _),label); }
    static void Main()
    {
        var d=Make(); Check(RebirthNpcResolvedAppearance.TryDecode(d.Encode(),out var copy)&&d.Equals(copy)&&d.GetHashCode()==copy.GetHashCode(),"roundtrip equality");
        var edit=d.Write(); edit.SetAttributeValue("race","changed"); Check(d.Race=="catalogue-race-A","detached write");
        Check(RebirthNpcResolvedAppearance.TryRead(d.Write(),out var detached),"read detached"); edit=detached.Write();edit.SetAttributeValue("eye","other");Check(detached.Eye=="eye-id","immutable read");
        foreach(var field in new[]{"version","pipeline","archetype","catalogue","generator","model","sex","race","variant","eye"}){edit=d.Write();edit.Attribute(field).Remove();Invalid(edit,"missing "+field);}
        foreach(var value in new[]{"0","2","01",""," 1"}){edit=d.Write();edit.SetAttributeValue("version",value);Invalid(edit,"unknown/noncanonical schema");}
        foreach(var value in new[]{"Auto","sdcs","2","unknown"}){edit=d.Write();edit.SetAttributeValue("pipeline",value);Invalid(edit,"unresolved/unknown pipeline");}
        foreach(var value in new[]{"0","-1","01","2147483648"}){edit=d.Write();edit.SetAttributeValue("variant",value);Invalid(edit,"invalid variant");}
        foreach(var value in new[]{"+77","077","-0","2147483648"}){edit=d.Write();edit.SetAttributeValue("legacySeed",value);Invalid(edit,"seed canonical/bounded");}
        edit=d.Write();edit.Add(new XAttribute("unknown","x"));Invalid(edit,"unknown attribute");
        edit=d.Write();edit.Add(new XElement("extra"));Invalid(edit,"child nodes");
        edit=d.Write();edit.Add(new XComment("hidden"));Invalid(edit,"comment node");
        edit=d.Write();edit.Name=XName.Get("resolvedAppearance","unexpected");Invalid(edit,"namespace");
        foreach(var value in new[]{" leading","trailing ","bad\nname",new string('x',257),"\ud800"}){edit=d.Write();edit.SetAttributeValue("model",value);Invalid(edit,"bad identifier");}
        Check(!RebirthNpcResolvedAppearance.TryDecode(d.Encode().Replace("version=\"1\"","version=\"1\" version=\"1\""),out _),"duplicate attribute");
        Check(!RebirthNpcResolvedAppearance.TryDecode("<!DOCTYPE resolvedAppearance [<!ENTITY x SYSTEM 'file:///never-read'>]>"+d.Encode(),out _),"DTD prohibited");
        Check(!RebirthNpcResolvedAppearance.TryDecode(d.Encode()+"<extra/>",out _),"trailing root");
        Check(!RebirthNpcResolvedAppearance.TryDecode(new string('x',8193),out _),"payload bound");
        Check(RebirthNpcResolvedAppearance.TryReadOptional(new XElement("appearance"),out var missing)&&missing==null,"legacy stays unresolved");
        Check(!RebirthNpcResolvedAppearance.TryReadOptional(new XElement("appearance",d.Write(),d.Write()),out _),"duplicate component");
        edit=d.Write();edit.SetAttributeValue("version",2);Check(!RebirthNpcResolvedAppearance.TryReadOptional(new XElement("appearance",edit),out _),"invalid optional not legacy");
        var changed=Make("catalogue-race-B");Check(RebirthNpcResolvedAppearance.TryResolveOnce(d,d.Pipeline,77,d.Archetype,changed,out var chosen)&&ReferenceEquals(chosen,d),"resolved never rerolls");
        Check(RebirthNpcResolvedAppearance.TryResolveOnce(d,d.Pipeline,77,d.Archetype,null,out chosen)&&ReferenceEquals(chosen,d),"resolved no catalogue candidate needed");
        Check(!RebirthNpcResolvedAppearance.TryResolveOnce(d,d.Pipeline,78,d.Archetype,changed,out _),"original seed mismatch");
        Check(!RebirthNpcResolvedAppearance.TryResolveOnce(null,d.Pipeline,77,d.Archetype,null,out _),"missing does not synthesize");
        Check(RebirthNpcResolvedAppearance.TryResolveOnce(null,d.Pipeline,77,d.Archetype,d,out chosen)&&ReferenceEquals(chosen,d),"first qualified resolution");
        Check(RebirthNpcResolvedAppearance.TryCreate(RebirthHumanNpcModelPipeline.Custom,0,"authored","","author-v1","authored-model",false,"",0,"","","","","","",out var custom),"custom preserved");
        Check(RebirthNpcResolvedAppearance.TryDecode(custom.Encode(),out copy)&&custom.Equals(copy),"custom roundtrip");
        edit=custom.Write();edit.SetAttributeValue("race","x");Invalid(edit,"custom cannot carry hidden SDCS choices");
        edit=d.Write();edit.Add(new XText(new string(' ',9000)));Invalid(edit,"bounded leaf rejects whitespace payload");
        var reordered=new XElement("resolvedAppearance");foreach(var a in d.Write().Attributes().Reverse())reordered.Add(new XAttribute(a));
        Check(RebirthNpcResolvedAppearance.TryRead(reordered,out copy)&&copy.Encode()==d.Encode(),"canonical attribute order");
        foreach(var seed in new[]{0,-1,int.MinValue,int.MaxValue}) { var bound=Make(seed:seed);Check(bound.LegacySeed==seed,"existing full int seed range preserved"); }
        foreach(var invalid in new[]{"\ufffe","\uffff","\u0000","\ud800"})Check(!RebirthNpcResolvedAppearance.TryCreate(RebirthHumanNpcModelPipeline.SDCS,77,"authored", "cat", "gen",invalid,true,"race",1,"eye","","","","","",out _),"TryCreate invalid XML identifier false");
        edit=d.Write();foreach(var key in new[]{"archetype","catalogue","generator","model","race","eye","hair","hairColour","mustache","chops","beard"}) edit.SetAttributeValue(key,new string('&',256));Invalid(edit,"escaped aggregate payload bound");
        Check(!d.Equals(changed),"choice equality detects change");
        Console.WriteLine("PASS "+passed+" actual resolved-appearance domain checks; selector enum doubled; no catalogue/native/game/persistence integration simulated.");
    }
}
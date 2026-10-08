using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;

// Immutable publication identities bound to the original immutable plan entry indices.
// This is intent, never evidence that a native entity was spawned or durably saved.
public sealed class RebirthGearRecoveryManifest
{
    private readonly XElement xml;
    private RebirthGearRecoveryManifest(XElement value){xml=new XElement(value);}
    public XElement ToXml(){return new XElement(xml);}
    public static bool TryCreate(RebirthGearInventoryPlan plan,out RebirthGearRecoveryManifest manifest)
    {
        manifest=null;if(plan==null||!plan.IsConserved()||plan.Recovery.Count==0)return false;
        var root=new XElement("recoveryManifest",new XAttribute("version",1));
        var backpack=Enumerable.Range(0,plan.Recovery.Count).Where(i=>plan.Recovery[i].Origin==RebirthGearInventoryPlan.RecoveryOrigin.Backpack).ToArray();
        if(backpack.Length>0)root.Add(Publication("backpack",backpack));
        for(int i=0;i<plan.Recovery.Count;i++)if(plan.Recovery[i].Origin!=RebirthGearInventoryPlan.RecoveryOrigin.Backpack)root.Add(Publication("item",new[]{i}));
        return TryRead(root,plan,out manifest);
    }
    private static XElement Publication(string kind,IEnumerable<int> indices)
    {
        return new XElement("publication",new XAttribute("id",Guid.NewGuid().ToString("N")),new XAttribute("kind",kind),indices.Select(i=>new XElement("entry",new XAttribute("index",i))));
    }
    public static bool TryRead(XElement value,RebirthGearInventoryPlan plan,out RebirthGearRecoveryManifest manifest)
    {
        manifest=null;
        if(plan==null||!plan.IsConserved()||plan.Recovery.Count==0||value==null||value.Name!="recoveryManifest"||value.Attributes().Count()!=1||(string)value.Attribute("version")!="1"||BadNodes(value))return false;
        var publications=value.Elements().Take(190).ToArray();if(publications.Length==0||publications.Length>189)return false;
        var covered=new HashSet<int>();var ids=new HashSet<Guid>();bool hadBackpack=false;
        foreach(var publication in publications)
        {
            Guid id;string kind=(string)publication.Attribute("kind");
            if(publication.Name!="publication"||publication.Attributes().Count()!=2||!Guid.TryParseExact((string)publication.Attribute("id"),"N",out id)||id==Guid.Empty||(string)publication.Attribute("id")!=id.ToString("N")||!ids.Add(id)||(kind!="backpack"&&kind!="item")||BadNodes(publication))return false;
            var entries=publication.Elements().Take(190).ToArray();if(entries.Length==0||entries.Length>189||(kind=="item"&&entries.Length!=1)||(kind=="backpack"&&hadBackpack))return false;
            if(kind=="backpack")hadBackpack=true;
            int previous=-1;
            foreach(var entry in entries)
            {
                int index;
                if(entry.Name!="entry"||entry.Attributes().Count()!=1||entry.Nodes().Any()||!int.TryParse((string)entry.Attribute("index"),NumberStyles.None,CultureInfo.InvariantCulture,out index)||index<=previous||index>=plan.Recovery.Count||!covered.Add(index))return false;
                if((plan.Recovery[index].Origin==RebirthGearInventoryPlan.RecoveryOrigin.Backpack)!=(kind=="backpack"))return false;
                previous=index;
            }
        }
        if(covered.Count!=plan.Recovery.Count)return false;
        manifest=new RebirthGearRecoveryManifest(value);return true;
    }
    private static bool BadNodes(XElement value){return value.Nodes().Any(n=>!(n is XElement)&&(!(n is XText)||!string.IsNullOrWhiteSpace(((XText)n).Value)));}
}
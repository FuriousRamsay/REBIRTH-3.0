using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;

// Immutable owner-bound save container. Does not authorize transfers or prove
// that the corresponding semantic quantities/source debits have been saved.
internal sealed class RebirthNpcNativeStackSet
{
    internal const int MaximumRecords=4096;
    private readonly XElement image;
    private readonly Dictionary<string,long> quantities;
    internal string Owner=>(string)image.Attribute("owner");
    internal uint Revision=>(uint)image.Attribute("revision");
    private RebirthNpcNativeStackSet(XElement node,Dictionary<string,long> totals){image=new XElement(node);quantities=totals;}
    internal int GetQuantity(string key)=>key!=null&&quantities.TryGetValue(key,out var count)?(int)count:0;
    internal bool FitsQuantities(Dictionary<string,int> proposed){if(proposed==null)return false;foreach(var pair in quantities)if(!proposed.TryGetValue(pair.Key,out var count)||count<pair.Value)return false;return true;}
    internal RebirthNpcNativeStackSet WithRevision(uint revision){var node=Write();node.SetAttributeValue("revision",revision);return new RebirthNpcNativeStackSet(node,quantities);}
    internal XElement Write()=>new XElement(image);
    internal static bool TryCreate(RebirthNpcStableId owner,uint revision,IEnumerable<RebirthNpcNativeStackRecord> records,
        out RebirthNpcNativeStackSet set)
    {
        set=null;if(owner.IsEmpty||records==null)return false;
        try
        {
            var node=new XElement("nativeStacks",new XAttribute("version",1),new XAttribute("owner",owner.ToString()),
                new XAttribute("revision",revision.ToString(CultureInfo.InvariantCulture)));
            int count=0;
            foreach(var record in records){if(record==null||++count>MaximumRecords)return false;node.Add(record.Write());}
            return TryRead(node,out set);
        }
        catch{return false;}
    }
    internal static bool TryRead(XElement node,out RebirthNpcNativeStackSet set)
    {
        set=null;
        if(node==null||node.Name!="nativeStacks"||node.Attributes().Count()!=3||
            (string)node.Attribute("version")!="1"||
            !RebirthNpcStableId.TryParse((string)node.Attribute("owner"),out var owner)||owner.IsEmpty||
            owner.ToString()!=(string)node.Attribute("owner")||
            !uint.TryParse((string)node.Attribute("revision"),NumberStyles.Integer,CultureInfo.InvariantCulture,out _)||
            node.Nodes().Any(n=>!(n is XElement)&&(!(n is XText)||!string.IsNullOrWhiteSpace(((XText)n).Value))))return false;
        var ids=new HashSet<Guid>();var quantities=new Dictionary<string,long>(StringComparer.OrdinalIgnoreCase);
        var records=new List<RebirthNpcNativeStackRecord>();
        foreach(var child in node.Elements())
        {
            if(records.Count>=MaximumRecords||!RebirthNpcNativeStackRecord.TryRead(child,out var record)||
                record.Owner!=owner.ToString()||!ids.Add(record.StackId))return false;
            quantities.TryGetValue(record.ItemKey,out var count);count+=record.Count;
            if(count>int.MaxValue)return false;
            quantities[record.ItemKey]=count;records.Add(record);
        }
        var canonical=new XElement(node.Name,node.Attributes());
        foreach(var record in records.OrderBy(r=>r.StackId.ToString("N"),StringComparer.Ordinal))canonical.Add(record.Write());
        set=new RebirthNpcNativeStackSet(canonical,quantities);return true;
    }
}
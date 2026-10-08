using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using System.IO;

// Detached refund obligation only. No destination custody, inventory mutation or settlement authority.
internal sealed class RebirthStationCancellationRefund
{
    private readonly ItemStack[] refunds;
    private readonly XElement image;
    private RebirthStationCancellationRefund(ItemStack[] value,XElement image){refunds=value;this.image=new XElement(image);}
    internal XElement Write()=>new XElement(image);
    internal static bool TryCreate(RebirthStationGridAdmission admission,IList<Recipe> definitions,
        RebirthStationTerminalIntent intent,out RebirthStationCancellationRefund plan)
    {
        plan=null;
        try
        {
            if(admission==null||intent==null||intent.IsCompletion||
                !RebirthStationTerminalIntent.TryRead(intent.Write(),admission,out _)||
                !admission.TryMaterialize(definitions,out var recipe,out var before,out var after)||
                !RebirthStationGridQueue.ConservesPayment(before,after,recipe)||recipe.ingredients==null||
                recipe.ingredients.Count<1||recipe.ingredients.Count>9)return false;
            var detached=new ItemStack[recipe.ingredients.Count];
            for(int i=0;i<detached.Length;i++)
            {
                var paid=recipe.ingredients[i];
                if(paid?.itemValue==null||paid.itemValue.type<=0||paid.count<=0)return false;
                var refund=RebirthStationGridQueue.RefundSnapshot(paid);
                if(refund?.itemValue==null||refund.count!=paid.count||refund.itemValue.type!=paid.itemValue.type||
                    refund.itemValue.Metadata!=null&&refund.itemValue.Metadata.Keys.Any(k=>k.StartsWith(RebirthStationGridQueue.Prefix,StringComparison.Ordinal)))return false;
                detached[i]=refund;
            }
            if(!RebirthStationGridSnapshotCodec.TryWrite(detached,out var grid))return false;
            return TryReadStored(new XElement("stationCancellationRefund",new XAttribute("version",1),intent.Write(),grid),admission,intent,out plan);
        }
        catch{return false;}
    }
    // Validates stored refund obligation against the retained paid image, without guessing a current recipe.
    internal static bool TryReadStored(XElement node,RebirthStationGridAdmission admission,
        RebirthStationTerminalIntent intent,out RebirthStationCancellationRefund plan)
    {
        plan=null;
        try
        {
            if(node==null||node.Name!="stationCancellationRefund"||node.Attributes().Count()!=1||
                (string)node.Attribute("version")!="1"||node.Elements().Count()!=2||
                node.Elements("stationTerminalIntent").Count()!=1||node.Elements("grid").Count()!=1||
                node.Nodes().Any(n=>!(n is XElement)&&(!(n is XText)||!string.IsNullOrWhiteSpace(((XText)n).Value)))||
                admission==null||intent==null||intent.IsCompletion||
                !RebirthStationTerminalIntent.TryRead(intent.Write(),admission,out _)||
                !XNode.DeepEquals(node.Element("stationTerminalIntent"),intent.Write())||
                !RebirthStationGridSnapshotCodec.TryRead(node.Element("grid"),out var stored)||stored.Length<1||
                !RebirthStationGridSnapshotCodec.TryRead(admission.Write().Element("recipe")?.Element("grid"),out var paid)||
                paid.Length!=stored.Length)return false;
            for(int i=0;i<paid.Length;i++)
            {
                if(paid[i]?.itemValue==null||paid[i].count<=0||paid[i].itemValue.type<=0||
                    stored[i]?.itemValue==null||stored[i].count<=0||
                    !RebirthStationGridIngredients.IsSameStackSnapshot(stored[i],RebirthStationGridQueue.RefundSnapshot(paid[i])))return false;
            }
            plan=new RebirthStationCancellationRefund(stored,node);return true;
        }
        catch{return false;}
    }
    internal RebirthStationCancellationRefund Clone()=>new RebirthStationCancellationRefund(CopyRefunds(),image);
    internal static XElement WriteAll(IDictionary<string,RebirthStationCancellationRefund> records,
        IDictionary<string,RebirthStationGridAdmission> admissions,IDictionary<string,RebirthStationTerminalIntent> intents)
    {
        if(records==null||admissions==null||intents==null||records.Count>64)throw new InvalidDataException("Invalid station cancellation refunds");
        var section=new XElement("stationCancellationRefunds",new XAttribute("version",1));
        foreach(var pair in records.OrderBy(p=>p.Key,StringComparer.Ordinal))
        {
            if(pair.Value==null||!admissions.TryGetValue(pair.Key,out var admission)||!intents.TryGetValue(pair.Key,out var intent)||
                !TryReadStored(pair.Value.image,admission,intent,out var valid))throw new InvalidDataException("Invalid station cancellation refund binding");
            section.Add(valid.Write());
        }
        return section;
    }
    internal static bool ReadAll(XElement progression,IDictionary<string,RebirthStationGridAdmission> admissions,
        IDictionary<string,RebirthStationTerminalIntent> intents,out Dictionary<string,RebirthStationCancellationRefund> records)
    {
        records=new Dictionary<string,RebirthStationCancellationRefund>(StringComparer.Ordinal);
        try
        {
            var sections=progression.Elements("stationCancellationRefunds").ToArray();if(sections.Length==0)return true;
            if(sections.Length!=1||sections[0].Attributes().Count()!=1||(string)sections[0].Attribute("version")!="1"||
                sections[0].Nodes().Any(n=>!(n is XElement)&&(!(n is XText)||!string.IsNullOrWhiteSpace(((XText)n).Value))))return false;
            foreach(var node in sections[0].Elements())
            {
                string job=(string)node.Element("stationTerminalIntent")?.Attribute("job");
                if(records.Count>=64||job==null||records.ContainsKey(job)||!admissions.TryGetValue(job,out var admission)||
                    !intents.TryGetValue(job,out var intent)||!TryReadStored(node,admission,intent,out var record))return false;
                records.Add(job,record);
            }
            return true;
        }
        catch{return false;}
    }
    internal ItemStack[] CopyRefunds()
    {var copy=new ItemStack[refunds.Length];for(int i=0;i<copy.Length;i++)copy[i]=refunds[i].Clone();return copy;}
}
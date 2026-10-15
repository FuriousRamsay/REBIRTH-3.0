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
    internal bool IsMaterialReturn=>(string)image.Attribute("version")=="2"||(string)image.Attribute("version")=="4";
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
                if(refund?.itemValue==null||(!admission.IsMaterialPayment&&refund.count!=paid.count)||refund.itemValue.type!=paid.itemValue.type||
                    refund.itemValue.Metadata!=null&&refund.itemValue.Metadata.Keys.Any(k=>k.StartsWith(RebirthStationGridQueue.Prefix,StringComparison.Ordinal)))return false;
                detached[i]=refund;
            }
            if(admission.IsPerBatch&&!TryRemaining(admission,recipe.ingredients,intent.RemainingBatches,out detached))return false;
            XElement grid;
            if(admission.IsMaterialPayment)grid=WriteCounters(detached);
            else if(!RebirthStationGridSnapshotCodec.TryWrite(detached,out grid))return false;
            return TryReadStored(new XElement("stationCancellationRefund",new XAttribute("version",admission.IsPerBatch?(admission.IsMaterialPayment?4:3):admission.IsMaterialPayment?2:1),intent.Write(),grid),admission,intent,out plan);
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
            bool material=admission?.IsMaterialPayment==true;
            if(node==null||node.Name!="stationCancellationRefund"||node.Attributes().Count()!=1||
                (string)node.Attribute("version")!=(admission?.IsPerBatch==true?(material?"4":"3"):material?"2":"1")||node.Elements().Count()!=2||
                node.Elements("stationTerminalIntent").Count()!=1||node.Elements(material?"counterGrid":"grid").Count()!=1||
                node.Nodes().Any(n=>!(n is XElement)&&(!(n is XText)||!string.IsNullOrWhiteSpace(((XText)n).Value)))||
                admission==null||intent==null||intent.IsCompletion||
                !RebirthStationTerminalIntent.TryRead(intent.Write(),admission,out _)||
                !XNode.DeepEquals(node.Element("stationTerminalIntent"),intent.Write())||
                !ReadRefund(node,material,out var stored)||stored.Length<1||
                !RebirthStationGridSnapshotCodec.TryRead(admission.WritePaymentImage().Element("recipe")?.Element("grid"),out var paid))return false;
            if(!TryRemaining(admission,paid,intent.RemainingBatches,out var expected)||expected.Length!=stored.Length)return false;
            for(int i=0;i<expected.Length;i++)
            {
                if(stored[i]?.itemValue==null||stored[i].count<=0||
                    !RebirthStationGridIngredients.IsSameStackSnapshot(stored[i],expected[i]))return false;
            }
            plan=new RebirthStationCancellationRefund(stored,node);return true;
        }
        catch{return false;}
    }
    internal RebirthStationCancellationRefund Clone()=>new RebirthStationCancellationRefund(CopyCounterRefunds(),image);
    // Full paid fragments retain exact source payloads. Remaining native batches
    // receive a deterministic prefix of each ingredient type's original custody.
    private static bool TryRemaining(RebirthStationGridAdmission admission,IList<ItemStack> paid,int remaining,out ItemStack[] result)
    {
        result=null;if(admission==null||paid==null||paid.Count<1||paid.Count>9)return false;
        var full=new List<ItemStack>();var totals=new Dictionary<int,long>();
        foreach(var cell in paid)
        {
            if(cell?.itemValue==null||cell.itemValue.type<=0||cell.count<1)return false;
            var refund=RebirthStationGridQueue.RefundSnapshot(cell);
            if(refund?.itemValue==null||refund.count<1)return false;
            full.Add(refund);totals.TryGetValue(refund.itemValue.type,out long prior);
            totals[refund.itemValue.type]=prior+refund.count;
        }
        if(!admission.IsPerBatch){result=full.ToArray();return remaining==1;}
        int original=admission.OriginalBatches;if(original<1||remaining<1||remaining>original)return false;
        var needed=new Dictionary<int,long>();
        foreach(var pair in totals)
        {if(pair.Value%original!=0)return false;needed[pair.Key]=(pair.Value/original)*remaining;}
        var selected=new List<ItemStack>();
        foreach(var cell in full)
        {
            int type=cell.itemValue.type;int take=(int)Math.Min(cell.count,needed[type]);
            if(take>0){cell.count=take;selected.Add(cell);needed[type]-=take;}
        }
        if(needed.Values.Any(n=>n!=0)||selected.Count<1)return false;
        result=selected.ToArray();return true;
    }
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
        =>IsMaterialReturn?new ItemStack[0]:CopyCounterRefunds();
    internal ItemStack[] CopyCounterRefunds()
    {var copy=new ItemStack[refunds.Length];for(int i=0;i<copy.Length;i++)copy[i]=refunds[i].Clone();return copy;}
    private static XElement WriteCounters(ItemStack[] cells)
    {
        var node=new XElement("counterGrid");
        foreach(var cell in cells)node.Add(new XElement("counter",new XAttribute("count",cell.count),new XAttribute("data",RebirthNativeItemCodec.Encode(cell.itemValue))));
        return node;
    }
    private static bool ReadRefund(XElement node,bool material,out ItemStack[] cells)
    {
        cells=null;if(!material)return RebirthStationGridSnapshotCodec.TryRead(node.Element("grid"),out cells);
        var grid=node.Element("counterGrid");
        if(grid==null||grid.HasAttributes||grid.Elements().Count()<1||grid.Elements().Count()>9||
            grid.Nodes().Any(n=>!(n is XElement)&&(!(n is XText)||!string.IsNullOrWhiteSpace(((XText)n).Value))))return false;
        var result=new List<ItemStack>();
        foreach(var entry in grid.Elements())
        {
            string data=(string)entry.Attribute("data");
            if(entry.Name!="counter"||entry.Attributes().Count()!=2||entry.HasElements||
                entry.Nodes().Any(n=>!(n is XText)||!string.IsNullOrWhiteSpace(((XText)n).Value))||
                !int.TryParse((string)entry.Attribute("count"),out int count)||count<1||string.IsNullOrEmpty(data)||
                data.Length>RebirthStationGridSnapshotCodec.MaximumItemText||!RebirthNativeItemCodec.TryDecode(data,out var value)||value.IsEmpty())return false;
            result.Add(new ItemStack(value,count));
        }
        cells=result.ToArray();return true;
    }
}

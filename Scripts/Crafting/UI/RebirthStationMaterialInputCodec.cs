using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;

// Transaction image only: native item stacks serialize counts as UInt16, while forge
// counters use Int32 in memory. Keep the unchanged physical image in its original format.
public static class RebirthStationMaterialInputCodec
{
    public static bool TryWrite(IList<ItemStack> input,int physicalSlots,out XElement image)
    {
        image=null;
        if(input==null||(physicalSlots!=3&&physicalSlots!=9)||input.Count<=physicalSlots||input.Count>255||
            !RebirthStationGridSnapshotCodec.TryWrite(input.Take(physicalSlots).ToArray(),out var physical))return false;
        try
        {
            var node=new XElement("materialInput",new XAttribute("version",1),new XAttribute("physicalSlots",physicalSlots),physical);
            for(int i=physicalSlots;i<input.Count;i++)
            {
                var cell=input[i];
                if(cell?.itemValue==null||cell.itemValue.IsEmpty()||cell.count<0)return false;
                string data=RebirthNativeItemCodec.Encode(cell.itemValue);
                if(data==null||data.Length>RebirthStationGridSnapshotCodec.MaximumItemText||
                    !RebirthNativeItemCodec.TryDecode(data,out var decoded)||
                    !RebirthStationGridIngredients.IsSameStackSnapshot(cell,new ItemStack(decoded,cell.count)))return false;
                node.Add(new XElement("counter",new XAttribute("count",cell.count),new XAttribute("data",data)));
            }
            image=node;return true;
        }
        catch{return false;}
    }
    public static bool TryRead(XElement image,out ItemStack[] input,out int physicalSlots)
    {
        input=null;physicalSlots=0;
        if(image==null||image.Name!="materialInput"||image.Attributes().Count()!=2||
            (string)image.Attribute("version")!="1"||!int.TryParse((string)image.Attribute("physicalSlots"),out int slots)||
            (slots!=3&&slots!=9)||image.Elements("grid").Count()!=1||image.Elements().Count()<2||
            image.Elements().Count()>256-slots||
            image.Nodes().Any(n=>!(n is XElement)&&(!(n is XText)||!string.IsNullOrWhiteSpace(((XText)n).Value)))||
            !RebirthStationGridSnapshotCodec.TryRead(image.Element("grid"),out var physical)||physical.Length!=slots)return false;
        try
        {
            var result=new List<ItemStack>(physical);
            foreach(var node in image.Elements().Where(n=>n.Name!="grid"))
            {
                if(node.Name!="counter"||node.HasElements||node.Attributes().Count()!=2||
                    node.Nodes().Any(n=>!(n is XText)||!string.IsNullOrWhiteSpace(((XText)n).Value))||
                    !int.TryParse((string)node.Attribute("count"),NumberStyles.None,CultureInfo.InvariantCulture,out int count))return false;
                string data=(string)node.Attribute("data");
                if(string.IsNullOrEmpty(data)||data.Length>RebirthStationGridSnapshotCodec.MaximumItemText||
                    !RebirthNativeItemCodec.TryDecode(data,out var value)||value.IsEmpty())return false;
                result.Add(new ItemStack(value,count));
            }
            input=result.ToArray();physicalSlots=slots;return true;
        }
        catch{return false;}
    }
}

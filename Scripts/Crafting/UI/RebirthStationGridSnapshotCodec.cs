using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;

// Exact bounded grid images for station transaction records; grants no payment authority.
public static class RebirthStationGridSnapshotCodec
{
    public const int MaximumSlots=9;
    public const int MaximumItemText=262144;

    public static bool TryWrite(IList<ItemStack> slots,out XElement image)
    {
        image=null;
        if(slots==null||slots.Count>MaximumSlots)return false;
        try
        {
            var node=new XElement("grid",new XAttribute("version",1));
            foreach(var stack in slots)
            {
                if(stack==null){node.Add(new XElement("slot",new XAttribute("null",true)));continue;}
                if(stack.itemValue==null||stack.count<0||stack.count>ushort.MaxValue||
                    stack.itemValue.Metadata!=null&&stack.itemValue.Metadata.Count>byte.MaxValue)return false;
                if(stack.count==0)
                {
                    // Preserve nonempty zero-count payloads as well as native empty slots.
                    if(stack.itemValue.IsEmpty()){node.Add(new XElement("slot",new XAttribute("count",0),new XAttribute("data","")));continue;}
                }
                else if(stack.itemValue.IsEmpty())return false;
                string encoded=RebirthNativeItemCodec.Encode(stack.itemValue);
                if(string.IsNullOrEmpty(encoded)||encoded.Length>MaximumItemText||
                    !RebirthNativeItemCodec.TryDecode(encoded,out var decoded)||
                    !RebirthStationGridIngredients.IsSameStackSnapshot(stack,new ItemStack(decoded,stack.count)))return false;
                node.Add(new XElement("slot",new XAttribute("count",stack.count),new XAttribute("data",encoded)));
            }
            image=node;return true;
        }
        catch{return false;}
    }

    public static bool TryRead(XElement image,out ItemStack[] slots)
    {
        slots=null;
        if(image==null||image.Name!="grid"||image.Attributes().Count()!=1||
            (string)image.Attribute("version")!="1"||image.Elements().Count()>MaximumSlots||
            image.Nodes().Any(n=>!(n is XElement)&&(!(n is XText)||!string.IsNullOrWhiteSpace(((XText)n).Value))))return false;
        try
        {
            var result=new List<ItemStack>();
            foreach(var node in image.Elements())
            {
                if(node.Name!="slot"||node.HasElements||
                    node.Nodes().Any(n=>!(n is XText)||!string.IsNullOrWhiteSpace(((XText)n).Value)))return false;
                if(node.Attribute("null")!=null)
                {
                    if(node.Attributes().Count()!=1||(string)node.Attribute("null")!="true")return false;
                    result.Add(null);continue;
                }
                if(node.Attributes().Count()!=2||node.Attribute("count")==null||node.Attribute("data")==null||
                    !int.TryParse((string)node.Attribute("count"),NumberStyles.None,CultureInfo.InvariantCulture,out int count)||
                    count>ushort.MaxValue)return false;
                string encoded=(string)node.Attribute("data");
                if(count==0&&encoded==""){result.Add(ItemStack.Empty.Clone());continue;}
                if(string.IsNullOrEmpty(encoded)||encoded.Length>MaximumItemText||
                    !RebirthNativeItemCodec.TryDecode(encoded,out var value)||
                    value.Metadata!=null&&value.Metadata.Count>byte.MaxValue)return false;
                result.Add(new ItemStack(value,count));
            }
            slots=result.ToArray();return true;
        }
        catch{return false;}
    }
}
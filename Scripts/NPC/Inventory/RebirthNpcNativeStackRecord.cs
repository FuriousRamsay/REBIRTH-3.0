using System;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;

// Native stack custody payload. Capturing/decoding grants no transfer permission,
// debits no inventory and does not acknowledge a durable source/destination write.
internal sealed class RebirthNpcNativeStackRecord
{
    private readonly XElement image;
    internal Guid StackId=>Guid.ParseExact((string)image.Attribute("id"),"N");
    internal string Owner=>(string)image.Attribute("owner");
    internal string ItemKey=>(string)image.Attribute("item");
    internal int Count=>(int)image.Attribute("count");
    internal string Payload=>(string)image.Attribute("payload");
    private RebirthNpcNativeStackRecord(XElement node){image=new XElement(node);}
    internal XElement Write()=>new XElement(image);
    internal static bool TryCapture(Guid stackId,RebirthNpcStableId owner,ItemStack stack,out RebirthNpcNativeStackRecord record)
    {
        record=null;
        if(stackId==Guid.Empty||owner.IsEmpty||stack==null||stack.count<=0||stack.itemValue==null||
            stack.itemValue.IsEmpty()||stack.itemValue.ItemClass==null)return false;
        try
        {
            string key=stack.itemValue.ItemClass.GetItemName();
            string payload=RebirthNativeItemCodec.Encode(stack.itemValue.Clone());
            if(!TryRead(new XElement("nativeStack",new XAttribute("version",1),new XAttribute("id",stackId.ToString("N")),
                new XAttribute("owner",owner.ToString()),new XAttribute("item",key),new XAttribute("count",stack.count),
                new XAttribute("payload",payload)),out var captured)||!captured.TryDecode(out _))return false;
            record=captured;return true;
        }
        catch{return false;}
    }
    internal bool TryDecode(out ItemStack stack)
    {
        stack=null;
        if(!RebirthNativeItemCodec.TryDecode(Payload,out var value)||value?.ItemClass==null||
            !string.Equals(value.ItemClass.GetItemName(),ItemKey,StringComparison.Ordinal))return false;
        stack=new ItemStack(value,Count);return true;
    }
    internal static bool TryRead(XElement node,out RebirthNpcNativeStackRecord record)
    {
        record=null;
        if(node==null||node.Name!="nativeStack"||node.HasElements||node.Attributes().Count()!=6||
            node.Nodes().Any(n=>!(n is XText)||!string.IsNullOrWhiteSpace(((XText)n).Value))||
            (string)node.Attribute("version")!="1"||!Guid.TryParseExact((string)node.Attribute("id"),"N",out var id)||id==Guid.Empty||
            !RebirthNpcStableId.TryParse((string)node.Attribute("owner"),out var owner)||owner.IsEmpty||
            owner.ToString()!=(string)node.Attribute("owner"))return false;
        string key=(string)node.Attribute("item"),payload=(string)node.Attribute("payload");
        if(string.IsNullOrWhiteSpace(key)||key.Length>256||key!=key.Trim()||key.Any(char.IsControl)||
            !int.TryParse((string)node.Attribute("count"),NumberStyles.Integer,CultureInfo.InvariantCulture,out var count)||count<=0||
            string.IsNullOrEmpty(payload)||payload.Length>262144)return false;
        try
        {
            byte[] bytes=Convert.FromBase64String(payload);
            if(bytes.Length==0||Convert.ToBase64String(bytes)!=payload)return false;
            record=new RebirthNpcNativeStackRecord(node);return true;
        }
        catch{return false;}
    }
}
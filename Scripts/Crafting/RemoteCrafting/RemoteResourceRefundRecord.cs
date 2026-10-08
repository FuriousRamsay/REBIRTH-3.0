using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;

// Immutable unused-grant custody image. Only an authenticated authority may create it
// from its retained removal outcome. Parsing is not authentication or delivery proof.
internal sealed class RemoteResourceRefundRecord
{
    internal const int MaxStacks=64;
    internal const int MaxItemText=262144;
    internal const int MaxTotalItemText=1048576;
    internal string WorldKey {get;private set;}
    internal string OwnerKey {get;private set;}
    internal string CreationId {get;private set;}
    internal ulong SessionEpoch {get;private set;}
    internal ulong RequestId {get;private set;}
    internal RemoteResourceClientOperation Operation {get;private set;}
    private readonly XElement image;
    private RemoteResourceRefundRecord(XElement xml,string world,string owner,string creation,ulong epoch,ulong request,RemoteResourceClientOperation operation)
    {image=new XElement(xml);WorldKey=world;OwnerKey=owner;CreationId=creation;SessionEpoch=epoch;RequestId=request;Operation=operation;}
    internal XElement ToXml()=>new XElement(image);
    internal static bool ValidStorageKey(string value)
        =>value!=null&&value.Length==64&&value.All(c=>c>='0'&&c<='9'||c>='a'&&c<='f');
    private static bool Shape(XElement node,string name,string[] attributes)
        =>node!=null&&node.Name==name&&node.Attributes().Count()==attributes.Length&&
            attributes.All(a=>node.Attribute(a)!=null)&&
            !node.Nodes().Any(n=>!(n is XElement)&&(!(n is XText)||!string.IsNullOrWhiteSpace(((XText)n).Value)));
    internal static bool TryCreate(string world,string owner,string creation,ulong epoch,ulong request,
        RemoteResourceClientOperation operation,IList<ItemStack> unused,out RemoteResourceRefundRecord record)
    {
        record=null;
        if(unused==null||unused.Count<1||unused.Count>MaxStacks)return false;
        try
        {
            var node=new XElement("remoteResourceRefund",new XAttribute("version",1),new XAttribute("world",world??""),
                new XAttribute("owner",owner??""),new XAttribute("creation",creation??""),new XAttribute("epoch",epoch),
                new XAttribute("request",request),new XAttribute("operation",(byte)operation));
            foreach(var stack in unused)
            {
                if(stack==null||stack.IsEmpty()||stack.itemValue==null)return false;
                node.Add(new XElement("stack",new XAttribute("count",stack.count),
                    new XAttribute("data",RebirthNativeItemCodec.Encode(stack.itemValue))));
            }
            return TryRead(node,world,owner,creation,out record);
        }
        catch{return false;}
    }
    internal static bool TryRead(XElement node,string expectedWorld,string expectedOwner,string expectedCreation,
        out RemoteResourceRefundRecord record)
    {
        record=null;
        try
        {
            if(!ValidStorageKey(expectedWorld)||!ValidStorageKey(expectedOwner)||
                !RebirthSurvivorRequestScope.TryNormalize(expectedCreation,out var creation)||
                !Shape(node,"remoteResourceRefund",new[]{"version","world","owner","creation","epoch","request","operation"})||
                (string)node.Attribute("version")!="1"||(string)node.Attribute("world")!=expectedWorld||
                (string)node.Attribute("owner")!=expectedOwner||(string)node.Attribute("creation")!=creation||
                !ulong.TryParse((string)node.Attribute("epoch"),NumberStyles.None,CultureInfo.InvariantCulture,out var epoch)||epoch==0||
                !ulong.TryParse((string)node.Attribute("request"),NumberStyles.None,CultureInfo.InvariantCulture,out var request)||request==0||
                !byte.TryParse((string)node.Attribute("operation"),NumberStyles.None,CultureInfo.InvariantCulture,out var operation)||
                operation<1||operation>5||node.Elements().Count()<1||node.Elements().Count()>MaxStacks)return false;
            int total=0;
            foreach(var entry in node.Elements())
            {
                if(!Shape(entry,"stack",new[]{"count","data"})||entry.Elements().Any()||
                    !int.TryParse((string)entry.Attribute("count"),NumberStyles.None,CultureInfo.InvariantCulture,out int count)||
                    count<1||count>ushort.MaxValue)return false;
                string data=(string)entry.Attribute("data");
                if(string.IsNullOrEmpty(data)||data.Length>MaxItemText)return false;
                total=checked(total+data.Length);if(total>MaxTotalItemText)return false;
                if(!RebirthNativeItemCodec.TryDecode(data,out var value)||value==null||value.IsEmpty()||value.ItemClass==null||
                    count>value.ItemClass.MaxCount||RebirthNativeItemCodec.Encode(value)!=data)return false;
            }
            record=new RemoteResourceRefundRecord(node,expectedWorld,expectedOwner,creation,epoch,request,(RemoteResourceClientOperation)operation);
            return true;
        }
        catch{return false;}
    }
    internal bool TryGetStacks(out List<ItemStack> items)
    {
        items=null;
        try
        {
            var result=new List<ItemStack>();
            foreach(var entry in image.Elements())
            {
                if(!RebirthNativeItemCodec.TryDecode((string)entry.Attribute("data"),out var value))return false;
                result.Add(new ItemStack(value,int.Parse((string)entry.Attribute("count"),CultureInfo.InvariantCulture)));
            }
            items=result;return true;
        }
        catch{return false;}
    }
}
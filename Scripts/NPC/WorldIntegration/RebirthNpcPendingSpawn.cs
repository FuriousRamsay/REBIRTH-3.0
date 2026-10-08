using System;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;

// Immutable prepared request. Parsing never proves safe placement or native absence.
internal sealed class RebirthNpcPendingSpawn
{
    private readonly XElement image;
    internal bool IsAttempted=>image.Attribute("phase")!=null;
    internal bool IsPublishing=>(string)image.Attribute("phase")=="publishing";
    internal bool IsConstructed=>(string)image.Attribute("phase")=="constructed"||IsPublishing;
    internal int NativeEntityId=>(int?)image.Attribute("nativeId")??0;
    internal string ReplayKey=>(string)image.Attribute("key");
    internal string StableId=>(string)image.Attribute("stable");
    internal string Profile=>(string)image.Attribute("profile");
    internal string EntityClass=>(string)image.Attribute("entity");
    internal float X=>(float)image.Attribute("x");
    internal float Y=>(float)image.Attribute("y");
    internal float Z=>(float)image.Attribute("z");
    internal float Yaw=>(float)image.Attribute("yaw");
    internal long CreatedTicks=>(long)image.Attribute("created");
    private RebirthNpcPendingSpawn(XElement node){image=new XElement(node);}
    internal XElement Write()=>new XElement(image);
    internal static bool TryCreate(string replayKey,RebirthNpcStableId stable,string profile,string entity,
        float x,float y,float z,float yaw,long ticks,out RebirthNpcPendingSpawn request)
    {
        request=null;
        try{return TryRead(new XElement("pendingSpawn",new XAttribute("version",1),new XAttribute("key",replayKey??""),
            new XAttribute("stable",stable.ToString()),new XAttribute("profile",profile??""),new XAttribute("entity",entity??""),
            new XAttribute("x",x.ToString("R",CultureInfo.InvariantCulture)),new XAttribute("y",y.ToString("R",CultureInfo.InvariantCulture)),
            new XAttribute("z",z.ToString("R",CultureInfo.InvariantCulture)),new XAttribute("yaw",yaw.ToString("R",CultureInfo.InvariantCulture)),
            new XAttribute("created",ticks)),out request);}catch{return false;}
    }
    internal bool TryMarkAttempted(out RebirthNpcPendingSpawn attempted)
    {
        attempted=null;if(IsAttempted)return false;
        var next=Write();next.SetAttributeValue("version",2);next.SetAttributeValue("phase","attempted");
        return TryRead(next,out attempted);
    }
    internal bool TryMarkConstructed(int nativeId,out RebirthNpcPendingSpawn constructed)
    {
        constructed=null;if(!IsAttempted||IsConstructed||nativeId<=0)return false;
        var next=Write();next.SetAttributeValue("version",3);next.SetAttributeValue("phase","constructed");next.SetAttributeValue("nativeId",nativeId);
        return TryRead(next,out constructed);
    }
    internal bool TryMarkPublishing(out RebirthNpcPendingSpawn publishing)
    {
        publishing=null;if(!IsConstructed||IsPublishing)return false;
        var next=Write();next.SetAttributeValue("version",4);next.SetAttributeValue("phase","publishing");
        return TryRead(next,out publishing);
    }
    private static bool Text(string value)=>!string.IsNullOrWhiteSpace(value)&&value.Length<=128&&value==value.Trim()&&!value.Any(char.IsControl);
    private static bool Number(XElement node,string key,out float value)=>float.TryParse((string)node.Attribute(key),NumberStyles.Float,CultureInfo.InvariantCulture,out value)&&!float.IsNaN(value)&&!float.IsInfinity(value);
    internal static bool TryRead(XElement node,out RebirthNpcPendingSpawn request)
    {
        request=null;
        if(node==null||node.Name!="pendingSpawn"||node.HasElements||
            node.Nodes().Any(n=>!(n is XText)||!string.IsNullOrWhiteSpace(((XText)n).Value))||
                        !(((string)node.Attribute("version")=="1"&&node.Attributes().Count()==10)||
              ((string)node.Attribute("version")=="2"&&node.Attributes().Count()==11&&(string)node.Attribute("phase")=="attempted")||
              ((((string)node.Attribute("version")=="3"&&(string)node.Attribute("phase")=="constructed")||
               ((string)node.Attribute("version")=="4"&&(string)node.Attribute("phase")=="publishing"))&&node.Attributes().Count()==12&&
               int.TryParse((string)node.Attribute("nativeId"),NumberStyles.Integer,CultureInfo.InvariantCulture,out var nativeId)&&nativeId>0))||
            !RebirthNpcSpawnReplayCodec.ValidKey((string)node.Attribute("key"))||
            !((string)node.Attribute("key")).StartsWith("spawn:",StringComparison.Ordinal)||
            !RebirthNpcStableId.TryParse((string)node.Attribute("stable"),out var stable)||stable.ToString()!=(string)node.Attribute("stable")||
            !Text((string)node.Attribute("profile"))||!Text((string)node.Attribute("entity"))||
            !Number(node,"x",out var x)||Math.Abs(x)>1000000f||!Number(node,"z",out var z)||Math.Abs(z)>1000000f||
            !Number(node,"y",out var y)||y< -1024f||y>4096f||!Number(node,"yaw",out var yaw)||yaw<0f||yaw>=360f||
            !long.TryParse((string)node.Attribute("created"),NumberStyles.Integer,CultureInfo.InvariantCulture,out var ticks)||ticks<=0||ticks>DateTime.MaxValue.Ticks)return false;
        request=new RebirthNpcPendingSpawn(node);return true;
    }
}
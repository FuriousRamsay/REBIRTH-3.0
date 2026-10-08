using System;
using System.Globalization;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

// Immutable required native metadata omitted by Entity.Write. No native-ID grants.
internal sealed class RebirthNpcNativeHeader
{
    private static readonly string[] Integers={"team","deathTime","homeX","homeY","homeZ","homeRange","clientId","sleeperPose","stunKnee","stunProne","stunType","headState","bodyPart","focusedSlot","holdingMode","statWait","statNetWait"};
    private static readonly string[] Floats={"lifetime","size","headSize","stunDuration","statEnclosed","statBuffRemainder"};
    private static readonly string[] Booleans={"ground","sleeper","passiveSleeper","sleeping","dancing","spawnShare","crawler","nameNull","spawnNameNull"};
    private static readonly string[] Strings={"name","spawnName","belongsKind","belongsKey","spawnKind","spawnKey"};
    private readonly XElement image;
    private RebirthNpcNativeHeader(XElement source){image=new XElement(source);}
    internal XElement Write()=>new XElement(image);
    internal int Integer(string name)=>int.Parse((string)image.Attribute(name),CultureInfo.InvariantCulture);
    internal float Number(string name)=>float.Parse((string)image.Attribute(name),CultureInfo.InvariantCulture);
    internal bool Flag(string name)=>(string)image.Attribute(name)=="1";
    internal string Text(string name)=>(string)image.Attribute(name);
    internal static bool TryRead(XElement source,out RebirthNpcNativeHeader value)
    {
        value=null;
        if(source==null||source.Name!="nativeHeader"||source.HasElements||
            source.Nodes().Any(n=>!(n is XText t&&string.IsNullOrWhiteSpace(t.Value)))||
            source.Attributes().Count()!=2+Integers.Length+Floats.Length+Booleans.Length+Strings.Length||
            (string)source.Attribute("version")!="1"||(string)source.Attribute("required")!="1")return false;
        foreach(string name in Integers)
        {
            string text=(string)source.Attribute(name);
            if(!int.TryParse(text,NumberStyles.AllowLeadingSign,CultureInfo.InvariantCulture,out int parsed)||parsed.ToString(CultureInfo.InvariantCulture)!=text)return false;
        }
        foreach(string name in Floats)
        {
            string text=(string)source.Attribute(name);
            if(!float.TryParse(text,NumberStyles.Float,CultureInfo.InvariantCulture,out float parsed)||float.IsNaN(parsed)||float.IsInfinity(parsed)||parsed.ToString("R",CultureInfo.InvariantCulture)!=text)return false;
        }
        foreach(string name in Booleans){string text=(string)source.Attribute(name);if(text!="0"&&text!="1")return false;}
        foreach(string name in Strings)
        {
            string text=(string)source.Attribute(name);if(text==null||text.Length>4096)return false;
            try{XmlConvert.VerifyXmlChars(text);}catch(XmlException){return false;}
        }
        int holding=(int)source.Attribute("holdingMode"),focused=(int)source.Attribute("focusedSlot");
        // Transient hands/action coroutines have no durable reconstruction contract.
        if(holding<0||holding>1||focused<0||focused>=RebirthNpcNativeReconstruction.MaximumSlots||
            (int)source.Attribute("clientId")>0||
            (string)source.Attribute("nameNull")=="1"&&((string)source.Attribute("name")).Length!=0||
            (string)source.Attribute("spawnNameNull")=="1"&&((string)source.Attribute("spawnName")).Length!=0||
            !Identity((string)source.Attribute("belongsKind"),(string)source.Attribute("belongsKey"),false)||
            !Identity((string)source.Attribute("spawnKind"),(string)source.Attribute("spawnKey"),true))return false;
        value=new RebirthNpcNativeHeader(source);return true;
    }
    private static bool Identity(string kind,string key,bool allowNpc)
    {
        if(kind=="none")return int.TryParse(key,NumberStyles.AllowLeadingSign,CultureInfo.InvariantCulture,out var id)&&id<=0&&id.ToString(CultureInfo.InvariantCulture)==key;
        if(kind=="player")return !string.IsNullOrWhiteSpace(key)&&key==key.Trim();
        if(kind=="npc"&&allowNpc)return RebirthNpcStableId.TryParse(key,out var stable)&&!stable.IsEmpty&&stable.ToString()==key;
        return false;
    }
}
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

#nullable disable

// Immutable per-key provenance inside the existing world journal, not a second identity store.
internal sealed class RebirthNpcSpawnCompletion
{
    private readonly XElement image;
    private static readonly string[] Fields={"version","key","stable","profile","entity","nativeId","generation","completed"};
    internal string ReplayKey=>(string)image.Attribute("key");
    internal RebirthNpcStableId Stable {get{RebirthNpcStableId.TryParse((string)image.Attribute("stable"),out var value);return value;}}
    internal string Profile=>(string)image.Attribute("profile");
    internal string EntityClass=>(string)image.Attribute("entity");
    internal int NativeId=>(int)image.Attribute("nativeId");
    internal uint Generation=>(uint)image.Attribute("generation");
    private RebirthNpcSpawnCompletion(XElement node){image=new XElement(node);}
    internal XElement Write()=>new XElement(image);
    internal bool Matches(RebirthNpcStableId stable,string profile,string entity,int nativeId,uint generation)=>
        Stable==stable&&Profile==profile&&EntityClass==entity&&NativeId==nativeId&&Generation==generation;
    internal static bool TryCreate(RebirthNpcPendingSpawn request,uint generation,long ticks,out RebirthNpcSpawnCompletion value)
    {
        value=null;if(request==null||!request.IsPublishing)return false;
        return TryRead(new XElement("completedSpawn",new XAttribute("version","1"),new XAttribute("key",request.ReplayKey),
            new XAttribute("stable",request.StableId),new XAttribute("profile",request.Profile),new XAttribute("entity",request.EntityClass),
            new XAttribute("nativeId",request.NativeEntityId.ToString(CultureInfo.InvariantCulture)),
            new XAttribute("generation",generation.ToString(CultureInfo.InvariantCulture)),
            new XAttribute("completed",ticks.ToString(CultureInfo.InvariantCulture))),out value);
    }
    internal static bool TryRead(XElement node,out RebirthNpcSpawnCompletion value)
    {
        value=null;
        if(node==null||node.Name!="completedSpawn"||node.Nodes().Any()||node.Attributes().Count()!=Fields.Length||
            node.Attributes().Any(a=>!Fields.Contains(a.Name.ToString()))||(string)node.Attribute("version")!="1"||
            !RebirthNpcSpawnReplayCodec.ValidKey((string)node.Attribute("key"))||
            !((string)node.Attribute("key")).StartsWith("spawn:",StringComparison.Ordinal)||
            !RebirthNpcStableId.TryParse((string)node.Attribute("stable"),out var stable)||stable.IsEmpty||stable.ToString()!=(string)node.Attribute("stable")||
            !Text((string)node.Attribute("profile"))||!Text((string)node.Attribute("entity"))||
            !int.TryParse((string)node.Attribute("nativeId"),NumberStyles.None,CultureInfo.InvariantCulture,out int nativeId)||nativeId<=0||nativeId.ToString(CultureInfo.InvariantCulture)!=(string)node.Attribute("nativeId")||
            !uint.TryParse((string)node.Attribute("generation"),NumberStyles.None,CultureInfo.InvariantCulture,out uint generation)||generation==0||generation.ToString(CultureInfo.InvariantCulture)!=(string)node.Attribute("generation")||
            !long.TryParse((string)node.Attribute("completed"),NumberStyles.None,CultureInfo.InvariantCulture,out long ticks)||ticks<=0||ticks>DateTime.MaxValue.Ticks||ticks.ToString(CultureInfo.InvariantCulture)!=(string)node.Attribute("completed"))return false;
        value=new RebirthNpcSpawnCompletion(node);return true;
    }
    private static bool Text(string value)
    {
        if(string.IsNullOrWhiteSpace(value)||value.Length>128||value!=value.Trim()||value.Any(char.IsControl))return false;
        try{XmlConvert.VerifyXmlChars(value);return true;}catch(XmlException){return false;}
    }
    internal static bool TryReadSection(XElement root,bool required,out Dictionary<string,RebirthNpcSpawnCompletion> values)
    {
        values=new Dictionary<string,RebirthNpcSpawnCompletion>(StringComparer.Ordinal);if(root==null)return false;
        var sections=root.Elements().Where(e=>string.Equals(e.Name.LocalName,"completedSpawns",StringComparison.OrdinalIgnoreCase)).ToArray();
        if(sections.Length==0)return !required;
        if(sections.Length!=1||sections[0].Name!="completedSpawns")return false;
        var section=sections[0];
        if(section.Attributes().Count()!=2||(string)section.Attribute("version")!="1"||(string)section.Attribute("required")!="1"||
            section.Nodes().Any(n=>!(n is XElement)&&(!(n is XText)||!string.IsNullOrWhiteSpace(((XText)n).Value))))return false;
        foreach(var node in section.Elements())
            if(values.Count>=RebirthNpcSpawnReplayCodec.MaximumRecords||!TryRead(node,out var value)||values.ContainsKey(value.ReplayKey))return false;
            else values.Add(value.ReplayKey,value);
        return true;
    }
    internal static void WriteSection(XmlWriter writer,Dictionary<string,RebirthNpcSpawnCompletion> values)
    {
        if(writer==null||values==null||values.Count>RebirthNpcSpawnReplayCodec.MaximumRecords)throw new InvalidOperationException("Completion provenance collection invalid.");
        foreach(var row in values)if(row.Value==null||row.Key!=row.Value.ReplayKey)throw new InvalidOperationException("Completion provenance key invalid.");
        writer.WriteStartElement("completedSpawns");writer.WriteAttributeString("version","1");writer.WriteAttributeString("required","1");
        foreach(var row in values.OrderBy(p=>p.Key,StringComparer.Ordinal))row.Value.Write().WriteTo(writer);
        writer.WriteEndElement();
    }
}
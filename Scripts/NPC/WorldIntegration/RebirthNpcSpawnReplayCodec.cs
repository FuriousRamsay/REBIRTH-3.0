using System;
using System.Collections.Generic;
using System.Xml;

internal static class RebirthNpcSpawnReplayCodec
{
    internal const int MaximumRecords=65536;
    internal static bool ValidKey(string key)
    {
        if(string.IsNullOrWhiteSpace(key)||key.Length>512||key!=key.Trim()||
            !(key.StartsWith("spawn:",StringComparison.Ordinal)||key.StartsWith("promote:",StringComparison.Ordinal)))return false;
        int prefix=key.IndexOf(':');if(prefix==key.Length-1)return false;
        foreach(char ch in key)if(char.IsControl(ch))return false;
        return true;
    }
    internal static bool TryRead(XmlElement root,out HashSet<string> records)
    {
        records=new HashSet<string>(StringComparer.Ordinal);if(root==null)return false;
        var sections=root.SelectNodes("replays");if(sections.Count==0)return true;
        if(sections.Count!=1)return false;var section=sections[0];
        if(section.Attributes.Count!=1||section.Attributes["version"]?.Value!="1")return false;
        foreach(XmlNode node in section.ChildNodes)
        {
            if(node.NodeType==XmlNodeType.Whitespace||node.NodeType==XmlNodeType.SignificantWhitespace||
                node.NodeType==XmlNodeType.Text&&string.IsNullOrWhiteSpace(node.Value))continue;
            if(node.NodeType!=XmlNodeType.Element||node.Name!="replay"||node.Attributes.Count!=1||node.Attributes["key"]==null||
                node.HasChildNodes||!ValidKey(node.Attributes["key"].Value)||records.Count>=MaximumRecords||
                !records.Add(node.Attributes["key"].Value))return false;
        }
        return true;
    }
    internal static void Write(XmlWriter writer,HashSet<string> records)
    {
        if(writer==null||records==null||records.Count>MaximumRecords)throw new InvalidOperationException("NPC spawn replay collection invalid.");
        var ordered=new List<string>(records);ordered.Sort(StringComparer.Ordinal);
        foreach(string key in ordered)if(!ValidKey(key))throw new InvalidOperationException("NPC spawn replay key invalid.");
        writer.WriteStartElement("replays");writer.WriteAttributeString("version","1");
        foreach(string key in ordered){writer.WriteStartElement("replay");writer.WriteAttributeString("key",key);writer.WriteEndElement();}
        writer.WriteEndElement();
    }
}
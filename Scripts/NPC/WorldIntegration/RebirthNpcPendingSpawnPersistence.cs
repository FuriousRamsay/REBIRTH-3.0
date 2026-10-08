using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

internal static class RebirthNpcPendingSpawnPersistence
{
    internal static bool TryRead(XElement root,out Dictionary<string,RebirthNpcPendingSpawn> records)
    {
        records=new Dictionary<string,RebirthNpcPendingSpawn>(StringComparer.Ordinal);if(root==null)return false;
        var sections=root.Elements("pendingSpawns").ToArray();if(sections.Length==0)return true;
        if(sections.Length!=1)return false;var section=sections[0];
        if(section.Attributes().Count()!=1||(string)section.Attribute("version")!="1"||
            section.Nodes().Any(n=>!(n is XElement)&&(!(n is XText)||!string.IsNullOrWhiteSpace(((XText)n).Value))))return false;
        var identities=new HashSet<string>(StringComparer.Ordinal);
        foreach(var node in section.Elements())
        {
            if(records.Count>=1024||!RebirthNpcPendingSpawn.TryRead(node,out var record)||
                records.ContainsKey(record.ReplayKey)||!identities.Add(record.StableId))return false;
            records.Add(record.ReplayKey,record);
        }
        return true;
    }
    internal static void Write(XmlWriter writer,Dictionary<string,RebirthNpcPendingSpawn> records)
    {
        if(writer==null||records==null||records.Count>1024)throw new InvalidOperationException("Pending spawn collection invalid.");
        var identities=new HashSet<string>(StringComparer.Ordinal);
        foreach(var pair in records)if(pair.Value==null||pair.Key!=pair.Value.ReplayKey||!identities.Add(pair.Value.StableId))
            throw new InvalidOperationException("Pending spawn identity invalid.");
        writer.WriteStartElement("pendingSpawns");writer.WriteAttributeString("version","1");
        foreach(var pair in records.OrderBy(p=>p.Key,StringComparer.Ordinal))pair.Value.Write().WriteTo(writer);
        writer.WriteEndElement();
    }
}
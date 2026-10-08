using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

// Save-owned preparation records only. Presence does not prove debit or native queue acceptance.
public static class RebirthStationPreparationPersistence
{
    public const int MaximumRecords=64;
    public static bool MatchesOwner(IDictionary<string,RebirthStationGridAdmission> records,string creation)
    {
        if(records==null||records.Count>MaximumRecords)return false;
        if(records.Count==0)return true;
        if(!RebirthStationGridAdmission.TryNormalizeCreation(creation,out var owner))return false;
        return records.All(p=>p.Value!=null&&p.Key==p.Value.JobId&&p.Value.CreationId==owner);
    }
    // Caller must authenticate the owner and serialize admission on the authority thread.
    // True proves only that its save callback succeeded, never debit or queue acceptance.
    // Retain a failed/uncertain preparation for retry; never silently evict custody intent.
    public static bool TryRegister(IDictionary<string,RebirthStationGridAdmission> records,string creation,
        RebirthStationGridAdmission admission,Func<bool> save)
    {
        if(save==null||admission==null||!MatchesOwner(records,creation)||
            !RebirthStationGridAdmission.TryNormalizeCreation(creation,out var owner)||admission.CreationId!=owner)return false;
        if(records.TryGetValue(admission.JobId,out var existing))
        {
            if(existing==null||!XNode.DeepEquals(existing.Write(),admission.Write()))return false;
        }
        else
        {
            if(records.Count>=MaximumRecords||records.Values.Any(p=>p.SharesStation(admission)))return false;
            records.Add(admission.JobId,admission.Clone());
        }
        try{return save();}catch{return false;}
    }
    public static XElement Write(IDictionary<string,RebirthStationGridAdmission> records)
    {
        if(records==null||records.Count>MaximumRecords)throw new InvalidDataException("Invalid station preparations");
        var section=new XElement("stationPreparations",new XAttribute("version","1"));
        foreach(var pair in records.OrderBy(p=>p.Key,StringComparer.Ordinal))
        {
            if(pair.Value==null||pair.Key!=pair.Value.JobId)throw new InvalidDataException("Invalid station preparation identity");
            section.Add(pair.Value.Write());
        }
        return section;
    }
    public static bool TryRead(XElement progression,out Dictionary<string,RebirthStationGridAdmission> records,out string error)
    {
        records=null;error=string.Empty;
        if(progression==null){error="Missing station preparation parent";return false;}
        var sections=progression.Elements("stationPreparations").ToArray();
        if(sections.Length>1){error="Duplicate station preparations";return false;}
        var result=new Dictionary<string,RebirthStationGridAdmission>(StringComparer.Ordinal);
        if(sections.Length==0){records=result;return true;}
        var section=sections[0];
        if(section.Attributes().Count()!=1||(string)section.Attribute("version")!="1"||
            section.Nodes().Any(n=>!(n is XElement)&&(!(n is XText)||!string.IsNullOrWhiteSpace(((XText)n).Value))))
        {error="Invalid station preparations format";return false;}
        foreach(var node in section.Elements())
        {
            if(result.Count>=MaximumRecords||!RebirthStationGridAdmission.TryReadStored(node,out var record)||result.ContainsKey(record.JobId))
            {error="Invalid or duplicate station preparation; custody records were not loaded";return false;}
            result.Add(record.JobId,record);
        }
        records=result;return true;
    }
}
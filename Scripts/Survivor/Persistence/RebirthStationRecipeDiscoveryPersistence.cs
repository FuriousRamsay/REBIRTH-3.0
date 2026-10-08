using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

// Optional permanent record storage. Syntax/owner binding never proves a paid completion.
internal static class RebirthStationRecipeDiscoveryPersistence
{
    internal const int MaximumRecords=8192;
    internal const int MaximumBytes=8388608;
    internal static XElement Write(IDictionary<string,RebirthStationRecipeDiscoveryRecord> records,string owner)
    {
        var section=new XElement("recipeDiscoveries",new XAttribute("version","1"));
        if(records==null||records.Count>MaximumRecords)throw new InvalidDataException("Invalid recipe discovery collection.");
        var jobs=new HashSet<string>(StringComparer.Ordinal);
        foreach(var pair in records.OrderBy(p=>p.Key,StringComparer.Ordinal))
        {
            RebirthStationRecipeDiscoveryRecord validated;
            if(pair.Value==null||pair.Key!=pair.Value.CanonicalRecipe||!jobs.Add(pair.Value.JobId)||
                !RebirthStationRecipeDiscoveryRecord.TryReadStored(pair.Value.Write(),out validated)||
                (string)validated.Write().Attribute("owner")!=owner)throw new InvalidDataException("Invalid recipe discovery owner or record.");
            section.Add(validated.Write());
        }
        if(new UTF8Encoding(false,true).GetByteCount(section.ToString(SaveOptions.DisableFormatting))>MaximumBytes)
            throw new InvalidDataException("Recipe discovery section exceeds budget.");
        return section;
    }
    internal static bool TryRead(XElement progression,string owner,out Dictionary<string,RebirthStationRecipeDiscoveryRecord> records,out string error)
    {
        records=null;error=null;
        try
        {
            if(progression==null){error="Missing progression";return false;}
            var sections=progression.Elements("recipeDiscoveries").ToArray();
            if(sections.Length>1){error="Duplicate recipe discovery section";return false;}
            var result=new Dictionary<string,RebirthStationRecipeDiscoveryRecord>(StringComparer.Ordinal);
            if(sections.Length==0){records=result;return true;}
            var section=sections[0];
            if(section.Attributes().Count()!=1||(string)section.Attribute("version")!="1"||
                section.Nodes().Any(n=>!(n is XElement)&&(!(n is XText)||!string.IsNullOrWhiteSpace(((XText)n).Value))))
            {error="Invalid recipe discovery section";return false;}
            if(new UTF8Encoding(false,true).GetByteCount(section.ToString(SaveOptions.DisableFormatting))>MaximumBytes)
            {error="Recipe discovery section exceeds budget";return false;}
            var jobs=new HashSet<string>(StringComparer.Ordinal);
            foreach(var node in section.Elements())
            {
                RebirthStationRecipeDiscoveryRecord record;
                if(result.Count>=MaximumRecords||!RebirthStationRecipeDiscoveryRecord.TryReadStored(node,out record)||
                    (string)record.Write().Attribute("owner")!=owner||result.ContainsKey(record.CanonicalRecipe)||!jobs.Add(record.JobId))
                {error="Invalid or duplicate recipe discovery record";return false;}
                result.Add(record.CanonicalRecipe,record);
            }
            records=result;return true;
        }
        catch(Exception){error="Invalid recipe discovery encoding";return false;}
    }
    internal static bool MatchesCreation(IEnumerable<RebirthStationRecipeDiscoveryRecord> records,string creation)
        =>records!=null&&records.All(r=>r!=null&&(string)r.Write().Attribute("creation")==creation);
}
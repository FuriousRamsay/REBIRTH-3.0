using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

// B07: configurable minimum water level for NPC provider maintenance. Stored as one
// optional element in the NPC aggregate record, so older saves (no element) resolve to
// the default and older builds ignore the unknown element. No world or item effect here.
internal static class RebirthP8NpcWaterPolicy
{
    private const string Element="farmWaterPolicy";
    internal const int DefaultPercent=50,MinPercent=5,MaxPercent=100;

    internal static int ThresholdUnits(int maxWater,int percent)
    {
        if(maxWater<=0)return 0;
        percent=Math.Max(MinPercent,Math.Min(MaxPercent,percent));
        return Math.Min(maxWater,(int)Math.Ceiling(maxWater*(double)percent/100d));
    }

    internal static int GetPercent(RebirthNpcStableId npc)
    {
        try
        {
            if(npc.IsEmpty||!RebirthNpcAggregatePersistenceStore.TryReadSavedPerson(npc,out var person))return DefaultPercent;
            var row=Read(person);
            if(row==null||(string)row.Attribute("version")!="1"||!int.TryParse((string)row.Attribute("percent"),out int percent)||
               percent<MinPercent||percent>MaxPercent)return DefaultPercent;
            return percent;
        }
        catch{return DefaultPercent;}
    }

    // Standing maintenance is OFF unless the Work Board saved enabled=1 with a Farm target id; rows from
    // before this field resolve to disabled, so existing saves never start refilling on their own.
    internal static bool GetEnabled(RebirthNpcStableId npc,out string farmTarget)
    {
        farmTarget=null;
        try
        {
            if(npc.IsEmpty||!RebirthNpcAggregatePersistenceStore.TryReadSavedPerson(npc,out var person))return false;
            var row=Read(person);
            if(row==null||(string)row.Attribute("enabled")!="1")return false;
            farmTarget=(string)row.Attribute("farm");
            return !string.IsNullOrEmpty(farmTarget)&&farmTarget.Length<=256;
        }
        catch{return false;}
    }
    internal static bool TrySetPercent(RebirthNpcStableId npc,int percent)
    {
        bool enabled=GetEnabled(npc,out string farm);
        return TryConfigure(npc,percent,enabled,farm);
    }

    // Server only. Takes effect for the next prepared refill; a retained original keeps the threshold
    // it was prepared with, so a policy edit never re-interprets saved work.
    // Accepts a bare desk id or the full key and stores the canonical "desk:<id>:Farm" (null when unusable).
    internal static string CanonicalFarm(string value)
    {
        value=value?.Trim();
        if(string.IsNullOrEmpty(value)||value.Length>200)return null;
        var parts=value.Split(':');
        if(parts.Length==1)return "desk:"+value+":Farm";
        return parts.Length==3&&parts[0]=="desk"&&parts[1].Length>0&&parts[2]=="Farm"?value:null;
    }
    internal static bool TryConfigure(RebirthNpcStableId npc,int percent,bool enabled,string farmTarget)
    {
        try
        {
            farmTarget=CanonicalFarm(farmTarget);
            if(enabled&&farmTarget==null)return false;
            var world=GameManager.Instance?.World;
            if(npc.IsEmpty||percent<MinPercent||percent>MaxPercent||world==null||world.IsRemote()||!ThreadManager.IsMainThread())return false;
            var image=new XElement(Element,new XAttribute("version",1),new XAttribute("percent",percent),
                new XAttribute("enabled",enabled&&!string.IsNullOrEmpty(farmTarget)?"1":"0"),new XAttribute("farm",farmTarget??string.Empty));
            string raw=image.ToString(SaveOptions.DisableFormatting);
            if(!RebirthNpcAggregatePersistenceStore.Mutate(npc,record=>
            {
                var rows=(record.UnknownOptionalElements??Array.Empty<string>()).Where(x=>XElement.Parse(x).Name!=Element);
                record.UnknownOptionalElements=rows.Concat(new[]{raw}).ToArray();
            }))return false;
            RebirthNpcAggregatePersistenceStore.Save();
            return RebirthNpcAggregatePersistenceStore.TryReadSavedPerson(npc,out var saved)&&XNode.DeepEquals(Read(saved),image);
        }
        catch{return false;}
    }

    private static XElement Read(RebirthNpcPersistentRecord person)
    {
        XElement result=null;
        foreach(var raw in person.UnknownOptionalElements??Array.Empty<string>())
        {
            var row=RebirthCompanionDeskStore.Parse(raw);
            if(row.Name!=Element)continue;
            if(result!=null)throw new InvalidDataException("Duplicate water policy.");
            result=row;
        }
        return result;
    }
}

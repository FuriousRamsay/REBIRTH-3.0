using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;

internal static class RebirthHordeNightStatisticsPersistence
{
    internal static XElement Write(RebirthStatisticsRecord record)
    {
        var totals=record.HordeNightTotals;
        var root=new XElement("hordeNights",new XAttribute("version",1));
        var lifetime=Counters("lifetime",totals.Kills,totals.HeadshotKills,totals.HeadDecapitations,totals.Deaths);
        lifetime.Add(new XAttribute("nights",totals.NightsParticipated),new XAttribute("bestKills",totals.BestNightKills),new XAttribute("bestDay",totals.BestNightWorldDay));
        AddMaps(lifetime,totals.WeaponCategories,totals.ZombieTypes,totals.ZombieVariants);root.Add(lifetime);
        foreach(var night in record.HordeNights.OrderBy(x=>x.WorldDay))
        {
            var node=Counters("night",night.Kills,night.HeadshotKills,night.HeadDecapitations,night.Deaths);
            node.Add(new XAttribute("day",night.WorldDay),new XAttribute("completed",night.Completed));
            AddMaps(node,night.WeaponCategories,night.ZombieTypes,night.ZombieVariants);root.Add(node);
        }
        return root;
    }
    private static XElement Counters(string name,long kills,long heads,long decapitations,long deaths)=>new XElement(name,
        new XAttribute("kills",kills),new XAttribute("headshots",heads),new XAttribute("decapitations",decapitations),new XAttribute("deaths",deaths));
    private static void AddMaps(XElement node,Dictionary<string,long> weapons,Dictionary<string,long> types,Dictionary<string,long> variants)
    {AddMap(node,"weapons",weapons);AddMap(node,"types",types);AddMap(node,"variants",variants);}
    private static void AddMap(XElement node,string name,Dictionary<string,long> values)
    {node.Add(new XElement(name,values.OrderBy(x=>x.Key,StringComparer.OrdinalIgnoreCase).Select(x=>new XElement("entry",new XAttribute("id",x.Key),new XAttribute("count",x.Value)))));}
    internal static void Read(XElement root,RebirthStatisticsRecord record)
    {
        if(root==null)return;
        if(Number(root,"version")!=1)throw new InvalidDataException("Unsupported Horde Night statistics version.");
        var lifetime=root.Element("lifetime");if(lifetime==null)throw new InvalidDataException("Missing Horde Night lifetime totals.");
        var totals=record.HordeNightTotals;
        totals.Kills=Number(lifetime,"kills");totals.HeadshotKills=Number(lifetime,"headshots");totals.HeadDecapitations=Number(lifetime,"decapitations");totals.Deaths=Number(lifetime,"deaths");
        totals.NightsParticipated=Number(lifetime,"nights");totals.BestNightKills=Number(lifetime,"bestKills");totals.BestNightWorldDay=Day(lifetime,"bestDay");
        ReadMaps(lifetime,totals.WeaponCategories,totals.ZombieTypes,totals.ZombieVariants);
        ValidateCounters(totals.Kills,totals.HeadshotKills,totals.HeadDecapitations);
        var days=new HashSet<int>();
        long kills=0,heads=0,decaps=0,deaths=0,best=0;
        int unfinished=0;
        foreach(var node in root.Elements("night"))
        {
            int day=Day(node,"day");if(day<1||!days.Add(day))throw new InvalidDataException("Invalid Horde Night day.");
            bool completed;if(!bool.TryParse((string)node.Attribute("completed"),out completed))throw new InvalidDataException("Invalid Horde Night completion.");
            var night=new RebirthHordeNightStatistics{WorldDay=day,Completed=completed,Kills=Number(node,"kills"),HeadshotKills=Number(node,"headshots"),HeadDecapitations=Number(node,"decapitations"),Deaths=Number(node,"deaths")};
            ValidateCounters(night.Kills,night.HeadshotKills,night.HeadDecapitations);
            kills=Sum(kills,night.Kills);heads=Sum(heads,night.HeadshotKills);decaps=Sum(decaps,night.HeadDecapitations);deaths=Sum(deaths,night.Deaths);
            best=Math.Max(best,night.Kills);
            if(!completed&&++unfinished>1)throw new InvalidDataException("Multiple active Horde Night records.");
            ReadMaps(node,night.WeaponCategories,night.ZombieTypes,night.ZombieVariants);record.AddHordeNight(night);
        }
        if(totals.NightsParticipated!=days.Count||totals.Kills!=kills||totals.HeadshotKills!=heads||totals.HeadDecapitations!=decaps||totals.Deaths!=deaths||totals.BestNightKills!=best)
            throw new InvalidDataException("Horde Night lifetime totals do not match history.");
        RebirthHordeNightStatistics bestNight;
        if(best>0&&(!record.HordeNightIndex.TryGetValue(totals.BestNightWorldDay,out bestNight)||bestNight.Kills!=best)
            ||best==0&&totals.BestNightWorldDay!=0)
            throw new InvalidDataException("Invalid best Horde Night reference.");
    }
    private static void ValidateCounters(long kills,long heads,long decaps)
    {if(decaps>heads||heads>kills)throw new InvalidDataException("Invalid Horde Night headshot totals.");}
    private static long Sum(long left,long right)=>left>long.MaxValue-right?long.MaxValue:left+right;
    private static void ReadMaps(XElement node,Dictionary<string,long> weapons,Dictionary<string,long> types,Dictionary<string,long> variants)
    {ReadMap(node.Element("weapons"),weapons);ReadMap(node.Element("types"),types);ReadMap(node.Element("variants"),variants);}
    private static void ReadMap(XElement node,Dictionary<string,long> values)
    {
        if(node==null)throw new InvalidDataException("Missing Horde Night breakdown.");
        foreach(var entry in node.Elements("entry"))
        {
            string id=(string)entry.Attribute("id");
            if(string.IsNullOrEmpty(id)||id.Length>256||values.ContainsKey(id)||values.Count>=4096)throw new InvalidDataException("Invalid Horde Night breakdown entry.");
            values.Add(id,Number(entry,"count"));
        }
    }
    private static int Day(XElement node,string key){long day=Number(node,key);if(day>int.MaxValue)throw new InvalidDataException("Invalid Horde Night day.");return (int)day;}
    private static long Number(XElement node,string key)
    {long value;if(!long.TryParse((string)node.Attribute(key),NumberStyles.Integer,CultureInfo.InvariantCulture,out value)||value<0)throw new InvalidDataException("Invalid Horde Night counter "+key);return value;}
}

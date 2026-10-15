using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

// Lifetime totals stay independent of the bounded rankings sent to the character window.
internal static class RebirthHordeNightStatisticsProjection
{
    internal const int RankingLimit=64;
    internal static RebirthHordeNightLifetimeStatistics Lifetime(RebirthHordeNightLifetimeStatistics source)
    {
        var result=new RebirthHordeNightLifetimeStatistics
        {
            NightsParticipated=source.NightsParticipated,Kills=source.Kills,HeadshotKills=source.HeadshotKills,
            HeadDecapitations=source.HeadDecapitations,Deaths=source.Deaths,
            BestNightKills=source.BestNightKills,BestNightWorldDay=source.BestNightWorldDay
        };
        Rank(source.WeaponCategories,result.WeaponCategories);
        Rank(source.ZombieTypes,result.ZombieTypes);
        Rank(source.ZombieVariants,result.ZombieVariants);
        return result;
    }
    private static void Rank(Dictionary<string,long> source,Dictionary<string,long> target)
    {
        foreach(var entry in source.OrderByDescending(x=>x.Value).ThenBy(x=>x.Key,StringComparer.OrdinalIgnoreCase).Take(RankingLimit))
            target.Add(entry.Key,entry.Value);
    }
    internal static void History(RebirthStatisticsRecord record,RebirthStatisticsSnapshot snapshot,int page,int selectedDay)
    {
        int size=RebirthStatisticsProtocol.HordeHistoryPageSize;
        snapshot.HordeHistoryCount=record.HordeNights.Count;
        snapshot.HordeHistoryPage=Math.Min(Math.Max(0,page),Math.Max(0,(record.HordeNights.Count-1)/size));
        foreach(var night in record.HordeNights.OrderByDescending(x=>x.WorldDay).Skip(snapshot.HordeHistoryPage*size).Take(size))
            snapshot.HordeHistory.Add(Summary(night));
        RebirthHordeNightStatistics selected;
        if(selectedDay>0&&record.HordeNightIndex.TryGetValue(selectedDay,out selected))
        {
            snapshot.SelectedHordeNight=Summary(selected);
            Rank(selected.WeaponCategories,snapshot.SelectedHordeNight.WeaponCategories);
            Rank(selected.ZombieTypes,snapshot.SelectedHordeNight.ZombieTypes);
            Rank(selected.ZombieVariants,snapshot.SelectedHordeNight.ZombieVariants);
        }
    }
    private static RebirthHordeNightStatistics Summary(RebirthHordeNightStatistics source)=>new RebirthHordeNightStatistics
    {WorldDay=source.WorldDay,Completed=source.Completed,Kills=source.Kills,HeadshotKills=source.HeadshotKills,HeadDecapitations=source.HeadDecapitations,Deaths=source.Deaths};
    internal static int EstimateHistory(RebirthStatisticsSnapshot snapshot)
    {
        int length=10+37*snapshot.HordeHistory.Count;
        if(snapshot.SelectedHordeNight!=null)
        {
            length+=40;
            foreach(var map in new[]{snapshot.SelectedHordeNight.WeaponCategories,snapshot.SelectedHordeNight.ZombieTypes,snapshot.SelectedHordeNight.ZombieVariants})
                foreach(var entry in map.Take(RankingLimit))length+=8+RebirthSurvivorNetworkCodec.EstimateString(entry.Key,256);
        }
        return length;
    }
    internal static void WriteHistory(BinaryWriter writer,RebirthStatisticsSnapshot snapshot)
    {
        writer.Write(snapshot.HordeHistoryPage);writer.Write(snapshot.HordeHistoryCount);
        int count=Math.Min(snapshot.HordeHistory.Count,RebirthStatisticsProtocol.HordeHistoryPageSize);writer.Write((byte)count);
        for(int i=0;i<count;i++)WriteNight(writer,snapshot.HordeHistory[i]);
        writer.Write(snapshot.SelectedHordeNight!=null);
        if(snapshot.SelectedHordeNight==null)return;
        var selected=snapshot.SelectedHordeNight;WriteNight(writer,selected);
        WriteMap(writer,selected.WeaponCategories);WriteMap(writer,selected.ZombieTypes);WriteMap(writer,selected.ZombieVariants);
    }
    internal static void ReadHistory(BinaryReader reader,RebirthStatisticsSnapshot snapshot)
    {
        snapshot.HordeHistoryPage=reader.ReadInt32();snapshot.HordeHistoryCount=reader.ReadInt32();
        int count=reader.ReadByte();
        if(snapshot.HordeHistoryPage<0||snapshot.HordeHistoryCount<0||count>RebirthStatisticsProtocol.HordeHistoryPageSize||count>snapshot.HordeHistoryCount
            ||snapshot.HordeHistoryPage>Math.Max(0,(snapshot.HordeHistoryCount-1)/RebirthStatisticsProtocol.HordeHistoryPageSize))
            throw new InvalidDataException("Invalid Horde Night history page.");
        int previous=int.MaxValue;
        for(int i=0;i<count;i++)
        {
            var night=ReadNight(reader);if(night.WorldDay>=previous)throw new InvalidDataException("Unsorted Horde Night history.");
            previous=night.WorldDay;snapshot.HordeHistory.Add(night);
        }
        if(!reader.ReadBoolean())return;
        var selected=ReadNight(reader);snapshot.SelectedHordeNight=selected;
        ReadMap(reader,selected.WeaponCategories);ReadMap(reader,selected.ZombieTypes);ReadMap(reader,selected.ZombieVariants);
    }
    private static void WriteNight(BinaryWriter writer,RebirthHordeNightStatistics night)
    {writer.Write(night.WorldDay);writer.Write(night.Completed);writer.Write(night.Kills);writer.Write(night.HeadshotKills);writer.Write(night.HeadDecapitations);writer.Write(night.Deaths);}
    private static RebirthHordeNightStatistics ReadNight(BinaryReader reader)
    {
        var night=new RebirthHordeNightStatistics{WorldDay=reader.ReadInt32(),Completed=reader.ReadBoolean(),Kills=Counter(reader),HeadshotKills=Counter(reader),HeadDecapitations=Counter(reader),Deaths=Counter(reader)};
        if(night.WorldDay<1||night.HeadDecapitations>night.HeadshotKills||night.HeadshotKills>night.Kills)throw new InvalidDataException("Invalid Horde Night history counters.");
        return night;
    }
    internal static int Estimate(RebirthHordeNightLifetimeStatistics value)
    {
        int length=55;
        foreach(var map in new[]{value.WeaponCategories,value.ZombieTypes,value.ZombieVariants})
            foreach(var entry in map.Take(RankingLimit))length+=8+RebirthSurvivorNetworkCodec.EstimateString(entry.Key,256);
        return length;
    }
    internal static void Write(BinaryWriter writer,RebirthHordeNightLifetimeStatistics value)
    {
        writer.Write(value.NightsParticipated);writer.Write(value.Kills);writer.Write(value.HeadshotKills);
        writer.Write(value.HeadDecapitations);writer.Write(value.Deaths);writer.Write(value.BestNightKills);writer.Write(value.BestNightWorldDay);
        WriteMap(writer,value.WeaponCategories);WriteMap(writer,value.ZombieTypes);WriteMap(writer,value.ZombieVariants);
    }
    internal static RebirthHordeNightLifetimeStatistics Read(BinaryReader reader)
    {
        var result=new RebirthHordeNightLifetimeStatistics
        {
            NightsParticipated=Counter(reader),Kills=Counter(reader),HeadshotKills=Counter(reader),
            HeadDecapitations=Counter(reader),Deaths=Counter(reader),BestNightKills=Counter(reader),BestNightWorldDay=reader.ReadInt32()
        };
        if(result.BestNightWorldDay<0||result.HeadDecapitations>result.HeadshotKills||result.HeadshotKills>result.Kills||result.BestNightKills>result.Kills)
            throw new InvalidDataException("Invalid Horde Night totals.");
        ReadMap(reader,result.WeaponCategories);ReadMap(reader,result.ZombieTypes);ReadMap(reader,result.ZombieVariants);
        return result;
    }
    private static long Counter(BinaryReader reader)
    {long value=reader.ReadInt64();if(value<0)throw new InvalidDataException("Invalid Horde Night count.");return value;}
    private static void WriteMap(BinaryWriter writer,Dictionary<string,long> map)
    {
        var entries=map.OrderByDescending(x=>x.Value).ThenBy(x=>x.Key,StringComparer.OrdinalIgnoreCase).Take(RankingLimit).ToArray();
        writer.Write((byte)entries.Length);
        foreach(var entry in entries){RebirthSurvivorNetworkCodec.WriteString(writer,entry.Key,256);writer.Write(entry.Value);}
    }
    private static void ReadMap(BinaryReader reader,Dictionary<string,long> map)
    {
        int count=reader.ReadByte();if(count>RankingLimit)throw new InvalidDataException("Invalid Horde Night ranking size.");
        for(int i=0;i<count;i++)
        {
            string key=RebirthSurvivorNetworkCodec.ReadBoundedString(reader,256);long value=Counter(reader);
            if(string.IsNullOrEmpty(key)||map.ContainsKey(key))throw new InvalidDataException("Invalid Horde Night ranking entry.");
            map.Add(key,value);
        }
    }
}

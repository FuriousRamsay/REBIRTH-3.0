using System;
using System.IO;
using UnityEngine.Scripting;

#nullable disable

public static class RebirthStatisticsClientState
{
    private static readonly object Sync = new object();
    private static RebirthStatisticsSnapshot snapshot;
    private static long generation;
    private static bool pending;
    public static event Action<RebirthStatisticsSnapshot> Changed;
    public static event Action ProjectionChanged;
    public static long Generation { get { lock (Sync) return generation; } }
    public static long KnownEpoch { get { lock (Sync) return snapshot != null ? snapshot.Epoch : 0L; } }
    public static long KnownRevision { get { lock (Sync) return snapshot != null ? snapshot.Revision : 0L; } }
    public static void Reset() { lock (Sync) { snapshot = null; unchecked { generation++; } pending = true; } }
    public static RebirthStatisticsSnapshot Get() { long ignored; return Get(out ignored); }
    public static RebirthStatisticsSnapshot Get(out long version)
    { lock (Sync) { version = generation; return snapshot != null ? snapshot.Clone() : null; } }
    internal static void Receive(RebirthStatisticsSnapshot incoming)
    {
        if (incoming == null || incoming.ProtocolVersion != RebirthStatisticsProtocol.Version) return;
        lock (Sync)
        {
            if (snapshot != null && (incoming.Epoch < snapshot.Epoch || (incoming.Epoch == snapshot.Epoch && incoming.Revision < snapshot.Revision))) return;
            snapshot = incoming.Clone(); unchecked { generation++; } pending = true;
        }
    }
    // The Statistics GameUpdate hook calls this on clients as well as the authority.
    public static void PumpNotifications()
    {
        RebirthStatisticsSnapshot accepted = null;
        Action<RebirthStatisticsSnapshot> legacy = Changed;
        lock (Sync)
        {
            if (!pending) return;
            pending = false;
            if (legacy != null && snapshot != null) accepted = snapshot.Clone();
        }
        if (legacy != null) foreach (Action<RebirthStatisticsSnapshot> listener in legacy.GetInvocationList())
            try { listener(accepted != null ? accepted.Clone() : null); }
            catch (Exception ex) { Log.Warning("[REBIRTH Statistics] listener failed: " + ex.Message); }
        Action handlers = ProjectionChanged;
        if (handlers != null) foreach (Action listener in handlers.GetInvocationList())
            try { listener(); } catch (Exception ex) { Log.Warning("[REBIRTH Statistics] projection listener failed: " + ex.Message); }
    }
}

[Preserve]
public sealed class NetPackageRebirthStatisticsRequest : NetPackage
{
    private int protocol=RebirthStatisticsProtocol.Version; private int playerEntityId; private long knownEpoch; private long knownRevision; private bool force;
    private bool history;private int historyPage,historyDay;
    public override NetPackageDirection PackageDirection { get { return NetPackageDirection.ToServer; } }
    public NetPackageRebirthStatisticsRequest Setup(int entityId,long epoch,long known,bool forceRefresh){protocol=RebirthStatisticsProtocol.Version;playerEntityId=entityId;knownEpoch=Math.Max(0L,epoch);knownRevision=Math.Max(0L,known);force=forceRefresh;history=false;historyPage=historyDay=0;return this;}
    public NetPackageRebirthStatisticsRequest SetupHistory(int entityId,int page,int day){Setup(entityId,0,0,true);history=true;historyPage=page;historyDay=day;return this;}
    public override void read(PooledBinaryReader reader){BinaryReader b=(BinaryReader)reader;protocol=b.ReadInt32();playerEntityId=b.ReadInt32();knownEpoch=b.ReadInt64();knownRevision=b.ReadInt64();force=b.ReadBoolean();history=false;historyPage=historyDay=0;if(protocol>=4){history=b.ReadBoolean();historyPage=b.ReadInt32();historyDay=b.ReadInt32();}}
    public override void write(PooledBinaryWriter writer){base.write(writer);BinaryWriter b=(BinaryWriter)writer;b.Write(protocol);b.Write(playerEntityId);b.Write(knownEpoch);b.Write(knownRevision);b.Write(force);if(protocol>=4){b.Write(history);b.Write(historyPage);b.Write(historyDay);}}
    public override void ProcessPackage(World world,GameManager callbacks){if(world==null||world.IsRemote()||protocol!=RebirthStatisticsProtocol.Version||!ValidEntityIdForSender(playerEntityId))return;EntityPlayer p=world.GetEntity(playerEntityId) as EntityPlayer;if(p==null)return;if(history){if(historyPage>=0&&historyDay>=0)RebirthStatisticsService.SelectHordeHistory(p,historyPage,historyDay);}else RebirthStatisticsService.SendSnapshot(p,knownEpoch,knownRevision,force,"client-statistics-request");}
    public int GetLength(){return protocol>=4?37:28;}
}

[Preserve]
public sealed class NetPackageRebirthStatisticsSnapshot : NetPackage
{
    private RebirthStatisticsSnapshot snapshot=new RebirthStatisticsSnapshot();
    public override NetPackageDirection PackageDirection { get { return NetPackageDirection.ToClient; } }
    public NetPackageRebirthStatisticsSnapshot Setup(RebirthStatisticsSnapshot value){snapshot=value!=null?value.Clone():new RebirthStatisticsSnapshot();return this;}
    public override void read(PooledBinaryReader reader){snapshot=Read((BinaryReader)reader);}
    public override void write(PooledBinaryWriter writer){base.write(writer);Write((BinaryWriter)writer,snapshot);}
    public override void ProcessPackage(World world,GameManager callbacks){RebirthStatisticsClientState.Receive(snapshot);}
    public int GetLength()
    {
        RebirthStatisticsSnapshot s=snapshot??new RebirthStatisticsSnapshot();
        int length=512;
        int wc=Math.Min(s.Weapons.Count,RebirthStatisticsProtocol.MaxWeaponEntries);
        for(int i=0;i<wc;i++)length+=8+RebirthSurvivorNetworkCodec.EstimateString(s.Weapons[i].ItemName,128);
        int mc=Math.Min(s.Milestones.Count,RebirthStatisticsProtocol.MaxMilestones);
        for(int i=0;i<mc;i++)length+=4+RebirthSurvivorNetworkCodec.EstimateString(s.Milestones[i].Id,96)+RebirthSurvivorNetworkCodec.EstimateString(s.Milestones[i].Name,160);
        length+=Math.Min(s.Trend.Count,RebirthStatisticsProtocol.MaxTrendDays)*24;
        length+=32;
        foreach(var list in new[]{s.FoodsEaten,s.DrinksConsumed,s.FoodsCooked})
            for(int i=0;i<Math.Min(list.Count,RebirthStatisticsProtocol.MaxSurvivalEntries);i++)length+=8+RebirthSurvivorNetworkCodec.EstimateString(list[i].Id,256);
        if(s.ProtocolVersion>=4)length+=26+RebirthHordeNightStatisticsProjection.Estimate(s.HordeNightTotals)+RebirthHordeNightStatisticsProjection.EstimateHistory(s);
        if(s.ProtocolVersion>=5){length+=10;foreach(var list in new[]{s.HealthEpisodes,s.HealingItems})foreach(var entry in list)length+=8+RebirthSurvivorNetworkCodec.EstimateString(entry.Id,256);}
        return length;
    }

    private static void Write(BinaryWriter b,RebirthStatisticsSnapshot s)
    {
        if(s==null)s=new RebirthStatisticsSnapshot();
        b.Write(s.ProtocolVersion);b.Write(s.Epoch);b.Write(s.Revision);b.Write(s.PlayerLevel);b.Write(s.ActivePlaySeconds);b.Write(s.CurrentLifeSeconds);b.Write(s.LongestLifeSeconds);b.Write(s.DaysSurvived);b.Write(s.DistanceMeters);b.Write(s.OnFootMeters);
        b.Write(s.ZombiesKilled);b.Write(s.PlayerKills);b.Write(s.Deaths);b.Write(s.AnimalsKilled);b.Write(s.HeadshotKills);b.Write(s.MeleeKills);b.Write(s.RangedKills);b.Write(s.KnowledgeDiscovered);b.Write(s.SkillProgressGained);b.Write(s.ItemsCrafted);b.Write(s.ResourcesGathered);b.Write(s.PoisCleared);b.Write(s.LocationsDiscovered);b.Write(s.TradersVisited);b.Write(s.BiomesVisited);b.Write(s.DamageDealt);b.Write(s.DamageTaken);b.Write(s.DamageBlocked);b.Write(s.HasAuthoritativeBlockedDamage);b.Write(s.HighestDamageHit);b.Write(s.HighestFallSurvived);b.Write(s.BestZombiesInDay);b.Write(s.BestItemsCraftedInDay);b.Write(s.BestResourcesInDay);b.Write(s.BestOnFootDistanceInDay);b.Write(s.CombatSeconds);b.Write(s.SurvivalSeconds);b.Write(s.CraftingSeconds);b.Write(s.ExplorationSeconds);b.Write(s.ManagementSeconds);
        int wc=Math.Min(s.Weapons.Count,RebirthStatisticsProtocol.MaxWeaponEntries);b.Write((byte)wc);for(int i=0;i<wc;i++){WriteString(b,s.Weapons[i].ItemName,128);b.Write(s.Weapons[i].Kills);}
        int mc=Math.Min(s.Milestones.Count,RebirthStatisticsProtocol.MaxMilestones);b.Write((byte)mc);for(int i=0;i<mc;i++){WriteString(b,s.Milestones[i].Id,96);WriteString(b,s.Milestones[i].Name,160);b.Write(s.Milestones[i].WorldDay);}
        int tc=Math.Min(s.Trend.Count,RebirthStatisticsProtocol.MaxTrendDays);b.Write((byte)tc);for(int i=0;i<tc;i++){var t=s.Trend[i];b.Write(t.WorldDay);b.Write(t.HealthPercent);b.Write(t.StaminaPercent);b.Write(t.NutritionPercent);b.Write(t.HydrationPercent);b.Write(t.EnergyPercent);}
        if(s.ProtocolVersion>=3)
        {
            b.Write(s.MealsEaten);b.Write(s.DrinkServings);b.Write(s.CookedOutputUnits);b.Write(s.DrinkVolumeMl);
            WriteCounts(b,s.FoodsEaten);WriteCounts(b,s.DrinksConsumed);WriteCounts(b,s.FoodsCooked);
        }
        if(s.ProtocolVersion>=4){RebirthHordeNightStatisticsProjection.Write(b,s.HordeNightTotals);RebirthHordeNightStatisticsProjection.WriteHistory(b,s);b.Write(s.PurgeKillsKnown);b.Write(s.PurgeSuppliesKnown);b.Write(s.PurgeKills);b.Write(s.PurgeDropsEarned);b.Write(s.PurgeDropsLaunched);}
        if(s.ProtocolVersion>=5){WriteCounts(b,s.HealthEpisodes);b.Write(s.HealingUses);WriteCounts(b,s.HealingItems);}
    }
    private static void WriteCounts(BinaryWriter b,System.Collections.Generic.List<RebirthStatisticsCountSnapshot> entries)
    {
        int count=Math.Min(entries.Count,RebirthStatisticsProtocol.MaxSurvivalEntries);b.Write((byte)count);
        for(int i=0;i<count;i++){WriteString(b,entries[i].Id,256);b.Write(entries[i].Count);}
    }
    private static void ReadCounts(BinaryReader b,System.Collections.Generic.List<RebirthStatisticsCountSnapshot> entries)
    {
        int count=b.ReadByte();if(count>RebirthStatisticsProtocol.MaxSurvivalEntries)throw new InvalidDataException("Invalid survival ranking count.");
        for(int i=0;i<count;i++)
        {
            string id=RebirthSurvivorNetworkCodec.ReadBoundedString(b,256);long uses=b.ReadInt64();
            if(string.IsNullOrEmpty(id)||uses<0)throw new InvalidDataException("Invalid survival ranking entry.");
            entries.Add(new RebirthStatisticsCountSnapshot{Id=id,Count=uses});
        }
    }
    private static RebirthStatisticsSnapshot Read(BinaryReader b)
    {
        RebirthStatisticsSnapshot s=new RebirthStatisticsSnapshot();
        s.ProtocolVersion=b.ReadInt32();s.Epoch=b.ReadInt64();s.Revision=b.ReadInt64();s.PlayerLevel=b.ReadInt32();
        s.ActivePlaySeconds=b.ReadDouble();s.CurrentLifeSeconds=b.ReadDouble();s.LongestLifeSeconds=b.ReadDouble();s.DaysSurvived=b.ReadInt32();
        s.DistanceMeters=b.ReadDouble();s.OnFootMeters=b.ReadDouble();s.ZombiesKilled=b.ReadInt64();s.PlayerKills=b.ReadInt64();s.Deaths=b.ReadInt64();
        s.AnimalsKilled=b.ReadInt64();s.HeadshotKills=b.ReadInt64();s.MeleeKills=b.ReadInt64();s.RangedKills=b.ReadInt64();s.KnowledgeDiscovered=b.ReadInt64();
        s.SkillProgressGained=b.ReadDouble();s.ItemsCrafted=b.ReadInt64();s.ResourcesGathered=b.ReadInt64();s.PoisCleared=b.ReadInt64();
        s.LocationsDiscovered=b.ReadInt32();s.TradersVisited=b.ReadInt32();s.BiomesVisited=b.ReadInt32();s.DamageDealt=b.ReadDouble();s.DamageTaken=b.ReadDouble();
        s.DamageBlocked=b.ReadDouble();s.HasAuthoritativeBlockedDamage=b.ReadBoolean();s.HighestDamageHit=b.ReadDouble();s.HighestFallSurvived=b.ReadDouble();
        s.BestZombiesInDay=b.ReadInt64();s.BestItemsCraftedInDay=b.ReadInt64();s.BestResourcesInDay=b.ReadInt64();s.BestOnFootDistanceInDay=b.ReadDouble();
        s.CombatSeconds=b.ReadDouble();s.SurvivalSeconds=b.ReadDouble();s.CraftingSeconds=b.ReadDouble();s.ExplorationSeconds=b.ReadDouble();s.ManagementSeconds=b.ReadDouble();

        int wc=b.ReadByte();
        if(wc<0||wc>RebirthStatisticsProtocol.MaxWeaponEntries)throw new InvalidDataException("Invalid statistics weapon count.");
        for(int i=0;i<wc;i++)s.Weapons.Add(new RebirthStatisticsWeaponSnapshot{ItemName=RebirthSurvivorNetworkCodec.ReadBoundedString(b,128),Kills=b.ReadInt64()});
        int mc=b.ReadByte();
        if(mc<0||mc>RebirthStatisticsProtocol.MaxMilestones)throw new InvalidDataException("Invalid statistics milestone count.");
        for(int i=0;i<mc;i++)s.Milestones.Add(new RebirthStatisticsMilestoneSnapshot{Id=RebirthSurvivorNetworkCodec.ReadBoundedString(b,96),Name=RebirthSurvivorNetworkCodec.ReadBoundedString(b,160),WorldDay=b.ReadInt32()});
        int tc=b.ReadByte();
        if(tc<0||tc>RebirthStatisticsProtocol.MaxTrendDays)throw new InvalidDataException("Invalid statistics trend count.");
        for(int i=0;i<tc;i++)s.Trend.Add(new RebirthStatisticsTrendSnapshot{WorldDay=b.ReadInt32(),HealthPercent=b.ReadSingle(),StaminaPercent=b.ReadSingle(),NutritionPercent=b.ReadSingle(),HydrationPercent=b.ReadSingle(),EnergyPercent=b.ReadSingle()});
        if(s.ProtocolVersion>=3)
        {
            s.MealsEaten=b.ReadInt64();s.DrinkServings=b.ReadInt64();s.CookedOutputUnits=b.ReadInt64();s.DrinkVolumeMl=b.ReadDouble();
            if(s.MealsEaten<0||s.DrinkServings<0||s.CookedOutputUnits<0||s.DrinkVolumeMl<0||double.IsNaN(s.DrinkVolumeMl)||double.IsInfinity(s.DrinkVolumeMl))throw new InvalidDataException("Invalid survival totals.");
            ReadCounts(b,s.FoodsEaten);ReadCounts(b,s.DrinksConsumed);ReadCounts(b,s.FoodsCooked);
        }
        if(s.ProtocolVersion>=4)
        {
            s.HordeNightTotals=RebirthHordeNightStatisticsProjection.Read(b);RebirthHordeNightStatisticsProjection.ReadHistory(b,s);
            s.PurgeKillsKnown=b.ReadBoolean();s.PurgeSuppliesKnown=b.ReadBoolean();s.PurgeKills=b.ReadInt64();s.PurgeDropsEarned=b.ReadInt64();s.PurgeDropsLaunched=b.ReadInt64();
            if(s.PurgeKills<0||s.PurgeDropsEarned<0||s.PurgeDropsLaunched<0||s.PurgeDropsLaunched>s.PurgeDropsEarned||!s.PurgeKillsKnown&&s.PurgeKills!=0||!s.PurgeSuppliesKnown&&(s.PurgeDropsEarned!=0||s.PurgeDropsLaunched!=0))throw new InvalidDataException("Invalid personal Purge totals.");
        }
        if(s.ProtocolVersion>=5)
        {
            ReadCounts(b,s.HealthEpisodes);
            var seen=new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
            foreach(var entry in s.HealthEpisodes)if(!RebirthSurvivalHealthEpisodes.IsEpisode(entry.Id)||!seen.Add(entry.Id))throw new InvalidDataException("Invalid health episode projection.");
            s.HealingUses=b.ReadInt64();if(s.HealingUses<0)throw new InvalidDataException("Invalid healing total.");
            ReadCounts(b,s.HealingItems);
            seen.Clear();foreach(var entry in s.HealingItems)if(!seen.Add(entry.Id))throw new InvalidDataException("Duplicate healing item.");
        }
        return s;
    }
    private static void WriteString(BinaryWriter b,string value,int max){RebirthSurvivorNetworkCodec.WriteString(b,value,max);}
}

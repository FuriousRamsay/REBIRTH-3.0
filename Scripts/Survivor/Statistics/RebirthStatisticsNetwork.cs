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
    public override NetPackageDirection PackageDirection { get { return NetPackageDirection.ToServer; } }
    public NetPackageRebirthStatisticsRequest Setup(int entityId,long epoch,long known,bool forceRefresh){protocol=RebirthStatisticsProtocol.Version;playerEntityId=entityId;knownEpoch=Math.Max(0L,epoch);knownRevision=Math.Max(0L,known);force=forceRefresh;return this;}
    public override void read(PooledBinaryReader reader){BinaryReader b=(BinaryReader)reader;protocol=b.ReadInt32();playerEntityId=b.ReadInt32();knownEpoch=b.ReadInt64();knownRevision=b.ReadInt64();force=b.ReadBoolean();}
    public override void write(PooledBinaryWriter writer){base.write(writer);BinaryWriter b=(BinaryWriter)writer;b.Write(protocol);b.Write(playerEntityId);b.Write(knownEpoch);b.Write(knownRevision);b.Write(force);}
    public override void ProcessPackage(World world,GameManager callbacks){if(world==null||world.IsRemote()||protocol!=RebirthStatisticsProtocol.Version||!ValidEntityIdForSender(playerEntityId))return;EntityPlayer p=world.GetEntity(playerEntityId) as EntityPlayer;if(p!=null)RebirthStatisticsService.SendSnapshot(p,knownEpoch,knownRevision,force,"client-statistics-request");}
    public int GetLength(){return 28;}
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
        return s;
    }
    private static void WriteString(BinaryWriter b,string value,int max){RebirthSurvivorNetworkCodec.WriteString(b,value,max);}
}

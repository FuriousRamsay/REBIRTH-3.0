using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine.Scripting;

#nullable disable

public sealed class RebirthNpcProgressionProjectionEntry
{
    public string Id; public byte Kind; public long Xp; public int Level; public int MaximumLevel;
    public long XpToNext; public long EntryRevision;
}
public sealed class RebirthNpcProgressionProjection
{
    public RebirthNpcStableId NpcId; public long Revision; public bool IsBaseline;
    public RebirthNpcProgressionProjectionEntry[] Entries=new RebirthNpcProgressionProjectionEntry[0];
    public string[] Certifications=new string[0]; public string[] Specializations=new string[0];
    public string[] Mentorships=new string[0];
}


public static class RebirthNpcProgressionProjectionRevisionService
{
    private static readonly object Sync=new object();
    private static readonly Dictionary<RebirthNpcStableId,long> Revisions=new Dictionary<RebirthNpcStableId,long>();
    public static long Bump(RebirthNpcStableId id){if(id.IsEmpty)return 0;lock(Sync){long r;Revisions.TryGetValue(id,out r);r=r==long.MaxValue?1:r+1;Revisions[id]=r;return r;}}
    public static long Current(RebirthNpcStableId id){lock(Sync){long r;return Revisions.TryGetValue(id,out r)?r:0;}}
    public static void Reset(){lock(Sync)Revisions.Clear();}
}

public static class RebirthNpcProgressionProjectionService
{
    public static RebirthNpcProgressionProjection Capture(RebirthNpcStableId id,bool baseline)
    {
        RebirthNpcProgressionRecord record;RebirthNpcProgressionService.TryExportRecord(id,out record);
        var result=new RebirthNpcProgressionProjection{NpcId=id,IsBaseline=baseline,Revision=RebirthNpcProgressionProjectionRevisionService.Current(id)};
        if(record!=null){var list=new List<RebirthNpcProgressionProjectionEntry>();foreach(var pair in record.Entries.OrderBy(x=>x.Key,StringComparer.OrdinalIgnoreCase)){RebirthNpcProgressionView v;if(!RebirthNpcProgressionService.TryGetView(id,pair.Key,out v))continue;list.Add(new RebirthNpcProgressionProjectionEntry{Id=v.ProgressionId,Kind=(byte)ResolveKind(v.ProgressionId),Xp=v.CumulativeXp,Level=v.Level,MaximumLevel=v.MaximumLevel,XpToNext=v.XpToNext,EntryRevision=v.EntryRevision});}result.Entries=list.ToArray();}
        RebirthNpcAdvancedProgressionRecord advanced;RebirthNpcAdvancedProgressionService.TryExportRecord(id,out advanced);
        if(advanced!=null){result.Certifications=advanced.Certifications.OrderBy(x=>x,StringComparer.OrdinalIgnoreCase).ToArray();result.Specializations=advanced.Specializations.OrderBy(x=>x.Key).Select(x=>x.Key+":"+x.Value).ToArray();}
        result.Mentorships=RebirthNpcAdvancedProgressionService.ExportMentorshipsFor(id).Where(x=>x.Active).OrderBy(x=>x.SessionId).Select(x=>x.Profession+":"+(x.MentorId.Equals(id)?"mentor":"trainee")+":"+x.SessionId.ToString("N")).ToArray();
        return result;
    }
    private static RebirthNpcProgressionTrackKind ResolveKind(string id){if(id.StartsWith("attribute.",StringComparison.OrdinalIgnoreCase))return RebirthNpcProgressionTrackKind.Attribute;if(id.StartsWith("weapon.",StringComparison.OrdinalIgnoreCase))return RebirthNpcProgressionTrackKind.WeaponSpecialty;if(id.StartsWith("profession.",StringComparison.OrdinalIgnoreCase))return RebirthNpcProgressionTrackKind.Profession;return RebirthNpcProgressionTrackKind.Generic;}
}

public static class RebirthNpcProgressionClientCache
{
    private static readonly object Sync=new object(); private static readonly Dictionary<RebirthNpcStableId,RebirthNpcProgressionProjection> Values=new Dictionary<RebirthNpcStableId,RebirthNpcProgressionProjection>();
    private static long applied,staleRejected,baselines;
    public static bool Apply(RebirthNpcProgressionProjection value){if(value==null||value.NpcId.IsEmpty)return false;lock(Sync){RebirthNpcProgressionProjection old;if(Values.TryGetValue(value.NpcId,out old)&&(value.Revision<old.Revision||(!value.IsBaseline&&value.Revision==old.Revision))){staleRejected++;return false;}Values[value.NpcId]=value;if(value.IsBaseline)baselines++;applied++;return true;}}
    public static bool TryGet(RebirthNpcStableId id,out RebirthNpcProgressionProjection value){lock(Sync)return Values.TryGetValue(id,out value);}
    public static string Report(){lock(Sync)return "[REBIRTH NPC Progression Client Cache] entries="+Values.Count+" applied="+applied+" baselines="+baselines+" staleRejected="+staleRejected;}
    public static void Reset(){lock(Sync)Values.Clear();}
}

public static class RebirthNpcProgressionReplicationService
{
    private static long baselinesQueued,baselinesSent,deltasQueued,deltasSent,entriesSent,staleRejected,clientApplied;
    public static void SendBaseline(ClientInfo client)
    {
        ConnectionManager c=SingletonMonoBehaviour<ConnectionManager>.Instance;if(c==null||!c.IsServer||client==null)return;
        RebirthNpcProgressionRecord[] records=RebirthNpcProgressionService.ExportRecords();
        for(int i=0;i<records.Length;i++)
        {
            RebirthNpcProgressionProjection v=RebirthNpcProgressionProjectionService.Capture(records[i].NpcId,true);
            baselinesQueued++; c.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthNpcProgression>().Setup(v),_attachedToEntityId:client.entityId);
            baselinesSent++; entriesSent+=v.Entries.Length;
        }
    }
    public static void PublishDelta(RebirthNpcStableId id)
    {
        ConnectionManager c=SingletonMonoBehaviour<ConnectionManager>.Instance;if(c==null||!c.IsServer)return;
        RebirthNpcProgressionProjection v=RebirthNpcProgressionProjectionService.Capture(id,false);deltasQueued++;
        // NetPackage is the production delivery adapter for this projection. Generic batcher
        // counters remain separate; do not count a summary byte blob as a delivered projection.
        if(!GameManager.IsDedicatedServer){if(RebirthNpcProgressionClientCache.Apply(v))clientApplied++;}
        c.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthNpcProgression>().Setup(v));
        deltasSent++;entriesSent+=v.Entries.Length;
    }
    public static void RecordStale(){staleRejected++;}
    public static void RecordClientApplied(){clientApplied++;}
    public static string GetReport(){return "[REBIRTH NPC Progression Replication] baselinesQueued="+baselinesQueued+" baselinesSent="+baselinesSent+" deltasQueued="+deltasQueued+" deltasSent="+deltasSent+" clientApplied="+clientApplied+" entries="+entriesSent+" staleRejected="+staleRejected;}
    public static void Reset(){RebirthNpcProgressionClientCache.Reset();}
}

[Preserve]
public sealed class NetPackageRebirthNpcProgression : NetPackage
{
    private const ushort SchemaVersion=2;
    private uint worldEpoch;
    private RebirthNpcProgressionProjection value;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToClient;
    public NetPackageRebirthNpcProgression Setup(RebirthNpcProgressionProjection v){worldEpoch=RebirthNpcNetworkEpoch.GetServerEpoch();value=v;return this;}
    public override void write(PooledBinaryWriter w)
    {
        base.write(w);BinaryWriter binary=(BinaryWriter)w;binary.Write(SchemaVersion);binary.Write(worldEpoch);
        RebirthNpcStableId id=value!=null?value.NpcId:default(RebirthNpcStableId);binary.Write(id.High);binary.Write(id.Low);
        binary.Write(value!=null?value.Revision:0L);binary.Write(value!=null&&value.IsBaseline);
        WriteEntries(binary,value!=null?value.Entries:null);WriteStrings(binary,value!=null?value.Certifications:null);
        WriteStrings(binary,value!=null?value.Specializations:null);WriteStrings(binary,value!=null?value.Mentorships:null);
    }
    public override void read(PooledBinaryReader r)
    {
        ushort version=r.ReadUInt16();worldEpoch=r.ReadUInt32();if(version!=SchemaVersion){value=null;return;}
        RebirthNpcStableId id=new RebirthNpcStableId(r.ReadUInt64(),r.ReadUInt64());
        value=new RebirthNpcProgressionProjection{NpcId=id,Revision=r.ReadInt64(),IsBaseline=r.ReadBoolean(),Entries=ReadEntries(r),Certifications=ReadStrings(r),Specializations=ReadStrings(r),Mentorships=ReadStrings(r)};
    }
    public override void ProcessPackage(World world,GameManager callbacks)
    {
        ConnectionManager c=SingletonMonoBehaviour<ConnectionManager>.Instance;if(c!=null&&c.IsServer)return;
        if(!RebirthNpcNetworkEpoch.AcceptClientEpoch(worldEpoch))return;
        if(RebirthNpcProgressionClientCache.Apply(value))RebirthNpcProgressionReplicationService.RecordClientApplied();else RebirthNpcProgressionReplicationService.RecordStale();
    }
    public int GetLength()=>0;
    private static void WriteEntries(BinaryWriter w,RebirthNpcProgressionProjectionEntry[] a)
    {
        a=a??new RebirthNpcProgressionProjectionEntry[0];int n=Math.Min(a.Length,RebirthNpcNetworkFraming.MaxProgressionEntries);w.Write((ushort)n);
        for(int i=0;i<n;i++){RebirthNpcProgressionProjectionEntry e=a[i]??new RebirthNpcProgressionProjectionEntry();RebirthNpcNetworkFraming.WriteString(w,e.Id,RebirthNpcNetworkFraming.MaxId);w.Write(e.Kind);w.Write(e.Xp);w.Write(e.Level);w.Write(e.MaximumLevel);w.Write(e.XpToNext);w.Write(e.EntryRevision);}
    }
    private static RebirthNpcProgressionProjectionEntry[] ReadEntries(PooledBinaryReader r)
    {
        int n=RebirthNpcNetworkFraming.ReadUShortCount(r,RebirthNpcNetworkFraming.MaxProgressionEntries,"progression entries");var a=new RebirthNpcProgressionProjectionEntry[n];
        for(int i=0;i<n;i++)a[i]=new RebirthNpcProgressionProjectionEntry{Id=RebirthNpcNetworkFraming.ReadString(r,RebirthNpcNetworkFraming.MaxId),Kind=r.ReadByte(),Xp=r.ReadInt64(),Level=r.ReadInt32(),MaximumLevel=r.ReadInt32(),XpToNext=r.ReadInt64(),EntryRevision=r.ReadInt64()};return a;
    }
    private static void WriteStrings(BinaryWriter w,string[] a)
    {
        a=a??new string[0];int n=Math.Min(a.Length,RebirthNpcNetworkFraming.MaxProgressionStrings);w.Write((ushort)n);for(int i=0;i<n;i++)RebirthNpcNetworkFraming.WriteString(w,a[i],RebirthNpcNetworkFraming.MaxLabel);
    }
    private static string[] ReadStrings(PooledBinaryReader r)
    {
        int n=RebirthNpcNetworkFraming.ReadUShortCount(r,RebirthNpcNetworkFraming.MaxProgressionStrings,"progression strings");var a=new string[n];for(int i=0;i<n;i++)a[i]=RebirthNpcNetworkFraming.ReadString(r,RebirthNpcNetworkFraming.MaxLabel);return a;
    }
}

public static class RebirthNpcProgressionTelemetry
{
    private const int Capacity=256;private static readonly object Sync=new object();private static readonly Queue<string> Recent=new Queue<string>();private static long accepted,rejected,duplicates,levelChanges,cacheQueries,cacheMisses;
    public static void Award(RebirthNpcProgressionAwardResult r,string track){lock(Sync){if(r==null||!r.Accepted){rejected++;if(r!=null&&r.Status==RebirthNpcProgressionAwardStatus.Duplicate)duplicates++;}else{accepted++;if(r.NewLevel!=r.OldLevel)levelChanges++;}Append((r==null?"null":r.Status.ToString())+":"+track+":"+(r==null?"":r.Code));}}
    public static void ModifierQuery(bool hit){lock(Sync){cacheQueries++;if(!hit)cacheMisses++;}}
    private static void Append(string s){while(Recent.Count>=Capacity)Recent.Dequeue();Recent.Enqueue(DateTime.UtcNow.ToString("o",CultureInfo.InvariantCulture)+" "+s);}
    public static string Report(){lock(Sync)return "[REBIRTH NPC Progression Telemetry] accepted="+accepted+" rejected="+rejected+" duplicates="+duplicates+" levelChanges="+levelChanges+" modifierQueries="+cacheQueries+" modifierMisses="+cacheMisses+" retained="+Recent.Count;}
    public static string[] Export(){lock(Sync)return Recent.ToArray();}
}

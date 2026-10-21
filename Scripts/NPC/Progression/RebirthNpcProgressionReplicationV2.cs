using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Xml;
using UnityEngine.Scripting;

// Additive framing. The existing progression packet remains byte-for-byte V1.
[Preserve]
public sealed class NetPackageRebirthNpcProgressionV2:NetPackage
{
    private uint epoch,connection;private ulong generation;
    private RebirthNpcProgressionProjection value;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToClient;
    public NetPackageRebirthNpcProgressionV2 Setup(RebirthNpcProgressionProjection projection,uint recipientConnection)
    {epoch=RebirthNpcNetworkEpoch.GetServerEpoch();connection=recipientConnection;generation=RebirthNpcRecoveryGeneration.RequireCurrent();value=projection;return this;}
    public override void write(PooledBinaryWriter writer)
    {
        base.write(writer);BinaryWriter w=(BinaryWriter)writer;w.Write((ushort)1);w.Write(epoch);w.Write(connection);w.Write(generation);
        if(value==null||value.NpcId.IsEmpty)throw new InvalidDataException("Progression original projection missing");
        if((value.Entries?.Length??0)>RebirthNpcNetworkFraming.MaxProgressionEntries||(value.Certifications?.Length??0)>RebirthNpcNetworkFraming.MaxProgressionStrings||(value.Specializations?.Length??0)>RebirthNpcNetworkFraming.MaxProgressionStrings||(value.Mentorships?.Length??0)>RebirthNpcNetworkFraming.MaxProgressionStrings)
            throw new InvalidDataException("Progression original projection exceeds lossless framing");
        ValidateStrings(value);
        w.Write(value.NpcId.High);w.Write(value.NpcId.Low);w.Write(value.Revision);w.Write(value.IsBaseline);
        NetPackageRebirthNpcProgression.WriteEntries(w,value.Entries);NetPackageRebirthNpcProgression.WriteStrings(w,value.Certifications);NetPackageRebirthNpcProgression.WriteStrings(w,value.Specializations);NetPackageRebirthNpcProgression.WriteStrings(w,value.Mentorships);
    }
    public override void read(PooledBinaryReader r)
    {
        if(r.ReadUInt16()!=1)throw new InvalidDataException("Unsupported NPC progression recovery projection");
        epoch=r.ReadUInt32();connection=r.ReadUInt32();generation=r.ReadUInt64();
        value=new RebirthNpcProgressionProjection{NpcId=new RebirthNpcStableId(r.ReadUInt64(),r.ReadUInt64()),Revision=r.ReadInt64(),IsBaseline=r.ReadBoolean(),Entries=NetPackageRebirthNpcProgression.ReadEntries(r),Certifications=NetPackageRebirthNpcProgression.ReadStrings(r),Specializations=NetPackageRebirthNpcProgression.ReadStrings(r),Mentorships=NetPackageRebirthNpcProgression.ReadStrings(r)};
    }
    public override void ProcessPackage(World world,GameManager callbacks)
    {
        if(!ThreadManager.IsMainThread()||world?.IsRemote()!=true||!ReferenceEquals(world,GameManager.Instance?.World)||
            connection==0||connection!=RebirthNpcNetworkEpoch.ClientConnectionEpoch||!RebirthNpcNetworkEpoch.AcceptClientEpoch(epoch)||!RebirthNpcRecoveryProjectionScope.Observe(generation))return;
        if(RebirthNpcProgressionClientCache.ApplyGeneration(value,generation))RebirthNpcProgressionReplicationService.RecordClientApplied();else RebirthNpcProgressionReplicationService.RecordStale();
    }
    public int GetLength()=>0;
    internal static void ValidateStrings(RebirthNpcProgressionProjection projection)
    {
        foreach(var entry in projection.Entries??Array.Empty<RebirthNpcProgressionProjectionEntry>())if(entry==null||(entry.Id?.Length??0)>RebirthNpcNetworkFraming.MaxId)throw new InvalidDataException("Progression entry exceeds lossless framing");
        foreach(var values in new[]{projection.Certifications,projection.Specializations,projection.Mentorships})foreach(string value in values??Array.Empty<string>())if((value?.Length??0)>RebirthNpcNetworkFraming.MaxLabel)throw new InvalidDataException("Progression projection string exceeds lossless framing");
    }
}

public static partial class RebirthNpcProgressionClientCache
{
    private static Dictionary<RebirthNpcStableId,ulong> Generations=new Dictionary<RebirthNpcStableId,ulong>();
    private static World projectionWorld;private static uint projectionEpoch,projectionConnection;
    private static bool TryGetCurrent(RebirthNpcStableId id,out RebirthNpcProgressionProjection value)
    {
        value=null;if(!ThreadManager.IsMainThread())return false;
        lock(Sync)try
        {
            var world=GameManager.Instance?.World;if(world==null||!ReferenceEquals(world,projectionWorld))return false;
            uint epoch=world.IsRemote()?RebirthNpcNetworkEpoch.ClientEpoch:RebirthNpcNetworkEpoch.GetServerEpoch();
            uint connection=world.IsRemote()?RebirthNpcNetworkEpoch.ClientConnectionEpoch:0;
            if(epoch!=projectionEpoch||connection!=projectionConnection||!Generations.TryGetValue(id,out ulong generation)||generation!=RebirthNpcRecoveryProjectionScope.Current||!Values.TryGetValue(id,out var current))return false;
            value=Copy(current);return true;
        }catch{return false;}
    }
    internal static bool ApplyGeneration(RebirthNpcProgressionProjection value,ulong generation)
    {
        if(!ThreadManager.IsMainThread()||value==null||value.NpcId.IsEmpty)return false;
        var world=GameManager.Instance?.World;if(world==null)return false;
        uint epoch=world.IsRemote()?RebirthNpcNetworkEpoch.ClientEpoch:RebirthNpcNetworkEpoch.GetServerEpoch();
        uint connection=world.IsRemote()?RebirthNpcNetworkEpoch.ClientConnectionEpoch:0;
        lock(Sync)
        {
            if(projectionWorld!=null&&(!ReferenceEquals(world,projectionWorld)||epoch!=projectionEpoch||connection!=projectionConnection))return false;
            if(world.IsRemote()&&!RebirthNpcRecoveryProjectionScope.Observe(generation))return false;
            Generations.TryGetValue(value.NpcId,out ulong prior);
            bool exists=Values.TryGetValue(value.NpcId,out var old);
            if(!CanReplace(exists,prior,old?.Revision??0,generation,value.Revision,value.IsBaseline))
            {staleRejected++;return false;}
            projectionWorld=world;projectionEpoch=epoch;projectionConnection=connection;
            Values[value.NpcId]=Copy(value);Generations[value.NpcId]=generation;if(value.IsBaseline)baselines++;applied++;return true;
        }
    }
    private static RebirthNpcProgressionProjection Copy(RebirthNpcProgressionProjection value)
    {
        var entries=value.Entries??Array.Empty<RebirthNpcProgressionProjectionEntry>();var copied=new RebirthNpcProgressionProjectionEntry[entries.Length];
        for(int i=0;i<entries.Length;i++){var e=entries[i];if(e==null)throw new InvalidDataException("Missing progression projection entry");copied[i]=new RebirthNpcProgressionProjectionEntry{Id=e.Id,Kind=e.Kind,Xp=e.Xp,Level=e.Level,MaximumLevel=e.MaximumLevel,XpToNext=e.XpToNext,EntryRevision=e.EntryRevision};}
        return new RebirthNpcProgressionProjection{NpcId=value.NpcId,Revision=value.Revision,IsBaseline=value.IsBaseline,Entries=copied,Certifications=(string[])(value.Certifications??Array.Empty<string>()).Clone(),Specializations=(string[])(value.Specializations??Array.Empty<string>()).Clone(),Mentorships=(string[])(value.Mentorships??Array.Empty<string>()).Clone()};
    }

    internal static bool CanRetireEmptyProgression
    {get{lock(Sync)return ThreadManager.IsMainThread()&&Values.Count==0&&Generations.Count==0;}}
    internal static bool CanReplace(bool exists,ulong priorGeneration,long priorRevision,ulong generation,long revision,bool baseline)
    {return generation>=priorGeneration&&(!exists||generation>priorGeneration||revision>priorRevision||baseline&&revision==priorRevision);}

    internal static bool TryWriteRecoveryReadback(BinaryWriter writer,RebirthNpcStableId id,ulong generation,bool server)
    {
        RebirthNpcProgressionProjection value;
        if(server)value=RebirthNpcProgressionProjectionService.Capture(id,true);
        else lock(Sync)
        {
            if(!ReferenceEquals(projectionWorld,GameManager.Instance?.World)||projectionEpoch!=RebirthNpcNetworkEpoch.ClientEpoch||projectionConnection!=RebirthNpcNetworkEpoch.ClientConnectionEpoch||!Generations.TryGetValue(id,out ulong current)||current!=generation||!Values.TryGetValue(id,out value))return false;
            value=Copy(value);
        }
        if((value.Entries?.Length??0)>RebirthNpcNetworkFraming.MaxProgressionEntries||(value.Certifications?.Length??0)>RebirthNpcNetworkFraming.MaxProgressionStrings||(value.Specializations?.Length??0)>RebirthNpcNetworkFraming.MaxProgressionStrings||(value.Mentorships?.Length??0)>RebirthNpcNetworkFraming.MaxProgressionStrings)return false;
        NetPackageRebirthNpcProgressionV2.ValidateStrings(value);
        writer.Write(value.NpcId.High);writer.Write(value.NpcId.Low);writer.Write(value.Revision);
        NetPackageRebirthNpcProgression.WriteEntries(writer,value.Entries);NetPackageRebirthNpcProgression.WriteStrings(writer,value.Certifications);NetPackageRebirthNpcProgression.WriteStrings(writer,value.Specializations);NetPackageRebirthNpcProgression.WriteStrings(writer,value.Mentorships);return true;
    }

    // This memory-only adapter is valid for both a server-local cache and a
    // remote projection lane. It never reads a host backing path or sends ACK.
    internal static RebirthNpcSaveQuarantineDomain PrepareProgressionCacheQuarantine(World originalWorld,uint originalEpoch,uint originalConnection)
    {
        lock(Sync)
        {
            if(!ThreadManager.IsMainThread()||originalWorld==null||
                (projectionWorld!=null&&(!ReferenceEquals(projectionWorld,originalWorld)||projectionEpoch!=originalEpoch||projectionConnection!=originalConnection))||
                (projectionWorld==null&&(Values.Count!=0||Generations.Count!=0)))throw new InvalidOperationException("Original progression projection scope unavailable");
            var values=Values;var generations=Generations;var world=projectionWorld;uint epoch=projectionEpoch,connection=projectionConnection;
            var nextValues=new Dictionary<RebirthNpcStableId,RebirthNpcProgressionProjection>();var nextGenerations=new Dictionary<RebirthNpcStableId,ulong>();var doc=ProgressionCacheImage();
            return new RebirthNpcSaveQuarantineDomain("progression-client-cache",doc,
                ()=>{lock(Sync)return ReferenceEquals(Values,values)&&ReferenceEquals(Generations,generations)&&ReferenceEquals(projectionWorld,world)&&projectionEpoch==epoch&&projectionConnection==connection&&ProgressionCacheImage().OuterXml==doc.OuterXml;},
                ()=>{lock(Sync){Values=nextValues;Generations=nextGenerations;projectionWorld=null;projectionEpoch=projectionConnection=0;}},
                ()=>{lock(Sync){Values=values;Generations=generations;projectionWorld=world;projectionEpoch=epoch;projectionConnection=connection;}});
        }
    }
    private static XmlDocument ProgressionCacheImage()
    {
        if(Values.Count!=Generations.Count||Values.Count>8192)throw new InvalidDataException("Progression projection membership unavailable");
        var doc=new XmlDocument{XmlResolver=null};var root=doc.CreateElement("progressionCache");doc.AppendChild(root);
        var ids=new List<RebirthNpcStableId>(Values.Keys);ids.Sort((a,b)=>string.CompareOrdinal(a.ToString(),b.ToString()));
        foreach(var id in ids)
        {
            var value=Values[id];if(value==null||value.NpcId!=id||!Generations.TryGetValue(id,out ulong generation))throw new InvalidDataException("Progression projection binding unavailable");
            var row=doc.CreateElement("npc");root.AppendChild(row);row.SetAttribute("id",id.ToString());row.SetAttribute("generation",generation.ToString(CultureInfo.InvariantCulture));row.SetAttribute("revision",value.Revision.ToString(CultureInfo.InvariantCulture));row.SetAttribute("baseline",value.IsBaseline?"1":"0");
            foreach(var entry in value.Entries??Array.Empty<RebirthNpcProgressionProjectionEntry>())
            {if(entry==null)throw new InvalidDataException("Progression projection entry missing");var e=doc.CreateElement("entry");row.AppendChild(e);e.SetAttribute("id",entry.Id??string.Empty);e.SetAttribute("kind",entry.Kind.ToString(CultureInfo.InvariantCulture));e.SetAttribute("xp",entry.Xp.ToString(CultureInfo.InvariantCulture));e.SetAttribute("level",entry.Level.ToString(CultureInfo.InvariantCulture));e.SetAttribute("maximum",entry.MaximumLevel.ToString(CultureInfo.InvariantCulture));e.SetAttribute("next",entry.XpToNext.ToString(CultureInfo.InvariantCulture));e.SetAttribute("revision",entry.EntryRevision.ToString(CultureInfo.InvariantCulture));}
            AppendStrings(doc,row,"certification",value.Certifications);AppendStrings(doc,row,"specialization",value.Specializations);AppendStrings(doc,row,"mentorship",value.Mentorships);
        }
        return doc;
    }
    private static void AppendStrings(XmlDocument doc,XmlElement row,string name,string[] values)
    {foreach(string value in values??Array.Empty<string>()){var e=doc.CreateElement(name);e.SetAttribute("null",value==null?"1":"0");e.InnerText=value??string.Empty;row.AppendChild(e);}}
}

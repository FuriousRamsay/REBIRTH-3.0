using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Scripting;

internal sealed class RebirthPurgeSupplyTarget
{
    internal readonly int Id;
    internal readonly Vector3 Position;
    internal RebirthPurgeSupplyTarget(int id,Vector3 position){Id=id;Position=position;}
    internal static int Next(IList<RebirthPurgeSupplyTarget> targets,int previous)
    {for(int i=0;i<targets.Count;i++)if(targets[i].Id>previous)return i;return targets.Count==0?-1:0;}
}
internal static class RebirthPurgeSupplyTargets
{
    internal const int Limit=256;
    internal static bool ValidPosition(Vector3 p)=>Valid(p.x)&&Valid(p.y)&&Valid(p.z);
    private static bool Valid(float n)=>!float.IsNaN(n)&&!float.IsInfinity(n)&&Math.Abs(n)<=10000000;
    internal static bool Collect(World world,string player,out List<RebirthPurgeSupplyTarget> targets)
    {
        targets=new List<RebirthPurgeSupplyTarget>();
        if(!ThreadManager.IsMainThread()||!RebirthPurgeReleasePolicy.Enabled||!RebirthSandboxOptionManager.Current.IsPurge||world==null||world.IsRemote()||!ReferenceEquals(GameManager.Instance?.World,world)||!RebirthPoiSupplyCredits.ValidKey(player))return false;
        var director=world.aiDirector?.GetComponent<AIDirectorAirDropComponent>();
        if(director==null||director.supplyCrates.Count>4096)return false;
        RebirthPurgeSupplyMarkerLabels.Bind(director);
        Guid id;if(!Guid.TryParseExact(world.worldState?.Guid,"N",out id))return false;
        var seen=new HashSet<int>();
        foreach(var cache in director.supplyCrates)
        {
            if(cache.entityId<=0||!seen.Add(cache.entityId))continue;
            var loaded=world.GetEntity(cache.entityId);var crate=loaded as EntitySupplyCrate;
            Vector3 position;
            if(loaded!=null)
            {
                RebirthPurgeSupplyCrateStamp stamp;
                if(crate==null||!RebirthPurgeSupplyCrateSerialization.TryReadAuthoritative(crate,id,out stamp)||stamp.Player!=player)continue;
                position=crate.position;
                string name=RebirthPurgeDiscoveryNotification.CleanDisplayName(crate.spawnByName);
                RebirthPurgeSupplyMarkerLabels.Capture(crate,string.IsNullOrEmpty(name)?Localization.Get("xuiRebirthPurgeSupplyOwner"):name);
            }
            else
            {
                if(!RebirthPurgeSupplyMarkerLabels.Owned(cache.entityId,cache.blockPos,player))continue;
                position=(Vector3)cache.blockPos;
            }
            if(!ValidPosition(position))continue;
            if(targets.Count==Limit){targets.Clear();return false;}
            targets.Add(new RebirthPurgeSupplyTarget(cache.entityId,position));
        }
        targets.Sort((a,b)=>a.Id.CompareTo(b.Id));return true;
    }
    internal static bool Sender(World world,NetPackage package,out EntityPlayer player)
    {
        player=null;var c=ConnectionManager.Instance;
        if(!ThreadManager.IsMainThread()||world==null||world.IsRemote()||c==null||!c.IsServer||package.Sender==null||!package.Sender.loginDone||!ReferenceEquals(GameManager.Instance?.World,world)||!ReferenceEquals(c.Clients.ForEntityId(package.Sender.entityId),package.Sender))return false;
        player=world.GetEntity(package.Sender.entityId) as EntityPlayer;
        return RebirthPurgeKillContributor.Identify(player,world)!=null;
    }
}
[Preserve]
public sealed class NetPackageRebirthPurgeSupplyTargetsRequest:NetPackage
{
    private Guid world,nonce;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToServer;
    internal NetPackageRebirthPurgeSupplyTargetsRequest Setup(Guid id,Guid request){world=id;nonce=request;return this;}
    public override void read(PooledBinaryReader r){world=new Guid(r.ReadBytes(16));nonce=new Guid(r.ReadBytes(16));}
    public override void write(PooledBinaryWriter writer){base.write(writer);BinaryWriter w=writer;w.Write(world.ToByteArray());w.Write(nonce.ToByteArray());}
    public int GetLength()=>32;
    public override void ProcessPackage(World native,GameManager callbacks)
    {
        EntityPlayer player;Guid id;
        if(nonce==Guid.Empty||!RebirthPurgeSupplyTargets.Sender(native,this,out player)||!Guid.TryParseExact(native.worldState?.Guid,"N",out id)||id!=world)return;
        List<RebirthPurgeSupplyTarget> targets;bool complete=RebirthPurgeSupplyTargets.Collect(native,RebirthPurgeKillContributor.Identify(player,native),out targets);
        ConnectionManager.Instance.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthPurgeSupplyTargetsReply>().Setup(id,nonce,complete,targets),_attachedToEntityId:player.entityId);
    }
}
[Preserve]
public sealed class NetPackageRebirthPurgeSupplyTargetsReply:NetPackage
{
    private Guid world,nonce;
    private bool complete;
    private List<RebirthPurgeSupplyTarget> targets;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToClient;
    internal NetPackageRebirthPurgeSupplyTargetsReply Setup(Guid id,Guid request,bool valid,List<RebirthPurgeSupplyTarget> values)
    {world=id;nonce=request;complete=valid;targets=valid?values:new List<RebirthPurgeSupplyTarget>();return this;}
    public override void read(PooledBinaryReader r)
    {
        world=new Guid(r.ReadBytes(16));nonce=new Guid(r.ReadBytes(16));complete=r.ReadBoolean();int count=r.ReadUInt16();
        if(count>RebirthPurgeSupplyTargets.Limit||!complete&&count!=0)throw new InvalidDataException("Invalid supply target count.");
        targets=new List<RebirthPurgeSupplyTarget>(count);int previous=0;
        for(int i=0;i<count;i++)
        {
            int id=r.ReadInt32();var p=new Vector3(r.ReadSingle(),r.ReadSingle(),r.ReadSingle());
            if(id<=previous||!RebirthPurgeSupplyTargets.ValidPosition(p))throw new InvalidDataException("Invalid supply target.");
            previous=id;targets.Add(new RebirthPurgeSupplyTarget(id,p));
        }
    }
    public override void write(PooledBinaryWriter writer)
    {base.write(writer);BinaryWriter w=writer;w.Write(world.ToByteArray());w.Write(nonce.ToByteArray());w.Write(complete);w.Write((ushort)targets.Count);foreach(var t in targets){w.Write(t.Id);w.Write(t.Position.x);w.Write(t.Position.y);w.Write(t.Position.z);}}
    public int GetLength()=>35+(targets?.Count??0)*16;
    public override void ProcessPackage(World native,GameManager callbacks)
    {if(ThreadManager.IsMainThread()&&native!=null&&native.IsRemote()&&ReferenceEquals(GameManager.Instance?.World,native))XUiC_RebirthPurgeSupplyButton.Receive(native,world,nonce,complete,targets);}
}

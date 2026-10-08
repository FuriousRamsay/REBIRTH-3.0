using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Scripting;

#nullable disable

public sealed class RebirthHunterTrackingRow
{
    public int EntityId;public Vector3 Position;public byte Kind;public float SecondsRemaining;public string Name=string.Empty;
}

[Preserve]
public sealed class NetPackageRebirthCombatMomentumState:NetPackage
{
    private int playerId,stacks;public override NetPackageDirection PackageDirection=>NetPackageDirection.ToClient;
    public NetPackageRebirthCombatMomentumState Setup(int p,int s){playerId=p;stacks=s;return this;}
    public override void read(PooledBinaryReader r){BinaryReader b=(BinaryReader)r;playerId=b.ReadInt32();stacks=b.ReadInt32();}
    public override void write(PooledBinaryWriter w){base.write(w);BinaryWriter b=(BinaryWriter)w;b.Write(playerId);b.Write(stacks);}
    public override void ProcessPackage(World world,GameManager callbacks){RebirthCombatPatrolTrackingSignatureService.ApplyClientCombatState(playerId,stacks);}
    public int GetLength()=>8;
}

[Preserve]
public sealed class NetPackageRebirthHunterObservationRequest:NetPackage
{
    private int playerId,targetId;public override NetPackageDirection PackageDirection=>NetPackageDirection.ToServer;
    public NetPackageRebirthHunterObservationRequest Setup(int p,int t){playerId=p;targetId=t;return this;}
    public override void read(PooledBinaryReader r){BinaryReader b=(BinaryReader)r;playerId=b.ReadInt32();targetId=b.ReadInt32();}
    public override void write(PooledBinaryWriter w){base.write(w);BinaryWriter b=(BinaryWriter)w;b.Write(playerId);b.Write(targetId);}
    public override void ProcessPackage(World world,GameManager callbacks){if(world==null||world.IsRemote()||!ValidEntityIdForSender(playerId))return;EntityPlayer p=world.GetEntity(playerId) as EntityPlayer;if(p!=null)RebirthCombatPatrolTrackingSignatureService.ReceiveObservationRequest(p,targetId);}
    public int GetLength()=>8;
}

[Preserve]
public sealed class NetPackageRebirthHunterObservationState:NetPackage
{
    private int playerId;private bool active;private float seconds;public override NetPackageDirection PackageDirection=>NetPackageDirection.ToClient;
    public NetPackageRebirthHunterObservationState Setup(int p,bool a,float s){playerId=p;active=a;seconds=s;return this;}
    public override void read(PooledBinaryReader r){BinaryReader b=(BinaryReader)r;playerId=b.ReadInt32();active=b.ReadBoolean();seconds=b.ReadSingle();}
    public override void write(PooledBinaryWriter w){base.write(w);BinaryWriter b=(BinaryWriter)w;b.Write(playerId);b.Write(active);b.Write(seconds);}
    public override void ProcessPackage(World world,GameManager callbacks){RebirthCombatPatrolTrackingSignatureService.ApplyClientObservationState(playerId,active,seconds);}
    public int GetLength()=>9;
}

[Preserve]
public sealed class NetPackageRebirthHunterTrackingSnapshot:NetPackage
{
    private const int MaxRows=256;
    private const int MaxNameLength=96;
    private int playerId;private List<RebirthHunterTrackingRow> rows=new List<RebirthHunterTrackingRow>();public override NetPackageDirection PackageDirection=>NetPackageDirection.ToClient;
    public NetPackageRebirthHunterTrackingSnapshot Setup(int p,List<RebirthHunterTrackingRow> r){if(r!=null&&r.Count>MaxRows)throw new InvalidDataException("Hunter tracking snapshot exceeds packet bound.");playerId=p;rows=r!=null?new List<RebirthHunterTrackingRow>(r):new List<RebirthHunterTrackingRow>();return this;}
    public override void read(PooledBinaryReader r){BinaryReader b=(BinaryReader)r;playerId=b.ReadInt32();int n=(int)b.ReadUInt16();if(n>MaxRows)throw new InvalidDataException("Hunter tracking snapshot exceeds packet bound.");List<RebirthHunterTrackingRow> decoded=new List<RebirthHunterTrackingRow>(n);for(int i=0;i<n;i++)decoded.Add(new RebirthHunterTrackingRow{EntityId=b.ReadInt32(),Position=new Vector3(b.ReadSingle(),b.ReadSingle(),b.ReadSingle()),Kind=b.ReadByte(),SecondsRemaining=b.ReadSingle(),Name=RebirthSurvivorNetworkCodec.ReadBoundedString(b,MaxNameLength)});rows=decoded;}
    public override void write(PooledBinaryWriter w){base.write(w);BinaryWriter b=(BinaryWriter)w;b.Write(playerId);int n=rows!=null?rows.Count:0;if(n>MaxRows)throw new InvalidDataException("Hunter tracking snapshot exceeds packet bound.");b.Write((ushort)n);for(int i=0;i<n;i++){RebirthHunterTrackingRow x=rows[i]??new RebirthHunterTrackingRow();b.Write(x.EntityId);b.Write(x.Position.x);b.Write(x.Position.y);b.Write(x.Position.z);b.Write(x.Kind);b.Write(x.SecondsRemaining);RebirthSurvivorNetworkCodec.WriteString(b,x.Name??string.Empty,MaxNameLength);}}
    public override void ProcessPackage(World world,GameManager callbacks){RebirthHunterMarkerService.ApplySnapshot(playerId,rows);}
    public int GetLength(){int n=rows!=null?rows.Count:0;if(n>MaxRows)throw new InvalidDataException("Hunter tracking snapshot exceeds packet bound.");int len=6+n*25;for(int i=0;i<n;i++)len+=RebirthSurvivorNetworkCodec.EstimateString(rows[i]!=null?rows[i].Name:string.Empty,MaxNameLength);return len;}
}

public static class RebirthHunterMarkerService
{
    private const string LivingClass="RebirthHunterMarkedAnimal",CarcassClass="RebirthHunterCarcass";
    private static readonly Dictionary<string,NavObject> Markers=new Dictionary<string,NavObject>(StringComparer.OrdinalIgnoreCase);
    public static void ApplySnapshot(int playerId,List<RebirthHunterTrackingRow> rows)
    {
        World w=GameManager.Instance!=null?GameManager.Instance.World:null;if(w==null)return;EntityPlayerLocal local=w.GetPrimaryPlayer();if(local==null||local.entityId!=playerId)return;HashSet<string> keep=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if(rows!=null)for(int i=0;i<rows.Count;i++){RebirthHunterTrackingRow row=rows[i];if(row==null)continue;string key=row.Kind+":"+row.EntityId;keep.Add(key);Upsert(local,key,row);}
        List<string> remove=new List<string>();foreach(KeyValuePair<string,NavObject> kv in Markers)if(!keep.Contains(kv.Key))remove.Add(kv.Key);for(int i=0;i<remove.Count;i++)Remove(remove[i]);
    }
    private static void Upsert(EntityPlayerLocal local,string key,RebirthHunterTrackingRow row)
    {
        if(!NavObjectManager.HasInstance)return;NavObject marker;Markers.TryGetValue(key,out marker);if(marker==null||!NavObjectManager.Instance.NavObjectList.Contains(marker)){try{marker=NavObjectManager.Instance.RegisterNavObject(row.Kind==0?LivingClass:CarcassClass,row.Position,string.Empty,false,SyntheticId(key),local);if(marker==null)return;Markers[key]=marker;}catch{return;}}
        try{marker.name=string.IsNullOrEmpty(row.Name)?(row.Kind==0?"Marked animal":"Animal carcass"):row.Name;marker.TrackedPosition=row.Position;marker.EntityID=SyntheticId(key);marker.OwnerEntity=local;marker.IsActive=true;marker.ForceDisabled=false;marker.hiddenOnCompass=false;marker.hiddenOnMap=true;}catch{}
    }
    private static int SyntheticId(string key){unchecked{int h=17;for(int i=0;i<key.Length;i++)h=h*31+key[i];if(h>0)h=-h;if(h==0)h=-17001;return h;}}
    private static void Remove(string key){NavObject n;if(!Markers.TryGetValue(key,out n))return;Markers.Remove(key);if(n!=null&&NavObjectManager.HasInstance)try{NavObjectManager.Instance.UnRegisterNavObject(n);}catch{}}
    public static void Reset(){List<string> keys=new List<string>(Markers.Keys);for(int i=0;i<keys.Count;i++)Remove(keys[i]);Markers.Clear();}
}

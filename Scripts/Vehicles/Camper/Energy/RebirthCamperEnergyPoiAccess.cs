using System;
using System.IO;
using System.Text;
using System.Security.Cryptography;
using System.Runtime.CompilerServices;
using UnityEngine.Scripting;

// Radial world action: never opens management, acquires a native lock, or
// requests inventory/source custody. Remote observations use the existing
// world/vehicle-scoped persisted POI projection; mutations stay authoritative.
internal static class RebirthCamperEnergyPoiAccess
{
    private sealed class PeerSequence {internal World World;internal long Sequence;}
    private static readonly ConditionalWeakTable<ClientInfo,PeerSequence> sequences=new ConditionalWeakTable<ClientInfo,PeerSequence>();
    private static long sequence;
    internal static bool Allowed(EntityVehicle vehicle,EntityPlayer player)
    {
        if(!ThreadManager.IsMainThread()||!RebirthCamperEnergyAccess.Registered||vehicle?.world==null||player==null||player.IsDead()||
            !ReferenceEquals(GameManager.Instance?.World,vehicle.world)||!ReferenceEquals(vehicle.world,player.world)||
            !ReferenceEquals(vehicle.world.GetEntity(vehicle.entityId),vehicle)||!ReferenceEquals(vehicle.world.GetEntity(player.entityId),player)||
            !RebirthCamperRadial.IsCamper(vehicle)||vehicle.IsDead()||RebirthCamperRadial.IsDriver(vehicle,player)||
            (vehicle.position-player.position).sqrMagnitude>64f||RebirthCamperModuleInteraction.Current(vehicle,player as EntityPlayerLocal)||
            RebirthCamperRemoteAdmission.NativeInteractionOwner(vehicle)>=0||
            !RebirthCamperEnergyRuntime.TryObserveMode(vehicle,out var mode,out _,out _)||mode!=RebirthCamperEnergyMode.Camp)return false;
        var owner=vehicle.GetOwner();var data=GameManager.Instance.GetPersistentPlayerList()?.GetPlayerDataFromEntityID(player.entityId);
        return owner!=null&&data!=null&&(owner.Equals(data.PrimaryId)||owner.Equals(data.NativeId))&&(!vehicle.world.IsRemote()||PackageAvailable());
    }
    internal static void Request(EntityVehicle vehicle,EntityPlayerLocal player,bool on)
    {
        if(!Allowed(vehicle,player)||!RebirthCamperEnergyDeviceAdapter.TryObservePoiOn(vehicle,out bool expected))
        {Refuse(player);return;}
        if(expected==on)
        {
            GameManager.ShowTooltip(player,Localization.Get(on?
                "xuiRebirthCamperEnergyPoiEnabled":"xuiRebirthCamperEnergyPoiDisabled"));
            return;
        }
        string poi=RebirthCamperEnergyDeviceAdapter.ObservePoiTarget(vehicle);if(string.IsNullOrEmpty(poi)){Refuse(player);return;}
        if(!vehicle.world.IsRemote()){RebirthCamperEnergyAccess.ExecutePoiRadial(vehicle,player,on);return;}
        var item=vehicle.vehicle.GetUpdatedItemValue();
        if(!item.TryGetMetadata("RebirthCamperIdentity",out string text)||!Guid.TryParse(text,out var identity)){Refuse(player);return;}
        string creation=RebirthCamperCharacterAccess.CurrentCreation(player);
        if(creation==null||!Guid.TryParse(GamePrefs.GetString(EnumGamePrefs.GameGuidClient),out var worldId)||!PackageAvailable()){Refuse(player);return;}
        var projection=ProjectionHash(vehicle);if(projection.Length!=32){Refuse(player);return;}
        var connection=SingletonMonoBehaviour<ConnectionManager>.Instance;
        if(connection==null||!connection.IsClient||connection.IsServer){Refuse(player);return;}
        try{connection.SendToServer(NetPackageManager.GetPackage<NetPackageRebirthCamperPoiRadialV1>().Setup(
            player.entityId,vehicle.entityId,identity,worldId,creation,poi,projection,++sequence,expected,on),false);}catch{Refuse(player);}
    }
    private static void Refuse(EntityPlayerLocal player)
    {if(player!=null)GameManager.ShowTooltip(player,Localization.Get("xuiRebirthCamperEnergyOwnerChanged"));}
    private static byte[] ProjectionHash(EntityVehicle vehicle)
    {
        if(!vehicle.vehicle.GetUpdatedItemValue().TryGetMetadata(RebirthCamperEnergyDeviceLeaseStorage.Key,out string value))return new byte[0];
        using(var hash=SHA256.Create())return hash.ComputeHash(Encoding.UTF8.GetBytes(value));
    }
    private static bool Equal(byte[] a,byte[] b)
    {if(a==null||b==null||a.Length!=32||b.Length!=32)return false;for(int i=0;i<32;i++)if(a[i]!=b[i])return false;return true;}
    internal static bool PackageAvailable()
    {try{NetPackageManager.GetPackageId(typeof(NetPackageRebirthCamperPoiRadialV1));return true;}catch{return false;}}
    internal static void Receive(World world,ClientInfo sender,int actor,int entity,Guid identity,Guid worldId,string creation,string poi,byte[] projection,long serial,bool expected,bool on)
    {
        if(!ThreadManager.IsMainThread()||SingletonMonoBehaviour<ConnectionManager>.Instance?.IsServer!=true||world==null||!ReferenceEquals(GameManager.Instance?.World,world)||world.IsRemote()||sender?.entityId!=actor||sender.InternalId==null||identity==Guid.Empty||creation==null||
            !Guid.TryParse(world.worldState?.Guid,out var currentWorld)||currentWorld!=worldId)return;
        var player=world.GetEntity(actor) as EntityPlayer;var vehicle=world.GetEntity(entity) as EntityVehicle;
        if(!Allowed(vehicle,player)||RebirthCamperCharacterAccess.CurrentCreation(player)!=creation||
            !ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance.Clients.ForEntityId(actor),sender)||
            !vehicle.vehicle.GetUpdatedItemValue().TryGetMetadata("RebirthCamperIdentity",out string text)||!Guid.TryParse(text,out var current)||current!=identity)return;
        // Re-evaluate the native selected POI now, not the client's stale projection.
        if(!Equal(ProjectionHash(vehicle),projection)||
            !RebirthCamperEnergyDeviceAdapter.TryObservePoiOn(vehicle,out bool currentOn)||currentOn!=expected||on==expected||
            RebirthCamperEnergyDeviceAdapter.ObservePoiTarget(vehicle)!=poi)return;
        var seen=sequences.GetValue(sender,_=>new PeerSequence());
        if(ReferenceEquals(seen.World,world)&&serial<=seen.Sequence||serial<=0)return;
        seen.World=world;seen.Sequence=serial;
        RebirthCamperEnergyAccess.ExecutePoiRadial(vehicle,player,on);
    }
}

[Preserve]
public sealed class NetPackageRebirthCamperPoiRadialV1:NetPackage
{
    private int actor,entity;private Guid identity,worldId;private string creation,poi;private byte[] projection;private long serial;private bool expected,on;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToServer;
    internal NetPackageRebirthCamperPoiRadialV1 Setup(int a,int e,Guid i,Guid w,string c,string p,byte[] r,long q,bool x,bool o)
    {actor=a;entity=e;identity=i;worldId=w;creation=c;poi=p;projection=r;serial=q;expected=x;on=o;return this;}
    private static string ReadText(PooledBinaryReader r)
    {int count=r.ReadUInt16();if(count>2048)throw new InvalidDataException("POI radial text too large.");var bytes=r.ReadBytes(count);if(bytes.Length!=count)throw new EndOfStreamException();return new UTF8Encoding(false,true).GetString(bytes);}
    private static void WriteText(PooledBinaryWriter w,string text)
    {var bytes=Encoding.UTF8.GetBytes(text??string.Empty);if(bytes.Length>2048)throw new InvalidDataException("POI radial text too large.");w.Write((ushort)bytes.Length);w.Write(bytes);}
    public override void read(PooledBinaryReader r)
    {if(r.ReadByte()!=1)throw new InvalidDataException("POI radial version changed.");actor=r.ReadInt32();entity=r.ReadInt32();identity=new Guid(r.ReadBytes(16));worldId=new Guid(r.ReadBytes(16));creation=ReadText(r);poi=ReadText(r);projection=r.ReadBytes(32);if(projection.Length!=32)throw new EndOfStreamException();serial=r.ReadInt64();expected=r.ReadBoolean();on=r.ReadBoolean();}
    public override void write(PooledBinaryWriter w)
    {base.write(w);w.Write((byte)1);w.Write(actor);w.Write(entity);w.Write(identity.ToByteArray());w.Write(worldId.ToByteArray());WriteText(w,creation);WriteText(w,poi);w.Write(projection);w.Write(serial);w.Write(expected);w.Write(on);}
    public override void ProcessPackage(World world,GameManager callbacks)
    {if(ValidEntityIdForSender(actor))RebirthCamperEnergyPoiAccess.Receive(world,Sender,actor,entity,identity,worldId,creation,poi,projection,serial,expected,on);}
    public int GetLength()=>0;
}

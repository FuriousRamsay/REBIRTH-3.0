using System;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Scripting;

// Server-to-client presentation only. No metadata marker/client write can become
// authority: the server's claim predicates never read this projection cache.
public static class RebirthCamperClaimProjection
{
    private sealed class Projection
    {internal World World;internal string WorldId,Identity;internal Vector3 Position;internal Quaternion Rotation;internal long Sequence,Revision;internal RebirthCamperEnergyMode Mode;internal bool Ready;internal float Received,Range;}
    private sealed class Publication
    {internal bool Ready,Warned;internal Vector3 Position;internal Quaternion Rotation;internal string Identity;internal RebirthCamperEnergyMode Mode;internal float Next,RetryAfter,Range;}
    private static ConditionalWeakTable<EntityVehicle,Projection> projections=new ConditionalWeakTable<EntityVehicle,Projection>();
    private static ConditionalWeakTable<EntityVehicle,Publication> publications=new ConditionalWeakTable<EntityVehicle,Publication>();
    private static long sequence;
    private static bool installed;
    public static void Install()
    {
        if(installed)return;installed=true;
        ModEvents.GameUpdate.RegisterHandler(Tick);
        ModEvents.WorldShuttingDown.RegisterHandler(Shutdown);
        RebirthHarmonyBootstrap.PatchClassOnce(new Harmony("rebirth.camper.energy.claimprojection"),typeof(RebirthCamperClaimProjectionUnload));
    }
    private static string Identity(EntityVehicle vehicle)
    {string value;return vehicle.vehicle.GetUpdatedItemValue().TryGetMetadata("RebirthCamperIdentity",out value)&&Guid.TryParse(value,out var id)&&id!=Guid.Empty?id.ToString("D"):null;}
    public static bool Ready(EntityVehicle vehicle)
    {
        if(!TryReadRadius(vehicle,out _)||!projections.TryGetValue(vehicle,out var observed))return false;
        return RebirthCamperEnergyRuntime.TryObserveMode(vehicle,out var mode,out long revision,out _)&&
            mode==RebirthCamperEnergyMode.Camp&&revision>=observed.Revision;
    }
    // Pure cache read used by Runtime range projection; it must never call
    // TryObserveMode/Observe, which themselves read this radius on clients.
    public static bool TryReadRadius(EntityVehicle vehicle,out float radius)
    {
        radius=0;
        if(!ThreadManager.IsMainThread()||vehicle?.world==null||!vehicle.world.IsRemote()||!ReferenceEquals(GameManager.Instance?.World,vehicle.world)||
            !ReferenceEquals(vehicle.world.GetEntity(vehicle.entityId),vehicle)||!RebirthCamperEnergyRuntime.IsRegistered||
            !projections.TryGetValue(vehicle,out var observed)||!ReferenceEquals(observed.World,vehicle.world)||!observed.Ready||
            observed.WorldId!=vehicle.world.worldState?.Guid||observed.Identity!=Identity(vehicle)||
            Time.realtimeSinceStartup-observed.Received>15f||(vehicle.position-observed.Position).sqrMagnitude>0.01f||Quaternion.Angle(vehicle.qrotation,observed.Rotation)>0.1f)return false;
        if(observed.Mode!=RebirthCamperEnergyMode.Camp)return false;
        radius=observed.Range;return true;
    }
    public static bool TryRange(EntityVehicle vehicle,out float range)
    {range=0;if(!Ready(vehicle)||!projections.TryGetValue(vehicle,out var projected))return false;range=projected.Range;return true;}
    internal static void Receive(World world,int entity,string worldId,string identity,Vector3 position,Quaternion rotation,RebirthCamperEnergyMode mode,long revision,float range,bool ready,long serial)
    {
        // ToClient package direction is the native server channel. A client sent
        // package cannot create a server claim or cause a server rebroadcast.
        if(!ThreadManager.IsMainThread()||world?.IsRemote()!=true||!ReferenceEquals(GameManager.Instance?.World,world)||
            worldId!=world.worldState?.Guid||serial<=0||revision<0||float.IsNaN(range)||float.IsInfinity(range)||range<=0||range>200||!Enum.IsDefined(typeof(RebirthCamperEnergyMode),mode)||ready&&mode!=RebirthCamperEnergyMode.Camp||!Guid.TryParse(identity,out var id)||id==Guid.Empty||
            float.IsNaN(position.x)||float.IsInfinity(position.x)||float.IsNaN(position.y)||float.IsInfinity(position.y)||float.IsNaN(position.z)||float.IsInfinity(position.z))return;
        float rotationNorm=rotation.x*rotation.x+rotation.y*rotation.y+rotation.z*rotation.z+rotation.w*rotation.w;
        if(float.IsNaN(rotationNorm)||float.IsInfinity(rotationNorm)||Mathf.Abs(rotationNorm-1f)>0.01f)return;
        var vehicle=world.GetEntity(entity) as EntityVehicle;
        if(!RebirthCamperRadial.IsCamper(vehicle)||Identity(vehicle)!=id.ToString("D"))return;
        if(projections.TryGetValue(vehicle,out var old)&&ReferenceEquals(old.World,world)&&serial<=old.Sequence)return;
        projections.Remove(vehicle);projections.Add(vehicle,new Projection{World=world,WorldId=worldId,Identity=id.ToString("D"),Position=position,
            Rotation=rotation,Mode=mode,Revision=revision,Range=range,Ready=ready,Sequence=serial,Received=Time.realtimeSinceStartup});
    }
    private static void Tick(ref ModEvents.SGameUpdateData data)
    {
        var world=GameManager.Instance?.World;
        if(!ThreadManager.IsMainThread()||world==null||world.IsRemote()||!RebirthCamperEnergyRuntime.IsRegistered||
            SingletonMonoBehaviour<ConnectionManager>.Instance?.IsServer!=true)return;
        foreach(var entity in world.Entities.list)
        {
            if(!(entity is EntityVehicle vehicle)||!RebirthCamperRadial.IsCamper(vehicle))continue;
            string identity=Identity(vehicle);if(identity==null)continue;
            bool ready=RebirthCamperEnergyImmobility.CampClaimReady(vehicle);
            if(!RebirthCamperEnergyRuntime.TryObserveMode(vehicle,out var mode,out long revision,out float range))continue;
            var published=publications.GetValue(vehicle,_=>new Publication());
            if(Time.realtimeSinceStartup<published.RetryAfter)continue;
            if(published.Identity==identity&&published.Ready==ready&&published.Mode==mode&&published.Range==range&&
                (!ready||published.Position==vehicle.position&&published.Rotation==vehicle.qrotation)&&Time.realtimeSinceStartup<published.Next)continue;
            try{SingletonMonoBehaviour<ConnectionManager>.Instance.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthCamperClaimProjection>()
                .Setup(vehicle.entityId,world.worldState?.Guid,identity,vehicle.position,vehicle.qrotation,mode,revision,range,ready,checked(++sequence)));}
            catch(Exception error){published.RetryAfter=Time.realtimeSinceStartup+5f;if(!published.Warned){published.Warned=true;Log.Warning("[REBIRTH CamperClaim] Projection send refused: "+error.GetType().Name);}continue;}
            published.Identity=identity;published.Ready=ready;published.Mode=mode;published.Range=range;published.Position=vehicle.position;published.Rotation=vehicle.qrotation;published.Next=Time.realtimeSinceStartup+5f;
        }
    }
    private static void Shutdown(ref ModEvents.SWorldShuttingDownData data)
    {projections=new ConditionalWeakTable<EntityVehicle,Projection>();publications=new ConditionalWeakTable<EntityVehicle,Publication>();sequence=0;}
    internal static void Revoke(EntityVehicle vehicle)
    {
        if(vehicle==null||!ThreadManager.IsMainThread())return;
        projections.Remove(vehicle);
        if(vehicle.world!=null&&!vehicle.world.IsRemote()&&ReferenceEquals(GameManager.Instance?.World,vehicle.world)&&
            SingletonMonoBehaviour<ConnectionManager>.Instance?.IsServer==true&&publications.TryGetValue(vehicle,out var previous)&&previous.Identity!=null)
            try{SingletonMonoBehaviour<ConnectionManager>.Instance.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthCamperClaimProjection>()
                .Setup(vehicle.entityId,vehicle.world.worldState?.Guid,previous.Identity,vehicle.position,vehicle.qrotation,RebirthCamperEnergyMode.Travel,0,
                    RebirthCamperEnergyConfiguration.Current.Range,false,checked(++sequence)));}
            catch(Exception error){Log.Warning("[REBIRTH CamperClaim] Teardown projection unavailable: "+error.GetType().Name);}
        publications.Remove(vehicle);
    }
}

[HarmonyPatch(typeof(EntityVehicle),nameof(EntityVehicle.OnEntityUnload))]
internal static class RebirthCamperClaimProjectionUnload
{private static void Prefix(EntityVehicle __instance)=>RebirthCamperClaimProjection.Revoke(__instance);}

[Preserve]
public sealed class NetPackageRebirthCamperClaimProjection:NetPackage
{
    private int entity;private string worldId,identity;private Vector3 position;private Quaternion rotation;private RebirthCamperEnergyMode mode;private bool ready;private long serial,revision;private float range;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToClient;
    internal NetPackageRebirthCamperClaimProjection Setup(int entityId,string game,string camper,Vector3 pose,Quaternion orientation,RebirthCamperEnergyMode currentMode,long currentRevision,float radius,bool pinned,long sequence)
    {entity=entityId;worldId=game??string.Empty;identity=camper;position=pose;rotation=orientation;mode=currentMode;revision=currentRevision;range=radius;ready=pinned;serial=sequence;return this;}
    public override void read(PooledBinaryReader reader)
    {
        entity=reader.ReadInt32();worldId=reader.ReadString();identity=reader.ReadString();
        if(worldId.Length>128||identity.Length>36)throw new System.IO.InvalidDataException("Bounded claim projection identity required.");
        position=new Vector3(reader.ReadSingle(),reader.ReadSingle(),reader.ReadSingle());rotation=new Quaternion(reader.ReadSingle(),reader.ReadSingle(),reader.ReadSingle(),reader.ReadSingle());
        mode=(RebirthCamperEnergyMode)reader.ReadByte();revision=reader.ReadInt64();range=reader.ReadSingle();ready=reader.ReadBoolean();serial=reader.ReadInt64();
    }
    public override void write(PooledBinaryWriter writer)
    {base.write(writer);writer.Write(entity);writer.Write(worldId);writer.Write(identity);writer.Write(position.x);writer.Write(position.y);writer.Write(position.z);
        writer.Write(rotation.x);writer.Write(rotation.y);writer.Write(rotation.z);writer.Write(rotation.w);writer.Write((byte)mode);writer.Write(revision);writer.Write(range);writer.Write(ready);writer.Write(serial);}
    public override void ProcessPackage(World world,GameManager callbacks)
    {if(world?.IsRemote()==true)RebirthCamperClaimProjection.Receive(world,entity,worldId,identity,position,rotation,mode,revision,range,ready,serial);}
    public int GetLength()=>0;
}

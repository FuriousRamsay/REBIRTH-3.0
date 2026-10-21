using System;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;

internal sealed partial class RebirthCamperTurretRuntime
{
    private static readonly ConditionalWeakTable<EntityVehicle,RebirthCamperTurretRuntime> owners=new ConditionalWeakTable<EntityVehicle,RebirthCamperTurretRuntime>();
    private readonly EntityVehicle vehicle;
    private RebirthCamperTurretState state;
    private string observed;
    private float nextSync,nextAim;private readonly float[] nextBind=new float[4];private bool dirty;private Transform boundRoot;
    private readonly Gun[] guns=new Gun[4];
    private readonly Collider[] barrelHits=new Collider[32];
    private readonly RaycastHit[] sightClearanceHits=new RaycastHit[64];
    private float inspectionUntil,inspectionYaw,inspectionPitch;
    private int inspectionCorner=-1;
    private string lastClearanceBlock="none";
    private int shotTraceCount;
    internal bool CameraPose(int corner,out Transform muzzle,out float yaw,out float pitch)
    {var g=corner>=0&&corner<4?Bind(corner):null;muzzle=g?.Muzzle;yaw=g?.YawAngle??0;pitch=g?.PitchAngle??0;return muzzle&&Observe().Slots[corner].HasTurret;}
    internal void ClampAim(int corner,ref float yaw,ref float pitch)
    {
        var g=corner>=0&&corner<4?Bind(corner):null;
        yaw=Mathf.Clamp(yaw,g?.YawMinimum??-90,g?.YawMaximum??90);pitch=Mathf.Clamp(pitch,g?.PitchMinimum??-75,g?.PitchMaximum??75);
        if(g!=null){RebirthCamperAimBoundary.Clamp(g.YawAngle,g.PitchAngle,ref yaw,ref pitch,(y,p)=>{Pose(g,y,p);return BarrelClear(g);});Pose(g,g.YawAngle,g.PitchAngle);}
    }
    private sealed class Gun
    {
        internal Transform Corner,Yaw,Pitch,Muzzle;
        internal AutoTurretFireController Fire;
        internal TileEntityPoweredRangedTrap Tile;
        internal PowerRangedTrap Power;
        internal RebirthCamperTurretLaser Laser;
        internal bool RemoteManual;
        internal float PreviewUntil;
        internal int Kind,Target=-1,Burst;
        internal float Scan,Wake,NextShot,Cooldown,YawAngle,PitchAngle,RemoteYaw,RemotePitch;
        internal Quaternion Rest;
        internal float PitchMinimum=-75,PitchMaximum=75;
        internal float YawMinimum=-90,YawMaximum=90;
        internal float OwnedRadius;
        internal Renderer[] LensRenderers;
        internal MaterialPropertyBlock LensBlock;
        internal bool? LensActive;
        internal long ShotsFired;
        internal string ShotSound,ShotWeapon;
        internal float ShotInterval;
    }
    private RebirthCamperTurretRuntime(EntityVehicle v){vehicle=v;}
    internal static RebirthCamperTurretRuntime For(EntityVehicle v)=>owners.GetValue(v,k=>new RebirthCamperTurretRuntime(k));
    internal RebirthCamperTurretState Observe()
    {
        vehicle.vehicle.GetUpdatedItemValue().TryGetMetadata(RebirthCamperTurretState.Key,out string text);
        if(state==null||text!=observed){state=RebirthCamperTurretState.Read(vehicle);observed=text;}return state;
    }
    internal void Changed(RebirthCamperTurretState value,bool sync=true)
    {state=value;dirty=!sync;state.Save(vehicle,sync);vehicle.vehicle.GetUpdatedItemValue().TryGetMetadata(RebirthCamperTurretState.Key,out observed);}
    private Gun Bind(int corner)
    {
        var root=vehicle.RootTransform;if(!root)return null;
        var g=guns[corner];
        if(g?.Corner&&g.Corner.IsChildOf(root))return g;
        if(boundRoot!=root){foreach(var old in guns)old?.Laser?.Destroy();boundRoot=root;Array.Clear(nextBind,0,4);Array.Clear(guns,0,4);}
        if(Time.time<nextBind[corner])return null;nextBind[corner]=Time.time+2;
        var t=root.GetComponentsInChildren<Transform>(true).FirstOrDefault(c=>c.name=="REBIRTHCamperTurretSlot"+corner);if(!t)return null;
        g=new Gun{Corner=t,Yaw=t.GetComponentsInChildren<Transform>(true).FirstOrDefault(c=>c.name=="TurretYaw"),Pitch=t.GetComponentsInChildren<Transform>(true).FirstOrDefault(c=>c.name=="TurretPitch"),Muzzle=t.GetComponentsInChildren<Transform>(true).FirstOrDefault(c=>c.name=="TurretMuzzle")};
        if(!g.Yaw||!g.Pitch||!g.Muzzle)return null;
        // Asset metadata keeps old bundles compatible while allowing a new
        // whole-body rig to stop before its fixed inner bearing ring.
        var limits=t.GetComponentsInChildren<Transform>(true).FirstOrDefault(c=>c.name=="TurretPitchLimits");
        if(limits)
        {
            var range=limits.localPosition;
            if(!float.IsNaN(range.x)&&!float.IsNaN(range.y)&&range.x>=-75&&range.x<0&&range.y>0&&range.y<=75)
            {g.PitchMinimum=range.x;g.PitchMaximum=range.y;}
        }
        var yawLimits=t.GetComponentsInChildren<Transform>(true).FirstOrDefault(c=>c.name=="TurretYawLimits");
        if(yawLimits)
        {
            var range=yawLimits.localPosition;
            if(!float.IsNaN(range.x)&&!float.IsNaN(range.y)&&range.x>=-90&&range.x<0&&range.y>0&&range.y<=90){g.YawMinimum=range.x;g.YawMaximum=range.y;}
        }
        g.Rest=g.Yaw.localRotation;guns[corner]=g;
        g.OwnedRadius=OwnedClearanceRadius(root);
        RebirthCamperTurretPhysicalTarget.Attach(vehicle,corner,g.Pitch);
        RebirthCamperOwnCollision.Invalidate(vehicle);
        return g;
    }
    internal void Tick()
    {
        var snapshot=Observe();bool server=!vehicle.world.IsRemote();float now=Time.time;
        IdleHum(snapshot,server);
        for(int i=0;i<4;i++)
        {
            var g=Bind(i);if(g==null)continue;var slot=snapshot.Slots[i];
            bool wiredPower=RebirthCamperRelayWiring.TurretPowered(vehicle,i);
            Lens(g,slot.HasTurret&&wiredPower&&!vehicle.IsDead()&&(slot.Enabled||(server?LiveControl(i)!=null:g.RemoteManual)));
            if(g.Corner.gameObject.activeSelf!=slot.HasTurret)g.Corner.gameObject.SetActive(slot.HasTurret);
            if(!slot.HasTurret)
            {
                g.Laser?.Destroy();g.Laser=null;g.RemoteManual=false;
                if(g.Fire){UnityEngine.Object.Destroy(g.Fire.gameObject);g.Fire=null;}g.Kind=0;g.Target=-1;g.Burst=0;g.YawAngle=g.PitchAngle=g.RemoteYaw=g.RemotePitch=0;Pose(g,0,0);continue;
            }
            if(!server){if(Time.realtimeSinceStartup>=g.PreviewUntil){float y=g.RemoteManual?g.RemoteYaw:Mathf.Lerp(g.YawAngle,g.RemoteYaw,Time.deltaTime*12);float p=g.RemoteManual?g.RemotePitch:Mathf.Lerp(g.PitchAngle,g.RemotePitch,Time.deltaTime*12);Move(g,y,p,float.MaxValue);}Laser(g,slot,wiredPower&&!vehicle.IsDead()&&(slot.Enabled||g.RemoteManual||Time.realtimeSinceStartup<g.PreviewUntil));continue;}
            var control=LiveControl(i);
            bool pressed=control?.Press.Take(Time.unscaledTime)==true;
            bool requestedFire=pressed||(control!=null&&control.Fire&&Time.unscaledTime<control.InputUntil);
            if(!wiredPower){Laser(g,slot,false);g.Target=-1;continue;}
            Laser(g,slot,!vehicle.IsDead()&&(slot.Enabled||control!=null));
            if(control!=null)
            {
                Configure(g,slot);g.Target=-1;g.Wake=0;
                bool clear=Move(g,control.Yaw,control.Pitch,float.MaxValue);
                Laser(g,slot,!vehicle.IsDead());
                if(requestedFire&&clear&&now>=g.NextShot&&now>=g.Cooldown
                    &&!ManualRangeBlocked(g,control,i,slot))Fire(g,slot,snapshot,now,control.Player.entityId);
                continue;
            }
            if(inspectionCorner==i&&now<inspectionUntil){Move(g,inspectionYaw,inspectionPitch,45);continue;}
            if(!slot.Enabled||vehicle.IsDead()){g.Target=-1;g.Wake=0;continue;}
            Configure(g,slot);
            float range=RebirthCamperTurretProfiles.Range(slot);
            if(range<=0){g.Target=-1;g.Wake=0;IdleScan(g,i,now);continue;}
            if(g.Target<0||now>=g.Scan)
            {
                if(now>=g.Scan)
                {
                g.Scan=now+.5f;
                var origin=g.Muzzle.position+Origin.position;
                var targets=vehicle.world.GetEntitiesInBounds(typeof(EntityAlive),new Bounds(origin,Vector3.one*range*2),new List<Entity>());
                EntityAlive best=null;float distance=float.MaxValue;
                foreach(var e in targets)
                {
                    if(!(e is EntityAlive a)||!Eligible(a))continue;
                    if(!Angles(g,a,out _,out _))continue;float d=(a.getChestPosition()-origin).sqrMagnitude;
                    if(d>=distance||d>range*range||!Sight(g,a,range))continue;best=a;distance=d;
                }
                int next=best?.entityId??-1;if(next!=g.Target)g.Wake=now+RebirthCamperTurretProfiles.Number(RebirthCamperTurretProfiles.Block(slot),"WakeUpTime",.5f);g.Target=next;
                }
            }
            var target=vehicle.world.GetEntity(g.Target) as EntityAlive;
            if(!Eligible(target)||!Angles(g,target,out float yaw,out float pitch))
            {
                g.Target=-1;IdleScan(g,i,now);continue;
            }
            float speed=RebirthCamperTurretProfiles.Number(RebirthCamperTurretProfiles.Block(slot),"TurnSpeed",22.5f);
            if(!Move(g,yaw,pitch,speed)){g.Target=-1;continue;}
            if(now<g.Wake||now<g.NextShot||now<g.Cooldown||Mathf.Abs(Mathf.DeltaAngle(yaw,g.YawAngle))>2||Mathf.Abs(pitch-g.PitchAngle)>2||!Sight(g,target,range))continue;
            int ammo=slot.LoadedAmmo;if(ammo<0)continue;
            if((target.getChestPosition()-Origin.position-g.Muzzle.position).sqrMagnitude>Mathf.Pow(g.Fire.GetRange(slot.Ammo[ammo].itemValue),2))continue;
            Fire(g,slot,snapshot,now,OwnerEntity(),target.entityId);
        }
        if(server&&dirty&&now>=nextSync){nextSync=now+.5f;dirty=false;vehicle.SendSyncData(4);}
        if(server&&now>=nextAim)
        {
            nextAim=now+.1f;for(int i=0;i<4;i++)if(guns[i]?.Corner&&snapshot.Slots[i].HasTurret)
                SingletonMonoBehaviour<ConnectionManager>.Instance.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthCamperTurretAim>().Setup(vehicle.entityId,i,guns[i].YawAngle,guns[i].PitchAngle,LiveControl(i)!=null),false,-1,vehicle.entityId);
        }
    }
    private void Fire(Gun g,RebirthCamperTurretSlot slot,RebirthCamperTurretState snapshot,float now,int actor,int targetId=-1)
    {
            int powerCorner=Array.IndexOf(guns,g);
            if(!RebirthCamperRelayWiring.TurretPowered(vehicle,powerCorner)||now<g.NextShot||now<g.Cooldown||!BarrelClear(g))return;
            int ammo=slot.LoadedAmmo;if(ammo<0)return;
            if(targetId>=0)
            {
                var target=vehicle.world.GetEntity(targetId) as EntityAlive;
                if(!Eligible(target)||!Angles(g,target,out _,out _)||!Sight(g,target,RebirthCamperTurretProfiles.Range(slot)))return;
            }
            if(shotTraceCount++<100)
                Log.Out("[CamperTurretShot] vehicle="+vehicle.entityId+" corner="+Array.IndexOf(guns,g)+
                    " mode="+(targetId>=0?"automatic":"manual")+" target="+targetId+" actor="+actor+
                    " ammo="+slot.Ammo[ammo].itemValue.ItemClass.GetItemName()+" yaw="+g.YawAngle+" pitch="+g.PitchAngle);
            var profile=RebirthCamperTurretFiringProfiles.Resolve(slot.Ammo[ammo].itemValue,slot.Silencer,slot.SniperMode);
            g.Fire.fireSound=profile.Sound;g.ShotSound=profile.Sound;g.ShotWeapon=profile.Weapon;g.ShotInterval=profile.Interval;
            // Native Fire owns spread, pellet count, ammo passive effects, penetration,
            // damage, impacts, water attenuation and networked muzzle/sound effects.
            g.Power.Stacks=new[]{slot.Ammo[ammo]};g.Power.IsLocked=true;g.Tile.ownerEntityID=actor;g.Fire.BlockPosition=g.Muzzle.position+Origin.position;
            g.Fire.Cone.position=g.Muzzle.position;g.Fire.Cone.rotation=g.Muzzle.rotation*Quaternion.Euler(0,180,0);
            int before=slot.Ammo[ammo].count;
            var priorContext=RebirthCamperTurretAmmoEffects.ActiveAmmo;
            try{RebirthCamperTurretAmmoEffects.ActiveAmmo=slot.Ammo[ammo].itemValue;RebirthCamperTurretManualShot.Fire(g.Fire,targetId<0);}
            finally{RebirthCamperTurretAmmoEffects.ActiveAmmo=priorContext;}
            slot.Ammo[ammo]=g.Power.Stacks[0];
            if(slot.Ammo[ammo].count<before)g.ShotsFired+=before-slot.Ammo[ammo].count;
            slot.ResolveAmmo();snapshot.Revision++;Changed(snapshot,false);
            g.NextShot=now+profile.Interval;g.Cooldown=now;g.Burst=0;
    }
    private bool Move(Gun g,float yaw,float pitch,float speed)
    {
        float oldYaw=g.YawAngle,oldPitch=g.PitchAngle;
        float nextYaw=Mathf.MoveTowards(oldYaw,Mathf.Clamp(yaw,g.YawMinimum,g.YawMaximum),Time.deltaTime*speed);
        float nextPitch=Mathf.MoveTowards(oldPitch,Mathf.Clamp(pitch,g.PitchMinimum,g.PitchMaximum),Time.deltaTime*speed);
        bool reached=RebirthCamperAimBoundary.Clamp(oldYaw,oldPitch,ref nextYaw,ref nextPitch,(y,p)=>{Pose(g,y,p);return BarrelClear(g);});
        g.YawAngle=nextYaw;g.PitchAngle=nextPitch;return reached;
    }
    // Explicit diagnostic only; uses the production movement/clearance path and expires automatically.
    internal string Inspect(int corner,float yaw,float pitch,bool move)
    {
        if(corner<0||corner>3)throw new ArgumentOutOfRangeException(nameof(corner));
        var g=Bind(corner);if(g==null)return "rig missing";
        if(move){inspectionCorner=corner;inspectionYaw=Mathf.Clamp(yaw,g.YawMinimum,g.YawMaximum);inspectionPitch=Mathf.Clamp(pitch,g.PitchMinimum,g.PitchMaximum);inspectionUntil=Time.time+20;}
        return "corner="+corner+" yaw="+g.YawAngle.ToString("F2")+" pitch="+g.PitchAngle.ToString("F2")+" pitchLimits="+g.PitchMinimum+","+g.PitchMaximum+" clear="+BarrelClear(g)+" base="+g.Yaw.parent.position.ToString("F3")+" muzzle="+(g.Muzzle.position+Origin.position).ToString("F3")+" target="+g.Target+" lastBlock="+lastClearanceBlock;
    }
    internal static bool Eligible(EntityAlive e)=>e!=null&&e.IsAlive()&&!(e is EntityPlayer)&&!(e is EntityVehicle)&&!(e is EntityTurret)&&!(e is EntityDrone)&&
        (e is EntityEnemy||e is EntityAnimal&&e.EntityClass.bIsEnemyEntity);
    private static bool Angles(Gun g,EntityAlive target,out float yaw,out float pitch)
    {
        var local=Quaternion.Inverse(g.Yaw.parent.rotation*g.Rest)*(target.getChestPosition()-Origin.position-g.Pitch.position);
        yaw=Mathf.Atan2(local.x,local.z)*Mathf.Rad2Deg;pitch=-Mathf.Atan2(local.y,new Vector2(local.x,local.z).magnitude)*Mathf.Rad2Deg;
        return yaw>=g.YawMinimum&&yaw<=g.YawMaximum&&pitch>=g.PitchMinimum&&pitch<=g.PitchMaximum;
    }
    private bool Sight(Gun g,EntityAlive target,float range)
    {
        var delta=target.getChestPosition()-Origin.position-g.Muzzle.position;
        if(delta.sqrMagnitude>range*range)return false;
        if(!Voxel.Raycast(vehicle.world,new Ray(g.Muzzle.position+Origin.position,delta.normalized),delta.magnitude+.1f,-538750989,8,0))return false;
        var hit=Voxel.voxelRayHitInfo;if(!hit.tag.StartsWith("E_"))return false;
        var root=GameUtils.GetHitRootTransform(hit.tag,hit.transform);return root&&root.GetComponent<Entity>()==target;
    }
    private bool BarrelClear(Gun g)
    {
        int count=Physics.OverlapCapsuleNonAlloc(g.Pitch.position,g.Muzzle.position,.025f,barrelHits,~0,QueryTriggerInteraction.Ignore);
        if(count==barrelHits.Length)return false;
        for(int i=0;i<count;i++){var hit=barrelHits[i];if(OwnedObstruction(hit,g)){lastClearanceBlock=hit.name;return false;}}
        // A clear barrel can still point through a wall/window/plough several
        // metres ahead. CCTV/fire must stop before that own-vehicle sightline.
        float reach=g.OwnedRadius+(g.Muzzle.position-vehicle.RootTransform.position).magnitude+.1f;
        count=Physics.SphereCastNonAlloc(g.Muzzle.position,.035f,g.Muzzle.forward,sightClearanceHits,reach,~0,QueryTriggerInteraction.Ignore);
        if(count==sightClearanceHits.Length)return false;
        for(int i=0;i<count;i++)if(OwnedObstruction(sightClearanceHits[i].collider,g)){lastClearanceBlock=sightClearanceHits[i].collider.name;return false;}
        return true;
    }
    private static void Pose(Gun g,float yaw,float pitch){g.Yaw.localRotation=g.Rest*Quaternion.Euler(0,yaw,0);g.Pitch.localRotation=Quaternion.Euler(pitch,0,0);}
    internal void Aim(int slot,float yaw,float pitch,bool manual)
    {if(slot<0||slot>=4||float.IsNaN(yaw)||float.IsNaN(pitch)||Mathf.Abs(yaw)>90||Mathf.Abs(pitch)>75)return;var g=Bind(slot);if(g!=null){g.RemoteYaw=Mathf.Clamp(yaw,g.YawMinimum,g.YawMaximum);g.RemotePitch=Mathf.Clamp(pitch,g.PitchMinimum,g.PitchMaximum);g.RemoteManual=manual;}}
    private void Laser(Gun gun,RebirthCamperTurretSlot slot,bool visible)
    {
        if(GameManager.IsDedicatedServer)return;
        float range=RebirthCamperTurretProfiles.Range(slot);visible&=range>0;
        if(gun.Laser==null&&visible)gun.Laser=new RebirthCamperTurretLaser(gun.Corner);
        // The aim indicator reaches the surface under the crosshair even beyond
        // firing range. Damage/range admission remains in the server fire path.
        gun.Laser?.Update(vehicle.world,gun.Muzzle,visible,1000f);
    }
    internal void PreviewManualAim(int corner,float yaw,float pitch)
    {
        if(corner<0||corner>=4||!RebirthCamperRelayWiring.TurretPowered(vehicle,corner))return;
        if(!vehicle.world.IsRemote()&&!(LiveControl(corner)?.Player is EntityPlayerLocal))return;
        var gun=Bind(corner);if(gun==null)return;
        gun.PreviewUntil=Time.realtimeSinceStartup+.25f;
        Move(gun,yaw,pitch,float.MaxValue);
        // Local host input is already on the authoritative game thread. Keep
        // the lease pose current each frame instead of snapping to 20 Hz input.
        if(!vehicle.world.IsRemote()){var lease=LiveControl(corner);lease.Yaw=gun.YawAngle;lease.Pitch=gun.PitchAngle;}
        Laser(gun,Observe().Slots[corner],true);
    }
    private int OwnerEntity(){var owner=vehicle.GetOwner();return owner==null?-1:GameManager.Instance.GetPersistentPlayerList()?.GetPlayerData(owner)?.EntityId??-1;}
    private void Configure(Gun g,RebirthCamperTurretSlot slot)
    {
        int kind=slot.Installed.itemValue.type;if(g.Fire&&g.Kind==kind)return;
        if(g.Fire)UnityEngine.Object.Destroy(g.Fire.gameObject);
        var native=new GameObject("Camper native turret firing adapter");native.transform.SetParent(g.Corner,false);
        var controller=native.AddComponent<AutoTurretController>();controller.enabled=false;
        var fire=native.AddComponent<AutoTurretFireController>();fire.enabled=false;
        int corner=Array.IndexOf(guns,g);if(corner<0)throw new InvalidOperationException("Unbound camper firing adapter");
        RebirthCamperTurretAmmoEffects.Register(fire,()=>Observe().Slots[corner]);
        var cone=GameObject.CreatePrimitive(PrimitiveType.Cube);cone.name="Native fire origin";cone.transform.SetParent(native.transform,false);UnityEngine.Object.Destroy(cone.GetComponent<Collider>());cone.GetComponent<Renderer>().enabled=false;
        var laser=new GameObject("Unused native targeting laser");laser.transform.SetParent(native.transform,false);
        fire.Cone=cone.transform;fire.Laser=laser.transform;fire.Muzzle=g.Muzzle;fire.BlockPosition=g.Muzzle.position+Origin.position;
        fire.Init(RebirthCamperTurretProfiles.Block(slot).Properties,controller);
        fire.muzzleSmokeParticle=string.Empty;
        var power=new PowerRangedTrap();var tile=new TileEntityPoweredRangedTrap(null){PowerItem=power,ownerID=vehicle.GetOwner(),ownerEntityID=OwnerEntity()};tile.SetDisableModifiedCheck(true);
        fire.TileEntity=tile;controller.FireController=fire;g.Tile=tile;g.Power=power;g.Fire=fire;g.Kind=kind;
    }
}
[HarmonyPatch(typeof(EntityVehicle),"Update")]
internal static class RebirthCamperTurretTick
{
    private static readonly ConditionalWeakTable<EntityVehicle,object> warned=new ConditionalWeakTable<EntityVehicle,object>();
    static void Postfix(EntityVehicle __instance)
    {
        if(!Rebirth.Vehicles.Camper.RebirthCamperModulePatchInstaller.IsInstalled||!ThreadManager.IsMainThread()||!RebirthCamperRadial.IsCamper(__instance)||__instance.world==null||!ReferenceEquals(GameManager.Instance?.World,__instance.world)||!ReferenceEquals(__instance.world.GetEntity(__instance.entityId),__instance))return;
        try{RebirthCamperTurretRuntime.For(__instance).Tick();}
        catch(Exception e){if(!warned.TryGetValue(__instance,out _)){warned.Add(__instance,new object());Log.Warning("[CamperTurrets] State retained; turret tick refused: "+e.Message);}}
    }
}
[UnityEngine.Scripting.Preserve]
public sealed class NetPackageRebirthCamperTurretAim:NetPackage
{
    int entity,slot;float yaw,pitch;bool manual;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToClient;
    internal NetPackageRebirthCamperTurretAim Setup(int e,int s,float y,float p,bool m){entity=e;slot=s;yaw=y;pitch=p;manual=m;return this;}
    public override void read(PooledBinaryReader r){entity=r.ReadInt32();slot=r.ReadByte();yaw=r.ReadSingle();pitch=r.ReadSingle();manual=r.ReadBoolean();}
    public override void write(PooledBinaryWriter w){base.write(w);w.Write(entity);w.Write((byte)slot);w.Write(yaw);w.Write(pitch);w.Write(manual);}
    public override void ProcessPackage(World world,GameManager callbacks){if(world?.IsRemote()==true&&world.GetEntity(entity) is EntityVehicle v&&RebirthCamperRadial.IsCamper(v))RebirthCamperTurretRuntime.For(v).Aim(slot,yaw,pitch,manual);}
    public int GetLength()=>14;
}


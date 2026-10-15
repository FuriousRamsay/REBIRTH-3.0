using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Xml.Linq;
using HarmonyLib;
using UnityEngine;

public sealed class RebirthCamperEnergyPoiSnapshot
{
    public bool InRange {get;internal set;}
    public string PoiName {get;internal set;}
    public int SupportedLights {get;internal set;}
    public bool IsOn {get;internal set;}
    public float Range {get;internal set;}
    public int DemandWatts {get;internal set;}
    // Prospective load is distinct from currently powered/metered demand.
    // Older observation packets leave this unknown and cannot enable ON.
    public int RequiredWatts {get;internal set;} = -1;
}

// Authoritative unattached native leaf consumers only. Never reparent a native
// circuit, simulate a source bank, replace ammunition or change targeting policy.
public static class RebirthCamperEnergyDeviceAdapter
{
    private const string PoiPowerObservationKey="RebirthCamperPoiPowerV1";
    private sealed class PoiPowerGate
    {internal World World;internal string GameGuid,Identity,Target,Observation;internal float Next;internal bool Value;}
    private static readonly ConditionalWeakTable<EntityVehicle,PoiPowerGate> poiPowerGates=new ConditionalWeakTable<EntityVehicle,PoiPowerGate>();
    // Legacy configuration/lease field retained for read compatibility. POI
    // eligibility now requires footprint containment and never uses this radius.
    public static float PoiRange {get;private set;}=20f;
    public static float DefenseRange {get;private set;}=12f;
    // Conversion for native BlockLight, which has no RequiredPower field.
    public static int OrdinaryPoiLightWatts {get;private set;}=1;
    private static int PlainDemand(EntityVehicle vehicle)=>OrdinaryPoiLightWatts;
    private static double PlainEnergyDemand(EntityVehicle vehicle)
    {
        float saving=RebirthCamperEnergyEfficiency.OwnerSaving(vehicle);
        if(!Finite(saving))saving=0;
        return OrdinaryPoiLightWatts*(1d-Math.Max(0d,Math.Min(0.35d,saving)));
    }
    private sealed class Baseline
    {internal Vector3i Position;internal uint OrdinaryStamp;internal ushort Block;internal byte Meta,PreviousMeta,ExpectedMeta;internal bool Powered,Active,Toggle,PreviousToggle,ExpectedToggle;internal bool HasToggle,Applied,Removed,HasWritten,Proofed;}
    private sealed class Session
    {
        internal EntityVehicle Vehicle;internal World World;internal string WorldId;internal Guid Identity,Operation;
        internal readonly Dictionary<Vector3i,Baseline> Baselines=new Dictionary<Vector3i,Baseline>();
        internal readonly Dictionary<Vector3i,PowerItem> Nodes=new Dictionary<Vector3i,PowerItem>();
        internal readonly Dictionary<Vector3i,TileEntityLight> PlainNodes=new Dictionary<Vector3i,TileEntityLight>();
        internal readonly HashSet<Vector3i> Restored=new HashSet<Vector3i>();
        internal readonly HashSet<Vector3i> Excluded=new HashSet<Vector3i>();
        internal readonly HashSet<Vector3i> ResetPlain=new HashSet<Vector3i>();
        internal bool PoiOn,PoiControl;internal PrefabInstance Poi;internal byte[] SavedImage,ProposedImage;
        internal bool PoiEligible,Dirty,WitnessWarned;internal string PoiName;internal int PoiSupported;internal float NextPoiObservation;
        internal RebirthCamperSavedVehicle.Expected Witness;
    }
    private sealed class PlainConsumer : IRebirthCamperEnergyConsumer,IRebirthCamperEnergyAccountingConsumer
    {
        internal Session Session;internal TileEntityLight Light;internal int Watts;internal double EnergyWatts;
        public double EnergyDemandWatts=>EnergyWatts;
        public string Id=>"native-poi-light:"+Light.ToWorldPos();
        public int Priority=>1;
        public int DemandWatts=>Watts;
        public float Distance=>0;
        public void SetSupply(EntityVehicle vehicle,int suppliedWatts)
        {
            if(!ReferenceEquals(vehicle,Session.Vehicle)||!Current(Session)||Session.Operation!=Guid.Empty||!CurrentPlain(Session,Light)||!PlainAuthorized(Session,Light))return;
            var position=Light.ToWorldPos();var baseline=Session.Baselines[position];
            if(!MatchesPlain(Session,Light)){Session.Excluded.Add(position);plainOwners.Remove(Light);return;}
            if(Watts>0&&suppliedWatts<Watts){RestorePlain(Session,Light);return;}
            var value=Session.World.GetBlock(position);
            if(value.meta!=baseline.ExpectedMeta){value.meta=baseline.ExpectedMeta;Session.World.SetBlockRPC(position,value);}
            baseline.Applied=true;baseline.HasWritten=true;Session.Restored.Remove(position);plainOwners[Light]=Session;
        }
    }
    private sealed class Consumer : IRebirthCamperEnergyConsumer
    {
        internal Session Session;internal PowerConsumer Node;internal bool Poi,Defense;internal int Watts;internal float RangeDistance;
        public string Id=>"native-device:"+Node.Position.x+":"+Node.Position.y+":"+Node.Position.z;
        public int Priority=>Defense?0:1;
        public int DemandWatts=>Watts;
        public float Distance=>RangeDistance;
        public void SetSupply(EntityVehicle vehicle,int suppliedWatts)
        {
            if(!ReferenceEquals(vehicle,Session.Vehicle)||!Current(Session)||!CurrentNode(Session,Node)||!Eligible(Node)||Session.Operation!=Guid.Empty)return;
            if(Poi?!PoiOwnerAuthorized(vehicle,Node):!DeviceOwnerAuthorized(Session,Node)){Relinquish(Session,Node);return;}
            if(!MatchesExpected(Session,Node)){Relinquish(Session,Node);return;}
            if(Watts>0&&suppliedWatts<Watts){Restore(Session,Node);return;}
            if(Poi&&Node is PowerConsumerToggle toggle)toggle.IsToggled=Session.PoiOn;
            ushort supply=(ushort)(Watts>0&&suppliedWatts>=Watts?Node.RequiredPower:0);
            Node.HandlePowerReceived(ref supply);Node.HandlePowerUpdate(Watts>0&&suppliedWatts>=Watts);
            Session.Baselines[Node.Position].Applied=true;Session.Baselines[Node.Position].HasWritten=true;
            Session.Restored.Remove(Node.Position);Session.Nodes[Node.Position]=Node;owners[Node]=Session;
        }
    }
    private static readonly Dictionary<EntityVehicle,Session> sessions=new Dictionary<EntityVehicle,Session>();
    private static readonly Dictionary<PowerItem,Session> owners=new Dictionary<PowerItem,Session>();
    private static readonly Dictionary<TileEntityLight,Session> plainOwners=new Dictionary<TileEntityLight,Session>();
    private sealed class PoiProjection
    {internal World World;internal object Metadata;internal string Signature,Identity,GameGuid;internal bool Valid,Eligible,On;internal int Supported=-1;internal string Name,Target;internal float Range;}
    private static ConditionalWeakTable<EntityVehicle,PoiProjection> projections=new ConditionalWeakTable<EntityVehicle,PoiProjection>();
    private static bool installed;
    public static void Configure(float poiRange,float defenseRange,int ordinaryPoiLightWatts=1)
    {
        if(!ThreadManager.IsMainThread()||!Finite(poiRange)||!Finite(defenseRange)||poiRange<=0||poiRange>200||defenseRange<=0||defenseRange>200)
            throw new ArgumentException("Finite positive bounded energy adapter ranges required.");
        if(ordinaryPoiLightWatts<1||ordinaryPoiLightWatts>10000)throw new ArgumentException("Positive bounded provisional ordinary POI light demand required.");
        PoiRange=poiRange;DefenseRange=defenseRange;OrdinaryPoiLightWatts=ordinaryPoiLightWatts;
    }
    private static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
    public static void Install()
    {
        if(installed)return;
        RebirthHarmonyBootstrap.PatchClassOnce(new Harmony("rebirth.camper.energy.devices"),typeof(RebirthCamperEnergyDeviceUnload));
        RebirthHarmonyBootstrap.PatchClassOnce(new Harmony("rebirth.camper.energy.devices"),typeof(RebirthCamperEnergyDeviceRemoval));
        RebirthHarmonyBootstrap.PatchClassOnce(new Harmony("rebirth.camper.energy.devices"),typeof(RebirthCamperEnergyLightPlacement));
        RebirthHarmonyBootstrap.PatchClassOnce(new Harmony("rebirth.camper.energy.devices"),typeof(RebirthCamperEnergyDevicePlacement));
        RebirthCamperWorldLighting.Install(new Harmony("rebirth.camper.energy.world-lights"));
        RebirthHarmonyBootstrap.PatchClassOnce(new Harmony("rebirth.camper.energy.devices"),typeof(RebirthCamperEnergyOrdinaryLightUnload));
        RebirthHarmonyBootstrap.PatchClassOnce(new Harmony("rebirth.camper.energy.devices"),typeof(RebirthCamperEnergyOrdinaryLightRemoval));
        ModEvents.WorldShuttingDown.RegisterHandler(Shutdown);
        RebirthCamperEnergyRuntime.Consumers=Consumers;installed=true;
        RebirthCamperEnergyRuntime.SuspendConsumers=Suspend;
    }
    private static bool Authority(EntityVehicle vehicle)=>ThreadManager.IsMainThread()&&vehicle?.world!=null&&!vehicle.world.IsRemote()&&
        ReferenceEquals(GameManager.Instance?.World,vehicle.world)&&ReferenceEquals(vehicle.world.GetEntity(vehicle.entityId),vehicle)&&
        SingletonMonoBehaviour<ConnectionManager>.Instance?.IsServer==true;
    private static bool Current(Session s)=>Authority(s.Vehicle)&&ReferenceEquals(s.World,s.Vehicle.world)&&s.WorldId==s.World.worldState?.Guid;
    private static bool CurrentNode(Session s,PowerItem node)=>ReferenceEquals(GameManager.Instance?.World,s.World)&&!s.World.IsRemote()&&node?.TileEntity!=null&&
        PowerManager.HasInstance&&PowerManager.Instance.PowerItemDictionary.TryGetValue(node.Position,out var current)&&ReferenceEquals(current,node)&&
        ReferenceEquals(s.World.GetTileEntity(node.Position),node.TileEntity)&&s.World.GetBlock(node.Position).type==node.BlockID;
    private static bool Eligible(PowerItem node)=>node is PowerConsumer&&node.TileEntity is TileEntityPoweredBlock&&node.Parent==null&&
        node.Children.Count==0&&!node.TileEntity.HasParent()&&node.TileEntity.ChildCount==0&&node.RequiredPower>0&&
        (Block.list[node.BlockID] is BlockPoweredLight||Block.list[node.BlockID] is BlockPoweredTrap||Block.list[node.BlockID] is BlockRanged||Block.list[node.BlockID] is BlockLauncher);
    private static bool Light(PowerItem node)=>Block.list[node.BlockID] is BlockPoweredLight;
    private static bool Defense(PowerItem node)=>node is PowerRangedTrap||Block.list[node.BlockID] is BlockPoweredTrap;
    // Native ranged placement persists its actual owner but does not set the
    // generic IsPlayerPlaced flag. Its non-null native owner is placement proof;
    // DeviceOwnerAuthorized still requires the camper's exact owner afterward.
    private static bool PlayerDevice(PowerItem node)=>node.TileEntity.IsPlayerPlaced||
        node.TileEntity is TileEntityPoweredRangedTrap ranged&&ranged.GetOwner()!=null||
        node.TileEntity is TileEntityPoweredMeleeTrap melee&&melee.GetOwner()!=null;
    private static Session For(EntityVehicle vehicle)
    {
        if(sessions.TryGetValue(vehicle,out var found))return found;
        var s=new Session{Vehicle=vehicle,World=vehicle.world,WorldId=vehicle.world.worldState?.Guid,Identity=RebirthCamperEnergyRuntime.For(vehicle).Identity};
        s.SavedImage=RebirthCamperEnergyRuntime.ReadDeviceLease(vehicle);
        if(s.SavedImage.Length>0)
        {
            var root=ReadLease(s.SavedImage);
            if(root.Name!="energyDeviceLease"||(int)root.Attribute("version")!=1||(string)root.Attribute("world")!=s.WorldId||Guid.Parse((string)root.Attribute("vehicle"))!=s.Identity)
                throw new InvalidOperationException("Energy device lease belongs to another world/vehicle.");
            s.PoiOn=(bool?)root.Attribute("poiOn")??false;s.PoiControl=(bool?)root.Attribute("poiControl")??false;
            s.PoiEligible=(bool?)root.Attribute("poiEligible")??false;s.PoiName=(string)root.Attribute("poiName");
            foreach(var row in root.Elements("device"))
            {
                var b=new Baseline{Position=new Vector3i((int)row.Attribute("x"),(int)row.Attribute("y"),(int)row.Attribute("z")),Block=checked((ushort)(int)row.Attribute("block")),Meta=checked((byte)(int)row.Attribute("meta")),
                    Powered=(bool)row.Attribute("powered"),Active=(bool)row.Attribute("active"),Toggle=(bool)row.Attribute("toggle"),HasToggle=(bool)row.Attribute("hasToggle"),
                    PreviousMeta=checked((byte)((int?)row.Attribute("previousMeta")??(int)row.Attribute("meta"))),PreviousToggle=(bool?)row.Attribute("previousToggle")??(bool)row.Attribute("toggle"),
                    ExpectedMeta=checked((byte)((int?)row.Attribute("expectedMeta")??(int)row.Attribute("meta"))),ExpectedToggle=(bool?)row.Attribute("expectedToggle")??(bool)row.Attribute("toggle"),OrdinaryStamp=(uint?)row.Attribute("ordinaryStamp")??0,Removed=(bool?)row.Attribute("removed")??false,Proofed=true};
                if(s.Baselines.ContainsKey(b.Position))throw new InvalidOperationException("Duplicate energy device baseline.");s.Baselines.Add(b.Position,b);
            }
        }
        sessions.Add(vehicle,s);return s;
    }
    private static XElement ReadLease(byte[] bytes,bool headerOnly=false)
    {
        if(bytes==null||bytes.Length==0||bytes.Length>RebirthCamperEnergyDeviceLeaseStorage.MaximumBytes)throw new InvalidOperationException("Bounded native device lease required.");
        using(var stream=new System.IO.MemoryStream(bytes,false))
        using(var reader=new System.IO.BinaryReader(stream,Encoding.UTF8,true))
        {
            int magic=bytes.Length>=4?reader.ReadInt32():0;
            if(magic==0x32444C45||magic==0x33444C45||magic==0x34444C45||magic==0x35444C45)
            {
                string world=reader.ReadString();var vehicle=new Guid(reader.ReadBytes(16));bool eligible=reader.ReadBoolean();float range=reader.ReadSingle();string name=reader.ReadString();
                bool on=magic>=0x33444C45&&reader.ReadBoolean(),control=magic>=0x33444C45&&reader.ReadBoolean();int supported=magic==0x35444C45?reader.ReadInt32():-1;string target=magic==0x35444C45?reader.ReadString():string.Empty;int count=reader.ReadInt32();
                if(!Guid.TryParse(world,out _)||!Finite(range)||range<=0||range>200||count<0||supported< -1||(long)count*(magic>=0x34444C45?22:18)!=stream.Length-stream.Position)throw new InvalidOperationException("Malformed compact device lease.");
                var root=new XElement("energyDeviceLease",new XAttribute("version",1),new XAttribute("world",world),new XAttribute("vehicle",vehicle),new XAttribute("poiEligible",eligible),new XAttribute("poiRange",range),new XAttribute("poiName",name),new XAttribute("poiOn",on),new XAttribute("poiControl",control),new XAttribute("poiSupported",supported),new XAttribute("poiTarget",target));
                if(headerOnly)
                {
                    for(int i=0;i<count;i++){stream.Position+=17;if((reader.ReadByte()&128)!=0)throw new InvalidOperationException("Unsupported compact lease flags.");if(magic>=0x34444C45)stream.Position+=4;}
                    return root;
                }
                for(int i=0;i<count;i++)
                {
                    int x=reader.ReadInt32(),y=reader.ReadInt32(),z=reader.ReadInt32();ushort block=reader.ReadUInt16();byte meta=reader.ReadByte(),previous=reader.ReadByte(),expected=reader.ReadByte(),flags=reader.ReadByte();
                    uint stamp=magic>=0x34444C45?reader.ReadUInt32():0;
                    if((flags&128)!=0)throw new InvalidOperationException("Unsupported compact lease flags.");
                    root.Add(new XElement("device",new XAttribute("x",x),new XAttribute("y",y),new XAttribute("z",z),new XAttribute("block",block),new XAttribute("meta",meta),new XAttribute("previousMeta",previous),new XAttribute("expectedMeta",expected),
                        new XAttribute("ordinaryStamp",stamp),new XAttribute("powered",(flags&1)!=0),new XAttribute("active",(flags&2)!=0),new XAttribute("toggle",(flags&4)!=0),new XAttribute("hasToggle",(flags&8)!=0),new XAttribute("previousToggle",(flags&16)!=0),new XAttribute("expectedToggle",(flags&32)!=0),new XAttribute("removed",(flags&64)!=0)));
                }
                return root;
            }
        }
        using(var stream=new System.IO.MemoryStream(bytes,false))
        using(var reader=System.Xml.XmlReader.Create(stream,new System.Xml.XmlReaderSettings{DtdProcessing=System.Xml.DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=RebirthCamperEnergyDeviceLeaseStorage.MaximumBytes}))
            return XElement.Load(reader);
    }
    internal static bool TryRetainPlainProvenance(World world,Vector3i position,BlockValue value,out uint stamp)
    {
        stamp=0;if(!ThreadManager.IsMainThread()||world==null||world.IsRemote())return false;
        foreach(var entity in world.Entities.list)
        {
            if(!(entity is EntityVehicle vehicle)||!Authority(vehicle))continue;
            try
            {
                var bytes=RebirthCamperEnergyRuntime.ReadDeviceLease(vehicle);if(bytes.Length==0)continue;
                var root=ReadLease(bytes);
                if((string)root.Attribute("world")!=world.worldState?.Guid||(bool?)root.Attribute("poiOn")!=true||(bool?)root.Attribute("poiControl")!=true)continue;
                foreach(var row in root.Elements("device"))
                    if((int)row.Attribute("x")==position.x&&(int)row.Attribute("y")==position.y&&(int)row.Attribute("z")==position.z&&
                        (int)row.Attribute("block")==value.type&&(int?)row.Attribute("expectedMeta")==value.meta&&(bool?)row.Attribute("removed")!=true)
                    {stamp=(uint?)row.Attribute("ordinaryStamp")??0;if(stamp!=0)return true;}
            }
            catch{ /* Malformed/foreign leases never authorize native light changes. */ }
        }
        return false;
    }
    private static byte[] Encode(Session s)
    {
        using(var stream=new System.IO.MemoryStream())using(var writer=new System.IO.BinaryWriter(stream,Encoding.UTF8,true))
        {
            writer.Write(0x35444C45);writer.Write(s.WorldId);writer.Write(s.Identity.ToByteArray());writer.Write(s.PoiEligible);writer.Write(PoiRange);writer.Write(s.PoiName??"");writer.Write(s.PoiOn);writer.Write(s.PoiControl);writer.Write(s.PoiSupported);writer.Write(PoiTarget(s.Poi));writer.Write(s.Baselines.Count);
            foreach(var b in s.Baselines.Values.OrderBy(b=>b.Position.x).ThenBy(b=>b.Position.y).ThenBy(b=>b.Position.z))
            {
                writer.Write(b.Position.x);writer.Write(b.Position.y);writer.Write(b.Position.z);writer.Write(b.Block);writer.Write(b.Meta);writer.Write(b.PreviousMeta);writer.Write(b.ExpectedMeta);
                writer.Write((byte)((b.Powered?1:0)|(b.Active?2:0)|(b.Toggle?4:0)|(b.HasToggle?8:0)|(b.PreviousToggle?16:0)|(b.ExpectedToggle?32:0)|(b.Removed?64:0)));
                writer.Write(b.OrdinaryStamp);
            }
            writer.Flush();var result=stream.ToArray();if(result.Length>RebirthCamperEnergyDeviceLeaseStorage.MaximumBytes)throw new InvalidOperationException("Restoration lease exceeds native transport frame; retain existing policy.");return result;
        }
    }
    private static bool Witness(Session s)
    {
        if(s.Operation==Guid.Empty)return true;
        try
        {
            var runtime=RebirthCamperEnergyRuntime.For(s.Vehicle);
            if(!runtime.Hold(s.Operation))return false;
            if(s.Witness==null&&!runtime.RequestDeviceLeasePersistence(s.Operation,s.SavedImage,s.ProposedImage,out s.Witness))return false;
            if(!RebirthCamperSavedVehicle.Verify(s.Vehicle,s.Witness,()=>Current(s)))return false;
            s.SavedImage=s.ProposedImage;s.ProposedImage=null;s.Witness=null;s.WitnessWarned=false;
            foreach(var baseline in s.Baselines.Values)baseline.Proofed=true;
            runtime.Release(s.Operation);s.Operation=Guid.Empty;return true;
        }
        catch(Exception error)
        {
            if(!s.WitnessWarned){s.WitnessWarned=true;Log.Warning("[REBIRTH Energy] Device baseline native-save request retained for exact retry: "+error.GetType().Name);}
            return false; // Same operation/images/hold; no native world setter.
        }
    }
    public static IEnumerable<IRebirthCamperEnergyConsumer> Consumers(EntityVehicle vehicle)
    {
        if(!Authority(vehicle))return Array.Empty<IRebirthCamperEnergyConsumer>();
        var s=For(vehicle);if(!Current(s))return Array.Empty<IRebirthCamperEnergyConsumer>();
        if(!Witness(s))return Array.Empty<IRebirthCamperEnergyConsumer>();
        if(s.Dirty)
        {s.Dirty=false;s.ProposedImage=Encode(s);s.Operation=Guid.NewGuid();if(!Witness(s))return Array.Empty<IRebirthCamperEnergyConsumer>();}
        var energy=RebirthCamperEnergyRuntime.For(vehicle).Snapshot();
        var mode=energy.Mode;
        if(mode!=RebirthCamperEnergyMode.Camp){Release(vehicle);return Array.Empty<IRebirthCamperEnergyConsumer>();}
        float range=RebirthCamperEnergyBoundsRangePolicy.ForVehicle(vehicle);
        var selectedPoi=NearestPoi(vehicle);
        if(s.Poi!=null&&!ReferenceEquals(s.Poi,selectedPoi)||s.PoiName!=null&&s.PoiName!=(selectedPoi?.prefab?.PrefabName)){s.PoiOn=false;s.PoiControl=false;}
        s.Poi=selectedPoi;if(s.Poi==null){s.PoiOn=false;s.PoiControl=false;}
        string poiName=s.Poi?.prefab?.PrefabName;bool changed=s.PoiEligible!=(s.Poi!=null)||s.PoiName!=poiName;
        s.PoiEligible=s.Poi!=null;s.PoiName=poiName;
        if(UnityEngine.Time.realtimeSinceStartup>=s.NextPoiObservation)
        {
            s.NextPoiObservation=UnityEngine.Time.realtimeSinceStartup+1f;
            var observation=ObservePoi(vehicle);int supported=observation.SupportedLights;
            changed|=supported!=s.PoiSupported;s.PoiSupported=supported;
            PublishPoiPowerObservation(vehicle,s,energy,observation);
        }
        var candidates=new List<IRebirthCamperEnergyConsumer>();
        bool usable=energy.Sources.Any(source=>source.IsOn&&source.Status==RebirthCamperEnergySourceStatus.Available);
        foreach(var node in PowerManager.HasInstance?PowerManager.Instance.PowerItemDictionary.Values.ToArray():Array.Empty<PowerItem>())
        {
            if(!Eligible(node)||!CurrentNode(s,node)||s.Excluded.Contains(node.Position)||owners.TryGetValue(node,out var owner)&&!ReferenceEquals(owner,s))continue;
            if(node.IsPowered&&!owners.ContainsKey(node))continue; // Existing foreign/native power remains its owner's responsibility.
            bool poi=s.PoiControl&&!node.TileEntity.IsPlayerPlaced&&Light(node)&&s.Poi!=null&&Inside(s.Poi,node.Position);
            if(poi&&!PoiOwnerAuthorized(vehicle,node))continue;
            float distance=RebirthCamperEnergyBounds.Distance(RebirthCamperEnergyBounds.CurrentCenter(vehicle),node.Position.ToVector3Center());
            if(!poi&&(!PlayerDevice(node)||!RebirthCamperEnergyBounds.ContainsDistance(distance,range)||!DeviceOwnerAuthorized(s,node)))continue;
            bool defense=Defense(node);bool active=usable&&(!poi||s.PoiOn)&&(!defense||EnemyNear(s.World,node.Position));
            int watts=active?RebirthCamperEnergyEfficiency.Demand(vehicle,node.TileEntity,poi?(int?)node.RequiredPower:null):0;
            if(poi&&s.Baselines.TryGetValue(node.Position,out var old)&&old.OrdinaryStamp!=0&&old.OrdinaryStamp!=RebirthCamperWorldLighting.Stamp(node.TileEntity))
            {old.Removed=true;s.Excluded.Add(node.Position);s.ResetPlain.Add(node.Position);s.PoiOn=false;s.PoiControl=false;changed=true;continue;}
            if(!s.Baselines.ContainsKey(node.Position)||s.Restored.Contains(node.Position)||s.Baselines[node.Position].Removed)
            {
                var toggle=node as PowerConsumerToggle;
                byte meta=s.World.GetBlock(node.Position).meta;bool toggled=toggle?.IsToggled??false;
                s.Baselines[node.Position]=new Baseline{Position=node.Position,Block=node.BlockID,OrdinaryStamp=poi?RebirthCamperWorldLighting.Stamp(node.TileEntity):0,Meta=meta,Powered=node.IsPowered,
                    Active=((PowerConsumer)node).lastActivate,HasToggle=toggle!=null,Toggle=toggled,PreviousMeta=meta,PreviousToggle=toggled,ExpectedMeta=meta,ExpectedToggle=toggled};changed=true;
            }
            var baseline=s.Baselines[node.Position];
            if(!MatchesExpected(s,node)){Relinquish(s,node);continue;}
            // Native consumer activation writes only the power bit in the
            // audited supported classes. Persist intended state before its RPC.
            byte expected=(byte)((baseline.Meta&~2)|(watts>0?2:0));
            bool expectedToggle=poi?s.PoiOn:baseline.Toggle;
            if(baseline.ExpectedMeta!=expected||baseline.ExpectedToggle!=expectedToggle)
            {baseline.PreviousMeta=s.World.GetBlock(node.Position).meta;baseline.PreviousToggle=(node as PowerConsumerToggle)?.IsToggled??false;
                baseline.ExpectedMeta=expected;baseline.ExpectedToggle=expectedToggle;baseline.Applied=false;changed=true;}
            s.Nodes[node.Position]=node;owners[node]=s;s.Restored.Remove(node.Position);
            candidates.Add(new Consumer{Session=s,Node=(PowerConsumer)node,Poi=poi,Defense=defense,Watts=watts,RangeDistance=poi?0:distance});
        }
        var currentPositions=new HashSet<Vector3i>(candidates.OfType<Consumer>().Select(c=>c.Node.Position));
        foreach(var pair in s.Nodes.ToArray())if(!currentPositions.Contains(pair.Key))Restore(s,pair.Value);
        var plainPositions=new HashSet<Vector3i>();
        if(s.PoiControl&&s.Poi!=null)
        {
            RebirthCamperWorldLighting.MigratePoi(s.World,s.Poi);
            foreach(var light in RebirthCamperWorldLighting.Lights(s.World,s.Poi))
            {
                var position=light.ToWorldPos();
                if(s.Excluded.Contains(position)||!PlainAuthorized(s,light)||plainOwners.TryGetValue(light,out var supplier)&&!ReferenceEquals(s,supplier))continue;
                var value=s.World.GetBlock(position);
                if(s.Baselines.TryGetValue(position,out var prior)&&prior.OrdinaryStamp!=0&&prior.OrdinaryStamp!=RebirthCamperWorldLighting.Stamp(light))
                {prior.Removed=true;s.Excluded.Add(position);s.ResetPlain.Add(position);s.PoiOn=false;s.PoiControl=false;changed=true;continue;}
                if(!s.Baselines.TryGetValue(position,out var baseline)||s.Restored.Contains(position)||baseline.Removed)
                {
                    baseline=new Baseline{Position=position,Block=checked((ushort)value.type),OrdinaryStamp=RebirthCamperWorldLighting.Stamp(light),Meta=value.meta,PreviousMeta=value.meta,ExpectedMeta=value.meta};
                    s.Baselines[position]=baseline;changed=true;
                }
                if(!MatchesPlain(s,light)){s.Excluded.Add(position);plainOwners.Remove(light);continue;}
                int watts=usable&&s.PoiOn?PlainDemand(vehicle):0;
                byte expected=(byte)((baseline.Meta&~2)|(watts>0?2:0));
                if(baseline.ExpectedMeta!=expected){baseline.PreviousMeta=value.meta;baseline.ExpectedMeta=expected;baseline.Applied=false;changed=true;}
                s.PlainNodes[position]=light;plainOwners[light]=s;s.Restored.Remove(position);plainPositions.Add(position);
                candidates.Add(new PlainConsumer{Session=s,Light=light,Watts=watts,EnergyWatts=watts>0?PlainEnergyDemand(vehicle):0});
            }
        }
        foreach(var pair in s.PlainNodes.ToArray())if(!plainPositions.Contains(pair.Key))RestorePlain(s,pair.Value);
        if(changed)
        {
            s.ProposedImage=Encode(s);s.Operation=Guid.NewGuid();RebirthCamperEnergyRuntime.For(vehicle).Hold(s.Operation);
            if(!Witness(s))return Array.Empty<IRebirthCamperEnergyConsumer>();
        }
        return candidates;
    }
    private static bool CurrentPlain(Session s,TileEntityLight light)
        =>light!=null&&Current(s)&&ReferenceEquals(s.World.GetTileEntity(light.ToWorldPos()),light)&&s.World.GetBlock(light.ToWorldPos()).Block is BlockLight;
    private static bool PlainAuthorized(Session s,TileEntityLight light)
    {
        if(!CurrentPlain(s,light)||s.World.IsWithinTraderArea(light.ToWorldPos()))return false;
        var owner=s.Vehicle.GetOwner();var player=owner==null?null:GameManager.Instance.GetPersistentPlayerList()?.GetPlayerData(owner);
        if(player==null)return false;var claim=s.World.GetLandClaimOwner(light.ToWorldPos(),player);
        return claim==EnumLandClaimOwner.None||claim==EnumLandClaimOwner.Self;
    }
    private static bool MatchesPlain(Session s,TileEntityLight light)
    {
        if(!CurrentPlain(s,light)||!s.Baselines.TryGetValue(light.ToWorldPos(),out var baseline))return false;
        var value=s.World.GetBlock(light.ToWorldPos());
        return value.type==baseline.Block&&baseline.OrdinaryStamp!=0&&baseline.OrdinaryStamp==RebirthCamperWorldLighting.Stamp(light)&&(value.meta==baseline.ExpectedMeta||!baseline.Applied&&value.meta==baseline.PreviousMeta);
    }
    private static void RestorePlain(Session s,TileEntityLight light)
    {
        var position=light.ToWorldPos();
        if(!s.Baselines.TryGetValue(position,out var b)||!b.Proofed||b.Removed||s.Restored.Contains(position))return;
        if(CurrentPlain(s,light)&&PlainAuthorized(s,light)&&MatchesPlain(s,light))
        {
            var value=s.World.GetBlock(position);byte restored=(byte)((value.meta&~2)|(b.Meta&2));
            if(value.meta!=restored){value.meta=restored;s.World.SetBlockRPC(position,value);}
            b.HasWritten=false;s.Restored.Add(position);
        }
        plainOwners.Remove(light);s.PlainNodes.Remove(position);
    }
    private static bool DeviceOwnerAuthorized(Session s,PowerItem node)=>DeviceOwnerAuthorized(s.Vehicle,node);
    private static bool DeviceOwnerAuthorized(EntityVehicle vehicle,PowerItem node)
    {
        var owner=vehicle.GetOwner();if(owner==null)return false;
        if(node.TileEntity is TileEntityPoweredRangedTrap ranged)return ranged.IsOwner(owner);
        if(node.TileEntity is TileEntityPoweredMeleeTrap melee)return melee.IsOwner(owner);
        // Generic native power tile entities have no placement owner field.
        // Require the camper owner's actual native land claim; do not guess
        // ownership from mere proximity or IsPlayerPlaced.
        var player=GameManager.Instance.GetPersistentPlayerList()?.GetPlayerData(owner);
        return player!=null&&vehicle.world.GetLandClaimOwner(node.Position,player)==EnumLandClaimOwner.Self;
    }
    private static bool PoiOwnerAuthorized(EntityVehicle vehicle,PowerItem node)
    {
        if(vehicle.world.IsWithinTraderArea(node.Position))return false;
        var owner=vehicle.GetOwner();var player=owner==null?null:GameManager.Instance.GetPersistentPlayerList()?.GetPlayerData(owner);if(player==null)return false;
        var claim=vehicle.world.GetLandClaimOwner(node.Position,player);return claim==EnumLandClaimOwner.None||claim==EnumLandClaimOwner.Self;
    }
    private static bool MatchesExpected(Session s,PowerItem node)
    {
        if(!s.Baselines.TryGetValue(node.Position,out var b)||node.BlockID!=b.Block)return false;
        if(b.OrdinaryStamp!=0&&b.OrdinaryStamp!=RebirthCamperWorldLighting.Stamp(node.TileEntity))return false;
        var block=s.World.GetBlock(node.Position);bool toggle=(node as PowerConsumerToggle)?.IsToggled??false;
        return (block.meta==b.ExpectedMeta&&(!b.HasToggle||toggle==b.ExpectedToggle))||
            (!b.Applied&&block.meta==b.PreviousMeta&&(!b.HasToggle||toggle==b.PreviousToggle));
    }
    private static void Relinquish(Session s,PowerItem node)
    {ushort power=0;node.HandlePowerReceived(ref power);node.HandlePowerUpdate(false);owners.Remove(node);s.Nodes.Remove(node.Position);s.Restored.Add(node.Position);s.Excluded.Add(node.Position);}
    private static bool EnemyNear(World world,Vector3i position)
    {
        float square=DefenseRange*DefenseRange;var center=position.ToVector3Center();
        foreach(var entity in world.Entities.list)
            if(entity is EntityEnemy enemy&&!enemy.IsDead()&&!enemy.IsSleeping&&(enemy.position-center).sqrMagnitude<=square)return true;
        return false;
    }
    private static bool Inside(PrefabInstance poi,Vector3i p)
    {var a=poi.boundingBoxPosition;var b=poi.boundingBoxSize;return p.x>=a.x&&p.x<a.x+b.x&&p.y>=a.y&&p.y<a.y+b.y&&p.z>=a.z&&p.z<a.z+b.z;}
    private static PrefabInstance NearestPoi(EntityVehicle vehicle)
    {
        var prefabs=GameManager.Instance.GetDynamicPrefabDecorator()?.allPrefabs;if(prefabs==null)return null;
        PrefabInstance best=null;
        foreach(var poi in prefabs)
        {
            if(poi?.prefab==null||poi.boundingBoxSize.x<8||poi.boundingBoxSize.z<8)continue;
            var a=poi.boundingBoxPosition.ToVector3();var b=poi.boundingBoxSize.ToVector3();
            // Match the prefab's horizontal footprint, with no added altitude
            // restriction. The previous tie order for overlapping POIs remains.
            if(vehicle.position.x>=a.x&&vehicle.position.x<a.x+b.x&&
                vehicle.position.z>=a.z&&vehicle.position.z<a.z+b.z)best=poi;
        }
        return best;
    }
    public static RebirthCamperEnergyPoiSnapshot ObservePoi(EntityVehicle vehicle)
    {
        var result=new RebirthCamperEnergyPoiSnapshot{Range=PoiRange};if(!Authority(vehicle))return result;
        // Containment is cheap location information, not a light scan. Outside
        // Camp preserve unknown eligibility/load rather than claiming no lights.
        var poi=NearestPoi(vehicle);result.InRange=poi!=null;result.PoiName=poi?.prefab?.PrefabName;
        if(!RebirthCamperEnergyRuntime.TryObserveMode(vehicle,out var mode,out _,out _)||mode!=RebirthCamperEnergyMode.Camp)return result;
        result.RequiredWatts=0;
        result.IsOn=sessions.TryGetValue(vehicle,out var s)&&s.PoiOn&&poi!=null;
        if(poi!=null&&PowerManager.HasInstance)foreach(var node in PowerManager.Instance.PowerItemDictionary.Values)
            if(Eligible(node)&&ReferenceEquals(vehicle.world.GetTileEntity(node.Position),node.TileEntity)&&Light(node)&&!node.TileEntity.IsPlayerPlaced&&Inside(poi,node.Position)&&PoiOwnerAuthorized(vehicle,node))
            {
                result.SupportedLights++;
                result.RequiredWatts=checked(result.RequiredWatts+RebirthCamperEnergyEfficiency.Demand(vehicle,node.TileEntity,node.RequiredPower));
                if(s!=null&&s.PoiOn&&node.IsPowered&&owners.TryGetValue(node,out var supplier)&&ReferenceEquals(s,supplier)&&
                    s.Baselines.TryGetValue(node.Position,out var baseline)&&baseline.HasWritten)
                    result.DemandWatts=checked(result.DemandWatts+RebirthCamperEnergyEfficiency.Demand(vehicle,node.TileEntity));
            }
        if(poi!=null)foreach(var light in RebirthCamperWorldLighting.Lights(vehicle.world,poi))
        {
            var owner=vehicle.GetOwner();var player=owner==null?null:GameManager.Instance.GetPersistentPlayerList()?.GetPlayerData(owner);
            if(player==null)continue;var claim=vehicle.world.GetLandClaimOwner(light.ToWorldPos(),player);
            if(claim!=EnumLandClaimOwner.None&&claim!=EnumLandClaimOwner.Self)continue;
            result.SupportedLights++;
            result.RequiredWatts=checked(result.RequiredWatts+PlainDemand(vehicle));
            if(s!=null&&s.PoiOn&&plainOwners.TryGetValue(light,out var supplier)&&ReferenceEquals(s,supplier)&&
                s.Baselines.TryGetValue(light.ToWorldPos(),out var baseline)&&baseline.HasWritten&&(vehicle.world.GetBlock(light.ToWorldPos()).meta&2)!=0)
                result.DemandWatts=checked(result.DemandWatts+PlainDemand(vehicle));
        }
        return result;
    }
    // Bridge observation only: never For/Consumers/Witness, and never hydrates leases.
    public static Newtonsoft.Json.Linq.JObject ObserveDevices(EntityVehicle vehicle,int limit)
    {
        var rows=new Newtonsoft.Json.Linq.JArray();int total=0;
        bool authority=Authority(vehicle);sessions.TryGetValue(vehicle,out var session);
        var poi=authority?NearestPoi(vehicle):null;
        if(authority&&PowerManager.HasInstance)
            foreach(var node in PowerManager.Instance.PowerItemDictionary.Values.Where(n=>n!=null).OrderBy(n=>n.Position.x).ThenBy(n=>n.Position.y).ThenBy(n=>n.Position.z))
            {
                if(node?.TileEntity==null||!ReferenceEquals(vehicle.world.GetTileEntity(node.Position),node.TileEntity))continue;
                bool owned=owners.TryGetValue(node,out var supplier)&&ReferenceEquals(supplier,session)&&session!=null;
                float distance=RebirthCamperEnergyBounds.Distance(RebirthCamperEnergyBounds.CurrentCenter(vehicle),node.Position.ToVector3Center());
                bool inPoi=poi!=null&&Inside(poi,node.Position);
                if(!owned&&distance>RebirthCamperEnergyBoundsRangePolicy.ForVehicle(vehicle)&&!inPoi&&session?.Baselines.ContainsKey(node.Position)!=true)continue;
                total++;if(rows.Count>=limit)continue;
                var children=new Newtonsoft.Json.Linq.JArray();foreach(var child in node.Children.Take(512))children.Add(PositionJson(child.Position));
                Baseline baseline=null;if(session!=null)session.Baselines.TryGetValue(node.Position,out baseline);
                bool defense=Defense(node),enemy=defense&&EnemyNear(vehicle.world,node.Position);
                bool authorized=DeviceOwnerAuthorized(vehicle,node);
                bool poiLight=Light(node)&&!node.TileEntity.IsPlayerPlaced&&inPoi;
                rows.Add(new Newtonsoft.Json.Linq.JObject{
                    ["position"]=PositionJson(node.Position),["blockId"]=node.BlockID,["block"]=Block.list[node.BlockID].GetBlockName(),
                    ["nativeType"]=node.GetType().Name,["tileType"]=node.TileEntity.GetType().Name,
                    ["meta"]=vehicle.world.GetBlock(node.Position).meta,["currentBlockId"]=vehicle.world.GetBlock(node.Position).type,["powered"]=node.IsPowered,
                    ["active"]=node is PowerConsumer consumer?(Newtonsoft.Json.Linq.JToken)new Newtonsoft.Json.Linq.JValue(consumer.lastActivate):Newtonsoft.Json.Linq.JValue.CreateNull(),
                    ["toggle"]=node is PowerConsumerToggle toggle?(Newtonsoft.Json.Linq.JToken)new Newtonsoft.Json.Linq.JValue(toggle.IsToggled):Newtonsoft.Json.Linq.JValue.CreateNull(),
                    ["requiredPower"]=node.RequiredPower,["nativePowerUsed"]=node.TileEntity.PowerUsed,
                    ["nativeParent"]=node.Parent==null?null:PositionJson(node.Parent.Position),["nativeChildren"]=children,
                    ["nativeChildCount"]=node.Children.Count,["wiringTruncated"]=node.Children.Count>children.Count,
                    ["tileHasParent"]=node.TileEntity.HasParent(),["tileParent"]=node.TileEntity.HasParent()?PositionJson(node.TileEntity.GetParent()):null,
                    ["tileChildCount"]=node.TileEntity.ChildCount,["playerPlaced"]=node.TileEntity.IsPlayerPlaced,
                    ["supportedLeaf"]=Eligible(node),["playerDeviceProof"]=PlayerDevice(node),["ownedByCamper"]=owned,["distance"]=distance,["insidePoi"]=inPoi,
                    ["placementOwnerAuthorized"]=authorized,
                    ["admissionReason"]=!Eligible(node)?"unsupported_or_wired":!PlayerDevice(node)&&!poiLight?"no_player_placement_proof":
                        !(poiLight?PoiOwnerAuthorized(vehicle,node):authorized)?"placement_owner_not_authorized":
                        defense&&!enemy?"defense_standby_no_awake_enemy":"eligible_subject_to_camp_supply_and_lease",
                    ["defense"]=defense,["awakeEnemyInDefenseRange"]=enemy,
                    ["activeMeteredDemand"]=owned&&node.IsPowered?Math.Max(0,node.TileEntity.PowerUsed):0,
                    ["campMeteredDemand"]=owned&&node.IsPowered?RebirthCamperEnergyEfficiency.Demand(vehicle,node.TileEntity):0,
                    ["baseline"]=baseline==null?null:new Newtonsoft.Json.Linq.JObject{
                        ["blockId"]=baseline.Block,["originalMeta"]=baseline.Meta,["originalPowered"]=baseline.Powered,
                        ["originalActive"]=baseline.Active,["originalToggle"]=baseline.HasToggle?(Newtonsoft.Json.Linq.JToken)new Newtonsoft.Json.Linq.JValue(baseline.Toggle):Newtonsoft.Json.Linq.JValue.CreateNull(),
                        ["expectedMeta"]=baseline.ExpectedMeta,["expectedToggle"]=baseline.ExpectedToggle,
                        ["proofed"]=baseline.Proofed,["hasWritten"]=baseline.HasWritten,["applied"]=baseline.Applied,["removed"]=baseline.Removed,
                        ["expectedCurrentStateMatches"]=MatchesExpected(session,node)}});
            }
        var ordinaryRows=new Newtonsoft.Json.Linq.JArray();int ordinaryTotal=0;
        if(authority&&poi!=null)foreach(var light in RebirthCamperWorldLighting.Lights(vehicle.world,poi))
        {
            ordinaryTotal++;if(ordinaryRows.Count>=limit)continue;var position=light.ToWorldPos();var value=vehicle.world.GetBlock(position);
            bool owned=plainOwners.TryGetValue(light,out var supplier)&&ReferenceEquals(supplier,session)&&session!=null;
            Baseline baseline=null;if(session!=null)session.Baselines.TryGetValue(position,out baseline);
            ordinaryRows.Add(new Newtonsoft.Json.Linq.JObject{["position"]=PositionJson(position),["blockId"]=value.type,["block"]=value.Block.GetBlockName(),
                ["tileType"]=light.GetType().Name,["nativeMeta"]=value.meta,["nativeLightOn"]=(value.meta&2)!=0,["nativeRequiredPower"]=Newtonsoft.Json.Linq.JValue.CreateNull(),
                ["configuredProvisionalWatts"]=OrdinaryPoiLightWatts,["ownedByCamper"]=owned,["activeMeteredDemand"]=owned&&(value.meta&2)!=0?PlainEnergyDemand(vehicle):0,
                ["baseline"]=baseline==null?null:new Newtonsoft.Json.Linq.JObject{["originalMeta"]=baseline.Meta,["expectedMeta"]=baseline.ExpectedMeta,
                    ["proofed"]=baseline.Proofed,["hasWritten"]=baseline.HasWritten,["expectedCurrentStateMatches"]=MatchesPlain(session,light)}});
        }
        return new Newtonsoft.Json.Linq.JObject{["authoritative"]=authority,["nodes"]=rows,["total"]=total,["truncated"]=total>rows.Count,
            ["ordinaryPoiLights"]=ordinaryRows,["ordinaryPoiLightCount"]=ordinaryTotal,["ordinaryPoiLightsTruncated"]=ordinaryTotal>ordinaryRows.Count,
            ["poiLightCandidates"]=authority&&poi!=null&&RebirthCamperEnergyRuntime.TryObserveMode(vehicle,out var observedMode,out _,out _)&&observedMode==RebirthCamperEnergyMode.Camp?
                RebirthCamperWorldLighting.DescribeCandidates(vehicle.world,poi,limit):new Newtonsoft.Json.Linq.JArray(),
            ["electricalShape"]="sphere",["electricalRadius"]=RebirthCamperEnergyBoundsRangePolicy.ForVehicle(vehicle),
            ["electricalCenter"]=new Newtonsoft.Json.Linq.JObject{["x"]=vehicle.position.x,["y"]=vehicle.position.y,["z"]=vehicle.position.z},
            ["discovery"]=RebirthCamperEnergyDeviceDiscoveryObservation.Observe(vehicle),
            ["sessionExists"]=session!=null,["baselineCount"]=session?.Baselines.Count??0,
            ["leaseWitnessPending"]=session!=null&&session.Operation!=Guid.Empty,["poiRange"]=PoiRange,["defenseRange"]=DefenseRange};
    }
    private static Newtonsoft.Json.Linq.JArray PositionJson(Vector3i p)=>new Newtonsoft.Json.Linq.JArray(p.x,p.y,p.z);
    public static bool TryObservePoiEligibility(EntityVehicle vehicle,out bool eligible,out string name,out float range)
    {
        eligible=false;name=null;range=PoiRange;
        if(!ThreadManager.IsMainThread()||vehicle?.world==null||!ReferenceEquals(GameManager.Instance?.World,vehicle.world))return false;
        if(!vehicle.world.IsRemote())
        {
            var snapshot=ObservePoi(vehicle);
            bool camp=RebirthCamperEnergyRuntime.TryObserveMode(vehicle,out var mode,out _,out _)&&mode==RebirthCamperEnergyMode.Camp;
            eligible=camp&&snapshot.InRange;name=camp?snapshot.PoiName:null;range=snapshot.Range;return true;
        }
        // Only an observation cache is allocated; no energy/runtime registration.
        var item=vehicle.vehicle.GetUpdatedItemValue();string signature,identity;
        if(!item.TryGetMetadata(RebirthCamperEnergyDeviceLeaseStorage.Key,out signature)||!item.TryGetMetadata("RebirthCamperIdentity",out identity))return false;
        string gameGuid=GamePrefs.GetString(EnumGamePrefs.GameGuidClient);var cached=projections.GetValue(vehicle,_=>new PoiProjection());
        if(ReferenceEquals(cached.World,vehicle.world)&&ReferenceEquals(cached.Metadata,item.Metadata)&&cached.Signature==signature&&cached.Identity==identity&&cached.GameGuid==gameGuid)
        {eligible=ContainedProjection(vehicle,cached);name=eligible?cached.Name:null;range=cached.Range;return cached.Valid;}
        cached.World=vehicle.world;cached.Metadata=item.Metadata;cached.Signature=signature;cached.Identity=identity;cached.GameGuid=gameGuid;cached.Valid=false;cached.Eligible=false;cached.Name=null;cached.Range=PoiRange;
        try{var image=RebirthCamperEnergyDeviceLeaseStorage.Read(item);if(image.Length==0)return false;var root=ReadLease(image,true);
            if(root.Name!="energyDeviceLease"||(int)root.Attribute("version")!=1||
                !Guid.TryParse(identity,out var actual)||!Guid.TryParse((string)root.Attribute("vehicle"),out var expected)||actual!=expected||
                !Guid.TryParse(gameGuid,out var actualWorld)||!Guid.TryParse((string)root.Attribute("world"),out var expectedWorld)||actualWorld!=expectedWorld)return false;
            eligible=(bool)root.Attribute("poiEligible");name=(string)root.Attribute("poiName");range=(float)root.Attribute("poiRange");cached.Valid=Finite(range)&&range>0&&range<=200;
            cached.On=(bool?)root.Attribute("poiOn")??false;cached.Supported=(int?)root.Attribute("poiSupported")??-1;cached.Target=(string)root.Attribute("poiTarget");
            cached.Eligible=eligible;cached.Name=name;cached.Range=range;
            eligible=ContainedProjection(vehicle,cached);name=eligible?cached.Name:null;return cached.Valid;}
        catch{return false;}
    }
    private static string PoiTarget(PrefabInstance poi)
        =>poi==null?string.Empty:poi.prefab.PrefabName+"@"+poi.boundingBoxPosition.x+","+poi.boundingBoxPosition.y+","+poi.boundingBoxPosition.z+":"+poi.boundingBoxSize.x+","+poi.boundingBoxSize.y+","+poi.boundingBoxSize.z;
    private static bool ContainedProjection(EntityVehicle vehicle,PoiProjection projection)
    {
        var poi=NearestPoi(vehicle);
        return projection.Valid&&projection.Eligible&&poi!=null&&PoiTarget(poi)==projection.Target;
    }
    internal static string ObservePoiTarget(EntityVehicle vehicle)
    {
        if(!TryObservePoiOn(vehicle,out _))return null;
        return vehicle.world.IsRemote()?projections.GetValue(vehicle,_=>new PoiProjection()).Target:PoiTarget(NearestPoi(vehicle));
    }
    internal static bool TryObservePoiOn(EntityVehicle vehicle,out bool on)
    {
        on=false;
        if(!RebirthCamperEnergyRuntime.TryObserveMode(vehicle,out var mode,out _,out _)||mode!=RebirthCamperEnergyMode.Camp)return false;
        if(!vehicle.world.IsRemote())
        {
            if(!sessions.TryGetValue(vehicle,out var session)||!session.PoiEligible)return false;
            var poi=NearestPoi(vehicle);if(poi==null||PoiTarget(poi)!=PoiTarget(session.Poi))return false;
            on=session.PoiOn;return on||session.PoiSupported>0;
        }
        if(!TryObservePoiEligibility(vehicle,out bool eligible,out _,out _)||!eligible)return false;
        var cached=projections.GetValue(vehicle,_=>new PoiProjection());on=cached.On;
        return !string.IsNullOrEmpty(cached.Target)&&(on||cached.Supported>0);
    }
    public static bool SetPoiLights(EntityPlayer player,EntityVehicle vehicle,bool on)
    {
        if(!Authority(vehicle)||player==null||player.IsDead()||!ReferenceEquals(player.world,vehicle.world)||!ReferenceEquals(vehicle.world.GetEntity(player.entityId),player)||
            (player.position-vehicle.position).sqrMagnitude>64f||
            (player is EntityPlayerLocal local&&!vehicle.AllowActivationCommand("service",local))||
            RebirthCamperEnergyRuntime.For(vehicle).Snapshot().Mode!=RebirthCamperEnergyMode.Camp)return false;
        var owner=vehicle.GetOwner();var data=GameManager.Instance.GetPersistentPlayerList()?.GetPlayerDataFromEntityID(player.entityId);
        if(owner==null||data==null||!owner.Equals(data.PrimaryId)&&!owner.Equals(data.NativeId))return false;
        var snapshot=ObservePoi(vehicle);
        if(on&&(!snapshot.InRange||snapshot.SupportedLights==0||
            !CanSupplyPoiLights(RebirthCamperEnergyRuntime.For(vehicle).Snapshot(),snapshot,out _)))return false;
        var s=For(vehicle);if(!s.PoiControl||s.PoiOn!=on||s.PoiName!=snapshot.PoiName)s.Dirty=true;
        foreach(var position in s.ResetPlain)s.Excluded.Remove(position);s.ResetPlain.Clear();
        s.PoiOn=on;s.PoiControl=true;s.Poi=NearestPoi(vehicle);s.PoiName=snapshot.PoiName;s.PoiEligible=snapshot.InRange;return true;
    }
    internal static bool CanSupplyPoiLights(RebirthCamperEnergySnapshot energy,RebirthCamperEnergyPoiSnapshot poi,out int available)
    {
        available=0;
        if(energy?.Mode!=RebirthCamperEnergyMode.Camp||poi==null||!poi.InRange||poi.SupportedLights<=0||poi.RequiredWatts<0)return false;
        // CurrentPower/AvailableWatts are stored native power buffers and may
        // be zero before a fueled generator's first demand tick. Native rated
        // output of enabled, actually usable banks is the generation capacity.
        int generation=energy.Sources.Where(source=>source.IsOn&&source.Status==RebirthCamperEnergySourceStatus.Available)
            .Sum(source=>(int)source.MaxOutput);
        int otherDemand=Math.Max(0,energy.DemandWatts-poi.DemandWatts);
        available=Math.Max(0,generation-otherDemand);
        return available>0&&available>=poi.RequiredWatts;
    }
    private static void PublishPoiPowerObservation(EntityVehicle vehicle,Session session,RebirthCamperEnergySnapshot energy,RebirthCamperEnergyPoiSnapshot poi)
    {
        if(!Guid.TryParse(session.WorldId,out var worldId))return;
        string frame;
        using(var stream=new System.IO.MemoryStream())
        using(var writer=new System.IO.BinaryWriter(stream,Encoding.UTF8,true))
        {
            writer.Write((byte)1);writer.Write(worldId.ToByteArray());writer.Write(session.Identity.ToByteArray());
            writer.Write(PoiTarget(session.Poi));writer.Write(poi.RequiredWatts);writer.Write(Math.Max(0,energy.DemandWatts-poi.DemandWatts));
            writer.Flush();frame=Convert.ToBase64String(stream.ToArray());
        }
        var item=vehicle.vehicle.GetUpdatedItemValue();
        if(item.TryGetMetadata(PoiPowerObservationKey,out string previous)&&previous==frame)return;
        if(item.Metadata!=null&&!item.Metadata.ContainsKey(PoiPowerObservationKey)&&item.Metadata.Count>=byte.MaxValue)return;
        item.SetMetadata(PoiPowerObservationKey,frame);vehicle.SendSyncData(4);
    }
    internal static bool CanTurnPoiLightsOn(EntityVehicle vehicle)
    {
        if(!TryObservePoiOn(vehicle,out _))return false;
        var item=vehicle.vehicle.GetUpdatedItemValue();
        if(!item.TryGetMetadata("RebirthCamperIdentity",out string identity))return false;
        string worldId=vehicle.world.IsRemote()?GamePrefs.GetString(EnumGamePrefs.GameGuidClient):vehicle.world.worldState?.Guid;
        string target=ObservePoiTarget(vehicle);if(string.IsNullOrEmpty(target))return false;
        item.TryGetMetadata(PoiPowerObservationKey,out string observation);
        if(vehicle.world.IsRemote()&&string.IsNullOrEmpty(observation))return false;
        var gate=poiPowerGates.GetValue(vehicle,_=>new PoiPowerGate());
        if(ReferenceEquals(gate.World,vehicle.world)&&gate.GameGuid==worldId&&gate.Identity==identity&&gate.Target==target&&gate.Observation==observation&&
            UnityEngine.Time.realtimeSinceStartup<gate.Next)return gate.Value;
        gate.World=vehicle.world;gate.GameGuid=worldId;gate.Identity=identity;gate.Target=target;gate.Observation=observation;
        gate.Next=UnityEngine.Time.realtimeSinceStartup+.25f;
        gate.Value=ObservePoiPowerGate(vehicle);
        return gate.Value;
    }
    private static bool ObservePoiPowerGate(EntityVehicle vehicle)
    {
        var energy=RebirthCamperEnergyRuntime.Observe(vehicle);if(energy==null)return false;
        if(!vehicle.world.IsRemote())return CanSupplyPoiLights(energy,ObservePoi(vehicle),out _);
        // Read-only world observation, independent of an open Energy window.
        // It can disable/enable a local caption, never authorize the mutation.
        var item=vehicle.vehicle.GetUpdatedItemValue();
        if(!item.TryGetMetadata(PoiPowerObservationKey,out string frame)||string.IsNullOrEmpty(frame)||frame.Length>16384||
            !item.TryGetMetadata("RebirthCamperIdentity",out string text)||!Guid.TryParse(text,out var identity)||
            !Guid.TryParse(GamePrefs.GetString(EnumGamePrefs.GameGuidClient),out var worldId))return false;
        try
        {
            using(var stream=new System.IO.MemoryStream(Convert.FromBase64String(frame),false))
            using(var reader=new System.IO.BinaryReader(stream,Encoding.UTF8,true))
            {
                if(reader.ReadByte()!=1||new Guid(reader.ReadBytes(16))!=worldId||new Guid(reader.ReadBytes(16))!=identity)return false;
                string target=reader.ReadString();int required=reader.ReadInt32(),otherDemand=reader.ReadInt32();
                if(stream.Position!=stream.Length||target.Length>4096||required<0||otherDemand<0||target!=ObservePoiTarget(vehicle))return false;
                var cached=projections.GetValue(vehicle,_=>new PoiProjection());
                var poi=new RebirthCamperEnergyPoiSnapshot {InRange=true,SupportedLights=cached.Supported,RequiredWatts=required};
                energy.DemandWatts=otherDemand;
                return CanSupplyPoiLights(energy,poi,out _);
            }
        }
        catch(FormatException){return false;}catch(System.IO.IOException){return false;}catch(ArgumentException){return false;}
    }
    private static void Restore(Session s,PowerItem node)
    {
        if(!s.Baselines.TryGetValue(node.Position,out var b)||!b.Proofed||b.Removed||s.Restored.Contains(node.Position))return;
        if(CurrentNode(s,node)&&node.Parent==null&&node.Children.Count==0&&
            (PlayerDevice(node)?!DeviceOwnerAuthorized(s,node):!PoiOwnerAuthorized(s.Vehicle,node)))
        {Relinquish(s,node);return;}
        if(CurrentNode(s,node)&&node.BlockID==b.Block&&node.Parent==null&&node.Children.Count==0&&MatchesExpected(s,node))
        {
            if(b.HasToggle&&node is PowerConsumerToggle toggle)toggle.IsToggled=b.Toggle;
            ushort amount=b.Powered?node.RequiredPower:(ushort)0;node.HandlePowerReceived(ref amount);node.HandlePowerUpdate(b.Active);
            var current=s.World.GetBlock(node.Position);byte restored=(byte)((current.meta&~2)|(b.Meta&2));
            if(restored!=current.meta){current.meta=restored;s.World.SetBlockRPC(node.Position,current);}
            s.Restored.Add(node.Position);
            b.HasWritten=false;
        }
        owners.Remove(node);s.Nodes.Remove(node.Position);
    }
    public static void Release(EntityVehicle vehicle)
    {
        if(!ThreadManager.IsMainThread())return;
        poiPowerGates.Remove(vehicle);
        if(Authority(vehicle))
        {
            var item=vehicle.vehicle.GetUpdatedItemValue();
            if(item.Metadata?.Remove(PoiPowerObservationKey)==true)vehicle.SendSyncData(4);
        }
        if(!sessions.TryGetValue(vehicle,out var s))return;
        s.PoiOn=false;s.PoiControl=false;s.Excluded.Clear();
        if(PowerManager.HasInstance)foreach(var b in s.Baselines.Values)
            if(PowerManager.Instance.PowerItemDictionary.TryGetValue(b.Position,out var node))Restore(s,node);
        foreach(var b in s.Baselines.Values.ToArray())
            if(Block.list[b.Block] is BlockLight&&s.World.GetTileEntity(b.Position) is TileEntityLight light)RestorePlain(s,light);
        // Retain original durable baselines: vehicle save alone cannot prove that
        // the restored native world chunks have reached their own final files.
    }
    public static void Suspend(EntityVehicle vehicle)
    {
        if(!ThreadManager.IsMainThread()||!sessions.TryGetValue(vehicle,out var s))return;
        foreach(var node in s.Nodes.Values.ToArray())
            if(s.Baselines.TryGetValue(node.Position,out var baseline)&&baseline.HasWritten)Restore(s,node);
        foreach(var light in s.PlainNodes.Values.ToArray())
            if(s.Baselines.TryGetValue(light.ToWorldPos(),out var baseline)&&baseline.HasWritten)RestorePlain(s,light);
        // Recovery/custody gates stop only actual owned supplies. Requested POI
        // controls and durable original/expected records remain for exact resume.
    }
    internal static void OnUnload(TileEntityPowered tile)
    {var node=tile?.GetPowerItem();if(node!=null&&owners.TryGetValue(node,out var s))Restore(s,node);}
    internal static void OnPlainUnload(TileEntityLight light)
    {if(light!=null&&plainOwners.TryGetValue(light,out var s))RestorePlain(s,light);}
    internal static void OnPlainRemoved(WorldBase world,Vector3i position)
    {
        if(!ThreadManager.IsMainThread()||world.IsRemote())return;
        foreach(var s in sessions.Values.ToArray())
            if(ReferenceEquals(s.World,world)&&s.PlainNodes.TryGetValue(position,out var light))
            {RestorePlain(s,light);s.Excluded.Add(position);if(s.Baselines.TryGetValue(position,out var b)){b.Removed=true;s.Dirty=true;}}
    }
    internal static void OnPlainReset(WorldBase world,Vector3i position)
    {
        if(!ThreadManager.IsMainThread()||world.IsRemote())return;
        foreach(var s in sessions.Values.ToArray())
            if(ReferenceEquals(s.World,world)&&s.Baselines.TryGetValue(position,out var b)&&(Block.list[b.Block] is BlockLight||Block.list[b.Block] is BlockPoweredLight))
            {
                if(s.PlainNodes.TryGetValue(position,out var light))plainOwners.Remove(light);
                if(s.Nodes.TryGetValue(position,out var node)){owners.Remove(node);s.Nodes.Remove(position);}
                s.PlainNodes.Remove(position);s.Excluded.Add(position);s.ResetPlain.Add(position);b.Removed=true;s.Dirty=true;
                s.PoiOn=false;s.PoiControl=false;
            }
    }
    internal static void OnRemoved(WorldBase world,Vector3i position)
    {
        if(!ThreadManager.IsMainThread()||!ReferenceEquals(GameManager.Instance?.World,world)||world.IsRemote()||!PowerManager.HasInstance)return;
        if(PowerManager.Instance.PowerItemDictionary.TryGetValue(position,out var node)&&owners.TryGetValue(node,out var s))
        {Restore(s,node);s.Excluded.Add(position);if(s.Baselines.TryGetValue(position,out var b)){b.Removed=true;s.Dirty=true;}}
    }
    private static void Shutdown(ref ModEvents.SWorldShuttingDownData data)
    {foreach(var vehicle in sessions.Keys.ToArray())Release(vehicle);sessions.Clear();owners.Clear();plainOwners.Clear();projections=new ConditionalWeakTable<EntityVehicle,PoiProjection>();}
}

[HarmonyPatch(typeof(TileEntityPowered),nameof(TileEntityPowered.OnUnload))]
internal static class RebirthCamperEnergyDeviceUnload
{private static void Prefix(TileEntityPowered __instance)=>RebirthCamperEnergyDeviceAdapter.OnUnload(__instance);}

[HarmonyPatch(typeof(BlockPowered),nameof(BlockPowered.OnBlockRemoved))]
internal static class RebirthCamperEnergyDeviceRemoval
{private static void Prefix(WorldBase world,Vector3i _blockPos)=>RebirthCamperEnergyDeviceAdapter.OnRemoved(world,_blockPos);}

[HarmonyPatch(typeof(TileEntity),nameof(TileEntity.OnUnload))]
internal static class RebirthCamperEnergyOrdinaryLightUnload
{private static void Prefix(TileEntity __instance){if(__instance is TileEntityLight light)RebirthCamperEnergyDeviceAdapter.OnPlainUnload(light);}}
[HarmonyPatch(typeof(Block),nameof(Block.OnBlockRemoved))]
internal static class RebirthCamperEnergyOrdinaryLightRemoval
{private static void Prefix(Block __instance,WorldBase __0,Vector3i __2){if(__instance is BlockLight)RebirthCamperEnergyDeviceAdapter.OnPlainRemoved(__0,__2);}}

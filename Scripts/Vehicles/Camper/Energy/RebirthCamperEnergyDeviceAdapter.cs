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
    public int SelectedLights {get;internal set;}
    public int PinnedOnLights {get;internal set;}
    public int SelectableLights {get;internal set;}
    public bool IsOn {get;internal set;}
    public float Range {get;internal set;}
    public int DemandWatts {get;internal set;}
    // Prospective load is distinct from currently powered/metered demand.
    // Older observation packets leave this unknown and cannot enable ON.
    public int RequiredWatts {get;internal set;} = -1;
    public double ProspectiveRuntimeSeconds {get;internal set;} = -1;
}

// Authoritative native electrical consumers and existing circuit roots. Never
// reparent wires, simulate a source bank, replace ammunition or change targeting.
public static partial class RebirthCamperEnergyDeviceAdapter
{
    private const string PoiPowerObservationKey="RebirthCamperPoiPowerV1";
    private sealed class PoiLookupCache
    {
        internal int DiscoveryGeneration=-1;
        internal World World;internal UnityEngine.Vector3 Position;internal float Until,NodesUntil,NextMigration;
        internal PrefabInstance Poi;internal PowerItem[] LightNodes;internal Vector3i[] RankedLights,RankedPositions;internal float RankedUntil;
        internal Vector3i RankedOrigin,RankedSize;
        internal readonly List<PrefabInstance> Nearby=new List<PrefabInstance>();
    }
    private static readonly ConditionalWeakTable<EntityVehicle,PoiLookupCache> poiLookups=new ConditionalWeakTable<EntityVehicle,PoiLookupCache>();
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
    private static double PlainEnergyDemand(float saving)
    {
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
        internal readonly Dictionary<Vector3i,bool> PlainClaims=new Dictionary<Vector3i,bool>();
        internal float NextPlainClaimRefresh;
        internal bool PoiOn,PoiControl;internal int PoiLimit=int.MaxValue;internal PrefabInstance Poi;internal string PoiTargetIdentity;internal byte[] SavedImage,ProposedImage;
        internal bool PoiEligible,Dirty,WitnessWarned,DestroyedReleased;internal string PoiName;internal int PoiSupported;internal float NextPoiObservation;
        internal double LastPoiRuntime=-1;
        internal RebirthCamperSavedVehicle.Expected Witness;
        internal float NextWitnessLog,LastResaveAt,NextPoiOffReclaimLog;internal Guid LastWitnessOperation;internal string LastWitnessStage;
    }
    private sealed class PlainConsumer : IRebirthCamperEnergyConsumer,IRebirthCamperEnergyAccountingConsumer
    {
        internal Session Session;internal TileEntityLight Light;internal int Watts;internal double EnergyWatts;internal float RangeDistance;
        public double EnergyDemandWatts=>EnergyWatts;
        public string Id=>"native-light:"+Light.ToWorldPos();
        public int Priority=>1;
        public int DemandWatts=>Watts;
        public float Distance=>RangeDistance;
        public void SetSupply(EntityVehicle vehicle,int suppliedWatts)
        {
            if(!ReferenceEquals(vehicle,Session.Vehicle)||!Current(Session)||Session.Operation!=Guid.Empty||!CurrentPlain(Session,Light)||!PlainAuthorized(Session,Light))return;
            var position=Light.ToWorldPos();var baseline=Session.Baselines[position];
            if(!MatchesPlain(Session,Light)){Session.Excluded.Add(position);plainOwners.Remove(Light);return;}
            if(Watts>0&&suppliedWatts<Watts){RestorePlain(Session,Light,true);return;}
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
            if(Watts>0&&suppliedWatts<Watts){Restore(Session,Node,true);return;}
            if(Poi&&Node is PowerConsumerToggle toggle)toggle.IsToggled=Watts>0;
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
        RebirthHarmonyBootstrap.PatchClassOnce(new Harmony("rebirth.camper.energy.devices"),typeof(RebirthCamperEnergyCircuitVehicleUnload));
        RebirthHarmonyBootstrap.PatchClassOnce(new Harmony("rebirth.camper.energy.devices"),typeof(RebirthCamperEnergyDeviceRemoval));
        RebirthHarmonyBootstrap.PatchClassOnce(new Harmony("rebirth.camper.energy.devices"),typeof(RebirthCamperEnergyLightPlacement));
        RebirthHarmonyBootstrap.PatchClassOnce(new Harmony("rebirth.camper.energy.devices"),typeof(RebirthCamperEnergyDevicePlacement));
        RebirthCamperWorldLighting.Install(new Harmony("rebirth.camper.energy.world-lights"));
        RebirthCamperPoiLightInteraction.Install();
        RebirthHarmonyBootstrap.PatchClassOnce(new Harmony("rebirth.camper.energy.devices"),typeof(RebirthCamperEnergyOrdinaryLightUnload));
        RebirthHarmonyBootstrap.PatchClassOnce(new Harmony("rebirth.camper.energy.devices"),typeof(RebirthCamperEnergyOrdinaryLightRemoval));
        ModEvents.WorldShuttingDown.RegisterHandler(Shutdown);
        ModEvents.GameUpdate.RegisterHandler(AdvanceDeviceSaves);
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
        node.Children.Count==0&&!node.TileEntity.HasParent()&&node.TileEntity.ChildCount==0&&node.RequiredPower>0;
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
            s.PoiLimit=Math.Max(0,(int?)root.Attribute("poiLimit")??int.MaxValue);
            s.PoiTargetIdentity=(string)root.Attribute("poiTarget");
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
            if(magic==0x32444C45||magic==0x33444C45||magic==0x34444C45||magic==0x35444C45||magic==0x36444C45||magic==0x37444C45)
            {
                string world=reader.ReadString();var vehicle=new Guid(reader.ReadBytes(16));bool eligible=reader.ReadBoolean();float range=reader.ReadSingle();string name=reader.ReadString();
                bool on=magic>=0x33444C45&&reader.ReadBoolean(),control=magic>=0x33444C45&&reader.ReadBoolean();int supported=magic>=0x35444C45?reader.ReadInt32():-1;string target=magic>=0x35444C45?reader.ReadString():string.Empty;int limit=magic>=0x36444C45?reader.ReadInt32():int.MaxValue;int count=reader.ReadInt32();
                if(!Guid.TryParse(world,out _)||!Finite(range)||range<=0||range>200||count<0||supported< -1||limit<(magic>=0x37444C45?0:1)||(long)count*(magic>=0x34444C45?22:18)!=stream.Length-stream.Position)throw new InvalidOperationException("Malformed compact device lease.");
                var root=new XElement("energyDeviceLease",new XAttribute("version",1),new XAttribute("world",world),new XAttribute("vehicle",vehicle),new XAttribute("poiEligible",eligible),new XAttribute("poiRange",range),new XAttribute("poiName",name),new XAttribute("poiOn",on),new XAttribute("poiControl",control),new XAttribute("poiSupported",supported),new XAttribute("poiTarget",target),new XAttribute("poiLimit",limit));
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
            writer.Write(0x37444C45);writer.Write(s.WorldId);writer.Write(s.Identity.ToByteArray());writer.Write(s.PoiEligible);writer.Write(PoiRange);writer.Write(s.PoiName??"");writer.Write(s.PoiOn);writer.Write(s.PoiControl);writer.Write(s.PoiSupported);writer.Write(s.Poi!=null?PoiTarget(s.Poi):s.PoiTargetIdentity??string.Empty);writer.Write(s.PoiLimit);writer.Write(s.Baselines.Count);
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
            if(!runtime.Hold(s.Operation,false)){WitnessLog(s,"hold-refused");return false;}
            if(s.Witness==null)
            {
                if(!runtime.RequestDeviceLeasePersistence(s.Operation,s.SavedImage,s.ProposedImage,out s.Witness,out var refusal))
                {WitnessLog(s,"lease-save-request-refused",refusal);return false;}
                WitnessLog(s,"lease-save-requested");
            }
            if(!RebirthCamperSavedVehicle.Verify(s.Vehicle,s.Witness,()=>Current(s)))
            {
                // Native vehicle fields can change after the lease snapshot was
                // captured. Only retry an exact byte mismatch while the live
                // lease still matches this operation's proposed image. The new
                // full-vehicle snapshot must itself reach disk before release.
                float now=UnityEngine.Time.realtimeSinceStartup;
                if(s.Witness.LastDiagnostic?.StartsWith("bytes-differ",StringComparison.Ordinal)==true&&
                    now-s.LastResaveAt>=10f&&s.ProposedImage!=null&&
                    RebirthCamperEnergyRuntime.ReadDeviceLease(s.Vehicle).SequenceEqual(s.ProposedImage))
                {
                    var previous=s.Witness;
                    s.Witness=RebirthCamperSavedVehicle.RequestSave(s.Vehicle);
                    previous.Dispose();s.LastResaveAt=now;
                    WitnessLog(s,"native-save-rescheduled");
                }
                else WitnessLog(s,"awaiting-native-save-proof");
                return false;
            }
            WitnessLog(s,"native-save-proved");
            s.Witness.Dispose();
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
    private static void WitnessLog(Session s,string stage,string detail=null)
    {
        float now=UnityEngine.Time.realtimeSinceStartup;
        if(s.LastWitnessOperation==s.Operation&&s.LastWitnessStage==stage&&now<s.NextWitnessLog)return;
        s.LastWitnessOperation=s.Operation;s.LastWitnessStage=stage;s.NextWitnessLog=now+10f;
        Log.Out("[REBIRTH EnergyFlow] device-lease stage="+stage+" vehicle="+s.Vehicle.entityId+
            " operation="+s.Operation+" poiOn="+s.PoiOn+" baselines="+s.Baselines.Count+(detail==null?"":" reason="+detail));
    }
    private static void AdvanceDeviceSaves(ref ModEvents.SGameUpdateData data)
    {
        // Save completion must not depend on the consumer tick: that tick can
        // itself be paused by another writer waiting for this held save.
        foreach(var session in sessions.Values.ToArray())
        {
            if(session.Vehicle!=null&&session.Vehicle.IsDead()&&!session.DestroyedReleased)
            {
                session.DestroyedReleased=true;
                RebirthCamperRelayWiring.ReleaseDestroyed(session.Vehicle);
                Release(session.Vehicle);
                continue;
            }
            if(session.Operation!=Guid.Empty&&Current(session))Witness(session);
        }
    }
    internal static bool HasPendingDeviceLease(EntityVehicle vehicle)
        =>vehicle!=null&&sessions.TryGetValue(vehicle,out var session)&&session.Operation!=Guid.Empty;
    public static IEnumerable<IRebirthCamperEnergyConsumer> Consumers(EntityVehicle vehicle)
    {
        if(!Authority(vehicle))return Array.Empty<IRebirthCamperEnergyConsumer>();
        var s=For(vehicle);if(!Current(s))return Array.Empty<IRebirthCamperEnergyConsumer>();
        if(!Witness(s))return Array.Empty<IRebirthCamperEnergyConsumer>();
        if(s.Dirty)
        {s.Dirty=false;s.ProposedImage=Encode(s);s.Operation=Guid.NewGuid();if(!Witness(s))return Array.Empty<IRebirthCamperEnergyConsumer>();}
        var energy=RebirthCamperEnergyRuntime.For(vehicle).Snapshot();
        var mode=energy.Mode;
        if(mode!=RebirthCamperEnergyMode.Camp)
        {
            Release(vehicle);
            if(mode==RebirthCamperEnergyMode.Packing||mode==RebirthCamperEnergyMode.Travel)
                RebirthCamperRelayWiring.ReleaseForTravel(vehicle);
            return Array.Empty<IRebirthCamperEnergyConsumer>();
        }
        float ownerSaving=RebirthCamperEnergyEfficiency.OwnerSaving(vehicle);
        bool poiSourceUsable=energy.Sources.Any(source=>source.Kind!=RebirthCamperEnergySourceKind.Solar&&
            source.IsOn&&source.Status==RebirthCamperEnergySourceStatus.Available);
        // A temporarily empty but enabled bank may be recharged. Keep the
        // player's ON choice until they turn it off or disable every bank.
        bool poiSourceEnabled=energy.Sources.Any(source=>source.Kind!=RebirthCamperEnergySourceKind.Solar&&source.IsOn);
        if(s.PoiOn&&!poiSourceEnabled)
        {s.PoiOn=false;s.Dirty=true;Log.Out("[REBIRTH EnergyFlow] poi-auto-off vehicle="+vehicle.entityId+" reason=all-dispatchable-sources-off");}
        float range=RebirthCamperEnergyBoundsRangePolicy.ForVehicle(vehicle);
        var selectedPoi=NearestPoi(vehicle);
        string selectedTarget=PoiTarget(selectedPoi);
        if(s.Poi!=null&&!ReferenceEquals(s.Poi,selectedPoi)||s.PoiName!=null&&s.PoiName!=(selectedPoi?.prefab?.PrefabName)||
            !string.IsNullOrEmpty(s.PoiTargetIdentity)&&s.PoiTargetIdentity!=selectedTarget){s.PoiOn=false;s.PoiControl=false;}
        s.PoiTargetIdentity=selectedTarget;
        s.Poi=selectedPoi;if(s.Poi==null){s.PoiOn=false;s.PoiControl=false;}
        string poiName=s.Poi?.prefab?.PrefabName;bool changed=s.PoiEligible!=(s.Poi!=null)||s.PoiName!=poiName;
        s.PoiEligible=s.Poi!=null;s.PoiName=poiName;
        PreparePoiLights(vehicle,s.Poi);
        var selectedPoiLights=s.PoiControl&&s.Poi!=null
            ?SelectPoiLights(vehicle,s.Poi,s.PoiLimit).Selected
            :new HashSet<Vector3i>();
        // The two-minute reserve is an ON admission rule. Once switched on,
        // lights continue to spend their reserve through the five- and
        // one-minute warning points; they do not shut off at two minutes.
        if(UnityEngine.Time.realtimeSinceStartup>=s.NextPoiObservation)
        {
            s.NextPoiObservation=UnityEngine.Time.realtimeSinceStartup+1f;
            var observation=ObservePoi(vehicle,null,ownerSaving);int supported=observation.SupportedLights;
            changed|=supported!=s.PoiSupported;s.PoiSupported=supported;
            PublishPoiPowerObservation(vehicle,s,energy,observation);
            if(s.PoiOn&&observation.ProspectiveRuntimeSeconds>=0)
            {
                double seconds=observation.ProspectiveRuntimeSeconds;
                if(s.LastPoiRuntime>300&&seconds<=300||s.LastPoiRuntime>60&&seconds<=60)
                    Log.Out("[REBIRTH EnergyFlow] poi-runtime-warning vehicle="+vehicle.entityId+" remaining="+seconds.ToString("0"));
                s.LastPoiRuntime=seconds;
            }
            else s.LastPoiRuntime=-1;
        }
        var candidates=new List<IRebirthCamperEnergyConsumer>();
        bool usable=energy.Sources.Any(source=>source.IsOn&&source.Status==RebirthCamperEnergySourceStatus.Available);
        // Only explicit POI control needs global fixture discovery. Native circuits
        // resolve this camper's recorded relay endpoints in CircuitConsumers.
        var powerNodes=s.PoiControl&&s.Poi!=null&&PowerManager.HasInstance
            ?PowerManager.Instance.PowerItemDictionary.Values.ToArray():Array.Empty<PowerItem>();
        int reclaimedPoi=0;
        foreach(var node in powerNodes)
        {
            if(!Eligible(node)||!CurrentNode(s,node)||s.Excluded.Contains(node.Position)||owners.TryGetValue(node,out var owner)&&!ReferenceEquals(owner,s))continue;
            if(node.IsPowered&&!owners.ContainsKey(node))
            {
                // Never claim unrelated native power. An older bank hold may
                // have restored this exact proved original and dropped only
                // its in-memory owner. Re-admit that scoped lease to the normal
                // OFF proposal/witness path, including after save/reload.
                if(!CanReclaimRestoredPoi(s,node))continue;
                s.Restored.Add(node.Position);
                reclaimedPoi++;
            }
            bool poi=s.PoiControl&&PoiPoolLight(node)&&s.Poi!=null&&Inside(s.Poi,node.Position);
            // The POI switch claims eligible native fixtures within its POI;
            // those fixtures are not physically wired to a vehicle relay.
            // POI lighting keeps its explicit switch/restoration policy. All
            // ordinary electrical devices use the native circuit adapter below,
            // including standalone consumers; no block-class allowlist there.
            if(!poi||!PoiOwnerAuthorized(vehicle,node))continue;
            float distance=RebirthCamperEnergyBounds.Distance(RebirthCamperEnergyBounds.CurrentCenter(vehicle),node.Position.ToVector3Center());
            if(!poi&&(!RebirthCamperEnergyBounds.ContainsDistance(distance,range)||!DeviceOwnerAuthorized(s,node)))continue;
            bool defense=Defense(node);bool active=poi?poiSourceUsable&&s.PoiOn&&selectedPoiLights.Contains(node.Position):usable;
            int watts=active?RebirthCamperEnergyEfficiency.Demand(node.TileEntity,ownerSaving,poi?(int?)node.RequiredPower:null):0;
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
            bool expectedToggle=poi?active:baseline.Toggle;
            if(baseline.ExpectedMeta!=expected||baseline.ExpectedToggle!=expectedToggle)
            {baseline.PreviousMeta=s.World.GetBlock(node.Position).meta;baseline.PreviousToggle=(node as PowerConsumerToggle)?.IsToggled??false;
                baseline.ExpectedMeta=expected;baseline.ExpectedToggle=expectedToggle;baseline.Applied=false;changed=true;}
            s.Nodes[node.Position]=node;owners[node]=s;s.Restored.Remove(node.Position);
            candidates.Add(new Consumer{Session=s,Node=(PowerConsumer)node,Poi=poi,Defense=defense,Watts=watts,RangeDistance=poi?0:distance});
        }
        if(reclaimedPoi>0&&UnityEngine.Time.realtimeSinceStartup>=s.NextPoiOffReclaimLog)
        {
            s.NextPoiOffReclaimLog=UnityEngine.Time.realtimeSinceStartup+10f;
            Log.Out("[REBIRTH EnergyFlow] bank-poi-off-reclaim vehicle="+vehicle.entityId+" count="+reclaimedPoi+" requestedOn="+s.PoiOn);
        }
        var currentPositions=new HashSet<Vector3i>(candidates.OfType<Consumer>().Select(c=>c.Node.Position));
        foreach(var pair in s.Nodes.ToArray())if(!currentPositions.Contains(pair.Key))Restore(s,pair.Value);
        var plainPositions=new HashSet<Vector3i>();
        {
            if(s.PoiControl&&s.Poi!=null)RebirthCamperWorldLighting.MigratePoi(s.World,s.Poi);
            var center=RebirthCamperEnergyBounds.CurrentCenter(vehicle);
            var nearby=RebirthCamperWorldLighting.PlacedLights(s.World,center,range,light=>PlainAuthorized(s,light),position=>PlainAuthorized(s,position));
            var poiLights=s.PoiControl&&s.Poi!=null?RebirthCamperWorldLighting.Lights(s.World,s.Poi):Array.Empty<TileEntityLight>();
            var poiLightSet=new HashSet<TileEntityLight>(poiLights);
            foreach(var light in nearby.Concat(poiLights).Distinct())
            {
                var position=light.ToWorldPos();
                if(s.Excluded.Contains(position)||!PlainAuthorized(s,light)||plainOwners.TryGetValue(light,out var supplier)&&!ReferenceEquals(s,supplier))continue;
                // Player-placed lights still require an explicit relay link;
                // native POI lights are selected by the POI switch and claim.
                if(!poiLightSet.Contains(light)&&!RebirthCamperRelayWiring.Connected(vehicle,light))continue;
                var value=s.World.GetBlock(position);
                if(s.Baselines.TryGetValue(position,out var prior)&&prior.OrdinaryStamp!=0&&prior.OrdinaryStamp!=RebirthCamperWorldLighting.Stamp(light))
                {prior.Removed=true;s.Excluded.Add(position);s.ResetPlain.Add(position);s.PoiOn=false;s.PoiControl=false;changed=true;continue;}
                if(!s.Baselines.TryGetValue(position,out var baseline)||s.Restored.Contains(position)||baseline.Removed)
                {
                    baseline=new Baseline{Position=position,Block=checked((ushort)value.type),OrdinaryStamp=RebirthCamperWorldLighting.Stamp(light),Meta=value.meta,PreviousMeta=value.meta,ExpectedMeta=value.meta};
                    s.Baselines[position]=baseline;changed=true;
                }
                if(!MatchesPlain(s,light)){s.Excluded.Add(position);plainOwners.Remove(light);continue;}
                bool placed=RebirthCamperEnergyBounds.ContainsDistance(RebirthCamperEnergyBounds.Distance(center,position.ToVector3Center()),range);
                int watts=poiLightSet.Contains(light)
                    ?poiSourceUsable&&s.PoiOn&&selectedPoiLights.Contains(position)?PlainDemand(vehicle):0
                    :usable&&placed?PlainDemand(vehicle):0;
                byte expected=(byte)((baseline.Meta&~2)|(watts>0?2:0));
                if(baseline.ExpectedMeta!=expected){baseline.PreviousMeta=value.meta;baseline.ExpectedMeta=expected;baseline.Applied=false;changed=true;}
                s.PlainNodes[position]=light;plainOwners[light]=s;s.Restored.Remove(position);plainPositions.Add(position);
                candidates.Add(new PlainConsumer{Session=s,Light=light,Watts=watts,EnergyWatts=watts>0?PlainEnergyDemand(ownerSaving):0,RangeDistance=placed?RebirthCamperEnergyBounds.Distance(center,position.ToVector3Center()):0});
            }
        }
        foreach(var pair in s.PlainNodes.ToArray())if(!plainPositions.Contains(pair.Key))RestorePlain(s,pair.Value);
        if(changed)
        {
            if(s.PoiControl)Log.Out("[REBIRTH EnergyFlow] poi-lease-candidates vehicle="+vehicle.entityId+
                " poi="+s.PoiName+" requestedOn="+s.PoiOn+" supported="+s.PoiSupported+
                " candidates="+candidates.Count+" baselines="+s.Baselines.Count+" excluded="+s.Excluded.Count);
            s.ProposedImage=Encode(s);s.Operation=Guid.NewGuid();RebirthCamperEnergyRuntime.For(vehicle).Hold(s.Operation,false);
            if(!Witness(s))return Array.Empty<IRebirthCamperEnergyConsumer>();
        }
        candidates.AddRange(CircuitConsumers(s,range,usable,ownerSaving));
        return candidates;
    }
    private static bool CurrentPlain(Session s,TileEntityLight light)
        =>light!=null&&Current(s)&&ReferenceEquals(s.World.GetTileEntity(light.ToWorldPos()),light)&&s.World.GetBlock(light.ToWorldPos()).Block is BlockLight;
    private static bool PlainAuthorized(Session s,TileEntityLight light)
    {
        return CurrentPlain(s,light)&&PlainAuthorized(s,light.ToWorldPos());
    }
    private static bool PlainAuthorized(Session s,Vector3i position)
    {
        if(!Current(s)||s.World.IsWithinTraderArea(position))return false;
        float now=UnityEngine.Time.realtimeSinceStartup;
        if(now>=s.NextPlainClaimRefresh){s.PlainClaims.Clear();s.NextPlainClaimRefresh=now+1f;}
        if(s.PlainClaims.TryGetValue(position,out var allowed))return allowed;
        var owner=s.Vehicle.GetOwner();var player=owner==null?null:GameManager.Instance.GetPersistentPlayerList()?.GetPlayerData(owner);
        if(owner!=null&&player==null)return false;
        var claim=s.World.GetLandClaimOwner(position,player);
        allowed=claim==EnumLandClaimOwner.None||claim==EnumLandClaimOwner.Self;
        s.PlainClaims[position]=allowed;return allowed;
    }
    private static bool MatchesPlain(Session s,TileEntityLight light)
    {
        if(!CurrentPlain(s,light)||!s.Baselines.TryGetValue(light.ToWorldPos(),out var baseline))return false;
        var value=s.World.GetBlock(light.ToWorldPos());
        return value.type==baseline.Block&&baseline.OrdinaryStamp!=0&&baseline.OrdinaryStamp==RebirthCamperWorldLighting.Stamp(light)&&(value.meta==baseline.ExpectedMeta||!baseline.Applied&&value.meta==baseline.PreviousMeta);
    }
    private static void RestorePlain(Session s,TileEntityLight light,bool retainPoiOff=false)
    {
        var position=light.ToWorldPos();
        if(!s.Baselines.TryGetValue(position,out var b)||!b.Proofed||b.Removed||s.Restored.Contains(position))return;
        if(plainOwners.TryGetValue(light,out var supplier)&&!ReferenceEquals(supplier,s))
        {s.PlainNodes.Remove(position);s.Restored.Add(position);b.HasWritten=false;return;}
        // Keep an admitted OFF request's exact current claim while custody/save
        // proof is pending. It may still be ON until the OFF image is proved;
        // replaying the original here would lose the claim before OFF dispatch.
        // Release/removal use retainPoiOff=false and retain normal cleanup.
        if(retainPoiOff&&KeepPoiOff(s,position,b)&&CurrentPlain(s,light)&&PlainAuthorized(s,light)&&
            ReferenceEquals(supplier,s)&&MatchesPlain(s,light))return;
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
        var owner=vehicle.GetOwner();
        if(node.TileEntity is TileEntityPoweredRangedTrap ranged)
            return owner!=null?ranged.IsOwner(owner):ranged.GetOwner()!=null&&vehicle.world.GetLandClaimOwner(node.Position,null)==EnumLandClaimOwner.None;
        if(node.TileEntity is TileEntityPoweredMeleeTrap melee)
            return owner!=null?melee.IsOwner(owner):melee.GetOwner()!=null&&vehicle.world.GetLandClaimOwner(node.Position,null)==EnumLandClaimOwner.None;
        // Generic electrical devices follow native public/unclaimed access.
        // A different player's claim remains excluded; traps retain native ownership.
        var player=GameManager.Instance.GetPersistentPlayerList()?.GetPlayerData(owner);
        if(owner!=null&&player==null)return false;
        var claim=vehicle.world.GetLandClaimOwner(node.Position,player);
        return claim==EnumLandClaimOwner.Self||claim==EnumLandClaimOwner.None;
    }
    private static bool PoiOwnerAuthorized(EntityVehicle vehicle,PowerItem node)
    {
        if(vehicle.world.IsWithinTraderArea(node.Position))return false;
        var owner=vehicle.GetOwner();var player=owner==null?null:GameManager.Instance.GetPersistentPlayerList()?.GetPlayerData(owner);if(owner!=null&&player==null)return false;
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
        var cache=poiLookups.GetValue(vehicle,_=>new PoiLookupCache());
        float now=UnityEngine.Time.realtimeSinceStartup;
        if(ReferenceEquals(cache.World,vehicle.world)&&cache.Position==vehicle.position&&now<cache.Until)return cache.Poi;
        if(GameManager.Instance.GetDynamicPrefabDecorator()?.allPrefabs==null)return null;
        var best=FindPoiAt(vehicle.position,cache.Nearby);
        bool changedWorld=!ReferenceEquals(cache.World,vehicle.world);
        cache.World=vehicle.world;cache.Position=vehicle.position;cache.Until=now+.25f;
        if(changedWorld||!ReferenceEquals(cache.Poi,best)){cache.LightNodes=null;cache.RankedLights=null;cache.NextMigration=0;}
        cache.Poi=best;return best;
    }
    internal static PrefabInstance FindPoiAt(UnityEngine.Vector3 position,List<PrefabInstance> nearby)
    {
        var decorator=GameManager.Instance.GetDynamicPrefabDecorator();
        if(decorator?.allPrefabs==null)return null;
        nearby.Clear();
        int x=UnityEngine.Mathf.FloorToInt(position.x),z=UnityEngine.Mathf.FloorToInt(position.z);
        decorator.GetPrefabsAtXZ(x,x,z,z,nearby);
        PrefabInstance best=null;int matches=0;
        foreach(var poi in nearby)if(ContainsPoi(poi,position)){best=poi;matches++;}
        // Native spatial order differs from allPrefabs. Only ambiguous overlaps
        // need the original last-match traversal; ordinary lookups stay spatial.
        if(matches>1)foreach(var poi in decorator.allPrefabs)if(ContainsPoi(poi,position))best=poi;
        return best;
    }
    private static bool ContainsPoi(PrefabInstance poi,UnityEngine.Vector3 position)
    {
        if(poi?.prefab==null||poi.boundingBoxSize.x<8||poi.boundingBoxSize.z<8)return false;
        var a=poi.boundingBoxPosition;var b=poi.boundingBoxSize;
        return position.x>=a.x&&position.x<a.x+b.x&&position.z>=a.z&&position.z<a.z+b.z;
    }
    private static PowerItem[] PoiLightNodes(EntityVehicle vehicle,PrefabInstance poi)
    {
        if(poi==null||!PowerManager.HasInstance)return Array.Empty<PowerItem>();
        var cache=poiLookups.GetValue(vehicle,_=>new PoiLookupCache());
        float now=UnityEngine.Time.realtimeSinceStartup;
        if(ReferenceEquals(cache.World,vehicle.world)&&ReferenceEquals(cache.Poi,poi)&&cache.LightNodes!=null&&now<cache.NodesUntil)
            return cache.LightNodes;
        cache.World=vehicle.world;cache.Poi=poi;cache.NodesUntil=now+.5f;
        return cache.LightNodes=PowerManager.Instance.PowerItemDictionary.Values
            .Where(node=>node?.TileEntity!=null&&PoiPoolLight(node)&&Inside(poi,node.Position)).ToArray();
    }
    private static Vector3i[] RankedPoiLights(EntityVehicle vehicle,PrefabInstance poi)
    {
        if(poi==null)return Array.Empty<Vector3i>();
        bool measure=RebirthGameBridgeFrames.IsRecording;
        long started=measure?System.Diagnostics.Stopwatch.GetTimestamp():0;
        var cache=poiLookups.GetValue(vehicle,_=>new PoiLookupCache());
        float now=UnityEngine.Time.realtimeSinceStartup;
        bool sameContext=ReferenceEquals(cache.World,vehicle.world)&&ReferenceEquals(cache.Poi,poi);
        bool sameBounds=cache.RankedOrigin==poi.boundingBoxPosition&&cache.RankedSize==poi.boundingBoxSize;
        int generation=RebirthCamperWorldLighting.DiscoveryGeneration;
        bool sameGeneration=cache.DiscoveryGeneration==generation;
        if(!sameGeneration){cache.LightNodes=null;cache.NodesUntil=0;cache.DiscoveryGeneration=generation;}
        if(sameContext&&sameBounds&&sameGeneration&&cache.RankedLights!=null&&now<cache.RankedUntil)
            return cache.RankedLights;
        var poweredLights=PoiLightNodes(vehicle,poi);
        long afterNodes=measure?System.Diagnostics.Stopwatch.GetTimestamp():0;
        var owner=vehicle.GetOwner();var player=owner==null?null:GameManager.Instance.GetPersistentPlayerList()?.GetPlayerData(owner);
        bool hasSession=sessions.TryGetValue(vehicle,out var session)&&Current(session);
        var worldLights=player==null?Array.Empty<TileEntityLight>():RebirthCamperWorldLighting.Lights(vehicle.world,poi).ToArray();
        long afterWorldLights=measure?System.Diagnostics.Stopwatch.GetTimestamp():0;
        var positions=new HashSet<Vector3i>(poweredLights.Length+worldLights.Length);
        foreach(var node in poweredLights)
            if(Eligible(node)&&ReferenceEquals(vehicle.world.GetTileEntity(node.Position),node.TileEntity)&&PoiOwnerAuthorized(vehicle,node))
                positions.Add(node.Position);
        if(player!=null)foreach(var light in worldLights)
        {
            var position=light.ToWorldPos();
            bool authorized;
            if(hasSession)authorized=PlainAuthorized(session,position);
            else
            {
                var claim=vehicle.world.GetLandClaimOwner(position,player);
                authorized=claim==EnumLandClaimOwner.None||claim==EnumLandClaimOwner.Self;
            }
            if(authorized)positions.Add(position);
        }
        long afterEligibility=measure?System.Diagnostics.Stopwatch.GetTimestamp():0;
        var remaining=positions.OrderBy(p=>p.x).ThenBy(p=>p.y).ThenBy(p=>p.z).ToList();
        // Revalidate live eligibility above at the existing cadence. The spatial
        // spread depends only on this exact position set and the POI bounds.
        if(sameContext&&sameBounds&&cache.RankedLights!=null&&cache.RankedPositions!=null&&remaining.SequenceEqual(cache.RankedPositions))
        {cache.RankedUntil=now+.5f;return cache.RankedLights;}
        var rankedPositions=remaining.ToArray();
        var ranked=RankPoiLightPositions(remaining);
        if(measure)
        {
            long end=System.Diagnostics.Stopwatch.GetTimestamp();double ms=1000.0/System.Diagnostics.Stopwatch.Frequency;
            if((end-started)*ms>=2)
                Log.Out("[REBIRTH RankPerf] frame="+UnityEngine.Time.frameCount+
                    " totalMs="+((end-started)*ms).ToString("F2")+
                    " nodesMs="+((afterNodes-started)*ms).ToString("F2")+
                    " worldLightsMs="+((afterWorldLights-afterNodes)*ms).ToString("F2")+
                    " eligibilityMs="+((afterEligibility-afterWorldLights)*ms).ToString("F2")+
                    " spreadMs="+((end-afterEligibility)*ms).ToString("F2")+
                    " powered="+poweredLights.Length+" plain="+worldLights.Length);
        }
        cache.RankedPositions=rankedPositions;cache.RankedOrigin=poi.boundingBoxPosition;cache.RankedSize=poi.boundingBoxSize;
        cache.RankedUntil=now+.5f;return cache.RankedLights=ranked;
    }
    // BEGIN PURE POI SPREAD: source-extracted by the disposable ranking fixture.
    internal static Vector3i[] RankPoiLightPositions(List<Vector3i> sortedPositions)
    {
        if(sortedPositions.Count==0)return Array.Empty<Vector3i>();
        // Equal normalized coverage across occupied width, floors and depth. A
        // four-block minimum prevents slight fixture-height offsets (or a narrow
        // corridor) from receiving the same weight as a substantial full axis.
        // Use occupied bounds rather than prefab basements/empty decorative space.
        var remaining=new List<Vector3i>(sortedPositions);
        double minX=remaining[0].x,maxX=minX,minY=remaining[0].y,maxY=minY,minZ=remaining[0].z,maxZ=minZ;
        foreach(var p in remaining)
        {minX=Math.Min(minX,p.x);maxX=Math.Max(maxX,p.x);minY=Math.Min(minY,p.y);maxY=Math.Max(maxY,p.y);minZ=Math.Min(minZ,p.z);maxZ=Math.Max(maxZ,p.z);}
        double scaleX=Math.Max(4d,maxX-minX),scaleY=Math.Max(4d,maxY-minY),scaleZ=Math.Max(4d,maxZ-minZ);
        double centerX=(minX+maxX)*.5,centerY=(minY+maxY)*.5,centerZ=(minZ+maxZ)*.5;
        var ranked=new List<Vector3i>(remaining.Count);
        var nearestDistance=new List<double>(remaining.Count);
        for(int i=0;i<remaining.Count;i++)nearestDistance.Add(double.MaxValue);
        // Farthest-point ordering gives nested prefixes: reducing the count only
        // removes the tail. Input is x/y/z sorted; strict comparisons preserve
        // that deterministic tie-break, independent of discovery order.
        while(remaining.Count>0)
        {
            int best=0;double bestDistance=ranked.Count==0?double.MaxValue:-1;
            var previous=ranked.Count==0?default(Vector3i):ranked[ranked.Count-1];
            for(int i=0;i<remaining.Count;i++)
            {
                var p=remaining[i];double distance;
                if(ranked.Count==0)
                {
                    double dx=(p.x-centerX)/scaleX,dy=(p.y-centerY)/scaleY,dz=(p.z-centerZ)/scaleZ;
                    distance=dx*dx+dy*dy+dz*dz;
                    if(distance<bestDistance){bestDistance=distance;best=i;}
                }
                else
                {
                    double dx=((double)p.x-previous.x)/scaleX,dy=((double)p.y-previous.y)/scaleY,dz=((double)p.z-previous.z)/scaleZ;
                    distance=nearestDistance[i]=Math.Min(nearestDistance[i],dx*dx+dy*dy+dz*dz);
                    if(distance>bestDistance){bestDistance=distance;best=i;}
                }
            }
            ranked.Add(remaining[best]);remaining.RemoveAt(best);nearestDistance.RemoveAt(best);
        }
        return ranked.ToArray();
    }
    // END PURE POI SPREAD
    public static RebirthCamperEnergyPoiSnapshot ObservePoi(EntityVehicle vehicle,int? requestedLimit=null)
        =>ObservePoi(vehicle,requestedLimit,RebirthCamperEnergyEfficiency.OwnerSaving(vehicle));
    private static RebirthCamperEnergyPoiSnapshot ObservePoi(EntityVehicle vehicle,int? requestedLimit,float ownerSaving)
    {
        bool measure=RebirthGameBridgeFrames.IsRecording;
        long started=measure?System.Diagnostics.Stopwatch.GetTimestamp():0;
        var result=new RebirthCamperEnergyPoiSnapshot{Range=PoiRange};if(!Authority(vehicle))return result;
        // Containment is cheap location information, not a light scan. Outside
        // Camp preserve unknown eligibility/load rather than claiming no lights.
        var poi=NearestPoi(vehicle);result.InRange=poi!=null;result.PoiName=poi?.prefab?.PrefabName;
        if(!RebirthCamperEnergyRuntime.TryObserveMode(vehicle,out var mode,out _,out _)||mode!=RebirthCamperEnergyMode.Camp)return result;
        PreparePoiLights(vehicle,poi);
        long afterMigration=measure?System.Diagnostics.Stopwatch.GetTimestamp():0;
        double requiredEnergy=0,currentEnergy=0;
        result.RequiredWatts=0;
        result.IsOn=sessions.TryGetValue(vehicle,out var s)&&s.PoiOn&&poi!=null&&
            (s.Poi==null||ReferenceEquals(s.Poi,poi))&&
            (string.IsNullOrEmpty(s.PoiTargetIdentity)||s.PoiTargetIdentity==PoiTarget(poi));
        var plan=SelectPoiLights(vehicle,poi,requestedLimit??s?.PoiLimit??int.MaxValue);
        var selected=plan.Selected;result.PinnedOnLights=plan.PinnedOnLights;result.SelectableLights=plan.SelectableLights;
        long afterRank=measure?System.Diagnostics.Stopwatch.GetTimestamp():0;
        result.SelectedLights=selected.Count;
        if(poi!=null)foreach(var node in PoiLightNodes(vehicle,poi))
            if(Eligible(node)&&ReferenceEquals(vehicle.world.GetTileEntity(node.Position),node.TileEntity)&&PoiOwnerAuthorized(vehicle,node))
            {
                result.SupportedLights++;
                if(!selected.Contains(node.Position))continue;
                int lightWatts=RebirthCamperEnergyEfficiency.Demand(node.TileEntity,ownerSaving,node.RequiredPower);
                result.RequiredWatts=checked(result.RequiredWatts+lightWatts);requiredEnergy+=lightWatts;
                if(s!=null&&s.PoiOn&&node.IsPowered&&owners.TryGetValue(node,out var supplier)&&ReferenceEquals(s,supplier)&&
                    s.Baselines.TryGetValue(node.Position,out var baseline)&&baseline.HasWritten)
                {int activeWatts=RebirthCamperEnergyEfficiency.Demand(node.TileEntity,ownerSaving);
                    result.DemandWatts=checked(result.DemandWatts+activeWatts);currentEnergy+=activeWatts;}
            }
        long afterNative=measure?System.Diagnostics.Stopwatch.GetTimestamp():0;
        if(poi!=null)foreach(var light in RebirthCamperWorldLighting.Lights(vehicle.world,poi))
        {
            if(s!=null){if(!PlainAuthorized(s,light.ToWorldPos()))continue;}
            else
            {
                var owner=vehicle.GetOwner();var player=owner==null?null:GameManager.Instance.GetPersistentPlayerList()?.GetPlayerData(owner);
                if(player==null)continue;var claim=vehicle.world.GetLandClaimOwner(light.ToWorldPos(),player);
                if(claim!=EnumLandClaimOwner.None&&claim!=EnumLandClaimOwner.Self)continue;
            }
            result.SupportedLights++;
            if(!selected.Contains(light.ToWorldPos()))continue;
            result.RequiredWatts=checked(result.RequiredWatts+PlainDemand(vehicle));
            requiredEnergy+=PlainEnergyDemand(ownerSaving);
            if(s!=null&&s.PoiOn&&plainOwners.TryGetValue(light,out var supplier)&&ReferenceEquals(s,supplier)&&
                s.Baselines.TryGetValue(light.ToWorldPos(),out var baseline)&&baseline.HasWritten&&(vehicle.world.GetBlock(light.ToWorldPos()).meta&2)!=0)
            {result.DemandWatts=checked(result.DemandWatts+PlainDemand(vehicle));currentEnergy+=PlainEnergyDemand(ownerSaving);}
        }
        long afterPlain=measure?System.Diagnostics.Stopwatch.GetTimestamp():0;
        if(poi!=null&&result.SelectedLights>0&&!vehicle.world.IsRemote())
            result.ProspectiveRuntimeSeconds=RebirthCamperEnergyRuntime.For(vehicle)
                .EstimatePoiRuntimeSeconds(currentEnergy,requiredEnergy);
        if(measure)
        {
            long end=System.Diagnostics.Stopwatch.GetTimestamp();
            double ms=1000.0/System.Diagnostics.Stopwatch.Frequency;
            if((end-started)*ms>=2)
                Log.Out("[REBIRTH PoiPerf] frame="+UnityEngine.Time.frameCount+
                    " totalMs="+((end-started)*ms).ToString("F2")+
                    " migrationMs="+((afterMigration-started)*ms).ToString("F2")+
                    " rankMs="+((afterRank-afterMigration)*ms).ToString("F2")+
                    " nativeMs="+((afterNative-afterRank)*ms).ToString("F2")+
                    " plainMs="+((afterPlain-afterNative)*ms).ToString("F2")+
                    " estimateMs="+((end-afterPlain)*ms).ToString("F2"));
        }
        return result;
    }
    // Bridge observation only: never For/Consumers/Witness, and never hydrates leases.
    public static Newtonsoft.Json.Linq.JObject ObserveDevices(EntityVehicle vehicle,int limit)
    {
        var rows=new Newtonsoft.Json.Linq.JArray();int total=0;
        bool authority=Authority(vehicle);sessions.TryGetValue(vehicle,out var session);
        float ownerSaving=authority?RebirthCamperEnergyEfficiency.OwnerSaving(vehicle):0f;
        var poi=authority?NearestPoi(vehicle):null;
        if(authority&&PowerManager.HasInstance)
            foreach(var node in PowerManager.Instance.PowerItemDictionary.Values.Where(n=>n!=null).OrderBy(n=>n.Position.x).ThenBy(n=>n.Position.y).ThenBy(n=>n.Position.z))
            {
                if(node?.TileEntity==null||!ReferenceEquals(vehicle.world.GetTileEntity(node.Position),node.TileEntity))continue;
                bool circuit=circuits.Values.Any(c=>ReferenceEquals(c.Session,session)&&c.Nodes.Contains(node));
                bool owned=circuit||owners.TryGetValue(node,out var supplier)&&ReferenceEquals(supplier,session)&&session!=null;
                float distance=RebirthCamperEnergyBounds.Distance(RebirthCamperEnergyBounds.CurrentCenter(vehicle),node.Position.ToVector3Center());
                bool inPoi=poi!=null&&Inside(poi,node.Position);
                if(!owned&&distance>RebirthCamperEnergyBoundsRangePolicy.ForVehicle(vehicle)&&!inPoi&&session?.Baselines.ContainsKey(node.Position)!=true)continue;
                total++;if(rows.Count>=limit)continue;
                var children=new Newtonsoft.Json.Linq.JArray();foreach(var child in node.Children.Take(512))children.Add(PositionJson(child.Position));
                Baseline baseline=null;if(session!=null)session.Baselines.TryGetValue(node.Position,out baseline);
                bool defense=Defense(node),enemy=defense&&EnemyNear(vehicle.world,node.Position);
                bool authorized=DeviceOwnerAuthorized(vehicle,node);
                bool poiLight=PoiPoolLight(node)&&inPoi;
                rows.Add(new Newtonsoft.Json.Linq.JObject{
                    ["position"]=PositionJson(node.Position),["blockId"]=node.BlockID,["block"]=Block.list[node.BlockID].GetBlockName(),
                    ["nativeType"]=node.GetType().Name,["tileType"]=node.TileEntity.GetType().Name,
                    ["poiLight"]=poiLight,["manualChoice"]=RebirthCamperPoiLightChoices.Read(vehicle.world,node.TileEntity).ToString(),
                    ["meta"]=vehicle.world.GetBlock(node.Position).meta,["currentBlockId"]=vehicle.world.GetBlock(node.Position).type,["powered"]=node.IsPowered,
                    ["active"]=node is PowerConsumer consumer?(Newtonsoft.Json.Linq.JToken)new Newtonsoft.Json.Linq.JValue(consumer.lastActivate):Newtonsoft.Json.Linq.JValue.CreateNull(),
                    ["toggle"]=node is PowerConsumerToggle toggle?(Newtonsoft.Json.Linq.JToken)new Newtonsoft.Json.Linq.JValue(toggle.IsToggled):Newtonsoft.Json.Linq.JValue.CreateNull(),
                    ["requiredPower"]=node.RequiredPower,["nativePowerUsed"]=node.TileEntity.PowerUsed,
                    ["nativeParent"]=node.Parent==null?null:PositionJson(node.Parent.Position),["nativeChildren"]=children,
                    ["nativeChildCount"]=node.Children.Count,["wiringTruncated"]=node.Children.Count>children.Count,
                    ["tileHasParent"]=node.TileEntity.HasParent(),["tileParent"]=node.TileEntity.HasParent()?PositionJson(node.TileEntity.GetParent()):null,
                    ["tileChildCount"]=node.TileEntity.ChildCount,["playerPlaced"]=node.TileEntity.IsPlayerPlaced,
                    ["supportedLeaf"]=Eligible(node),["nativeCircuit"]=circuit,["playerDeviceProof"]=PlayerDevice(node),["ownedByCamper"]=owned,["distance"]=distance,["insidePoi"]=inPoi,
                    ["placementOwnerAuthorized"]=authorized,
                    ["admissionReason"]=circuit?"native_circuit":node is PowerSource?"native_power_source":node.Parent!=null?"wired_branch":!Eligible(node)?"native_circuit_pending_supply":!(poiLight?PoiOwnerAuthorized(vehicle,node):authorized)?"placement_owner_not_authorized":
                        defense&&!enemy?"defense_standby_no_awake_enemy":"eligible_subject_to_camp_supply_and_lease",
                    ["defense"]=defense,["awakeEnemyInDefenseRange"]=enemy,
                    ["activeMeteredDemand"]=owned&&node.IsPowered?Math.Max(0,node.TileEntity.PowerUsed):0,
                    ["campMeteredDemand"]=owned&&node.IsPowered?RebirthCamperEnergyEfficiency.Demand(node.TileEntity,ownerSaving):0,
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
                ["manualChoice"]=RebirthCamperPoiLightChoices.Read(vehicle.world,light).ToString(),
                ["configuredProvisionalWatts"]=OrdinaryPoiLightWatts,["ownedByCamper"]=owned,["activeMeteredDemand"]=owned&&(value.meta&2)!=0?PlainEnergyDemand(ownerSaving):0,
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
            // The HUD needs location eligibility only. Rich light preparation, claims,
            // selection and runtime estimates remain in Consumers/control observations.
            var poi=Authority(vehicle)?NearestPoi(vehicle):null;
            bool camp=RebirthCamperEnergyRuntime.TryObserveMode(vehicle,out var mode,out _,out _)&&mode==RebirthCamperEnergyMode.Camp;
            eligible=camp&&poi!=null;name=camp?poi?.prefab?.PrefabName:null;return true;
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
            RebirthCamperEnergyRuntime.For(vehicle).Snapshot().Mode!=RebirthCamperEnergyMode.Camp)
        {Log.Out("[REBIRTH EnergyFlow] poi-refused vehicle="+vehicle?.entityId+" on="+on+" reason=actor-or-camp-admission");return false;}
        var owner=vehicle.GetOwner();var data=GameManager.Instance.GetPersistentPlayerList()?.GetPlayerDataFromEntityID(player.entityId);
        if(owner==null||data==null||!owner.Equals(data.PrimaryId)&&!owner.Equals(data.NativeId))
        {Log.Out("[REBIRTH EnergyFlow] poi-refused vehicle="+vehicle.entityId+" on="+on+" reason=owner-admission");return false;}
        var snapshot=ObservePoi(vehicle);
        if(on&&(!snapshot.InRange||snapshot.SupportedLights==0||
            !CanSupplyPoiLights(RebirthCamperEnergyRuntime.For(vehicle).Snapshot(),snapshot,out _)))
        {Log.Out("[REBIRTH EnergyFlow] poi-refused vehicle="+vehicle.entityId+" on=true reason=target-or-dispatchable-source inRange="+snapshot.InRange+
            " supported="+snapshot.SupportedLights+" requiredWatts="+snapshot.RequiredWatts);return false;}
        var s=For(vehicle);if(!s.PoiControl||s.PoiOn!=on||s.PoiName!=snapshot.PoiName)s.Dirty=true;
        foreach(var position in s.ResetPlain)s.Excluded.Remove(position);s.ResetPlain.Clear();
        s.PoiOn=on;s.PoiControl=true;s.Poi=NearestPoi(vehicle);s.PoiName=snapshot.PoiName;s.PoiEligible=snapshot.InRange;
        s.PoiTargetIdentity=PoiTarget(s.Poi);
        Log.Out("[REBIRTH EnergyFlow] poi-switch vehicle="+vehicle.entityId+" on="+on+" target="+snapshot.PoiName+
            " supported="+snapshot.SupportedLights+" requiredWatts="+snapshot.RequiredWatts+" dirty="+s.Dirty);
        return true;
    }
    // Called only after the existing authoritative source transaction accepts
    // bank OFF. No second, stricter POI-owner admission or observation scan may
    // prevent that accepted OFF from clearing this camper's request.
    internal static void ClearAcceptedBankPoiRequest(EntityVehicle vehicle)
    {
        if(!Authority(vehicle))return;
        var s=For(vehicle);
        bool priorOn=s.PoiOn;
        if(s.PoiOn){s.PoiOn=false;s.Dirty=true;}
        poiPowerGates.Remove(vehicle);
        Log.Out("[REBIRTH EnergyFlow] bank-poi-off-accepted vehicle="+vehicle.entityId+" priorOn="+priorOn+
            " requestedOn="+s.PoiOn+" ownedPowered="+s.Nodes.Count+" ownedPlain="+s.PlainNodes.Count+" dirty="+s.Dirty);
    }
    public static bool SetPoiLimit(EntityPlayer player,EntityVehicle vehicle,int count)
    {
        if(!Authority(vehicle)||player==null||player.IsDead()||!ReferenceEquals(player.world,vehicle.world)||
            !ReferenceEquals(vehicle.world.GetEntity(player.entityId),player)||
            (player.position-vehicle.position).sqrMagnitude>64f||
            (player is EntityPlayerLocal local&&!vehicle.AllowActivationCommand("service",local))||
            RebirthCamperEnergyRuntime.For(vehicle).Snapshot().Mode!=RebirthCamperEnergyMode.Camp)return false;
        var owner=vehicle.GetOwner();var data=GameManager.Instance.GetPersistentPlayerList()?.GetPlayerDataFromEntityID(player.entityId);
        if(owner==null||data==null||!owner.Equals(data.PrimaryId)&&!owner.Equals(data.NativeId))return false;
        var current=ObservePoi(vehicle);
        if(!current.InRange||count<current.PinnedOnLights||count>current.SelectableLights)return false;
        var session=For(vehicle);if(session.PoiLimit==count||session.PoiLimit>=current.SelectableLights&&count==current.SelectableLights)return true;
        var proposal=ObservePoi(vehicle,count);
        if(session.PoiOn&&count>current.SelectedLights&&
            !CanSupplyPoiLights(RebirthCamperEnergyRuntime.For(vehicle).Snapshot(),proposal,out _))
        {Log.Out("[REBIRTH EnergyFlow] poi-selection-refused vehicle="+vehicle.entityId+" count="+count+
            " runtime="+proposal.ProspectiveRuntimeSeconds.ToString("0.0"));return false;}
        session.PoiLimit=count;session.Dirty=true;
        Log.Out("[REBIRTH EnergyFlow] poi-selection vehicle="+vehicle.entityId+" count="+count+
            " total="+current.SupportedLights+" runtime="+proposal.ProspectiveRuntimeSeconds.ToString("0.0"));
        return true;
    }
    internal static bool CanSupplyPoiLights(RebirthCamperEnergySnapshot energy,RebirthCamperEnergyPoiSnapshot poi,out int available)
    {
        available=0;
        if(energy?.Mode!=RebirthCamperEnergyMode.Camp||poi==null||!poi.InRange||poi.SelectedLights<=0||poi.RequiredWatts<0)return false;
        // POI admission is based on the complete stored native reserve at the
        // proposed load. Output rating is not a cap on spending that reserve.
        // Solar may recharge batteries later but is never direct POI supply.
        available=Math.Max(0,energy.Sources.Where(source=>source.Kind!=RebirthCamperEnergySourceKind.Solar&&
            source.IsOn&&source.Status==RebirthCamperEnergySourceStatus.Available).Sum(source=>(int)source.MaxOutput)-
            Math.Max(0,energy.DemandWatts-poi.DemandWatts));
        return poi.ProspectiveRuntimeSeconds>120d;
    }
    private static void PublishPoiPowerObservation(EntityVehicle vehicle,Session session,RebirthCamperEnergySnapshot energy,RebirthCamperEnergyPoiSnapshot poi)
    {
        if(!Guid.TryParse(session.WorldId,out var worldId))return;
        string frame;
        using(var stream=new System.IO.MemoryStream())
        using(var writer=new System.IO.BinaryWriter(stream,Encoding.UTF8,true))
        {
            writer.Write((byte)3);writer.Write(worldId.ToByteArray());writer.Write(session.Identity.ToByteArray());
            writer.Write(PoiTarget(session.Poi));writer.Write(poi.RequiredWatts);writer.Write(Math.Max(0,energy.DemandWatts-poi.DemandWatts));writer.Write(poi.ProspectiveRuntimeSeconds);writer.Write(poi.SelectedLights);
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
                if(reader.ReadByte()!=3||new Guid(reader.ReadBytes(16))!=worldId||new Guid(reader.ReadBytes(16))!=identity)return false;
                string target=reader.ReadString();int required=reader.ReadInt32(),otherDemand=reader.ReadInt32();double seconds=reader.ReadDouble();int selectedLights=reader.ReadInt32();
                if(stream.Position!=stream.Length||target.Length>4096||required<0||otherDemand<0||selectedLights<0||double.IsNaN(seconds)||double.IsInfinity(seconds)||seconds< -1||target!=ObservePoiTarget(vehicle))return false;
                var cached=projections.GetValue(vehicle,_=>new PoiProjection());
                var poi=new RebirthCamperEnergyPoiSnapshot {InRange=true,SupportedLights=cached.Supported,SelectedLights=Math.Min(cached.Supported,selectedLights),RequiredWatts=required,ProspectiveRuntimeSeconds=seconds};
                energy.DemandWatts=otherDemand;
                return CanSupplyPoiLights(energy,poi,out _);
            }
        }
        catch(FormatException){return false;}catch(System.IO.IOException){return false;}catch(ArgumentException){return false;}
    }
    private static bool CanReclaimRestoredPoi(Session s,PowerItem node)
    {
        if(s.PoiOn||!s.PoiControl||s.Poi==null||owners.ContainsKey(node)||!Current(s)||!CurrentNode(s,node)||!Eligible(node)||
            !Light(node)||(node.TileEntity.IsPlayerPlaced&&RebirthCamperWorldLighting.Stamp(node.TileEntity)==0)||!Inside(s.Poi,node.Position)||!PoiOwnerAuthorized(s.Vehicle,node)||
            !s.Baselines.TryGetValue(node.Position,out var b)||!b.Proofed||b.Removed||b.OrdinaryStamp==0||
            b.OrdinaryStamp!=RebirthCamperWorldLighting.Stamp(node.TileEntity)||b.Block!=node.BlockID)return false;
        if(sessions.Values.Any(other=>!ReferenceEquals(other,s)&&other.PoiOn&&ReferenceEquals(other.Poi,s.Poi)&&Current(other)))return false;
        var value=s.World.GetBlock(node.Position);
        return value.meta==b.Meta&&node.IsPowered==b.Powered&&((PowerConsumer)node).lastActivate==b.Active&&
            (!b.HasToggle||node is PowerConsumerToggle toggle&&toggle.IsToggled==b.Toggle);
    }
    private static bool KeepPoiOff(Session s,Vector3i position,Baseline b)
        =>!s.PoiOn&&s.PoiControl&&s.Poi!=null&&Inside(s.Poi,position)&&b.HasWritten;
    private static void Restore(Session s,PowerItem node,bool retainPoiOff=false)
    {
        if(!s.Baselines.TryGetValue(node.Position,out var b)||!b.Proofed||b.Removed||s.Restored.Contains(node.Position))return;
        if(owners.TryGetValue(node,out var supplier)&&!ReferenceEquals(supplier,s))
        {s.Nodes.Remove(node.Position);s.Restored.Add(node.Position);b.HasWritten=false;return;}
        if(retainPoiOff&&KeepPoiOff(s,node.Position,b)&&Current(s)&&CurrentNode(s,node)&&Eligible(node)&&Light(node)&&
            (!node.TileEntity.IsPlayerPlaced||RebirthCamperWorldLighting.Stamp(node.TileEntity)!=0)&&ReferenceEquals(supplier,s)&&PoiOwnerAuthorized(s.Vehicle,node)&&MatchesExpected(s,node)&&
            node.IsPowered==((s.World.GetBlock(node.Position).meta&2)!=0)&&node is PowerConsumer heldConsumer&&
            heldConsumer.lastActivate==node.IsPowered)return;
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
        SuspendCircuits(vehicle,true);
        if(!ThreadManager.IsMainThread())return;
        poiPowerGates.Remove(vehicle);
        if(Authority(vehicle))
        {
            var item=vehicle.vehicle.GetUpdatedItemValue();
            if(item.Metadata?.Remove(PoiPowerObservationKey)==true)vehicle.SendSyncData(4);
        }
        if(!sessions.TryGetValue(vehicle,out var s))return;
        var poiPlain=s.PlainNodes.Keys.Where(position=>s.Poi!=null&&Inside(s.Poi,position)).ToArray();
        var poiPowered=s.Nodes.Where(pair=>s.Poi!=null&&Inside(s.Poi,pair.Key)&&
            Light(pair.Value)&&(!pair.Value.TileEntity.IsPlayerPlaced||RebirthCamperWorldLighting.Stamp(pair.Value.TileEntity)!=0)).Select(pair=>pair.Key).ToArray();
        // A position can already contain a replacement when an old lease is
        // released. Capture only our proved current incarnation before Restore,
        // then recheck the exact tile/node again before the final zero supply.
        var releaseTiles=new Dictionary<Vector3i,TileEntity>();
        var releaseNodes=new Dictionary<Vector3i,PowerItem>();
        var releaseStamps=new Dictionary<Vector3i,uint>();
        foreach(var position in poiPlain)
            if(s.PlainNodes.TryGetValue(position,out var light)&&s.Baselines.TryGetValue(position,out var baseline)&&
                baseline.Proofed&&baseline.HasWritten&&!baseline.Removed&&baseline.OrdinaryStamp!=0&&
                plainOwners.TryGetValue(light,out var owner)&&ReferenceEquals(owner,s)&&
                CurrentPlain(s,light)&&PlainAuthorized(s,light)&&MatchesPlain(s,light))
            {releaseTiles[position]=light;releaseStamps[position]=baseline.OrdinaryStamp;}
        foreach(var position in poiPowered)
            if(s.Nodes.TryGetValue(position,out var node)&&s.Baselines.TryGetValue(position,out var baseline)&&
                baseline.Proofed&&baseline.HasWritten&&!baseline.Removed&&baseline.OrdinaryStamp!=0&&
                owners.TryGetValue(node,out var owner)&&ReferenceEquals(owner,s)&&Eligible(node)&&
                CurrentNode(s,node)&&PoiOwnerAuthorized(s.Vehicle,node)&&MatchesExpected(s,node))
            {releaseTiles[position]=node.TileEntity;releaseNodes[position]=node;releaseStamps[position]=baseline.OrdinaryStamp;}
        s.PoiOn=false;s.PoiControl=false;s.Excluded.Clear();
        if(PowerManager.HasInstance)foreach(var b in s.Baselines.Values)
            if(PowerManager.Instance.PowerItemDictionary.TryGetValue(b.Position,out var node))Restore(s,node);
        foreach(var b in s.Baselines.Values.ToArray())
            if(Block.list[b.Block] is BlockLight&&s.World.GetTileEntity(b.Position) is TileEntityLight light)RestorePlain(s,light);
        // An authored POI fixture must go dark when its last camper leaves,
        // even if its saved baseline had an old native ON bit.
        foreach(var position in releaseTiles.Keys)
        {
            var tile=releaseTiles[position];
            if(!ReferenceEquals(s.World.GetTileEntity(position),tile)||RebirthCamperWorldLighting.Stamp(tile)!=releaseStamps[position]||
                !s.Baselines.TryGetValue(position,out var baseline)||s.World.GetBlock(position).type!=baseline.Block||
                s.World.IsWithinTraderArea(position))continue;
            if(releaseNodes.TryGetValue(position,out var capturedNode))
            {
                if(!CurrentNode(s,capturedNode)||!Eligible(capturedNode)||!PoiOwnerAuthorized(s.Vehicle,capturedNode)||
                    owners.TryGetValue(capturedNode,out var currentOwner)&&!ReferenceEquals(currentOwner,s))continue;
            }
            else if(!(tile is TileEntityLight light)||!CurrentPlain(s,light)||!PlainAuthorized(s,light)||
                plainOwners.TryGetValue(light,out var currentOwner)&&!ReferenceEquals(currentOwner,s))continue;
            var value=s.World.GetBlock(position);
            if((value.meta&2)!=0){value.meta=(byte)(value.meta&~2);s.World.SetBlockRPC(position,value);}
            if(capturedNode!=null){ushort zero=0;capturedNode.HandlePowerReceived(ref zero);capturedNode.HandlePowerUpdate(false);}
        }
        // Retain original durable baselines: vehicle save alone cannot prove that
        // the restored native world chunks have reached their own final files.
    }
    public static void Suspend(EntityVehicle vehicle)
    {
        SuspendCircuits(vehicle,false);
        if(!ThreadManager.IsMainThread()||!sessions.TryGetValue(vehicle,out var s))return;
        foreach(var node in s.Nodes.Values.ToArray())
            if(s.Baselines.TryGetValue(node.Position,out var baseline)&&baseline.HasWritten)Restore(s,node,true);
        foreach(var light in s.PlainNodes.Values.ToArray())
            if(s.Baselines.TryGetValue(light.ToWorldPos(),out var baseline)&&baseline.HasWritten)RestorePlain(s,light,true);
        // Recovery/custody gates stop only actual owned supplies. Requested POI
        // controls and durable original/expected records remain for exact resume.
    }
    internal static void OnUnload(TileEntityPowered tile)
    {var node=tile?.GetPowerItem();CircuitNodeRemoved(node);if(node!=null&&owners.TryGetValue(node,out var s))Restore(s,node);}
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
        if(PowerManager.Instance.PowerItemDictionary.TryGetValue(position,out var node))
        {
            CircuitNodeRemoved(node);
            if(owners.TryGetValue(node,out var s))
            {Restore(s,node);s.Excluded.Add(position);if(s.Baselines.TryGetValue(position,out var b)){b.Removed=true;s.Dirty=true;}}
        }
    }
    private static void Shutdown(ref ModEvents.SWorldShuttingDownData data)
    {foreach(var vehicle in sessions.Keys.ToArray())Release(vehicle);
        foreach(var session in sessions.Values){session.Witness?.Dispose();session.Witness=null;}
        sessions.Clear();owners.Clear();plainOwners.Clear();circuits.Clear();projections=new ConditionalWeakTable<EntityVehicle,PoiProjection>();}
}

[HarmonyPatch(typeof(TileEntityPowered),nameof(TileEntityPowered.OnUnload))]
internal static class RebirthCamperEnergyDeviceUnload
{private static void Prefix(TileEntityPowered __instance)=>RebirthCamperEnergyDeviceAdapter.OnUnload(__instance);}

[HarmonyPatch(typeof(BlockPowered),nameof(BlockPowered.OnBlockRemoved))]
internal static class RebirthCamperEnergyDeviceRemoval
{private static void Prefix(WorldBase world,Vector3i _blockPos){RebirthCamperRelayWiring.BlockRemoved(world,_blockPos);RebirthCamperEnergyDeviceAdapter.OnRemoved(world,_blockPos);}}

[HarmonyPatch(typeof(TileEntity),nameof(TileEntity.OnUnload))]
internal static class RebirthCamperEnergyOrdinaryLightUnload
{private static void Prefix(TileEntity __instance){if(__instance is TileEntityLight light)RebirthCamperEnergyDeviceAdapter.OnPlainUnload(light);}}
[HarmonyPatch(typeof(Block),nameof(Block.OnBlockRemoved))]
internal static class RebirthCamperEnergyOrdinaryLightRemoval
{private static void Prefix(Block __instance,WorldBase __0,Vector3i __2){if(__instance is BlockLight){RebirthCamperRelayWiring.BlockRemoved(__0,__2);RebirthCamperEnergyDeviceAdapter.OnPlainRemoved(__0,__2);}}}

[HarmonyPatch(typeof(EntityVehicle),nameof(EntityVehicle.OnEntityUnload))]
internal static class RebirthCamperEnergyCircuitVehicleUnload
{
    private static void Prefix(EntityVehicle __instance)
    {
        if(__instance==null)return;
        if(!__instance.IsDead()){RebirthCamperEnergyDeviceAdapter.OnCircuitVehicleUnload(__instance);return;}
        if(RebirthCamperRadial.IsCamper(__instance))RebirthCamperRelayWiring.ReleaseDestroyed(__instance);
        RebirthCamperEnergyDeviceAdapter.Release(__instance);
    }
}

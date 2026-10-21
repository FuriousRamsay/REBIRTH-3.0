using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Xml.Linq;

// Initial source identity admission and pure debit planning. No owner input or
// source item is mutated here; only existing native identity owners may publish
// an initial incarnation before the durable immutable original is written.
internal sealed class RebirthRemoteResourceAdmission:IDisposable
{
    internal const int MaximumOutcomeBytes=6*1024*1024;
    private sealed class Frame
    {
        internal IRemoteResourceSource Source;internal ItemStack[] Before,After,Paid;internal bool[] Locks;
    }
    private readonly EntityPlayer actor;private readonly World world;private readonly string owner,creation,worldId;
    private readonly int deaths;private readonly ulong epoch,request;private readonly RemoteResourceClientOperation kind;
    private readonly Guid operation;private readonly ItemStack[] requirements;
    private readonly List<IRemoteResourceSource> selected=new List<IRemoteResourceSource>();
    private readonly Dictionary<string,string> tokens=new Dictionary<string,string>(StringComparer.Ordinal);
    private readonly Dictionary<string,object> native=new Dictionary<string,object>(StringComparer.Ordinal);
    private IDisposable watch;private Func<bool> saved;private DateTime expires;
    private int cursor;private bool closed,finished;private string refusal;
    private RebirthRemoteResourceDurableJournal.Record result;
    private readonly RebirthRemoteResourceNetworkScope network;
    internal RebirthRemoteResourceAdmission(EntityPlayer actor,string owner,string creation,string worldId,ulong epoch,ulong request,RemoteResourceClientOperation kind,ItemStack[] requirements)
    {
        this.actor=actor;world=actor.world;this.owner=owner;this.creation=creation;this.worldId=worldId;deaths=actor.Died;
        this.epoch=epoch;this.request=request;this.kind=kind;this.requirements=ItemStack.Clone(requirements);
        operation=RebirthRemoteResourceDurableJournal.Operation(worldId,owner,creation,epoch,request,kind);
        network=RebirthRemoteResourceNetworkScope.Capture(actor);
        try
        {
            var sources=RemoteResourceSourceDiscovery.ResolveNearby(world,actor,RemoteResourcesRuntimePolicy.Radius);
            sources=sources.OrderBy(source=>source.StableId,StringComparer.Ordinal).ToList();
            var plan=Plan(sources);foreach(var frame in plan)selected.Add(frame.Source);
        }
        catch{refusal="Current authorized sources do not cover the original remote request.";}
    }
    internal Guid Operation=>operation;
    private bool Current()=>!closed&&network?.Current==true&&ThreadManager.IsMainThread()&&!RebirthRemoteResourcePreAdmissionRefusal.BlocksAdmission(operation)&&world?.IsRemote()==false&&ReferenceEquals(GameManager.Instance?.World,world)&&
        ReferenceEquals(world.GetEntity(actor.entityId),actor)&&!actor.IsDead()&&actor.Died==deaths&&RebirthOriginalActionScope.Matches(actor,owner,creation)&&
        RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var identity)&&identity.StorageKey==owner&&RemoteResourceWorldIdentity.TryGet(world,out string current)&&current==worldId;
    private bool Eligible(IRemoteResourceSource source)=>Current()&&RemoteResourcesRuntimePolicy.Enabled&&!RebirthRemoteResourceSourceCustody.BlocksSource(source)&&
        (source.Position-actor.position).sqrMagnitude<=RemoteResourcesRuntimePolicy.Radius*RemoteResourcesRuntimePolicy.Radius&&RemoteResourceEligibility.CanUse(source,actor,true,false,out _);
    private List<Frame> Plan(IEnumerable<IRemoteResourceSource> sources)
    {
        var needed=new Dictionary<int,ItemStack>();
        foreach(var stack in requirements)
        {
            if(stack==null||stack.IsEmpty()||stack.count<1)throw new InvalidDataException();
            if(needed.TryGetValue(stack.itemValue.type,out var previous))
            {
                previous.count=checked(previous.count+stack.count);
                if(stack.itemValue.HasMetadata("rebirth.cooking.fullIngredient"))previous.itemValue.SetMetadata("rebirth.cooking.fullIngredient","1");
            }
            else needed.Add(stack.itemValue.type,stack.Clone());
        }
        var frames=new List<Frame>();
        foreach(var candidate in sources)
        {
            IRemoteResourceSource source=candidate;
            if(source is RemoteNpcResourceSource npc)source=new RemoteNpcResourceSource(npc.NativeActor);
            if(!Eligible(source))continue;
            var before=source.Slots;if(before==null||before.Length<1||before.Length>short.MaxValue)throw new InvalidDataException();
            var frame=new Frame{Source=source,Before=ItemStack.Clone(before),After=ItemStack.Clone(before),Locks=new bool[before.Length]};var paid=new List<ItemStack>();
            if(source.SlotLocks!=null&&source.SlotLocks.Length!=before.Length)throw new InvalidDataException();
            for(int slot=0;slot<before.Length;slot++)
            {
                frame.Locks[slot]=source.SlotLocks!=null&&source.SlotLocks[slot];var stack=before[slot];
                if(stack==null||stack.count<0||stack.count>ushort.MaxValue)throw new InvalidDataException();
                if(frame.Locks[slot]||stack.IsEmpty()||!needed.TryGetValue(stack.itemValue.type,out var requirement)||requirement.count==0||!RemoteResourceTransactions.MatchesConsumable(stack,requirement.itemValue))continue;
                int count=Math.Min(stack.count,requirement.count);var payment=stack.Clone();payment.count=count;paid.Add(payment);requirement.count-=count;
                if(count==stack.count)frame.After[slot]=ItemStack.Empty.Clone();else frame.After[slot].count-=count;
            }
            if(paid.Count>0){frame.Paid=paid.ToArray();frames.Add(frame);if(frames.Count>128)throw new InvalidDataException();}
            if(needed.Values.All(value=>value.count==0))break;
        }
        if(needed.Values.Any(value=>value.count!=0))throw new InvalidDataException("Current authorized remote sources do not cover the original request.");
        var all=frames.SelectMany(frame=>frame.Paid).ToArray();if(all.Length>short.MaxValue||RebirthCamperInventorySnapshot.Capture(all).Length>MaximumOutcomeBytes)throw new InvalidDataException("Original outcome exceeds the bounded native delivery frame.");
        return frames;
    }
    private bool BeginIdentity(IRemoteResourceSource source)
    {
        if(!Eligible(source))return false;expires=DateTime.UtcNow.AddSeconds(30);
        if(source is StaticContainerResourceSource container&&container.NativeTile is TileEntityComposite tile)
        {
            string token=tile.GetFeature<TEFeatureRebirthContainerName>()?.Incarnation;
            if(!RebirthRemoteResourceNativeBinding.TryResolve(world,source.StableId,source.Kind,token,out var binding))return false;
            var witness=RebirthRemoteResourceNativePublication.Begin(binding,()=>Eligible(source));watch=witness;saved=()=>witness?.Saved()==true;tokens[source.StableId]=token;native[source.StableId]=tile;return witness!=null;
        }
        if(source is WorkstationOutputResourceSource station)
        {
            var witness=RebirthLogisticsEndpointIdentity.Begin(world,station.NativeStation,()=>Eligible(source));watch=witness;saved=()=>witness?.Saved()==true;
            tokens[source.StableId]=witness?.Id;native[source.StableId]=station.NativeStation;return witness!=null;
        }
        if(source is VehicleResourceSource vehicle)
        {
            var witness=RebirthLogisticsVehiclePublication.BeginIdentityAdmission(vehicle.Vehicle,()=>Eligible(source));watch=witness;saved=()=>witness!=null&&RebirthLogisticsVehiclePublication.Verify(witness,()=>Eligible(source));
            tokens[source.StableId]=RebirthLogisticsVehiclePublication.CurrentIncarnation(vehicle.Vehicle);native[source.StableId]=vehicle.Vehicle;return witness!=null;
        }
        if(source is DroneResourceSource drone)
        {
            var witness=RebirthLogisticsDronePublication.BeginIdentityAdmission(drone.Drone,()=>Eligible(source));watch=witness;saved=()=>witness!=null&&RebirthLogisticsDronePublication.Verify(witness,()=>Eligible(source));
            tokens[source.StableId]=RebirthLogisticsDronePublication.CurrentIncarnation(drone.Drone);native[source.StableId]=drone.Drone;return witness!=null;
        }
        if(source is RemoteNpcResourceSource)
        {
            // Canonical primary before-state validation happens after the
            // original is durably written and capacity reserved, before debit.
            RebirthNpcInventoryPersistenceStore.Save();RebirthNpcAggregatePersistenceStore.Save();saved=()=>Current();return true;
        }
        return false;
    }
    private static string Image(ItemStack[] items)=>Convert.ToBase64String(RebirthCamperInventorySnapshot.Capture(items));
    private XElement Header(IEnumerable<XElement> sources)
    {
        byte[] bytes=RebirthCamperInventorySnapshot.Capture(requirements);string hash;
        using(var sha=SHA256.Create())hash=BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();
        return new XElement("resourceOutcome",new XAttribute("version",1),new XAttribute("operation",operation.ToString("N")),new XAttribute("world",worldId),new XAttribute("owner",owner),new XAttribute("creation",creation),
            new XAttribute("deaths",deaths),new XAttribute("epoch",epoch.ToString(CultureInfo.InvariantCulture)),new XAttribute("request",request.ToString(CultureInfo.InvariantCulture)),new XAttribute("kind",(byte)kind),new XAttribute("phase",0),new XAttribute("outcome",0),new XAttribute("requirementsHash",hash),
            new XElement("requirements",Convert.ToBase64String(bytes)),new XElement("sources",sources),new XElement("removed",Image(new ItemStack[0])),new XElement("reason",refusal??""));
    }
    internal bool Step(out RebirthRemoteResourceDurableJournal.Record original)
    {
        original=null;if(finished){original=result;return result!=null;}if(!Current())return false;
        try
        {
            if(cursor<selected.Count&&refusal==null)
            {
                var source=selected[cursor];if(saved==null&&!BeginIdentity(source))refusal="Native source identity admission was unavailable.";
                else if(saved?.Invoke()!=true){if(DateTime.UtcNow>=expires)refusal="Native source identity publication did not complete.";else return false;}
                if(refusal==null){CloseWatch();cursor++;return false;}
                CloseWatch();
            }
            var nodes=new List<XElement>();var frames=refusal==null?Plan(selected):new List<Frame>();
            foreach(var frame in frames)
            {
                if(frame.Source is RemoteNpcResourceSource npc)
                {nodes.Add(RebirthRemoteResourceDurableOutcome.CaptureNpcSource(actor,npc,frame.After,frame.Paid,Guid.NewGuid()));continue;}
                if(!tokens.TryGetValue(frame.Source.StableId,out string token)||!RebirthRemoteResourceNativeBinding.TryResolve(world,frame.Source.StableId,frame.Source.Kind,token,out var binding)||
                    !ReferenceEquals(binding.Endpoint,native[frame.Source.StableId]))throw new InvalidDataException();
                nodes.Add(new XElement("source",new XAttribute("id",frame.Source.StableId),new XAttribute("kind",(int)frame.Source.Kind),new XAttribute("incarnation",token),new XAttribute("attempted",0),new XAttribute("published",0),new XAttribute("beforeProof",""),new XAttribute("proof",""),
                    new XElement("before",Image(frame.Before)),new XElement("after",Image(frame.After)),new XElement("paid",Image(frame.Paid)),new XElement("locks",new string(frame.Locks.Select(value=>value?'1':'0').ToArray()))));
            }
            if(!RebirthRemoteResourceDurableJournal.TryPrepare(Header(nodes),Current,out result))return false;
            finished=true;original=result;return true;
        }
        catch(Exception error)
        {
            // No source item mutation occurs in this class. A failed candidate
            // can therefore publish an exact empty-payment refusal original.
            refusal=error is InvalidDataException?"Original remote source plan was unavailable.":"Original source admission could not be completed.";CloseWatch();
            if(!Current()||!RebirthRemoteResourceDurableJournal.TryPrepare(Header(new XElement[0]),Current,out result))return false;
            finished=true;original=result;return true;
        }
    }
    private void CloseWatch(){watch?.Dispose();watch=null;saved=null;}
    public void Dispose(){CloseWatch();closed=true;}
}

using System;
using System.Globalization;

// Read-only binding to one current authoritative native endpoint. Source IDs
// locate candidates; the persisted native incarnation owns the original, so an
// entity-ID reuse or replacement tile cannot inherit a pending operation.
internal sealed class RebirthRemoteResourceNativeBinding
{
    internal World World { get; }
    internal string Id { get; }
    internal RemoteResourceSourceKind Kind { get; }
    internal string Incarnation { get; }
    internal object Endpoint { get; }
    internal TEFeatureStorage Storage { get; }
    internal TileEntityWorkstation Station=>Endpoint as TileEntityWorkstation;
    internal EntityVehicle Vehicle=>Endpoint as EntityVehicle;
    internal EntityDrone Drone=>Endpoint as EntityDrone;
    internal ItemStackGrid Grid=>Storage?.ItemGrid??Vehicle?.bag?.ItemGrid??Drone?.bag?.ItemGrid;
    internal Vector3i Position { get; }
    internal UnityEngine.Vector3 SourcePosition=>source.Position;
    private readonly IRemoteResourceSource source;
    private RebirthRemoteResourceNativeBinding(World world,string id,RemoteResourceSourceKind kind,string incarnation,object endpoint,TEFeatureStorage storage,Vector3i position,IRemoteResourceSource source)
    {World=world;Id=id;Kind=kind;Incarnation=incarnation;Endpoint=endpoint;Storage=storage;Position=position;this.source=source;}
    internal bool Current()
    {
        if(!ThreadManager.IsMainThread()||World==null||World.IsRemote()||!ReferenceEquals(GameManager.Instance?.World,World))return false;
        if(Endpoint is TileEntityComposite tile)
            return ReferenceEquals(World.GetTileEntity(Position),tile)&&ReferenceEquals(tile.GetFeature<TEFeatureStorage>(),Storage)&&
                tile.GetFeature<TEFeatureRebirthContainerName>()?.Incarnation==Incarnation;
        if(Station!=null)return ReferenceEquals(World.GetTileEntity(Position),Station)&&RebirthLogisticsEndpointIdentity.TryGetIncarnation(World,Station,out string token)&&token==Incarnation;
        if(Vehicle!=null)return !Vehicle.IsDead()&&ReferenceEquals(World.GetEntity(Vehicle.entityId),Vehicle)&&RebirthLogisticsVehiclePublication.CurrentIncarnation(Vehicle)==Incarnation;
        return Drone!=null&&!Drone.IsDead()&&ReferenceEquals(World.GetEntity(Drone.entityId),Drone)&&RebirthLogisticsDronePublication.CurrentIncarnation(Drone)==Incarnation;
    }
    internal ItemStack[] CopySlots()=>Current()?ItemStack.Clone(source.Slots):null;
    internal int Revision=>source.Revision;
    internal bool[] CopyLocks()
    {
        if(!Current()||source.Slots==null)return null;var result=new bool[source.Slots.Length];var locks=source.SlotLocks;
        for(int i=0;i<result.Length;i++)result[i]=locks!=null&&i<locks.Length&&locks[i];return result;
    }
    internal bool IsAuthorized(EntityPlayer actor,out string reason)
    {
        reason="original source unavailable";
        if(!Current()||!source.IsLoaded||source.IsBusy)return false;
        if(Vehicle!=null&&(RebirthCamperEnergyMaintenanceCargoGuard.BlocksVehicle(Vehicle)||
            RebirthCamperModuleRuntime.TryGetExisting(Vehicle,out var runtime)&&runtime.TransferPending))return false;
        if(Station!=null&&RebirthCookingRemoteAdmissionBackend.Held(Station))return false;
        return source.IsAuthorized(actor,false,out reason);
    }
    // Only the source-custody one-use setter permit can make these native
    // methods pass its installed guards. Never call during pure planning.
    internal void SetSlot(int slot,ItemStack value)=>source.SetSlot(slot,value);
    internal void MarkModified()=>source.MarkModified();
    internal static bool TryResolve(World world,string id,RemoteResourceSourceKind kind,string incarnation,out RebirthRemoteResourceNativeBinding binding)
    {
        binding=null;
        try
        {
            if(!ThreadManager.IsMainThread()||world?.IsRemote()!=false||!ReferenceEquals(GameManager.Instance?.World,world)||
                !Guid.TryParseExact(incarnation,"N",out var token)||token==Guid.Empty||id==null||id.Length>512)return false;
            IRemoteResourceSource source;object endpoint;TEFeatureStorage storage=null;Vector3i position=default;
            if(kind==RemoteResourceSourceKind.StaticContainer||kind==RemoteResourceSourceKind.WorkstationOutput)
            {
                string prefix=kind==RemoteResourceSourceKind.StaticContainer?"S:":"W:";if(!id.StartsWith(prefix,StringComparison.Ordinal))return false;
                string[] parts=id.Substring(2).Split(',');if(parts.Length!=3)return false;
                if(!int.TryParse(parts[0],NumberStyles.AllowLeadingSign,CultureInfo.InvariantCulture,out int x)||!int.TryParse(parts[1],NumberStyles.AllowLeadingSign,CultureInfo.InvariantCulture,out int y)||!int.TryParse(parts[2],NumberStyles.AllowLeadingSign,CultureInfo.InvariantCulture,out int z))return false;
                position=new Vector3i(x,y,z);
                if((kind==RemoteResourceSourceKind.StaticContainer?RemoteResourceIdentity.Static(position):RemoteResourceIdentity.Workstation(position))!=id)return false;
                endpoint=world.GetTileEntity(position);
                if(kind==RemoteResourceSourceKind.StaticContainer)
                {if(endpoint is not TileEntityComposite tile||(storage=tile.GetFeature<TEFeatureStorage>())==null)return false;source=new StaticContainerResourceSource(world,position);}
                else{if(endpoint is not TileEntityWorkstation)return false;source=new WorkstationOutputResourceSource(world,position);}
            }
            else
            {
                string prefix=kind==RemoteResourceSourceKind.VehicleStorage?"V:":kind==RemoteResourceSourceKind.DroneStorage?"D:":null;
                if(prefix==null||!id.StartsWith(prefix,StringComparison.Ordinal)||!int.TryParse(id.Substring(2),NumberStyles.None,CultureInfo.InvariantCulture,out int entity)||entity<0||prefix+entity.ToString(CultureInfo.InvariantCulture)!=id)return false;
                endpoint=world.GetEntity(entity);
                if(kind==RemoteResourceSourceKind.VehicleStorage)
                {if(endpoint is not EntityVehicle vehicle)return false;source=new VehicleResourceSource(vehicle);}
                else{if(endpoint is not EntityDrone drone)return false;source=new DroneResourceSource(drone);}
            }
            var result=new RebirthRemoteResourceNativeBinding(world,id,kind,incarnation,endpoint,storage,position,source);
            if(!result.Current()||source.Slots==null||source.Slots.Length<1||source.Slots.Length>short.MaxValue)return false;binding=result;return true;
        }
        catch{return false;}
    }
}

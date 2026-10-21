using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

// Server preparation only. Native identity publication precedes the immutable
// journal. No native owner/source item setter is called by this class.
internal sealed class RebirthLogisticsOriginalAdmission:IDisposable
{
    private readonly EntityPlayer actor;private readonly World world;private readonly RebirthStablePlayerIdentity owner;
    private readonly Guid operation;private readonly ulong epoch;private readonly int request,deaths;private readonly string creation,target,selection,directory;
    private readonly QuickStackRadialAction action;private readonly DateTime expires;
    private readonly List<IRemoteResourceSource> sources=new List<IRemoteResourceSource>();
    private readonly List<EntityRebirthNPC> logical=new List<EntityRebirthNPC>();
    private readonly List<RebirthLogisticsPlanning.Observation> admitted=new List<RebirthLogisticsPlanning.Observation>();
    private IDisposable watch;private Func<bool> saved;private object native;private string incarnation;private int cursor;private bool closed,finished;
    private RebirthLogisticsTransferJournal.Record result;
    private readonly ConnectionManager manager;private readonly ClientInfo peer;private readonly INetConnection connection;
    private RebirthLogisticsOriginalAdmission(EntityPlayer actor,RebirthStablePlayerIdentity owner,Guid operation,ulong epoch,int request,QuickStackRadialAction action,string target,string selection)
    {
        this.actor=actor;world=actor.world;this.owner=owner;this.operation=operation;this.epoch=epoch;this.request=request;this.action=action;this.target=target??string.Empty;this.selection=selection??string.Empty;
        deaths=actor.Died;creation=RebirthCamperCharacterAccess.CurrentCreation(actor);directory=GameIO.GetSaveGameDir();expires=DateTime.UtcNow.AddSeconds(120);
        manager=SingletonMonoBehaviour<ConnectionManager>.Instance;RebirthLogisticsOriginalTransport.TryCurrentPeer(actor,out peer,out connection);
    }
    // Expiry ends only this transient, pre-effect discovery/publication watch.
    // The caller retains the same owner operation and immutable request.
    internal bool Expired=>closed||DateTime.UtcNow>=expires;
    private bool Current()=>!closed&&ThreadManager.IsMainThread()&&DateTime.UtcNow<expires&&ReferenceEquals(GameManager.Instance?.World,world)&&!world.IsRemote()&&GameIO.GetSaveGameDir()==directory&&
        ReferenceEquals(world.GetEntity(actor.entityId),actor)&&!actor.IsDead()&&actor.Died==deaths&&RebirthOriginalActionScope.Matches(actor,owner.StorageKey,creation)&&
        RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var identity)&&identity.StorageKey==owner.StorageKey&&
        ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance,manager)&&RebirthLogisticsOriginalTransport.TryCurrentPeer(actor,out var livePeer,out var liveConnection)&&
        ReferenceEquals(livePeer,peer)&&ReferenceEquals(liveConnection,connection);
    private bool ReadOwner(out PlayerDataFile data)
    {data=null;return Current()&&RebirthLogisticsOwnerReceipt.TrySaved(owner,operation,0,out data)&&data.deaths==deaths&&RebirthLogisticsHeaderAdmission.ValidateNativeBefore(actor,Header(),data);}
    internal static RebirthLogisticsOriginalAdmission Begin(EntityPlayer actor,Guid operation,ulong epoch,int request,QuickStackRadialAction action,string target,string selection,bool cancel=false)
    {
        if(!ThreadManager.IsMainThread()||actor?.world?.IsRemote()!=false||operation==Guid.Empty||epoch==0||request<=0||
            (byte)action>(byte)QuickStackRadialAction.PullDroneCredits||target?.Length>512||selection?.Length>8192||
            !ReferenceEquals(GameManager.Instance?.World,actor.world)||!ReferenceEquals(actor.world.GetEntity(actor.entityId),actor)||actor.IsDead()||
            !RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var owner)||!RebirthNpcOriginalEffectAdmission.Bootstrap(actor.world))return null;
        var admission=new RebirthLogisticsOriginalAdmission(actor,owner,operation,epoch,request,action,target,selection);
        try
        {
            if(!admission.ReadOwner(out var savedOwner))return null;
            if(RebirthLogisticsTransferJournal.TryRead(owner.StorageKey,operation,out var original))
            {
                if(original.Epoch!=epoch||original.Request!=request||original.Action!=action||original.Target!=(target??string.Empty)||original.Selection!=(selection??string.Empty))return null;
                admission.result=original;admission.finished=true;return admission;
            }
            if(!RebirthLogisticsHeaderAdmission.ValidateNativeBefore(actor,admission.Header(),savedOwner)||!RebirthLogisticsPreAdmissionReceipt.Prepare(admission.Header(),savedOwner)||!RebirthLogisticsHeaderAdmission.BeginAdmission(actor,admission.Header()))return null;
            if(cancel){admission.Reject();return admission;}
            if(!LogisticsTransferService.CanDispatch(actor.world,actor,action,out _))
            {admission.Reject();return admission;}
            admission.Discover();return admission;
        }
        catch{admission.Reject();return admission;}
    }
    private void Discover()
    {
        if(!Current()||RebirthLogisticsPlanning.IsActive)throw new InvalidDataException();
        RebirthNpcInventoryPersistenceStore.EnsureLoaded();RebirthNpcAggregatePersistenceStore.EnsureLoaded();
        float radius=Math.Max(QuickStackService.Radius,LogisticsPreviewService.CompanionRadiusForPlanning);
        // Native registration happens before pure planning; it never runs from
        // an overlay callback while a route is being composed.
        QuickStackWorkstationOutputService.FindNearbyWorkstationPositions(world,actor.position,radius);
        foreach(var source in RemoteResourceRegistry.ResolveNearby(world,actor,radius,true,true,true))Add(source,radius);
        foreach(var entry in QuickStackContainerRegistry.Query(world,actor.position,radius))Add(new StaticContainerResourceSource(world,entry.Position),radius);
        var bounds=new Bounds(actor.position,Vector3.one*LogisticsPreviewService.CompanionRadiusForPlanning*2f);
        foreach(var entity in world.GetEntitiesInBounds(typeof(EntityAlive),bounds,new List<Entity>()))
        {
            if(!(entity is EntityRebirthNPC npc)||npc.IsDead()||(npc.position-actor.position).sqrMagnitude>LogisticsPreviewService.CompanionRadiusForPlanning*LogisticsPreviewService.CompanionRadiusForPlanning||
                npc.RebirthRuntimeState==null||npc.RebirthRuntimeState.StableId.IsEmpty||!RemoteNpcResourceSource.IsAuthorized(npc,actor,false,out _)||
                RebirthNpcInteractionSessionService.IsInventoryAccessing(npc.RebirthRuntimeState.StableId)||
                !RebirthNpcProfileRegistry.TryResolve(npc.RebirthRuntimeState.ProfileId,out var profile)||!profile.Has(RebirthNpcCapabilities.Inventory))continue;
            if(profile.Category==RebirthNpcCategory.DogCompanion)Add(new RemoteNpcResourceSource(npc),radius);
            else if(profile.Category==RebirthNpcCategory.Survivor||profile.Category==RebirthNpcCategory.PantherCompanion)
            {
                if(!RebirthNpcInventoryTransactionService.PruneSavedTerminalLogisticsEffects(npc.RebirthRuntimeState.StableId))throw new InvalidDataException();logical.Add(npc);
            }
        }
        if(sources.Count+logical.Count>RebirthLogisticsTransferJournal.MaximumLegs)throw new InvalidDataException();
    }
    private void Add(IRemoteResourceSource source,float radius)
    {
        if(source==null||!source.IsLoaded||source.IsBusy||RebirthRemoteResourceSourceCustody.Blocks(RebirthLogisticsPlanning.NativeEndpoint(source))||!source.IsAuthorized(actor,false,out _)||(source.Position-actor.position).sqrMagnitude>radius*radius)return;
        if(sources.Any(value=>value.StableId==source.StableId))return;sources.Add(source);
    }
    private RebirthLogisticsTransferJournal.Record Header()=>new RebirthLogisticsTransferJournal.Record{Operation=operation,World=RebirthLogisticsTransferJournal.WorldKey(world),Owner=owner.StorageKey,Creation=creation,Epoch=epoch,Request=request,Deaths=deaths,Action=action,Target=target,Selection=selection};
    private static bool[] Locks(IRemoteResourceSource source)
    {
        int length=source.Slots?.Length??0;var locks=source.SlotLocks;
        if(length<1||length>256||locks!=null&&locks.Length!=length)throw new InvalidDataException();var values=new bool[length];for(int i=0;i<length;i++)values[i]=locks!=null&&locks[i];return values;
    }
    private bool Start(IRemoteResourceSource source)
    {
        if(!Current()||source.IsBusy||!source.IsLoaded||!source.IsAuthorized(actor,false,out _))return false;
        if(source is StaticContainerResourceSource container&&container.NativeTile is TileEntityComposite tile)
        {
            native=tile;incarnation=tile.GetFeature<TEFeatureRebirthContainerName>()?.Incarnation;
            var proof=RebirthLogisticsCompositePublication.Begin(world,tile,Current);watch=proof;saved=()=>proof?.Saved()==true;return proof!=null;
        }
        if(source is WorkstationOutputResourceSource workstation)
        {
            native=workstation.NativeStation;var proof=RebirthLogisticsEndpointIdentity.Begin(world,workstation.NativeStation,Current);watch=proof;incarnation=proof?.Id;saved=()=>proof?.Saved()==true;return proof!=null;
        }
        if(source is VehicleResourceSource vehicle)
        {
            native=vehicle.Vehicle;var proof=RebirthLogisticsVehiclePublication.BeginIdentityAdmission(vehicle.Vehicle,Current);watch=proof;incarnation=RebirthLogisticsVehiclePublication.CurrentIncarnation(vehicle.Vehicle);saved=()=>proof!=null&&RebirthLogisticsVehiclePublication.Verify(proof,Current);return proof!=null;
        }
        if(source is DroneResourceSource drone)
        {
            native=drone.Drone;var proof=RebirthLogisticsDronePublication.BeginIdentityAdmission(drone.Drone,Current);watch=proof;incarnation=RebirthLogisticsDronePublication.CurrentIncarnation(drone.Drone);saved=()=>proof!=null&&RebirthLogisticsDronePublication.Verify(proof,Current);return proof!=null;
        }
        if(source is RemoteNpcResourceSource dog&&dog.IsDogStorage)
        {
            native=dog.NativeActor;incarnation=RebirthLogisticsDogTransfer.Identity(dog.NativeIdentity,dog.OriginalRevision);
            RebirthNpcAggregatePersistenceStore.Save();
            var leg=new RebirthLogisticsTransferJournal.Leg{Id=source.StableId,Kind=source.Kind,NativeIdentity=incarnation,Before=ItemStack.Clone(source.Slots),After=ItemStack.Clone(source.Slots),Locks=Locks(source)};
            saved=()=>dog.IsSnapshotCurrent()&&RebirthNpcAggregatePersistenceStore.TryReadSavedLogisticsDog(dog.NativeIdentity,out var row)&&
                RebirthRemoteResourceDogTransfer.Matches(row,source.Slots,Locks(source),dog.OriginalRevision)&&RebirthLogisticsDogTransfer.MayReplaceReceipt(row,Header(),leg,false);return saved();
        }
        return false;
    }
    internal bool Step(out RebirthLogisticsTransferJournal.Record original)
    {
        original=null;
        if(finished){original=result;return result!=null;}
        if(!Current()){CloseWatch();return false;}
        try
        {
            if(cursor<sources.Count)
            {
                var source=sources[cursor];if(saved==null&&!Start(source)){Reject();original=result;return finished;}
                if(saved?.Invoke()!=true)return false;
                if(string.IsNullOrEmpty(incarnation)||native==null)throw new InvalidDataException();
                admitted.Add(new RebirthLogisticsPlanning.Observation{Source=source,NativeObject=native,Identity=incarnation,Items=ItemStack.Clone(source.Slots),Locks=source.SlotLocks});
                CloseWatch();cursor++;return false;
            }
            if(!ReadOwner(out var data))return false;
            if(admitted.Any(observation=>!SameIncarnation(observation)))throw new InvalidDataException("Original native endpoint incarnation changed before plan.");
            var record=Header();var user=GameManager.Instance.GetPersistentPlayerList()?.GetPlayerDataFromEntityID(actor.entityId)?.PrimaryId;if(user==null)throw new InvalidDataException();
            using(var plan=new RebirthLogisticsPlanning(actor,record,data,admitted,logical))
            {LogisticsTransferService.ProcessWithNotifications(world,actor.entityId,user,action,target,selection);plan.Complete();}
            if(!Current()||!ReadOwner(out _)||!RebirthLogisticsPreAdmissionReceipt.BeginPublication(record)||!RebirthLogisticsTransferJournal.TryPublish(record,Current))return false;
            result=record;finished=true;original=result;return true;
        }
        catch{Reject();original=result;return finished;}
    }
    private bool SameIncarnation(RebirthLogisticsPlanning.Observation observation)
    {
        var source=observation.Source;
        if(source is StaticContainerResourceSource container)return ReferenceEquals(container.NativeTile,observation.NativeObject)&&
            (container.NativeTile as TileEntityComposite)?.GetFeature<TEFeatureRebirthContainerName>()?.Incarnation==observation.Identity;
        if(source is WorkstationOutputResourceSource station)return ReferenceEquals(station.NativeStation,observation.NativeObject)&&RebirthLogisticsEndpointIdentity.TryGetIncarnation(world,station.NativeStation,out var id)&&id==observation.Identity;
        if(source is VehicleResourceSource vehicle)return ReferenceEquals(vehicle.Vehicle,observation.NativeObject)&&RebirthLogisticsVehiclePublication.CurrentIncarnation(vehicle.Vehicle)==observation.Identity;
        if(source is DroneResourceSource drone)return ReferenceEquals(drone.Drone,observation.NativeObject)&&RebirthLogisticsDronePublication.CurrentIncarnation(drone.Drone)==observation.Identity;
        if(source is RemoteNpcResourceSource dog)return dog.IsDogStorage&&ReferenceEquals(dog.NativeActor,observation.NativeObject)&&dog.IsSnapshotCurrent()&&RebirthLogisticsDogTransfer.Identity(dog.NativeIdentity,dog.OriginalRevision)==observation.Identity;
        return false;
    }
    // Rejection is an original zero-effect plan, never a copy of a partially
    // composed candidate. Only final native phase5 can later prove it unpaid.
    private void Reject()
    {
        CloseWatch();
        if(!ReadOwner(out var data))return;
        var bag=RebirthGearPlayerDataInventory.ReadSlots(data,true);var belt=RebirthGearPlayerDataInventory.ReadSlots(data,false);if(bag==null||belt==null)return;
        var record=Header();record.OriginalOwnerBag=ItemStack.Clone(bag);record.OriginalOwnerBelt=ItemStack.Clone(belt);
        record.BeforeOwner=bag.Select(_=>ItemStack.Empty.Clone()).ToArray();record.DebitedOwner=ItemStack.Clone(record.BeforeOwner);record.AfterOwner=ItemStack.Clone(record.BeforeOwner);
        if(!RebirthLogisticsPreAdmissionReceipt.BeginPublication(record)||!RebirthLogisticsTransferJournal.TryPublish(record,Current)||!RebirthLogisticsTransferProtocol.PrepareRejection(record,Current,out var rejected))return;
        result=rejected;finished=true;
    }
    internal void RequestCancel(){if(!finished&&!closed)Reject();}
    private void CloseWatch(){watch?.Dispose();watch=null;saved=null;native=null;incarnation=null;}
    public void Dispose(){CloseWatch();closed=true;}
}

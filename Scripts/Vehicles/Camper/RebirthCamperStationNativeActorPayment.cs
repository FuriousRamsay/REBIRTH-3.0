using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Xml.Linq;
using HarmonyLib;

// Native-mode write-ahead actor payment. Journal publishes through the existing
// durable transfer writer; final native owner and vehicle files prove settlement.
internal static class RebirthCamperStationNativeActorPayment
{
    private const string ReceiptPrefix="rbCamperNativePayment_";
    private sealed class Pending
    {
        internal EntityPlayerLocal Player,ReservedPlayer;internal EntityPlayer Actor;internal ClientInfo Sender;
        internal RebirthStablePlayerIdentity Identity;internal RebirthCamperStationState Station;
        internal RebirthCamperStationAddress Address;internal object Lease;internal string Creation,Directory;
        internal RebirthCamperTransferJournal Journal;internal RebirthCamperTransferJournal.Record Record;
        internal Frame Before,After;internal RebirthCamperSavedVehicle.Expected VehicleSave,BeforeVehicleSave;
        internal float Next;
        internal bool RemoteSent,RemoteConfirmed,RetirementSent;internal byte[] RemotePayload;
    }
    private sealed class Frame
    {
        internal Guid Station;internal string Owner,OwnerKey,WorldKey;internal int QueueSlot,OwnedBelt;internal bool Remote;internal ItemStack[] Bag,Belt;
        internal bool[] BagLocks,BeltLocks;internal byte[] StationImage;
    }
    private sealed class Permit{internal object Inventory,Value;internal int Slot,Count;internal bool Used;}
    private static Permit permit;
    private static readonly Dictionary<RebirthCamperStationState,Pending> pending=new Dictionary<RebirthCamperStationState,Pending>();
    private static readonly Dictionary<Guid,Pending> retirements=new Dictionary<Guid,Pending>();
    private sealed class ClientHold
    {internal EntityPlayerLocal Player;internal World World;internal object Peer;internal string Creation;internal Frame Before,After;internal Guid Original;internal RebirthCamperStationAddress Address;internal byte[] Payload;internal bool ApplyingSaved;}
    private static readonly Dictionary<Guid,ClientHold> clientHolds=new Dictionary<Guid,ClientHold>();
    private sealed class RecoveryScope{internal World World;internal string Directory;}
    private static ConditionalWeakTable<EntityVehicle,RecoveryScope> recovered=new ConditionalWeakTable<EntityVehicle,RecoveryScope>();
    internal static bool IsInstalled{get;private set;}
    internal static void Install()
    {
        if(IsInstalled)return;
        RebirthHarmonyBootstrap.PatchClassOnce(new Harmony("rebirth.camper.native.actor-payment"),typeof(RebirthCamperNativeActorMutationGuard));
        RebirthHarmonyBootstrap.PatchClassOnce(new Harmony("rebirth.camper.native.actor-payment"),typeof(RebirthCamperNativeActorQueueGuard));
        RebirthHarmonyBootstrap.PatchClassOnce(new Harmony("rebirth.camper.native.actor-payment"),typeof(RebirthCamperNativeActorCursorGuard));
        ModEvents.GameUpdate.RegisterHandler(Tick);
        ModEvents.WorldShuttingDown.RegisterHandler(Shutdown);IsInstalled=true;
    }
    internal static bool IsHeld(RebirthCamperStationState station)=>station!=null&&pending.Values.Any(p=>ReferenceEquals(p.Station.BoundVehicle,station.BoundVehicle));
    internal static bool Blocks(object inventory)=>pending.Values.Any(p=>p.ReservedPlayer!=null&&(ReferenceEquals(p.ReservedPlayer.bag,inventory)||ReferenceEquals(p.ReservedPlayer.inventory,inventory)))||
        clientHolds.Values.Any(p=>ReferenceEquals(p.Player.bag,inventory)||ReferenceEquals(p.Player.inventory,inventory));
    internal static bool MatchesPendingOperation(RebirthCamperStationState station,Guid operation)=>station!=null&&pending.TryGetValue(station,out var p)&&p.Record.Operation==operation;
    private static bool OtherOwnerHold(EntityPlayerLocal player)=>RebirthBackpackLibraryReservation.IsHeld(player)||RebirthGearOwnerReservation.IsHeld(player)||RemoteResourceClientTransactionCoordinator.HasPendingInventoryOperation(player);
    internal static void Suspend(RebirthCamperStationState station)
    {if(station!=null&&pending.TryGetValue(station,out var p)){p.Player=null;p.Actor=null;p.Lease=null;}}
    internal static bool BeforeMutation(object inventory,MethodBase method,object[] args)
    {
        if(!Blocks(inventory))return true;
        var p=permit;
        if(p==null||p.Used||!ReferenceEquals(p.Inventory,inventory)||args==null||args.Length<2||!(args[0] is int slot)||slot!=p.Slot)return false;
        bool exact=method.Name=="SetSlot"&&args[1] is ItemStack stack&&ReferenceEquals(stack,p.Value)&&stack.count==p.Count||
            method.Name=="SetItem"&&args.Length==3&&ReferenceEquals(args[1],p.Value)&&args[2] is int count&&count==p.Count;
        if(exact)p.Used=true;return exact;
    }
    internal static bool[] Locks(PackedBoolArray values)
    {if(values==null)return null;var result=new bool[values.Length];for(int i=0;i<result.Length;i++)result[i]=values[i];return result;}
    private static bool Same(ItemStack[] a,ItemStack[] b)
    {if(a==null||b==null||a.Length!=b.Length)return false;for(int i=0;i<a.Length;i++)if(!RebirthStationGridIngredients.IsSameStackSnapshot(a[i],b[i]))return false;return true;}
    private static bool Equal(byte[] a,byte[] b)=>a!=null&&b!=null&&a.SequenceEqual(b);
    private static bool SameLocks(bool[] a,bool[] b)=>a==null||b==null?a==b:a.SequenceEqual(b);
    private static XElement Cells(string name,ItemStack[] cells)=>new XElement(name,cells.Select(s=>new XElement("slot",new XAttribute("count",s.count),new XAttribute("item",RebirthNativeItemCodec.Encode(s.itemValue)))));
    private static XElement LockImage(string name,bool[] locks)=>new XElement(name,new XAttribute("null",locks==null),locks==null?"":string.Concat(locks.Select(x=>x?"1":"0")));
    private static byte[] Encode(Frame f)=>Encoding.UTF8.GetBytes(new XElement("nativeActorPayment",new XAttribute("version",1),new XAttribute("station",f.Station.ToString("N")),new XAttribute("owner",f.Owner),new XAttribute("ownerKey",f.OwnerKey),new XAttribute("world",f.WorldKey),new XAttribute("queueSlot",f.QueueSlot),new XAttribute("ownedBelt",f.OwnedBelt),new XAttribute("remote",f.Remote),
        Cells("bag",f.Bag),Cells("belt",f.Belt),LockImage("bagLocks",f.BagLocks),LockImage("beltLocks",f.BeltLocks),new XElement("stationImage",Convert.ToBase64String(f.StationImage))).ToString(SaveOptions.DisableFormatting));
    private static ItemStack[] ReadCells(XElement node,int maximum)
    {
        var rows=node.Elements("slot").ToArray();if(rows.Length>maximum)throw new InvalidDataException();
        var result=new ItemStack[rows.Length];
        for(int i=0;i<rows.Length;i++)
        {
            int count=(int)rows[i].Attribute("count");string image=(string)rows[i].Attribute("item");
            if(count<0||count>ushort.MaxValue)throw new InvalidDataException();
            ItemValue value;
            if(!RebirthNativeItemCodec.TryDecode(image,out value))
            {
                // Canonical codec's nonempty admission is intentionally stricter
                // than native empty cells. Empty images still reencode exactly.
                if(count!=0)throw new InvalidDataException();var bytes=Convert.FromBase64String(image);value=new ItemValue();
                using(var stream=new MemoryStream(bytes,false))using(var reader=MemoryPools.poolBinaryReader.AllocSync(true))
                {reader.SetBaseStream(stream);value.Read(reader);if(stream.Position!=stream.Length||!value.IsEmpty())throw new InvalidDataException();}
                if(RebirthNativeItemCodec.Encode(value)!=image)throw new InvalidDataException();
            }
            result[i]=new ItemStack(value,count);
        }
        return result;
    }
    private static bool[] ReadLocks(XElement e)=> (bool)e.Attribute("null")?null:e.Value.Select(c=>c=='1'?true:c=='0'?false:throw new InvalidDataException()).ToArray();
    private static Frame Decode(byte[] bytes)
    {
        if(bytes==null||bytes.Length>8*1024*1024)throw new InvalidDataException();var e=XElement.Parse(Encoding.UTF8.GetString(bytes));
        if(e.Name!="nativeActorPayment"||(int)e.Attribute("version")!=1)throw new InvalidDataException();
        var f=new Frame{Station=Guid.ParseExact((string)e.Attribute("station"),"N"),Owner=(string)e.Attribute("owner"),OwnerKey=(string)e.Attribute("ownerKey"),WorldKey=(string)e.Attribute("world"),QueueSlot=(int)e.Attribute("queueSlot"),OwnedBelt=(int)e.Attribute("ownedBelt"),Remote=(bool)e.Attribute("remote"),Bag=ReadCells(e.Element("bag"),169),Belt=ReadCells(e.Element("belt"),20),
            BagLocks=ReadLocks(e.Element("bagLocks")),BeltLocks=ReadLocks(e.Element("beltLocks")),StationImage=Convert.FromBase64String(e.Element("stationImage").Value)};
        if(f.Station==Guid.Empty||f.QueueSlot<0||f.QueueSlot>254||f.OwnedBelt<0||f.OwnedBelt>f.Belt.Length||string.IsNullOrEmpty(f.Owner)||string.IsNullOrEmpty(f.OwnerKey)||string.IsNullOrEmpty(f.WorldKey)||!Equal(Encode(f),bytes))throw new InvalidDataException();return f;
    }
    private static RebirthCamperTransferJournal Journal()=>new RebirthCamperTransferJournal(Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH","NativeActorPayments"));
    internal static bool RestorePending(EntityVehicle vehicle)
    {
        if(!IsInstalled||vehicle?.world==null||vehicle.world.IsRemote()||!RebirthCamperModuleRuntime.TryGetExisting(vehicle,out var runtime))return false;
        string save=GameIO.GetSaveGameDir();
        if(recovered.TryGetValue(vehicle,out var checkedScope)&&ReferenceEquals(checkedScope.World,vehicle.world)&&checkedScope.Directory==save)return true;
        string directory=Path.Combine(save,"REBIRTH","NativeActorPayments");
        if(!System.IO.Directory.Exists(directory)){recovered.Remove(vehicle);recovered.Add(vehicle,new RecoveryScope{World=vehicle.world,Directory=save});return true;}
        var journal=new RebirthCamperTransferJournal(directory);var record=journal.FindPending(runtime.Identity);
        if(record==null){recovered.Remove(vehicle);recovered.Add(vehicle,new RecoveryScope{World=vehicle.world,Directory=save});return true;}
        var before=Decode(record.Before);var after=Decode(record.After);var station=runtime.Stations.Find(before.Station)?.State;
        if(station==null||after.Station!=before.Station||before.QueueSlot!=after.QueueSlot||before.QueueSlot>=station.Queue.Length||before.Owner!=after.Owner||before.WorldKey!=after.WorldKey||before.WorldKey!=vehicle.world.worldState?.Guid)return false;
        if(!pending.ContainsKey(station))pending.Add(station,new Pending{Station=station,Address=new RebirthCamperStationAddress(runtime.Identity,station.InstallationId),Creation=record.OwnerCreation,
            Directory=save,Journal=journal,Record=record,Before=before,After=after});
        recovered.Remove(vehicle);recovered.Add(vehicle,new RecoveryScope{World=vehicle.world,Directory=save});return true;
    }
    internal static bool TryResume(EntityPlayer player,RebirthCamperStationState station,RebirthCamperStationAddress address,string creation)
    {
        if(player!=null&&!(player is EntityPlayerLocal)&&pending.TryGetValue(station,out var remote))
        {
            if(!IsInstalled||!remote.Before.Remote||!remote.Address.Equals(address)||remote.Creation!=creation||
                !RemoteIdentity(player,out var identity,out var sender)||identity.CanonicalId!=remote.Before.Owner||identity.StorageKey!=remote.Before.OwnerKey||
                !RebirthStationLiveAccess.TryGetMobileLease(player,address,creation,out var live,out var remoteLease)||!ReferenceEquals(live,station))return false;
            if(remote.Actor!=null&&(!ReferenceEquals(remote.Actor,player)||!ReferenceEquals(remote.Lease,remoteLease)||!ReferenceEquals(remote.Sender,sender)))return false;
            remote.Actor=player;remote.Identity=identity;remote.Sender=sender;remote.Lease=remoteLease;station.lastTickTime=0;Advance(remote);return true;
        }
        if(!IsInstalled||!(player is EntityPlayerLocal local)||!pending.TryGetValue(station,out var p)||!p.Address.Equals(address)||p.Creation!=creation||
            GameManager.Instance?.World?.GetPrimaryPlayer()!=local||!RebirthStationLiveAccess.TryGetMobileLease(player,address,creation,out var current,out var lease)||!ReferenceEquals(current,station))return false;
        if(p.Player!=null&&(!ReferenceEquals(p.Player,local)||!ReferenceEquals(p.Lease,lease)))return false;
        if(GameManager.Instance.getPersistentPlayerID(null)?.CombinedString!=p.Before.Owner)return false;
        if(!RebirthStablePlayerIdentity.TryFromLocalPlatform(out var localIdentity)||localIdentity.CanonicalId!=p.Before.Owner||localIdentity.StorageKey!=p.Before.OwnerKey)return false;
        p.Player=local;p.Actor=local;p.Identity=localIdentity;p.ReservedPlayer=local;p.Lease=lease;station.lastTickTime=0;Advance(p);return true;
    }
    internal static bool Begin(EntityPlayer player,RebirthCamperStationState station,RebirthCamperStationAddress address,string creation,Guid operation,
        ItemStack[] beforeBag,ItemStack[] afterBag,ItemStack[] beforeBelt,ItemStack[] afterBelt,RecipeQueueItem[] afterQueue,RebirthStablePlayerIdentity remoteOwner=null,bool[] remoteLocks=null)
    {
        var local=player as EntityPlayerLocal;bool remote=remoteOwner!=null;ClientInfo sender=null;RebirthStablePlayerIdentity identity=remoteOwner;
        if(!IsInstalled||player==null||operation==Guid.Empty||IsHeld(station)||
            !RebirthStationLiveAccess.TryGetMobileLease(player,address,creation,out var current,out var lease)||!ReferenceEquals(current,station))return false;
        if(remote)
        {
            if(!RebirthCamperRemoteStationPlayerSource.IsCurrent(player,station)||!RemoteIdentity(player,out var peer,out sender)||
                peer.CanonicalId!=identity.CanonicalId||peer.StorageKey!=identity.StorageKey||
                !RebirthCamperStationPlayerSavedSource.TryCopySavedSource(identity,out var b,out var t,out var locks)||!Same(beforeBag,b)||!Same(beforeBelt,t)||!SameLocks(remoteLocks,locks))return false;
        }
        else if(local==null||OtherOwnerHold(local)||local.PlayerUI?.xui?.DragAndDropWindow?.IsEmpty()!=true||!RebirthStationPlayerIngredients.UsesPlayerPayment(player,station)||!RebirthStablePlayerIdentity.TryFromLocalPlatform(out identity)||
            !Same(beforeBag,player.bag.ItemGrid.items)||!Same(beforeBelt,player.inventory.ItemGrid.items)||GameManager.Instance.getPersistentPlayerID(null)?.CombinedString!=identity.CanonicalId)return false;
        string owner=identity.CanonicalId;
        if(afterQueue==null||station.Queue==null||afterQueue.Length!=station.Queue.Length)return false;
        var changed=Enumerable.Range(0,afterQueue.Length).Where(i=>!ReferenceEquals(afterQueue[i],station.Queue[i])).ToArray();
        if(changed.Length!=1)return false;int queueSlot=changed[0];
        var installed=RebirthCamperModuleRuntime.For(station.BoundVehicle).Stations.Find(station.InstallationId).InstalledItem;
        string worldKey=player.world.worldState?.Guid;if(string.IsNullOrEmpty(worldKey))return false;
        // Same zero-baseline used by every existing custody clock gate. No paid
        // batch/timer advancement is allowed while the coupled save is pending.
        station.lastTickTime=0;
        var image=RebirthCamperStationSnapshot.Capture(station.InstallationId,station);
        var candidate=RebirthCamperStationSnapshot.Restore(station.InstallationId,installed,image);
        // Native detached hydration may omit paint textures. Preserve all stock
        // images from the original; the candidate changes only queue/revision.
        candidate.Tools=station.Tools.Select(s=>s.Clone()).ToArray();candidate.Fuel=station.Fuel.Select(s=>s.Clone()).ToArray();
        candidate.Input=station.Input.Select(s=>s.Clone()).ToArray();candidate.Output=station.Output.Select(s=>s.Clone()).ToArray();
        candidate.lastInput=station.lastInput.Select(s=>s.Clone()).ToArray();
        candidate.CraftCompleteList=station.CraftCompleteList==null?null:new List<CraftCompleteData>(station.CraftCompleteList);
        candidate.Queue=afterQueue;candidate.lastTickTime=0;candidate.setModified();
        var before=new Frame{Station=station.InstallationId,QueueSlot=queueSlot,Owner=owner,OwnerKey=identity.StorageKey,OwnedBelt=player.inventory.Length,Remote=remote,WorldKey=worldKey,Bag=beforeBag,Belt=beforeBelt,BagLocks=remote?remoteLocks:Locks(player.bag.LockedSlots),BeltLocks=remote?null:Locks(player.inventory.ItemGrid.SlotLocks),StationImage=image};
        var after=new Frame{Station=station.InstallationId,QueueSlot=queueSlot,Owner=owner,OwnerKey=before.OwnerKey,OwnedBelt=before.OwnedBelt,Remote=remote,WorldKey=worldKey,Bag=afterBag,Belt=afterBelt,BagLocks=before.BagLocks,BeltLocks=before.BeltLocks,StationImage=RebirthCamperStationSnapshot.Capture(station.InstallationId,candidate)};
        byte[] beforeBytes=Encode(before),afterBytes=Encode(after);
        if(beforeBytes.Length>8*1024*1024||afterBytes.Length>8*1024*1024||remote&&((long)beforeBytes.Length+afterBytes.Length)*4/3+4096>8*1024*1024)return false;
        // Both durable images must be readable before taking any native custody.
        if(!Equal(Encode(Decode(beforeBytes)),beforeBytes)||!Equal(Encode(Decode(afterBytes)),afterBytes))return false;
        var journal=Journal();if(journal.FindPending(address.Vehicle)!=null||journal.Read(operation)!=null)return false;
        var record=journal.Prepare(operation,address.Vehicle,creation,station.Revision,beforeBytes,afterBytes);
        var p=new Pending{Player=local,ReservedPlayer=local,Actor=player,Identity=identity,Sender=sender,Station=station,Address=address,Lease=lease,Creation=creation,Directory=GameIO.GetSaveGameDir(),Journal=journal,Record=record,Before=before,After=after};
        pending.Add(station,p);Advance(p);return true; // Accepted durable intent, not completion.
    }
    internal static bool RemoteIdentity(EntityPlayer player,out RebirthStablePlayerIdentity identity,out ClientInfo sender)
    {
        identity=null;sender=null;var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;
        if(!ThreadManager.IsMainThread()||player?.world==null||player.world.IsRemote()||manager==null||!manager.IsServer||manager.Clients==null)return false;
        sender=manager.Clients.ForEntityId(player.entityId);
        return sender!=null&&RebirthStablePlayerIdentity.TryFromClientInfo(sender,out identity)&&identity!=null;
    }
    internal static bool CraftPlayer(EntityPlayer player,RebirthCamperStationAddress address,string creation,Guid job,Recipe recipe,int batches,int tier)
    {
        if(!IsInstalled||RebirthSurvivorMode.IsEnabledForCurrentWorld()||
            !RebirthStationLiveAccess.TryGetMobileLease(player,address,creation,out var station,out _)||
            !RebirthCamperRemoteStationPlayerSource.IsCurrent(player,station)||!RemoteIdentity(player,out var identity,out _)||
            creation!=RebirthCamperCharacterAccess.CurrentCreation(player))return false;
        if(pending.TryGetValue(station,out var original))return original.Record.Operation==job&&TryResume(player,station,address,creation);
        if(!RebirthCamperStationPlayerSavedSource.TryCopySavedSource(identity,out var bag,out var belt,out var locks)||
            !RebirthStationPlayerIngredients.TryPlan(player,recipe,batches,tier,bag,belt,locks,null,player.inventory.Length,out var plan)||
            !RebirthCamperStationNativeCrafting.BuildActorQueue(player,station,job,recipe,batches,tier,plan,identity.CanonicalId,out var queue)||
            !plan.TryGetImages(out var oldBag,out var newBag,out var oldBelt,out var newBelt))return false;
        return Begin(player,station,address,creation,job,oldBag,newBag,oldBelt,newBelt,queue,identity,locks);
    }
    private static bool Current(Pending p)=>p.Before.Remote?CurrentRemote(p):ThreadManager.IsMainThread()&&p.Player!=null&&p.Directory==GameIO.GetSaveGameDir()&&
        GameManager.Instance?.World==p.Player.world&&!p.Player.world.IsRemote()&&p.Player.world.GetPrimaryPlayer()==p.Player&&p.Player.world.GetEntity(p.Player.entityId)==p.Player&&
        p.Before.WorldKey==p.Player.world.worldState?.Guid&&
        p.Player.IsSpawned()&&!p.Player.IsDead()&&GameManager.Instance.getPersistentPlayerID(null)?.CombinedString==p.Before.Owner&&
        p.Creation==RebirthCamperCharacterAccess.CurrentCreation(p.Player)&&
        RebirthStationLiveAccess.TryGetMobileLease(p.Player,p.Address,p.Creation,out var s,out var lease)&&ReferenceEquals(s,p.Station)&&ReferenceEquals(lease,p.Lease)&&
        SameLocks(p.Before.BagLocks,Locks(p.Player.bag.LockedSlots))&&SameLocks(p.Before.BeltLocks,Locks(p.Player.inventory.ItemGrid.SlotLocks));
    private static bool CurrentRemote(Pending p)=>ThreadManager.IsMainThread()&&p.Actor!=null&&p.Actor.world!=null&&!p.Actor.world.IsRemote()&&
        p.Directory==GameIO.GetSaveGameDir()&&ReferenceEquals(GameManager.Instance?.World,p.Actor.world)&&
        ReferenceEquals(p.Actor.world.GetEntity(p.Actor.entityId),p.Actor)&&!p.Actor.IsDead()&&p.Actor.IsSpawned()&&
        p.Before.WorldKey==p.Actor.world.worldState?.Guid&&p.Creation==RebirthCamperCharacterAccess.CurrentCreation(p.Actor)&&
        RemoteIdentity(p.Actor,out var identity,out var sender)&&ReferenceEquals(sender,p.Sender)&&identity.CanonicalId==p.Before.Owner&&identity.StorageKey==p.Before.OwnerKey&&
        RebirthStationLiveAccess.TryGetMobileLease(p.Actor,p.Address,p.Creation,out var station,out var lease)&&ReferenceEquals(station,p.Station)&&ReferenceEquals(lease,p.Lease);
    private static bool Compatible(ItemStack[] actual,ItemStack[] before,ItemStack[] after)
    {if(actual.Length!=before.Length||after.Length!=before.Length)return false;for(int i=0;i<actual.Length;i++)if(!RebirthStationGridIngredients.IsSameStackSnapshot(actual[i],before[i])&&!RebirthStationGridIngredients.IsSameStackSnapshot(actual[i],after[i]))return false;return true;}
    private static bool OwnerSaved(Pending p,bool applied=true)
    {
        var expected=applied?p.After:p.Before;
        var owner=p.Identity;
        if(!Current(p)||owner==null||owner.CanonicalId!=p.Before.Owner||
            !RebirthGearNativePlayerFile.TryRead(owner,out var data)||!Same(RebirthGearPlayerDataInventory.ReadSlots(data,true),expected.Bag)||
            !Same(RebirthGearPlayerDataInventory.ReadSlots(data,false),expected.Belt))return false;
        if(data.buffData==null||data.buffData.Length>1024*1024)return false;
        using(var stream=new MemoryStream(data.buffData.ToArray(),false))using(var reader=MemoryPools.poolBinaryReader.AllocSync(true))
        {
            reader.SetBaseStream(stream);int version=reader.ReadByte();if(version!=EntityBuffs.Version||version<2)return false;
            int buffs=reader.ReadUInt16();for(int i=0;i<buffs;i++)new BuffValue().Read(reader,version);
            int variables=reader.ReadUInt16();bool found=false;string receipt=ReceiptPrefix+p.Record.Operation.ToString("N");
            for(int i=0;i<variables;i++){string name=reader.ReadString();float value=reader.ReadSingle();if(name==receipt){if(found||value!=(applied?1f:2f))return false;found=true;}}
            return (p.Before.Remote&&!applied||found)&&stream.Position==stream.Length&&Current(p);
        }
    }
    private static void WriteArea(Pending p,bool bag)
    {
        var desired=bag?p.After.Bag:p.After.Belt;
        for(int i=0;i<desired.Length;i++)
        {
            if(!Current(p)||!Compatible(p.Player.bag.ItemGrid.items,p.Before.Bag,p.After.Bag)||!Compatible(p.Player.inventory.ItemGrid.items,p.Before.Belt,p.After.Belt))throw new InvalidOperationException("Native actor source changed.");
            var actual=bag?p.Player.bag.ItemGrid.items:p.Player.inventory.ItemGrid.items;
            if(RebirthStationGridIngredients.IsSameStackSnapshot(actual[i],desired[i]))continue;
            var value=desired[i].Clone();permit=new Permit{Inventory=bag?(object)p.Player.bag:p.Player.inventory,Slot=i,Count=value.count,Value=bag?(object)value:value.itemValue};
            try{if(bag)p.Player.bag.SetSlot(i,value);else p.Player.inventory.SetItem(i,value.itemValue,value.count);}
            finally{permit=null;}
        }
    }
    private static void Advance(Pending p)
    {
        try
        {
            if(!Current(p)||UnityEngine.Time.realtimeSinceStartup<p.Next)return;
            p.Next=UnityEngine.Time.realtimeSinceStartup+.25f;
            var record=p.Journal.Read(p.Record.Operation);if(record==null)return;p.Record=record;
            var live=RebirthCamperStationSnapshot.Capture(p.Station.InstallationId,p.Station);
            bool before=Equal(live,p.Before.StationImage),after=Equal(live,p.After.StationImage);
            if(record.State==RebirthCamperTransferJournal.Phase.Prepared)
            {
                bool sourceBefore=p.Before.Remote?RebirthCamperStationPlayerSavedSource.TryCopySavedSource(p.Identity,out var rb,out var rt,out var rl)&&Same(p.Before.Bag,rb)&&Same(p.Before.Belt,rt)&&SameLocks(p.Before.BagLocks,rl):
                    Same(p.Before.Bag,p.Player.bag.ItemGrid.items)&&Same(p.Before.Belt,p.Player.inventory.ItemGrid.items);
                if(!before||!sourceBefore)
                {
                    // No owner setter is permitted before Uncertain. A saved
                    // Prepared record therefore proves this original paid nothing.
                    p.Journal.Advance(record.Operation,record.State,RebirthCamperTransferJournal.Phase.Rejected);
                    if(Current(p)&&p.Journal.Read(record.Operation)?.State==RebirthCamperTransferJournal.Phase.Rejected)pending.Remove(p.Station);
                    return;
                }
                // Persist original station/cargo BEFORE any owner setter, so a
                // crash cannot reload an older vehicle while its owner is debited.
                if(p.BeforeVehicleSave==null)
                {
                    RebirthCamperModuleRuntime.For(p.Station.BoundVehicle).Persist(true);
                    p.BeforeVehicleSave=RebirthCamperSavedVehicle.RequestSave(p.Station.BoundVehicle);
                }
                if(!RebirthCamperSavedVehicle.Verify(p.Station.BoundVehicle,p.BeforeVehicleSave,()=>Current(p)))return;
                if(!OwnerSaved(p,false))
                {
                    if(p.Before.Remote)return;
                    float marker=p.Player.Buffs.GetCustomVar(ReceiptPrefix+record.Operation.ToString("N"));
                    if(marker!=0f&&marker!=2f)return;
                    p.Player.Buffs.SetCustomVar(ReceiptPrefix+record.Operation.ToString("N"),2f,true);
                    GameManager.Instance.SaveLocalPlayerData();if(!OwnerSaved(p,false)||!Current(p))return;
                }
                p.Record=p.Journal.Advance(record.Operation,record.State,RebirthCamperTransferJournal.Phase.Uncertain);}
            if(!before&&!after)return;
            if(p.Record.State!=RebirthCamperTransferJournal.Phase.Uncertain)return;
            if(before)
            {
                if(p.Before.Remote)
                {
                    if(!OwnerSaved(p))
                    {
                        if(!p.RemoteSent)
                        {
                            p.RemotePayload=RemotePayload(p,false);
                            p.RemoteSent=RebirthCamperRemoteOwnerApplication.SendOwnerApplication(p.Actor,p.Record.Operation,RebirthCamperRemoteOwnerEffectKind.Debit,p.RemotePayload,
                                success=>{p.RemoteSent=false;p.RemoteConfirmed=success;});
                        }
                        return;
                    }
                }
                else
                {
                    WriteArea(p,true);WriteArea(p,false);
                    if(!Current(p)||!Same(p.After.Bag,p.Player.bag.ItemGrid.items)||!Same(p.After.Belt,p.Player.inventory.ItemGrid.items))return;
                    p.Player.Buffs.SetCustomVar(ReceiptPrefix+p.Record.Operation.ToString("N"),1f,true);
                    GameManager.Instance.SaveLocalPlayerData();if(!OwnerSaved(p)||!Current(p))return;
                }
                var installed=RebirthCamperModuleRuntime.For(p.Station.BoundVehicle).Stations.Find(p.Station.InstallationId).InstalledItem;
                var candidate=RebirthCamperStationSnapshot.Restore(p.Station.InstallationId,installed,p.After.StationImage);
                // Retain every unrelated live native row; only the prepared row
                // changes. Rebind actor fragments to their canonical full image.
                var changedRow=candidate.Queue[p.After.QueueSlot];
                if(!RebirthCamperStationNativeCrafting.RestoreActorImages(changedRow?.Recipe))return;
                var queue=(RecipeQueueItem[])p.Station.Queue.Clone();queue[p.After.QueueSlot]=changedRow;
                p.Station.Queue=queue;p.Station.lastTickTime=0;p.Station.RestoreRevision(candidate.Revision);
                RebirthCamperModuleRuntime.For(p.Station.BoundVehicle).Persist(true);
            }
            if(!Current(p)||!OwnerSaved(p))return;
            if(p.VehicleSave==null)p.VehicleSave=RebirthCamperSavedVehicle.RequestSave(p.Station.BoundVehicle);
            if(!RebirthCamperSavedVehicle.Verify(p.Station.BoundVehicle,p.VehicleSave,()=>Current(p))||!Current(p))return;
            p.Journal.Advance(p.Record.Operation,RebirthCamperTransferJournal.Phase.Uncertain,RebirthCamperTransferJournal.Phase.Applied);
            pending.Remove(p.Station);p.Station.ResetTickTime();
            if(p.Before.Remote){retirements[p.Record.Operation]=p;SendRetirement(p);}
            else foreach(var group in p.Player.PlayerUI.xui.WindowGroups)
                    if(group?.isShowing==true&&group.Controller is XUiC_WorkstationWindowGroup window&&ReferenceEquals(window.WorkstationData?.TileEntity,p.Station))
                        RebirthCamperStationQueueProjection.Refresh(window,p.Station);
        }
        catch(Exception error){Log.Warning("[CamperNativePayment] Original intent remains pending: "+error.GetType().Name);}
    }
    private static void Tick(ref ModEvents.SGameUpdateData data)
    {if(!ThreadManager.IsMainThread())return;foreach(var p in pending.Values.ToArray())Advance(p);foreach(var p in retirements.Values.ToArray())SendRetirement(p);}
    private static void Shutdown(ref ModEvents.SWorldShuttingDownData data){pending.Clear();retirements.Clear();clientHolds.Clear();permit=null;recovered=new ConditionalWeakTable<EntityVehicle,RecoveryScope>();} // Durable journal retained, never refund/replay.
    private const string RetiredPrefix="rbCamperNativeRetired_";
    private static Guid RetirementOperation(Guid original)
    {byte[] bytes;using(var hash=System.Security.Cryptography.SHA256.Create())bytes=hash.ComputeHash(Encoding.UTF8.GetBytes(original.ToString("N")+":retired"));return new Guid(bytes.Take(16).ToArray());}
    private static byte[] RemotePayload(Pending p,bool retired)
    {
        var node=new XElement("camperNativeActorPayment",new XAttribute("version",1),new XAttribute("actor",p.Actor.entityId),new XAttribute("original",p.Record.Operation.ToString("N")),
            new XAttribute("retired",retired),new XAttribute("creation",p.Creation),new XAttribute("address",p.Address.ToString()),
            new XElement("before",Convert.ToBase64String(p.Record.Before)),new XElement("after",Convert.ToBase64String(p.Record.After)));
        var bytes=Encoding.UTF8.GetBytes(node.ToString(SaveOptions.DisableFormatting));RebirthCamperRemoteTransport.AssertFrame(bytes);return bytes;
    }
    internal static bool LooksNativePayload(byte[] bytes)
    {try{return bytes!=null&&bytes.Length>0&&bytes.Length<=RebirthCamperRemoteTransport.MaximumFrame&&ReadPayloadRoot(bytes).Name=="camperNativeActorPayment";}catch{return false;}}
    private static XElement ReadPayloadRoot(byte[] bytes)
    {
        if(bytes==null||bytes.Length<1||bytes.Length>RebirthCamperRemoteTransport.MaximumFrame)throw new InvalidDataException();
        using(var memory=new MemoryStream(bytes,false))using(var xml=System.Xml.XmlReader.Create(memory,new System.Xml.XmlReaderSettings{DtdProcessing=System.Xml.DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=RebirthCamperRemoteTransport.MaximumFrame}))return XElement.Load(xml);
    }
    private static bool ReadPayload(byte[] bytes,int actor,Guid effect,out ClientHold hold,out bool retired)
    {
        hold=null;retired=false;
        try
        {
            var e=ReadPayloadRoot(bytes);
            if(e.Name!="camperNativeActorPayment"||e.Attributes().Count()!=6||(int?)e.Attribute("version")!=1||(int?)e.Attribute("actor")!=actor||
                e.Elements().Count()!=2||e.Elements("before").Count()!=1||e.Elements("after").Count()!=1||
                !Guid.TryParseExact((string)e.Attribute("original"),"N",out var original)||original==Guid.Empty||
                !RebirthCamperStationAddress.TryParse((string)e.Attribute("address"),out var address))return false;
            retired=(bool)e.Attribute("retired");if(effect!=(retired?RetirementOperation(original):original))return false;
            var before=Decode(Convert.FromBase64String(e.Element("before").Value));var after=Decode(Convert.FromBase64String(e.Element("after").Value));
            if(!before.Remote||!after.Remote||before.Station!=address.Installation||after.Station!=before.Station||before.QueueSlot!=after.QueueSlot||
                before.Owner!=after.Owner||before.OwnerKey!=after.OwnerKey||before.WorldKey!=after.WorldKey||before.OwnedBelt!=after.OwnedBelt||
                !SameLocks(before.BagLocks,after.BagLocks)||!SameLocks(before.BeltLocks,after.BeltLocks)||before.Bag.Length!=after.Bag.Length||before.Belt.Length!=after.Belt.Length||
                (string)e.Attribute("creation")!="native-mode:"+before.Owner)return false;
            hold=new ClientHold{Original=original,Address=address,Creation=(string)e.Attribute("creation"),Before=before,After=after,Payload=(byte[])bytes.Clone()};return true;
        }
        catch{return false;}
    }
    private static bool ClientCurrent(ClientHold p)
    {
        var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;var peers=manager?.connectionToServer;
        return ThreadManager.IsMainThread()&&p.Player!=null&&p.World!=null&&p.World.IsRemote()&&ReferenceEquals(GameManager.Instance?.World,p.World)&&
            ReferenceEquals(p.Player.world,p.World)&&ReferenceEquals(p.World.GetPrimaryPlayer(),p.Player)&&ReferenceEquals(p.World.GetEntity(p.Player.entityId),p.Player)&&
            p.Player.IsSpawned()&&!p.Player.IsDead()&&!OtherOwnerHold(p.Player)&&!RebirthSurvivorMode.IsEnabledForCurrentWorld()&&
            p.Creation==RebirthCamperCharacterScope.ResolveLocal(p.Player)&&p.Player.inventory.Length==p.Before.OwnedBelt&&
            Guid.TryParse(GamePrefs.GetString(EnumGamePrefs.GameGuidClient),out var actualWorld)&&Guid.TryParse(p.Before.WorldKey,out var expectedWorld)&&actualWorld==expectedWorld&&
            peers!=null&&peers.Length>0&&ReferenceEquals(peers[0],p.Peer)&&peers[0]!=null&&!peers[0].IsDisconnected()&&
            SameLocks(p.Before.BagLocks,Locks(p.Player.bag.LockedSlots))&&SameLocks(p.Before.BeltLocks,Locks(p.Player.inventory.ItemGrid.SlotLocks))&&
            p.Player.PlayerUI?.xui?.DragAndDropWindow?.IsEmpty()==true;
    }
    private static bool SavedFrame(RebirthStablePlayerIdentity identity,Frame expected,Guid operation,string prefix,float marker,bool exact,PlayerDataFile checkpoint=null)
    {
        var saved=checkpoint;
        if(identity==null||identity.CanonicalId!=expected.Owner||identity.StorageKey!=expected.OwnerKey||saved==null&&!RebirthGearNativePlayerFile.TryRead(identity,out saved)||
            saved.buffData==null||saved.buffData.Length>1024*1024||exact&&(!Same(RebirthGearPlayerDataInventory.ReadSlots(saved,true),expected.Bag)||!Same(RebirthGearPlayerDataInventory.ReadSlots(saved,false),expected.Belt)))return false;
        using(var stream=new MemoryStream(saved.buffData.ToArray(),false))using(var reader=MemoryPools.poolBinaryReader.AllocSync(true))
        {
            reader.SetBaseStream(stream);int version=reader.ReadByte();if(version!=EntityBuffs.Version||version<2)return false;
            int count=reader.ReadUInt16();for(int i=0;i<count;i++)new BuffValue().Read(reader,version);
            int variables=reader.ReadUInt16();bool found=false;string wanted=prefix+operation.ToString("N");
            for(int i=0;i<variables;i++){string key=reader.ReadString();float value=reader.ReadSingle();if(key==wanted){if(found||value!=marker)return false;found=true;}}
            return found&&stream.Position==stream.Length;
        }
    }
    internal static bool TryApplyRemote(EntityPlayerLocal player,Guid operation,byte[] payload)
    {
        if(!IsInstalled||player==null||!RebirthStablePlayerIdentity.TryFromLocalPlatform(out var identity)||
            !ReadPayload(payload,player.entityId,operation,out var p,out var retired)||identity.CanonicalId!=p.Before.Owner||identity.StorageKey!=p.Before.OwnerKey)return false;
        var peers=SingletonMonoBehaviour<ConnectionManager>.Instance?.connectionToServer;if(peers==null||peers.Length==0||peers[0]==null)return false;
        p.Player=player;p.World=player.world;p.Peer=peers[0];if(!ClientCurrent(p))return false;
        if(retired)
        {
            // Saved terminal from an already-applied journal can be acknowledged
            // again after legitimate later movement; never re-create source hold.
            if(SavedFrame(identity,p.After,p.Original,RetiredPrefix,1f,false))return true;
            // Cold reconnect may retire only the exact saved postimage; it does
            // not debit again or accept a merely compatible partial source.
            if(!clientHolds.TryGetValue(p.Original,out var retained))retained=p;
            if(!ClientCurrent(retained)||!retained.Address.Equals(p.Address)||!Equal(Encode(retained.Before),Encode(p.Before))||!Equal(Encode(retained.After),Encode(p.After))||
                !Same(player.bag.ItemGrid.items,p.After.Bag)||!Same(player.inventory.ItemGrid.items,p.After.Belt)||!SavedFrame(identity,p.After,p.Original,ReceiptPrefix,1f,true))return false;
            player.Buffs.SetCustomVar(RetiredPrefix+p.Original.ToString("N"),1f,true);GameManager.Instance.SaveLocalPlayerData();
            if(!ClientCurrent(retained)||!SavedFrame(identity,p.After,p.Original,RetiredPrefix,1f,true))return false;
            clientHolds.Remove(p.Original);return true;
        }
        if(clientHolds.TryGetValue(p.Original,out var prior))
        {if(!ClientCurrent(prior)||!Equal(prior.Payload,p.Payload))return false;p=prior;}
        else
        {
            bool savedApplied=SavedFrame(identity,p.After,p.Original,ReceiptPrefix,1f,true)&&Same(player.bag.ItemGrid.items,p.After.Bag)&&Same(player.inventory.ItemGrid.items,p.After.Belt);
            if(!savedApplied&&(!RebirthCamperStationWindowSession.TryGetCurrent(player,out _,out var address)||!address.Equals(p.Address)))return false;
            if(clientHolds.Count>=64||!Compatible(player.bag.ItemGrid.items,p.Before.Bag,p.After.Bag)||!Compatible(player.inventory.ItemGrid.items,p.Before.Belt,p.After.Belt))return false;
            clientHolds.Add(p.Original,p);
        }
        float receipt=player.Buffs.GetCustomVar(ReceiptPrefix+p.Original.ToString("N"));
        if(receipt==1f)
        {
            if(!ClientCurrent(p)||!Same(player.bag.ItemGrid.items,p.After.Bag)||!Same(player.inventory.ItemGrid.items,p.After.Belt))return false;
            if(SavedFrame(identity,p.After,p.Original,ReceiptPrefix,1f,true))return true;
            // Retry only the same postimage save, never its already-applied debit.
            GameManager.Instance.SaveLocalPlayerData();
            return ClientCurrent(p)&&Same(player.bag.ItemGrid.items,p.After.Bag)&&Same(player.inventory.ItemGrid.items,p.After.Belt)&&
                SavedFrame(identity,p.After,p.Original,ReceiptPrefix,1f,true);
        }
        if(receipt!=0f&&receipt!=2f)return false;
        if(receipt==2f&&!p.ApplyingSaved)
        {
            // Cold resume is restricted to the original authenticated intent's
            // per-cell pre/post images plus its saved applying receipt.
            bool savedApplying=RebirthGearNativePlayerFile.TryRead(identity,out var checkpoint)&&
                Compatible(RebirthGearPlayerDataInventory.ReadSlots(checkpoint,true),p.Before.Bag,p.After.Bag)&&
                Compatible(RebirthGearPlayerDataInventory.ReadSlots(checkpoint,false),p.Before.Belt,p.After.Belt)&&
                SavedFrame(identity,p.Before,p.Original,ReceiptPrefix,2f,false,checkpoint);
            if(!savedApplying)
            {
                // A failed first save can leave only the live phase2 marker.
                // Retry the same save only while no source cell has changed.
                // Partial source is never turned into a new applying baseline.
                if(!ClientCurrent(p)||!Same(player.bag.ItemGrid.items,p.Before.Bag)||!Same(player.inventory.ItemGrid.items,p.Before.Belt))return false;
                GameManager.Instance.SaveLocalPlayerData();
                if(!ClientCurrent(p)||!SavedFrame(identity,p.Before,p.Original,ReceiptPrefix,2f,true))return false;
            }
            p.ApplyingSaved=true;
        }
        if(receipt==0f)
        {
            if(!Same(player.bag.ItemGrid.items,p.Before.Bag)||!Same(player.inventory.ItemGrid.items,p.Before.Belt))return false;
            player.Buffs.SetCustomVar(ReceiptPrefix+p.Original.ToString("N"),2f,true);GameManager.Instance.SaveLocalPlayerData();
            if(!ClientCurrent(p)||!SavedFrame(identity,p.Before,p.Original,ReceiptPrefix,2f,true))return false;
            p.ApplyingSaved=true;
        }
        for(int area=0;area<2;area++)
        {
            var after=area==0?p.After.Bag:p.After.Belt;
            for(int slot=0;slot<after.Length;slot++)
            {
                if(!ClientCurrent(p)||!Compatible(player.bag.ItemGrid.items,p.Before.Bag,p.After.Bag)||!Compatible(player.inventory.ItemGrid.items,p.Before.Belt,p.After.Belt))return false;
                var current=area==0?player.bag.ItemGrid.items:player.inventory.ItemGrid.items;
                if(RebirthStationGridIngredients.IsSameStackSnapshot(current[slot],after[slot]))continue;
                var item=after[slot].Clone();permit=new Permit{Inventory=area==0?(object)player.bag:player.inventory,Value=area==0?(object)item:item.itemValue,Slot=slot,Count=item.count};
                try{if(area==0)player.bag.SetSlot(slot,item);else player.inventory.SetItem(slot,item.itemValue,item.count);}finally{permit=null;}
            }
        }
        if(!ClientCurrent(p)||!Same(player.bag.ItemGrid.items,p.After.Bag)||!Same(player.inventory.ItemGrid.items,p.After.Belt))return false;
        player.Buffs.SetCustomVar(ReceiptPrefix+p.Original.ToString("N"),1f,true);GameManager.Instance.SaveLocalPlayerData();
        return ClientCurrent(p)&&SavedFrame(identity,p.After,p.Original,ReceiptPrefix,1f,true);
    }
    internal static bool VerifyRemoteSaved(EntityPlayer player,Guid operation,byte[] payload,Func<bool> current)
    {
        if(current==null||!current()||!RemoteIdentity(player,out var identity,out _)||!ReadPayload(payload,player.entityId,operation,out var p,out var retired)||
            p.Before.Owner!=identity.CanonicalId||p.Before.OwnerKey!=identity.StorageKey||p.Creation!=RebirthCamperCharacterAccess.CurrentCreation(player))return false;
        var record=Journal().Read(p.Original);
        if(record==null||record.Vehicle!=p.Address.Vehicle||record.OwnerCreation!=p.Creation||!Equal(record.Before,Encode(p.Before))||!Equal(record.After,Encode(p.After)))return false;
        if(retired)return record.State==RebirthCamperTransferJournal.Phase.Applied&&SavedFrame(identity,p.After,p.Original,RetiredPrefix,1f,false)&&current();
        return record.State==RebirthCamperTransferJournal.Phase.Uncertain&&SavedFrame(identity,p.After,p.Original,ReceiptPrefix,1f,true)&&current();
    }
    private static void SendRetirement(Pending p)
    {
        if(UnityEngine.Time.realtimeSinceStartup<p.Next||p.RetirementSent||p.Actor==null||p.Actor.world==null||p.Actor.world.IsRemote()||!RemoteIdentity(p.Actor,out var identity,out var sender)||
            !ReferenceEquals(sender,p.Sender)||identity.CanonicalId!=p.Before.Owner||identity.StorageKey!=p.Before.OwnerKey||RebirthCamperCharacterAccess.CurrentCreation(p.Actor)!=p.Creation)return;
        p.Next=UnityEngine.Time.realtimeSinceStartup+1f;
        p.RetirementSent=RebirthCamperRemoteOwnerApplication.SendOwnerApplication(p.Actor,RetirementOperation(p.Record.Operation),RebirthCamperRemoteOwnerEffectKind.Debit,RemotePayload(p,true),
            success=>{p.RetirementSent=false;if(success)retirements.Remove(p.Record.Operation);});
    }
    internal static void ObserveRemote(EntityPlayer player,RebirthCamperStationState station)
    {
        if(!IsInstalled||RebirthSurvivorMode.IsEnabledForCurrentWorld()||!RemoteIdentity(player,out var identity,out var sender)||
            !RebirthCamperRemoteStationTransport.TryGetCurrent(player,out var current,out var address,out var creation,out _)||!ReferenceEquals(current,station))return;
        RestorePending(station.BoundVehicle);TryResume(player,station,address,creation);
        string directory=Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH","NativeActorPayments");if(!System.IO.Directory.Exists(directory))return;
        foreach(var record in new RebirthCamperTransferJournal(directory).FindApplied(address.Vehicle,creation))
        {
            var before=Decode(record.Before);var after=Decode(record.After);
            if(!before.Remote||before.Station!=address.Installation||before.Owner!=identity.CanonicalId||before.OwnerKey!=identity.StorageKey)continue;
            if(SavedFrame(identity,after,record.Operation,RetiredPrefix,1f,false))continue;
            if(!retirements.TryGetValue(record.Operation,out var terminal))retirements.Add(record.Operation,new Pending{Actor=player,Identity=identity,Sender=sender,Station=station,Address=address,Creation=creation,Record=record,Before=before,After=after});
            else{terminal.Actor=player;terminal.Sender=sender;terminal.Identity=identity;terminal.RetirementSent=false;}
        }
    }
}

// Fence the native cursor before a slot handler can publish an item to it and
// subsequently encounter the exact source mutation fence.
[HarmonyPatch]
internal static class RebirthCamperNativeActorCursorGuard
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        foreach(string name in new[]{"HandleStackSwap","HandlePartialStackPickup","HandleDropOne","SwapItem","HandleMoveToPreferredLocation"})
        {
            var method=AccessTools.DeclaredMethod(typeof(XUiC_ItemStack),name,Type.EmptyTypes);
            if(method==null)throw new MissingMethodException(typeof(XUiC_ItemStack).FullName,name);
            yield return method;
        }
        yield return AccessTools.DeclaredMethod(typeof(XUiC_DragAndDropWindow),nameof(XUiC_DragAndDropWindow.PlaceItemBackInInventory));
    }
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(XUiController __instance)
    {
        var player=__instance?.xui?.playerUI?.entityPlayer;
        return player==null||!RebirthCamperStationNativeActorPayment.Blocks(player.bag)&&!RebirthCamperStationNativeActorPayment.Blocks(player.inventory);
    }
}

[HarmonyPatch(typeof(XUiM_Workstation),nameof(XUiM_Workstation.SetRecipeQueueItems))]
internal static class RebirthCamperNativeActorQueueGuard
{
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(XUiM_Workstation __instance)=>!RebirthCamperStationNativeActorPayment.IsHeld(__instance?.TileEntity as RebirthCamperStationState);
}

[HarmonyPatch]
internal static class RebirthCamperNativeActorMutationGuard
{
    private static IEnumerable<MethodBase> TargetMethods()
    {foreach(var type in new[]{typeof(Bag),typeof(Inventory)})foreach(var method in type.GetMethods(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance))
        if(method.DeclaringType==type&&new[]{"SetSlot","SetSlots","SetItem","DecItem","AddItem","AddItemAt","ChangeSlotCount","clearSlotByIndex"}.Contains(method.Name))yield return method;}
    private static bool Prefix(object __instance,MethodBase __originalMethod,object[] __args)=>RebirthCamperStationNativeActorPayment.BeforeMutation(__instance,__originalMethod,__args);
}

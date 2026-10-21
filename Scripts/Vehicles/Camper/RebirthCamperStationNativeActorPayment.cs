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
        internal float Next,TerminalProbeNext,EndedProbeNext;
        internal bool RemoteSent,RemoteConfirmed,RetirementSent;internal byte[] RemotePayload;
    }
    private sealed class Frame
    {
        internal Guid Station;internal string Owner,OwnerKey,WorldKey;internal int QueueSlot,OwnedBelt;internal bool Remote;internal ItemStack[] Bag,Belt;
        // Null is the exact canonical legacy v1 frame, never inferred on load.
        internal int? OriginalDeaths;
        internal bool[] BagLocks,BeltLocks;internal byte[] StationImage;
    }
    private sealed class Permit{internal object Inventory,Value;internal int Slot,Count;internal bool Used;}
    private static Permit permit;
    private sealed class FetchedHandoff{internal EntityPlayer Actor;internal RebirthCamperStationState Station;internal Guid Operation;internal bool Used;}
    private static FetchedHandoff fetchedHandoff;
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
        RebirthHarmonyBootstrap.PatchClassOnce(new Harmony("rebirth.camper.native.actor-payment"),typeof(RebirthCamperEnergyDetachedStackRead));
        RebirthHarmonyBootstrap.PatchClassOnce(new Harmony("rebirth.camper.native.actor-payment"),typeof(RebirthCamperEnergyDetachedArrayRead));
        ModEvents.GameUpdate.RegisterHandler(Tick);
        ModEvents.WorldShuttingDown.RegisterHandler(Shutdown);IsInstalled=true;
    }
    internal static bool IsHeld(RebirthCamperStationState station)=>station!=null&&pending.Values.Any(p=>ReferenceEquals(p.Station.BoundVehicle,station.BoundVehicle));
    private static bool SameLife(Frame frame,EntityPlayer player)=>frame.OriginalDeaths==null||player!=null&&frame.OriginalDeaths.Value==player.Died;
    private static bool OwnerWorld(EntityPlayerLocal player,Frame frame)=>player.world.IsRemote()?
        Guid.TryParse(GamePrefs.GetString(EnumGamePrefs.GameGuidClient),out var actual)&&Guid.TryParse(frame.WorldKey,out var original)&&actual==original:
        frame.WorldKey==player.world.worldState?.Guid;
    private static bool OwnerLife(EntityPlayerLocal player,Frame frame,string creation)=>player!=null&&player.IsSpawned()&&!player.IsDead()&&SameLife(frame,player)&&
        ReferenceEquals(GameManager.Instance?.World,player.world)&&ReferenceEquals(player.world.GetEntity(player.entityId),player)&&OwnerWorld(player,frame)&&
        creation==(player.world.IsRemote()?RebirthCamperCharacterScope.ResolveLocal(player):RebirthCamperCharacterAccess.CurrentCreation(player));
    internal static bool Blocks(object inventory)
    {
        if(pending.Count==0&&clientHolds.Count==0)return false;
        return pending.Values.Any(p=>p.Directory==GameIO.GetSaveGameDir()&&OwnerLife(p.ReservedPlayer,p.Before,p.Creation)&&(ReferenceEquals(p.ReservedPlayer.bag,inventory)||ReferenceEquals(p.ReservedPlayer.inventory,inventory)))||
            clientHolds.Values.Any(p=>ReferenceEquals(p.World,p.Player?.world)&&OwnerLife(p.Player,p.Before,p.Creation)&&(ReferenceEquals(p.Player.bag,inventory)||ReferenceEquals(p.Player.inventory,inventory)));
    }
    internal static bool MatchesPendingOperation(RebirthCamperStationState station,Guid operation)=>station!=null&&pending.TryGetValue(station,out var p)&&p.Record.Operation==operation;
    internal static bool MatchesCurrentFetchedPrepared(EntityPlayer actor,RebirthCamperStationState station,Guid operation,byte[] before,byte[] after)=>
        station!=null&&pending.TryGetValue(station,out var p)&&ReferenceEquals(p.Actor,actor)&&p.Record.Operation==operation&&Current(p)&&
        p.Journal.Read(operation)?.State==RebirthCamperTransferJournal.Phase.Prepared&&Equal(p.Record.Before,before)&&Equal(p.Record.After,after);
    internal static bool MatchesFetchedPreparedForRefusal(EntityPlayer actor,RebirthCamperStationState station,Guid operation,byte[] before,byte[] after)
    {
        // Read-only identity/history evidence for the caller's independently
        // proven all-attempts-zero/never-ownerAction refusal. No references,
        // receipts, lease, or mutation permission are changed here.
        return station!=null&&pending.TryGetValue(station,out var p)&&p.Directory==GameIO.GetSaveGameDir()&&
            p.Record.Operation==operation&&p.Before.OriginalDeaths.HasValue&&SameLife(p.Before,actor)&&
            RemoteIdentity(actor,out var owner,out _)&&owner.CanonicalId==p.Before.Owner&&owner.StorageKey==p.Before.OwnerKey&&
            p.Creation==RebirthCamperCharacterAccess.CurrentCreation(actor)&&
            p.Journal.Read(operation)?.State==RebirthCamperTransferJournal.Phase.Prepared&&Equal(p.Record.Before,before)&&Equal(p.Record.After,after);
    }
    private static bool OtherOwnerHold(EntityPlayerLocal player)=>RebirthDogLifecycleService.HasHireInventoryHold(player)||RebirthBackpackLibraryReservation.IsHeld(player)||RebirthGearOwnerReservation.IsHeld(player)||RemoteResourceClientTransactionCoordinator.HasPendingInventoryOperation(player);
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
    private static byte[] Encode(Frame f)=>Encoding.UTF8.GetBytes(new XElement("nativeActorPayment",new XAttribute("version",f.OriginalDeaths.HasValue?2:1),new XAttribute("station",f.Station.ToString("N")),new XAttribute("owner",f.Owner),new XAttribute("ownerKey",f.OwnerKey),new XAttribute("world",f.WorldKey),new XAttribute("queueSlot",f.QueueSlot),new XAttribute("ownedBelt",f.OwnedBelt),new XAttribute("remote",f.Remote),f.OriginalDeaths.HasValue?new XAttribute("deaths",f.OriginalDeaths.Value):null,
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
        int version=(int?)e.Attribute("version")??0;
        if(e.Name!="nativeActorPayment"||(version!=1&&version!=2)||e.Attributes().Count()!=(version==1?8:9))throw new InvalidDataException();
        var f=new Frame{Station=Guid.ParseExact((string)e.Attribute("station"),"N"),Owner=(string)e.Attribute("owner"),OwnerKey=(string)e.Attribute("ownerKey"),WorldKey=(string)e.Attribute("world"),QueueSlot=(int)e.Attribute("queueSlot"),OwnedBelt=(int)e.Attribute("ownedBelt"),Remote=(bool)e.Attribute("remote"),Bag=ReadCells(e.Element("bag"),169),Belt=ReadCells(e.Element("belt"),20),
            BagLocks=ReadLocks(e.Element("bagLocks")),BeltLocks=ReadLocks(e.Element("beltLocks")),StationImage=Convert.FromBase64String(e.Element("stationImage").Value),OriginalDeaths=version==2?(int?)e.Attribute("deaths"):null};
        if(version==2&&(!f.OriginalDeaths.HasValue||f.OriginalDeaths.Value<0)||f.Station==Guid.Empty||f.QueueSlot<0||f.QueueSlot>254||f.OwnedBelt<0||f.OwnedBelt>f.Belt.Length||string.IsNullOrEmpty(f.Owner)||string.IsNullOrEmpty(f.OwnerKey)||string.IsNullOrEmpty(f.WorldKey)||!Equal(Encode(f),bytes))throw new InvalidDataException();return f;
    }
    private static RebirthCamperTransferJournal Journal()=>new RebirthCamperTransferJournal(Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH","NativeActorPayments"));
    internal static bool AreCanonicalFetchedFrames(byte[] before,byte[] after)
    {try{return Equal(Encode(Decode(before)),before)&&Equal(Encode(Decode(after)),after);}catch{return false;}}
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
        if(station==null||after.OriginalDeaths!=before.OriginalDeaths||after.Station!=before.Station||before.QueueSlot!=after.QueueSlot||before.QueueSlot>=station.Queue.Length||before.Owner!=after.Owner||before.WorldKey!=after.WorldKey||before.WorldKey!=vehicle.world.worldState?.Guid)return false;
        if(!pending.ContainsKey(station))pending.Add(station,new Pending{Station=station,Address=new RebirthCamperStationAddress(runtime.Identity,station.InstallationId),Creation=record.OwnerCreation,
            Directory=save,Journal=journal,Record=record,Before=before,After=after});
        recovered.Remove(vehicle);recovered.Add(vehicle,new RecoveryScope{World=vehicle.world,Directory=save});return true;
    }
    internal static bool TryResume(EntityPlayer player,RebirthCamperStationState station,RebirthCamperStationAddress address,string creation)
    {
        if(player!=null&&pending.TryGetValue(station,out var originalScope)&&originalScope.Before.OriginalDeaths.HasValue&&originalScope.Before.OriginalDeaths.Value!=player.Died)return false;
        if(player!=null&&!(player is EntityPlayerLocal)&&pending.TryGetValue(station,out var remote))
        {
            if(!IsInstalled||!remote.Before.Remote||!remote.Address.Equals(address)||remote.Creation!=creation||
                !RemoteIdentity(player,out var identity,out var sender)||identity.CanonicalId!=remote.Before.Owner||identity.StorageKey!=remote.Before.OwnerKey||
                !RebirthStationLiveAccess.TryGetMobileLease(player,address,creation,out var live,out var remoteLease)||!ReferenceEquals(live,station))return false;
            if(remote.Actor!=null&&(!ReferenceEquals(remote.Actor,player)||!ReferenceEquals(remote.Lease,remoteLease)||!ReferenceEquals(remote.Sender,sender))&&
                !CanRebindFetchedServer(remote,player,identity,sender))return false;
            remote.Actor=player;remote.Identity=identity;remote.Sender=sender;remote.Lease=remoteLease;station.lastTickTime=0;Advance(remote);return true;
        }
        if(!IsInstalled||!(player is EntityPlayerLocal local)||!pending.TryGetValue(station,out var p)||!p.Address.Equals(address)||p.Creation!=creation||
            GameManager.Instance?.World?.GetPrimaryPlayer()!=local||!RebirthStationLiveAccess.TryGetMobileLease(player,address,creation,out var current,out var lease)||!ReferenceEquals(current,station))return false;
        if(p.Player!=null&&(!ReferenceEquals(p.Player,local)||!ReferenceEquals(p.Lease,lease)))return false;
        if(GameManager.Instance.getPersistentPlayerID(null)?.CombinedString!=p.Before.Owner)return false;
        if(!RebirthStablePlayerIdentity.TryFromLocalPlatform(out var localIdentity)||localIdentity.CanonicalId!=p.Before.Owner||localIdentity.StorageKey!=p.Before.OwnerKey)return false;
        p.Player=local;p.Actor=local;p.Identity=localIdentity;p.ReservedPlayer=local;p.Lease=lease;station.lastTickTime=0;Advance(p);return true;
    }
    private static bool CanRebindFetchedServer(Pending p,EntityPlayer actor,RebirthStablePlayerIdentity identity,ClientInfo sender,bool retirement=false)
    {
        try
        {
            // Both old references must have ended. A live lease replacement
            // alone is never permission to substitute a pending actor.
            if(p.Actor==null||p.Sender==null||ReferenceEquals(p.Actor,actor)||ReferenceEquals(p.Sender,sender)||
                ReferenceEquals(p.Actor.world?.GetEntity(p.Actor.entityId),p.Actor)||
                ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance?.Clients.ForEntityId(p.Actor.entityId),p.Sender)||
                p.Directory!=GameIO.GetSaveGameDir()||!p.Before.OriginalDeaths.HasValue||p.Before.OriginalDeaths.Value!=actor.Died||!FetchedTransportCandidate(p)||
                sender.latestPlayerData==null||sender.latestPlayerData.id!=actor.entityId||sender.latestPlayerData.deaths!=actor.Died)return false;
            bool enqueue=FetchedCandidate(p);
            if(enqueue&&!retirement&&p.Record.State==RebirthCamperTransferJournal.Phase.Prepared&&
                RebirthRemoteResourceFetchedStationOriginal.CanRebindPreparedServer(actor,p.Station,p.Record.Operation,p.Record.Before,p.Record.After))return true;
            bool used=false;RebirthCamperEnergyActorImage image;PlayerDataFile saved;
            bool proof=enqueue?RebirthRemoteResourceFetchedStationOriginal.CanRebindNativeServer(actor,p.Station,p.Record.Operation,p.Record.Before,p.Record.After,out used,out image,out saved):
                RebirthRemoteResourceFetchedStationOriginal.CanRebindCancellationServer(actor,p.Station,p.Record.Operation,p.Record.Before,p.Record.After,
                    p.Before.Bag,p.Before.Belt,p.After.Bag,p.After.Belt,p.Before.BagLocks,out image,out saved);
            if(!proof||
                saved.id!=actor.entityId||saved.deaths!=p.Before.OriginalDeaths.Value||
                !SameLocks(image.BagLocks,p.Before.BagLocks)||
                !RebirthCookingRemoteAdmissionBackend.NativePhase(saved,ReceiptPrefix+p.Record.Operation.ToString("N"),out float phase,out bool present))return false;
            if(sender.latestPlayerData==null||sender.latestPlayerData.id!=actor.entityId||sender.latestPlayerData.deaths!=actor.Died||
                retirement&&(enqueue&&!used||p.Record.State!=RebirthCamperTransferJournal.Phase.Applied))return false;
            bool after=Same(image.Bag,p.After.Bag)&&Same(image.Belt,p.After.Belt);
            if(used||retirement)
            {
                // A paid terminal may finish metadata only. Advance must see
                // the already-published After station, never install Before.
                return present&&phase==1f&&after&&SavedFrame(identity,p.After,p.Record.Operation,ReceiptPrefix,1f,true,saved)&&
                    (retirement||Equal(RebirthCamperStationSnapshot.Capture(p.Station.InstallationId,p.Station),p.After.StationImage));
            }
            if(!present||phase==0f)return Same(image.Bag,p.Before.Bag)&&Same(image.Belt,p.Before.Belt);
            if(phase==1f)return after&&SavedFrame(identity,p.After,p.Record.Operation,ReceiptPrefix,1f,true,saved);
            return phase==2f&&Compatible(image.Bag,p.Before.Bag,p.After.Bag)&&Compatible(image.Belt,p.Before.Belt,p.After.Belt)&&
                SavedFrame(identity,p.Before,p.Record.Operation,ReceiptPrefix,2f,false,saved);
        }
        catch{return false;}
    }
    internal static bool Begin(EntityPlayer player,RebirthCamperStationState station,RebirthCamperStationAddress address,string creation,Guid operation,
        ItemStack[] beforeBag,ItemStack[] afterBag,ItemStack[] beforeBelt,ItemStack[] afterBelt,RecipeQueueItem[] afterQueue,RebirthStablePlayerIdentity remoteOwner=null,bool[] remoteLocks=null,
        ItemStack[] recyclerInput=null,Guid recyclerGroup=default(Guid),ItemStack[] recyclerOutput=null,ItemStack[] recyclerTools=null)
    {
        bool recycler=recyclerGroup!=Guid.Empty&&RebirthRecyclerRuntime.Supports(station)
            &&RebirthRecyclerRuntime.ValidDelta(station?.Queue,afterQueue,recyclerGroup);
        if(recyclerGroup!=Guid.Empty&&!recycler)return false;
        var local=player as EntityPlayerLocal;bool remote=remoteOwner!=null;ClientInfo sender=null;RebirthStablePlayerIdentity identity=remoteOwner;
        var fetched=fetchedHandoff;
        bool fetchedOriginal=fetched!=null&&!fetched.Used&&ReferenceEquals(fetched.Actor,player)&&ReferenceEquals(fetched.Station,station)&&fetched.Operation==operation;
        if(!IsInstalled||player==null||player.IsDead()||!player.IsSpawned()||player.Died<0||operation==Guid.Empty||IsHeld(station)||
            RebirthRemoteResourceFetchedStationOriginal.BlocksStation(station)&&!fetchedOriginal||RebirthRemoteResourceSourceCustody.BlocksStation(station)||
            !RebirthStationLiveAccess.TryGetMobileLease(player,address,creation,out var current,out var lease)||!ReferenceEquals(current,station))return false;
        if(fetchedOriginal)fetched.Used=true;
        if(remote)
        {
            if(!recycler&&!RebirthCamperRemoteStationPlayerSource.IsCurrent(player,station)||!RemoteIdentity(player,out var peer,out sender)||
                peer.CanonicalId!=identity.CanonicalId||peer.StorageKey!=identity.StorageKey||
                sender.latestPlayerData==null||sender.latestPlayerData.id!=player.entityId||sender.latestPlayerData.deaths!=player.Died||
                !RebirthCamperEnergySavedOwner.TryRead(identity,out var nativeBefore,out var savedBefore)||savedBefore.id!=player.entityId||savedBefore.deaths!=player.Died||
                !Same(beforeBag,nativeBefore.Bag)||!Same(beforeBelt,nativeBefore.Belt)||!SameLocks(remoteLocks,nativeBefore.BagLocks))return false;
        }
        else if(local==null||OtherOwnerHold(local)||local.PlayerUI?.xui?.DragAndDropWindow?.IsEmpty()!=true||!recycler&&!RebirthStationPlayerIngredients.UsesPlayerPayment(player,station)||!RebirthStablePlayerIdentity.TryFromLocalPlatform(out identity)||
            !Same(beforeBag,player.bag.ItemGrid.items)||!Same(beforeBelt,player.inventory.ItemGrid.items)||GameManager.Instance.getPersistentPlayerID(null)?.CombinedString!=identity.CanonicalId)return false;
        if(!TryEncodeFrames(player,station,address,creation,operation,identity,remote,beforeBag,afterBag,beforeBelt,afterBelt,afterQueue,remoteLocks,
            recycler,recyclerInput,recyclerOutput,recyclerTools,out var before,out var after,out var beforeBytes,out var afterBytes))return false;
        var journal=Journal();if(journal.FindPending(address.Vehicle)!=null||journal.Read(operation)!=null)return false;
        var record=journal.Prepare(operation,address.Vehicle,creation,station.Revision,beforeBytes,afterBytes);
        // Only the actual accepted payment takes the native clock hold. The
        // shared encoder/preflight above never changes a live station.
        station.lastTickTime=0;
        var p=new Pending{Player=local,ReservedPlayer=local,Actor=player,Identity=identity,Sender=sender,Station=station,Address=address,Lease=lease,Creation=creation,Directory=GameIO.GetSaveGameDir(),Journal=journal,Record=record,Before=before,After=after};
        pending.Add(station,p);Advance(p);return true; // Accepted durable intent, not completion.
    }
    internal static bool BeginFetchedOriginal(EntityPlayer actor,RebirthCamperStationState station,RebirthCamperStationAddress address,string creation,Guid job,Guid sourceOperation,string binding)
    {
        if(fetchedHandoff!=null||!RemoteIdentity(actor,out var owner,out _)||!RebirthRemoteResourceFetchedStationOriginal.PrepareNativePermission(actor,station,address,creation,job,sourceOperation,binding)||
            !RebirthRemoteResourceFetchedStationOriginal.TryReadForBegin(actor,station,address,creation,job,sourceOperation,binding,out var plan)||
            owner.StorageKey!=plan.OwnerKey||owner.CanonicalId!=plan.CanonicalOwner)return false;
        if(MatchesPendingOperation(station,job))return TryResume(actor,station,address,creation);
        var before=plan.CopyBeforeActor();var after=plan.CopyAfterActor();
        try
        {
            fetchedHandoff=new FetchedHandoff{Actor=actor,Station=station,Operation=job};
            if(!Begin(actor,station,address,creation,job,before.Bag,after.Bag,before.Belt,after.Belt,plan.CopyQueueFor(station),owner,before.BagLocks))return false;
            return RebirthRemoteResourceFetchedStationOriginal.CompleteNativeHandoff(station,job,sourceOperation,binding);
        }
        finally{fetchedHandoff=null;}
    }
    internal static bool PrepareFetchedOriginal(EntityPlayer actor,RebirthCamperStationState station,RebirthCamperStationAddress address,string creation,Guid job,Guid source,string binding)
    {
        if(fetchedHandoff!=null||!RemoteIdentity(actor,out var owner,out _)||
            !RebirthRemoteResourceFetchedStationOriginal.TryReadForPrepare(actor,station,address,creation,job,source,binding,out var plan))return false;
        if(MatchesPendingOperation(station,job))return TryResume(actor,station,address,creation);
        var before=plan.CopyBeforeActor();var after=plan.CopyAfterActor();
        try
        {
            fetchedHandoff=new FetchedHandoff{Actor=actor,Station=station,Operation=job};
            if(!Begin(actor,station,address,creation,job,before.Bag,after.Bag,before.Belt,after.Belt,plan.CopyQueueFor(station),owner,before.BagLocks))return false;
            return RebirthRemoteResourceFetchedStationOriginal.CompleteNativeHandoff(station,job,source,binding);
        }
        finally{fetchedHandoff=null;}
    }
    internal static bool TryGetFetchedSavedBefore(EntityPlayer actor,RebirthCamperStationState station,Guid job,out byte[] before,out byte[] after)
    {
        before=after=null;
        if(!RebirthRemoteResourceFetchedStationOriginal.HasOnlyNeverEnteredNativeFiles(job,false)||!pending.TryGetValue(station,out var p)||p.Record.Operation!=job||!ReferenceEquals(p.Actor,actor)||!Current(p)||
            p.Journal.Read(job)?.State!=RebirthCamperTransferJournal.Phase.Prepared||
            !RebirthRemoteResourceStationPaymentWitness.TryReadSavedBefore(actor.world,job,p.Address.ToString(),p.Before.OwnerKey,p.Creation,p.Record.Before,p.Record.After,out _))return false;
        before=(byte[])p.Record.Before.Clone();after=(byte[])p.Record.After.Clone();return true;
    }
    internal static bool RejectFetchedPrepared(EntityPlayer actor,RebirthCamperStationState station,Guid job,Guid source,string binding)
    {
        if(!ThreadManager.IsMainThread())return false;
        if(!pending.TryGetValue(station,out var p))
        {
            var rejected=Journal().Read(job);
            return rejected?.State==RebirthCamperTransferJournal.Phase.Rejected&&
                RebirthRemoteResourceFetchedStationOriginal.CanRejectNativePrepared(actor,station,job,source,binding,rejected.Before,rejected.After);
        }
        if(p.Record.Operation!=job||!ReferenceEquals(p.Actor,actor)||!Current(p))return false;
        var record=p.Journal.Read(job);
        if(record==null||record.State!=RebirthCamperTransferJournal.Phase.Prepared&&record.State!=RebirthCamperTransferJournal.Phase.Rejected||
            !RebirthRemoteResourceFetchedStationOriginal.CanRejectNativePrepared(actor,station,job,source,binding,record.Before,record.After))return false;
        if(record.State==RebirthCamperTransferJournal.Phase.Prepared)record=p.Journal.Advance(job,record.State,RebirthCamperTransferJournal.Phase.Rejected);
        if(record.State!=RebirthCamperTransferJournal.Phase.Rejected)return false;
        pending.Remove(station);station.ResetTickTime();return true;
    }
    private static bool FetchedCandidate(Pending p)
    {
        var installed=RebirthCamperModuleRuntime.For(p.Station.BoundVehicle).Stations.Find(p.Station.InstallationId)?.InstalledItem;
        if(installed==null)return true;
        var state=RebirthCamperStationSnapshot.Restore(p.Station.InstallationId,installed,p.After.StationImage);
        return RebirthRemoteResourceFetchedStationOriginal.IsCancellationCandidate(state.Queue[p.After.QueueSlot]?.Recipe);
    }
    private static bool FetchedTransportCandidate(Pending p)
    {
        if(FetchedCandidate(p))return true;
        var installed=RebirthCamperModuleRuntime.For(p.Station.BoundVehicle).Stations.Find(p.Station.InstallationId)?.InstalledItem;
        if(installed==null)return true;
        var state=RebirthCamperStationSnapshot.Restore(p.Station.InstallationId,installed,p.Before.StationImage);
        return RebirthRemoteResourceFetchedStationOriginal.IsCancellationCandidate(state.Queue[p.Before.QueueSlot]?.Recipe);
    }
    private static bool TryEncodeFrames(EntityPlayer player,RebirthCamperStationState station,RebirthCamperStationAddress address,string creation,Guid operation,
        RebirthStablePlayerIdentity identity,bool remote,ItemStack[] beforeBag,ItemStack[] afterBag,ItemStack[] beforeBelt,ItemStack[] afterBelt,
        RecipeQueueItem[] afterQueue,bool[] remoteLocks,bool recycler,ItemStack[] recyclerInput,ItemStack[] recyclerOutput,ItemStack[] recyclerTools,
        out Frame before,out Frame after,out byte[] beforeBytes,out byte[] afterBytes)
    {
        before=after=null;beforeBytes=afterBytes=null;
        try
        {
            if(!ThreadManager.IsMainThread()||player==null||station==null||identity==null||!address.IsValid||operation==Guid.Empty||
                afterQueue==null||station.Queue==null||afterQueue.Length!=station.Queue.Length||
                !RebirthCamperModuleRuntime.TryGetExisting(station.BoundVehicle,out var runtime)||runtime.Identity!=address.Vehicle||station.InstallationId!=address.Installation)return false;
            var changed=Enumerable.Range(0,afterQueue.Length).Where(i=>!ReferenceEquals(afterQueue[i],station.Queue[i])).ToArray();
            if(changed.Length<1||!recycler&&changed.Length!=1)return false;int queueSlot=changed[0];
            if(recycler&&(recyclerInput==null||recyclerInput.Length!=station.Input.Length))return false;
            var installed=runtime.Stations.Find(station.InstallationId)?.InstalledItem;if(installed==null)return false;
            string worldKey=player.world?.worldState?.Guid;if(string.IsNullOrEmpty(worldKey))return false;
            var live=RebirthCamperStationSnapshot.Capture(station.InstallationId,station);
            var candidate=RebirthCamperStationSnapshot.Restore(station.InstallationId,installed,live);
            // Retain full rich stock images across detached native hydration.
            candidate.Tools=ItemStack.Clone(station.Tools);candidate.Fuel=ItemStack.Clone(station.Fuel);candidate.Input=ItemStack.Clone(station.Input);candidate.Output=ItemStack.Clone(station.Output);
            candidate.lastInput=ItemStack.Clone(station.lastInput);candidate.CraftCompleteList=station.CraftCompleteList==null?null:new List<CraftCompleteData>(station.CraftCompleteList);
            candidate.Queue=(RecipeQueueItem[])station.Queue.Clone();candidate.lastTickTime=0;
            var image=RebirthCamperStationSnapshot.Capture(station.InstallationId,candidate);
            if(recycler)candidate.Input=ItemStack.Clone(recyclerInput);
            if(recyclerOutput!=null){if(!recycler||recyclerOutput.Length!=station.Output.Length)return false;candidate.Output=ItemStack.Clone(recyclerOutput);}
            if(recyclerTools!=null){if(!recycler||recyclerTools.Length!=station.Tools.Length)return false;candidate.Tools=ItemStack.Clone(recyclerTools);}
            candidate.Queue=afterQueue;candidate.setModified();
            before=new Frame{Station=station.InstallationId,QueueSlot=queueSlot,Owner=identity.CanonicalId,OwnerKey=identity.StorageKey,OwnedBelt=player.inventory.Length,Remote=remote,
                WorldKey=worldKey,OriginalDeaths=player.Died,Bag=beforeBag,Belt=beforeBelt,BagLocks=remote?remoteLocks:Locks(player.bag.LockedSlots),BeltLocks=remote?null:Locks(player.inventory.ItemGrid.SlotLocks),StationImage=image};
            after=new Frame{Station=before.Station,QueueSlot=queueSlot,Owner=before.Owner,OwnerKey=before.OwnerKey,OwnedBelt=before.OwnedBelt,Remote=remote,WorldKey=worldKey,OriginalDeaths=before.OriginalDeaths,
                Bag=afterBag,Belt=afterBelt,BagLocks=before.BagLocks,BeltLocks=before.BeltLocks,StationImage=RebirthCamperStationSnapshot.Capture(station.InstallationId,candidate)};
            beforeBytes=Encode(before);afterBytes=Encode(after);
            if(beforeBytes.Length>8*1024*1024||afterBytes.Length>8*1024*1024||remote&&((long)beforeBytes.Length+afterBytes.Length)*4/3+4096>8*1024*1024||
                !Equal(Encode(Decode(beforeBytes)),beforeBytes)||!Equal(Encode(Decode(afterBytes)),afterBytes))return false;
            // Reserve the longest valid actor id for reconnect, and false's
            // longer boolean spelling. Later peer ids/retirement cannot
            // overflow an original that already paid under this preflight.
            if(remote)EncodeRemotePayload(int.MaxValue,operation,false,creation,address,beforeBytes,afterBytes);
            return true;
        }
        catch{before=after=null;beforeBytes=afterBytes=null;return false;}
    }
    // Pure exact-byte preflight, never native application or admission authority.
    internal static bool TryPreflightFetchedFrames(EntityPlayer actor,RebirthCamperStationState station,RebirthCamperStationAddress address,string creation,
        RebirthRemoteResourceFetchedStationPlan.Plan plan,out byte[] before,out byte[] after)
    {
        before=after=null;
        try
        {
            if(plan==null||plan.Address!=address.ToString()||plan.Creation!=creation||actor?.Died!=plan.Deaths||!RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var owner)||
                owner.StorageKey!=plan.OwnerKey||owner.CanonicalId!=plan.CanonicalOwner)return false;
            var prior=plan.CopyBeforeActor();var next=plan.CopyAfterActor();
            return TryEncodeFrames(actor,station,address,creation,plan.Job,owner,true,prior.Bag,next.Bag,prior.Belt,next.Belt,plan.CopyQueueFor(station),prior.BagLocks,
                false,null,null,null,out _,out _,out before,out after);
        }
        catch{return false;}
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
        if(!RebirthCamperEnergySavedOwner.TryRead(identity,out var nativeSource,out var sourceFile)||sourceFile.id!=player.entityId||sourceFile.deaths!=player.Died||
            !RebirthStationPlayerIngredients.TryPlan(player,recipe,batches,tier,nativeSource.Bag,nativeSource.Belt,nativeSource.BagLocks,null,player.inventory.Length,out var plan)||
            !RebirthCamperStationNativeCrafting.BuildActorQueue(player,station,job,recipe,batches,tier,plan,identity.CanonicalId,out var queue)||
            !plan.TryGetImages(out var oldBag,out var newBag,out var oldBelt,out var newBelt))return false;
        return Begin(player,station,address,creation,job,oldBag,newBag,oldBelt,newBelt,queue,identity,nativeSource.BagLocks);
    }
    private static bool Current(Pending p)=>p.Before.Remote?CurrentRemote(p):ThreadManager.IsMainThread()&&p.Player!=null&&p.Directory==GameIO.GetSaveGameDir()&&
        GameManager.Instance?.World==p.Player.world&&!p.Player.world.IsRemote()&&p.Player.world.GetPrimaryPlayer()==p.Player&&p.Player.world.GetEntity(p.Player.entityId)==p.Player&&
        p.Before.WorldKey==p.Player.world.worldState?.Guid&&SameLife(p.Before,p.Player)&&
        p.Player.IsSpawned()&&!p.Player.IsDead()&&GameManager.Instance.getPersistentPlayerID(null)?.CombinedString==p.Before.Owner&&
        p.Creation==RebirthCamperCharacterAccess.CurrentCreation(p.Player)&&
        RebirthStationLiveAccess.TryGetMobileLease(p.Player,p.Address,p.Creation,out var s,out var lease)&&ReferenceEquals(s,p.Station)&&ReferenceEquals(lease,p.Lease)&&
        SameLocks(p.Before.BagLocks,Locks(p.Player.bag.LockedSlots))&&SameLocks(p.Before.BeltLocks,Locks(p.Player.inventory.ItemGrid.SlotLocks));
    private static bool CurrentRemote(Pending p)=>ThreadManager.IsMainThread()&&p.Actor!=null&&p.Actor.world!=null&&!p.Actor.world.IsRemote()&&
        p.Directory==GameIO.GetSaveGameDir()&&ReferenceEquals(GameManager.Instance?.World,p.Actor.world)&&
        ReferenceEquals(p.Actor.world.GetEntity(p.Actor.entityId),p.Actor)&&!p.Actor.IsDead()&&p.Actor.IsSpawned()&&
        p.Before.WorldKey==p.Actor.world.worldState?.Guid&&SameLife(p.Before,p.Actor)&&p.Creation==RebirthCamperCharacterAccess.CurrentCreation(p.Actor)&&
        RemoteIdentity(p.Actor,out var identity,out var sender)&&ReferenceEquals(sender,p.Sender)&&identity.CanonicalId==p.Before.Owner&&identity.StorageKey==p.Before.OwnerKey&&
        (!p.Before.OriginalDeaths.HasValue||sender.latestPlayerData!=null&&sender.latestPlayerData.id==p.Actor.entityId&&sender.latestPlayerData.deaths==p.Before.OriginalDeaths.Value)&&
        RebirthStationLiveAccess.TryGetMobileLease(p.Actor,p.Address,p.Creation,out var station,out var lease)&&ReferenceEquals(station,p.Station)&&ReferenceEquals(lease,p.Lease);
    private static bool Compatible(ItemStack[] actual,ItemStack[] before,ItemStack[] after)
    {if(actual.Length!=before.Length||after.Length!=before.Length)return false;for(int i=0;i<actual.Length;i++)if(!RebirthStationGridIngredients.IsSameStackSnapshot(actual[i],before[i])&&!RebirthStationGridIngredients.IsSameStackSnapshot(actual[i],after[i]))return false;return true;}
    private static bool HasCurrentLaterLife(Pending p)
    {
        var world=GameManager.Instance?.World;var peers=SingletonMonoBehaviour<ConnectionManager>.Instance?.Clients.List;
        if(world?.IsRemote()!=false||peers==null||!p.Before.OriginalDeaths.HasValue||world.worldState.Guid!=p.Before.WorldKey)return false;
        foreach(var peer in peers)
        {
            var actor=world.GetEntity(peer.entityId) as EntityPlayer;
            if(actor!=null&&actor.IsSpawned()&&!actor.IsDead()&&actor.Died>p.Before.OriginalDeaths.Value&&RebirthCamperStationNativeActorPayment.RemoteIdentity(actor,out var owner,out var sender)&&
                ReferenceEquals(sender,peer)&&owner.CanonicalId==p.Before.Owner&&owner.StorageKey==p.Before.OwnerKey&&RebirthCamperCharacterAccess.CurrentCreation(actor)==p.Creation)return true;
        }
        return false;
    }
    private static bool OwnerSaved(Pending p,bool applied=true)
    {
        var expected=applied?p.After:p.Before;
        var owner=p.Identity;
        if(!Current(p)||owner==null||owner.CanonicalId!=p.Before.Owner||
            !RebirthGearNativePlayerFile.TryRead(owner,out var data)||expected.OriginalDeaths.HasValue&&(data.deaths!=expected.OriginalDeaths.Value||data.id!=p.Actor.entityId)||!Same(RebirthGearPlayerDataInventory.ReadSlots(data,true),expected.Bag)||
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
            if(p.Before.Remote&&p.Before.OriginalDeaths.HasValue&&p.Record.State==RebirthCamperTransferJournal.Phase.Prepared&&UnityEngine.Time.realtimeSinceStartup>=p.EndedProbeNext)
            {
                p.EndedProbeNext=UnityEngine.Time.realtimeSinceStartup+1f;
                if(HasCurrentLaterLife(p)&&RebirthRemoteResourceFetchedStationOriginal.PrepareEndedZeroEffect(p.Station,p.Record.Operation,p.Record.Before,p.Record.After))
                {
                    var original=p.Journal.Read(p.Record.Operation);
                    if(original?.State==RebirthCamperTransferJournal.Phase.Prepared&&Equal(original.Before,p.Record.Before)&&Equal(original.After,p.Record.After))
                    {
                        // Source3 was durably published first. No new-life
                        // inventory or native queue setter is involved.
                        p.Record=p.Journal.Advance(original.Operation,original.State,RebirthCamperTransferJournal.Phase.Rejected);
                        if(p.Record.State==RebirthCamperTransferJournal.Phase.Rejected){pending.Remove(p.Station);p.Station.ResetTickTime();}return;
                    }
                }
            }
            if(!Current(p)||UnityEngine.Time.realtimeSinceStartup<p.Next)return;
            p.Next=UnityEngine.Time.realtimeSinceStartup+.25f;
            var record=p.Journal.Read(p.Record.Operation);if(record==null)return;p.Record=record;
            var live=RebirthCamperStationSnapshot.Capture(p.Station.InstallationId,p.Station);
            bool before=Equal(live,p.Before.StationImage),after=Equal(live,p.After.StationImage);
            if(record.State==RebirthCamperTransferJournal.Phase.Applied&&FetchedTransportCandidate(p))
            {
                // A final phase file may have published before a subsequent
                // exception prevented transient pending->retirement cleanup.
                // Revalidate the actual native publication; Applied alone is
                // not permission to discard custody or retire an owner.
                if(!after||!OwnerSaved(p)||!RebirthRemoteResourceStationPaymentWitness.TryReadSavedPayment(p.Actor.world,record.Operation,p.Address.ToString(),p.Before.OwnerKey,p.Creation,out var saved)||
                    saved.OriginalDeaths!=p.Before.OriginalDeaths||saved.CopyBeforeFrame().ToString(SaveOptions.DisableFormatting)!=new UTF8Encoding(false,true).GetString(record.Before)||
                    !Equal(RebirthCamperStationSnapshot.Capture(p.Station.InstallationId,saved.RestoreAfter()),p.After.StationImage)||
                    !Same(saved.RestoreAfterActor().Bag,p.After.Bag)||!Same(saved.RestoreAfterActor().Belt,p.After.Belt))return;
                bool admitted=FetchedCandidate(p)?RebirthRemoteResourceFetchedStationOriginal.CanRebindNativeServer(p.Actor,p.Station,record.Operation,record.Before,record.After,out _):
                    RebirthRemoteResourceFetchedStationOriginal.CanRebindCancellationServer(p.Actor,p.Station,record.Operation,record.Before,record.After,p.Before.Bag,p.Before.Belt,p.After.Bag,p.After.Belt,p.Before.BagLocks,out _,out _);
                if(!admitted)return;
                pending.Remove(p.Station);p.Station.ResetTickTime();retirements[record.Operation]=p;SendRetirement(p);return;
            }
            if(record.State==RebirthCamperTransferJournal.Phase.Prepared)
            {
                bool fetched=FetchedCandidate(p);
                if(fetched&&RebirthRemoteResourceFetchedStationOriginal.CanRejectNativeUnpaid(p.Actor,p.Station,record.Operation,record.Before,record.After))
                {
                    p.Record=p.Journal.Advance(record.Operation,record.State,RebirthCamperTransferJournal.Phase.Rejected);
                    if(p.Record.State==RebirthCamperTransferJournal.Phase.Rejected){pending.Remove(p.Station);p.Station.ResetTickTime();}return;
                }
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
                bool historicalBefore=fetched&&RebirthRemoteResourceStationPaymentWitness.TryReadSavedBefore(p.Actor.world,record.Operation,p.Address.ToString(),p.Before.OwnerKey,p.Creation,record.Before,record.After,out _);
                if(!historicalBefore&&p.BeforeVehicleSave==null)
                {
                    RebirthCamperModuleRuntime.For(p.Station.BoundVehicle).Persist(true);
                    p.BeforeVehicleSave=RebirthCamperSavedVehicle.RequestSave(p.Station.BoundVehicle);
                }
                if(!historicalBefore&&!RebirthCamperSavedVehicle.Verify(p.Station.BoundVehicle,p.BeforeVehicleSave,()=>Current(p)))return;
                if(!historicalBefore&&p.Before.OriginalDeaths.HasValue&&(!RebirthRemoteResourceStationPaymentWitness.CanRetainExpected(p.BeforeVehicleSave)||
                    !RebirthRemoteResourceStationPaymentWitness.PrepareExpected(record,p.BeforeVehicleSave)))return;
                if(fetched&&!RebirthRemoteResourceFetchedStationOriginal.MayAdvancePrepared(p.Actor,p.Station,record.Operation,record.Before,record.After))return;
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
            if(FetchedCandidate(p)&&(!RebirthRemoteResourceFetchedStationOriginal.CanRebindNativeServer(p.Actor,p.Station,p.Record.Operation,record.Before,record.After,out bool sourceUsed)||sourceUsed&&!after))return;
            if(!FetchedCandidate(p)&&FetchedTransportCandidate(p)&&!RebirthRemoteResourceFetchedStationOriginal.CanRebindCancellationServer(p.Actor,p.Station,p.Record.Operation,record.Before,record.After,
                p.Before.Bag,p.Before.Belt,p.After.Bag,p.After.Belt,p.Before.BagLocks,out _,out _))return;
            if(before)
            {
                if(p.Before.Remote)
                {
                    if(!OwnerSaved(p))
                    {
                        if(FetchedTransportCandidate(p))
                        {
                            // Transport completion is not payment authority.
                            // Retry the exact body until the final native owner
                            // file satisfies OwnerSaved on a later pass.
                            RebirthRemoteResourceFetchedStationOriginal.SendNativeFrame(p.Actor,p.Record.Operation,RemotePayload(p,false));return;
                        }
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
                var original=RebirthCamperStationSnapshot.Restore(p.Station.InstallationId,installed,p.Before.StationImage);
                bool recycler=RebirthRecyclerRuntime.HasGroups(original.Queue)||RebirthRecyclerRuntime.HasGroups(candidate.Queue);
                var changedRow=candidate.Queue[p.After.QueueSlot];
                if(!RebirthCamperStationNativeCrafting.RestoreActorImages(changedRow?.Recipe))return;
                var queue=(RecipeQueueItem[])p.Station.Queue.Clone();queue[p.After.QueueSlot]=changedRow;
                if(recycler)
                {
                    // Entire grouped payment was already validated in Begin and held
                    // by the exact original before/after station-image comparison.
                    queue=candidate.Queue;
                    var input=ItemStack.Clone(p.Station.Input);
                    for(int i=0;i<input.Length;i++)
                    {
                        if(candidate.Input[i].count==original.Input[i].count)continue;
                        if(candidate.Input[i].count==0)input[i]=ItemStack.Empty.Clone();
                        else if(input[i].itemValue.type==candidate.Input[i].itemValue.type)input[i].count=candidate.Input[i].count;
                        else return;
                    }
                    p.Station.Input=input;
                    // The full guarded station image includes native output/tool
                    // completion changes coupled to the same actor bonus receipt.
                    p.Station.Output=candidate.Output;p.Station.Tools=candidate.Tools;
                }
                p.Station.Queue=queue;p.Station.lastTickTime=0;p.Station.RestoreRevision(candidate.Revision);
                RebirthCamperModuleRuntime.For(p.Station.BoundVehicle).Persist(true);
            }
            if(!Current(p)||!OwnerSaved(p))return;
            if(p.VehicleSave==null)p.VehicleSave=RebirthCamperSavedVehicle.RequestSave(p.Station.BoundVehicle);
            if(!RebirthCamperSavedVehicle.Verify(p.Station.BoundVehicle,p.VehicleSave,()=>Current(p))||!Current(p))return;
            if(p.Before.OriginalDeaths.HasValue&&!RebirthRemoteResourceStationPaymentWitness.ObserveSavedNativePayment(p.Actor,p.Station,p.Record,p.Address.ToString(),p.Creation,p.VehicleSave))return;
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
    {
        if(!ThreadManager.IsMainThread())return;
        if(pending.Count!=0)foreach(var p in pending.Values.ToArray())Advance(p);
        if(retirements.Count!=0)foreach(var p in retirements.Values.ToArray())SendRetirement(p);
    }
    private static void Shutdown(ref ModEvents.SWorldShuttingDownData data){pending.Clear();retirements.Clear();clientHolds.Clear();permit=null;recovered=new ConditionalWeakTable<EntityVehicle,RecoveryScope>();} // Durable journal retained, never refund/replay.
    private const string RetiredPrefix="rbCamperNativeRetired_";
    private static Guid RetirementOperation(Guid original)
    {byte[] bytes;using(var hash=System.Security.Cryptography.SHA256.Create())bytes=hash.ComputeHash(Encoding.UTF8.GetBytes(original.ToString("N")+":retired"));return new Guid(bytes.Take(16).ToArray());}
    private static byte[] RemotePayload(Pending p,bool retired)
        =>EncodeRemotePayload(p.Actor.entityId,p.Record.Operation,retired,p.Creation,p.Address,p.Record.Before,p.Record.After);
    private static byte[] EncodeRemotePayload(int actor,Guid operation,bool retired,string creation,RebirthCamperStationAddress address,byte[] before,byte[] after)
    {
        var node=new XElement("camperNativeActorPayment",new XAttribute("version",1),new XAttribute("actor",actor),new XAttribute("original",operation.ToString("N")),
            new XAttribute("retired",retired),new XAttribute("creation",creation),new XAttribute("address",address.ToString()),
            new XElement("before",Convert.ToBase64String(before)),new XElement("after",Convert.ToBase64String(after)));
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
                before.Owner!=after.Owner||before.OwnerKey!=after.OwnerKey||before.WorldKey!=after.WorldKey||before.OriginalDeaths!=after.OriginalDeaths||before.OwnedBelt!=after.OwnedBelt||
                !SameLocks(before.BagLocks,after.BagLocks)||!SameLocks(before.BeltLocks,after.BeltLocks)||before.Bag.Length!=after.Bag.Length||before.Belt.Length!=after.Belt.Length||
                (string)e.Attribute("creation")!="native-mode:"+before.Owner)return false;
            hold=new ClientHold{Original=original,Address=address,Creation=(string)e.Attribute("creation"),Before=before,After=after,Payload=(byte[])bytes.Clone()};return true;
        }
        catch{return false;}
    }
    private static bool ClientScope(ClientHold p)
    {
        var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;var peers=manager?.connectionToServer;
        return ThreadManager.IsMainThread()&&p.Player!=null&&p.World!=null&&p.World.IsRemote()&&ReferenceEquals(GameManager.Instance?.World,p.World)&&
            ReferenceEquals(p.Player.world,p.World)&&ReferenceEquals(p.World.GetPrimaryPlayer(),p.Player)&&ReferenceEquals(p.World.GetEntity(p.Player.entityId),p.Player)&&
            p.Player.IsSpawned()&&!p.Player.IsDead()&&SameLife(p.Before,p.Player)&&!RebirthSurvivorMode.IsEnabledForCurrentWorld()&&
            p.Creation==RebirthCamperCharacterScope.ResolveLocal(p.Player)&&p.Player.inventory.Length==p.Before.OwnedBelt&&
            Guid.TryParse(GamePrefs.GetString(EnumGamePrefs.GameGuidClient),out var actualWorld)&&Guid.TryParse(p.Before.WorldKey,out var expectedWorld)&&actualWorld==expectedWorld&&
            peers!=null&&peers.Length>0&&ReferenceEquals(peers[0],p.Peer)&&peers[0]!=null&&!peers[0].IsDisconnected()&&
            SameLocks(p.Before.BagLocks,Locks(p.Player.bag.LockedSlots))&&SameLocks(p.Before.BeltLocks,Locks(p.Player.inventory.ItemGrid.SlotLocks));
    }
    private static bool ClientCurrent(ClientHold p)=>ClientScope(p)&&
        (RebirthRemoteResourceFetchedStationOriginal.IsCancellationClientCandidate(p.Original)?
            !OtherOwnerHold(p.Player)&&p.Player.PlayerUI?.xui?.DragAndDropWindow?.IsEmpty()==true&&
            RebirthRemoteResourceFetchedStationOriginal.MatchesCancellationPayload(p.Player,p.Original,p.Address,p.Creation,p.Payload):
         RebirthRemoteResourceFetchedStationOriginal.IsClientCandidate(p.Original)?
            !clientHolds.Any(pair=>pair.Key!=p.Original&&ReferenceEquals(pair.Value.Player,p.Player))&&
            RebirthRemoteResourceFetchedStationOriginal.MatchesClientPayload(p.Player,p.Original,p.Address,p.Creation,p.Payload,p.Before.Bag,p.Before.Belt,p.After.Bag,p.After.Belt):
            !OtherOwnerHold(p.Player)&&p.Player.PlayerUI?.xui?.DragAndDropWindow?.IsEmpty()==true);
    private static bool ObserveCancellationTerminal(ClientHold p,RebirthStablePlayerIdentity identity)
    {
        var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;var peers=manager?.connectionToServer;
        if(!ThreadManager.IsMainThread()||p.Player==null||!p.Before.OriginalDeaths.HasValue||!OwnerLife(p.Player,p.Before,p.Creation)||
            !ReferenceEquals(GameManager.Instance?.World,p.Player.world)||!p.Player.world.IsRemote()||!ReferenceEquals(p.Player.world.GetPrimaryPlayer(),p.Player)||
            !ReferenceEquals(p.Player.world.GetEntity(p.Player.entityId),p.Player)||!OwnerWorld(p.Player,p.Before)||peers==null||peers.Length==0||!ReferenceEquals(peers[0],p.Peer)||peers[0]==null||peers[0].IsDisconnected()||
            !RebirthRemoteResourceFetchedStationOriginal.MatchesCancellationTerminalPayload(p.Player,p.Original,p.Address,p.Creation,p.Payload)||
            !RebirthCamperEnergySavedOwner.TryRead(identity,out _,out var saved)||saved.id!=p.Player.entityId||saved.deaths!=p.Before.OriginalDeaths.Value)return false;
        return SavedFrame(identity,p.After,p.Original,RetiredPrefix,1f,false,saved);
    }
    private static bool SavedFrame(RebirthStablePlayerIdentity identity,Frame expected,Guid operation,string prefix,float marker,bool exact,PlayerDataFile checkpoint=null,int? actorId=null)
    {
        var saved=checkpoint;
        if(identity==null||identity.CanonicalId!=expected.Owner||identity.StorageKey!=expected.OwnerKey||saved==null&&!RebirthGearNativePlayerFile.TryRead(identity,out saved)||
            saved.buffData==null||saved.buffData.Length>1024*1024||exact&&(!Same(RebirthGearPlayerDataInventory.ReadSlots(saved,true),expected.Bag)||!Same(RebirthGearPlayerDataInventory.ReadSlots(saved,false),expected.Belt)))return false;
        if(expected.OriginalDeaths.HasValue)
        {int? currentId=actorId??(GameManager.Instance?.World?.IsRemote()==true?GameManager.Instance.World.GetPrimaryPlayer()?.entityId:null);if(!currentId.HasValue||saved.id!=currentId.Value||saved.deaths!=expected.OriginalDeaths.Value)return false;}
        using(var stream=new MemoryStream(saved.buffData.ToArray(),false))using(var reader=MemoryPools.poolBinaryReader.AllocSync(true))
        {
            reader.SetBaseStream(stream);int version=reader.ReadByte();if(version!=EntityBuffs.Version||version<2)return false;
            int count=reader.ReadUInt16();for(int i=0;i<count;i++)new BuffValue().Read(reader,version);
            int variables=reader.ReadUInt16();bool found=false;string wanted=prefix+operation.ToString("N");
            for(int i=0;i<variables;i++){string key=reader.ReadString();float value=reader.ReadSingle();if(key==wanted){if(found||value!=marker)return false;found=true;}}
            return found&&stream.Position==stream.Length;
        }
    }
    internal static bool TryGetSavedFetchedRetirement(EntityPlayerLocal player,Guid job,out byte[] payload)
    {
        payload=null;
        if(!IsInstalled||player==null||!RebirthRemoteResourceFetchedStationOriginal.TryReadClientPayload(player,job,out var retained)||
            !ReadPayload(retained,player.entityId,job,out var p,out var retired)||retired||!RebirthStablePlayerIdentity.TryFromLocalPlatform(out var identity)||
            identity.CanonicalId!=p.Before.Owner||identity.StorageKey!=p.Before.OwnerKey)return false;
        var peers=SingletonMonoBehaviour<ConnectionManager>.Instance?.connectionToServer;if(peers==null||peers.Length==0||peers[0]==null)return false;
        p.Player=player;p.World=player.world;p.Peer=peers[0];
        if(!ClientCurrent(p)&&!FetchedRetirementMetadata(p,identity,true)||!Same(player.bag.ItemGrid.items,p.After.Bag)||!Same(player.inventory.ItemGrid.items,p.After.Belt)||
            !SavedFrame(identity,p.After,job,RetiredPrefix,1f,true))return false;
        payload=retained;return true;
    }
    private static bool FetchedRetirementMetadata(ClientHold p,RebirthStablePlayerIdentity identity,bool retired)
    {
        try
        {
            if(!ClientScope(p)||!RebirthRemoteResourceFetchedStationOriginal.HasRetirementMetadataBinding(p.Player,p.Original,p.Address,p.Creation,p.Payload)||
                !RebirthRemoteResourceClientOriginal.TryRead(p.Original,out var original)||
                !RebirthCamperEnergySavedOwner.TryRead(identity,out var image,out var saved)||saved.id!=p.Player.entityId||saved.deaths!=p.Before.OriginalDeaths||
                !Same(image.Bag,p.After.Bag)||!Same(image.Belt,p.After.Belt)||!Same(p.Player.bag.ItemGrid.items,p.After.Bag)||!Same(p.Player.inventory.ItemGrid.items,p.After.Belt)||
                !RebirthCookingRemoteAdmissionBackend.NativePhase(saved,RebirthRemoteResourceOwnerAction.Receipt(p.Original),out float source,out bool present)||!present||source!=10f||
                p.Player.Buffs.GetCustomVar(RebirthRemoteResourceOwnerAction.Receipt(p.Original))!=10f||
                !SavedFrame(identity,p.After,p.Original,ReceiptPrefix,1f,true,saved))return false;
            var before=RebirthRemoteResourceClientOriginal.ReadActor(original.Copy().Element("actorBefore").Value);
            if(before.Bag.Length!=image.Bag.Length||before.Belt.Length!=image.Belt.Length)return false;
            Array.Copy(image.Bag,before.Bag,image.Bag.Length);Array.Copy(image.Belt,before.Belt,image.Belt.Length);
            if(!before.Matches(image)||!before.Matches(RebirthCamperEnergyActorImage.Capture(p.Player)))return false;
            return !retired||SavedFrame(identity,p.After,p.Original,RetiredPrefix,1f,true,saved);
        }
        catch{return false;}
    }
    internal static bool TryReadFetchedClientRecovery(EntityPlayerLocal player,Guid effect,byte[] payload,
        RebirthCamperEnergyActorImage originalBefore,RebirthCamperEnergyActorImage originalAfter,bool retainedAppliedPayload,out int receipt)
    {
        receipt=0;
        try
        {
            if(!IsInstalled||player==null||!ReadPayload(payload,player.entityId,effect,out var p,out _)||
                !RebirthStablePlayerIdentity.TryFromLocalPlatform(out var identity)||identity.CanonicalId!=p.Before.Owner||identity.StorageKey!=p.Before.OwnerKey||
                originalBefore==null||originalAfter==null||!Same(originalBefore.Bag,p.Before.Bag)||!Same(originalBefore.Belt,p.Before.Belt)||
                !Same(originalAfter.Bag,p.After.Bag)||!Same(originalAfter.Belt,p.After.Belt))return false;
            var peers=SingletonMonoBehaviour<ConnectionManager>.Instance?.connectionToServer;if(peers==null||peers.Length==0||peers[0]==null)return false;
            p.Player=player;p.World=player.world;p.Peer=peers[0];
            if(!ClientScope(p)||clientHolds.Any(pair=>pair.Key!=p.Original&&ReferenceEquals(pair.Value.Player,player))||
                !RebirthCamperEnergySavedOwner.TryRead(identity,out var savedActor,out var saved)||saved.id!=player.entityId||saved.deaths!=p.Before.OriginalDeaths||
                !RebirthCookingRemoteAdmissionBackend.NativePhase(saved,RebirthRemoteResourceOwnerAction.Receipt(p.Original),out float sourcePhase,out bool sourcePresent)||!sourcePresent||sourcePhase!=10f||
                player.Buffs.GetCustomVar(RebirthRemoteResourceOwnerAction.Receipt(p.Original))!=10f)return false;
            var live=RebirthCamperEnergyActorImage.Capture(player);
            bool NonInventory(RebirthCamperEnergyActorImage actual)
            {
                if(actual.Bag.Length!=originalBefore.Bag.Length||actual.Belt.Length!=originalBefore.Belt.Length)return false;
                var unchanged=originalBefore.Copy();Array.Copy(actual.Bag,unchanged.Bag,actual.Bag.Length);Array.Copy(actual.Belt,unchanged.Belt,actual.Belt.Length);
                return unchanged.Matches(actual);
            }
            if(!NonInventory(live)||!NonInventory(savedActor))return false;
            if(!RebirthCookingRemoteAdmissionBackend.NativePhase(saved,ReceiptPrefix+p.Original.ToString("N"),out float phase,out bool present))return false;
            float livePhase=player.Buffs.GetCustomVar(ReceiptPrefix+p.Original.ToString("N"));
            if(!present||phase==0f)
            {
                if(livePhase!=0f||!originalBefore.Matches(live)||!originalBefore.Matches(savedActor))return false;
                receipt=10;return true;
            }
            if(!retainedAppliedPayload||livePhase!=phase)return false;
            if(phase==1f&&originalAfter.Matches(live)&&originalAfter.Matches(savedActor)&&SavedFrame(identity,p.After,p.Original,ReceiptPrefix,1f,true,saved))
            {receipt=1;return true;}
            if(phase!=2f||!Compatible(live.Bag,p.Before.Bag,p.After.Bag)||!Compatible(live.Belt,p.Before.Belt,p.After.Belt)||
                !Compatible(savedActor.Bag,p.Before.Bag,p.After.Bag)||!Compatible(savedActor.Belt,p.Before.Belt,p.After.Belt)||
                !SavedFrame(identity,p.Before,p.Original,ReceiptPrefix,2f,false,saved))return false;
            receipt=2;return true;
        }
        catch{receipt=0;return false;}
    }
    internal static bool TryRetainFetchedCancellation(EntityPlayerLocal player,Guid originalJob,Guid effect,byte[] payload)
    {
        try
        {
            if(!IsInstalled||!ReadPayload(payload,player.entityId,effect,out var p,out bool retired)||!RebirthStablePlayerIdentity.TryFromLocalPlatform(out var identity)||
                identity.CanonicalId!=p.Before.Owner||identity.StorageKey!=p.Before.OwnerKey)return false;
            var peers=SingletonMonoBehaviour<ConnectionManager>.Instance?.connectionToServer;if(peers==null||peers.Length==0||peers[0]==null)return false;
            p.Player=player;p.World=player.world;p.Peer=peers[0];
            if(retired&&ObserveCancellationTerminal(p,identity))return true;
            if(!ClientScope(p)||OtherOwnerHold(player)||RebirthRemoteResourceFetchedStationOriginal.CancellationForeignHold(player)||player.PlayerUI?.xui?.DragAndDropWindow?.IsEmpty()!=true||
                !RebirthCamperEnergySavedOwner.TryRead(identity,out var image,out var saved)||saved.id!=player.entityId||saved.deaths!=p.Before.OriginalDeaths||image.Cursor?.IsEmpty()!=true||
                !SameLocks(image.BagLocks,p.Before.BagLocks)||!RebirthCookingRemoteAdmissionBackend.NativePhase(saved,ReceiptPrefix+p.Original.ToString("N"),out float phase,out bool present))return false;
            bool existing=RebirthRemoteResourceFetchedStationOriginal.HasCancellationPayload(p.Original);
            bool before=Same(image.Bag,p.Before.Bag)&&Same(image.Belt,p.Before.Belt)&&Same(player.bag.ItemGrid.items,p.Before.Bag)&&Same(player.inventory.ItemGrid.items,p.Before.Belt);
            bool applied=Same(image.Bag,p.After.Bag)&&Same(image.Belt,p.After.Belt)&&Same(player.bag.ItemGrid.items,p.After.Bag)&&Same(player.inventory.ItemGrid.items,p.After.Belt);
            if(!present||phase==0f){if(!before||player.Buffs.GetCustomVar(ReceiptPrefix+p.Original.ToString("N"))!=0f)return false;}
            else
            {
                if(!existing||player.Buffs.GetCustomVar(ReceiptPrefix+p.Original.ToString("N"))!=phase)return false;
                if(phase==1f){if(!applied||!SavedFrame(identity,p.After,p.Original,ReceiptPrefix,1f,true,saved))return false;}
                else if(phase!=2f||!Compatible(image.Bag,p.Before.Bag,p.After.Bag)||!Compatible(image.Belt,p.Before.Belt,p.After.Belt)||
                    !Compatible(player.bag.ItemGrid.items,p.Before.Bag,p.After.Bag)||!Compatible(player.inventory.ItemGrid.items,p.Before.Belt,p.After.Belt)||
                    !SavedFrame(identity,p.Before,p.Original,ReceiptPrefix,2f,false,saved))return false;
            }
            return RebirthRemoteResourceFetchedStationOriginal.RetainCancellationPayload(player,originalJob,p.Original,p.Address,p.Creation,payload,
                p.Before.Bag,p.Before.Belt,p.After.Bag,p.After.Belt,p.Before.BagLocks,()=>ClientScope(p));
        }
        catch{return false;}
    }
    internal static bool TryApplyRemote(EntityPlayerLocal player,Guid operation,byte[] payload)
    {
        if(!IsInstalled||player==null||!RebirthStablePlayerIdentity.TryFromLocalPlatform(out var identity)||
            !ReadPayload(payload,player.entityId,operation,out var p,out var retired)||identity.CanonicalId!=p.Before.Owner||identity.StorageKey!=p.Before.OwnerKey)return false;
        var peers=SingletonMonoBehaviour<ConnectionManager>.Instance?.connectionToServer;if(peers==null||peers.Length==0||peers[0]==null)return false;
        p.Player=player;p.World=player.world;p.Peer=peers[0];
        if(retired&&ObserveCancellationTerminal(p,identity))
        {
            if(clientHolds.TryGetValue(p.Original,out var old)&&(!old.Address.Equals(p.Address)||old.Creation!=p.Creation||!Equal(Encode(old.Before),Encode(p.Before))||!Equal(Encode(old.After),Encode(p.After))))return false;
            clientHolds.Remove(p.Original);RebirthRemoteResourceFetchedStationOriginal.ForgetTerminalCancellationLease(player,p.Original,p.Address,p.Creation,p.Payload);return true;
        }
        if(!ClientScope(p))return false;
        if(clientHolds.TryGetValue(p.Original,out var collision)&&(!collision.Address.Equals(p.Address)||collision.Creation!=p.Creation||
            !Equal(Encode(collision.Before),Encode(p.Before))||!Equal(Encode(collision.After),Encode(p.After))))return false;
        if(retired&&RebirthRemoteResourceFetchedStationOriginal.IsClientCandidate(p.Original)&&
            RebirthRemoteResourceFetchedStationOriginal.HasClientUsedTerminal(player,p.Original,p.Address,p.Creation,payload,out _,out _,out _)&&
            SavedFrame(identity,p.After,p.Original,RetiredPrefix,1f,false))return true; // Metadata ACK only; no setters or permission.
        if(retired&&FetchedRetirementMetadata(p,identity,false))
        {
            // Authenticated host-used attestation plus the already Applied
            // payload permit only this terminal scalar, without a UI lease.
            player.Buffs.SetCustomVar(RetiredPrefix+p.Original.ToString("N"),1f,true);
            GameManager.Instance.SaveLocalPlayerData();GameManager.Instance.TriggerSendOfLocalPlayerDataFile(0f);
            if(!FetchedRetirementMetadata(p,identity,true))return false;
            clientHolds.Remove(p.Original);return true;
        }
        if(RebirthRemoteResourceFetchedStationOriginal.IsClientCandidate(p.Original))
        {
            if(clientHolds.Any(pair=>pair.Key!=p.Original&&ReferenceEquals(pair.Value.Player,player)))return false;
            if(!RebirthRemoteResourceFetchedStationOriginal.MatchesClientPayload(player,p.Original,p.Address,p.Creation,p.Payload,p.Before.Bag,p.Before.Belt,p.After.Bag,p.After.Belt)&&
                !RebirthRemoteResourceFetchedStationOriginal.RetainClientPayload(player,p.Original,p.Address,p.Creation,p.Payload,p.Before.Bag,p.Before.Belt,p.After.Bag,p.After.Belt,()=>ClientScope(p)))return false;
        }
        if(!ClientCurrent(p))return false;
        if(retired)
        {
            // Saved terminal from an already-applied journal can be acknowledged
            // again after legitimate later movement; never re-create source hold.
            if(SavedFrame(identity,p.After,p.Original,RetiredPrefix,1f,false))
            {
                if(RebirthRemoteResourceFetchedStationOriginal.IsCancellationClientCandidate(p.Original))clientHolds.Remove(p.Original);
                RebirthRemoteResourceFetchedStationOriginal.ForgetCancellationLease(player,p.Original,p.Address,p.Creation,p.Payload);return true;
            }
            // Cold reconnect may retire only the exact saved postimage; it does
            // not debit again or accept a merely compatible partial source.
            if(!clientHolds.TryGetValue(p.Original,out var retained))retained=p;
            else if(!ClientCurrent(retained)&&(RebirthRemoteResourceFetchedStationOriginal.HasClientRecoveryBinding(player,p.Original,p.Payload)||
                RebirthRemoteResourceFetchedStationOriginal.MatchesCancellationPayload(player,p.Original,p.Address,p.Creation,p.Payload))&&ClientCurrent(p)&&
                retained.Address.Equals(p.Address)&&retained.Creation==p.Creation&&Equal(Encode(retained.Before),Encode(p.Before))&&Equal(Encode(retained.After),Encode(p.After)))retained=p;
            if(!ClientCurrent(retained)||!retained.Address.Equals(p.Address)||!Equal(Encode(retained.Before),Encode(p.Before))||!Equal(Encode(retained.After),Encode(p.After))||
                !Same(player.bag.ItemGrid.items,p.After.Bag)||!Same(player.inventory.ItemGrid.items,p.After.Belt)||!SavedFrame(identity,p.After,p.Original,ReceiptPrefix,1f,true))return false;
            player.Buffs.SetCustomVar(RetiredPrefix+p.Original.ToString("N"),1f,true);GameManager.Instance.SaveLocalPlayerData();
            if(!ClientCurrent(retained)||!SavedFrame(identity,p.After,p.Original,RetiredPrefix,1f,true))return false;
            RebirthRemoteResourceFetchedStationOriginal.ForgetCancellationLease(player,p.Original,p.Address,p.Creation,p.Payload);
            clientHolds.Remove(p.Original);return true;
        }
        if(clientHolds.TryGetValue(p.Original,out var prior))
        {
            if(!ClientCurrent(prior)||!Equal(prior.Payload,p.Payload))
            {
                if((!RebirthRemoteResourceFetchedStationOriginal.HasClientRecoveryBinding(player,p.Original,p.Payload)&&
                    !RebirthRemoteResourceFetchedStationOriginal.MatchesCancellationPayload(player,p.Original,p.Address,p.Creation,p.Payload))||!ClientCurrent(p)||
                    !prior.Address.Equals(p.Address)||prior.Creation!=p.Creation||!Equal(Encode(prior.Before),Encode(p.Before))||!Equal(Encode(prior.After),Encode(p.After)))return false;
                p.ApplyingSaved=false;clientHolds[p.Original]=p;
            }
            else p=prior;
        }
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
                var once=permit;bool applied=true;
                try
                {
                    if(RebirthRemoteResourceFetchedStationOriginal.IsClientCandidate(p.Original))
                    {
                        if(!RebirthRemoteResourceFetchedStationOriginal.TryGetClientPayloadBinding(player,p.Original,p.Address,p.Creation,p.Payload,p.Before.Bag,p.Before.Belt,p.After.Bag,p.After.Belt,out string binding))return false;
                        applied=RebirthRemoteResourceOwnerAction.WriteFetchedNativeSlot(player,p.Original,binding,area==0?player.bag.ItemGrid:player.inventory.ItemGrid,slot,item,
                            ()=>{if(area==0)player.bag.SetSlot(slot,item);else player.inventory.SetItem(slot,item.itemValue,item.count);});
                    }
                    else if(area==0)player.bag.SetSlot(slot,item);else player.inventory.SetItem(slot,item.itemValue,item.count);
                }
                finally{permit=null;}
                if(!applied||!once.Used)return false;
            }
        }
        if(!ClientCurrent(p)||!Same(player.bag.ItemGrid.items,p.After.Bag)||!Same(player.inventory.ItemGrid.items,p.After.Belt))return false;
        player.Buffs.SetCustomVar(ReceiptPrefix+p.Original.ToString("N"),1f,true);GameManager.Instance.SaveLocalPlayerData();
        return ClientCurrent(p)&&SavedFrame(identity,p.After,p.Original,ReceiptPrefix,1f,true);
    }
    internal static bool VerifyRemoteSaved(EntityPlayer player,Guid operation,byte[] payload,Func<bool> current)
    {
        if(current==null||!current()||!RemoteIdentity(player,out var identity,out var sender)||!ReadPayload(payload,player.entityId,operation,out var p,out var retired)||
            !SameLife(p.Before,player)||p.Before.Owner!=identity.CanonicalId||p.Before.OwnerKey!=identity.StorageKey||p.Creation!=RebirthCamperCharacterAccess.CurrentCreation(player)||
            p.Before.OriginalDeaths.HasValue&&(sender.latestPlayerData==null||sender.latestPlayerData.id!=player.entityId||sender.latestPlayerData.deaths!=p.Before.OriginalDeaths.Value))return false;
        var record=Journal().Read(p.Original);
        if(record==null||record.Vehicle!=p.Address.Vehicle||record.OwnerCreation!=p.Creation||!Equal(record.Before,Encode(p.Before))||!Equal(record.After,Encode(p.After)))return false;
        if(retired)return record.State==RebirthCamperTransferJournal.Phase.Applied&&SavedFrame(identity,p.After,p.Original,RetiredPrefix,1f,false,actorId:player.entityId)&&current();
        return record.State==RebirthCamperTransferJournal.Phase.Uncertain&&SavedFrame(identity,p.After,p.Original,ReceiptPrefix,1f,true,actorId:player.entityId)&&current();
    }
    private static void SendRetirement(Pending p)
    {
        if(p.Before.Remote&&p.Before.OriginalDeaths.HasValue&&p.Record.State==RebirthCamperTransferJournal.Phase.Applied&&UnityEngine.Time.realtimeSinceStartup>=p.TerminalProbeNext)
        {
            p.TerminalProbeNext=UnityEngine.Time.realtimeSinceStartup+1f;
            var clients=SingletonMonoBehaviour<ConnectionManager>.Instance?.Clients.List;
            if(clients!=null)foreach(var peer in clients.ToArray())
            {
                var actor=GameManager.Instance?.World?.GetEntity(peer.entityId) as EntityPlayer;
                if(actor==null||actor.Died!=p.Before.OriginalDeaths.Value||!RemoteIdentity(actor,out var owner,out _)||owner.CanonicalId!=p.Before.Owner||owner.StorageKey!=p.Before.OwnerKey)continue;
                if(RebirthRemoteResourceFetchedStationOriginal.ObserveCancellationTerminalServer(actor,p.Record.Operation,p.Address.ToString(),p.Creation,p.Record.Before,p.Record.After))
                {retirements.Remove(p.Record.Operation);return;} // Already saved native retired1; metadata only.
            }
        }
        if(FetchedTransportCandidate(p)&&p.Actor!=null&&p.Sender!=null&&
            !ReferenceEquals(GameManager.Instance?.World?.GetEntity(p.Actor.entityId),p.Actor)&&
            !ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance?.Clients.ForEntityId(p.Actor.entityId),p.Sender))
        {
            var clients=SingletonMonoBehaviour<ConnectionManager>.Instance?.Clients.List;
            if(clients!=null)foreach(var peer in clients.ToArray())
            {
                var actor=GameManager.Instance?.World?.GetEntity(peer.entityId) as EntityPlayer;
                if(actor==null||!RemoteIdentity(actor,out var candidate,out var returningSender)||candidate.CanonicalId!=p.Before.Owner||candidate.StorageKey!=p.Before.OwnerKey||
                    !CanRebindFetchedServer(p,actor,candidate,returningSender,true))continue;
                // Retained Applied publication only. No lease, queue or item
                // mutation is possible in this retirement dictionary.
                p.Actor=actor;p.Identity=candidate;p.Sender=returningSender;break;
            }
        }
        if(UnityEngine.Time.realtimeSinceStartup<p.Next||p.RetirementSent||p.Actor==null||!SameLife(p.Before,p.Actor)||p.Actor.world==null||p.Actor.world.IsRemote()||!RemoteIdentity(p.Actor,out var identity,out var sender)||
            !ReferenceEquals(sender,p.Sender)||identity.CanonicalId!=p.Before.Owner||identity.StorageKey!=p.Before.OwnerKey||RebirthCamperCharacterAccess.CurrentCreation(p.Actor)!=p.Creation)return;
        p.Next=UnityEngine.Time.realtimeSinceStartup+1f;
        if(FetchedTransportCandidate(p))
        {
            var payload=RemotePayload(p,true);var operation=RetirementOperation(p.Record.Operation);
            bool Scope()=>p.Actor!=null&&SameLife(p.Before,p.Actor)&&ReferenceEquals(GameManager.Instance?.World,p.Actor.world)&&
                ReferenceEquals(p.Actor.world.GetEntity(p.Actor.entityId),p.Actor)&&!p.Actor.IsDead()&&p.Actor.IsSpawned()&&
                p.Directory==GameIO.GetSaveGameDir()&&RemoteIdentity(p.Actor,out var current,out var native)&&ReferenceEquals(native,p.Sender)&&
                current.CanonicalId==p.Before.Owner&&current.StorageKey==p.Before.OwnerKey&&RebirthCamperCharacterAccess.CurrentCreation(p.Actor)==p.Creation;
            if(VerifyRemoteSaved(p.Actor,operation,payload,Scope))retirements.Remove(p.Record.Operation);
            else RebirthRemoteResourceFetchedStationOriginal.SendNativeFrame(p.Actor,operation,payload,p.Station);
            return;
        }
        p.RetirementSent=RebirthCamperRemoteOwnerApplication.SendOwnerApplication(p.Actor,RetirementOperation(p.Record.Operation),RebirthCamperRemoteOwnerEffectKind.Debit,RemotePayload(p,true),
            success=>{p.RetirementSent=false;if(success)retirements.Remove(p.Record.Operation);});
    }
    internal static bool TryResumeFetchedRetirement(EntityPlayer actor,RebirthCamperStationState station,RebirthCamperStationAddress address,string creation,Guid job)
    {
        try
        {
            if(!IsInstalled||!RemoteIdentity(actor,out var identity,out var sender)||actor.IsDead()||!actor.IsSpawned()||
                !ReferenceEquals(GameManager.Instance?.World?.GetEntity(actor.entityId),actor)||creation!=RebirthCamperCharacterAccess.CurrentCreation(actor))return false;
            var record=Journal().Read(job);if(record?.State!=RebirthCamperTransferJournal.Phase.Applied||record.Vehicle!=address.Vehicle||record.OwnerCreation!=creation)return false;
            var before=Decode(record.Before);var after=Decode(record.After);
            if(!before.Remote||!before.OriginalDeaths.HasValue||before.OriginalDeaths!=actor.Died||before.Owner!=identity.CanonicalId||before.OwnerKey!=identity.StorageKey||before.Station!=address.Installation||
                !RebirthRemoteResourceFetchedStationOriginal.CanRebindNativeServer(actor,station,job,record.Before,record.After,out bool used,out var image,out var saved)||!used||
                !Same(image.Bag,after.Bag)||!Same(image.Belt,after.Belt)||!SavedFrame(identity,after,job,ReceiptPrefix,1f,true,saved))return false;
            if(retirements.TryGetValue(job,out var old))
            {
                if(!Equal(old.Record.Before,record.Before)||!Equal(old.Record.After,record.After))return false;
                if(!ReferenceEquals(old.Actor,actor)||!ReferenceEquals(old.Sender,sender))
                {if(!CanRebindFetchedServer(old,actor,identity,sender,true))return false;old.Actor=actor;old.Identity=identity;old.Sender=sender;}
                old.Directory=GameIO.GetSaveGameDir();old.RetirementSent=false;SendRetirement(old);return true;
            }
            var terminal=new Pending{Actor=actor,Identity=identity,Sender=sender,Station=station,Address=address,Creation=creation,Record=record,Before=before,After=after,Directory=GameIO.GetSaveGameDir()};
            retirements.Add(job,terminal);SendRetirement(terminal);return true;
        }
        catch{return false;}
    }
    // Read-only status of an ALREADY retained native payment original. It
    // never resumes, advances, retires, or derives rejection from live absence.
    internal static bool TryReadCommandStatusOriginal(EntityPlayer actor,RebirthCamperStationAddress address,string creation,object lease,Guid operation,
        out RebirthCamperRemoteStationCommandStatusNativeOriginal original)
    {
        original=null;
        try
        {
            if(!ThreadManager.IsMainThread()||actor==null||actor.IsDead()||!RemoteIdentity(actor,out var identity,out _)||
                !RebirthCamperRemoteStationTransport.IsCurrent(actor,address,creation,lease)||creation!=RebirthCamperCharacterAccess.CurrentCreation(actor))return false;
            string directory=Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH","NativeActorPayments");if(!Directory.Exists(directory))return false;
            for(var ancestor=new DirectoryInfo(directory);ancestor!=null;ancestor=ancestor.Parent)
                if(ancestor.Exists&&(ancestor.Attributes&FileAttributes.ReparsePoint)!=0)return false;
            string prefix=operation.ToString("N")+".";int candidates=0;
            foreach(string file in Directory.EnumerateFileSystemEntries(directory,operation.ToString("N")+"*"))
                if(++candidates>4||!new[]{prefix+"0.rcj",prefix+"1.rcj",prefix+"2.rcj",prefix+"3.rcj"}.Contains(Path.GetFileName(file))||!File.Exists(file)||(File.GetAttributes(file)&FileAttributes.ReparsePoint)!=0)return false;
            var record=Journal().Read(operation);
            if(record==null||record.Vehicle!=address.Vehicle||record.OwnerCreation!=creation||record.State==RebirthCamperTransferJournal.Phase.Applied)return false;
            byte[] beforeBytes=record.Before,afterBytes=record.After;
            var before=Decode(beforeBytes);var after=Decode(afterBytes);
            if(!before.Remote||!after.Remote||!before.OriginalDeaths.HasValue||before.OriginalDeaths!=after.OriginalDeaths||before.Station!=address.Installation||after.Station!=address.Installation||
                before.Owner!=identity.CanonicalId||after.Owner!=identity.CanonicalId||before.OwnerKey!=identity.StorageKey||after.OwnerKey!=identity.StorageKey||before.WorldKey!=actor.world.worldState.Guid||after.WorldKey!=before.WorldKey)return false;
            if(!RebirthRemoteResourceStationPaymentWitness.TryReadSavedBefore(actor.world,operation,address.ToString(),identity.StorageKey,creation,beforeBytes,afterBytes,out var savedBefore,out var definition)||
                !RebirthNativeItemCodec.TryDecode(definition,out var installed))return false;
            var prior=RebirthCamperStationSnapshot.Restore(address.Installation,new ItemStack(installed,1),before.StationImage);
            var next=RebirthCamperStationSnapshot.Restore(address.Installation,new ItemStack(installed,1),after.StationImage);
            // Read validates complete phase chain: Rejected may follow Prepared
            // only, and native setters are downstream of durable Uncertain.
            string proof;
            using(var sha=System.Security.Cryptography.SHA256.Create())
            {
                byte[] header=Encoding.UTF8.GetBytes("camper.command.native.status.v1:"+operation.ToString("N")+":"+(byte)record.State+":"+savedBefore+":"+beforeBytes.Length+":"+afterBytes.Length+":");
                sha.TransformBlock(header,0,header.Length,header,0);sha.TransformBlock(beforeBytes,0,beforeBytes.Length,beforeBytes,0);sha.TransformFinalBlock(afterBytes,0,afterBytes.Length);
                proof=BitConverter.ToString(sha.Hash).Replace("-","").ToLowerInvariant();
            }
            original=new RebirthCamperRemoteStationCommandStatusNativeOriginal(record.State,prior,next,proof,before.OriginalDeaths.Value);return true;
        }
        catch{original=null;return false;}
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
            if(!before.Remote||!SameLife(before,player)||before.Station!=address.Installation||before.Owner!=identity.CanonicalId||before.OwnerKey!=identity.StorageKey)continue;
            if(SavedFrame(identity,after,record.Operation,RetiredPrefix,1f,false,actorId:player.entityId))continue;
            if(!retirements.TryGetValue(record.Operation,out var terminal))
            {
                terminal=new Pending{Actor=player,Identity=identity,Sender=sender,Station=station,Address=address,Creation=creation,Record=record,Before=before,After=after};
                if(FetchedTransportCandidate(terminal))
                {
                    terminal.Directory=GameIO.GetSaveGameDir();
                    bool valid=FetchedCandidate(terminal)?RebirthRemoteResourceFetchedStationOriginal.CanRebindNativeServer(player,station,record.Operation,record.Before,record.After,out bool used)&&used:
                        RebirthRemoteResourceFetchedStationOriginal.CanRebindCancellationServer(player,station,record.Operation,record.Before,record.After,before.Bag,before.Belt,after.Bag,after.Belt,before.BagLocks,out _,out _);
                    if(!valid)continue;
                }
                retirements.Add(record.Operation,terminal);
            }
            else
            {
                if(FetchedTransportCandidate(terminal)&&(!ReferenceEquals(terminal.Actor,player)||!ReferenceEquals(terminal.Sender,sender))&&!CanRebindFetchedServer(terminal,player,identity,sender,true))continue;
                terminal.Actor=player;terminal.Sender=sender;terminal.Identity=identity;terminal.RetirementSent=false;
            }
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
        return player==null||!RebirthCamperStationNativeActorPayment.Blocks(player.bag)&&!RebirthCamperStationNativeActorPayment.Blocks(player.inventory)&&!RebirthDogLifecycleService.HireBlocksInventory(player.bag)&&!RebirthDogLifecycleService.HireBlocksInventory(player.inventory)&&!RemoteResourceTransactions.ActionBlocksNative(player.bag)&&!RemoteResourceTransactions.ActionBlocksNative(player.inventory);
    }
}

[HarmonyPatch(typeof(XUiM_Workstation),nameof(XUiM_Workstation.SetRecipeQueueItems))]
internal static class RebirthCamperNativeActorQueueGuard
{
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(XUiM_Workstation __instance)
    {
        var station=__instance?.TileEntity as RebirthCamperStationState;
        // The active local campfire window owns its native queue setter. An old
        // actor-payment hold must not leave a Take-cleared card queued forever.
        if(station?.block is BlockCampfire&&station.BoundVehicle?.world?.IsRemote()==false)return true;
        return !RebirthCamperStationNativeActorPayment.IsHeld(station)&&
            !RebirthRemoteResourceFetchedStationOriginal.BlocksStation(station);
    }
}

[HarmonyPatch]
internal static class RebirthCamperNativeActorMutationGuard
{
    private static IEnumerable<MethodBase> TargetMethods()
    {foreach(var type in new[]{typeof(Bag),typeof(Inventory)})foreach(var method in type.GetMethods(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance))
        if(method.DeclaringType==type&&new[]{"SetSlot","SetSlots","SetItem","DecItem","AddItem","AddItemAt","ChangeSlotCount","clearSlotByIndex"}.Contains(method.Name))yield return method;}
    private static bool Prefix(object __instance,MethodBase __originalMethod,object[] __args)=>RebirthCamperStationNativeActorPayment.BeforeMutation(__instance,__originalMethod,__args)&&RebirthDogLifecycleService.BeforeHireInventoryMutation(__instance,__originalMethod,__args)&&RemoteResourceTransactions.ActionAllowsOwnerMutation(__instance,__originalMethod,__args);
    private static void Postfix(object __instance,MethodBase __originalMethod,object[] __args,bool __runOriginal)=>RemoteResourceTransactions.ObserveActionOwnerMutation(__instance,__originalMethod,__args,__runOriginal);
}

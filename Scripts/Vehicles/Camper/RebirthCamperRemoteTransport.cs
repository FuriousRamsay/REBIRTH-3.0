using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Scripting;

// Normal native owner inventory publication plus the existing camper receipt.
// The request never supplies a vehicle candidate: the locked server prepares it.
public static class RebirthCamperRemoteTransport
{
    internal const int MaximumFrame = 8 * 1024 * 1024;
    internal const byte Snapshot = 0, Prepare = 1, Commit = 2, Cancel = 3, GridPrepare = 4, QuickPrepare = 5;
    internal const byte QueueOverview=6;
    internal const byte View = 4, Error = 5, RemovalContents = 6,QueueOverviewView=7;
    private static Func<EntityPlayer,string> creationProvider;
    private static readonly HashSet<Guid> observing = new HashSet<Guid>();
    public static bool IsRegistered => creationProvider != null;

    public static void Register(Func<EntityPlayer,string> authenticatedCreation)
    {
        if (authenticatedCreation == null) throw new ArgumentNullException(nameof(authenticatedCreation));
        if (creationProvider != null) throw new InvalidOperationException("Remote camper character provider already registered.");
        // ModAPI initialization precedes native StartServer/StartClient mappings.
        // Native discovery/handshake registers package classes; adapter install
        // must not dereference GetPackageId's still-null mapping dictionary.
        creationProvider = authenticatedCreation;
    }

    internal static void Handle(World world, ClientInfo sender, int playerId, int vehicleId,
        Guid request, Guid operation, byte action, bool crafting, long storageRevision,
        long stationRevision, byte[] offered)
    {
        if (!ThreadManager.IsMainThread() || !IsRegistered || world == null || world.IsRemote()
            || request == Guid.Empty || sender == null || sender.entityId != playerId) return;
        var player = world.GetEntity(playerId) as EntityPlayer;
        var vehicle = world.GetEntity(vehicleId) as EntityVehicle;
        string creation = player == null ? null : creationProvider(player);
        string receiptOwner = sender.InternalId == null ? null : "remote:" + sender.InternalId.CombinedString + ":" + creation;
        string directory = GameIO.GetSaveGameDir();
        Func<bool> current = () => ReferenceEquals(GameManager.Instance?.World,world)
            && GameIO.GetSaveGameDir() == directory && !string.IsNullOrWhiteSpace(creation)
            && ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance.Clients.ForEntityId(playerId),sender)
            && RebirthCamperRemoteAdmission.CanAccess(vehicle,player,sender,() => creationProvider(player) == creation);
        try
        {
            if (!current() || string.IsNullOrEmpty(directory)) throw new InvalidOperationException("Camper access expired.");
            if(action==QueueOverview)
            {
                // Informational status only. Do not construct a runtime, open
                // a journal, recover custody, save, or publish actor inventory.
                if(operation!=Guid.Empty||!crafting||offered==null||offered.Length!=0
                    ||!RebirthCamperModuleRuntime.TryGetExisting(vehicle,out var existing))return;
                int active=0;for(int i=0;i<RebirthCamperCraftingStation.ModuleSlots;i++)
                    if(existing.Stations.GetSlot(i)?.State.hasRecipeInQueue()==true)active++;
                byte[] frame;
                using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream))
                {writer.Write((byte)1);writer.Write(existing.Stations.Revision);writer.Write(active);writer.Flush();frame=stream.ToArray();}
                if(current())Reply(playerId,vehicleId,request,Guid.Empty,existing.Identity,QueueOverviewView,frame,Array.Empty<byte>());
                return;
            }
            var runtime = RebirthCamperModuleRuntime.For(vehicle);
            var journal = new RebirthCamperTransferJournal(Path.Combine(directory,"REBIRTH","CamperTransfers"));
            if (action == Snapshot)
            {
                var pending = journal.FindPending(runtime.Identity);
                if (pending != null)
                {
                    if (pending.OwnerCreation != receiptOwner) throw new InvalidOperationException("Another character owns a pending transfer.");
                    runtime.SetTransferPending(true);
                    runtime.Persist(true,12);
                    Reply(playerId,vehicleId,request,pending);
                }
                else
                {
                    if(offered!=null&&offered.Length>0)
                    {
                        AssertFrame(offered);
                        var held=RebirthCamperInventorySnapshot.Restore(offered);
                        if(held.Length!=1)throw new InvalidOperationException("One held recovery item required.");
                        if(held[0]?.itemValue?.Metadata?.ContainsKey(RebirthCamperStationRemovalContents.ContentsKey)==true
                            &&RebirthCamperPortableStation.TryGetSourceAddress(held[0],out var source))
                        {
                            foreach(var applied in journal.FindApplied(source.Vehicle,receiptOwner))
                            {
                                var image=RebirthCamperTransferSnapshot.Restore(applied.Vehicle,applied.After);
                                if(!RebirthCamperRemoteStationGridTransfer.Same(image.Cursor,held[0]))continue;
                                Reply(playerId,vehicleId,request,applied.Operation,applied.Vehicle,RemovalContents,applied.Before,applied.After);
                                return;
                            }
                        }
                    }
                    runtime.InitializeEmptyCargo();
                    runtime.Persist(true,12);
                    byte[] view = RebirthCamperTransferSnapshot.Capture(runtime.Storage,runtime.Stations,
                        vehicle.bag.ItemGrid.items,ItemStack.Empty);
                    Reply(playerId,vehicleId,request,Guid.Empty, runtime.Identity,View,view,Array.Empty<byte>());
                }
                return;
            }
            if (operation == Guid.Empty) throw new InvalidOperationException("Transfer receipt required.");
            journal.AssertAvailable(runtime.Identity,operation);
            var receipt = journal.Read(operation);
            if (receipt == null || receipt.Vehicle != runtime.Identity || receipt.OwnerCreation != receiptOwner)
                throw new InvalidOperationException("Authenticated transfer receipt unavailable.");
            if (action == Cancel)
            {
                // Client sends this only before changing its native cursor. A
                // published/uncertain receipt can never be cancelled or rolled back.
                if (receipt.State == RebirthCamperTransferJournal.Phase.Prepared && !observing.Contains(operation))
                {
                    receipt = journal.Advance(operation,receipt.State,RebirthCamperTransferJournal.Phase.Rejected);
                    runtime.SetTransferPending(false);
                    runtime.Persist(true,12);
                }
                Reply(playerId,vehicleId,request,receipt); return;
            }
            if (action != Commit) throw new InvalidOperationException("Unknown camper request.");
            if (receipt.State == RebirthCamperTransferJournal.Phase.Applied || receipt.State == RebirthCamperTransferJournal.Phase.Rejected)
            { Reply(playerId,vehicleId,request,receipt); return; }
            runtime.SetTransferPending(true);
            runtime.Persist(true,12);
            if (!observing.Add(operation)) { Reply(playerId,vehicleId,request,receipt); return; }
            try { GameManager.Instance.StartCoroutine(ObservePublication(Publish(journal,receipt,vehicle,playerId,sender,request,current))); }
            catch { observing.Remove(operation); throw; }
        }
        catch (Exception error)
        {
            Log.Warning("[CamperTransfer] Remote request retained: " + error.Message);
            Reply(playerId,vehicleId,request,operation,Guid.Empty,Error,Array.Empty<byte>(),Array.Empty<byte>());
        }
    }

    internal static void PrepareSlot(World world, ClientInfo sender, int playerId, int vehicleId,
        Guid request, Guid operation, bool crafting, int slot, long storageRevision, long stationRevision, byte[] offered,
        Guid installation=default(Guid),RebirthCamperRemoteStationGrid gridKind=RebirthCamperRemoteStationGrid.Fuel,
        RebirthCamperRemoteStationGesture gesture=RebirthCamperRemoteStationGesture.Swap,bool quick=false)
    {
        // Full shared admission is repeated immediately before preparation and
        // publication. This adapter uses the same normal native inventory trust
        // as vehicle parts; the owner receipt prevents a normal replay debit.
        if (!ThreadManager.IsMainThread() || !IsRegistered || world == null || world.IsRemote()
            || request == Guid.Empty || operation == Guid.Empty || slot < 0 || slot >= RebirthCamperCraftingStation.ModuleSlots) return;
        var player = world.GetEntity(playerId) as EntityPlayer;
        var vehicle = world.GetEntity(vehicleId) as EntityVehicle;
        string creation = player == null ? null : creationProvider(player);
        string receiptOwner = sender?.InternalId == null ? null : "remote:" + sender.InternalId.CombinedString + ":" + creation;
        Func<bool> current = () => !string.IsNullOrWhiteSpace(creation)
            && ReferenceEquals(GameManager.Instance?.World,world)
            && ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance.Clients.ForEntityId(playerId),sender)
            && RebirthCamperRemoteAdmission.CanAccess(vehicle,player,sender,() => creationProvider(player) == creation);
        RebirthCamperModuleRuntime preparedRuntime=null;
        RebirthCamperTransferJournal preparedJournal=null;
        byte[] refusalView=null;
        try
        {
            if (!current()) throw new InvalidOperationException("Camper access expired.");
            var runtime = RebirthCamperModuleRuntime.For(vehicle);
            var journal = new RebirthCamperTransferJournal(Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH","CamperTransfers"));
            preparedRuntime=runtime;preparedJournal=journal;
            journal.AssertAvailable(runtime.Identity,operation);
            var receipt = journal.Read(operation);
            if (receipt != null)
            {
                if (receipt.Vehicle != runtime.Identity || receipt.OwnerCreation != receiptOwner)
                    throw new InvalidOperationException("Receipt identity reused.");
                Reply(playerId,vehicleId,request,receipt); return;
            }
            if (runtime.TransferPending || runtime.Storage.Revision != storageRevision || runtime.Stations.Revision != stationRevision)
                throw new InvalidOperationException("Modules changed; reopen management.");
            bool stationGrid=installation!=Guid.Empty;
            if(!stationGrid)AssertStationsIdle(runtime);
            else if(!RebirthCamperRemoteStationTransport.TryResolve(player,new RebirthCamperStationAddress(runtime.Identity,installation),creation,out var gridStation,out _) ||
                RebirthCamperStationGridCustody.IsHeld(gridStation))
                throw new InvalidOperationException("Current native remote station lease required.");
            int backpackSlot=-1;
            if(quick)offered=RebirthCamperQuickTransferFrame.Read(offered,out backpackSlot);
            ItemStack[] cursor = RebirthCamperInventorySnapshot.Restore(offered);
            if (cursor.Length != (stationGrid?2:1)) throw new InvalidDataException("Exact native cursor/position required.");
            if(quick&&(stationGrid||backpackSlot>=player.bag.ItemGrid.items.Length))
                throw new InvalidOperationException("Native backpack cell changed.");
            var before = RebirthCamperTransferSnapshot.Restore(runtime.Identity,
                RebirthCamperTransferSnapshot.Capture(runtime.Storage,runtime.Stations,vehicle.bag.ItemGrid.items,cursor[0],backpackSlot));
            refusalView=before.Capture();
            if(quick&&!RebirthCamperRemoteStationGridTransfer.Same(player.bag.ItemGrid.items[backpackSlot],cursor[0]))
                throw new InvalidOperationException("Native backpack cell changed.");
            var storage = before.Storage; var stations = before.Stations; var cargo = before.Cargo;
            ItemStack next;
            if(stationGrid)
            {
                var station=stations.Find(installation)?.State;
                var plan=RebirthCamperRemoteStationGridTransfer.Prepare(station,gridKind,slot,gesture,cursor[0],cursor[1]);
                var grid=RebirthCamperRemoteStationGridTransfer.Grid(station,gridKind);
                if(RebirthCamperRemoteStationGridTransfer.Same(grid[slot],plan.Cell)
                    && RebirthCamperRemoteStationGridTransfer.Same(cursor[0],plan.Cursor))
                    throw new InvalidOperationException("Native position transfer has no effect.");
                var replacement=new ItemStack[grid.Length];for(int i=0;i<grid.Length;i++)replacement[i]=grid[i].Clone();
                replacement[slot]=plan.Cell;
                RebirthCamperRemoteStationGridTransfer.SetGrid(station,gridKind,replacement);
                station.SetModified();next=plan.Cursor;
            }
            else if (crafting)
            {
                var plan = RebirthCamperStationTransfer.PrepareCursor(stations,stations.Revision,cursor[0],slot);
                stations = plan.Stations; next = plan.Backpack[0];
            }
            else
            {
                var plan = RebirthCamperStorageTransfer.PrepareCursor(storage,storage.Revision,cursor[0],cargo,slot);
                storage = plan.Storage; cargo = plan.Cargo; next = plan.Backpack[0];
            }
            RebirthCamperTransferCommit.AssertCargoPolicy(vehicle.bag.ItemGrid,before.Cargo,cargo);
            byte[] prior = before.Capture();
            byte[] after = RebirthCamperTransferSnapshot.Capture(storage,stations,cargo,next,backpackSlot);
            AssertFrame(prior); AssertFrame(after);
            if (!current()) throw new InvalidOperationException("Camper access expired before preparing.");
            RebirthCamperStoragePackedTransfer.Prepare(before,RebirthCamperTransferSnapshot.Restore(runtime.Identity,after));
            receipt = journal.Prepare(operation,runtime.Identity,receiptOwner,storageRevision,prior,after);
            runtime.SetTransferPending(true);
            runtime.Persist(true,12);
            Reply(playerId,vehicleId,request,receipt);
        }
        catch (Exception error)
        {
            Log.Warning("[CamperTransfer] Remote preparation refused: " + error.Message);
            // A planning refusal owns no cursor or module mutation. Return a
            // valid unchanged view so normal full-cargo/occupied-cell refusal
            // does not strand the client in Waiting. Once a journal exists,
            // only its recovery phase can authorize another transfer.
            try
            {
                if(refusalView!=null && preparedRuntime!=null && !preparedRuntime.TransferPending
                    && preparedJournal!=null && preparedJournal.Read(operation)==null && current())
                {
                    Reply(playerId,vehicleId,request,operation,preparedRuntime.Identity,
                        (byte)RebirthCamperTransferJournal.Phase.Rejected,refusalView,refusalView);
                    return;
                }
            }
            catch(Exception refusalError)
            { Log.Warning("[CamperTransfer] Refusal retained: "+refusalError.Message); }
            Reply(playerId,vehicleId,request,operation,Guid.Empty,Error,Array.Empty<byte>(),Array.Empty<byte>());
        }
    }

    private static IEnumerator Publish(RebirthCamperTransferJournal journal,RebirthCamperTransferJournal.Record receipt,
        EntityVehicle vehicle,int playerId,ClientInfo sender,Guid request,Func<bool> current)
    {
        try
        {
            var after = RebirthCamperTransferSnapshot.Restore(receipt.Vehicle,receipt.After);
            float deadline = Time.realtimeSinceStartup + 15f;
            // NetPackagePlayerData writes the native owner save before commit.
            // Save workers can finish later, so acknowledge without blocking a frame.
            while (receipt.State == RebirthCamperTransferJournal.Phase.Prepared && current()
                && !RebirthCamperSavedCursor.VerifyPublishedReceipt(sender.InternalId.CombinedString,receipt.Operation,current))
            { if (Time.realtimeSinceStartup >= deadline) yield break; yield return null; }
            if (!current()) yield break;
            if (receipt.State == RebirthCamperTransferJournal.Phase.Prepared)
                receipt = journal.Advance(receipt.Operation,receipt.State,RebirthCamperTransferJournal.Phase.Uncertain);
            // In this endpoint Uncertain is written only after exact durable
            // owner acknowledgement. Reconnect need not pin that cursor forever:
            // a later legitimate inventory move/save does not repeat its debit.
            var runtime = RebirthCamperModuleRuntime.For(vehicle);
            var before=RebirthCamperTransferSnapshot.Restore(receipt.Vehicle,receipt.Before);
            bool gridTransfer=before.Stations.Revision==after.Stations.Revision
                && Equal(before.Storage.Capture(),after.Storage.Capture());
            if(!gridTransfer)AssertStationsIdle(runtime);
            RebirthCamperTransferSnapshot recovered;
            if (!RebirthCamperTransferRecovery.CanFinish(receipt,runtime.Storage,runtime.Stations,
                vehicle.bag.ItemGrid.items,after.Cursor,true,out recovered))
                throw new InvalidOperationException("Unrecognized server state; retain receipt for reconciliation.");
            RebirthCamperTransferCommit.AssertCargoPolicy(vehicle.bag.ItemGrid,vehicle.bag.ItemGrid.items,recovered.Cargo);
            if(!gridTransfer)recovered.Stations.RetainUnchangedStates(runtime.Stations,false);
            vehicle.bag.SetSlots(recovered.Cargo,true);
            if(gridTransfer)ApplyStationGrids(runtime,before,recovered);
            else { recovered.Stations.RetainUnchangedStates(runtime.Stations); runtime.Storage = recovered.Storage; runtime.Stations = recovered.Stations; }
            runtime.Persist(true,12); // one native metadata + cargo publication
            var expected = RebirthCamperSavedVehicle.RequestSave(vehicle);
            deadline = Time.realtimeSinceStartup + 30f;
            while (current() && !RebirthCamperSavedVehicle.Verify(vehicle,expected,current))
            { if (Time.realtimeSinceStartup >= deadline) yield break; yield return null; }
            if (!current()) yield break;
            RebirthCamperStoragePackedTransfer.Applied(before,recovered);
            journal.Advance(receipt.Operation,RebirthCamperTransferJournal.Phase.Uncertain,RebirthCamperTransferJournal.Phase.Applied);
            runtime.SetTransferPending(false);
            runtime.Persist(true,12);
        }
        finally
        {
            observing.Remove(receipt.Operation);
            // Even a lost lock/disconnect retains the durable receipt. Reopening
            // queries it and resumes publication without touching inventory again.
            var latest = journal.Read(receipt.Operation);
            if (current())
            {
                // CommitSent clients suppress duplicate nonterminal phase replies.
                // A publication observer that has stopped must explicitly tell the
                // view to release its interaction lease and reopen for recovery;
                // echoing Prepared/Uncertain here strands Waiting indefinitely.
                if(latest!=null && (latest.State==RebirthCamperTransferJournal.Phase.Applied
                    || latest.State==RebirthCamperTransferJournal.Phase.Rejected))
                    Reply(playerId,vehicle.entityId,request,latest);
                else
                    Reply(playerId,vehicle.entityId,request,receipt.Operation,receipt.Vehicle,
                        Error,Array.Empty<byte>(),Array.Empty<byte>());
            }
        }
    }

    private static IEnumerator ObservePublication(IEnumerator transfer)
    {
        // Listener/save exceptions keep the existing receipt; they must not
        // escape as repeated native networking or Unity coroutine errors.
        while(true)
        {
            bool more=false,failed=false;object next=null;
            try { more=transfer.MoveNext();if(more)next=transfer.Current; }
            catch(Exception error) { failed=true;Log.Warning("[CamperTransfer] Remote publication retained: "+error.Message); }
            if(failed || !more)yield break;
            yield return next;
        }
    }

    private static void AssertStationsIdle(RebirthCamperModuleRuntime runtime)
    {
        for (int slot=0;slot<RebirthCamperCraftingStation.ModuleSlots;slot++)
            if (RebirthCamperStationWindowSession.IsInUse(runtime.Stations.GetSlot(slot)?.State)||
                RebirthCamperStationGridCustody.IsHeld(runtime.Stations.GetSlot(slot)?.State))
                throw new InvalidOperationException("A station window owns the installation.");
    }
    private static void ApplyStationGrids(RebirthCamperModuleRuntime runtime,RebirthCamperTransferSnapshot before,
        RebirthCamperTransferSnapshot after)
    {
        // Preserve live paid-binding/watch identities. Client grid preparation
        // never changes installations, queue, completion or timing state.
        for(int slot=0;slot<RebirthCamperCraftingStation.ModuleSlots;slot++)
        {
            var old=before.Stations.GetSlot(slot);var next=after.Stations.GetSlot(slot);
            if(old==null && next==null)continue;
            if(old==null || next==null || old.Id!=next.Id)throw new InvalidOperationException("Grid receipt changed an installation.");
            var live=runtime.Stations.Find(old.Id)?.State;
            if(live==null)throw new InvalidOperationException("Grid installation disappeared.");
            if(Equal(RebirthCamperStationSnapshot.Capture(old.Id,old.State),RebirthCamperStationSnapshot.Capture(next.Id,next.State)))continue;
            live.Fuel=CloneGrid(next.State.Fuel);live.Input=CloneGrid(next.State.Input);
            live.Tools=CloneGrid(next.State.Tools);live.Output=CloneGrid(next.State.Output);
            live.RestoreRevision(next.State.Revision);
        }
    }
    private static ItemStack[] CloneGrid(ItemStack[] grid)
    {var result=new ItemStack[grid.Length];for(int i=0;i<grid.Length;i++)result[i]=grid[i].Clone();return result;}
    private static bool Equal(byte[] a,byte[] b)
    {if(a.Length!=b.Length)return false;for(int i=0;i<a.Length;i++)if(a[i]!=b[i])return false;return true;}
    internal static bool CanTakePendingLock(EntityVehicle vehicle,int playerId,ushort channel)
    {
        if (!IsRegistered || vehicle?.world == null || vehicle.world.IsRemote() || !RebirthCamperRadial.IsCamper(vehicle)) return true;
        var runtime=RebirthCamperModuleRuntime.For(vehicle);
        if(!runtime.TransferPending)return true;
        if(channel!=0)return false; // pickup cannot pack unresolved custody
        var player=vehicle.world.GetEntity(playerId) as EntityPlayer;
        string creation=player==null?null:creationProvider(player);
        if(string.IsNullOrWhiteSpace(creation))return false;
        var journal=new RebirthCamperTransferJournal(Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH","CamperTransfers"));
        var pending=journal.FindPending(runtime.Identity);
        if(player is EntityPlayerLocal && ReferenceEquals(vehicle.world.GetPrimaryPlayer(),player))
            return pending!=null && pending.OwnerCreation==creation;
        var owner=SingletonMonoBehaviour<ConnectionManager>.Instance.Clients.ForEntityId(playerId)?.InternalId;
        if(owner==null)return false;
        return pending!=null && pending.OwnerCreation=="remote:"+owner.CombinedString+":"+creation;
    }
    internal static void AssertFrame(byte[] bytes)
    { if (bytes == null || bytes.Length > MaximumFrame) throw new InvalidDataException("Camper network frame exceeds the supported bound."); }
    private static void Reply(int player,int vehicle,Guid request,RebirthCamperTransferJournal.Record receipt)
        => Reply(player,vehicle,request,receipt.Operation,receipt.Vehicle,(byte)receipt.State,receipt.Before,receipt.After);
    private static void Reply(int player,int vehicle,Guid request,Guid operation,Guid identity,byte state,byte[] before,byte[] after)
    {
        AssertFrame(before); AssertFrame(after);
        SingletonMonoBehaviour<ConnectionManager>.Instance.SendPackage(
            NetPackageManager.GetPackage<NetPackageRebirthCamperManagementResult>().Setup(vehicle,request,operation,identity,state,before,after),
            _attachedToEntityId:player);
    }
}

[Preserve]
public sealed class NetPackageRebirthCamperManagement : NetPackage
{
    private int player,vehicle,slot; private Guid request,operation; private byte action; private bool crafting;
    private long storage,stations; private byte[] offered;private Guid installation;private RebirthCamperRemoteStationGrid grid;
    private RebirthCamperRemoteStationGesture gesture;
    public override NetPackageDirection PackageDirection => NetPackageDirection.ToServer;
    public NetPackageRebirthCamperManagement Setup(int p,int v,Guid r,Guid op,byte a,bool c,int s,long sr,long tr,byte[] item,
        Guid station=default(Guid),RebirthCamperRemoteStationGrid kind=RebirthCamperRemoteStationGrid.Fuel,
        RebirthCamperRemoteStationGesture input=RebirthCamperRemoteStationGesture.Swap)
    { player=p;vehicle=v;request=r;operation=op;action=a;crafting=c;slot=s;storage=sr;stations=tr;offered=item;installation=station;grid=kind;gesture=input;return this; }
    public override void read(PooledBinaryReader r)
    { player=r.ReadInt32();vehicle=r.ReadInt32();request=new Guid(r.ReadBytes(16));operation=new Guid(r.ReadBytes(16));
      action=r.ReadByte();crafting=r.ReadBoolean();slot=r.ReadInt32();storage=r.ReadInt64();stations=r.ReadInt64();offered=CamperFrame.Read(r);
      installation=new Guid(r.ReadBytes(16));grid=(RebirthCamperRemoteStationGrid)r.ReadByte();gesture=(RebirthCamperRemoteStationGesture)r.ReadByte(); }
    public override void write(PooledBinaryWriter w)
    { base.write(w);w.Write(player);w.Write(vehicle);w.Write(request.ToByteArray());w.Write(operation.ToByteArray());
      w.Write(action);w.Write(crafting);w.Write(slot);w.Write(storage);w.Write(stations);CamperFrame.Write(w,offered);
      w.Write(installation.ToByteArray());w.Write((byte)grid);w.Write((byte)gesture); }
    public override void ProcessPackage(World world,GameManager callbacks)
    {
        if (world==null || world.IsRemote() || !ValidEntityIdForSender(player)) return;
        if (action==RebirthCamperRemoteTransport.Prepare || action==RebirthCamperRemoteTransport.GridPrepare || action==RebirthCamperRemoteTransport.QuickPrepare)
            RebirthCamperRemoteTransport.PrepareSlot(world,Sender,player,vehicle,request,operation,crafting,slot,storage,stations,offered,
                action==RebirthCamperRemoteTransport.GridPrepare?installation:Guid.Empty,grid,gesture,action==RebirthCamperRemoteTransport.QuickPrepare);
        else RebirthCamperRemoteTransport.Handle(world,Sender,player,vehicle,request,operation,action,crafting,storage,stations,offered);
    }
    public int GetLength() => 0;
}

[Preserve]
public sealed class NetPackageRebirthCamperManagementResult : NetPackage
{
    private int vehicle;private Guid request,operation,identity;private byte state;private byte[] before,after;
    public override NetPackageDirection PackageDirection => NetPackageDirection.ToClient;
    public NetPackageRebirthCamperManagementResult Setup(int v,Guid r,Guid op,Guid id,byte s,byte[] b,byte[] a)
    {vehicle=v;request=r;operation=op;identity=id;state=s;before=b;after=a;return this;}
    public override void read(PooledBinaryReader r)
    {vehicle=r.ReadInt32();request=new Guid(r.ReadBytes(16));operation=new Guid(r.ReadBytes(16));identity=new Guid(r.ReadBytes(16));
      state=r.ReadByte();before=CamperFrame.Read(r);after=CamperFrame.Read(r);}
    public override void write(PooledBinaryWriter w)
    {base.write(w);w.Write(vehicle);w.Write(request.ToByteArray());w.Write(operation.ToByteArray());w.Write(identity.ToByteArray());
      w.Write(state);CamperFrame.Write(w,before);CamperFrame.Write(w,after);}
    public override void ProcessPackage(World world,GameManager callbacks)
    {if(world!=null && world.IsRemote())RebirthCamperRemoteManagement.Receive(world,vehicle,request,operation,identity,state,before,after);}
    public int GetLength()=>0;
}

internal static class CamperFrame
{
    internal static byte[] Read(BinaryReader reader)
    {
        int length=reader.ReadInt32();
        if(length<0 || length>RebirthCamperRemoteTransport.MaximumFrame)throw new InvalidDataException("Invalid camper frame size.");
        byte[] bytes=reader.ReadBytes(length);
        if(bytes.Length!=length)throw new EndOfStreamException("Incomplete camper frame.");
        return bytes;
    }
    internal static void Write(BinaryWriter writer,byte[] bytes)
    {RebirthCamperRemoteTransport.AssertFrame(bytes);writer.Write(bytes.Length);writer.Write(bytes);}
}

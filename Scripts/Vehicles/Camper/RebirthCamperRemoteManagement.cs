using System;
using System.Collections.Generic;
using System.IO;

// Client presentation holds detached server manifests. Only the exact native
// lock callback and matching response can publish a cursor candidate.
public static class RebirthCamperRemoteManagement
{
    private sealed class Session
    {
        internal EntityVehicle Vehicle; internal EntityPlayerLocal Player;
        internal XUiC_RebirthCamperModules View; internal XUiC_DragAndDropWindow Cursor;
        internal Func<bool> Current; internal Action Release;
        internal RebirthCamperTransferSnapshot Snapshot;
        internal Guid Request,Operation; internal bool Crafting,Waiting,Published,Closed,CommitSent;
        internal Guid Installation; internal RebirthCamperRemoteStationGrid Grid;
        internal RebirthCamperRemoteStationGesture Gesture;
        internal Action<RebirthCamperTransferSnapshot> StationUpdated;
        internal Action StationRecovery;
        internal int RecoverySlot=-1;
        internal Guid OverviewRequest;internal int? ActiveQueues;internal float NextOverview;internal bool OverviewPending,OverviewWarned;
    }
    private static readonly Dictionary<Guid,Session> sessions = new Dictionary<Guid,Session>();

    // The native workstation callback supplies only its exact cell and held
    // cursor. Its detached presentation never writes a whole station manifest.
    internal static bool BeginStationGrid(EntityVehicle vehicle,EntityPlayerLocal player,
        XUiC_DragAndDropWindow cursor,RebirthCamperTransferSnapshot presentation,Guid installation,RebirthCamperRemoteStationGrid grid,int slot,
        RebirthCamperRemoteStationGesture gesture,ItemStack expected,Func<bool> current,
        Action<RebirthCamperTransferSnapshot> updated,Action recovery,out Action close)
    {
        close=null;
        if(!ThreadManager.IsMainThread() || vehicle?.world==null || !vehicle.world.IsRemote()
            || !RebirthCamperRemoteTransport.IsRegistered || player==null || cursor==null
            || presentation==null || installation==Guid.Empty || expected==null || current==null || !current() || updated==null || recovery==null
            || sessions.Count>=32)return false;
        foreach(var pending in sessions.Values)
            if(ReferenceEquals(pending.Player,player) && pending.Waiting && !pending.Closed)return false;
        var request=Guid.NewGuid();
        var session=new Session {Vehicle=vehicle,Player=player,Cursor=cursor,Current=current,
            Release=()=>{},Request=request,Operation=request,Waiting=true,Crafting=true,
            Snapshot=presentation,Installation=installation,Grid=grid,Gesture=gesture,StationUpdated=updated,StationRecovery=recovery};
        sessions.Add(request,session);close=()=>Close(session);
        try {Send(session,RebirthCamperRemoteTransport.GridPrepare,slot,
            RebirthCamperInventorySnapshot.Capture(new[]{cursor.CurrentStack,expected}));return true;}
        catch {sessions.Remove(request);session.Closed=true;throw;}
    }

    private static void Refresh(Session session)
    {
        if(session.Installation==Guid.Empty)
        {
            session.ActiveQueues=null;session.OverviewRequest=Guid.Empty;session.OverviewPending=false;
            session.NextOverview=UnityEngine.Time.realtimeSinceStartup+2.5f;session.View.RefreshModules();return;
        }
        session.StationUpdated(session.Snapshot);
        if(!session.Waiting){session.Closed=true;sessions.Remove(session.Request);}
    }

    public static void Open(EntityVehicle vehicle,EntityPlayerLocal player,bool crafting)
    {
        if (!ThreadManager.IsMainThread() || vehicle?.world == null || !vehicle.world.IsRemote()
            || !RebirthCamperRemoteTransport.IsRegistered || !RebirthCamperModuleInteraction.Current(vehicle,player)
            || sessions.Count >= 32) throw new InvalidOperationException("Current remote camper management session required.");
        var ui = LocalPlayerUI.GetUIForPlayer(player);
        string name = crafting ? RebirthCamperRadial.Stations : RebirthCamperRadial.Storage;
        GUIWindow window;
        if (ui == null || ui.windowManager.IsWindowOpen(name) || !ui.windowManager.TryGetWindow(name,out window)
            || !(window is XUiWindowGroup)) throw new InvalidOperationException("Management window unavailable.");
        var session = new Session { Vehicle=vehicle,Player=player,Crafting=crafting,
            View=((XUiWindowGroup)window).Controller.GetChildByType<XUiC_RebirthCamperModules>(),
            Cursor=ui.xui.GetChildByType<XUiC_DragAndDropWindow>(),
            Current=RebirthCamperModuleInteraction.CaptureCurrent(vehicle,player),
            Release=RebirthCamperModuleInteraction.CaptureRelease(vehicle,player),Request=Guid.NewGuid(),Waiting=true };
        if (session.View == null || session.Cursor == null) throw new InvalidOperationException("Management controllers missing.");
        sessions.Add(session.Request,session);
        try
        {
            session.View.Bind(Localization.Get(crafting ? "xuiRebirthCamperStationManagement" : "xuiRebirthCamperStorageManagement"),
                () => !session.Closed && session.Current() && !player.IsDead()
                    && ReferenceEquals(GameManager.Instance?.World,vehicle.world)
                    && ReferenceEquals(vehicle.world.GetEntity(vehicle.entityId),vehicle)
                    && (vehicle.position-player.position).sqrMagnitude<=64f,
                slot => session.Snapshot == null ? ItemStack.Empty : crafting
                    ? session.Snapshot.Stations.GetSlot(slot)?.InstalledItem : session.Snapshot.Storage.GetSlot(slot),
                stack => !session.Waiting && RebirthCamperManagementAccess.Accepts(stack,crafting),
                (slot,offered) => RequestTransfer(session,slot,offered), () => Close(session), crafting,
                (slot,backpackSlot)=>RequestTransfer(session,slot,player.bag.ItemGrid.items[backpackSlot].Clone(),backpackSlot),
                ()=>ReadActiveQueues(session));
            session.View.BoundVehicle=vehicle;
            ui.windowManager.Open(window,true,false);
            var held=session.Cursor.CurrentStack;
            Send(session,RebirthCamperRemoteTransport.Snapshot,-1,
                held?.itemValue?.Metadata?.ContainsKey(RebirthCamperStationRemovalContents.ContentsKey)==true
                    ?RebirthCamperInventorySnapshot.Capture(new[]{held}):Array.Empty<byte>());
        }
        catch { Close(session); throw; }
    }

    private static bool RequestTransfer(Session session,int slot,ItemStack offered,int backpackSlot=-1)
    {
        if (session.Closed || session.Waiting || session.Snapshot == null || !session.Current()
            || !(backpackSlot<0?Same(session.Cursor.CurrentStack,offered)
                :session.Cursor.IsEmpty()&&backpackSlot<session.Player.bag.ItemGrid.items.Length
                    &&Same(session.Player.bag.ItemGrid.items[backpackSlot],offered))) return false;
        sessions.Remove(session.Request);
        session.Request=Guid.NewGuid(); session.Operation=session.Request;
        session.Waiting=true;session.Published=false;session.CommitSent=false;
        sessions.Add(session.Request,session);
        if(offered.itemValue?.Metadata?.ContainsKey(RebirthCamperStationRemovalContents.ContentsKey)==true)
        {
            if(backpackSlot>=0){session.Waiting=false;return false;}
            session.Operation=Guid.Empty;session.RecoverySlot=slot;
            Send(session,RebirthCamperRemoteTransport.Snapshot,-1,RebirthCamperInventorySnapshot.Capture(new[]{offered}));
            return false;
        }
        Send(session,backpackSlot<0?RebirthCamperRemoteTransport.Prepare:RebirthCamperRemoteTransport.QuickPrepare,slot,
            backpackSlot<0?RebirthCamperInventorySnapshot.Capture(new[]{offered}):RebirthCamperQuickTransferFrame.Capture(backpackSlot,offered));
        return false; // sending a request does not debit the native cursor
    }

    internal static void Receive(World world,int vehicle,Guid request,Guid operation,Guid identity,byte state,byte[] before,byte[] after)
    {
        if(state==RebirthCamperRemoteTransport.QueueOverviewView)
        {ReceiveQueueOverview(world,vehicle,request,operation,identity,before,after);return;}
        if (!ThreadManager.IsMainThread() || !sessions.TryGetValue(request,out var session)
            || session.Closed || !session.Waiting || !session.Current() || !ReferenceEquals(session.Vehicle.world,world)
            || session.Vehicle.entityId != vehicle) return;
        try
        {
            if (state == RebirthCamperRemoteTransport.Error)
            {
                GameManager.ShowTooltip(session.Player,Localization.Get("xuiRebirthCamperTransferRecover"));
                RebirthCamperTrace.Write("remote.error",session.Vehicle,"operation="+session.Operation.ToString("N"));
                // An error is not permission to clear a durable receipt. Close
                // management's native lease so reopening can query recovery,
                // rather than leaving the view in Waiting forever.
                if(session.Installation==Guid.Empty)
                {
                    Close(session);
                    session.Player.PlayerUI.windowManager.Close((GUIWindow)session.View.WindowGroup);
                }
                else
                {
                    // Error conveys no terminal custody decision. Retire this
                    // request without cancelling even an unpublished receipt,
                    // then close the exact enclosing native station presentation.
                    Close(session,false);
                    session.StationRecovery();
                }
                return;
            }
            if (identity == Guid.Empty) throw new InvalidOperationException("Server camper identity missing.");
            if(state==RebirthCamperRemoteTransport.RemovalContents)
            {
                if(operation==Guid.Empty)throw new InvalidOperationException("Applied removal receipt required.");
                var priorRemoval=RebirthCamperTransferSnapshot.Restore(identity,before);
                var afterRemoval=RebirthCamperTransferSnapshot.Restore(identity,after);
                RebirthCamperStationContentsDelivery.Confirm(session.Player,session.Vehicle,
                    session.Cursor.xui,priorRemoval,afterRemoval);
                PublishOwner(session.Player);
                // Request the current manifest separately; the removal receipt
                // may predate later module edits and must never become the view.
                sessions.Remove(session.Request);session.Request=Guid.NewGuid();session.Operation=Guid.Empty;
                sessions.Add(session.Request,session);
                Send(session,RebirthCamperRemoteTransport.Snapshot,-1,Array.Empty<byte>());
                return;
            }
            if (state == RebirthCamperRemoteTransport.View)
            {
                if (operation != Guid.Empty) throw new InvalidOperationException("Unexpected snapshot receipt.");
                session.Snapshot=RebirthCamperTransferSnapshot.Restore(identity,before);
                session.Operation=Guid.Empty;session.Waiting=false;
                Refresh(session);
                int recoverySlot=session.RecoverySlot;session.RecoverySlot=-1;
                if(recoverySlot>=0&&session.Cursor.CurrentStack?.itemValue?.Metadata?.ContainsKey(RebirthCamperStationRemovalContents.ContentsKey)!=true)
                    RequestTransfer(session,recoverySlot,session.Cursor.CurrentStack);
                return;
            }
            if (operation == Guid.Empty || session.Operation != Guid.Empty && session.Operation != operation)
                throw new InvalidOperationException("Server receipt does not match the requested operation.");
            session.Operation=operation;
            var prior=RebirthCamperTransferSnapshot.Restore(identity,before);
            var next=RebirthCamperTransferSnapshot.Restore(identity,after);
            if(prior.BackpackSlot!=next.BackpackSlot)throw new InvalidOperationException("Server inventory owner changed.");
            if (state == (byte)RebirthCamperTransferJournal.Phase.Applied)
            {
                // Native vehicle flags12 update real cargo/metadata. The view
                // updates independently; it never copies its cargo to the entity.
                session.Snapshot=next;session.Waiting=false;session.Published=false;
                session.Player.Buffs.SetCustomVar(Receipt(operation),3f,true);
                session.Operation=Guid.Empty;Refresh(session);
                RebirthCamperPreferredRemoval.Confirm(session.Player,session.Vehicle,prior,next);
                PublishOwner(session.Player);return;
            }
            if (state == (byte)RebirthCamperTransferJournal.Phase.Rejected)
            {
                // Grid sessions use StationUpdated and have no module view.
                session.View?.CancelPreferredRemoval();
                session.Snapshot=prior;session.Waiting=false;session.Operation=Guid.Empty;
                GameManager.ShowTooltip(session.Player,Localization.Get("xuiRebirthCamperModuleTransferRefused"));
                Refresh(session);return;
            }
            if (state != (byte)RebirthCamperTransferJournal.Phase.Prepared && state != (byte)RebirthCamperTransferJournal.Phase.Uncertain)
                throw new InvalidOperationException("Unknown server transfer phase.");
            if (session.CommitSent) return; // duplicate reply cannot flood commits
            if (state == (byte)RebirthCamperTransferJournal.Phase.Uncertain)
            {
                // Server has durably acknowledged this owner's cursor. Resume
                // vehicle publication only, regardless of later inventory moves.
                session.Published=true;session.CommitSent=true;
                Send(session,RebirthCamperRemoteTransport.Commit,-1,Array.Empty<byte>());return;
            }
            string receipt=Receipt(operation);
            float marker=session.Player.Buffs.GetCustomVar(receipt);
            // Durable native receipts, not reply arrival order, determine whether
            // inventory publication has happened. Reconnect/replay never debits twice.
            if(prior.BackpackSlot>=session.Player.bag.ItemGrid.items.Length)throw new InvalidOperationException("Server backpack cell unavailable.");
            var ownerStack=prior.BackpackSlot<0?session.Cursor.CurrentStack:session.Player.bag.ItemGrid.items[prior.BackpackSlot];
            var decision=RebirthCamperRemoteCursorPolicy.Decide(false,marker,
                Same(ownerStack,prior.Cursor),Same(ownerStack,next.Cursor));
            if(decision==RebirthCamperRemoteCursorPolicy.Action.Cancel)
            {
                Send(session,RebirthCamperRemoteTransport.Cancel,-1,Array.Empty<byte>());return;
            }
            if(decision==RebirthCamperRemoteCursorPolicy.Action.Retain)
                throw new InvalidOperationException("Partial cursor publication must be reconciled.");
            if(decision==RebirthCamperRemoteCursorPolicy.Action.PublishCursor)
            {
                session.Player.Buffs.SetCustomVar(receipt,2f,true);
                if(next.BackpackSlot<0)session.Cursor.SetCurrentStack(next.Cursor.Clone(),false);
                else
                {
                    if(!session.Cursor.IsEmpty())throw new InvalidOperationException("Quick transfer cursor changed.");
                    session.Player.bag.SetSlot(next.BackpackSlot,next.Cursor.Clone());
                    session.View.GetChildByType<XUiC_RebirthBackpackSectionBackpack>()?.SetStacks(session.Player.bag.ItemGrid.items);
                }
            }
            if(decision!=RebirthCamperRemoteCursorPolicy.Action.Retain)
            {
                session.Published=true;
                session.Player.Buffs.SetCustomVar(receipt,1f,true);
                // Publish the complete normal native player state. Receipt1
                // remains valid if the resulting cursor was legitimately moved.
                PublishOwner(session.Player);
                session.CommitSent=true;
                Send(session,RebirthCamperRemoteTransport.Commit,-1,Array.Empty<byte>());
            }
        }
        catch (Exception error)
        {
            GameManager.ShowTooltip(session.Player,Localization.Get("xuiRebirthCamperTransferRecover"));
            Log.Warning("[CamperTransfer] Remote cursor retained for reconciliation: "+error.Message);
        }
    }

    private static void Close(Session session,bool cancelPrepared=true)
    {
        if (session.Closed) return;
        // Cancellation is permitted only before native owner publication. Once
        // published, the journal remains the reconnect/recovery authority.
        try
        {
            if (cancelPrepared && session.Operation!=Guid.Empty && !session.Published && session.Current()
                && session.Player.Buffs.GetCustomVar(Receipt(session.Operation))==0f)
                Send(session,RebirthCamperRemoteTransport.Cancel,-1,Array.Empty<byte>());
        }
        catch(Exception error)
        {Log.Warning("[CamperTransfer] Cancellation transport unavailable; receipt retained: "+error.Message);}
        finally
        {session.Closed=true;sessions.Remove(session.Request);session.Release();}
    }
    private static void Send(Session session,byte action,int slot,byte[] offered)
    {
        SingletonMonoBehaviour<ConnectionManager>.Instance.SendToServer(
            NetPackageManager.GetPackage<NetPackageRebirthCamperManagement>().Setup(session.Player.entityId,session.Vehicle.entityId,
                session.Request,session.Operation,action,session.Crafting,slot,session.Snapshot?.Storage.Revision??0,
                session.Snapshot?.Stations.Revision??0,offered,session.Installation,session.Grid,session.Gesture),false);
    }
    private static int CountActiveQueues(RebirthCamperStationInstallations stations)
    {
        // This is the existing validated server snapshot, not a client-created
        // runtime or an inferred count from installed station icons.
        int count=0;for(int i=0;i<RebirthCamperCraftingStation.ModuleSlots;i++)
            if(stations.GetSlot(i)?.State.hasRecipeInQueue()==true)count++;
        return count;
    }
    private static int? ReadActiveQueues(Session session)
    {
        if(!session.Crafting||session.Snapshot==null||session.Closed||!session.Current())return null;
        float now=UnityEngine.Time.realtimeSinceStartup;
        if(!session.Waiting&&now>=session.NextOverview)
        {
            session.NextOverview=now+(session.OverviewPending?10f:2.5f);session.OverviewPending=true;session.OverviewRequest=Guid.NewGuid();
            try
            {
                SingletonMonoBehaviour<ConnectionManager>.Instance.SendToServer(NetPackageManager.GetPackage<NetPackageRebirthCamperManagement>()
                    .Setup(session.Player.entityId,session.Vehicle.entityId,session.OverviewRequest,Guid.Empty,
                        RebirthCamperRemoteTransport.QueueOverview,true,-1,session.Snapshot.Storage.Revision,session.Snapshot.Stations.Revision,Array.Empty<byte>()),false);
            }
            catch(Exception error)
            {session.NextOverview=now+10f;if(!session.OverviewWarned){session.OverviewWarned=true;Log.Warning("[CamperOverview] Queue status transport unavailable: "+error.GetType().Name);}}
        }
        return session.ActiveQueues??CountActiveQueues(session.Snapshot.Stations);
    }
    private static void ReceiveQueueOverview(World world,int vehicle,Guid request,Guid operation,Guid identity,byte[] frame,byte[] after)
    {
        if(!ThreadManager.IsMainThread()||world?.IsRemote()!=true||!ReferenceEquals(GameManager.Instance?.World,world)
            ||request==Guid.Empty||operation!=Guid.Empty||identity==Guid.Empty||frame==null||frame.Length!=13||after==null||after.Length!=0)return;
        foreach(var session in sessions.Values)
        {
            if(session.Closed||session.Waiting||!session.Crafting||session.OverviewRequest!=request||!session.Current()
                ||!ReferenceEquals(session.Vehicle.world,world)||session.Vehicle.entityId!=vehicle
                ||session.Snapshot?.Stations.VehicleIdentity!=identity)continue;
            using(var stream=new MemoryStream(frame,false))using(var reader=new BinaryReader(stream))
            {
                if(reader.ReadByte()!=1)return;long revision=reader.ReadInt64();int count=reader.ReadInt32();
                if(revision<session.Snapshot.Stations.Revision||count<0||count>RebirthCamperCraftingStation.ModuleSlots)return;
                session.ActiveQueues=count;session.OverviewPending=false;session.NextOverview=UnityEngine.Time.realtimeSinceStartup+2.5f;return;
            }
        }
    }
    private static void PublishOwner(EntityPlayerLocal player)
        => SingletonMonoBehaviour<ConnectionManager>.Instance.SendToServer(NetPackageManager.GetPackage<NetPackagePlayerData>().Setup(player),false);
    private static string Receipt(Guid operation)=>"rbCamper_"+operation.ToString("N");
    private static bool Same(ItemStack left,ItemStack right)
    {
        if(left==null || right==null)return false;
        byte[] a=RebirthCamperInventorySnapshot.Capture(new[]{left}),b=RebirthCamperInventorySnapshot.Capture(new[]{right});
        if(a.Length!=b.Length)return false;for(int i=0;i<a.Length;i++)if(a[i]!=b[i])return false;return true;
    }
}

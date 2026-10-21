using System;
using System.Collections.Generic;

// Native picker/workstation presentation. Only server snapshots replace this
// detached state; all cursor mutations use the existing prepared receipt path.
public static class RebirthCamperRemoteStationClient
{
    private sealed class Session
    {
        internal EntityVehicle Vehicle;internal EntityPlayerLocal Player;internal Guid Token,Installation;
        internal Func<bool> NativeCurrent,PhysicalCurrent,TransportCurrent;internal Action NativeRelease,GridClose;
        internal string Creation;internal int OriginalDeaths;
        internal RebirthCamperTransferSnapshot Snapshot;internal RebirthCamperStationState State;
        internal XUiC_RebirthCamperStationPicker Picker;internal GUIWindow PickerWindow;
        internal bool Closed,Opening,GridPending,CommandPending;
        internal RebirthCamperRemoteStationCommand PendingCommand;
        internal byte PendingAction;
        internal Guid PendingOperation;
        internal bool PendingBurning;
        internal bool CommandUncertain;
        internal float NextCommandStatus;
        internal Action SwitchClosed;
        internal bool SwitchClosePending;
        internal float NextSwitchClose;
        internal float NextOverviewQuery;
        internal bool OverviewQueryPending;
    }
    private static readonly Dictionary<int,Session> sessions=new Dictionary<int,Session>();
    public static bool IsReady {get;private set;}
    internal static Newtonsoft.Json.Linq.JObject CaptureCommandAudit(EntityPlayerLocal player)
    {
        var result=new Newtonsoft.Json.Linq.JObject{["sessionPresent"]=false};
        var cursor=player==null?null:LocalPlayerUI.GetUIForPlayer(player)?.CursorController;
        if(cursor!=null){var position=cursor.GetScreenPosition();result["cursor"]=new Newtonsoft.Json.Linq.JObject{["x"]=position.x,["y"]=position.y,["visible"]=GameManager.Instance?.bCursorVisible??false};}
        result["window"]=RebirthCamperStationWindowSession.CaptureCommandAudit(player);
        if(!ThreadManager.IsMainThread()||player==null||!sessions.TryGetValue(player.entityId,out var session)||!ReferenceEquals(session.Player,player))return result;
        result["sessionPresent"]=true;result["current"]=Current(session);result["closed"]=session.Closed;result["opening"]=session.Opening;
        result["token"]=session.Token.ToString("N");result["installation"]=session.Installation.ToString("N");result["creation"]=session.Creation;result["deaths"]=session.OriginalDeaths;
        result["vehicle"]=session.Snapshot?.Storage.VehicleIdentity.ToString("N");result["vehicleEntity"]=session.Vehicle?.entityId??-1;
        result["gridPending"]=session.GridPending;result["commandPending"]=session.CommandPending;result["uncertain"]=session.CommandUncertain;
        result["switchClosePending"]=session.SwitchClosePending;result["switchCallbackPending"]=session.SwitchClosed!=null;
        result["operation"]=session.PendingOperation.ToString("N");result["pendingAction"]=session.PendingAction;
        if(session.PendingCommand!=null)result["command"]=RebirthCamperRemoteStationCommandAudit.Describe(session.PendingCommand);
        return result;
    }
    internal static RebirthCamperStationInstallations InstalledForNavigation(EntityPlayerLocal player)
        => player!=null && sessions.TryGetValue(player.entityId,out var session) && Current(session)
            ? session.Snapshot?.Stations : null;
    internal static void RefreshOverview(EntityPlayerLocal player,XUiC_RebirthCamperStationPicker picker)
    {
        if(player==null||!sessions.TryGetValue(player.entityId,out var s)||!ReferenceEquals(s.Picker,picker)
            ||!picker.IsOverviewCurrent||!Current(s)||s.Opening||s.Installation!=Guid.Empty
            ||UnityEngine.Time.realtimeSinceStartup<s.NextOverviewQuery)return;
        s.NextOverviewQuery=UnityEngine.Time.realtimeSinceStartup+(s.OverviewQueryPending?10f:2.5f);
        s.OverviewQueryPending=true;
        Send(s,RebirthCamperRemoteStationTransport.QuerySession);
    }
    internal static void Install()
    {
        if(IsReady)return;
        ModEvents.GameUpdate.RegisterHandler(PollOriginalCommands);
        RebirthCamperRemoteStationTransport.ReceiveSnapshot=Receive;
        RebirthCamperRemoteStationTransport.ReceiveCommandSnapshot=ReceiveCommand;
        RebirthCamperRemoteStationTransport.ReceiveUpdate=Update;
        IsReady=true;
    }
    public static void OpenPicker(EntityVehicle vehicle,EntityPlayerLocal player)
    {
        if(!ThreadManager.IsMainThread() || !IsReady || !RebirthCamperRemoteStationTransport.IsRegistered
            || vehicle?.world==null || !vehicle.world.IsRemote() || player==null
            || !RebirthCamperModuleInteraction.Current(vehicle,player) || sessions.ContainsKey(player.entityId))
            throw new InvalidOperationException("Current remote station session required.");
        var ui=LocalPlayerUI.GetUIForPlayer(player);GUIWindow window;
        if(ui==null || ui.windowManager.IsWindowOpen(RebirthCamperStationAccess.PickerWindow)
            || !ui.windowManager.TryGetWindow(RebirthCamperStationAccess.PickerWindow,out window) || !(window is XUiWindowGroup group))
            throw new InvalidOperationException("Native station picker unavailable.");
        var picker=group.Controller.GetChildByType<XUiC_RebirthCamperStationPicker>();
        if(picker==null)throw new InvalidOperationException("Native station picker controller missing.");
        var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;
        var channels=manager?.GetConnectionToServer();
        string creation=RebirthCamperCharacterAccess.CurrentCreation(player);
        if(manager==null||manager.IsServer||channels==null||channels.Length==0||channels[0]==null||channels[0].IsDisconnected()||string.IsNullOrEmpty(creation))
            throw new InvalidOperationException("Current native connection and character required.");
        var channel=channels[0];
        var session=new Session {Vehicle=vehicle,Player=player,Token=Guid.NewGuid(),Picker=picker,PickerWindow=window,
            Creation=creation,OriginalDeaths=player.Died,
            TransportCurrent=()=>{var live=manager.GetConnectionToServer();return ReferenceEquals(manager,SingletonMonoBehaviour<ConnectionManager>.Instance)&&!channel.IsDisconnected()&&live!=null&&live.Length>0&&ReferenceEquals(live[0],channel);},
            NativeCurrent=RebirthCamperModuleInteraction.CaptureCurrent(vehicle,player),
            PhysicalCurrent=RebirthCamperStationSelectionIntent.CapturePhysical(player,vehicle),
            NativeRelease=RebirthCamperModuleInteraction.CaptureRelease(vehicle,player)};
        sessions.Add(player.entityId,session);
        picker.Bind(new Guid[RebirthCamperCraftingStation.ModuleSlots],new string[RebirthCamperCraftingStation.ModuleSlots],()=>Current(session),id=>Select(session,id),()=>Close(session),true);
        try {RebirthCamperStationPointerTransition.BeforeOpen(player,vehicle,Guid.Empty);ui.windowManager.Open(window,true,false);RebirthCamperStationPointerTransition.Complete(player,vehicle,Guid.Empty,window,()=>Current(session));Send(session,RebirthCamperRemoteStationTransport.QuerySession);}
        catch {Close(session);throw;}
    }
    private static bool Current(Session s)=>ThreadManager.IsMainThread()&&s!=null&&!s.Closed&&s.Player?.world!=null&&s.Vehicle?.world!=null
        && ReferenceEquals(GameManager.Instance?.World,s.Vehicle.world) && ReferenceEquals(s.Vehicle.world,s.Player.world)
        && ReferenceEquals(s.Player.world.GetEntity(s.Player.entityId),s.Player)
        && ReferenceEquals(s.Vehicle.world.GetEntity(s.Vehicle.entityId),s.Vehicle)
        &&s.TransportCurrent?.Invoke()==true&&s.Player.Died==s.OriginalDeaths&&s.Creation==RebirthCamperCharacterAccess.CurrentCreation(s.Player)
        &&(s.PhysicalCurrent==null||s.PhysicalCurrent())&&s.NativeCurrent()&&!s.Player.IsDead()
        && (s.Vehicle.position-s.Player.position).sqrMagnitude<=64f;
    private static Session Find(EntityPlayerLocal player,RebirthCamperStationState state)
        =>player!=null && sessions.TryGetValue(player.entityId,out var s) && ReferenceEquals(s.Player,player)
            && ReferenceEquals(s.State,state) && Current(s)?s:null;
    private static void Select(Session s,Guid installation)
    {
        if(!Current(s)||s.Opening||s.Snapshot?.Stations.Find(installation)==null)return;
        RebirthCamperStationPointerTransition.Begin(s.Player,s.Vehicle,installation);
        var previous=s.Installation;
        s.Installation=installation;s.Opening=true;
        try{Send(s,RebirthCamperRemoteStationTransport.OpenSession);}
        catch{s.Installation=previous;s.Opening=false;RebirthCamperStationPointerTransition.Cancel(s.Player,s.Vehicle);throw;}
    }
    private static void Receive(EntityVehicle vehicle,Guid token,RebirthCamperTransferSnapshot snapshot,byte action,bool accepted)
    {
        // Legacy replies have no original-command identity and cannot settle a
        // command, including a delayed response from a preceding operation.
        if(action==RebirthCamperRemoteStationTransport.StationCommand||action==RebirthCamperRemoteStationTransport.FuelSwitch)return;
        ReceiveCore(vehicle,token,snapshot,action,accepted);
    }
    private static void ReceiveCommand(EntityVehicle vehicle,Guid token,RebirthCamperTransferSnapshot snapshot,byte action,Guid operation,bool accepted)
    {
        var player=vehicle?.world?.GetPrimaryPlayer();
        if(player==null||!sessions.TryGetValue(player.entityId,out var session)||session.Token!=token||!ReferenceEquals(session.Vehicle,vehicle)||!Current(session)||
            !session.CommandPending||session.PendingAction!=action||session.PendingOperation!=operation||operation==Guid.Empty)return;
        if(snapshot!=null&&snapshot.Storage.VehicleIdentity!=session.Snapshot?.Storage.VehicleIdentity)return;
        if(!accepted||snapshot==null)
        {
            // A handler exception may follow native effects. This is not a
            // durable never-admitted proof and must not permit a new command.
            session.CommandUncertain=true;
            GameManager.ShowTooltip(session.Player,Localization.Get("xuiRebirthCamperTransferRecover"));return;
        }
        if(RebirthCamperRemoteStationCommandStatus.HasNativeOriginal(session.PendingCommand))
        {
            // Handler admission is not saved terminal evidence. The separate
            // read-only query must resolve this original before UI retirement.
            session.CommandUncertain=true;return;
        }
        ReceiveCore(vehicle,token,snapshot,action,true);
    }
    internal static void ReceiveOriginalStatus(EntityVehicle vehicle,Guid token,Guid identity,Guid installation,Guid operation,Guid job,
        RebirthCamperRemoteStationCommandKind kind,RebirthCamperRemoteStationCommandStatus.State status,RebirthCamperTransferSnapshot snapshot)
    {
        var player=vehicle?.world?.GetPrimaryPlayer();
        if(!ThreadManager.IsMainThread()||player==null||!sessions.TryGetValue(player.entityId,out var s)||!Current(s)||
            !ReferenceEquals(s.Vehicle,vehicle)||s.Token!=token||s.Installation!=installation||s.Snapshot?.Storage.VehicleIdentity!=identity||
            !s.CommandPending||(s.PendingAction!=RebirthCamperRemoteStationTransport.StationCommand&&s.PendingAction!=RebirthCamperRemoteStationTransport.FuelSwitch)||s.PendingOperation!=operation||
            s.PendingCommand==null||s.PendingCommand.Operation!=operation||s.PendingCommand.Job!=job||s.PendingCommand.Kind!=kind)return;
        if(status!=RebirthCamperRemoteStationCommandStatus.State.Applied&&status!=RebirthCamperRemoteStationCommandStatus.State.Rejected)
        {s.CommandUncertain=true;return;}
        if(snapshot==null||snapshot.Storage.VehicleIdentity!=identity)return;
        if(!RebirthCamperRemoteCommandOutbox.TryMirrorTerminal(s.Player,new RebirthCamperStationAddress(identity,installation),s.PendingCommand,status))
        {s.CommandUncertain=true;return;}
        // Only this typed original status can classify a negative response as
        // no-effect. Generic command failure remains uncertain in ReceiveCommand.
        ReceiveCore(vehicle,token,snapshot,s.PendingAction,
            status==RebirthCamperRemoteStationCommandStatus.State.Applied);
    }
    private static void PollOriginalCommands(ref ModEvents.SGameUpdateData data)
    {
        if(!IsReady||!ThreadManager.IsMainThread()||sessions.Count==0)return;
        float now=UnityEngine.Time.realtimeSinceStartup;
        foreach(var s in new List<Session>(sessions.Values))
        {
            // Retire presentation even when no command is pending or the old
            // remote World has ended. Durable originals remain in the outbox.
            if(!Current(s))
            {
                try{Close(s);}
                catch(Exception error){Log.Warning("[CamperStation] Expired session release failed: "+error.Message);}
                continue;
            }
            if(GameManager.Instance?.World?.IsRemote()!=true)continue;
            if(!s.CommandPending||(s.PendingAction!=RebirthCamperRemoteStationTransport.StationCommand&&s.PendingAction!=RebirthCamperRemoteStationTransport.FuelSwitch)||
                !RebirthCamperRemoteStationCommandStatus.HasNativeOriginal(s.PendingCommand)||now<s.NextCommandStatus)continue;
            s.NextCommandStatus=now+2f;
            try
            {
                if(!Current(s))continue;
                if(RebirthCamperRemoteCommandOutbox.TryResolveNeverAttempted(s.Player,new RebirthCamperStationAddress(s.Snapshot.Storage.VehicleIdentity,s.Installation),s.PendingCommand))
                {
                    if(!Current(s))continue;
                    s.CommandPending=false;s.CommandUncertain=false;s.PendingCommand=null;
                    continue; // positive saved local no-send proof; no peer effect exists
                }
                // This packet has no path to Execute/Handle. A timeout never
                // causes a second native command or a fresh operation identity.
                SingletonMonoBehaviour<ConnectionManager>.Instance.SendToServer(
                    NetPackageManager.GetPackage<NetPackageRebirthCamperStationCommandStatusQuery>().Setup(s.Player.entityId,s.Vehicle.entityId,
                        s.Token,s.Snapshot.Storage.VehicleIdentity,s.Installation,s.PendingCommand,
                        RebirthCamperRemoteCommandOutbox.DeclaresAdmissionProtocol(s.Player,new RebirthCamperStationAddress(s.Snapshot.Storage.VehicleIdentity,s.Installation),s.PendingCommand)),false);
            }
            catch(Exception error){s.CommandUncertain=true;Log.Warning("[CamperStation] Original status query retained: "+error.Message);}
        }
    }
    private static void ReceiveCore(EntityVehicle vehicle,Guid token,RebirthCamperTransferSnapshot snapshot,byte action,bool accepted)
    {
        var player=vehicle.world.GetPrimaryPlayer();
        if(player==null || !sessions.TryGetValue(player.entityId,out var s) || s.Token!=token
            || !ReferenceEquals(s.Vehicle,vehicle) || !Current(s))return;
        if(action==RebirthCamperRemoteStationTransport.FuelSwitch&&(!s.CommandPending||s.PendingAction!=action))return;
        if(action==RebirthCamperRemoteStationTransport.CloseSession && s.SwitchClosed!=null)
        {
            s.SwitchClosePending=false;
            if(!accepted || snapshot==null)
            {
                // Server save custody may still be held after a command reply.
                // Retry this same token, keeping native channel0 throughout.
                s.NextSwitchClose=UnityEngine.Time.realtimeSinceStartup+.5f;
                return;
            }
            FinishSwitchClose(s);
            return;
        }
        if(!accepted||snapshot==null)
        {if(action==RebirthCamperRemoteStationTransport.StationCommand||action==RebirthCamperRemoteStationTransport.FuelSwitch){s.CommandPending=false;s.CommandUncertain=false;s.PendingCommand=null;}s.Opening=false;if(action==RebirthCamperRemoteStationTransport.OpenSession)RebirthCamperStationPointerTransition.Cancel(s.Player,s.Vehicle);GameManager.ShowTooltip(s.Player,Localization.Get("xuiRebirthCamperTransferRecover"));return;}
        if(action==RebirthCamperRemoteStationTransport.QuerySession
            &&(s.Opening||s.Installation!=Guid.Empty||!s.Picker.IsOverviewCurrent))return;
        if(action==RebirthCamperRemoteStationTransport.QuerySession
            &&!RebirthCamperOverviewSnapshotPolicy.IsFresh(s.Snapshot,snapshot))return;
        s.Snapshot=snapshot;
        if(action==RebirthCamperRemoteStationTransport.QuerySession)
        {
            s.OverviewQueryPending=false;
            s.NextOverviewQuery=UnityEngine.Time.realtimeSinceStartup+2.5f;
            var identities=new Guid[RebirthCamperCraftingStation.ModuleSlots];var names=new string[RebirthCamperCraftingStation.ModuleSlots];
            for(int slot=0;slot<RebirthCamperCraftingStation.ModuleSlots;slot++)
            {
                var station=snapshot.Stations.GetSlot(slot);if(station==null)continue;
                identities[slot]=station.Id;names[slot]=station.InstalledItem.itemValue.ItemClass.GetLocalizedItemName()+" ("+(slot+1)+")";
            }
            s.Picker.Bind(identities,names,()=>Current(s),id=>Select(s,id),()=>Close(s),inspection:id=>s.Snapshot?.Stations.Find(id));
            s.Picker.SelectInstallation(RebirthCamperStationSelectionIntent.Take(s.Player,vehicle));return;
        }
        var installed=snapshot.Stations.Find(s.Installation);
        if(installed==null){Close(s);return;}
        if(action==RebirthCamperRemoteStationTransport.OpenSession && s.Opening)
        {
            s.Opening=false;s.State=installed.State;
            if(!RebirthCamperRemoteCommandOutbox.TryReadPending(s.Player,new RebirthCamperStationAddress(snapshot.Storage.VehicleIdentity,s.Installation),out var original))
                throw new InvalidOperationException("Original command history requires reconciliation.");
            if(original!=null)
            {
                // Fresh native lease permits observation of the old metadata,
                // never a replay of its mutating command.
                s.CommandPending=true;s.CommandUncertain=true;s.PendingCommand=original;
                s.PendingAction=original.IsFuel?RebirthCamperRemoteStationTransport.FuelSwitch:RebirthCamperRemoteStationTransport.StationCommand;
                s.PendingBurning=original.Kind==RebirthCamperRemoteStationCommandKind.FuelOn;s.PendingOperation=original.Operation;s.NextCommandStatus=0f;
            }
            RebirthCamperStationPointerTransition.Trace("remote.overview.close.before","showing="+s.PickerWindow?.isShowing);
            s.Picker.RetainLeaseForSelection();LocalPlayerUI.GetUIForPlayer(s.Player).windowManager.Close(s.PickerWindow);
            RebirthCamperStationPointerTransition.Trace("remote.overview.close.after","showing="+s.PickerWindow?.isShowing);
            try {RebirthCamperStationWindowSession.OpenRemote(vehicle,s.Player,installed,
                new RebirthCamperStationAddress(snapshot.Storage.VehicleIdentity,s.Installation),()=>Current(s),()=>Close(s));}
            catch {Close(s);throw;}
        }
        else if(action==RebirthCamperRemoteStationTransport.StationCommand || action==RebirthCamperRemoteStationTransport.FuelSwitch)
        {s.CommandPending=false;s.CommandUncertain=false;s.PendingCommand=null;Project(s,installed.State);}
    }
    private static void Update(EntityVehicle vehicle,Guid token,Guid identity,ItemStack installed,RebirthCamperStationState state)
    {
        var player=vehicle.world.GetPrimaryPlayer();
        if(player==null||!sessions.TryGetValue(player.entityId,out var s)||s.Token!=token||!ReferenceEquals(s.Vehicle,vehicle)
            ||!Current(s)||s.State==null||s.Installation!=state.InstallationId||s.Snapshot.Storage.VehicleIdentity!=identity)return;
        if(!s.GridPending && state.Revision>=s.State.Revision)Project(s,state);
    }
    private static void Project(Session s,RebirthCamperStationState next)
    {
        if(s.State==null||next==null||!Current(s))return;
        var live=s.State;
        if(next.Revision<live.Revision)return;
        live.Fuel=next.Fuel;live.Input=next.Input;live.Tools=next.Tools;live.Output=next.Output;live.Queue=next.Queue;
        live.currentBurnTimeLeft=next.currentBurnTimeLeft;live.currentMeltTimesLeft=next.currentMeltTimesLeft;
        live.isBurning=next.isBurning;live.lastTickTime=next.lastTickTime;live.isBesideWater=next.isBesideWater;
        live.RestoreRevision(next.Revision);
        var window=RebirthCamperStationWindowSession.RemoteWindow(s.Player,live);
        if(window!=null)RebirthCamperStationQueueProjection.Refresh(window,live);
    }
    internal static bool Transfer(XUiC_ItemStack stack,RebirthCamperRemoteStationGesture gesture)
    {
        var window=stack?.GetParentByType<XUiC_WorkstationWindowGroup>();
        var state=window?.WorkstationData?.TileEntity as RebirthCamperStationState;
        if(state?.BoundVehicle?.world?.IsRemote()!=true)return true;
        var player=stack.xui?.playerUI?.entityPlayer;var s=Find(player,state);
        if(s==null||s.GridPending||s.CommandPending||stack.IsLocked)return false;
        RebirthCamperRemoteStationGrid grid;
        if(stack.GetParentByType<XUiC_WorkstationFuelGrid>()!=null)grid=RebirthCamperRemoteStationGrid.Fuel;
        else if(stack.GetParentByType<XUiC_WorkstationToolGrid>()!=null)grid=RebirthCamperRemoteStationGrid.Tools;
        else if(stack.GetParentByType<XUiC_WorkstationOutputGrid>()!=null)grid=RebirthCamperRemoteStationGrid.Output;
        else if(stack.GetParentByType<XUiC_WorkstationInputGrid>()!=null || stack.GetParentByType<XUiC_WorkstationMaterialInputGrid>()!=null)grid=RebirthCamperRemoteStationGrid.Input;
        else return false; // molten stock and unsupported native positions are presentation only
        var cursor=stack.xui.GetChildByType<XUiC_DragAndDropWindow>();
        if(cursor==null || (gesture==RebirthCamperRemoteStationGesture.Swap || gesture==RebirthCamperRemoteStationGesture.One)
            && !stack.CanSwap(cursor.CurrentStack))return false;
        s.GridPending=RebirthCamperRemoteManagement.BeginStationGrid(s.Vehicle,s.Player,cursor,s.Snapshot,s.Installation,grid,
            stack.SlotNumber,gesture,stack.ItemStack.Clone(),()=>Current(s),snapshot=>
            {s.GridPending=false;s.GridClose=null;s.Snapshot=snapshot;Project(s,snapshot.Stations.Find(s.Installation)?.State);},()=>
            {
                // A delayed grid error must never close a newer client session
                // or another installation presented by this same player.
                if(sessions.TryGetValue(s.Player.entityId,out var registered) && ReferenceEquals(registered,s)
                    && ReferenceEquals(s.State,state) && Current(s))
                    RebirthCamperStationWindowSession.CloseRemoteForRecovery(s.Player,state);
            },out s.GridClose);
        return false;
    }
    internal static bool Craft(EntityPlayerLocal player,RebirthCamperStationState state,Recipe recipe,int count,int tier)
    {
        var s=Find(player,state);if(s==null||s.GridPending||s.CommandPending||recipe==null||count<=0)return false;
        var kind=RebirthStationPlayerIngredients.UsesPlayerPayment(player,state)?
            RebirthCamperRemoteStationCommandKind.CraftPlayer:RebirthCamperRemoteStationCommandKind.Craft;
        if(kind==RebirthCamperRemoteStationCommandKind.CraftPlayer)
            SingletonMonoBehaviour<ConnectionManager>.Instance.SendToServer(NetPackageManager.GetPackage<NetPackagePlayerData>().Setup(player),false);
        var op=Guid.NewGuid();return Command(s,new RebirthCamperRemoteStationCommand(kind,op,op,recipe.GetName(),count,tier));
    }
    internal static bool Cancel(EntityPlayerLocal player,RebirthCamperStationState state,string jobId)
    {
        var s=Find(player,state);return Guid.TryParse(jobId,out var job) && s!=null && !s.GridPending && !s.CommandPending
            && Command(s,new RebirthCamperRemoteStationCommand(RebirthCamperRemoteStationCommandKind.Cancel,Guid.NewGuid(),job,"",0,0));
    }
    internal static bool RequestRecycler(EntityPlayerLocal player,RebirthCamperStationState state,
        int slot,int units,Guid operation,bool cancel,out string reason)
    {
        reason="xuiRebirthRecyclingAccessExpired";
        if(!ThreadManager.IsMainThread() || state?.BoundVehicle?.world?.IsRemote()!=true || operation==Guid.Empty)return false;
        var s=Find(player,state);
        if(s==null || s.Opening || s.SwitchClosed!=null)return false;
        if(s.GridPending || s.CommandPending){reason="xuiRebirthRecyclingBusy";return false;}
        if(!RebirthRecyclerRuntime.Supports(state))return false;
        RebirthCamperRemoteStationCommand request;
        if(cancel)
            request=new RebirthCamperRemoteStationCommand(RebirthCamperRemoteStationCommandKind.ScrapCancel,
                Guid.NewGuid(),operation,"",0,0);
        else
        {
            if(slot<0 || slot>byte.MaxValue || state.Input==null || slot>=state.Input.Length || units<=0 || units>ushort.MaxValue)return false;
            var input=state.Input[slot];
            if(input==null || input.IsEmpty() || input.count<units)return false;
            request=new RebirthCamperRemoteStationCommand(RebirthCamperRemoteStationCommandKind.ScrapQueue,
                operation,operation,input.itemValue.ItemClass.GetItemName(),units,slot);
        }
        try
        {
            // Scrap can return attachments or the original source to the owner.
            // Publish the same native player image used by existing actor-paid crafting
            // before the server validates its coupled player/station receipt.
            SingletonMonoBehaviour<ConnectionManager>.Instance.SendToServer(
                NetPackageManager.GetPackage<NetPackagePlayerData>().Setup(player),false);
            bool sent=Command(s,request);if(sent)reason=null;return sent;
        }
        catch {throw;}
    }
    private static bool Command(Session s,RebirthCamperRemoteStationCommand command)
    {
        s.CommandPending=true;s.PendingCommand=command;s.PendingAction=RebirthCamperRemoteStationTransport.StationCommand;s.PendingOperation=command.Operation;
        SendCommand(s);return true;
    }
    internal static void SetBurning(RebirthCamperStationState state,bool burning)
    {
        var s=Find(state?.BoundVehicle?.world?.GetPrimaryPlayer(),state);
        if(s==null||s.GridPending||s.CommandPending)return;
        s.PendingOperation=Guid.NewGuid();s.PendingCommand=RebirthCamperRemoteStationCommand.Fuel(s.PendingOperation,burning);
        s.CommandPending=true;s.PendingAction=RebirthCamperRemoteStationTransport.FuelSwitch;s.PendingBurning=burning;
        SendCommand(s);
    }
    // A Send exception does not prove that the peer received nothing. Retain the
    // original operation for reconciliation. Native completed jobs do not expose
    // a terminal tombstone, so automatic replay could debit a completed job again.
    private static void SendCommand(Session s)
    {
        try
        {
            if(RebirthCamperRemoteStationCommandStatus.HasNativeOriginal(s.PendingCommand))
            {
                if(!RebirthCamperRemoteCommandOutbox.TryRecordFirstAttempt(s.Player,new RebirthCamperStationAddress(s.Snapshot.Storage.VehicleIdentity,s.Installation),s.PendingCommand,out bool first))
                    throw new InvalidOperationException("Original command intent could not be durably retained.");
                if(!first){s.CommandUncertain=true;return;}
            }
            Send(s,s.PendingAction,s.PendingCommand,s.PendingBurning);
        }
        catch(Exception error){s.CommandUncertain=true;Log.Warning("[CamperStation] Retaining uncertain original command; reconciliation required: "+error.Message);}
    }
    internal static bool HasUncertainCommand(EntityPlayerLocal player,RebirthCamperStationState state)
    {var s=Find(player,state);return s!=null&&s.CommandUncertain;}
    private static void Send(Session s,byte action,RebirthCamperRemoteStationCommand command=null,bool burning=false)
    {
        if(action==RebirthCamperRemoteStationTransport.FuelSwitch)
        {
            SingletonMonoBehaviour<ConnectionManager>.Instance.SendToServer(NetPackageManager.GetPackage<NetPackageRebirthCamperFuelSwitchV2>()
                .Setup(s.Player.entityId,s.Vehicle.entityId,s.Token,s.Snapshot?.Storage.VehicleIdentity??Guid.Empty,s.Installation,s.PendingOperation,burning),false);
            return;
        }
        SingletonMonoBehaviour<ConnectionManager>.Instance.SendToServer(NetPackageManager.GetPackage<NetPackageRebirthCamperStationSession>()
            .Setup(s.Player.entityId,s.Vehicle.entityId,s.Token,s.Snapshot?.Storage.VehicleIdentity??Guid.Empty,s.Installation,action,command,burning),false);
    }

    internal static bool IsPending(EntityPlayerLocal player,RebirthCamperStationState state)
    {
        var s=Find(player,state);
        return s==null || s.GridPending || s.CommandPending || s.Opening;
    }

    private static void FinishSwitchClose(Session s)
    {
        var completed=s.SwitchClosed;if(completed==null)return;
        s.SwitchClosed=null;s.Closed=true;
        if(sessions.TryGetValue(s.Player.entityId,out var current)&&ReferenceEquals(current,s))sessions.Remove(s.Player.entityId);
        // This acknowledges presentation close, never paid command settlement.
        // Native release can throw after dropping its old logical lease; the
        // exact already-closed workstation association must still be retired.
        try{s.NativeRelease();}
        finally{completed();}
    }
    internal static void CloseForSwitch(EntityPlayerLocal player,RebirthCamperStationState state,Action completed)
    {
        var s=Find(player,state);
        if(s==null || s.GridPending || s.CommandPending || s.SwitchClosed!=null || completed==null)
            throw new InvalidOperationException("Settled remote station required before switching.");
        s.SwitchClosed=completed;
        RetrySwitchClose(player,state);
    }

    internal static void RetrySwitchClose(EntityPlayerLocal player,RebirthCamperStationState state)
    {
        var s=Find(player,state);
        if(s==null || s.SwitchClosed==null
            || UnityEngine.Time.realtimeSinceStartup<s.NextSwitchClose)return;
        s.SwitchClosePending=true;
        s.NextSwitchClose=UnityEngine.Time.realtimeSinceStartup+2f;
        try{Send(s,RebirthCamperRemoteStationTransport.CloseSession);}
        catch(Exception error)
        {s.SwitchClosePending=false;Log.Warning("[CamperStation] Retaining original close for retry: "+error.Message);}
    }
    private static void Close(Session s)
    {
        if(s.Closed)return;
        try
        {
            // A grid callback can fail while retaining its durable operation.
            // Presentation lease retirement must still run; it cannot authorize
            // another server mutation while the pending receipt remains.
            s.GridClose?.Invoke();
            if(Current(s))Send(s,RebirthCamperRemoteStationTransport.CloseSession);
        }
        catch(Exception error)
        {Log.Warning("[CamperStation] Close callback or transport unavailable; server custody retained: "+error.Message);}
        finally
        {
            try{RebirthCamperStationPointerTransition.Cancel(s.Player,s.Vehicle);}
            finally
            {
                s.Closed=true;
                if(sessions.TryGetValue(s.Player.entityId,out var current)&&ReferenceEquals(current,s))sessions.Remove(s.Player.entityId);
                s.NativeRelease();
            }
        }
    }
}

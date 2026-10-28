using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine.Scripting;

internal enum RebirthCamperEnergyCommand:byte {Query,Close,Install,Remove,Component,Refuel,Enabled,Camp,Travel,PoiLights,Bounds,PoiSelection}
internal static class RebirthCamperEnergyAccess
{
    private sealed class ClientSession
    {internal Guid Token;internal EntityVehicle Vehicle;internal EntityPlayerLocal Player;internal Func<bool> Current;internal Action Release;internal XUiC_RebirthCamperEnergy View;internal bool? BoundsDesired;internal string Status;}
    private sealed class ServerSession
    {internal Func<bool> PhysicalCurrent;internal Guid Token;internal EntityVehicle Vehicle;internal EntityPlayer Player;internal ClientInfo Sender;internal string Creation;internal RebirthStablePlayerIdentity Owner;internal Waiting Wait;internal string Status;internal bool BoundsShown;internal float NextRecoveryCheck;}
    private sealed class Waiting
    {internal RebirthCamperEnergyCommand Command;internal Guid Source;internal int Slot,BagSlot;internal bool On;internal long Revision;internal byte[] Hash;internal float Deadline;}
    private static ClientSession client;
    private static readonly Dictionary<int,ServerSession> servers=new Dictionary<int,ServerSession>();
    private static readonly Dictionary<int,string> busyObservations=new Dictionary<int,string>();
    internal static bool Registered {get;private set;}
    internal static bool IsCurrentOwnerScope(Guid token,Guid vehicle,EntityPlayerLocal player)
    {
        var s=client;string identity;
        return token!=Guid.Empty&&s!=null&&s.Token==token&&ReferenceEquals(s.Player,player)&&s.Current()&&
            s.Vehicle.vehicle.GetUpdatedItemValue().TryGetMetadata("RebirthCamperIdentity",out identity)&&Guid.TryParse(identity,out var id)&&id==vehicle;
    }
    internal static void Install()
    {
        if(Registered)return;RebirthCamperEnergySourceCoordinator.Install();
        ModEvents.GameUpdate.RegisterHandler(Tick);ModEvents.WorldShuttingDown.RegisterHandler(Shutdown);Registered=true;
    }
    internal static void Open(EntityVehicle vehicle,EntityPlayerLocal player)
    {
        if(!Registered||!ThreadManager.IsMainThread()||!RebirthCamperModuleInteraction.Current(vehicle,player))throw new InvalidOperationException("Current Energy lease required.");
        var ui=LocalPlayerUI.GetUIForPlayer(player);GUIWindow window;
        if(ui==null||!ui.windowManager.TryGetWindow(RebirthCamperRadial.Energy,out window)||!(window is XUiWindowGroup group))throw new InvalidOperationException("Energy window unavailable.");
        var view=group.Controller.GetChildByType<XUiC_RebirthCamperEnergy>();if(view==null)throw new InvalidOperationException("Energy controller missing.");
        var physical=RebirthCamperEnergySelectionIntent.Take(player,vehicle);
        var nativeCurrent=RebirthCamperModuleInteraction.CaptureCurrent(vehicle,player);string creation=RebirthCamperCharacterAccess.CurrentCreation(player);
        if(string.IsNullOrEmpty(creation)||!RebirthStablePlayerIdentity.TryFromLocalPlatform(out var owner))throw new InvalidOperationException("Current Energy owner unavailable.");
        var session=new ClientSession {Token=Guid.NewGuid(),Vehicle=vehicle,Player=player,Release=RebirthCamperModuleInteraction.CaptureRelease(vehicle,player),View=view};
        session.Current=()=>(physical==null||physical.Current())&&ReferenceEquals(client,session)&&nativeCurrent()&&ReferenceEquals(GameManager.Instance?.World,player.world)&&
            ReferenceEquals(player.world.GetPrimaryPlayer(),player)&&!player.IsDead()&&RebirthCamperCharacterAccess.CurrentCreation(player)==creation;
        client=session;
        Log.Out("[REBIRTH EnergyFlow] ui-open vehicle="+vehicle.entityId+" player="+player.entityId+" remote="+player.world.IsRemote());
        view.Bind(vehicle,session.Current,()=>Close(session),(command,source,slot,on,bag)=>Command(session,command,source,slot,on,bag));
        if(physical!=null)view.SelectSource(physical.Source);
        if(!player.world.IsRemote())RebirthCamperEnergySourceCoordinator.Resume(vehicle,player,owner,creation,session.Current,_=>Refresh(session),session.Token);
        try{ui.windowManager.Open(window,true,false);Command(session,RebirthCamperEnergyCommand.Query,Guid.Empty,-1,false);}
        catch{Close(session);throw;}
    }
    private static void Close(ClientSession session)
    {
        if(!ReferenceEquals(client,session))return;
        try{if(session.Player.world.IsRemote())Send(session,RebirthCamperEnergyCommand.Close,Guid.Empty,-1,false);}
        finally{Log.Out("[REBIRTH EnergyFlow] ui-close vehicle="+session.Vehicle.entityId);client=null;session.Release();}
    }
    private static void Command(ClientSession session,RebirthCamperEnergyCommand command,Guid source,int slot,bool on,int bagSlot=-1)
    {
        if(command!=RebirthCamperEnergyCommand.Query)RebirthCamperEnergyGestureTrace.Write("access.command","command="+command+" slot="+slot+" bagSlot="+bagSlot+
            " current="+session.Current()+" revision="+session.View.Revision+" remote="+session.Player.world.IsRemote()+" cursor "+RebirthCamperEnergyGestureTrace.Stack(session.Player.PlayerUI?.xui?.DragAndDropWindow?.CurrentStack));
        if(!session.Current()){RebirthCamperEnergyGestureTrace.Write("access.refused","reason=session-current command="+command);return;}
        if(command!=RebirthCamperEnergyCommand.Query)session.Status=null;
        if(session.Player.world.IsRemote())
        {if(command==RebirthCamperEnergyCommand.Bounds)session.BoundsDesired=on;Send(session,command,source,slot,on,bagSlot);return;}
        if(command==RebirthCamperEnergyCommand.Bounds)
        {
            // Visibility only; the native scoped display owns eligibility and
            // clearing. Never dispatch this to mode/source/claim mutations.
            bool shown=RebirthCamperEnergyBoundsDisplay.SetShown(session.Vehicle,session.Player,on,out _);
            Refresh(session,shown?null:"xuiRebirthCamperEnergyBoundsUnavailable");return;
        }
        if(!RebirthStablePlayerIdentity.TryFromLocalPlatform(out var owner))return;
        Execute(session.Vehicle,session.Player,owner,RebirthCamperCharacterAccess.CurrentCreation(session.Player),session.Current,
            command,source,slot,on,session.View.Revision,()=>Refresh(session),bagSlot,session.Token,reason=>session.Status=reason);Refresh(session);
    }
    private static void Refresh(ClientSession session,string status=null)
    {
        bool measure=RebirthGameBridgeFrames.IsRecording;
        long started=measure?System.Diagnostics.Stopwatch.GetTimestamp():0;
        double runtimeMs=-1,poiMs=-1,statusMs=-1,presentMs=-1;
        try
        {
            if(session.Current())
            {
                var snapshot=RebirthCamperEnergyRuntime.Observe(session.Vehicle);
                long afterRuntime=measure?System.Diagnostics.Stopwatch.GetTimestamp():0;
                var poi=RebirthCamperEnergyDeviceAdapter.ObservePoi(session.Vehicle);
                long afterPoi=measure?System.Diagnostics.Stopwatch.GetTimestamp():0;
                bool busy=Busy(session.Vehicle);
                bool bounds=CanShowBounds(session.Vehicle,session.Player);
                bool shown=RebirthCamperEnergyBoundsDisplay.IsShown(session.Vehicle,session.Player);
                int capacity=RebirthCamperEnergyCapacity.ForActor(session.Player);
                long afterStatus=measure?System.Diagnostics.Stopwatch.GetTimestamp():0;
                session.View.Present(snapshot,poi,busy,status??session.Status,bounds,shown,capacity);
                if(measure)
                {
                    long end=System.Diagnostics.Stopwatch.GetTimestamp();
                    double ms=1000.0/System.Diagnostics.Stopwatch.Frequency;
                    runtimeMs=(afterRuntime-started)*ms;poiMs=(afterPoi-afterRuntime)*ms;
                    statusMs=(afterStatus-afterPoi)*ms;presentMs=(end-afterStatus)*ms;
                    if((end-started)*ms>=12)
                        Log.Out("[REBIRTH EnergyPerf] refresh frame="+UnityEngine.Time.frameCount+
                            " totalMs="+((end-started)*ms).ToString("F2")+
                            " runtimeMs="+((afterRuntime-started)*ms).ToString("F2")+
                            " poiMs="+((afterPoi-afterRuntime)*ms).ToString("F2")+
                            " statusMs="+((afterStatus-afterPoi)*ms).ToString("F2")+
                            " presentMs="+((end-afterStatus)*ms).ToString("F2"));
                }
            }
        }
        finally
        {
            if(measure)RebirthGameBridgeFrames.RecordEnergyRefresh((System.Diagnostics.Stopwatch.GetTimestamp()-started)*1000.0/System.Diagnostics.Stopwatch.Frequency,runtimeMs,poiMs,statusMs,presentMs);
        }
    }
    private static bool CanShowBounds(EntityVehicle vehicle,EntityPlayer player)
    {
        if(!ThreadManager.IsMainThread()||vehicle?.world==null||player==null||player.IsDead()||!ReferenceEquals(vehicle.world,player.world)||
            !ReferenceEquals(GameManager.Instance?.World,vehicle.world)||!ReferenceEquals(vehicle.world.GetEntity(vehicle.entityId),vehicle)||
            !ReferenceEquals(vehicle.world.GetEntity(player.entityId),player)||
            !RebirthCamperEnergyRuntime.TryObserveMode(vehicle,out var mode,out _,out _)||mode!=RebirthCamperEnergyMode.Camp)return false;
        var owner=vehicle.GetOwner();var data=GameManager.Instance.GetPersistentPlayerList()?.GetPlayerDataFromEntityID(player.entityId);
        return owner!=null&&data!=null&&(owner.Equals(data.PrimaryId)||owner.Equals(data.NativeId));
    }
    internal static bool Busy(EntityVehicle vehicle)
    {
        try
        {
            string reasons=(RebirthCamperEnergyRuntime.IsHeld(vehicle)?"runtime,":"")+
                (RebirthCamperStationFuelOriginal.BlocksVehicle(vehicle)?"station-fuel,":"")+
                (RebirthCamperModuleRuntime.For(vehicle).TransferPending?"module,":"")+
                (RebirthCamperEnergySourceCoordinator.IsBusy(vehicle)?"source,":"")+
                (RebirthCamperEnergyMaintenance.IsBusy(vehicle)?"maintenance,":"")+
                (RebirthCamperEnergyCustody.FindPending(RebirthCamperEnergyRuntime.For(vehicle).Identity)!=null?"receipt,":"");
            if(!busyObservations.TryGetValue(vehicle.entityId,out var previous)||previous!=reasons)
            {busyObservations[vehicle.entityId]=reasons;Log.Out("[REBIRTH EnergyFlow] busy vehicle="+vehicle.entityId+" reasons="+(reasons.Length==0?"none":reasons));}
            return reasons.Length!=0;
        }
        catch(Exception error){Log.Error("[REBIRTH EnergyFlow] busy-check-failed vehicle="+vehicle?.entityId+" error="+error);return true;}
    }
    internal static byte[] ActorHash(RebirthCamperEnergyActorImage actor)
    {using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream,Encoding.UTF8,true)){actor.Write(writer);writer.Flush();using(var hash=SHA256.Create())return hash.ComputeHash(stream.ToArray());}}
    private static void Send(ClientSession s,RebirthCamperEnergyCommand command,Guid source,int slot,bool on,int bagSlot=-1)
    {
        if(command==RebirthCamperEnergyCommand.Query)on=s.BoundsDesired??RebirthCamperEnergyBoundsDisplay.IsShown(s.Vehicle,s.Player);
        byte[] hash=new byte[0];
        if(command>=RebirthCamperEnergyCommand.Install&&command<=RebirthCamperEnergyCommand.Enabled)
        {
            hash=ActorHash(RebirthCamperEnergyActorImage.Capture(s.Player));
            SingletonMonoBehaviour<ConnectionManager>.Instance.SendToServer(NetPackageManager.GetPackage<NetPackagePlayerData>().Setup(s.Player),false);
        }
        SingletonMonoBehaviour<ConnectionManager>.Instance.SendToServer(NetPackageManager.GetPackage<NetPackageRebirthCamperEnergyRequest>().Setup(
            s.Player.entityId,s.Vehicle.entityId,s.Token,command,source,slot,on,s.View.Revision,hash,bagSlot),false);
    }
    private static bool Current(ServerSession s)=>(s.PhysicalCurrent==null||s.PhysicalCurrent())&&RebirthCamperRemoteAdmission.CanAccess(s.Vehicle,s.Player,s.Sender,()=>
        ReferenceEquals(GameManager.Instance?.World,s.Player.world)&&RebirthCamperCharacterAccess.CurrentCreation(s.Player)==s.Creation&&
        ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance.Clients.ForEntityId(s.Player.entityId),s.Sender)&&
        servers.TryGetValue(s.Player.entityId,out var live)&&ReferenceEquals(live,s));
    internal static void Receive(World world,ClientInfo sender,int actor,int vehicle,Guid token,RebirthCamperEnergyCommand command,Guid source,int slot,bool on,long revision,byte[] hash,int bagSlot)
    {
        if(!Registered||!ThreadManager.IsMainThread()||world==null||world.IsRemote()||token==Guid.Empty||sender?.entityId!=actor||
            !Enum.IsDefined(typeof(RebirthCamperEnergyCommand),command)||bagSlot< -1||bagSlot>168||
            bagSlot>=0&&command!=RebirthCamperEnergyCommand.Install&&command!=RebirthCamperEnergyCommand.Remove&&command!=RebirthCamperEnergyCommand.Component)return;
        var player=world.GetEntity(actor) as EntityPlayer;var camper=world.GetEntity(vehicle) as EntityVehicle;
        string creation=RebirthCamperCharacterAccess.CurrentCreation(player);
        if(string.IsNullOrEmpty(creation)||!RebirthStablePlayerIdentity.TryFromClientInfo(sender,out var owner)||
            !RebirthCamperRemoteAdmission.CanAccess(camper,player,sender,()=>RebirthCamperCharacterAccess.CurrentCreation(player)==creation))return;
        if(command==RebirthCamperEnergyCommand.Query)
        {
            if(!servers.TryGetValue(actor,out var old)||old.Token!=token)
            {
                Func<bool> physical;
                try {physical=RebirthCamperPhysicalInteraction.TakeServerGuard(camper,player,RebirthCamperPhysicalInteraction.ServerSelection(player));}
                catch(InvalidOperationException){return;}
                servers[actor]=new ServerSession {Token=token,Vehicle=camper,Player=player,Sender=sender,Owner=owner,Creation=creation,PhysicalCurrent=physical};
            }
        }
        if(!servers.TryGetValue(actor,out var session)||session.Token!=token||!ReferenceEquals(session.Vehicle,camper)||!Current(session))return;
        if(command==RebirthCamperEnergyCommand.Close){servers.Remove(actor);return;}
        if(command==RebirthCamperEnergyCommand.Query)session.BoundsShown=on&&CanShowBounds(camper,player);
        if(command==RebirthCamperEnergyCommand.Bounds)
        {
            bool allowed=CanShowBounds(camper,player);session.BoundsShown=on&&allowed;
            session.Status=on&&!allowed?"xuiRebirthCamperEnergyBoundsCampOnly":null;Reply(session);return;
        }
        if(command>=RebirthCamperEnergyCommand.Install&&command<=RebirthCamperEnergyCommand.Enabled)
        {
            if(hash==null||hash.Length!=32)return;
            if(session.Wait!=null){Reply(session);return;}
            session.Status=null;
            if(!SourceReady(session,hash))
            {
                session.Wait=new Waiting {Command=command,Source=source,Slot=slot,BagSlot=bagSlot,On=on,Revision=revision,Hash=(byte[])hash.Clone(),Deadline=UnityEngine.Time.realtimeSinceStartup+10f};
                session.Status="xuiRebirthCamperEnergyOwnerWaiting";Reply(session);return;
            }
        }
        if(command==RebirthCamperEnergyCommand.Query&&UnityEngine.Time.realtimeSinceStartup>=session.NextRecoveryCheck)
        {
            // Status polling stays responsive, but durable transfer recovery
            // need not search the journal on every quarter-second poll.
            session.NextRecoveryCheck=UnityEngine.Time.realtimeSinceStartup+5f;
            RebirthCamperEnergySourceCoordinator.Resume(camper,player,owner,creation,()=>Current(session),_=>Reply(session),session.Token);
        }
        if(command!=RebirthCamperEnergyCommand.Query)session.Status=null;
        Execute(camper,player,owner,creation,()=>Current(session),command,source,slot,on,revision,()=>Reply(session),bagSlot,session.Token,reason=>session.Status=reason);Reply(session);
    }
    internal static void ExecutePoiRadial(EntityVehicle vehicle,EntityPlayer player,bool on)
    {
        Execute(vehicle,player,null,RebirthCamperCharacterAccess.CurrentCreation(player),()=>true,
            RebirthCamperEnergyCommand.PoiLights,Guid.Empty,-1,on,-1,()=>{},refused:key=>
            {
                if(player is EntityPlayerLocal local)GameManager.ShowTooltip(local,Localization.Get(key));
            });
    }
    internal static void ExecuteModeShortcut(EntityVehicle vehicle,EntityPlayer player,RebirthCamperEnergyMode target,long revision)
    {
        if(!RebirthCamperModeInput.Allowed(vehicle,player)
            ||string.IsNullOrEmpty(RebirthCamperCharacterAccess.CurrentCreation(player))
            ||!RebirthCamperEnergyRuntime.TryObservePresentation(vehicle,out var current,out long liveRevision,out _,out _)
            ||liveRevision!=revision
            ||!(current==RebirthCamperEnergyMode.Travel&&target==RebirthCamperEnergyMode.Camp
                ||current==RebirthCamperEnergyMode.Camp&&target==RebirthCamperEnergyMode.Travel))return;
        // Same custody-busy and authoritative RequestMode path as Energy UI.
        Execute(vehicle,player,null,RebirthCamperCharacterAccess.CurrentCreation(player),()=>true,
            target==RebirthCamperEnergyMode.Camp?RebirthCamperEnergyCommand.Camp:RebirthCamperEnergyCommand.Travel,
            Guid.Empty,-1,false,revision,()=>{});
    }
    private static void Execute(EntityVehicle vehicle,EntityPlayer player,RebirthStablePlayerIdentity owner,string creation,Func<bool> current,
        RebirthCamperEnergyCommand command,Guid source,int slot,bool on,long revision,Action refreshed,int bagSlot=-1,Guid token=default(Guid),Action<string> refused=null)
    {
        if(command==RebirthCamperEnergyCommand.Query)return;
        Log.Out("[REBIRTH EnergyFlow] execute command="+command+" vehicle="+vehicle?.entityId+" actor="+player?.entityId+" source="+source+" slot="+slot+" bagSlot="+bagSlot+" requestedRevision="+revision+" on="+on);
        if(Busy(vehicle)){refused?.Invoke("xuiRebirthCamperEnergySaving");RebirthCamperEnergyGestureTrace.Write("access.refused","reason=source-custody-busy command="+command);return;}
        if(command==RebirthCamperEnergyCommand.Camp||command==RebirthCamperEnergyCommand.Travel)
        {
            bool modeAccepted=RebirthCamperEnergyRuntime.RequestMode(vehicle,player,command==RebirthCamperEnergyCommand.Camp?RebirthCamperEnergyMode.Camp:RebirthCamperEnergyMode.Travel,out var reason);
            RebirthCamperEnergyGestureTrace.Write("access.mode","command="+command+" accepted="+modeAccepted+" reason="+reason);
            if(!modeAccepted)
            {
                string key=ModeRefusalKey(reason);refused?.Invoke(key);
                if(player is EntityPlayerLocal localRefused)GameManager.ShowTooltip(localRefused,Localization.Get(key));
            }
            else if(player is EntityPlayerLocal localAccepted)XUiC_RebirthCamperTransitionHud.Track(vehicle,localAccepted);
            return;
        }
        if(command==RebirthCamperEnergyCommand.PoiLights)
        {
            bool poiAccepted=RebirthCamperEnergyDeviceAdapter.SetPoiLights(player,vehicle,on);
            Log.Out("[REBIRTH EnergyFlow] poi-result vehicle="+vehicle.entityId+" requestedOn="+on+" accepted="+poiAccepted+" observedOn="+RebirthCamperEnergyDeviceAdapter.ObservePoi(vehicle)?.IsOn);
            if(!poiAccepted)
            {
                if(player is EntityPlayerLocal localPoiRefused)
                {
                    var poi=RebirthCamperEnergyDeviceAdapter.ObservePoi(vehicle);
                    var energy=RebirthCamperEnergyRuntime.For(vehicle).Snapshot();
                    string message=poi==null||!poi.InRange?Localization.Get("xuiRebirthCamperEnergyPoiOutside"):
                        poi.SupportedLights==0?Localization.Get("xuiRebirthCamperEnergyNoPoi"):
                        on&&!energy.Sources.Any(candidate=>candidate.Kind!=RebirthCamperEnergySourceKind.Solar&&
                            candidate.IsOn&&candidate.Status==RebirthCamperEnergySourceStatus.Available)?
                            Localization.Get("xuiRebirthCamperEnergyPoiGeneratorRequired"):
                        on&&!RebirthCamperEnergyDeviceAdapter.CanSupplyPoiLights(
                            energy,poi,out int available)?
                            poi.ProspectiveRuntimeSeconds<0?Localization.Get("xuiRebirthCamperEnergyPoiPowerUnknown"):
                            string.Format(Localization.Get("xuiRebirthCamperEnergyPoiRuntimeNeededFormat"),
                                (int)(poi.ProspectiveRuntimeSeconds/60),(int)(poi.ProspectiveRuntimeSeconds%60)):
                            Localization.Get("xuiRebirthCamperEnergyOwnerChanged");
                    GameManager.ShowTooltip(localPoiRefused,message);
                }
                else refused?.Invoke("xuiRebirthCamperEnergyOwnerChanged");
            }
            else if(player is EntityPlayerLocal localPoiAccepted)
                GameManager.ShowTooltip(localPoiAccepted,Localization.Get(on?
                    "xuiRebirthCamperEnergyPoiEnabled":"xuiRebirthCamperEnergyPoiDisabled"));
            return;
        }
        if(command==RebirthCamperEnergyCommand.PoiSelection)
        {
            bool limitAccepted=RebirthCamperEnergyDeviceAdapter.SetPoiLimit(player,vehicle,slot);
            Log.Out("[REBIRTH EnergyFlow] poi-selection vehicle="+vehicle.entityId+" count="+slot+" accepted="+limitAccepted);
            if(!limitAccepted)refused?.Invoke("xuiRebirthCamperEnergyOwnerChanged");
            return;
        }
        RebirthCamperEnergySourcePlan.Action action;
        switch(command)
        {
            case RebirthCamperEnergyCommand.Install:action=RebirthCamperEnergySourcePlan.Action.Install;break;
            case RebirthCamperEnergyCommand.Remove:action=RebirthCamperEnergySourcePlan.Action.Remove;break;
            case RebirthCamperEnergyCommand.Component:action=RebirthCamperEnergySourcePlan.Action.Component;break;
            case RebirthCamperEnergyCommand.Refuel:action=RebirthCamperEnergySourcePlan.Action.Refuel;break;
            case RebirthCamperEnergyCommand.Enabled:action=RebirthCamperEnergySourcePlan.Action.Enabled;break;
            default:return;
        }
        bool accepted=RebirthCamperEnergySourceCoordinator.Begin(vehicle,player,owner,creation,revision,action,source,slot,on,current,_=>refreshed(),bagSlot,token);
        // Explicit bank OFF is also an explicit POI OFF request, even when a
        // second bank in this camper remains on. Clear intent when the source
        // command is accepted, before its asynchronous custody/save work; waiting
        // for Consumers to notice an all-banks-off snapshot lets old ON intent
        // survive and restart the lights. The accepted source transaction has
        // already admitted this actor; clear only this camper's intent without
        // rescanning POI lights or reapplying the stricter POI-button owner gate.
        if(accepted&&command==RebirthCamperEnergyCommand.Enabled&&!on)
            RebirthCamperEnergyDeviceAdapter.ClearAcceptedBankPoiRequest(vehicle);
        if(!accepted)refused?.Invoke("xuiRebirthCamperEnergyOwnerChanged");
        RebirthCamperEnergyGestureTrace.Write("access.source-result","command="+command+" accepted="+accepted+" slot="+slot+" bagSlot="+bagSlot+" revision="+revision);
    }
    private static string ModeRefusalKey(string reason)
    {
        switch(reason)
        {
            case "Park and leave the driver seat before deploying.":return "xuiRebirthCamperModeParkRequired";
            case "Vehicle access required.":return "xuiRebirthCamperModeOwnerRequired";
            case "Source custody recovery is pending.":return "xuiRebirthCamperEnergySaving";
            case "Current camp transition does not admit this request.":return "xuiRebirthCamperModeTransitionBusy";
            default:return "xuiRebirthCamperEnergyOwnerChanged";
        }
    }
    private static void Reply(ServerSession s)
    {
        if(!Current(s))return;
        bool canShow=CanShowBounds(s.Vehicle,s.Player);if(!canShow)s.BoundsShown=false;
        s.Sender.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthCamperEnergySnapshot>().Setup(s.Token,s.Vehicle.entityId,
            RebirthCamperEnergySnapshotWire.Write(RebirthCamperEnergyRuntime.Observe(s.Vehicle),RebirthCamperEnergyDeviceAdapter.ObservePoi(s.Vehicle),s.Wait!=null||Busy(s.Vehicle),s.Status,canShow,s.BoundsShown,RebirthCamperEnergyCapacity.ForActor(s.Player))));
    }
    internal static void Present(Guid token,int entity,byte[] bytes)
    {
        var s=client;if(s==null||s.Token!=token||s.Vehicle.entityId!=entity||!s.Current()||!s.Player.world.IsRemote())return;
        RebirthCamperEnergySnapshotWire.Read(bytes,out var snapshot,out var poi,out bool busy,out string status,out bool boundsAllowed,out bool boundsShown,out int sourceCapacity);
        // An older poll may arrive after a human toggle. Keep that exact
        // desired visibility until a matching authorized reply acknowledges it.
        if(!boundsAllowed||!s.BoundsDesired.HasValue||s.BoundsDesired.Value==boundsShown)
        {
            bool applied=RebirthCamperEnergyBoundsDisplay.SetShown(s.Vehicle,s.Player,boundsAllowed&&boundsShown,out _);
            if(!boundsAllowed||applied)s.BoundsDesired=null;
            if(boundsAllowed&&boundsShown&&!applied){s.BoundsDesired=true;status="xuiRebirthCamperEnergyBoundsUnavailable";}
        }
        s.View.Present(snapshot,poi,busy,status,boundsAllowed,RebirthCamperEnergyBoundsDisplay.IsShown(s.Vehicle,s.Player),sourceCapacity);
    }
    private static bool SourceReady(ServerSession s,byte[] hash)=>RebirthCamperEnergySavedOwner.TryRead(s.Owner,out var saved,out _)&&ActorHash(saved).SequenceEqual(hash);
    private static void Tick(ref ModEvents.SGameUpdateData data)
    {
        if(servers.Count==0)return;
        foreach(var s in servers.Values.ToArray())
        {
            if(!Current(s)){servers.Remove(s.Player.entityId);continue;}
            var wait=s.Wait;if(wait==null)continue;
            if(UnityEngine.Time.realtimeSinceStartup>=wait.Deadline)
            {s.Wait=null;s.Status="xuiRebirthCamperEnergyOwnerChanged";Reply(s);continue;}
            if(!SourceReady(s,wait.Hash))continue;
            s.Wait=null;s.Status=null; // Retire the one request BEFORE executing. No repeat debit.
            Execute(s.Vehicle,s.Player,s.Owner,s.Creation,()=>Current(s),wait.Command,wait.Source,wait.Slot,wait.On,wait.Revision,()=>Reply(s),wait.BagSlot,s.Token);Reply(s);
        }
    }
    private static void Shutdown(ref ModEvents.SWorldShuttingDownData data){servers.Clear();client=null;busyObservations.Clear();RebirthCamperEnergyCustody.ClearRuntimeScope();RebirthCamperTransferHistoryScope.Clear();} // Durable source journals remain.
}

[Preserve]
public sealed class NetPackageRebirthCamperEnergyRequest:NetPackage
{
    private int actor,vehicle,slot,bagSlot;private Guid token,source;private RebirthCamperEnergyCommand command;private bool on;private long revision;private byte[] hash;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToServer;
    internal NetPackageRebirthCamperEnergyRequest Setup(int a,int v,Guid t,RebirthCamperEnergyCommand c,Guid s,int i,bool o,long r,byte[] h,int b)
    {actor=a;vehicle=v;token=t;command=c;source=s;slot=i;on=o;revision=r;hash=h;bagSlot=b;return this;}
    public override void read(PooledBinaryReader r)
    {actor=r.ReadInt32();vehicle=r.ReadInt32();token=new Guid(r.ReadBytes(16));command=(RebirthCamperEnergyCommand)r.ReadByte();source=new Guid(r.ReadBytes(16));slot=r.ReadInt32();on=r.ReadBoolean();revision=r.ReadInt64();hash=CamperFrame.Read(r);bagSlot=r.ReadInt32();if(hash.Length!=0&&hash.Length!=32)throw new InvalidDataException("Energy actor hash invalid.");}
    public override void write(PooledBinaryWriter w)
    {base.write(w);w.Write(actor);w.Write(vehicle);w.Write(token.ToByteArray());w.Write((byte)command);w.Write(source.ToByteArray());w.Write(slot);w.Write(on);w.Write(revision);CamperFrame.Write(w,hash);w.Write(bagSlot);}
    public override void ProcessPackage(World world,GameManager callbacks)
    {if(!ValidEntityIdForSender(actor))return;RebirthCamperEnergyAccess.Receive(world,Sender,actor,vehicle,token,command,source,slot,on,revision,hash,bagSlot);}
    public int GetLength()=>0;
}
[Preserve]
public sealed class NetPackageRebirthCamperEnergySnapshot:NetPackage
{
    private Guid token;private int entity;private byte[] bytes;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToClient;
    internal NetPackageRebirthCamperEnergySnapshot Setup(Guid t,int e,byte[] b){token=t;entity=e;bytes=b;return this;}
    public override void read(PooledBinaryReader r){token=new Guid(r.ReadBytes(16));entity=r.ReadInt32();bytes=CamperFrame.Read(r);}
    public override void write(PooledBinaryWriter w){base.write(w);w.Write(token.ToByteArray());w.Write(entity);CamperFrame.Write(w,bytes);}
    public override void ProcessPackage(World world,GameManager callbacks){if(world?.IsRemote()==true)RebirthCamperEnergyAccess.Present(token,entity,bytes);}
    public int GetLength()=>0;
}

// Actual client typed-status handler with explicit native/session/projection
// doubles. NOT_RUN. Does not qualify server saved evidence or network delivery.
using System;
using System.Collections.Generic;
class EntityPlayerLocal {internal int entityId=1;}
static class GameManager {internal static void ShowTooltip(EntityPlayerLocal p,string text){}}
static class Localization {internal static string Get(string key)=>key;}
class World {internal EntityPlayerLocal Player=new EntityPlayerLocal();internal EntityPlayerLocal GetPrimaryPlayer()=>Player;}
class EntityVehicle {internal World world=new World();}
class Storage {internal Guid VehicleIdentity;}
class RebirthCamperTransferSnapshot {internal Storage Storage=new Storage();}
enum RebirthCamperRemoteStationCommandKind {Craft,Cancel,CraftPlayer,ScrapQueue,ScrapCancel,FuelOn,FuelOff}
class RebirthCamperRemoteStationCommand {internal Guid Operation,Job;internal RebirthCamperRemoteStationCommandKind Kind;internal bool IsFuel=>Kind==RebirthCamperRemoteStationCommandKind.FuelOn||Kind==RebirthCamperRemoteStationCommandKind.FuelOff;internal static RebirthCamperRemoteStationCommand Fuel(Guid op,bool value)=>new RebirthCamperRemoteStationCommand{Operation=op,Job=op,Kind=value?RebirthCamperRemoteStationCommandKind.FuelOn:RebirthCamperRemoteStationCommandKind.FuelOff};}
class RebirthCamperStationState{internal EntityVehicle BoundVehicle;}
static class Log{internal static void Warning(string text){}}
struct RebirthCamperStationAddress {internal RebirthCamperStationAddress(Guid vehicle,Guid station){}}
static class RebirthCamperRemoteCommandOutbox
{
    internal static bool Mirror=true;
    internal static bool Record=true,First=true;internal static int Recorded;
    internal static bool TryRecordFirstAttempt(EntityPlayerLocal player,RebirthCamperStationAddress address,RebirthCamperRemoteStationCommand command,out bool first){Recorded++;first=First;return Record;}
    internal static bool TryMirrorTerminal(EntityPlayerLocal player,RebirthCamperStationAddress address,RebirthCamperRemoteStationCommand command,RebirthCamperRemoteStationCommandStatus.State status)=>Mirror;
}
static class RebirthCamperRemoteStationCommandStatus {internal enum State {Unknown,Pending,Applied,Rejected}internal static bool HasNativeOriginal(RebirthCamperRemoteStationCommand command)=>command!=null;}
static class RebirthCamperRemoteStationTransport {internal const byte StationCommand=2,FuelSwitch=4;}
static class ThreadManager {internal static bool IsMainThread()=>true;}
static class RebirthCamperCommandStatusClientFixture
{
    private sealed class Session
    {
        internal EntityVehicle Vehicle;internal EntityPlayerLocal Player;internal Guid Token,Installation,PendingOperation;
        internal RebirthCamperTransferSnapshot Snapshot;internal bool CommandPending=true,CommandUncertain,Live=true,GridPending,PendingBurning;
        internal byte PendingAction=2;internal RebirthCamperRemoteStationCommand PendingCommand;
    }
    private static readonly Dictionary<int,Session> sessions=new Dictionary<int,Session>();
    private static bool Current(Session s)=>s.Live;
    private static Session Find(EntityPlayerLocal p,RebirthCamperStationState state)=>p!=null&&sessions.TryGetValue(p.entityId,out var s)&&ReferenceEquals(s.Vehicle,state?.BoundVehicle)&&s.Live?s:null;
    private static int sent;private static byte sentAction;private static RebirthCamperRemoteStationCommand sentCommand;private static bool sendThrows;
    private static void Send(Session s,byte action,RebirthCamperRemoteStationCommand command,bool burning){sent++;sentAction=action;sentCommand=command;if(sendThrows)throw new InvalidOperationException("ambiguous send");}
    private static int retired;private static bool applied;private static byte retiredAction;
    private static void ReceiveCore(EntityVehicle vehicle,Guid token,RebirthCamperTransferSnapshot snapshot,byte action,bool accepted)
    {retired++;retiredAction=action;applied=accepted;sessions[1].CommandPending=false;}
    // PRODUCTION_CLIENT_STATUS
    // PRODUCTION_CLIENT_COMMAND_REPLY
    // PRODUCTION_CLIENT_FUEL
    // PRODUCTION_CLIENT_SEND
    private static Session Original()
    {
        retired=0;applied=false;sent=0;sendThrows=false;RebirthCamperRemoteCommandOutbox.Mirror=true;RebirthCamperRemoteCommandOutbox.Record=true;RebirthCamperRemoteCommandOutbox.First=true;RebirthCamperRemoteCommandOutbox.Recorded=0;
        var s=new Session{Vehicle=new EntityVehicle(),Token=Guid.NewGuid(),Installation=Guid.NewGuid(),PendingOperation=Guid.NewGuid(),Snapshot=new RebirthCamperTransferSnapshot()};
        s.Snapshot.Storage.VehicleIdentity=Guid.NewGuid();
        s.Player=s.Vehicle.world.Player;
        s.PendingCommand=new RebirthCamperRemoteStationCommand{Operation=s.PendingOperation,Job=Guid.NewGuid(),Kind=RebirthCamperRemoteStationCommandKind.CraftPlayer};
        sessions[1]=s;return s;
    }
    private static void Receive(Session s,RebirthCamperRemoteStationCommandStatus.State state,Guid? operation=null,Guid? job=null,Guid? station=null)
    {ReceiveOriginalStatus(s.Vehicle,s.Token,s.Snapshot.Storage.VehicleIdentity,station??s.Installation,operation??s.PendingOperation,job??s.PendingCommand.Job,s.PendingCommand.Kind,state,s.Snapshot);}
    private static void Need(bool value,string reason){if(!value)throw new Exception(reason);}
    public static void Main()
    {
        foreach(byte purpose in new[]{RebirthCamperRemoteStationTransport.StationCommand,RebirthCamperRemoteStationTransport.FuelSwitch})
        {
            var s=Original();s.PendingAction=purpose;
            if(purpose==RebirthCamperRemoteStationTransport.FuelSwitch){s.PendingCommand.Kind=RebirthCamperRemoteStationCommandKind.FuelOn;s.PendingCommand.Job=s.PendingOperation;}
            ReceiveCommand(s.Vehicle,s.Token,s.Snapshot,purpose,s.PendingOperation,false);Need(retired==0&&s.CommandPending&&s.CommandUncertain,"generic failure released possibly entered command");
            s.CommandUncertain=false;ReceiveCommand(s.Vehicle,s.Token,null,purpose,s.PendingOperation,true);Need(retired==0&&s.CommandPending&&s.CommandUncertain,"missing accepted postimage became terminal");
            s.CommandUncertain=false;ReceiveCommand(s.Vehicle,s.Token,s.Snapshot,purpose,s.PendingOperation,true);Need(retired==0&&s.CommandPending&&s.CommandUncertain,"handler acknowledgement bypassed saved terminal");
            var original=s.PendingCommand;Guid old=s.PendingOperation;Receive(s,RebirthCamperRemoteStationCommandStatus.State.Applied);Need(retired==1,"first saved terminal refused");
            s.CommandPending=true;s.CommandUncertain=false;s.PendingOperation=Guid.NewGuid();s.PendingCommand=new RebirthCamperRemoteStationCommand{Operation=s.PendingOperation,Job=Guid.NewGuid(),Kind=original.Kind};
            ReceiveCommand(s.Vehicle,s.Token,s.Snapshot,purpose,old,true);ReceiveCommand(s.Vehicle,s.Token,s.Snapshot,purpose,old,false);Need(retired==1&&!s.CommandUncertain&&s.CommandPending,"old reordered reply affected next original");
            ReceiveOriginalStatus(s.Vehicle,s.Token,s.Snapshot.Storage.VehicleIdentity,s.Installation,old,original.Job,original.Kind,RebirthCamperRemoteStationCommandStatus.State.Applied,s.Snapshot);Need(retired==1&&s.CommandPending,"old typed terminal cleared next original");
            byte wrong=purpose==RebirthCamperRemoteStationTransport.StationCommand?RebirthCamperRemoteStationTransport.FuelSwitch:RebirthCamperRemoteStationTransport.StationCommand;ReceiveCommand(s.Vehicle,s.Token,s.Snapshot,wrong,s.PendingOperation,true);Need(retired==1&&!s.CommandUncertain,"foreign purpose affected current original");
            var foreign=new RebirthCamperTransferSnapshot();foreign.Storage.VehicleIdentity=Guid.NewGuid();ReceiveCommand(s.Vehicle,s.Token,foreign,purpose,s.PendingOperation,true);Need(retired==1&&!s.CommandUncertain,"foreign snapshot affected current original");
            Receive(s,RebirthCamperRemoteStationCommandStatus.State.Applied);Need(retired==2,"second exact saved terminal failed");
        }
        foreach(var state in new[]{RebirthCamperRemoteStationCommandStatus.State.Unknown,RebirthCamperRemoteStationCommandStatus.State.Pending})
        {var s=Original();Receive(s,state);Need(retired==0&&s.CommandPending&&s.CommandUncertain,"Unknown/pending status released original");}
        var current=Original();Receive(current,RebirthCamperRemoteStationCommandStatus.State.Applied);Need(retired==1&&applied,"Exact applied status not accepted");
        Receive(current,RebirthCamperRemoteStationCommandStatus.State.Applied);Need(retired==1,"Late status settled twice");
        current=Original();Receive(current,RebirthCamperRemoteStationCommandStatus.State.Rejected);Need(retired==1&&!applied,"Typed rejected status promoted to applied");
        current=Original();Receive(current,RebirthCamperRemoteStationCommandStatus.State.Applied,operation:Guid.NewGuid());Need(retired==0,"Foreign operation settled original");
        current=Original();Receive(current,RebirthCamperRemoteStationCommandStatus.State.Applied,job:Guid.NewGuid());Need(retired==0,"Foreign job settled original");
        current=Original();Receive(current,RebirthCamperRemoteStationCommandStatus.State.Applied,station:Guid.NewGuid());Need(retired==0,"Foreign installation settled original");
        current=Original();current.Live=false;Receive(current,RebirthCamperRemoteStationCommandStatus.State.Applied);Need(retired==0,"Expired session settled original");
        current=Original();RebirthCamperRemoteCommandOutbox.Mirror=false;Receive(current,RebirthCamperRemoteStationCommandStatus.State.Applied);Need(retired==0&&current.CommandPending,"Failed terminal mirror released original");
        current=Original();current.PendingAction=RebirthCamperRemoteStationTransport.FuelSwitch;current.PendingCommand.Kind=RebirthCamperRemoteStationCommandKind.FuelOn;current.PendingCommand.Job=current.PendingOperation;
        Receive(current,RebirthCamperRemoteStationCommandStatus.State.Pending);Need(retired==0&&current.CommandPending,"Pending fuel released original");
        Receive(current,RebirthCamperRemoteStationCommandStatus.State.Applied);Need(retired==1&&applied&&retiredAction==RebirthCamperRemoteStationTransport.FuelSwitch,"Fuel terminal projected as crafting command");
        current=Original();current.PendingAction=RebirthCamperRemoteStationTransport.FuelSwitch;current.PendingCommand.Kind=RebirthCamperRemoteStationCommandKind.FuelOff;current.PendingCommand.Job=current.PendingOperation;
        Receive(current,RebirthCamperRemoteStationCommandStatus.State.Rejected);Need(retired==1&&!applied&&retiredAction==RebirthCamperRemoteStationTransport.FuelSwitch,"Typed fuel rejection lost its purpose");
        current=Original();current.CommandPending=false;var fuelState=new RebirthCamperStationState{BoundVehicle=current.Vehicle};SetBurning(fuelState,true);
        Need(sent==1&&sentAction==RebirthCamperRemoteStationTransport.FuelSwitch&&sentCommand.IsFuel&&sentCommand.Operation==current.PendingOperation&&sentCommand.Job==current.PendingOperation&&current.PendingBurning&&RebirthCamperRemoteCommandOutbox.Recorded==1,"Fuel send bypassed original metadata");
        SetBurning(fuelState,false);Need(sent==1,"Pending fuel admitted opposite command");
        current=Original();current.CommandPending=false;RebirthCamperRemoteCommandOutbox.Record=false;SetBurning(new RebirthCamperStationState{BoundVehicle=current.Vehicle},true);Need(sent==0&&current.CommandPending&&current.CommandUncertain,"Failed fuel outbox allowed send or retired intent");
        current=Original();current.CommandPending=false;RebirthCamperRemoteCommandOutbox.First=false;SetBurning(new RebirthCamperStationState{BoundVehicle=current.Vehicle},false);Need(sent==0&&current.CommandPending&&current.CommandUncertain,"Existing fuel intent resent");
        current=Original();current.CommandPending=false;sendThrows=true;SetBurning(new RebirthCamperStationState{BoundVehicle=current.Vehicle},true);Need(sent==1&&current.CommandPending&&current.CommandUncertain&&current.PendingCommand.Operation==current.PendingOperation,"Ambiguous fuel send discarded original");
    }
}

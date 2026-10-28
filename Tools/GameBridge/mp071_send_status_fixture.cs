// Appendix to actual dispatch fixture. AUTHORED_NOT_RUN.
class EntityVehicle{internal World world;}
class Storage{internal Guid VehicleIdentity=Guid.NewGuid();}
class RebirthCamperTransferSnapshot{internal Storage Storage=new Storage();}
static class Log{internal static void Warning(string text){}}
static class RebirthCamperRemoteCommandOutbox
{
 internal static bool Attempted,Mirror=true;internal static int Sends;
 internal static bool TryRecordFirstAttempt(EntityPlayerLocal p,RebirthCamperStationAddress a,RebirthCamperRemoteStationCommand q,out bool first){first=!Attempted;Attempted=true;return true;}
 internal static bool TryMirrorTerminal(EntityPlayerLocal p,RebirthCamperStationAddress a,RebirthCamperRemoteStationCommand q,RebirthCamperRemoteStationCommandStatus.State state)=>Mirror;
}
static class SendStatusFixture
{
 class Session{internal EntityPlayerLocal Player;internal EntityVehicle Vehicle;internal Guid Token,Installation=Guid.NewGuid(),PendingOperation;internal RebirthCamperTransferSnapshot Snapshot=new RebirthCamperTransferSnapshot();internal RebirthCamperRemoteStationCommand PendingCommand;internal bool CommandPending=true,CommandUncertain,PendingBurning;internal byte PendingAction=2;}
 static readonly Dictionary<int,Session> sessions=new Dictionary<int,Session>();static bool before;static int retired;
 static bool Current(Session s)=>ReferenceEquals(s.Player.world.GetPrimaryPlayer(),s.Player);
 static void Need(bool value,string text){if(!value)throw new Exception(text);}
 static void Send(Session s,byte action,RebirthCamperRemoteStationCommand q,bool burning){RebirthCamperRemoteCommandOutbox.Sends++;if(before)throw new InvalidOperationException("before acceptance");RebirthCamperRemoteStationTransport.Execute(s.Player,s.Token,q);throw new InvalidOperationException("after acceptance");}
 static void ReceiveCore(EntityVehicle v,Guid token,RebirthCamperTransferSnapshot snapshot,byte action,bool accepted){retired++;sessions[1].CommandPending=false;sessions[1].CommandUncertain=false;}
 // ACTUAL_CLIENT_SEND
 // ACTUAL_CLIENT_STATUS
 internal static void Run(EntityPlayerLocal p,RebirthCamperStationAddress a,Guid token,object lease,RebirthCamperRemoteStationCommand q,bool failBefore)
 {
  before=failBefore;retired=0;RebirthCamperRemoteCommandOutbox.Attempted=false;RebirthCamperRemoteCommandOutbox.Sends=0;RebirthCamperRemoteCommandOutbox.Mirror=true;
  var s=new Session{Player=p,Vehicle=new EntityVehicle{world=p.world},Token=token,PendingOperation=q.Operation,PendingCommand=q,PendingAction=q.IsFuel?(byte)4:(byte)2};sessions.Clear();sessions.Add(p.entityId,s);
  SendCommand(s);Need(s.CommandPending&&s.CommandUncertain&&RebirthCamperRemoteCommandOutbox.Sends==1,"send fault retains exact original");
  SendCommand(s);Need(RebirthCamperRemoteCommandOutbox.Sends==1,"attempt marker prevents retransmission");
  if(before)Need(RebirthCamperRemoteStationCommandStatusStore.FixtureRefuse(p,a,"creation",lease,q),"new protocol no-entry metadata reservation");
  var state=RebirthCamperRemoteStationCommandStatus.ReadStatus(p,a,"creation",lease,q,out var observation);
  Need(observation!=null&&observation.AuthorizesCommandRetirement&&state==(before?RebirthCamperRemoteStationCommandStatus.State.Rejected:RebirthCamperRemoteStationCommandStatus.State.Applied),"actual authority qualifies exact terminal");
  RebirthCamperRemoteCommandOutbox.Mirror=false;ReceiveOriginalStatus(s.Vehicle,s.Token,s.Snapshot.Storage.VehicleIdentity,s.Installation,q.Operation,q.Job,q.Kind,state,s.Snapshot);Need(retired==0&&s.CommandPending,"failed owner terminal mirror cannot retire");
  RebirthCamperRemoteCommandOutbox.Mirror=true;ReceiveOriginalStatus(s.Vehicle,s.Token,s.Snapshot.Storage.VehicleIdentity,s.Installation,q.Operation,q.Job,q.Kind,state,s.Snapshot);Need(retired==1&&!s.CommandPending&&!s.CommandUncertain,"exact typed terminal settles presentation");
  Need(Probe.Effects==(before?0:1),"financial effects exactly match entry");RebirthCamperRemoteStationTransport.Execute(p,token,q);Need(Probe.Effects==(before?0:1),"delayed original remains status only");
  ReceiveOriginalStatus(s.Vehicle,s.Token,s.Snapshot.Storage.VehicleIdentity,s.Installation,q.Operation,q.Job,q.Kind,state,s.Snapshot);Need(retired==1,"late terminal cannot settle twice");
 }
}

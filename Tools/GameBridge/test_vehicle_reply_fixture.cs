using System;
enum RebirthVehicleAssemblyCarrier { VehicleEntity }
enum RebirthVehicleRequestOutcome { Applied, Indeterminate }
struct Vector3i { public int x; }
sealed class RebirthVehicleAssembly { public int Revision; }
sealed class NetPackageRebirthVehicleAssemblySnapshot {
 public bool ThrowSetup; public int Setups; public Guid Id; public int Entity; public RebirthVehicleAssembly Snapshot; public string Message; public RebirthVehicleRequestOutcome Outcome;
 public NetPackageRebirthVehicleAssemblySnapshot Setup(Guid id, RebirthVehicleAssemblyCarrier carrier, Vector3i position, int entity, RebirthVehicleAssembly snapshot, string message, RebirthVehicleRequestOutcome outcome) {
  Setups++; if(ThrowSetup)throw new InvalidOperationException("serialization failure"); Id=id;Entity=entity;Snapshot=snapshot;Message=message;Outcome=outcome; return this;
 }
}
sealed class ConnectionManager {
 public bool ThrowSend; public int Sends; public int Owner;
 public void SendPackage(NetPackageRebirthVehicleAssemblySnapshot reply, int _attachedToEntityId) { Sends++;Owner=_attachedToEntityId;if(ThrowSend)throw new InvalidOperationException("send failure"); }
}
sealed class Actual {
 int playerId=42;
 // SOURCE
 public void Deliver(ConnectionManager c, NetPackageRebirthVehicleAssemblySnapshot r, Guid id, RebirthVehicleAssembly snapshot) { TrySendReply(c,r,id,RebirthVehicleAssemblyCarrier.VehicleEntity,new Vector3i{ x=3 },7,snapshot,"saved success",RebirthVehicleRequestOutcome.Applied); }
}
static class Test {
 static void Check(bool value,string message){if(!value)throw new Exception(message);}
 static void Main(){
  var actual=new Actual();var id=Guid.NewGuid();var snapshot=new RebirthVehicleAssembly{Revision=9};
  var c=new ConnectionManager();var r=new NetPackageRebirthVehicleAssemblySnapshot{ThrowSetup=true};
  actual.Deliver(c,r,id,snapshot);Check(r.Setups==1&&c.Sends==0&&snapshot.Revision==9,"serialization refusal");
  r=new NetPackageRebirthVehicleAssemblySnapshot();c.ThrowSend=true;
  actual.Deliver(c,r,id,snapshot);Check(c.Sends==1&&r.Outcome==RebirthVehicleRequestOutcome.Applied&&snapshot.Revision==9,"send preserves outcome");
  c.ThrowSend=false;actual.Deliver(c,r,id,snapshot);
  Check(c.Sends==2&&c.Owner==42&&r.Id==id&&r.Entity==7&&Object.ReferenceEquals(r.Snapshot,snapshot)&&r.Message=="saved success"&&r.Outcome==RebirthVehicleRequestOutcome.Applied,"retry identity and payload");
  Console.WriteLine("PASS actual reply helper: serialization failure, transport failure, unchanged outcome, retry payload and authenticated destination. Native transport/serializer doubled; journal not exercised.");
 }
}
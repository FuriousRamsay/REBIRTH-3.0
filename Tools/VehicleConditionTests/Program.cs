using System.Reflection;
class Program
{
 static int checks;static void Check(bool ok,string label){if(!ok)throw new Exception(label);checks++;}
 static RebirthInstalledVehiclePart Part(string name,float use=0,string identity="a")=>new(){ItemName=name,UseTimes=use,SerializedItemValue=name+"|100|"+use+"|"+identity};
 static EntityVehicle Car(){var e=new EntityVehicle();e.Assembly.Parts["Engine"]=Part("engine");e.Assembly.Parts["Wheel1"]=Part("wheel");e.Assembly.Parts["HeadlightLeft"]=Part("light");e.Assembly.Performance.CanOperate=RebirthVehicleConditionIntegration.IsOperational(e.Assembly);return e;}
 static void Main()
 {
  var e=Car();Check(!RebirthVehicleConditionIntegration.Blocked(e),"Complete operational family accepted");
  e.Health=0;Check(RebirthVehicleConditionIntegration.Blocked(e),"Dead recognized vehicle cannot operate despite complete components");e.Health=100;
  var damageState=new object[]{e,null};typeof(RebirthVehicleConditionDamage).GetMethod("Prefix",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,damageState);
  Check((bool)damageState[1],"Damage hook admits a previously living native vehicle");
  e.Health=0;damageState=new object[]{e,null};typeof(RebirthVehicleConditionDamage).GetMethod("Prefix",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,damageState);
  typeof(RebirthVehicleConditionDamage).GetMethod("Postfix",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{e,100,(bool)damageState[1]});
  Check(!(bool)damageState[1]&&e.Assembly.Revision==0,"Native already-dead ApplyDamage return never wears installed parts");e.Health=100;
  e.Assembly.Parts.Remove("HeadlightLeft");Check(RebirthVehicleConditionIntegration.IsOperational(e.Assembly),"Headlights remain required for repair but optional for operation");
  Check(RebirthVehicleDefinitionRegistry.Family.Slots[2].Required,"Authored repair requirement unchanged");
  foreach(string slot in new[]{"Engine","Wheel1"}){var before=e.Assembly.Parts[slot];e.Assembly.Parts.Remove(slot);Check(!RebirthVehicleConditionIntegration.IsOperational(e.Assembly),"Missing mechanical slot blocks operation");e.Assembly.Parts[slot]=before;}
  e.Assembly.Parts["Engine"]=Part("engine",100);Check(!RebirthVehicleConditionIntegration.IsOperational(e.Assembly),"Exhausted native serialized condition blocks operation");
  e.Assembly.Parts["Engine"]=Part("wrong");Check(!RebirthVehicleConditionIntegration.IsOperational(e.Assembly),"Wrong installed mechanical item rejected");
  Check(!RebirthVehicleConditionIntegration.IsPartUsable(new(){ItemName="engine",SerializedItemValue="corrupt"}),"Corrupt native item fails closed");
  e=Car();int decoded=RebirthVehicleInventoryTransactions.Decodes;for(int i=0;i<20;i++)RebirthVehicleConditionIntegration.Blocked(e);Check(decoded==RebirthVehicleInventoryTransactions.Decodes,"Steady operation gate does not decode parts each physics frame");
  e.Recognized=false;Check(!RebirthVehicleConditionIntegration.Blocked(e),"Unregistered native vehicle is unaffected");e.Recognized=true;e.Assembly=null;Check(RebirthVehicleConditionIntegration.Blocked(e),"Recognized metadata wait fails closed");
  Check(!RebirthVehicleConditionIntegration.DamageEligible(9,false)&&RebirthVehicleConditionIntegration.DamageEligible(10,false),"Legacy ordinary-night integer damage threshold");
  Check(RebirthVehicleConditionIntegration.DamageEligible(1,true)&&!RebirthVehicleConditionIntegration.DamageEligible(0,true),"Legacy active-horde damage threshold");
  e=Car();RebirthVehicleConditionIntegration.Damage(e,10);Check(e.Assembly.Parts["Engine"].UseTimes==3&&e.Assembly.Revision==1,"Authority publishes selected native part wear through one assembly commit");
  Check(RebirthVehicleInventoryTransactions.CreateItemStack(e.Assembly.Parts["Engine"]).itemValue.UseTimes==3,"Serialized native condition matches scalar snapshot");
  e=Car();e.world.Remote=true;RebirthVehicleConditionIntegration.Damage(e,10);Check(e.Assembly.Revision==0,"Remote clients never author wear");
  e=Car();e.world.Random.Choice=1;RebirthVehicleConditionIntegration.Damage(e,10);Check(e.Assembly.Revision==0,"Unselected random components remain unchanged");
  e=Car();e.Assembly.Parts["Engine"]=Part("engine",99);RebirthVehicleConditionIntegration.Damage(e,10);Check(e.Assembly.Parts["Engine"].UseTimes==100&&!e.Assembly.Performance.CanOperate,"Wear caps at native maximum and recompiles operational snapshot");
  e=Car();e.Assembly.PendingInventoryTransferId=Guid.NewGuid();RebirthVehicleConditionIntegration.Damage(e,10);RebirthVehicleConditionIntegration.Damage(e,10);Check(e.Assembly.Revision==0&&e.Assembly.Parts["Engine"].UseTimes==0,"Reserved custody row is not mutated by damage");
  Check(e.Syncs==0&&e.vehicle.Item.Rows.ContainsKey(RebirthVehicleConditionIntegration.DeferredWearKey),"Reserved native metadata persists without redundant client sync");
  e.Assembly.Parts["Engine"]=Part("engine",0,"replacement");e.Assembly.PendingInventoryTransferId=Guid.Empty;RebirthVehicleConditionIntegration.Flush(e);
  Check(e.Assembly.Parts["Engine"].UseTimes==0,"Deferred wear never damages replacement or moved component");
  Check(e.Assembly.Parts["Wheel1"].UseTimes==6&&e.Assembly.Revision==1,"Same exact retained component receives accumulated wear once");
  Check(e.Syncs==2,"Resolved wear sends only assembly commit and queue cleanup syncs");
  RebirthVehicleConditionIntegration.Flush(e);Check(e.Assembly.Revision==1,"Deferred wear is not replayed");
  Check(e.Syncs==2,"Clean queue does not repeat native sync on later ticks");
  e=Car();e.Assembly.PendingInventoryTransferId=Guid.NewGuid();RebirthVehicleConditionIntegration.Damage(e,10);
  string saved=e.vehicle.Item.Rows[RebirthVehicleConditionIntegration.DeferredWearKey];
  Check(saved.Length<16384&&!saved.Contains("engine|100"),"Bounded native queue stores hashes instead of complete native item payloads");
  var reloaded=new EntityVehicle{Assembly=e.Assembly.DeepClone()};reloaded.vehicle.Item.Rows[RebirthVehicleConditionIntegration.DeferredWearKey]=saved;
  RebirthVehicleConditionIntegration.Flush(reloaded);Check(reloaded.Assembly.Revision==0,"Reloaded reservation retains saved wear without changing reserved assembly");
  reloaded.Assembly.PendingInventoryTransferId=Guid.Empty;RebirthVehicleConditionIntegration.Flush(reloaded);
  Check(reloaded.Assembly.Parts["Engine"].UseTimes==3&&reloaded.vehicle.Item.Rows[RebirthVehicleConditionIntegration.DeferredWearKey]=="","Native saved queue applies after reload and clears only after committed wear");
  var crashReplay=new EntityVehicle{Assembly=reloaded.Assembly.DeepClone()};crashReplay.vehicle.Item.Rows[RebirthVehicleConditionIntegration.DeferredWearKey]=saved;
  RebirthVehicleConditionIntegration.Flush(crashReplay);Check(crashReplay.Assembly.Revision==1&&crashReplay.Assembly.Parts["Engine"].UseTimes==3,"Crash after commit before queue clear never applies wear twice");
  Check(crashReplay.vehicle.Item.Rows[RebirthVehicleConditionIntegration.DeferredWearKey]=="","Crash replay consumes already-applied before-image hashes safely");
  var clearFailure=Car();clearFailure.Assembly.PendingInventoryTransferId=Guid.NewGuid();RebirthVehicleConditionIntegration.Damage(clearFailure,10);clearFailure.Assembly.PendingInventoryTransferId=Guid.Empty;clearFailure.vehicle.Item.RefuseClears=true;
  bool clearRejected=false;try{RebirthVehicleConditionIntegration.Flush(clearFailure);}catch(InvalidOperationException){clearRejected=true;}
  Check(clearRejected&&clearFailure.Assembly.Parts["Engine"].UseTimes==3&&clearFailure.Assembly.Revision==1,"Commit succeeds independently of a failed verified queue clear");
  clearFailure.vehicle.Item.RefuseClears=false;RebirthVehicleConditionIntegration.Flush(clearFailure);Check(clearFailure.Assembly.Revision==1&&clearFailure.Assembly.Parts["Engine"].UseTimes==3,"Queue-clear retry never wears the committed item twice");
  var foreign=Car();foreign.vehicle.Item.Rows[RebirthVehicleConditionIntegration.DeferredWearKey]=saved;Check(RebirthVehicleConditionIntegration.Blocked(foreign)&&foreign.Assembly.Revision==0,"Foreign assembly queue fails closed without damaging current components");
  foreach(string bad in new[]{"not-base64",new string('A',16385),Queue(2,1,"Engine",new string('A',64),3),Queue(1,65,"Engine",new string('A',64),3),Queue(1,2,"Engine",new string('A',64),3),Queue(1,1,new string('E',65),new string('A',64),3),Queue(1,1,"Engine","BAD",3),Queue(1,1,"Engine",new string('A',64),float.NaN),Queue(1,1,"Engine",new string('A',64),float.PositiveInfinity),Queue(1,1,"Engine",new string('A',64),-1)})
  {
   var corrupt=Car();corrupt.vehicle.Item.Rows[RebirthVehicleConditionIntegration.DeferredWearKey]=bad;int logs=Log.Errors;
   Check(RebirthVehicleConditionIntegration.Blocked(corrupt),"Malformed saved queue fails closed");RebirthVehicleConditionIntegration.Blocked(corrupt);
   Check(corrupt.vehicle.Item.Rows[RebirthVehicleConditionIntegration.DeferredWearKey]==bad&&Log.Errors==logs+1,"Malformed queue retained and logged once rather than every frame");
  }
  var refused=Car();refused.Assembly.PendingInventoryTransferId=Guid.NewGuid();refused.vehicle.Item.RefuseWrites=true;
  bool rejected=false;try{RebirthVehicleConditionIntegration.Damage(refused,10);}catch(InvalidOperationException){rejected=true;}
  Check(rejected&&refused.Assembly.Revision==0,"Unverified owning native metadata setter cannot commit part wear");
  e=Car();e.Assembly.Parts.Remove("Engine");e.Assembly.Performance.CanOperate=false;var input=e.movementInput;
  var args=new object[]{e};typeof(RebirthVehicleConditionPhysics).GetMethod("Prefix",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,args);
  Check(ReferenceEquals(e.movementInput,input)&&e.motorTorque==0&&input.moveForward==0,"Required-parts gate keeps native input while refusing residual propulsion");
  Check(input.jump&&!input.down&&!input.running&&input.moveStrafe==0,"Native handbrake remains available while hop/turbo/steering commands are refused");
  // Mirror the inspected native branch: moveForward zero skips motor and
  // unstick branches; jump selects native wheelBrakes=2 without custom forces.
  float nativeMotor=input.moveForward==0?0:50;float nativeBrake=input.jump?2:0;
  Check(nativeMotor==0&&nativeBrake==2,"Sanitized input selects native handbrake without engine-off fallback torque");
  typeof(RebirthVehicleConditionPhysics).GetMethod("Postfix",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,args);Check(ReferenceEquals(e.movementInput,input)&&input.jump,"Physics postfix preserves exact native input object and brake button");
  Console.WriteLine("PASS "+checks+" linked production condition/authority/custody checks; native gameplay and persistence integration require separate qualification.");
 }
 static string Queue(byte version,int count,string slot,string hash,float amount)
 {using(var s=new MemoryStream())using(var w=new BinaryWriter(s)){w.Write(version);w.Write(Guid.NewGuid().ToByteArray());w.Write(count);for(int i=0;i<(count==2?2:1);i++){w.Write(slot);w.Write(hash);w.Write(amount);}w.Flush();return Convert.ToBase64String(s.ToArray());}}
}
namespace UnityEngine{public static class Mathf{public static float Min(float a,float b)=>Math.Min(a,b);}}
namespace HarmonyLib{[AttributeUsage(AttributeTargets.Class,AllowMultiple=true)]public class HarmonyPatch:Attribute{public HarmonyPatch(Type t,string n,params Type[] a){}}}
class MovementInput{public float moveForward=1,moveStrafe=1;public bool running=true,jump=true,down=true;}
class EntityPlayerLocal{}
class EntityVehicle{public int Health=100,Syncs;public World world=new();public Vehicle vehicle;public bool Recognized=true;public RebirthVehicleAssembly Assembly=new();public MovementInput movementInput=new();public float wheelMotor=1,wheelDir=1,motorTorque=1;public EntityVehicle(){vehicle=new(){entity=this};}public void SendSyncData(ushort flags){Syncs++;}public void MoveByAttachedEntity(EntityPlayerLocal p){}public void PhysicsFixedUpdate(){}public void ApplyDamage(int damage){}public void Update(){}}
class Vehicle{public EntityVehicle entity;public NativeMetadata Item=new();public NativeMetadata GetUpdatedItemValue()=>Item;public bool CurrentIsAccel;public float CurrentMotorTorquePercent,CurrentSteeringPercent;}
class NativeMetadata{public bool RefuseWrites,RefuseClears;public Dictionary<string,string> Rows=new();public bool TryGetMetadata(string key,out string text)=>Rows.TryGetValue(key,out text);public void SetMetadata(string key,string text){if(!RefuseWrites&&!(RefuseClears&&text==""))Rows[key]=text;}}
class VPEngine{public Vehicle vehicle;public void Update(float dt){}public void stopEngine(){}}
class World{public bool Remote;public bool IsRemote()=>Remote;public Director aiDirector=new();public RandomSource Random=new();public RandomSource GetGameRandom()=>Random;}
class Director{public Blood BloodMoonComponent=new();}class Blood{public bool BloodMoonActive;}
class RandomSource{public int Choice=2;public int RandomRange(int min,int max)=>Choice;}
class RebirthVehicleSlotDefinition{public string SlotId,ItemName;public bool Required=true;}
class Family{public RebirthVehicleSlotDefinition[] Slots={new(){SlotId="Engine",ItemName="engine"},new(){SlotId="Wheel1",ItemName="wheel"},new(){SlotId="HeadlightLeft",ItemName="light"}};}
static class RebirthVehicleDefinitionRegistry{public static Family Family=new();public static Family GetFamily(string id)=>id=="car"?Family:null;}
class RebirthInstalledVehiclePart{public string ItemName,SerializedItemValue;public float UseTimes;}
class Performance{public bool CanOperate;}
class RebirthVehicleAssembly{public Guid AssemblyId=Guid.NewGuid(),PendingInventoryTransferId;public string FamilyId="car";public long Revision;public Performance Performance=new();public Dictionary<string,RebirthInstalledVehiclePart> Parts=new();public RebirthVehicleAssembly DeepClone(){var a=new RebirthVehicleAssembly{AssemblyId=AssemblyId,FamilyId=FamilyId,Revision=Revision,PendingInventoryTransferId=PendingInventoryTransferId};foreach(var p in Parts)a.Parts[p.Key]=new(){ItemName=p.Value.ItemName,UseTimes=p.Value.UseTimes,SerializedItemValue=p.Value.SerializedItemValue};return a;}}
static class RebirthVehicleEntityResolver{public static bool Resolve(EntityVehicle e,out RebirthVehicleAssembly a){a=e.Assembly;return e.Recognized;}}
static class RebirthVehicleAssemblyIndex{public static bool TryGetEntity(EntityVehicle e,out RebirthVehicleAssembly a){a=e.Assembly;return a!=null;}public static void CommitEntity(EntityVehicle e,RebirthVehicleAssembly a){a.Revision++;a.Performance.CanOperate=RebirthVehicleConditionIntegration.IsOperational(a);e.Assembly=a;e.SendSyncData(4);}}
class NativeItem{public string Name,Identity;public float MaxUseTimes,UseTimes;}
class ItemStack{public NativeItem itemValue;public bool IsEmpty()=>itemValue==null;}
static class RebirthVehicleInventoryTransactions{public static int Decodes;public static ItemStack CreateItemStack(RebirthInstalledVehiclePart p){Decodes++;var s=p.SerializedItemValue.Split('|');return new(){itemValue=new(){Name=s[0],MaxUseTimes=float.Parse(s[1]),UseTimes=float.Parse(s[2]),Identity=s[3]}};}}
static class RebirthNativeItemCodec{public static string Encode(NativeItem n)=>n.Name+"|"+n.MaxUseTimes+"|"+n.UseTimes+"|"+n.Identity;}
static class Log{public static int Errors;public static void Error(string s){Errors++;}}

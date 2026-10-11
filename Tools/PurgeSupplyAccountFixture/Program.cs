using System;
class Log {public static void Warning(string s){}}
class Program
{
 static int checks;static void Check(bool value,string text){if(!value)throw new Exception(text);checks++;Console.WriteLine("PASS "+text);}
 static void Main()
 {
  var key=new string('a',64);var rules=new RebirthPurgeSupplyPolicy.Rules(75,5,150,100);var account=new RebirthPurgeSupplyAccount(key);
  Check(account.TryEarn(74,rules,out account)&&account.EarnedDrops==0,"below quota retains unspent credits");
  Check(account.TryEarn(75,rules,out account)&&account.EarnedDrops==1&&account.SpentCredits==75,"first quota earns exactly one entitlement");
  Check(account.TryEarn(155,rules,out account)&&account.EarnedDrops==2&&account.SpentCredits==155,"second quota uses historical increased target");
  Check(account.TryEarn(155,rules,out var duplicate)&&duplicate.EarnedDrops==2,"repeated source total cannot earn twice");
  Check(!account.TryEarn(154,rules,out _),"regressed source total refused");
  Check(account.TryReserve(out account)&&account.InFlight==1&&account.DeliveredDrops==0,"first flight enters custody before delivery");
  var world=Guid.NewGuid();var token=account.Token(world);
  Check(!account.TryReserve(out _),"uncertain flight cannot be re-reserved");
  Check(RebirthPurgeSupplyAccount.Read(account.Write()).Token(world)==token,"cold account retains exact original delivery token");
  Check(account.Token(Guid.NewGuid())!=token,"delivery token binds saved world");
  var another=new RebirthPurgeSupplyAccount(new string('b',64),155,155,2,0,1);Check(another.Token(world)!=token,"delivery token binds stable player");
  Check(!account.TryComplete(world,Guid.NewGuid(),out _),"foreign completion cannot consume entitlement");
  Check(account.TryComplete(world,token,out var delivered)&&delivered.DeliveredDrops==1&&delivered.InFlight==0&&delivered.IsSuccessorOf(account),"exact positive receipt completes one original entitlement");
  Check(!delivered.TryComplete(world,token,out _),"duplicate native completion cannot consume second drop");
  Check(delivered.TryReserve(out var second)&&second.InFlight==2&&second.Token(world)!=token,"next delivery gets distinct stable sequence");
  Check(!new RebirthPurgeSupplyAccount(key,155,155,2).IsSuccessorOf(account),"lost flight cannot be silently reset");
  Check(!new RebirthPurgeSupplyAccount(key,155,155,2,2).IsSuccessorOf(delivered),"undocumented delivery cannot leap over original sequence");
  var big=new RebirthPurgeSupplyAccount(key);Check(big.TryEarn(100000,rules,out big)&&big.EarnedDrops==128,"earning loop is bounded to128 per publication");
  Check(big.TryEarn(100000,rules,out var more)&&more.EarnedDrops>128&&more.ObservedCredits==100000,"bounded continuation retains full original earned source");
  var destination=new RebirthPurgeSupplyDeliveryPlan(14,61,-993);
  Check(delivered.TryReserve(destination,out var prepared)&&prepared.Delivery.Phase==RebirthPurgeSupplyDeliveryPhase.Prepared,"new original destination is durable before native flight");
  var coldPrepared=RebirthPurgeSupplyAccount.Read(prepared.Write());
  Check(coldPrepared.Delivery.X==14&&coldPrepared.Delivery.Y==61&&coldPrepared.Delivery.Z==-993&&coldPrepared.Token(world)==prepared.Token(world),"cold prepared destination and token remain exact");
  Check(!coldPrepared.TryStart(world,Guid.NewGuid(),out _),"foreign start cannot authorize native flight");
  Check(coldPrepared.TryStart(world,coldPrepared.Token(world),out var started)&&started.IsSuccessorOf(coldPrepared),"original prepared flight advances to durable started custody");
  Check(!started.TryStart(world,started.Token(world),out _),"started original cannot authorize a second native launch");
  Check(!coldPrepared.IsSuccessorOf(started),"started custody cannot regress to prepared");
  Check(!new RebirthPurgeSupplyAccount(key,155,155,2,1,2,new RebirthPurgeSupplyDeliveryPlan(15,61,-993,RebirthPurgeSupplyDeliveryPhase.Started)).IsSuccessorOf(started),"pending destination cannot move after publication");
  Check(started.TryEarn(240,rules,out var earningStarted)&&earningStarted.Delivery.Phase==RebirthPurgeSupplyDeliveryPhase.Started&&earningStarted.Delivery.X==14,"additional rewards retain outstanding started custody");
  Check(RebirthPurgeSupplyAccount.Read(account.Write()).Delivery==null&&!account.TryStart(world,account.Token(world),out _),"older pending flight remains uncertain instead of becoming launchable");
  Check(!new RebirthPurgeSupplyAccount(key,155,155,2,0,1,destination).IsSuccessorOf(account),"older uncertain flight cannot acquire a fabricated prepared destination");
  Check(started.TryBeginCrate(world,started.Token(world),out var crateStarted)&&crateStarted.TryComplete(world,crateStarted.Token(world),out var completedPlan)&&completedPlan.Delivery==null&&completedPlan.IsSuccessorOf(started),"positive original completion retires destination custody");
  Check(started.Delivery.CanResumeFlight&&!coldPrepared.Delivery.CanResumeFlight,"only current-schema pre-crate Started flight may resume");
  Check(!started.TryComplete(world,started.Token(world),out _),"pre-crate flight cannot manufacture completed delivery");
  Check(!started.TryBeginCrate(world,Guid.NewGuid(),out _),"foreign token cannot admit original crate creation");
  Check(crateStarted.Delivery.Phase==RebirthPurgeSupplyDeliveryPhase.CrateStarted&&crateStarted.IsSuccessorOf(started),"crate attempt records distinct durable original custody");
  Check(!crateStarted.Delivery.CanResumeFlight&&!crateStarted.TryBeginCrate(world,crateStarted.Token(world),out _),"crate-started uncertainty cannot re-admit crate or plane recovery");
  Check(!started.IsSuccessorOf(crateStarted),"crate custody cannot regress to retryable flight");
  var coldCrate=RebirthPurgeSupplyAccount.Read(crateStarted.Write());
  Check(coldCrate.Delivery.Version==2&&coldCrate.Delivery.Phase==RebirthPurgeSupplyDeliveryPhase.CrateStarted&&!coldCrate.Delivery.CanResumeFlight,"cold new schema preserves nonretryable crate attempt");
  var legacyPlan=started.Write();legacyPlan.Element("delivery").SetAttributeValue("version",1);
  var legacyStarted=RebirthPurgeSupplyAccount.Read(legacyPlan);
  Check(!legacyStarted.Delivery.CanResumeFlight&&legacyStarted.Token(world)==started.Token(world),"older started save stays uncertain with original token");
  Check(!started.IsSuccessorOf(legacyStarted),"schema promotion cannot invent proof that older flight lacked a crate");
  Check(RebirthPurgeSupplyAccount.Read(started.Write()).Delivery.CanResumeFlight,"cold version-two pre-crate Started has positive no-crate-admission evidence");
  var badPlan=prepared.Write();badPlan.Add(new System.Xml.Linq.XElement(badPlan.Element("delivery")));
  bool rejected=false;try{RebirthPurgeSupplyAccount.Read(badPlan);}catch(FormatException){rejected=true;}
  Check(rejected,"duplicate saved destination refused");
  badPlan=prepared.Write();badPlan.Element("delivery").SetAttributeValue("phase",99);
  rejected=false;try{RebirthPurgeSupplyAccount.Read(badPlan);}catch(ArgumentException){rejected=true;}
  Check(rejected,"unknown saved delivery phase refused");
  var stamp=new RebirthPurgeSupplyCrateStamp(world,key,second.InFlight,second.Token(world));
  using(var stream=new System.IO.MemoryStream())
  {
   using(var writer=new System.IO.BinaryWriter(stream,System.Text.Encoding.UTF8,true)){writer.Write(123456);stamp.Write(writer);}
   Check(stream.Length==4+RebirthPurgeSupplyCrateStamp.EncodedLength,"saved stamp has bounded fixed-size per-entity trailer");
   stream.Position=4;using(var reader=new System.IO.BinaryReader(stream,System.Text.Encoding.UTF8,true))
    Check(RebirthPurgeSupplyCrateStamp.TryRead(reader,out var round)&&round.Player==key&&round.Sequence==2&&round.Token==second.Token(world),"binary cold stamp recovers stable world owner and exact delivery");
   var bytes=stream.ToArray();bytes[5]=99;
   using(var malformed=new System.IO.MemoryStream(bytes)){malformed.Position=4;using(var reader=new System.IO.BinaryReader(malformed))
    Check(!RebirthPurgeSupplyCrateStamp.TryRead(reader,out _)&&malformed.Position==4,"unknown stamp version cannot establish completion and does not consume native data");}
   bytes=stream.ToArray();bytes[bytes.Length-1]^=1;
   using(var malformed=new System.IO.MemoryStream(bytes)){malformed.Position=4;using(var reader=new System.IO.BinaryReader(malformed))
    Check(!RebirthPurgeSupplyCrateStamp.TryRead(reader,out _)&&malformed.Position==4,"foreign delivery token cannot forge persisted stamp");}
   using(var nativeOnly=new System.IO.MemoryStream(new byte[]{1,2,3,4})){nativeOnly.Position=4;using(var reader=new System.IO.BinaryReader(nativeOnly))
    Check(!RebirthPurgeSupplyCrateStamp.TryRead(reader,out _)&&nativeOnly.Position==4,"legacy native-only entity blob needs no save migration");}
  }
  Check(prepared.TryLaunched(world,prepared.Token(world),out var accepted)&&accepted.DeliveredDrops==prepared.InFlight&&accepted.InFlight==0&&accepted.IsSuccessorOf(prepared),"accepted native flight consumes exactly one prepared entitlement");
  Check(started.TryLaunched(world,started.Token(world),out _),"legacy v2 pre-crate flight may resume through native launch");
  Check(!legacyStarted.TryLaunched(world,legacyStarted.Token(world),out _),"ambiguous v1 flight is not relaunched");
  Check(!crateStarted.TryLaunched(world,crateStarted.Token(world),out _),"legacy crate attempt is not relaunched");
  Check(!accepted.TryLaunched(world,prepared.Token(world),out _),"repeated launch accounting cannot double consume");
  Check(!prepared.TryLaunched(world,Guid.NewGuid(),out _),"wrong launch identity cannot consume entitlement");
  Console.WriteLine("RESULT "+checks+" PASS; actual supply entitlement rules and custody; no native flight or delivery claim.");
 }
}
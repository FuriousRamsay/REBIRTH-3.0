using System;
using System.IO;
using System.Linq;
internal static class Program
{
 static int checks;static void Check(bool ok,string text){if(!ok)throw new Exception(text);checks++;Console.WriteLine("PASS "+text);}
 static void Main()
 {
  var scope=new object();var world=Guid.NewGuid();var poi=new RebirthPoiIdentity("house",1,2,3,0,10,10,10,"forest");var ledger=new RebirthPoiClearanceLedger(world,scope);
  Check(ledger.TryDiscover(scope,world,0,poi,out ledger),"discover original");
  var token=Guid.NewGuid();var player=new string('a',64);var actor=new RebirthPoiActorObservation(token,11,"zombieArlene",0,Guid.NewGuid(),10,contributor:player);
  var journal=new RebirthPoiPartialObservation(Guid.NewGuid(),0,1,new[]{new RebirthPoiVolumeObservation(7,new string('b',64),new[]{actor})});
  Check(ledger.TryObservePartial(scope,world,ledger.Revision,poi,0,journal,out ledger),"attributed native death observed");
  Check(ledger.Records[poi.Key].SupplyCredits==null,"death before complete clearance earns nothing");
  var proof=new RebirthPoiClearEvidence(Guid.NewGuid(),1,1,1,0,0,false,11);
  Check(ledger.TryClear(scope,world,ledger.Revision,poi,0,proof,out ledger)&&ledger.Records[poi.Key].SupplyCredits.Totals[player]==1,"clearance atomically earns one kill");
  var xml=RebirthPoiClearanceCodec.Write(ledger);
  Check(RebirthPoiClearanceCodec.TryRead(xml,world,scope,out ledger)&&ledger.Records[poi.Key].SupplyCredits.Totals[player]==1,"cold credit and terminal evidence reload");
  long revision=ledger.Revision;
  Check(ledger.TryClear(scope,world,revision,poi,0,proof,out ledger)&&ledger.Revision==revision&&ledger.Records[poi.Key].SupplyCredits.Totals[player]==1,"clear retry cannot duplicate credit");
  var reset=Guid.NewGuid();Check(ledger.TryBeginReset(scope,world,ledger.Revision,poi,0,reset,out ledger)&&ledger.Records[poi.Key].SupplyCredits.Totals[player]==1,"pending reset preserves earned total");
  Check(ledger.TryFinishReset(scope,world,ledger.Revision,poi,0,reset,RebirthPoiResetDisposition.Completed,out ledger)&&ledger.Records[poi.Key].SupplyCredits.Totals[player]==1&&ledger.Records[poi.Key].Observations==null,"completed reset preserves credits after forgetting actors");
  Check(RebirthPoiClearanceCodec.TryRead(RebirthPoiClearanceCodec.Write(ledger),world,scope,out ledger)&&ledger.Records[poi.Key].SupplyCredits.Totals[player]==1,"post-reset cold reload preserves cumulative credit");
  var nextActor=new RebirthPoiActorObservation(Guid.NewGuid(),11,"zombieArlene",0,Guid.NewGuid(),20,contributor:player);
  var nextJournal=new RebirthPoiPartialObservation(Guid.NewGuid(),1,1,new[]{new RebirthPoiVolumeObservation(7,new string('b',64),new[]{nextActor})});
  Check(ledger.TryObservePartial(scope,world,ledger.Revision,poi,1,nextJournal,out ledger)&&ledger.TryClear(scope,world,ledger.Revision,poi,1,new RebirthPoiClearEvidence(Guid.NewGuid(),1,1,1,0,0,false,21),out ledger)&&ledger.Records[poi.Key].SupplyCredits.Totals[player]==2,"fresh generation earns once despite reused runtime entity ID");
  var credits=ledger.Records[poi.Key].SupplyCredits;RebirthPoiSupplyCredits again;
  Check(RebirthPoiSupplyCredits.TryCredit(credits,nextJournal,out again)&&ReferenceEquals(credits,again),"retained room tokens do not earn twice");
  var doc=System.Xml.Linq.XDocument.Parse(RebirthPoiClearanceCodec.Write(ledger));var node=doc.Root.Element("poi").Element("supplyCredits");node.Add(new System.Xml.Linq.XElement(node.Element("player")));
  Check(!RebirthPoiClearanceCodec.TryRead(doc.ToString(),world,scope,out _),"duplicate player total refused");
  doc=System.Xml.Linq.XDocument.Parse(RebirthPoiClearanceCodec.Write(ledger));doc.Root.Element("poi").Element("supplyCredits").Element("credited").SetAttributeValue("token",Guid.NewGuid().ToString("N"));
  Check(!RebirthPoiClearanceCodec.TryRead(doc.ToString(),world,scope,out _),"credit token without original witnessed death refused");
  var twoDeaths=new RebirthPoiPartialObservation(Guid.NewGuid(),1,1,new[]{new RebirthPoiVolumeObservation(7,new string('b',64),new[]{nextActor,new RebirthPoiActorObservation(Guid.NewGuid(),12,"zombieArlene",0,Guid.NewGuid(),22,contributor:player)})});
  RebirthPoiSupplyCredits.TryCredit(null,twoDeaths,out var twoCredits);
  var undercount=twoCredits.Write();undercount.Element("player").SetAttributeValue("count",1);
  Check(!RebirthPoiSupplyCredits.Read(undercount).Matches(twoDeaths),"saved total cannot understate retained credited original deaths");
  Check(twoCredits.Matches(twoDeaths),"exact retained credited death total remains valid");
  var dir=Path.Combine(Path.GetTempPath(),"rebirth-supply-credit-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
  bool armed=false;var binding=new RebirthPoiWorldBinding(scope,world.ToString("N").ToUpperInvariant(),dir,()=>true);
  Check(RebirthPoiWorldStore.TryOpen(binding,out var store,stage=>{if(armed&&stage=="afterManifestCandidateWritten")throw new IOException("lost publication reply");})==RebirthPoiStoreResult.Published,"actual atomic store opens");
  Check(store.TryDiscover(store.Published,poi)==RebirthPoiStoreResult.Published&&store.TryObservePartial(store.Published,poi,0,journal)==RebirthPoiStoreResult.Published,"original actor custody stored");
  armed=true;var before=store.Published;
  Check(store.TryClear(before,poi,0,proof)==RebirthPoiStoreResult.Uncertain&&store.Published.Revision==before.Revision,"interrupted credit publication retains predecessor");
  armed=false;Check(store.TryRetryPending()==RebirthPoiStoreResult.Published&&store.Published.TryGet(poi,out var saved)&&saved.SupplyCredits.Totals[player]==1,"original candidate retry publishes exactly one credit");
  Check(RebirthPoiWorldStore.TryOpen(binding,out var cold)==RebirthPoiStoreResult.Published&&cold.Published.TryGet(poi,out saved)&&saved.SupplyCredits.Totals[player]==1,"actual cold file recovery preserves earned credit");
  var rules=new RebirthPurgeSupplyPolicy.Rules(75,5,150,100);bool accountFault=false;
  var accounts=new RebirthPurgeSupplyAccountStore(binding,stage=>{if(accountFault&&stage=="afterCandidate")throw new IOException("lost account reply");});
  Check(accounts.TryOpen(),"supply account store opens original world");
  var account=new RebirthPurgeSupplyAccount(player);account.TryEarn(155,rules,out account);
  accountFault=true;Check(!accounts.TryCommit(accounts.Published,account)&&accounts.Published.Count==0,"interrupted account receipt keeps visible predecessor");
  accountFault=false;var reopened=new RebirthPurgeSupplyAccountStore(binding);
  Check(reopened.TryOpen()&&reopened.Published[player].EarnedDrops==2,"cold original account candidate reconciles once");
  var earned=reopened.Published[player];earned.TryReserve(out var reserved);
  Check(reopened.TryCommit(reopened.Published,reserved)&&reopened.Published[player].InFlight==1,"original native delivery reserved durably");
  var reload=new RebirthPurgeSupplyAccountStore(binding);
  Check(reload.TryOpen()&&reload.Published[player].Token(world)==reserved.Token(world),"cold pending flight retains exact world player sequence token");
  Check(!reload.TryCommit(reload.Published,new RebirthPurgeSupplyAccount(player,155,155,2)),"store rejects losing uncertain flight");
  reserved.TryComplete(world,reserved.Token(world),out var delivered);
  Check(reload.TryCommit(reload.Published,delivered),"positive original delivery receipt commits");
  var done=new RebirthPurgeSupplyAccountStore(binding);Check(done.TryOpen()&&done.Published[player].DeliveredDrops==1&&done.Published[player].InFlight==0,"cold completion consumes only original entitlement");
  Check(!done.TryCommit(reopened.Published,delivered),"old account publication witness refused");
  var flightStore=new RebirthPurgeSupplyAccountStore(binding,stage=>{if(accountFault&&stage=="afterCandidate")throw new IOException("lost preparation reply");});
  Check(flightStore.TryOpen(),"delivery transition recovery opens original account store");
  var other=new RebirthPurgeSupplyAccount(new string('c',64),155,155,2);
  Check(flightStore.TryCommit(flightStore.Published,other),"second stable owner has earned original entitlements");
  other.TryReserve(new RebirthPurgeSupplyDeliveryPlan(14,61,-993),out var prepared);
  var expected=flightStore.Published;accountFault=true;
  Check(!flightStore.TryCommit(expected,prepared),"interrupted preparation retains original candidate");
  prepared.TryStart(world,prepared.Token(world),out var started);accountFault=false;
  Check(!flightStore.TryCommit(expected,started)&&flightStore.Published[other.Player].Delivery.Phase==RebirthPurgeSupplyDeliveryPhase.Prepared,"reconciled preparation cannot falsely acknowledge a different flight start");
  Check(flightStore.TryCommit(flightStore.Published,started)&&flightStore.Published[other.Player].Delivery.Phase==RebirthPurgeSupplyDeliveryPhase.Started,"exact original start commits after preparation reconciliation");
  var flightCold=new RebirthPurgeSupplyAccountStore(binding);
  Check(flightCold.TryOpen()&&flightCold.Published[other.Player].Delivery.Phase==RebirthPurgeSupplyDeliveryPhase.Started&&flightCold.Published[other.Player].Delivery.X==14,"cold account retains started original destination");
  Console.WriteLine("RESULT "+checks+" PASS; production credit ledger/codec/atomic store; isolated files; no gameplay or aircraft proof.");
 }
}class WorldState {public string Guid;}
class World {public WorldState worldState=new WorldState();public bool IsRemote()=>false;}
class GameManager {public static GameManager Instance;public World World;}
class ConnectionManager {public bool IsServer;}
class SingletonMonoBehaviour<T> {public static T Instance;}
class ThreadManager {public static bool IsMainThread()=>true;}
class GameIO {public static string GetSaveGameDir()=>System.IO.Path.GetTempPath();}class Log {public static void Warning(string value){}}
using System;using System.Collections.Generic;using System.IO;using System.Linq;
class Program
{
 static int checks;static void Check(bool value,string text){if(!value)throw new Exception(text);checks++;Console.WriteLine("PASS "+text);}
 static void Step(){UnityEngine.Time.realtimeSinceStartup+=1;RebirthPurgeSupplyService.Pulse();}
 static void Main()
 {
  var dir=Path.Combine(Path.GetTempPath(),"rebirth-supply-service-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(Path.Combine(dir,"Config"));
  File.WriteAllText(Path.Combine(dir,"Config","_purge_supplies.xml"),"<purge_supplies version='1' baseKills='75' increment='5' maximumBonus='150' flightRadius='100'/>");RebirthPurgeSupplyPolicy.Load(dir);
  bool live=true;var scope=new object();var world=Guid.NewGuid();var binding=new RebirthPoiWorldBinding(scope,world.ToString("N").ToUpperInvariant(),dir,()=>live);
  RebirthPoiWorldStore.TryOpen(binding,out var store);RebirthPoiWorldLifecycle.Instance.Store=store;
  var player=new string('a',64);
  for(int n=0;n<130;n++)
  {
   var poi=new RebirthPoiIdentity("house",n*16,2,3,0,10,10,10,"forest");store.TryDiscover(store.Published,poi);
   var actor=new RebirthPoiActorObservation(Guid.NewGuid(),n+100,"zombieArlene",0,Guid.NewGuid(),10,contributor:player);
   var partial=new RebirthPoiPartialObservation(Guid.NewGuid(),0,1,new[]{new RebirthPoiVolumeObservation(7,new string('b',64),new[]{actor})});
   store.TryObservePartial(store.Published,poi,0,partial);store.TryClear(store.Published,poi,0,new RebirthPoiClearEvidence(Guid.NewGuid(),1,1,1,0,0,false,11));
  }
  Step();Check(RebirthPurgeSupplyService.Published.Count==0,"130-record aggregation stays bounded and does not publish incomplete sum");
  for(int n=0;n<5;n++)Step();
  Check(RebirthPurgeSupplyService.Published[player].ObservedCredits==130&&RebirthPurgeSupplyService.Published[player].EarnedDrops==1&&RebirthPurgeSupplyService.Published[player].SpentCredits==75,"connected service earns exact cumulative quota");
  var file=Path.Combine(dir,"RebirthData","Purge","Supplies","world.xml");var original=File.ReadAllText(file);
  for(int n=0;n<5;n++)Step();Check(File.ReadAllText(file)==original,"idle service does not rewrite account file");
  RebirthPurgeSupplyService.Reset();for(int n=0;n<5;n++)Step();
  Check(RebirthPurgeSupplyService.Published[player].EarnedDrops==1&&File.ReadAllText(file)==original,"restart does not grant original credits twice");
  var extra=new RebirthPoiIdentity("extra",3000,2,3,0,10,10,10,"forest");store.TryDiscover(store.Published,extra);
  Step();var extra2=new RebirthPoiIdentity("extra2",3016,2,3,0,10,10,10,"forest");store.TryDiscover(store.Published,extra2);
  for(int n=0;n<6;n++)Step();Check(RebirthPurgeSupplyService.Published[player].ObservedCredits==130,"newer snapshots cannot cancel or duplicate cumulative earning");
  var earned=RebirthPurgeSupplyService.Published[player];
  Check(RebirthPurgeSupplyService.TryPrepare(earned,new RebirthPurgeSupplyDeliveryPlan(14,61,-993),out var prepared),"pending entitlement retains original destination");
  Check(!RebirthPurgeSupplyService.TryPrepare(earned,new RebirthPurgeSupplyDeliveryPlan(15,61,-993),out _),"stale publication cannot reserve another entitlement");
  var stamp=new RebirthPurgeSupplyCrateStamp(world,player,prepared.InFlight,prepared.Token(world));
  var foreignWorld=Guid.NewGuid();
  Check(!RebirthPurgeSupplyService.TryLaunched(new RebirthPurgeSupplyCrateStamp(foreignWorld,player,stamp.Sequence,RebirthPurgeSupplyAccount.DeliveryToken(foreignWorld,player,stamp.Sequence))),"foreign saved world cannot consume pending entitlement");
  Check(!RebirthPurgeSupplyService.TryLaunched(new RebirthPurgeSupplyCrateStamp(world,player,stamp.Sequence+1,RebirthPurgeSupplyAccount.DeliveryToken(world,player,stamp.Sequence+1))),"future sequence cannot consume entitlement");
  Check(RebirthPurgeSupplyService.TryLaunched(stamp),"native accepted launch consumes one entitlement without disk crate proof");
  Check(RebirthPurgeSupplyService.Published[player].DeliveredDrops==1&&RebirthPurgeSupplyService.Published[player].InFlight==0,"accepted launch removes pending flight");
  Check(RebirthPurgeSupplyService.TryLaunched(stamp)&&RebirthPurgeSupplyService.Published[player].DeliveredDrops==1,"accepted launch retry is idempotent");
  RebirthPurgeSupplyService.Reset();for(int n=0;n<5;n++)Step();
  Check(RebirthPurgeSupplyService.Published[player].DeliveredDrops==1&&RebirthPurgeSupplyService.Published[player].InFlight==0,"cold service retains spent launch without replacement flight");
  live=false;Step();Check(RebirthPurgeSupplyService.Published==null,"replaced saved world makes publication unavailable");
  Check(!RebirthPurgeSupplyService.TryLaunched(stamp),"replaced world refuses launch commit");
  RebirthSandboxOptionManager.Current.IsPurge=false;Step();Check(RebirthPurgeSupplyService.Published==null,"None QoL resets Purge reward service");
  Console.WriteLine("RESULT "+checks+" PASS; production service/accounts/domain/store real isolated files; world clock doubled; no flight delivery proof.");
 }
}
class WorldState {public string Guid;}
class World {public WorldState worldState=new WorldState();public bool IsRemote()=>false;}
class GameManager {public static GameManager Instance;public World World;}
class ConnectionManager {public bool IsServer;}
class SingletonMonoBehaviour<T> {public static T Instance;}
class ThreadManager {public static bool IsMainThread()=>true;}
class GameIO {public static string GetSaveGameDir()=>System.IO.Path.GetTempPath();}
class Log {public static void Warning(string value){}}
namespace UnityEngine {internal static class Time {internal static float realtimeSinceStartup;}}
internal static class RebirthPurgeReleasePolicy {internal static bool Enabled=true;}
internal sealed class RebirthSandboxOptionManager {internal static readonly RebirthSandboxOptionManager Current=new RebirthSandboxOptionManager();internal bool IsPurge=true;}
internal sealed class RebirthPoiWorldLifecycle {internal static readonly RebirthPoiWorldLifecycle Instance=new RebirthPoiWorldLifecycle();internal RebirthPoiWorldStore Store;internal bool TryGetStore(out RebirthPoiWorldStore store){store=Store;return store!=null;}}
// Native crate decoding is covered by the serialization fixture.
internal sealed class EntitySupplyCrate { internal RebirthPurgeSupplyCrateStamp Stamp; }
internal static class RebirthPurgeSupplyCrateSerialization
{
 internal static bool TryReadAuthoritative(EntitySupplyCrate crate,Guid world,out RebirthPurgeSupplyCrateStamp stamp)
 { stamp=crate?.Stamp;return stamp!=null&&stamp.World==world; }
}

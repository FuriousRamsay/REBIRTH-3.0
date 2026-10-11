using System;using System.Collections.Generic;using System.IO;using System.Linq;
class Program
{
 static int checks;static void Check(bool ok,string label){if(!ok)throw new Exception(label);checks++;Console.WriteLine("PASS "+label);}
 static void Main()
 {
  var dir=Path.Combine(Path.GetTempPath(),"rebirth-milestone-barrier-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
  var scope=new object();var world=Guid.NewGuid();bool live=true;var binding=new RebirthPoiWorldBinding(scope,world.ToString("N").ToUpperInvariant(),dir,()=>live);
  RebirthPoiWorldStore.TryOpen(binding,out var store);RebirthPoiWorldLifecycle.Instance.Store=store;
  var pois=Enumerable.Range(0,4).Select(n=>new RebirthPoiIdentity("house",n*16,2,3,0,10,10,10,"forest")).ToArray();
  foreach(var poi in pois)store.TryDiscover(store.Published,poi);
  foreach(var poi in pois.Take(3))store.TryClear(store.Published,poi,0,new RebirthPoiClearEvidence(Guid.NewGuid(),1,1,1,0,0,false,1));
  var census=RebirthPurgePoiCensus.Instance;census.Published=pois.ToDictionary(p=>p.Key,p=>new RebirthPurgePoiCensus.Entry{Identity=p,Tier=0});
  store.ClearanceRegressionGuard=RebirthPurgeMilestoneService.TryPreserveBeforeRegression;
  var reset=Guid.NewGuid();var before=store.Published;
  Check(store.TryBeginReset(before,pois[0],0,reset)==RebirthPoiStoreResult.Deferred&&ReferenceEquals(store.Published,before),"unready census defers before durable or native reset mutation");
  census.Ready=true;
  Check(store.TryBeginReset(store.Published,pois[0],0,reset)==RebirthPoiStoreResult.Published,"ready original clearance checkpoint admits reset");
  Check(RebirthPurgeMilestoneService.Published["forest"].Cleared==3&&RebirthPurgeMilestoneService.Published["forest"].Eligible==4,"75 percent original milestone persisted before count falls");
  var receipt=RebirthPurgeMilestoneService.Published["forest"];var file=Path.Combine(dir,"RebirthData","Purge","Milestones","world.xml");var text=File.ReadAllText(file);
  Check(store.TryFinishReset(store.Published,pois[0],0,reset,RebirthPoiResetDisposition.Completed)==RebirthPoiStoreResult.Published,"completed reset lowers current truth");
  UnityEngine.Time.realtimeSinceStartup=1;RebirthPurgeObjectiveProgress.Instance.Pulse();RebirthPurgeMilestoneService.Pulse();
  Check(RebirthPurgeObjectiveProgress.Instance.Published["forest"].Cleared==2&&File.ReadAllText(file)==text,"current count falls while first-earned receipt remains unchanged");
  var cold=new RebirthPurgeMilestoneStore(binding);Check(cold.TryOpen()&&cold.Published["forest"].Revision==receipt.Revision,"cold milestone preserves original achievement revision");
  var proof=new RebirthPoiClearEvidence(Guid.NewGuid(),1,1,1,0,0,false,2);store.TryClear(store.Published,pois[0],1,proof);
  UnityEngine.Time.realtimeSinceStartup=2;RebirthPurgeObjectiveProgress.Instance.Pulse();RebirthPurgeMilestoneService.Pulse();
  Check(File.ReadAllText(file)==text,"reclear cannot farm or replace first-earned milestone");
  census.Ready=false;before=store.Published;
  Check(store.TryObserveRepopulation(before,pois[1],0,new RebirthPoiRepopulationEvidence(Guid.NewGuid(),1,3))==RebirthPoiStoreResult.Deferred&&ReferenceEquals(store.Published,before),"natural repopulation cannot erase an uncheckpointed current achievement");
  live=false;Check(!RebirthPurgeMilestoneService.TryPreserveBeforeRegression(before),"replaced world cannot acknowledge checkpoint custody");
  Console.WriteLine("RESULT "+checks+" PASS; production objective milestone store and regression guard; real isolated files; native reset effects not executed.");
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
internal sealed class RebirthPurgePoiCensus {internal static readonly RebirthPurgePoiCensus Instance=new RebirthPurgePoiCensus();internal bool Ready;internal long Generation=1;internal IReadOnlyDictionary<string,Entry> Published;internal sealed class Entry {internal RebirthPoiIdentity Identity;internal int Tier;}}
internal static class RebirthPurgeObjectiveFrame {internal const int MaximumEligible=200000;}
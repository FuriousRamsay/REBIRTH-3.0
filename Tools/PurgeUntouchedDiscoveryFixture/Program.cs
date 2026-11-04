using System;
using System.Collections.Generic;
static class RebirthSandboxOptionManager { public static Options Current=new(); public class Options { public bool IsPurge=true; } }
static class RebirthPoiNativeRestorationConnection { public static bool Pending; public static bool HasUnresolved(string identity)=>Pending; }
sealed class RebirthPoiWorldStore { public bool HasPending; public RebirthPoiWorldSnapshot Published=new(); }
sealed class RebirthPoiWorldSnapshot { public Binding Binding=new(); public bool Existing; public bool TryGet(string id,out object record){record=Existing?new():null;return Existing;} }
sealed class Binding { public bool IsCurrent=true;public object Scope; }
sealed class Root { public bool Current=true; public bool AuthoredCurrent()=>Current; }
sealed class Manifest { public bool Current=true;public Room[] Volumes; public bool StillMatches()=>Current; }
sealed class Operation { public bool IsCompleted=true; public object onComplete; }
sealed class Script { public bool Running; public bool IsRunning()=>Running; }
sealed class Room { public bool wasCleared,isSpawning;public int numSpawned;public Dictionary<int,int> respawnMap=new();public HashSet<int> pendingSpawnMap=new();public List<Operation> pendingSpawnOps=new();public Script minScript; }
partial class Probe {
 sealed class Candidate {
  public object World=new(),Prefab=new(),Touched,PendingClear,PendingRepopulation;public string Identity="poi";
  public RebirthPoiWorldStore Store;public bool Restored=true,Fault,ResetObserved,JournalDirty=true;public long? Epoch=0;
  public Guid BoundReset,Quarantine;public Root Root=new();public Manifest Manifest;
  public Dictionary<Room,object> Bindings=new();public Dictionary<Room,Dictionary<int,object>> Actors=new();public Dictionary<int,object> Unbound=new();
 }
 RebirthPoiWorldStore owner;bool pendingDeaths;Dictionary<object,object> quarantined=new();
 RebirthPoiWorldStore resolveStore()=>owner;bool HasPendingDeaths(string id)=>pendingDeaths;
 sealed class Setup {
  public Probe P=new();public Candidate C=new();public RebirthPoiWorldStore S=new();public Room R=new();
  public Setup(){RebirthSandboxOptionManager.Current.IsPurge=true;RebirthPoiNativeRestorationConnection.Pending=false;P.owner=S;C.Store=S;S.Published.Binding.Scope=C.World;C.Manifest=new(){Volumes=new[]{R}};C.Bindings[R]=new();C.Actors[R]=new();}
  public bool Omit()=>P.CanOmitUntouchedCandidate(C,S,S.Published);
 }
 static int count;
 static void Must(bool ok,string message){count++;if(!ok)throw new Exception(message);Console.WriteLine("PASS "+message);}
 static void Keep(string label,Action<Setup> change){var x=new Setup();change(x);Must(!x.Omit(),label);}
 static void Main(){
  var idle=new Setup();Must(idle.Omit(),"untouched empty native skeleton needs no discovered record");Must(idle.C.JournalDirty&&idle.C.Epoch==0&&!idle.S.Published.Existing,"omission does not forge journal acknowledgment or mutate authority");
  Keep("actual player visit publishes",x=>x.C.Touched=new());
  Keep("live actor custody publishes",x=>x.C.Actors[x.R][1]=new());
  Keep("dead actor custody also publishes",x=>x.C.Actors[x.R][2]=new());
  Keep("unbound saved room retained",x=>x.C.Unbound[1]=new());
  Keep("existing earned or legacy record preserved",x=>x.S.Published.Existing=true);
  Keep("pending death retained",x=>x.P.pendingDeaths=true);
  Keep("pending restoration retained",x=>RebirthPoiNativeRestorationConnection.Pending=true);
  Keep("pending clear retained",x=>x.C.PendingClear=new());Keep("pending repopulation retained",x=>x.C.PendingRepopulation=new());
  Keep("pending store write retained",x=>x.S.HasPending=true);Keep("wrong store retained",x=>x.C.Store=new());Keep("wrong owner retained",x=>x.P.owner=new());
  Keep("stale scope refused",x=>x.S.Published.Binding.IsCurrent=false);Keep("other world refused",x=>x.S.Published.Binding.Scope=new());
  Keep("journal not restored retained",x=>x.C.Restored=false);Keep("unknown epoch retained",x=>x.C.Epoch=null);Keep("reset epoch retained",x=>x.C.Epoch=1);
  Keep("bound reset retained",x=>x.C.BoundReset=Guid.NewGuid());Keep("quarantine retained",x=>x.C.Quarantine=Guid.NewGuid());Keep("quarantine map retained",x=>x.P.quarantined[x.C.Prefab]=new());
  Keep("fault retained",x=>x.C.Fault=true);Keep("reset observed retained",x=>x.C.ResetObserved=true);Keep("changed authoring retained",x=>x.C.Root.Current=false);
  Keep("missing manifest retained",x=>x.C.Manifest=null);Keep("stale manifest retained",x=>x.C.Manifest.Current=false);Keep("incomplete binding retained",x=>x.C.Bindings.Clear());Keep("missing actor map retained",x=>x.C.Actors.Clear());
  Keep("native spawning retained",x=>x.R.isSpawning=true);Keep("native spawned count retained",x=>x.R.numSpawned=1);Keep("cleared native room retained",x=>x.R.wasCleared=true);
  Keep("native respawn map retained",x=>x.R.respawnMap[1]=1);Keep("native pending map retained",x=>x.R.pendingSpawnMap.Add(1));
  Keep("unfinished native operation retained",x=>x.R.pendingSpawnOps.Add(new(){IsCompleted=false}));Keep("pending callback retained",x=>x.R.pendingSpawnOps.Add(new(){onComplete=new()}));Keep("null native operation retained",x=>x.R.pendingSpawnOps.Add(null));
  Keep("running native script retained",x=>x.R.minScript=new(){Running=true});Keep("sandbox behavior unchanged",x=>RebirthSandboxOptionManager.Current.IsPurge=false);
  var done=new Setup();done.R.pendingSpawnOps.Add(new());done.R.minScript=new();Must(done.Omit(),"completed callback-free operation and idle script add no custody");
  Console.WriteLine($"{count} checks PASS; extracted production omission method, native acceptance pending");
 }
}

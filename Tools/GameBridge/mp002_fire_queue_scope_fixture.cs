// AUTHORED_NOT_RUN. Actual queue class; heap/native simulation APIs doubled.
using System;
using System.Collections.Generic;
class World {internal bool Remote;internal ulong worldTime;internal bool IsRemote()=>Remote;}
class GameManager {internal static GameManager Instance=new GameManager();internal World World;}
class ConnectionManager {internal bool IsServer=true;}
static class SingletonMonoBehaviour<T> where T:new(){internal static T Instance=new T();}
static class ThreadManager {internal static bool Main=true;internal static bool IsMainThread()=>Main;}
struct Vector3i {internal int x;internal Vector3i(int value){x=value;}}
enum RebirthFireIgnitionCause {BlockEvent}
static class RebirthFireDefaults {internal const int MaxActiveFiresGlobal=8,MaxRequestPositions=4,MaxQueuedIgnitionsPerUpdate=2;}
class RebirthFireIgnitionEntry {internal Vector3i Position;internal int IgniterEntityId;internal RebirthFireIgnitionCause Cause;internal ulong DueWorldTime,Sequence;}
class RebirthFireIgnitionHeap {
 readonly List<RebirthFireIgnitionEntry> entries=new List<RebirthFireIgnitionEntry>();internal int Count=>entries.Count;
 internal void Push(RebirthFireIgnitionEntry value){entries.Add(value);entries.Sort((a,b)=>a.DueWorldTime.CompareTo(b.DueWorldTime));}
 internal bool TryPeek(out RebirthFireIgnitionEntry value){value=entries.Count==0?null:entries[0];return value!=null;}
 internal bool TryPop(out RebirthFireIgnitionEntry value){if(!TryPeek(out value))return false;entries.RemoveAt(0);return true;}
 internal void Clear()=>entries.Clear();
}
class RebirthFireService {
 internal static readonly RebirthFireService Instance=new RebirthFireService();internal readonly List<World> Applied=new List<World>();
 internal bool TryIgnite(World world,Vector3i position,int source,RebirthFireIgnitionCause cause){Applied.Add(world);return true;}
}
// ACTUAL_QUEUE_SCOPE
class Program {
 static void Require(bool value,string message){if(!value)throw new Exception(message);}
 static void Main(){
  var a=new World{worldTime=10};var b=new World{worldTime=2};var positions=new[]{new Vector3i(1)};
  GameManager.Instance.World=a;RebirthFireDelayedIgnitionQueue.Schedule(a,positions,1,RebirthFireIgnitionCause.BlockEvent,20);
  RebirthFireDelayedIgnitionQueue.Process(a);Require(RebirthFireDelayedIgnitionQueue.Count==1,"future action lost");
  GameManager.Instance.World=b;RebirthFireDelayedIgnitionQueue.Process(b);
  Require(RebirthFireDelayedIgnitionQueue.Count==0&&RebirthFireService.Instance.Applied.Count==0,"old action crossed world");
  RebirthFireDelayedIgnitionQueue.Schedule(a,positions,1,RebirthFireIgnitionCause.BlockEvent,0);
  Require(RebirthFireDelayedIgnitionQueue.Count==0,"late old-world callback admitted");
  RebirthFireDelayedIgnitionQueue.Schedule(b,positions,1,RebirthFireIgnitionCause.BlockEvent,2);
  ThreadManager.Main=false;RebirthFireDelayedIgnitionQueue.Process(b);ThreadManager.Main=true;
  Require(RebirthFireDelayedIgnitionQueue.Count==1,"off-thread processing changed queue");
  SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer=false;RebirthFireDelayedIgnitionQueue.Process(b);
  Require(RebirthFireDelayedIgnitionQueue.Count==1,"client processed authority queue");
  SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer=true;RebirthFireDelayedIgnitionQueue.Process(b);
  Require(RebirthFireService.Instance.Applied.Count==1&&ReferenceEquals(RebirthFireService.Instance.Applied[0],b),"current action not delivered to original world");
  b.Remote=true;RebirthFireDelayedIgnitionQueue.Schedule(b,positions,1,RebirthFireIgnitionCause.BlockEvent,0);
  Require(RebirthFireDelayedIgnitionQueue.Count==0,"remote simulation admitted ignition");
  Console.WriteLine("PASS actual delayed queue world fence with native/heap doubles; not fire simulation validation");
 }
}

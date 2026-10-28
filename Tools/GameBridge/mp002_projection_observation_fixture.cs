// AUTHORED_NOT_RUN. Actual read-only projection query and native-particle predicate.
using System;using System.Collections.Generic;
class World{}
struct Vector3i{internal int x;}
static class ThreadManager{internal static bool Main=true;internal static bool IsMainThread()=>Main;}
class GameManager{internal static GameManager Instance=new GameManager();internal static bool IsDedicatedServer;internal World World;internal bool Particle;internal bool HasBlockParticleEffect(Vector3i p)=>Particle;}
static class Subject
{
 static bool initialized,snapshotResyncNeeded;static World initializedWorld;static ulong currentWorldEpoch;static int pendingSnapshotRevision=-1;
 static readonly HashSet<Vector3i> Active=new HashSet<Vector3i>(),RenderedFire=new HashSet<Vector3i>();
 static readonly Dictionary<Vector3i,ulong> Smoke=new Dictionary<Vector3i,ulong>();
 // ACTUAL_POSITION
 // ACTUAL_PARTICLE
 static void Need(bool value,string message){if(!value)throw new Exception(message);}
 static void Main()
 {
  var world=new World();GameManager.Instance.World=world;var p=new Vector3i{x=7};
  Need(!TryObservePosition(world,p,out _,out _,out _,out _),"uninitialized projection refused");
  initialized=true;initializedWorld=world;Need(!TryObservePosition(world,p,out _,out _,out _,out _),"no received epoch refused");
  currentWorldEpoch=1;Active.Add(p);
  Need(TryObservePosition(world,p,out bool active,out ulong cooldown,out bool visible,out bool pending)&&active&&!visible&&!pending&&cooldown==0,"active row does not invent native particle");
  RenderedFire.Add(p);Need(TryObservePosition(world,p,out active,out cooldown,out visible,out pending)&&!visible,"render bookkeeping does not invent native particle");
  GameManager.Instance.Particle=true;
  Need(TryObservePosition(world,p,out active,out cooldown,out visible,out pending)&&visible,"actual native particle observed");
  pendingSnapshotRevision=2;Need(TryObservePosition(world,p,out active,out cooldown,out visible,out pending)&&pending,"incomplete snapshot disclosed");
  pendingSnapshotRevision=-1;snapshotResyncNeeded=true;Need(TryObservePosition(world,p,out active,out cooldown,out visible,out pending)&&pending,"resync uncertainty disclosed");
  snapshotResyncNeeded=false;Active.Remove(p);RenderedFire.Remove(p);Smoke[p]=100;
  Need(TryObservePosition(world,p,out active,out cooldown,out visible,out pending)&&!active&&!visible&&cooldown==100,"extinguished cooldown is distinct from active fire");
  var next=new World();GameManager.Instance.World=next;
  Need(!TryObservePosition(world,p,out _,out _,out _,out _)&&!TryObservePosition(next,p,out _,out _,out _,out _),"replaced native world cannot borrow projection");
  GameManager.Instance.World=world;GameManager.IsDedicatedServer=true;
  Need(!TryObservePosition(world,p,out _,out _,out _,out _),"headless server cannot claim client particle evidence");
  GameManager.IsDedicatedServer=false;ThreadManager.Main=false;Need(!TryObservePosition(world,p,out _,out _,out _,out _),"off-thread observation refused");
  ThreadManager.Main=true;Need(Active.Count==0&&Smoke[p]==100,"observation did not mutate fire projection");
  Console.WriteLine("PASS actual projection/particle observation with explicit native doubles; renderer/topology NOT_RUN");
 }
}

using System;
using System.Collections.Generic;
using System.Linq;
// AUTHORED_NOT_RUN. Entire actual saved-pending phase partial is injected.
// Actual terminal/publication admission and budget are explicit adapters.
// No station timer/tool/output/marker setter API exists in this fixture.
// ACTUAL_PENDING_PHASE_SOURCE
internal sealed class Recipe {}
internal sealed class RecipeQueueItem {internal Recipe Recipe=new Recipe();}
internal sealed class TileEntityWorkstation {internal RecipeQueueItem[] Queue=new[]{new RecipeQueueItem()};}
internal sealed class World {internal bool Remote;internal bool IsRemote()=>Remote;}
internal sealed class GameManager {internal static GameManager Instance=new GameManager();internal World World=new World();}
internal static class RebirthProductiveEarnedRemoteClient {internal static int Entries;internal static void ApplyNativePendingOnEnter(TileEntityWorkstation station){Entries++;}}
internal static class RebirthProductiveEarnedRemoteHost {internal static bool Handoff;internal static bool IsNativeHandoffTick(TileEntityWorkstation station)=>Handoff;}
internal static class RebirthRecyclerNativeLoop
{internal static bool Running=true;internal static bool IsCurrentInvocation(TileEntityWorkstation station)=>Running;}
internal sealed class Budget
{
 internal static Budget Phase;internal static bool Available=true;internal int Observes;
 internal sealed class Observation:IDisposable
 {internal Budget Owner;internal int Disposals;private bool closed;public void Dispose(){if(closed)return;closed=true;Disposals++;if(ReferenceEquals(Phase,Owner))Phase=null;}}
 internal Observation TryObserveNative(byte[] body,int offset)
 {if(!Available||Phase!=null)return null;Observes++;Phase=this;return new Observation{Owner=this};}
}
internal sealed class Watch:IDisposable
{internal bool ThrowOnce;internal int Disposals;public void Dispose(){Disposals++;if(ThrowOnce){ThrowOnce=false;throw new Exception("watch cleanup fault");}}}
internal static partial class RebirthProductiveEarnedCoordinator
{
 internal sealed class Attempt
 {internal TileEntityWorkstation Station=new TileEntityWorkstation();internal NativeTerminal Terminal;internal float PendingTimerBefore=-12,PendingTimerTarget=-7;internal bool OutputTimerObserved=true,NativeTickCustody;internal Func<bool> NativeAdoptionReleaseReady;internal object NativeOutput=new object();}
 internal sealed class NativeTerminal
 {
  internal bool PendingNextWork=true,SavedPending,Valid=true;internal object SavedOrigin;
  internal byte[] Body=new byte[]{1,2,3};internal int ElapsedOffset;
  internal Budget Memory=new Budget();internal Budget.Observation Allocation;internal Watch Watch=new Watch();
  internal bool Current()=>Valid&&(Budget.Phase==null||ReferenceEquals(Budget.Phase,Memory));
 }
 internal static int Calls;internal static bool ThrowContinuation,StartChild;
 private static readonly Dictionary<TileEntityWorkstation,Attempt> retained=new Dictionary<TileEntityWorkstation,Attempt>();
 private static bool Enabled=true;internal static int CompletionGates;
 private static bool TryNativeMarker(TileEntityWorkstation station,out byte[] marker){marker=new byte[]{9};return true;}
 private static bool TryRetireObservedNativeTerminal(TileEntityWorkstation station)=>false;
 private static bool TryRestorePendingNativeTerminal(TileEntityWorkstation station)=>false;
 private static bool TryFinishNativeTerminal(Attempt original)=>ContinueSavedNativePending(original);
 private static bool AllowsCompletion(TileEntityWorkstation station,RecipeQueueItem row){CompletionGates++;return false;}
 // ACTUAL_NATIVE_ENTRY
 internal static void Enter(Attempt original)=>ObserveEntered(original.Station);
 internal static Budget ChildMemory;
 private static bool ContinueSavedNativeSuccessor(Attempt original)
 {
  Calls++;if(!ReferenceEquals(Budget.Phase,original.Terminal.Memory))throw new Exception("continuation has no own observation phase");
  if(ThrowContinuation)throw new Exception("continuation fault");
  if(StartChild)
  {
   original.Terminal.Allocation.Dispose();original.Terminal.Allocation=null;
   ChildMemory=new Budget();ChildMemory.TryObserveNative(new byte[]{9},0);
  }
  return false;
 }
 internal static Attempt Setup(bool saved=true,bool origin=false,bool active=false)
 {
  Budget.Phase=null;Budget.Available=true;Calls=CompletionGates=0;ThrowContinuation=StartChild=false;ChildMemory=null;RebirthRecyclerNativeLoop.Running=true;retained.Clear();
  var a=new Attempt{Terminal=new NativeTerminal{SavedPending=saved&&!origin,SavedOrigin=saved&&origin?new object():null}};
  if(active)a.Terminal.Allocation=a.Terminal.Memory.TryObserveNative(a.Terminal.Body,0);retained.Add(a.Station,a);return a;
 }
 internal static bool Run(Attempt a)=>ContinueSavedNativePending(a);
 internal static void Park(Attempt a)=>ParkSavedPendingTerminal(a.Terminal);
}
internal static class Program
{
 private static void Need(bool value,string message){if(!value)throw new Exception(message);}
 public static void Main()
 {
  var a=RebirthProductiveEarnedCoordinator.Setup(active:true);var body=a.Terminal.Body;var graph=a.Terminal.Allocation;var watch=a.Terminal.Watch;
  Need(!RebirthProductiveEarnedCoordinator.Run(a)&&RebirthProductiveEarnedCoordinator.Calls==1&&Budget.Phase==null&&graph.Disposals==1&&watch.Disposals==1,
   "saved pending wait did not release old graph/watch");
  Need(ReferenceEquals(a.Terminal.Body,body)&&a.Terminal.Memory!=null&&a.PendingTimerBefore==-12&&a.PendingTimerTarget==-7&&a.Terminal.SavedPending,
   "parking dropped original body/account/debt or saved evidence");
  Need(!RebirthProductiveEarnedCoordinator.Run(a)&&RebirthProductiveEarnedCoordinator.Calls==2&&Budget.Phase==null&&a.Terminal.Memory.Observes==2,"later native invocation could not reacquire/release phase");
  a=RebirthProductiveEarnedCoordinator.Setup(origin:true);RebirthProductiveEarnedCoordinator.Run(a);Need(Budget.Phase==null&&RebirthProductiveEarnedCoordinator.Calls==1,"actual loaded saved source could not use continuation phase");
  a=RebirthProductiveEarnedCoordinator.Setup(saved:false,active:true);graph=a.Terminal.Allocation;Need(!RebirthProductiveEarnedCoordinator.Run(a)&&Budget.Phase!=null&&graph.Disposals==0&&RebirthProductiveEarnedCoordinator.Calls==0,"unsaved terminal released its observation authority");
  a=RebirthProductiveEarnedCoordinator.Setup(active:true);RebirthRecyclerNativeLoop.Running=false;RebirthProductiveEarnedCoordinator.Run(a);Need(Budget.Phase==null&&RebirthProductiveEarnedCoordinator.Calls==0,"non-invocation pump resumed work or held phase");
  a=RebirthProductiveEarnedCoordinator.Setup();a.Terminal.Valid=false;RebirthProductiveEarnedCoordinator.Run(a);Need(RebirthProductiveEarnedCoordinator.Calls==0&&a.Terminal.Body!=null,"changed source resumed or lost retained debt");
  a=RebirthProductiveEarnedCoordinator.Setup();Budget.Available=false;RebirthProductiveEarnedCoordinator.Run(a);Need(RebirthProductiveEarnedCoordinator.Calls==0&&a.Terminal.Allocation==null,"allocation refusal admitted continuation");
  a=RebirthProductiveEarnedCoordinator.Setup(active:true);RebirthProductiveEarnedCoordinator.ThrowContinuation=true;bool threw=false;try{RebirthProductiveEarnedCoordinator.Run(a);}catch{threw=true;}
  Need(threw&&Budget.Phase==null&&a.Terminal.Body!=null,"continuation exception stranded phase or erased original");
  a=RebirthProductiveEarnedCoordinator.Setup(active:true);graph=a.Terminal.Allocation;a.Terminal.Watch.ThrowOnce=true;threw=false;try{RebirthProductiveEarnedCoordinator.Park(a);}catch{threw=true;}
  Need(threw&&graph.Disposals==1&&Budget.Phase==null&&a.Terminal.Watch==null&&a.Terminal.Allocation==null,"watch exception stranded saved pending graph");
  a=RebirthProductiveEarnedCoordinator.Setup(active:true);RebirthProductiveEarnedCoordinator.StartChild=true;RebirthProductiveEarnedCoordinator.Run(a);
  Need(ReferenceEquals(Budget.Phase,RebirthProductiveEarnedCoordinator.ChildMemory),"parking predecessor disposed successor's distinct phase");
  a=RebirthProductiveEarnedCoordinator.Setup();var oldToken=a.NativeOutput;RebirthProductiveEarnedCoordinator.Enter(a);
  Need(RebirthProductiveEarnedCoordinator.Calls==1&&RebirthProductiveEarnedCoordinator.CompletionGates==1&&ReferenceEquals(a.NativeOutput,oldToken)&&Budget.Phase==null,
   "actual new invocation entry failed to resume saved terminal or rebound old output token");
  a=RebirthProductiveEarnedCoordinator.Setup();a.NativeAdoptionReleaseReady=()=>false;
  RebirthProductiveEarnedCoordinator.Run(a);Need(RebirthProductiveEarnedCoordinator.Calls==0&&Budget.Phase==null,"retained adoption custody was bypassed by native invocation");
  a=RebirthProductiveEarnedCoordinator.Setup();RebirthRecyclerNativeLoop.Running=false;a.NativeTickCustody=true;
  RebirthProductiveEarnedCoordinator.Run(a);Need(RebirthProductiveEarnedCoordinator.Calls==0,"custody alone fabricated native handoff tick");
  RebirthProductiveEarnedRemoteHost.Handoff=true;RebirthProductiveEarnedCoordinator.Run(a);
  Need(RebirthProductiveEarnedCoordinator.Calls==1&&Budget.Phase==null,"matching handoff tick and custody did not permit pending phase");
  RebirthProductiveEarnedRemoteHost.Handoff=false;
  a=RebirthProductiveEarnedCoordinator.Setup();GameManager.Instance.World.Remote=true;RebirthProductiveEarnedCoordinator.Enter(a);
  Need(RebirthProductiveEarnedRemoteClient.Entries==1&&RebirthProductiveEarnedCoordinator.Calls==0,"remote native entry ran authority-local continuation");
  Console.WriteLine("PASS extracted saved pending phase handoff; disclosed terminal/native/budget admission doubles, no gameplay setters");
 }
}

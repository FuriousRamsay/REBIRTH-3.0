// AUTHORED_NOT_RUN. Actual factory/control-flow; excluded native boundaries below.
using System;using System.Collections.Generic;
class ItemValue{internal bool Empty;internal bool IsEmpty()=>Empty;}
class Card{internal bool isInventoryFull=true;internal ItemValue outputItemValue=new ItemValue();}
class XUiC_WorkstationGrid{}
static class RebirthProductiveEarnedObservationBudget{
 internal static bool Allowed=true;internal static int Disposed;
 sealed class Lease:IDisposable{public void Dispose(){Disposed++;}}
 internal static bool TryRetainDetached(long size,out IDisposable lease){lease=Allowed?new Lease():null;return Allowed;}
}
class Subject{
 const int UiOriginalMaximum=12*1024*1024;
 class UiScope{internal bool Cached=true,Inserted;internal Card Card=new Card();internal object Grid;internal List<object> Outputs=new List<object>{new object()};internal UiOutputOriginal Original;}
 static void ConsumeStationOutputBefore(UiOutputOriginal original){} // Independent placed pre-generation source.
 class UiOutputOriginal{
  internal static bool ThrowConstructor,AllowObservation=true;
  internal readonly UiScope Scope;internal object World=new object(),Actor=new object(),Recipe=new object();internal int Count=1,Deaths;
  internal string Creation="creation",Path="original";internal Guid Operation=Guid.NewGuid();internal byte[] Original={1};
  internal IDisposable Memory;internal bool Entered,Returned,Live=true,GridAccepted;internal int OutputPrefix,Entries,Observations;internal bool LastNativeRan;
  internal UiOutputOriginal(UiScope scope,ItemValue primary){if(ThrowConstructor)throw new InvalidOperationException();Scope=scope;}
  internal bool Current()=>Live;
  internal object CapturedGrid;internal bool IsCapturedGrid(object grid)=>grid!=null&&ReferenceEquals(grid,CapturedGrid);
  internal void Enter(string effect){Entries++;Entered=true;}
  internal void Observe(bool ran,Exception error){Observations++;LastNativeRan=ran;Returned=AllowObservation&&error==null;}
 }
 class UiOutputPending{internal object World,Actor,Recipe;internal int Count,Deaths;internal string Creation,Path,Hash;internal Guid Operation;internal UiOutputOriginal Source;internal bool Returned;}
 static UiScope ui;static Dictionary<Card,UiOutputPending> pendingUiOutputs=new Dictionary<Card,UiOutputPending>();
 static bool HasPendingUiOutput(Card card)=>pendingUiOutputs.ContainsKey(card);
 static string UiOriginalHash(byte[] bytes)=>Convert.ToBase64String(bytes);
 static void ObserveOutputAuditOriginal(object actor,Guid operation,object card=null){} // Debug diagnostics excluded.
 // ACTUAL_PREPARE
 // ACTUAL_ENTER
 // ACTUAL_RETAIN
 // ACTUAL_DELIVERY
 // ACTUAL_GRID_GUARD
 static UiScope Fresh(){pendingUiOutputs.Clear();UiOutputOriginal.ThrowConstructor=false;UiOutputOriginal.AllowObservation=true;RebirthProductiveEarnedObservationBudget.Allowed=true;return ui=new UiScope();}
 static int cases;static void Check(bool value,string text){if(!value)throw new Exception(text);cases++;}
 static bool Refused(Action action){try{action();return false;}catch(InvalidOperationException){return true;}}
 static void Main(){
  var scope=Fresh();Check(PrepareUiCachedOutput(scope)&&scope.Original!=null&&!scope.Original.Entered&&pendingUiOutputs.Count==0,"cached selection is detached before actual native insertion");
  var original=scope.Original;EnterUiCachedOutput(scope);Check(original.Entered&&original.Returned&&original.Entries==1&&original.Observations==1&&!original.LastNativeRan&&ReferenceEquals(pendingUiOutputs[scope.Card].Source,original),"actual insertion retains one original without claiming giveExp ran");
  EnterUiCachedOutput(scope);Check(original.Entries==1&&original.Observations==1,"second insertion boundary cannot replace cached original");
  original.OutputPrefix=1;ObserveUiCachedDelivery(original);Check(!pendingUiOutputs[scope.Card].Returned,"incomplete accepted inventory prefix cannot mark delivery");
  original.OutputPrefix=2;ObserveUiCachedDelivery(original);Check(pendingUiOutputs[scope.Card].Returned,"complete accepted cached prefix marks only pending physical delivery");
  scope=Fresh();scope.Cached=false;Check(!PrepareUiCachedOutput(scope),"fresh source cannot borrow cached admission");
  scope=Fresh();ui=null;Check(!PrepareUiCachedOutput(scope),"unmounted selection cannot prepare cached source");
  scope=Fresh();scope.Card.isInventoryFull=false;Check(!PrepareUiCachedOutput(scope),"noncached native card cannot prepare cached source");
  scope=Fresh();scope.Card.outputItemValue.Empty=true;Check(!PrepareUiCachedOutput(scope),"missing generated primary cannot prepare source");
  scope=Fresh();RebirthProductiveEarnedObservationBudget.Allowed=false;Check(!PrepareUiCachedOutput(scope)&&scope.Original==null,"unavailable workspace cannot mint source");
  scope=Fresh();int disposed=RebirthProductiveEarnedObservationBudget.Disposed;UiOutputOriginal.ThrowConstructor=true;Check(!PrepareUiCachedOutput(scope)&&RebirthProductiveEarnedObservationBudget.Disposed==disposed+1,"failed detached source capture releases workspace");
  scope=Fresh();PrepareUiCachedOutput(scope);scope.Original.Live=false;Check(Refused(()=>EnterUiCachedOutput(scope))&&pendingUiOutputs.Count==0,"changed actual scope cannot enter cached insertion");
  scope=Fresh();PrepareUiCachedOutput(scope);UiOutputOriginal.AllowObservation=false;Check(Refused(()=>EnterUiCachedOutput(scope))&&pendingUiOutputs.ContainsKey(scope.Card),"unknown selection publication retains its original before setter entry");
  scope=Fresh();scope.Grid=new object();PrepareUiCachedOutput(scope);EnterUiCachedOutput(scope);ObserveUiCachedDelivery(scope.Original);Check(!pendingUiOutputs[scope.Card].Returned,"unaccepted native grid cannot mark delivery");scope.Original.GridAccepted=true;ObserveUiCachedDelivery(scope.Original);Check(pendingUiOutputs[scope.Card].Returned,"accepted cached grid marks only pending physical delivery");
  scope=Fresh();PrepareUiCachedOutput(scope);var grid=new XUiC_WorkstationGrid();scope.Original.CapturedGrid=grid;
  Check(Refused(()=>GuardUiCachedGrid(scope,grid)),"prepared inventory sink cannot silently become native grid delivery");
  scope.Grid=grid;Check(!Refused(()=>GuardUiCachedGrid(scope,grid)),"same prepared native grid remains eligible");
  scope.Grid=null;Check(!Refused(()=>GuardUiCachedGrid(scope,new XUiC_WorkstationGrid())),"unrelated native grid is outside the selected source");
  Console.WriteLine("MP028 cached original factory: "+cases+" assertions; native/publication/current adapters only.");
 }
}

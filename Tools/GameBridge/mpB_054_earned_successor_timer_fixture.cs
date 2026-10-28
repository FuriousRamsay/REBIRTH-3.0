using System;
using System.Collections.Generic;
// AUTHORED_NOT_RUN. Entire production successor-timer partial is injected.
// Native output admission/token and queue objects are explicit doubles.
// ACTUAL_SUCCESSOR_TIMER_SOURCE
internal sealed class Recipe {}
internal sealed class RecipeQueueItem {internal Recipe Recipe;internal int Multiplier;internal float CraftingTimeLeft,OneItemCraftTime;}
internal sealed class TileEntityWorkstation {internal RecipeQueueItem[] Queue;}
// Remote delegation is independently controlled, not a local settlement witness.
internal static class RebirthProductiveEarnedRemoteClient {internal static bool Defer;internal static bool DeferNativeOutputSuccessor(TileEntityWorkstation station,RecipeQueueItem row,float before,float target)=>Defer;}
internal static class RebirthRecyclerNativeLoop
{internal static object Token=new object();internal static object InvocationToken(TileEntityWorkstation ignored)=>Token;}
internal static partial class RebirthProductiveEarnedCoordinator
{
 // Definition capture is an explicit adapter here; definition persistence has a separate fixture.
 private static bool CapturePendingDefinition(Attempt value,RecipeQueueItem row)=>true;
 internal static bool Enabled=true;
 internal sealed class Attempt
 {
  internal string PendingRecipeBinding,PendingExtras;internal IDisposable PendingDefinitionRetention;
  internal RecipeQueueItem Row;internal Recipe Recipe;internal int Multiplier;
  internal bool OutputTimerObserved,PendingTimerObserved,PendingTimerAmbiguous;
  internal object NativeOutput;internal int PendingTimerIndex=-1;internal float PendingTimerBefore,PendingTimerTarget;
 }
 private static readonly Dictionary<TileEntityWorkstation,Attempt> retained=new Dictionary<TileEntityWorkstation,Attempt>();
 internal static Attempt Setup(TileEntityWorkstation station,RecipeQueueItem row,int multiplier)
 {retained.Clear();Enabled=true;var value=new Attempt{Row=row,Recipe=row.Recipe,Multiplier=multiplier,OutputTimerObserved=true,NativeOutput=RebirthRecyclerNativeLoop.Token};retained.Add(station,value);return value;}
}
internal static class Program
{
 private static void Need(bool value,string label){if(!value)throw new Exception(label);}
 public static void Main()
 {
  var row=new RecipeQueueItem{Recipe=new Recipe(),Multiplier=2,CraftingTimeLeft=-12,OneItemCraftTime=5};
  var station=new TileEntityWorkstation{Queue=new[]{row}};
  var original=RebirthProductiveEarnedCoordinator.Setup(station,row,3);
  RebirthProductiveEarnedRemoteClient.Defer=true;
  Need(RebirthProductiveEarnedCoordinator.DeferNativeSuccessorTimer(station,row,-12,-7)&&!original.PendingTimerObserved,"remote delegation also captured an authority-local successor");
  RebirthProductiveEarnedRemoteClient.Defer=false;
  Need(RebirthProductiveEarnedCoordinator.DeferNativeSuccessorTimer(station,row,-12,-7),"same-row native increase not deferred");
  Need(original.PendingTimerObserved&&original.PendingTimerIndex==0&&original.PendingTimerBefore==-12&&original.PendingTimerTarget==-7&&!original.PendingTimerAmbiguous,"same-row exact native source lost");
  Need(row.CraftingTimeLeft==-12,"source capture mutated unworked successor");
  Need(RebirthProductiveEarnedCoordinator.DeferNativeSuccessorTimer(station,row,-12,-7)&&!original.PendingTimerAmbiguous,"exact duplicate became ambiguous");
  Need(RebirthProductiveEarnedCoordinator.DeferNativeSuccessorTimer(station,row,-12,-6)&&original.PendingTimerAmbiguous,"non-native same-row target accepted");
  original=RebirthProductiveEarnedCoordinator.Setup(station,row,3);original.NativeOutput=new object();
  Need(!RebirthProductiveEarnedCoordinator.DeferNativeSuccessorTimer(station,row,-12,-7)&&!original.PendingTimerObserved,"foreign invocation issued successor");
  original=RebirthProductiveEarnedCoordinator.Setup(station,row,3);original.OutputTimerObserved=false;
  Need(!RebirthProductiveEarnedCoordinator.DeferNativeSuccessorTimer(station,row,-12,-7),"before-output write acquired successor");
  row.Multiplier=0;original=RebirthProductiveEarnedCoordinator.Setup(station,row,1);
  Need(!RebirthProductiveEarnedCoordinator.DeferNativeSuccessorTimer(station,row,-12,-7),"empty-row native cycle timer held");
  row.Multiplier=2;
  var next=new RecipeQueueItem{Recipe=new Recipe(),Multiplier=1,CraftingTimeLeft=8,OneItemCraftTime=8};
  station.Queue=new[]{next};original=RebirthProductiveEarnedCoordinator.Setup(station,row,3);
  Need(!RebirthProductiveEarnedCoordinator.DeferNativeSuccessorTimer(station,next,8,8)&&!original.PendingTimerObserved,"native zero carryover invented work");
  Need(RebirthProductiveEarnedCoordinator.DeferNativeSuccessorTimer(station,next,8,-4)&&original.PendingTimerTarget==-4&&next.CraftingTimeLeft==8,"cycled successor lost native carryover");
  Need(RebirthProductiveEarnedCoordinator.DeferNativeSuccessorTimer(station,next,8,-3)&&original.PendingTimerAmbiguous,"two distinct native requests merged");
  station.Queue=new[]{next,next};original=RebirthProductiveEarnedCoordinator.Setup(station,row,3);
  Need(RebirthProductiveEarnedCoordinator.DeferNativeSuccessorTimer(station,next,8,-4)&&original.PendingTimerAmbiguous,"aliased queue not retained ambiguous");
  station.Queue=new[]{next};original=RebirthProductiveEarnedCoordinator.Setup(station,row,3);
  Need(RebirthProductiveEarnedCoordinator.DeferNativeSuccessorTimer(station,next,8,float.NaN)&&original.PendingTimerAmbiguous,"nonfinite target accepted");
  Console.WriteLine("PASS extracted exact native successor timer capture; not native continuation qualification");
 }
}

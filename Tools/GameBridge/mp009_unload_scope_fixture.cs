// AUTHORED_NOT_RUN. Actual World.UnloadWorld postfix + resource cleanup method.
// Global stores are counters; this does not prove persistence or native shutdown.
using System;
namespace HarmonyLib{[AttributeUsage(AttributeTargets.Class)]class HarmonyPatch:Attribute{public HarmonyPatch(Type t,string name){}}}
class World{public void UnloadWorld(){}}
class GameManager{internal static GameManager Instance=new GameManager();internal World World;}
static class ThreadManager{internal static bool Main=true;internal static bool IsMainThread()=>Main;}
static class Calls{internal static int Global;}
static class RemoteResourceTransactionOutcomeJournal{internal static World Last;internal static int Observations;internal static void ResetForWorldUnload(World w){Last=w;Observations++;}}
static class RemoteResourceStateStore{internal static void FlushPending(){Calls.Global++;}internal static void ResetForWorldUnload(){Calls.Global++;}}
static class QuickStackAcceptedCategoryRegistry{internal static void FlushPending(){Calls.Global++;}internal static void ClearWorld(){Calls.Global++;}}
static class RemoteResourceRegistry{internal static void Clear(){Calls.Global++;}}
static class RemoteResourceClientAvailability{internal static void Clear(){Calls.Global++;}}
static class RemoteResourceClientTransactionCoordinator{internal static void Clear(){Calls.Global++;}}
static class LogisticsTransferOutcomeJournal{internal static void ResetForWorldUnload(){Calls.Global++;}}
static class LogisticsTransferService{internal static void ResetClientRequestScope(){Calls.Global++;}}
static class RemoteResourcePatchInstaller
{
 // ACTUAL_CLEANUP
}
// ACTUAL_UNLOAD
class Program
{
 static void Need(bool value,string message){if(!value)throw new Exception(message);}
 static void Unload(World world)=>typeof(RemoteResourceWorldUnloadPatch).GetMethod("Postfix",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static).Invoke(null,new object[]{world});
 static void Main()
 {
  var old=new World();var current=new World();GameManager.Instance.World=current;
  Unload(old);Need(ReferenceEquals(RemoteResourceTransactionOutcomeJournal.Last,old)&&Calls.Global==0,"actual stale unload forwards only its original World and cannot reset current global stores");
  Unload(current);Need(ReferenceEquals(RemoteResourceTransactionOutcomeJournal.Last,current)&&Calls.Global==9,"actual current native unload cleans existing stores");
  int journal=RemoteResourceTransactionOutcomeJournal.Observations;
  ThreadManager.Main=false;Unload(current);Need(Calls.Global==9&&RemoteResourceTransactionOutcomeJournal.Observations==journal,"off-thread unload refused");
  ThreadManager.Main=true;Unload(null);Need(Calls.Global==9&&RemoteResourceTransactionOutcomeJournal.Observations==journal,"missing actual World refused");
  GameManager.Instance.World=null;Unload(old);Need(Calls.Global==18&&ReferenceEquals(RemoteResourceTransactionOutcomeJournal.Last,old),"normal post-unload null current World still cleans original session");
  Console.WriteLine("PASS actual resource unload scope; stores/native shutdown/persistence doubled, NOT_RUN in game");
 }
}

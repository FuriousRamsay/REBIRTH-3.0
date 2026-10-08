using System;
class ItemStack{}
class EntityPlayerLocal{}
class World{public EntityPlayerLocal GetPrimaryPlayer(){return new EntityPlayerLocal();}}
class GameManager{public static GameManager Instance=new GameManager();public World World=new World();public static void ShowTooltip(EntityPlayerLocal p,string s){}}
static class Time{public static float unscaledTime;}
static class QuickStackHotkeyDiagnostics{public static bool Enabled;public static void Write(string s){}}
enum QuickStackRadialAction{Deposit=0,PullVehicle=5,PullDrone=7,CompanionInventoryPull=8,PullVehicleCredits=9,PullDroneCredits=10}
static class LogisticsInventoryCredits{
// RECEIPT_POLICY
public static int Calls;public static void Apply(ItemStack[] s){if(s!=null)Calls++;}}
static class QuickStackRemotePlayerBagSync{public static int Calls;public static void ApplyClientItems(ItemStack[] s){if(s!=null)Calls++;}}
class Subject{static ulong pendingRequestEpoch;static int pendingRequestId,lastAppliedResultRequestId;static float pendingRequestDeadline;static bool pendingRequestWasHotkey;static string pendingTargetId,pendingSourceKeys;static Action transferCompleted;static QuickStackRadialAction pendingAction;const float PendingOutcomeQuerySeconds=3;
static void ShowTransferNotifications(int[] a,int[] b){}static bool HasTransferNotifications(int[] a,int[] b){return false;}static void PlayHotkeyTransferSound(){}
// PRODUCTION_METHOD
public static void Reset(QuickStackRadialAction action){pendingRequestEpoch=99;pendingRequestId=3;lastAppliedResultRequestId=0;pendingAction=action;LogisticsInventoryCredits.Calls=0;QuickStackRemotePlayerBagSync.Calls=0;}
public static void Deliver(ulong epoch,int id,bool known){ReceiveCompleted(epoch,id,known,new[]{new ItemStack()},null,null);}}
class Check{static void Main(){foreach(var action in new[]{QuickStackRadialAction.Deposit,QuickStackRadialAction.CompanionInventoryPull,QuickStackRadialAction.PullVehicleCredits,QuickStackRadialAction.PullDroneCredits,QuickStackRadialAction.PullVehicle,QuickStackRadialAction.PullDrone}){bool companion=LogisticsInventoryCredits.UsesReceipt(action);Subject.Reset(action);Subject.Deliver(98,3,true);Subject.Deliver(99,2,true);Subject.Deliver(99,3,false);if(LogisticsInventoryCredits.Calls+QuickStackRemotePlayerBagSync.Calls!=0)throw new Exception("unmatched/unknown delivery");Subject.Deliver(99,3,true);Subject.Deliver(99,3,true);if(LogisticsInventoryCredits.Calls!=(companion?1:0)||QuickStackRemotePlayerBagSync.Calls!=(companion?0:1))throw new Exception("duplicate/wrong receiver");}Console.WriteLine("PASS: companion credits vs standard snapshots selected correctly; unknown/old identity ignored; duplicate receipt applied once");}}

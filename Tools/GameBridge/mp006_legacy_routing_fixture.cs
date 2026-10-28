// Explicit native identity/connection/transport doubles. No item effects simulated.
using System;
class EntityPlayer{internal int entityId=7;}class EntityPlayerLocal:EntityPlayer{}
class World{internal EntityPlayerLocal Player=new EntityPlayerLocal();internal EntityPlayerLocal GetPrimaryPlayer()=>Player;}
class PersistentPlayerData{internal object PrimaryId=new object();}
class GameManager{internal static GameManager Instance=new GameManager();internal World World=new World();internal PersistentPlayerData Persistent=new PersistentPlayerData();internal PersistentPlayerData GetPersistentLocalPlayer()=>Persistent;}
class ConnectionManager{internal bool IsServer;}
static class SingletonMonoBehaviour<T> where T:new(){internal static T Instance=new T();}
static class QuickStackRuntimePolicy{internal static bool Enabled=true;}
static class Log{internal static void Warning(string message){}}
enum QuickStackRadialAction{Deposit,DepositOwned,Restock}
static class Probe{internal static int Native,Requests;internal static QuickStackRadialAction Action;internal static string Target,Token;internal static void Check(bool value,string why){if(!value)throw new Exception(why);}internal static void Reset(){Native=Requests=0;}}
static class RebirthLogisticsOriginalTransport
{internal static bool Active;internal static void Request(EntityPlayerLocal player,QuickStackRadialAction action,string target,string token){Probe.Requests++;Probe.Action=action;Probe.Target=target;Probe.Token=token;}}
static class RebirthLogisticsPlanning{internal static EntityPlayer Original;internal static bool IsOriginalOwner(EntityPlayer player)=>ReferenceEquals(Original,player);}
static class LogisticsTransferService
{
 internal const string LegacyDepositToken="deposit",LegacyRestockToken="restock";internal static bool NativeAvailable=true;
 internal static bool CanUseNativeOwner(EntityPlayer player)=>NativeAvailable;
 // ACTUAL_OWNER_GUARD
}
static class QuickStackService
{
 // ACTUAL_DEPOSIT
 internal static void ProcessServer(World world,int player,object identity,bool owned){Probe.Native++;}
}
static class QuickStackRestockService
{
 // ACTUAL_RESTOCK
 internal static void ProcessServer(World world,int player,object identity,bool owned){Probe.Native++;}
}
class NetPackage{public virtual void ProcessPackage(World world,GameManager callbacks){}}
class NetPackageQuickStackRequest:NetPackage
{
 // ACTUAL_OLD_DEPOSIT
}
class NetPackageQuickStackRestockRequest:NetPackage
{
 // ACTUAL_OLD_RESTOCK
}
static class Program
{
 static void Main()
 {
  var connection=SingletonMonoBehaviour<ConnectionManager>.Instance;
  foreach(bool owned in new[]{false,true})
  {
   Probe.Reset();connection.IsServer=false;RebirthLogisticsOriginalTransport.Active=true;QuickStackService.RequestLocal(owned);
   Probe.Check(Probe.Native==0&&Probe.Requests==1&&Probe.Action==(owned?QuickStackRadialAction.DepositOwned:QuickStackRadialAction.Deposit)&&Probe.Token==LogisticsTransferService.LegacyDepositToken,"remote deposit uses original command/selection");
   Probe.Reset();QuickStackRestockService.RequestLocal(owned);Probe.Check(Probe.Native==0&&Probe.Requests==1&&Probe.Action==QuickStackRadialAction.Restock&&Probe.Target==(owned?"owned":"")&&Probe.Token==LogisticsTransferService.LegacyRestockToken,"remote restock preserves owned filter");
   Probe.Reset();RebirthLogisticsOriginalTransport.Active=false;QuickStackService.RequestLocal(owned);QuickStackRestockService.RequestLocal(owned);Probe.Check(Probe.Native==0&&Probe.Requests==0,"unready protocol cannot fall back to stale remote replicas");
   connection.IsServer=true;QuickStackService.RequestLocal(owned);QuickStackRestockService.RequestLocal(owned);Probe.Check(Probe.Native==2&&Probe.Requests==0,"local authority entry remains reachable");
  }
  foreach(bool active in new[]{false,true}){RebirthLogisticsOriginalTransport.Active=active;Probe.Reset();new NetPackageQuickStackRequest().ProcessPackage(GameManager.Instance.World,GameManager.Instance);new NetPackageQuickStackRestockRequest().ProcessPackage(GameManager.Instance.World,GameManager.Instance);Probe.Check(Probe.Native==0&&Probe.Requests==0,"legacy wire has no mutation route");}
  var remote=new EntityPlayer();Probe.Check(!LogisticsTransferService.CanUseLegacyOwner(remote),"raw remote server replica refused");RebirthLogisticsPlanning.Original=remote;Probe.Check(LogisticsTransferService.CanUseLegacyOwner(remote),"exact detached original plan accepted");Probe.Check(!LogisticsTransferService.CanUseLegacyOwner(new EntityPlayer()),"another actor cannot borrow plan");LogisticsTransferService.NativeAvailable=false;Probe.Check(!LogisticsTransferService.CanUseLegacyOwner(remote),"original plan cannot bypass native admission");
  Console.WriteLine("PASS extracted legacy routing and owner admission; native transport/grids are doubles");
 }
}

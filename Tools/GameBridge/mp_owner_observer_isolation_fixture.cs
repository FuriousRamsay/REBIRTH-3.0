// Disclosed adapters only. Prefix bodies are extracted from production.
using System;
using System.Collections.Generic;
sealed class PlayerDataFile{}
sealed class ClientInfo{}
sealed class NativeFile{}
sealed class RemoteWitness{}
sealed class NetPackagePlayerData{internal ClientInfo Sender;internal PlayerDataFile playerDataFile;}
static class ThreadManager{internal static bool IsMainThread(){return true;}}
static class RebirthProductiveEarnedRemoteHost{internal static void EnterNativeToolOwnerSave(PlayerDataFile a,string b,string c,bool d,out object state){Probe.Enter("nativeTool",out state);}internal static void ObserveNativeToolOwnerSave(bool ran,object state){Probe.End("nativeTool");}internal static void EnterNativeToolOwner(ClientInfo peer,PlayerDataFile data,bool ran,out object state){Probe.Enter("nativeTool",out state);}}
static class Probe
{
 internal static readonly List<string> Seen=new List<string>();internal static string Fail;
 internal static void Enter(string id,out object state){Seen.Add(id);state=new object();if(Fail==id)throw new InvalidOperationException(id);}
 internal static void End(string id){Seen.Add(id);if(Fail==id)throw new InvalidOperationException(id);}
 internal static void Assert(bool value){if(!value)throw new Exception("isolation assertion");}
 internal static void Check(string[] expected){Probe.Assert(Seen.Count==expected.Length);for(int i=0;i<expected.Length;i++)Probe.Assert(Seen[i]==expected[i]);}
}
static class RebirthRemoteResourcePlacedStationOriginal{internal static void EnterCancellationOwnerSave(PlayerDataFile a,string b,string c,bool d,out object s){Probe.Enter("cancel",out s);}internal static void ObserveCancellationOwnerSave(bool ran,object state){Probe.End("cancel");}}
static class RebirthRecyclerPlacedSettlement{internal static void EnterRecyclerOwnerSave(PlayerDataFile a,string b,string c,bool d,out object s){Probe.Enter("recycler",out s);}internal static void ObserveRecyclerOwnerSave(bool ran,object state){Probe.End("recycler");}internal static void EndRecyclerOwnerSave(object state){Probe.End("recyclerEnd");}}
static class RebirthRecipeOutputIntegration{internal static void EnterRecipeOutputOwnerSave(PlayerDataFile a,string b,string c,bool d,out object s){Probe.Enter("recipeOutput",out s);}internal static void ObserveRecipeOutputOwnerSave(bool ran,object state){Probe.End("recipeOutput");}}
static class RemoteResourceTransactions
{internal static void EnterActionPaymentOwnerSave(PlayerDataFile a,string b,string c,bool d,out object s){Probe.Enter("actionPayment",out s);}internal static void EnterAcceptedActionPaymentOwner(ClientInfo a,PlayerDataFile b,bool c,out object s){Probe.Enter("actionPayment",out s);}internal static void ObserveActionPaymentOwnerSave(bool ran,object state){Probe.End("actionPayment");}}
static class RebirthDogLifecycleService
{internal static void EnterHireOwnerSave(PlayerDataFile a,string b,string c,bool d,out object s){Probe.Enter("hire",out s);}internal static void EnterHireAcceptedOwner(ClientInfo a,PlayerDataFile b,bool c,out object s){Probe.Enter("hire",out s);}internal static void ObserveHireOwnerSave(bool ran,object state){Probe.End("hire");}}
static class RebirthNpcMetabolismCustody
{internal static void EnterOwnerSave(PlayerDataFile a,string b,string c,bool d,out object s){Probe.Enter("metabolism",out s);}internal static void EnterAcceptedOwner(ClientInfo a,PlayerDataFile b,bool c,out object s){Probe.Enter("metabolism",out s);}internal static void ObserveOwnerSave(bool ran,object state){Probe.End("metabolism");}}
static class RebirthConsumptionDurableDelivery
{internal static void EnterInventoryOwnerSave(PlayerDataFile a,string b,string c,bool d,out object s){Probe.Enter("consumption",out s);}internal static void EnterInventoryAcceptedOwner(ClientInfo a,PlayerDataFile b,bool c,out object s){Probe.Enter("consumption",out s);}internal static void ObserveInventoryOwnerSave(bool ran,object state){Probe.End("consumption");}}
static class RebirthWorkstationCompletionPipeline{internal static void BeforeCachedOwnerEffectSave(PlayerDataFile a,string b,string c,bool d,out object s){Probe.Enter("cache",out s);}internal static void AfterCachedOwnerEffectSave(bool ran,object state){Probe.End("cache");}}
static class RebirthProductiveEarnedUiDeferredTransport
{
 internal static class OwnerPds
 {internal static void BeforeSave(PlayerDataFile a,string b,string c,bool d,out object s){Probe.Enter("host",out s);}internal static void ObserveAcceptedPrefix(ClientInfo a,PlayerDataFile b,bool c,out object s){Probe.Enter("host",out s);}internal static void AfterSave(bool ran,object state){Probe.End("host");}}
 internal static class NativeViewerPds
 {internal static void BeforeSave(PlayerDataFile a,string b,string c,bool d,out object s){Probe.Enter("viewer",out s);}internal static void AcceptedPrefix(ClientInfo a,PlayerDataFile b,bool c,out object s){Probe.Enter("viewer",out s);}internal static void AfterSave(bool ran,object state){Probe.End("viewer");}}
}
static class Save
{
 internal sealed class Observation{internal NativeFile Native;internal object HostPds,CancellationOwner,NativeViewer,CachedOwnerEffect,HireOwner,MetabolismOwner,ConsumptionOwner,RecyclerOwner,ActionPaymentOwner,RecipeOutputOwner,NativeToolOwner;internal bool Finished;}
 // ACTUAL_SAVE_PREFIX
 // ACTUAL_SAVE_FINISH
 private static void ReleaseDeferredSaveState(NativeFile state){Probe.End("release");}
 private static void PrefixLegacy(PlayerDataFile a,string b,string c,bool d,out NativeFile s){Probe.Seen.Add("legacy");s=new NativeFile();if(Probe.Fail=="legacy")throw new Exception();}
}
static class AcceptedPlayer
{
 internal static NetPackagePlayerData readingPlayer;
 internal sealed class Observation{internal RemoteWitness Native;internal object HostPds,NativeViewer,HireOwner,MetabolismOwner,ConsumptionOwner,ActionPaymentOwner,NativeToolOwner;}
 // ACTUAL_ACCEPT_PREFIX
 private static void PrefixLegacy(ClientInfo a,PlayerDataFile b,bool c,out RemoteWitness s){Probe.Seen.Add("legacy");s=new RemoteWitness();if(Probe.Fail=="legacy")throw new Exception();}
}
static class Program
{
 static void Main()
 {
  var file=new PlayerDataFile();var peer=new ClientInfo();
  var save=new[]{"nativeTool","recipeOutput","consumption","cancel","hire","metabolism","recycler","actionPayment","host","viewer","cache","legacy"};
  foreach(var fail in save){Probe.Seen.Clear();Probe.Fail=fail;Save.Observation state;Save.Prefix(file,"root","owner",true,out state);Probe.Assert(state!=null);Probe.Check(save);}
  var accepted=new[]{"nativeTool","hire","metabolism","consumption","actionPayment","host","viewer","legacy"};
  AcceptedPlayer.readingPlayer=new NetPackagePlayerData{Sender=peer,playerDataFile=file};
  foreach(var fail in accepted){Probe.Seen.Clear();Probe.Fail=fail;AcceptedPlayer.Observation state;AcceptedPlayer.Prefix(peer,file,true,out state);Probe.Assert(state!=null);Probe.Check(accepted);}
  Probe.Seen.Clear();Probe.Fail=null;AcceptedPlayer.Observation ignored;AcceptedPlayer.Prefix(new ClientInfo(),file,true,out ignored);Probe.Check(new[]{"legacy"});
  var finish=new[]{"nativeTool","consumption","recipeOutput","host","viewer","cache","cancel","hire","metabolism","recycler","recyclerEnd","actionPayment","release"};
  foreach(bool ran in new[]{false,true})foreach(var fail in finish){
   Probe.Fail=null;Save.Observation state;Save.Prefix(file,"root","owner",true,out state);Probe.Seen.Clear();Probe.Fail=fail;
   try{Save.Finish(state,ran);}catch(Exception){Probe.Assert(fail=="release");}Probe.Check(finish);
   Probe.Seen.Clear();Save.Finish(state,ran);Probe.Check(new string[0]);
   Probe.Assert(state.Finished&&state.ConsumptionOwner==null&&state.RecipeOutputOwner==null);
  }
  Console.WriteLine("PASS extracted observer prefixes: exception isolation and accepted source identity gate (offline adapters only)");
 }
}

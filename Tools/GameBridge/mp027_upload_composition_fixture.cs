// AUTHORED_NOT_RUN. Actual sole upload dispatcher with explicit purpose/native doubles.
using System;
using System.Reflection;
namespace HarmonyLib {class HarmonyPatch:Attribute {internal HarmonyPatch(Type type,string name){}}}
class World {}
class PlayerDataFile {internal int id;}
class NetPackagePlayerData {
 internal object Sender=new object();internal PlayerDataFile playerDataFile=new PlayerDataFile();
 internal bool ValidEntityIdForSender(int id)=>true;internal void ProcessPackage(){}
}
static class RebirthProductiveEarnedUiDeferredTransport {
 internal static class NativeViewerPds {internal static bool Allow=true;internal static bool CanProcessUpload(NetPackagePlayerData packet,bool run)=>Allow;}
}
static class RebirthRecyclerPlacedSettlement {
 internal static bool Allow=true,ThrowEnter,ThrowObserve,ThrowEnd;internal static int Enters,Observes,Ends;internal static bool Ran;
 internal static bool CanProcessRecyclerOwnerUpload(NetPackagePlayerData packet,World world,bool run)=>Allow;
 internal static void EnterRecyclerOwnerUpload(NetPackagePlayerData packet,World world,bool run,out object state){state=new object();Enters++;if(ThrowEnter)throw new Exception();}
 internal static void ObserveRecyclerOwnerUpload(NetPackagePlayerData packet,World world,bool run,object state){Observes++;Ran=run;if(ThrowObserve)throw new Exception();}
 internal static void EndRecyclerOwnerUpload(object state){Ends++;if(ThrowEnd)throw new Exception();}
}
class Actual {
 internal static NetPackagePlayerData readingPlayer;
 // ACTUAL_UPLOAD_DISPATCHER
 internal static void Run(){
  var type=typeof(Actual).GetNestedType("PlayerUpload",BindingFlags.NonPublic);
  var prefix=type.GetMethod("Prefix",BindingFlags.NonPublic|BindingFlags.Static);
  var postfix=type.GetMethod("Postfix",BindingFlags.NonPublic|BindingFlags.Static);
  var final=type.GetMethod("Finalizer",BindingFlags.NonPublic|BindingFlags.Static);
  var packet=new NetPackagePlayerData();var prior=new NetPackagePlayerData();var world=new World();
  Action<bool,bool> invoke=(expected,ran)=>{
   readingPlayer=prior;object[] args={packet,world,true,null};
   bool permit=(bool)prefix.Invoke(null,args);Require(permit==expected,"admission mismatch");
   if(permit)Require(ReferenceEquals(readingPlayer,prior??packet),"native upload did not receive exact packet scope");
   postfix.Invoke(null,new object[]{packet,world,ran,args[3]});
   var error=new InvalidOperationException("native failure");
   Require(ReferenceEquals(final.Invoke(null,new object[]{error,args[3]}),error),"exception replaced");
   Require(ReferenceEquals(readingPlayer,prior),"previous packet scope lost");
  };
  invoke(true,true);Require(RebirthRecyclerPlacedSettlement.Ran,"native return not observed");
  prior=null;invoke(true,true);prior=new NetPackagePlayerData();
  RebirthProductiveEarnedUiDeferredTransport.NativeViewerPds.Allow=false;
  int untouched=RebirthRecyclerPlacedSettlement.Enters;invoke(false,false);
  Require(RebirthRecyclerPlacedSettlement.Enters==untouched,"native-viewer refusal entered recycler");
  RebirthProductiveEarnedUiDeferredTransport.NativeViewerPds.Allow=true;
  RebirthRecyclerPlacedSettlement.Allow=false;int entries=RebirthRecyclerPlacedSettlement.Enters;invoke(false,false);
  Require(RebirthRecyclerPlacedSettlement.Enters==entries,"refused purpose entered");
  RebirthRecyclerPlacedSettlement.Allow=true;RebirthRecyclerPlacedSettlement.ThrowEnter=true;invoke(false,false);
  RebirthRecyclerPlacedSettlement.ThrowEnter=false;RebirthRecyclerPlacedSettlement.ThrowObserve=true;RebirthRecyclerPlacedSettlement.ThrowEnd=true;invoke(true,false);
  Require(!RebirthRecyclerPlacedSettlement.Ran,"skipped original became success");
  Console.WriteLine("PASS actual upload dispatcher against callback/native doubles; not native settlement");
 }
 static void Require(bool value,string message){if(!value)throw new Exception(message);}
}
class Program {static void Main()=>Actual.Run();}

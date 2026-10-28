using System;using System.Collections.Generic;
// Actual Release class. Native saved stage/history/inventory and actual outer
// hold-clear scope are disclosed adapters; no production provenance certified.
class RebirthNpcTradeOriginal{internal Guid Operation=Guid.NewGuid();internal string Image="original",Account="owner",Creation="creation",World="world";internal int Deaths;}
class EntityPlayerLocal{internal int entityId=1;internal Buffs Buffs=new Buffs();}
class Buffs{internal Dictionary<string,float> Values=new Dictionary<string,float>();internal float GetCustomVar(string key)=>Values.TryGetValue(key,out var value)?value:0;}
static class ThreadManager{internal static bool Main=true;internal static bool IsMainThread()=>Main;}
static class RebirthNpcTradeOwnerOriginalStore{internal static bool Final=true,Unused;internal static bool TryReadFinalDisposition(EntityPlayerLocal p,RebirthNpcTradeOriginal o,out bool unused){unused=Unused;return Final;}}
static class RebirthNpcTradeOwnerCheckpoint
{
 internal const string Held="held";internal static string HoldMarker(Guid op)=>"own";internal static string Marker(Guid op)=>"phase";
 internal static bool Saved=true;internal static int Step=4,Phase=3;
 internal static bool TryReadRetainedStage(EntityPlayerLocal p,RebirthNpcTradeOriginal o,out int step,out int phase){step=Step;phase=Phase;return Saved;}
}
static class RebirthStablePlayerIdentity{internal static bool Available=true;internal static bool TryFromLocalPlatform(out object owner){owner=new object();return Available;}}
class NativeSaved:Dictionary<string,float>{internal int id=1,deaths;internal bool bDead;}
static class RebirthGearNativePlayerFile{internal static bool Available=true;internal static NativeSaved Values=new NativeSaved();internal static bool TryRead(object o,out NativeSaved saved){saved=Values;return Available;}}
static class RebirthNpcTradeService{internal static class OwnerAdmission{internal static bool Policy=true;internal static bool MatchesNativePolicy(NativeSaved saved,RebirthNpcTradeOriginal o,EntityPlayerLocal p)=>Policy;}}
static class RebirthGearPlayerDataInventory{internal static bool Same=true;internal static int[] ReadSlots(NativeSaved saved,bool bag)=>new[]{bag?1:2};}
static class RebirthRemoteResourcePlacedStationSavedReceipt{internal static bool TryRead(Dictionary<string,float> saved,string key,out float value,out bool present){present=saved.TryGetValue(key,out value);return true;}}
static class RebirthNpcTradeOwnerExchange
{
 private sealed class Hold{internal EntityPlayerLocal Player=new EntityPlayerLocal();internal RebirthNpcTradeOriginal Original=new RebirthNpcTradeOriginal();internal bool Entered,NeedsSave,Metadata=true,Prefix=true;internal int Progress=2;internal object Grant,LocalGrant;internal List<int> Changes=new List<int>{0,1};}
 private static Hold releasing;private static Dictionary<Guid,Hold> holds=new Dictionary<Guid,Hold>();
 private static bool MetadataCurrent(Hold hold)=>hold.Metadata;
 private static bool Prefix(Hold hold,int progress)=>hold.Prefix;
 private static string Step(Guid operation)=>"step";
 private static int[][] Expected(Hold hold,int progress)=>new[]{new[]{1},new[]{2}};
 private static bool Equal(int[] actual,int[] expected)=>RebirthGearPlayerDataInventory.Same;
 // ACTUAL_RELEASE_WITNESS
 internal static void Run()
 {
  int checks=0;void Check(bool value){checks++;if(!value)throw new Exception("Actual release witness assertion "+checks);}
  Hold Seed(){var h=new Hold();holds.Clear();holds.Add(h.Original.Operation,h);releasing=h;ThreadManager.Main=true;RebirthNpcTradeOwnerOriginalStore.Final=true;RebirthNpcTradeOwnerOriginalStore.Unused=false;RebirthNpcTradeOwnerCheckpoint.Saved=true;RebirthNpcTradeOwnerCheckpoint.Step=4;RebirthNpcTradeOwnerCheckpoint.Phase=3;RebirthStablePlayerIdentity.Available=RebirthGearNativePlayerFile.Available=true;RebirthGearNativePlayerFile.Values=new NativeSaved{{"phase",3},{"step",4}};RebirthNpcTradeService.OwnerAdmission.Policy=RebirthGearPlayerDataInventory.Same=true;return h;}
  var hold=Seed();var proof=Release.CaptureCurrent();Check(proof!=null&&proof.Current());
  releasing=null;Check(Release.CaptureCurrent()==null&&!proof.Current());
  hold=Seed();proof=Release.CaptureCurrent();ThreadManager.Main=false;Check(!proof.Current());
  hold=Seed();proof=Release.CaptureCurrent();holds.Clear();Check(!proof.Current());
  hold=Seed();proof=Release.CaptureCurrent();hold.Entered=true;Check(!proof.Current());
  hold=Seed();proof=Release.CaptureCurrent();hold.NeedsSave=true;Check(!proof.Current());
  hold=Seed();proof=Release.CaptureCurrent();hold.Metadata=false;Check(!proof.Current());
  hold=Seed();proof=Release.CaptureCurrent();hold.Prefix=false;Check(!proof.Current());
  hold=Seed();proof=Release.CaptureCurrent();RebirthNpcTradeOwnerOriginalStore.Final=false;Check(!proof.Current());
  hold=Seed();proof=Release.CaptureCurrent();RebirthNpcTradeOwnerCheckpoint.Saved=false;Check(!proof.Current());
  hold=Seed();proof=Release.CaptureCurrent();RebirthNpcTradeOwnerCheckpoint.Step=3;Check(!proof.Current());
  hold=Seed();proof=Release.CaptureCurrent();RebirthNpcTradeOwnerCheckpoint.Phase=2;Check(!proof.Current());
  hold=Seed();proof=Release.CaptureCurrent();hold.Player.Buffs.Values["held"]=1;Check(!proof.Current());
  hold=Seed();proof=Release.CaptureCurrent();RebirthGearNativePlayerFile.Values["own"]=1;Check(!proof.Current());
  hold=Seed();proof=Release.CaptureCurrent();RebirthGearNativePlayerFile.Available=false;Check(!proof.Current());
  hold=Seed();proof=Release.CaptureCurrent();RebirthNpcTradeOwnerOriginalStore.Unused=true;hold.Progress=0;RebirthNpcTradeOwnerCheckpoint.Step=0;RebirthNpcTradeOwnerCheckpoint.Phase=1;RebirthGearNativePlayerFile.Values["phase"]=1;RebirthGearNativePlayerFile.Values["step"]=0;Check(proof.Current());
  hold.Grant=new object();Check(!proof.Current());hold.Grant=null;
  Check(proof.Matches(hold.Original)&&!proof.Matches(new RebirthNpcTradeOriginal{Image="different"}));
  Check(proof.MatchesNative(hold.Original.Operation,"owner","creation","world",0)&&!proof.MatchesNative(hold.Original.Operation,"other","creation","world",0));
  releasing=null;Check(!proof.Current());
  hold=Seed();proof=Release.CaptureCurrent();RebirthGearNativePlayerFile.Values.id=99;Check(!proof.Current());
  hold=Seed();proof=Release.CaptureCurrent();RebirthGearPlayerDataInventory.Same=false;Check(!proof.Current());
  hold=Seed();proof=Release.CaptureCurrent();RebirthNpcTradeService.OwnerAdmission.Policy=false;Check(!proof.Current());
  hold=Seed();proof=Release.CaptureCurrent();RebirthGearNativePlayerFile.Values["phase"]=2;Check(!proof.Current());
  Console.WriteLine(checks+" actual release witness assertions passed; native history/stage/scope adapters NOT certified");
 }
}
class Program{static void Main()=>RebirthNpcTradeOwnerExchange.Run();}

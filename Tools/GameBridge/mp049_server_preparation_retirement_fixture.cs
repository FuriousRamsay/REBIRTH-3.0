// AUTHORED_NOT_RUN. Actual server capture, tracker and both bundle observers.
// Closed/history native completion and social readback sources are adapters.
using System;using System.Collections.Generic;using System.Linq;
class World{internal class State{internal string Guid=System.Guid.NewGuid().ToString();}internal State worldState=new State();internal bool Remote;internal bool IsRemote()=>Remote;}
static class ThreadManager{internal static bool Main=true;internal static bool IsMainThread()=>Main;}
class RebirthNpcTradeOriginal{internal string Image,World;}
class RebirthNpcTradeClosedHistory{internal bool HasSavedSocialCompletion=true;internal string Image;internal bool MatchesOriginal(RebirthNpcTradeOriginal value)=>value.Image==Image;}
public static partial class RebirthNpcTradeService{
 internal class HostPrepared{internal RebirthNpcTradeOriginal Original;internal HostClosed ClosedRecord;}
 internal class HostClosed{internal bool HasSavedSocialCompletion=true,HasObservedSavedSocialCompletion;internal HostPrepared Prepared;}
 class QuoteRecord{internal HostPrepared OwnerPreparation;}
 static object Sync=new object();static Dictionary<Guid,QuoteRecord> Quotes=new Dictionary<Guid,QuoteRecord>();const int MaxQuotes=512;static World tradeWorld;
 internal static void Seed(World world,params RebirthNpcTradeOriginal[] originals){tradeWorld=world;Quotes=originals.ToDictionary(value=>Guid.NewGuid(),value=>new QuoteRecord{OwnerPreparation=new HostPrepared{Original=value}});}
 internal static void ForgetBinding(){tradeWorld=null;}
 internal static void CloseFirst(bool saved,bool foreignPreparation=false){var prepared=Quotes.Values.First().OwnerPreparation;prepared.ClosedRecord=new HostClosed{HasSavedSocialCompletion=false,HasObservedSavedSocialCompletion=saved,Prepared=foreignPreparation?new HostPrepared{Original=prepared.Original}:prepared};}
 // ACTUAL_SERVER_CAPTURE
}
// ACTUAL_SERVER_TRACKER
class GameManager{internal static GameManager Instance=new GameManager();internal World World;}static class GameIO{internal static string GetSaveGameDir()=>null;}
internal static class RebirthNpcSaveQuarantineBundle{
 // Admission remains excluded; these observers cannot issue native namespace receipts.
 private static void RetryServerNativeAdmission(World world,string directory){}
 class Retained{internal bool Detached=true,RollbackIncomplete;internal RebirthNpcTradeServerRetirement ServerPreparationRetirement;internal object Domains=new object();}
 static List<Retained> retained=new List<Retained>();static bool transferring;internal static readonly bool AdmissionEnabled=false;
 // ACTUAL_HOST_COMPLETION_OBSERVER
 // ACTUAL_HISTORY_COMPLETION_OBSERVER
 static World outgoing;static Retained bundle;static RebirthNpcTradeOriginal first,second;static int checks;
 static void Seed(){ThreadManager.Main=true;transferring=false;outgoing=new World();first=new RebirthNpcTradeOriginal{Image="world-A/first/full original",World=outgoing.worldState.Guid};second=new RebirthNpcTradeOriginal{Image="world-A/second/full original",World=outgoing.worldState.Guid};RebirthNpcTradeService.Seed(outgoing,first,second);bundle=new Retained{ServerPreparationRetirement=RebirthNpcTradeService.CaptureServerPreparationRetirement(outgoing)};retained=new List<Retained>{bundle};}
 static RebirthNpcTradeService.HostClosed Host(RebirthNpcTradeOriginal original,bool saved=true)=>new RebirthNpcTradeService.HostClosed{HasSavedSocialCompletion=saved,Prepared=new RebirthNpcTradeService.HostPrepared{Original=original}};
 static void Need(bool value){if(!value)throw new Exception("server preparation retirement "+checks);checks++;}
 static void Refuses(Action action){bool refused=false;try{action();}catch(InvalidOperationException){refused=true;}Need(refused);}
 public static void Main(){
  Seed();var domains=bundle.Domains;Need(!bundle.ServerPreparationRetirement.Complete);ObserveServerTradeCompletion(Host(first,false));Need(!bundle.ServerPreparationRetirement.Complete);ObserveServerTradeCompletion(Host(new RebirthNpcTradeOriginal{Image="foreign"}));Need(!bundle.ServerPreparationRetirement.Complete);ObserveServerTradeCompletion(Host(first));Need(!bundle.ServerPreparationRetirement.Complete);
  RebirthNpcTradeService.Seed(new World(),new RebirthNpcTradeOriginal{Image="incoming active original"});ObserveServerTradeCompletion(new RebirthNpcTradeClosedHistory{Image=second.Image});Need(bundle.ServerPreparationRetirement.Complete);Need(bundle.Detached&&ReferenceEquals(bundle.Domains,domains)&&retained.Contains(bundle)&&!AdmissionEnabled);
  Seed();ObserveServerTradeCompletion(new RebirthNpcTradeClosedHistory{Image=first.Image,HasSavedSocialCompletion=false});ObserveServerTradeCompletion(Host(second));Need(!bundle.ServerPreparationRetirement.Complete);
  Seed();ThreadManager.Main=false;ObserveServerTradeCompletion(Host(first));ObserveServerTradeCompletion(new RebirthNpcTradeClosedHistory{Image=second.Image});Need(!bundle.ServerPreparationRetirement.Complete);ThreadManager.Main=true;
  Seed();transferring=true;ObserveServerTradeCompletion(Host(first));ObserveServerTradeCompletion(new RebirthNpcTradeClosedHistory{Image=second.Image});Need(!bundle.ServerPreparationRetirement.Complete);
  Seed();bundle.RollbackIncomplete=true;ObserveServerTradeCompletion(Host(first));ObserveServerTradeCompletion(new RebirthNpcTradeClosedHistory{Image=second.Image});Need(!bundle.ServerPreparationRetirement.Complete);
  Seed();Refuses(()=>RebirthNpcTradeService.CaptureServerPreparationRetirement(new World()));Refuses(()=>RebirthNpcTradeService.CaptureServerPreparationRetirement(new World{Remote=true}));Refuses(()=>RebirthNpcTradeService.CaptureServerPreparationRetirement(null));
  Seed();RebirthNpcTradeService.Seed(outgoing,new RebirthNpcTradeOriginal[]{null});Refuses(()=>RebirthNpcTradeService.CaptureServerPreparationRetirement(outgoing));
  Seed();RebirthNpcTradeService.ForgetBinding();Refuses(()=>RebirthNpcTradeService.CaptureServerPreparationRetirement(outgoing));
  Seed();first.World=Guid.NewGuid().ToString();Refuses(()=>RebirthNpcTradeService.CaptureServerPreparationRetirement(outgoing));
  Seed();second.World="invalid";Refuses(()=>RebirthNpcTradeService.CaptureServerPreparationRetirement(outgoing));
  Seed();outgoing.worldState=null;Refuses(()=>RebirthNpcTradeService.CaptureServerPreparationRetirement(outgoing));
  Seed();RebirthNpcTradeService.Seed(outgoing,first);RebirthNpcTradeService.CloseFirst(true);Need(RebirthNpcTradeService.CaptureServerPreparationRetirement(outgoing).Complete);
  Seed();RebirthNpcTradeService.Seed(outgoing,first);RebirthNpcTradeService.CloseFirst(false);Need(!RebirthNpcTradeService.CaptureServerPreparationRetirement(outgoing).Complete);
  Seed();RebirthNpcTradeService.Seed(outgoing,first);RebirthNpcTradeService.CloseFirst(true,true);Need(!RebirthNpcTradeService.CaptureServerPreparationRetirement(outgoing).Complete);
  Need(new RebirthNpcTradeServerRetirement(new RebirthNpcTradeOriginal[0]).Complete);Refuses(()=>new RebirthNpcTradeServerRetirement(new RebirthNpcTradeOriginal[513]));
  Console.WriteLine("PASS "+checks+" actual server preparation retirement assertions; native/social completion excluded");
 }
}

// AUTHORED_NOT_RUN. Production capture/tracker/bundle observer. Quote journal
// and typed unused native lineage are adapters; their full source cohorts differ.
using System;using System.Linq;using System.Collections.Generic;
class World{internal class State{internal string Guid=System.Guid.NewGuid().ToString();}internal State worldState=new State();internal bool Remote;internal bool IsRemote()=>Remote;}
static class ThreadManager{internal static bool Main=true;internal static bool IsMainThread()=>Main;}
public static partial class RebirthNpcTradeService{
 internal class HostUnused{internal bool Saved=true;internal string Identity;internal bool ReadRetained()=>Saved;}
 internal class HostQuote{internal Quote Quote;internal string World,Identity;internal bool HasSavedUnusedState;internal bool MatchesSavedUnused(HostUnused source)=>HasSavedUnusedState&&source.Saved&&source.Identity==Identity;}
 internal class Quote{internal Guid QuoteId=Guid.NewGuid();}
 class Record{internal Quote Quote=new Quote();internal object OwnerPreparation;internal HostQuote Issued;}
 static object Sync=new object();static World tradeWorld;const int MaxQuotes=512;static Dictionary<Guid,Record> Quotes=new Dictionary<Guid,Record>();
 internal static void Seed(World world,params HostQuote[] sources){tradeWorld=world;Quotes.Clear();foreach(var source in sources){var record=new Record{Issued=source};if(source!=null)source.Quote=record.Quote;Quotes.Add(record.Quote.QuoteId,record);}}
 internal static void MarkPrepared(){Quotes.Values.First().OwnerPreparation=new object();}
 // ACTUAL_ISSUED_CAPTURE
}
// ACTUAL_ISSUED_TRACKER
class GameManager{internal static GameManager Instance=new GameManager();internal World World;}static class GameIO{internal static string GetSaveGameDir()=>null;}
internal static class RebirthNpcSaveQuarantineBundle{
 // Admission remains excluded; these observers cannot issue native namespace receipts.
 private static void RetryServerNativeAdmission(World world,string directory){}
 class Retained{internal bool Detached=true,RollbackIncomplete;internal RebirthNpcTradeIssuedRetirement ServerIssuedRetirement;internal object Domains=new object();}
 static List<Retained> retained=new List<Retained>();static bool transferring;static readonly bool AdmissionEnabled=false;
 // ACTUAL_UNUSED_OBSERVER
 static World outgoing;static Retained bundle;static RebirthNpcTradeService.HostQuote first,second;static int checks;
 static void Seed(){ThreadManager.Main=true;transferring=false;outgoing=new World();first=new RebirthNpcTradeService.HostQuote{World=outgoing.worldState.Guid,Identity="first"};second=new RebirthNpcTradeService.HostQuote{World=outgoing.worldState.Guid,Identity="second"};RebirthNpcTradeService.Seed(outgoing,first,second);bundle=new Retained{ServerIssuedRetirement=RebirthNpcTradeService.CaptureServerIssuedRetirement(outgoing)};retained=new List<Retained>{bundle};}
 static void Observe(string identity,bool saved=true)=>ObserveServerUnusedTrade(new RebirthNpcTradeService.HostUnused{Identity=identity,Saved=saved});
 static void Need(bool value){if(!value)throw new Exception("issued retirement "+checks);checks++;}static void Refuses(Action action){bool refused=false;try{action();}catch(InvalidOperationException){refused=true;}Need(refused);}
 public static void Main(){
  Seed();Need(!bundle.ServerIssuedRetirement.Complete);Observe("first");Need(!bundle.ServerIssuedRetirement.Complete);first.HasSavedUnusedState=second.HasSavedUnusedState=true;Observe("first",false);Observe("foreign");Need(!bundle.ServerIssuedRetirement.Complete);Observe("first");Need(!bundle.ServerIssuedRetirement.Complete);
  RebirthNpcTradeService.Seed(new World());Observe("second");Need(bundle.ServerIssuedRetirement.Complete);Need(bundle.Detached&&bundle.Domains!=null&&retained.Contains(bundle)&&!AdmissionEnabled);
  Seed();first.HasSavedUnusedState=second.HasSavedUnusedState=true;ThreadManager.Main=false;Observe("first");Observe("second");Need(!bundle.ServerIssuedRetirement.Complete);ThreadManager.Main=true;
  Seed();first.HasSavedUnusedState=second.HasSavedUnusedState=true;transferring=true;Observe("first");Observe("second");Need(!bundle.ServerIssuedRetirement.Complete);
  Seed();first.HasSavedUnusedState=second.HasSavedUnusedState=true;bundle.RollbackIncomplete=true;Observe("first");Observe("second");Need(!bundle.ServerIssuedRetirement.Complete);
  Seed();first.HasSavedUnusedState=second.HasSavedUnusedState=true;Need(RebirthNpcTradeService.CaptureServerIssuedRetirement(outgoing).Complete);
  Seed();Refuses(()=>RebirthNpcTradeService.CaptureServerIssuedRetirement(new World()));Refuses(()=>RebirthNpcTradeService.CaptureServerIssuedRetirement(new World{Remote=true}));Refuses(()=>RebirthNpcTradeService.CaptureServerIssuedRetirement(null));
  Seed();first.World=Guid.NewGuid().ToString();Refuses(()=>RebirthNpcTradeService.CaptureServerIssuedRetirement(outgoing));
  Seed();RebirthNpcTradeService.Seed(outgoing,new RebirthNpcTradeService.HostQuote[]{null});Refuses(()=>RebirthNpcTradeService.CaptureServerIssuedRetirement(outgoing));RebirthNpcTradeService.MarkPrepared();Need(RebirthNpcTradeService.CaptureServerIssuedRetirement(outgoing).Complete);
  Need(new RebirthNpcTradeIssuedRetirement(new RebirthNpcTradeService.HostQuote[0]).Complete);Refuses(()=>new RebirthNpcTradeIssuedRetirement(new RebirthNpcTradeService.HostQuote[513]));
  Console.WriteLine("PASS "+checks+" production server issued quote retirement assertions; journal/native producers adapted");
 }
}

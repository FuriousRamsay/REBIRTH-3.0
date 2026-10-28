// AUTHORED_NOT_RUN. Actual local-attempt/hold capture providers, actual custody
// tracker and actual bundle observer. Native saved release Current is adapted.
using System;using System.Collections.Generic;using System.Linq;
class World{internal bool Remote;internal bool IsRemote()=>Remote;}
class EntityPlayerLocal{internal World world;}
class RebirthNpcTradeOriginal{internal string Image;}
static class ThreadManager{internal static bool Main=true;internal static bool IsMainThread()=>Main;}
static class RebirthNpcTradeOwnerExchange{
 internal sealed class Release{internal bool Valid=true;internal string Image;internal bool Current()=>Valid;internal bool Matches(RebirthNpcTradeOriginal original)=>Image==original.Image;}
 class Hold{internal RebirthNpcTradeOriginal Original;}
 static Dictionary<Guid,Hold> holds=new Dictionary<Guid,Hold>();static object permit,releasing;
 internal static void Seed(RebirthNpcTradeOriginal original){holds.Clear();holds.Add(Guid.NewGuid(),new Hold{Original=original});permit=releasing=null;}
 internal static void Busy(){permit=new object();}
 // ACTUAL_HOLD_CAPTURE
}
public static partial class RebirthNpcTradeService{
 class LocalAttempt{internal EntityPlayerLocal Player;internal RebirthNpcTradeOriginal Original;}
 static Dictionary<Guid,LocalAttempt> localAttempts=new Dictionary<Guid,LocalAttempt>();
 internal static void Seed(World world,params RebirthNpcTradeOriginal[] originals){localAttempts=originals.Select(original=>new LocalAttempt{Player=new EntityPlayerLocal{world=world},Original=original}).ToDictionary(value=>Guid.NewGuid());}
 // ACTUAL_LOCAL_CAPTURE
}
// ACTUAL_TRADE_TRACKER
class GameManager{internal static GameManager Instance=new GameManager();internal World World;}static class GameIO{internal static string GetSaveGameDir()=>null;}
internal static class RebirthNpcSaveQuarantineBundle{
 // Admission remains excluded; these observers cannot issue native namespace receipts.
 private static void RetryServerNativeAdmission(World world,string directory){}
 class Retained{internal bool Detached=true,RollbackIncomplete,ClientFinancialCustody;internal RebirthNpcTradeClientRetirement ServerLocalTradeRetirement,TradeRetirement;internal ObservationRetirement ServerTradeObservationRetirement;internal object[] Domains=new[]{new object()};}
 class ObservationRetirement{internal void Observe(RebirthNpcTradeOwnerExchange.Release release){}}
 static List<Retained> retained=new List<Retained>();static bool transferring;internal static readonly bool AdmissionEnabled=false;
 // ACTUAL_BUNDLE_TRADE_OBSERVER
 static int checks;static World outgoing;static RebirthNpcTradeOriginal first,second,clientOriginal;static Retained server,client;
 static void Seed(){ThreadManager.Main=true;transferring=false;outgoing=new World();first=new RebirthNpcTradeOriginal{Image="world-A/owner/creation/life/first"};second=new RebirthNpcTradeOriginal{Image="world-A/owner/creation/life/second"};clientOriginal=new RebirthNpcTradeOriginal{Image="world-C/client/creation/life/third"};RebirthNpcTradeService.Seed(outgoing,first,second);RebirthNpcTradeOwnerExchange.Seed(first);
  server=new Retained{ServerLocalTradeRetirement=new RebirthNpcTradeClientRetirement(RebirthNpcTradeService.CaptureLocalOriginalRetirement(outgoing),RebirthNpcTradeOwnerExchange.CaptureRetirement())};client=new Retained{ClientFinancialCustody=true,TradeRetirement=new RebirthNpcTradeClientRetirement(new[]{new Func<RebirthNpcTradeOwnerExchange.Release,bool>(release=>release.Matches(clientOriginal))})};retained=new List<Retained>{server,client};
 }
 static void Observe(string image,bool valid=true)=>ObserveTradeRelease(new RebirthNpcTradeOwnerExchange.Release{Image=image,Valid=valid});
 static void Need(bool value){if(!value)throw new Exception("server local trade retirement "+checks);checks++;}
 static void Refuses(Action action){bool refused=false;try{action();}catch(InvalidOperationException){refused=true;}Need(refused);}
 public static void Main(){
  Seed();var domains=server.Domains;Need(!server.ServerLocalTradeRetirement.Complete);Observe("foreign");Need(!server.ServerLocalTradeRetirement.Complete);Observe(first.Image,false);Need(!server.ServerLocalTradeRetirement.Complete);Observe(first.Image);Need(!server.ServerLocalTradeRetirement.Complete);
  RebirthNpcTradeService.Seed(new World(),new RebirthNpcTradeOriginal{Image="new world original"});RebirthNpcTradeOwnerExchange.Seed(new RebirthNpcTradeOriginal{Image="new world hold"});Observe(second.Image);Need(server.ServerLocalTradeRetirement.Complete);Need(ReferenceEquals(server.Domains,domains)&&server.Detached&&!server.ClientFinancialCustody&&retained.Contains(server)&&!AdmissionEnabled);Need(client.ClientFinancialCustody&&!client.TradeRetirement.Complete);Observe(clientOriginal.Image);Need(!client.ClientFinancialCustody&&client.TradeRetirement.Complete);
  Seed();ThreadManager.Main=false;Observe(first.Image);Observe(second.Image);Need(!server.ServerLocalTradeRetirement.Complete);ThreadManager.Main=true;
  Seed();transferring=true;Observe(first.Image);Observe(second.Image);Need(!server.ServerLocalTradeRetirement.Complete);
  Seed();server.RollbackIncomplete=true;Observe(first.Image);Observe(second.Image);Need(!server.ServerLocalTradeRetirement.Complete);
  Seed();Refuses(()=>RebirthNpcTradeService.CaptureLocalOriginalRetirement(new World{Remote=true}));Refuses(()=>RebirthNpcTradeService.CaptureLocalOriginalRetirement(null));Refuses(()=>RebirthNpcTradeService.CaptureLocalOriginalRetirement(new World()));
  Seed();RebirthNpcTradeService.Seed(outgoing,new RebirthNpcTradeOriginal[]{null});Refuses(()=>RebirthNpcTradeService.CaptureLocalOriginalRetirement(outgoing));
  Seed();ThreadManager.Main=false;Refuses(()=>RebirthNpcTradeService.CaptureLocalOriginalRetirement(outgoing));ThreadManager.Main=true;
  Seed();RebirthNpcTradeOwnerExchange.Busy();Refuses(()=>RebirthNpcTradeOwnerExchange.CaptureRetirement());
  Console.WriteLine("PASS "+checks+" production detached server local trade correlation assertions; native saved release excluded");
 }
}

using System;using System.IO;using System.Text;using System.Xml;using System.Linq;using System.Collections.Generic;using System.Reflection;
// Actual client orchestration, with explicit native checkpoint/transport/token
// adapters. Reflection advances only the one-second schedule, never authority.
class World{internal EntityPlayerLocal Player;internal bool IsRemote()=>true;internal EntityPlayerLocal GetPrimaryPlayer()=>Player;}
class EntityPlayer{}
class EntityPlayerLocal:EntityPlayer{internal World world;}
class GameManager{internal static GameManager Instance=new GameManager();internal World World;internal int Uploads;internal void TriggerSendOfLocalPlayerDataFile(float delay){Uploads++;}}
static class ThreadManager{internal static bool IsMainThread()=>true;}
// Native reentry production is qualified by its separate source cohort.
static class RebirthNpcClientNativeReentry{internal static void Update(){}}
static class RebirthNpcTradeService{internal static object CaptureLocalOriginalAuditState(EntityPlayerLocal p)=>new{adapter=true};}
class RebirthNpcTradeQuote{internal Guid QuoteId=Guid.NewGuid(),NpcId=Guid.NewGuid();internal string ActorId="owner";}
class RebirthNpcTradeOriginal{internal Guid Operation;internal string Digest="original",Image="original-image";}
// ACTUAL_QUARANTINE_DOMAIN
// Native source adapter exercises orchestration only; cannot certify actual
// FromServer Read/PlayerId/ToPlayer provenance.
static class RebirthNpcTradeOwnerLoad
{
 internal sealed class Source{internal EntityPlayerLocal Player;internal Guid Operation;internal bool Valid=true;internal bool Current(EntityPlayerLocal p)=>Valid&&ReferenceEquals(p,Player);}
 internal static void Update(){}
 internal static object CaptureAuditState(EntityPlayerLocal p)=>new{available=true};
}
class RebirthNpcInteractionProjection{internal string ActorId="owner",SessionKey="session";internal Guid NpcId;}
enum RebirthNpcInteractionCommandStatus{Accepted,Rejected}
class RebirthNpcInteractionCommandResponse{internal RebirthNpcInteractionCommandStatus Status=RebirthNpcInteractionCommandStatus.Accepted;internal string Detail="result";}
static class RebirthNpcTradeOwnerOriginalStore
{
 internal static RebirthNpcTradeOriginal Original;internal static int Retains;internal static string Ack=new string('a',64);
 internal static bool IsRetained(EntityPlayerLocal p,RebirthNpcTradeOriginal o)=>ReferenceEquals(o,Original);
 internal static bool TryRetain(EntityPlayerLocal p,RebirthNpcTradeQuote q,out RebirthNpcTradeOriginal o){Retains++;o=Original;return o.Operation==q.QuoteId;}
 internal static bool AckAvailable=true;
 internal static bool TryReadAcknowledgement(EntityPlayerLocal p,RebirthNpcTradeOriginal o,out string terminal){terminal=Ack;return AckAvailable;}
 internal static bool Final,Unused;
 internal static bool TryReadFinalDisposition(EntityPlayerLocal p,RebirthNpcTradeOriginal o,out bool unused){unused=Unused;return Final&&IsRetained(p,o);}
 internal static bool TryReadRetained(EntityPlayerLocal p,Guid id,out RebirthNpcTradeOriginal o){o=Original;return o!=null&&o.Operation==id;}
}
static class RebirthNpcTradeOwnerExchange
{
 internal sealed class Release{internal bool Matches(RebirthNpcTradeOriginal original)=>false;}
 internal static int Phase=1,Applies,Acks,Releases;internal static bool Before=true,Resume=true;
 internal static object CaptureAuditState(EntityPlayerLocal p)=>new{available=true};
 internal static bool TryHoldBefore(EntityPlayerLocal p,RebirthNpcTradeOriginal o)=>Before;
 internal static bool TryResumeHeld(EntityPlayerLocal p,RebirthNpcTradeOriginal o,out int phase){phase=Phase;return Resume;}
 internal static bool TryApply(EntityPlayerLocal p,RebirthNpcInteractionNetworkClient.NativeTradeGrant g){Applies++;Phase=2;return true;}
 internal static bool TryAcknowledge(EntityPlayerLocal p,RebirthNpcInteractionNetworkClient.NativeTradeResult r){Acks++;Phase=3;return true;}
 internal static bool TryRelease(EntityPlayerLocal p,RebirthNpcInteractionNetworkClient.NativeTradeResult r){Releases++;return true;}
 internal static bool TryReleaseUnused(EntityPlayerLocal p,RebirthNpcInteractionNetworkClient.NativeTradeUnused u)=>false;
 internal static bool TryHoldLoaded(RebirthNpcTradeOwnerLoad.Source s,RebirthNpcTradeOriginal o,out int phase){phase=Phase;return Resume&&s.Current(s.Player);}
 internal static bool TryReleaseHistorical(RebirthNpcTradeOwnerLoad.Source s,RebirthNpcTradeOriginal o){Releases++;return Resume&&s.Current(s.Player);}
}
static class RebirthNpcInteractionNetworkClient
{
 internal static string Operation,Terminal;internal static RebirthNpcTradeOriginal Sent;
 internal static Action<RebirthNpcInteractionProjection,RebirthNpcInteractionCommandResponse,string> Reply;
 internal static NativeTradeGrant Grant;internal static NativeTradeResult Result;
 internal static void RequestNativeTrade(RebirthNpcTradeOriginal o,string session,string operation,Action<RebirthNpcInteractionProjection,RebirthNpcInteractionCommandResponse,string> callback,string terminal=null)
 {Sent=o;Operation=operation;Reply=callback;Terminal=terminal;}
 internal sealed class NativeTradeGrant{internal string GrantDigest="grant";internal bool Valid=true;internal bool Current()=>Valid;internal static bool TryCapture(EntityPlayerLocal p,RebirthNpcTradeOriginal o,out NativeTradeGrant grant){grant=Grant;return grant!=null&&grant.Valid;}}
 internal sealed class NativeTradeResult{internal bool Valid=true,Closed;internal string TerminalDigest=new string('b',64);internal bool Current()=>Valid;internal static bool TryCapture(EntityPlayerLocal p,RebirthNpcTradeOriginal o,out NativeTradeResult result){result=Result;return result!=null&&result.Valid;}}
 internal sealed class NativeTradeUnused{internal static bool TryCapture(EntityPlayerLocal p,RebirthNpcTradeOriginal o,out NativeTradeUnused unused){unused=null;return false;}}
}
// ACTUAL_CLIENT_FLOW
static class Program
{
 static int checks;
 static void Check(bool value){checks++;if(!value)throw new Exception("Actual native trade flow assertion "+checks);}
 static void Tick()
 {
  var attempts=(System.Collections.IDictionary)typeof(RebirthNpcTradeClientFlow).GetField("attempts",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
  foreach(var value in attempts.Values)value.GetType().GetField("Next",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(value,0L);
  RebirthNpcTradeClientFlow.Update();
 }
 static void Reply(){var callback=RebirthNpcInteractionNetworkClient.Reply;RebirthNpcInteractionNetworkClient.Reply=null;callback(null,new RebirthNpcInteractionCommandResponse(),null);}
 static RebirthNpcTradeOriginal Begin()
 {
  var q=new RebirthNpcTradeQuote();var original=RebirthNpcTradeOwnerOriginalStore.Original=new RebirthNpcTradeOriginal{Operation=q.QuoteId};
  RebirthNpcTradeOwnerExchange.Before=true;RebirthNpcTradeOwnerExchange.Resume=true;RebirthNpcTradeOwnerExchange.Phase=1;
  Check(RebirthNpcTradeClientFlow.Begin(q,new RebirthNpcInteractionProjection{NpcId=q.NpcId},null));return original;
 }
 static void Main()
 {
  var world=new World();world.Player=new EntityPlayerLocal{world=world};GameManager.Instance.World=world;
  var original=Begin();Check(RebirthNpcInteractionNetworkClient.Operation=="prepareNative");
  RebirthNpcInteractionNetworkClient.Grant=new RebirthNpcInteractionNetworkClient.NativeTradeGrant();Reply();
  RebirthNpcInteractionNetworkClient.Grant.Valid=false;RebirthNpcTradeOwnerExchange.Before=false;Tick();
  Check(RebirthNpcInteractionNetworkClient.Operation=="prepareNative"&&ReferenceEquals(RebirthNpcInteractionNetworkClient.Sent,original)&&RebirthNpcTradeOwnerExchange.Applies==0&&RebirthNpcTradeOwnerOriginalStore.Retains==1);
  RebirthNpcInteractionNetworkClient.Grant=new RebirthNpcInteractionNetworkClient.NativeTradeGrant();Reply();Tick();
  Check(RebirthNpcInteractionNetworkClient.Operation=="settleNative"&&RebirthNpcTradeOwnerExchange.Applies==1);
  RebirthNpcInteractionNetworkClient.Result=new RebirthNpcInteractionNetworkClient.NativeTradeResult();Reply();RebirthNpcInteractionNetworkClient.Result.Valid=false;Tick();
  Check(RebirthNpcInteractionNetworkClient.Operation=="settleNative"&&RebirthNpcTradeOwnerExchange.Acks==0&&RebirthNpcTradeOwnerExchange.Applies==1);
  RebirthNpcInteractionNetworkClient.Result=new RebirthNpcInteractionNetworkClient.NativeTradeResult();Reply();Tick();
  Check(RebirthNpcInteractionNetworkClient.Operation=="ackNative"&&RebirthNpcInteractionNetworkClient.Terminal==RebirthNpcInteractionNetworkClient.Result.TerminalDigest&&RebirthNpcTradeOwnerExchange.Acks==1);
  RebirthNpcInteractionNetworkClient.Result=new RebirthNpcInteractionNetworkClient.NativeTradeResult{Closed=true};Reply();Check(RebirthNpcTradeOwnerExchange.Releases==1);
  original=Begin();RebirthNpcInteractionNetworkClient.Grant=new RebirthNpcInteractionNetworkClient.NativeTradeGrant();Reply();Tick();
  RebirthNpcInteractionNetworkClient.Result=new RebirthNpcInteractionNetworkClient.NativeTradeResult();Reply();RebirthNpcInteractionNetworkClient.Result.Valid=false;
  RebirthNpcTradeOwnerExchange.Phase=3;Tick();
  Check(RebirthNpcInteractionNetworkClient.Operation=="ackNative"&&RebirthNpcInteractionNetworkClient.Terminal==RebirthNpcTradeOwnerOriginalStore.Ack&&ReferenceEquals(RebirthNpcInteractionNetworkClient.Sent,original)&&RebirthNpcTradeOwnerExchange.Acks==1);
  RebirthNpcInteractionNetworkClient.Result=new RebirthNpcInteractionNetworkClient.NativeTradeResult{Closed=true,TerminalDigest=RebirthNpcTradeOwnerOriginalStore.Ack};Reply();Check(RebirthNpcTradeOwnerExchange.Releases==2);
  original=Begin();RebirthNpcInteractionNetworkClient.Grant=new RebirthNpcInteractionNetworkClient.NativeTradeGrant();Reply();RebirthNpcInteractionNetworkClient.Grant.Valid=false;
  RebirthNpcTradeOwnerExchange.Resume=false;int applies=RebirthNpcTradeOwnerExchange.Applies;Tick();Check(RebirthNpcTradeOwnerExchange.Applies==applies&&RebirthNpcInteractionNetworkClient.Reply==null);
  // Actual ResumeLoaded orchestration, with explicit source/checkpoint adapters.
  world.Player=new EntityPlayerLocal{world=world};
  original=RebirthNpcTradeOwnerOriginalStore.Original=new RebirthNpcTradeOriginal{Operation=Guid.NewGuid()};
  var source=new RebirthNpcTradeOwnerLoad.Source{Player=world.Player,Operation=original.Operation};
  RebirthNpcTradeOwnerExchange.Resume=true;RebirthNpcTradeOwnerExchange.Phase=2;
  int oldApplies=RebirthNpcTradeOwnerExchange.Applies,oldRetains=RebirthNpcTradeOwnerOriginalStore.Retains;
  Check(RebirthNpcTradeClientFlow.ResumeLoaded(source));
  Check(RebirthNpcInteractionNetworkClient.Operation=="settleNative"&&ReferenceEquals(RebirthNpcInteractionNetworkClient.Sent,original)&&RebirthNpcTradeOwnerExchange.Applies==oldApplies&&RebirthNpcTradeOwnerOriginalStore.Retains==oldRetains);
  source.Valid=false;Check(!RebirthNpcTradeClientFlow.ResumeLoaded(source));source.Valid=true;
  RebirthNpcTradeOwnerOriginalStore.Final=true;RebirthNpcTradeOwnerOriginalStore.Unused=false;
  int oldReleases=RebirthNpcTradeOwnerExchange.Releases;
  Check(RebirthNpcTradeClientFlow.ResumeLoaded(source)&&RebirthNpcTradeOwnerExchange.Releases==oldReleases+1&&RebirthNpcTradeOwnerExchange.Applies==oldApplies);
  RebirthNpcTradeOwnerOriginalStore.Final=false;
  original=RebirthNpcTradeOwnerOriginalStore.Original=new RebirthNpcTradeOriginal{Operation=Guid.NewGuid()};source.Operation=original.Operation;
  RebirthNpcTradeOwnerExchange.Phase=3;RebirthNpcTradeOwnerOriginalStore.AckAvailable=false;
  Check(!RebirthNpcTradeClientFlow.ResumeLoaded(source));
  RebirthNpcTradeOwnerOriginalStore.AckAvailable=true;Check(RebirthNpcTradeClientFlow.ResumeLoaded(source));
  Check(RebirthNpcInteractionNetworkClient.Operation=="ackNative"&&RebirthNpcInteractionNetworkClient.Terminal==RebirthNpcTradeOwnerOriginalStore.Ack&&RebirthNpcTradeOwnerExchange.Applies==oldApplies);
  // The retained first attempt belongs to the SAME native World; capture all
  // actual flow originals together and preserve them through pointer rollback.
  Check(RebirthNpcTradeClientFlow.HasLifecycleCustody);
  bool foreignRefused=false;try{RebirthNpcTradeClientFlow.PrepareQuarantine(new World());}catch(InvalidOperationException){foreignRefused=true;}Check(foreignRefused);
  var domain=RebirthNpcTradeClientFlow.PrepareQuarantine(world);Check(domain.IsCurrent);
  var evidence=new XmlDocument();evidence.AppendChild(evidence.CreateElement("evidence"));var body=domain.CopyEvidence(evidence);
  Check(body.SelectNodes("tradeClientFlow/attempt").Count==2&&body.InnerText.Contains("original-image"));
  domain.Detach();Check(domain.IsDetached&&!RebirthNpcTradeClientFlow.HasLifecycleCustody);
  domain.Rollback();Check(domain.IsCurrent&&RebirthNpcTradeClientFlow.HasLifecycleCustody);
  var repeated=new RebirthNpcTradeQuote{QuoteId=original.Operation};var projection=new RebirthNpcInteractionProjection{NpcId=repeated.NpcId};
  Action<bool,string> firstFeedback=(ok,text)=>{},secondFeedback=(ok,text)=>{if(ok)GameManager.Instance.Uploads+=0;};
  Check(RebirthNpcTradeClientFlow.Begin(repeated,projection,firstFeedback));
  domain=RebirthNpcTradeClientFlow.PrepareQuarantine(world);Check(domain.IsCurrent);
  Check(RebirthNpcTradeClientFlow.Begin(repeated,projection,secondFeedback));Check(!domain.IsCurrent);
  Console.WriteLine(checks+" actual trade flow assertions passed; native transport/checkpoint/token adapters not certified");
 }
}

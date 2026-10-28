using System;using System.Linq;using System.Threading;using System.Collections.Generic;
// Actual local orchestration. Native admission/permissions/saves/NPC settlement,
// close/release/social and quarantine domain are explicit adapters.
class World{internal EntityPlayerLocal Player;internal WorldState worldState=new WorldState();internal bool IsRemote()=>false;internal EntityPlayerLocal GetPrimaryPlayer()=>Player;internal class WorldState{internal string Guid="world";}}
class EntityPlayerLocal{internal World world;}
class GameManager{internal static GameManager Instance=new GameManager();internal World World;}
static class ThreadManager{internal static bool IsMainThread()=>true;}
class RebirthNpcInteractionContext{internal string ActorId="actor",SessionKey="session";internal Guid NpcId;}
class RebirthNpcTradeQuote{internal Guid QuoteId=Guid.NewGuid(),NpcId=Guid.NewGuid();internal string ActorId="actor";internal long ExpiresClockTicks=DateTime.UtcNow.AddMinutes(1).Ticks;internal int TotalPrice=10,Quantity=1;}
class RebirthNpcTradeOriginal{internal Guid Operation;internal string World="world",Digest="original",Image="full-original";internal RebirthNpcTradeQuote Quote;internal RebirthNpcTradeQuote CopyQuote()=>Quote;}
class RebirthNpcInteractionCommandResponse{internal bool Accepted;}
class RebirthNpcSaveQuarantineDomain{internal RebirthNpcSaveQuarantineDomain(string n,System.Xml.XmlDocument d,Func<bool> c,Action detach,Action restore){}}
static class RebirthNpcTradeOwnerOriginalStore{internal static bool Final;internal static int Retains;internal static RebirthNpcTradeOriginal Last;internal static bool TryRetain(EntityPlayerLocal p,RebirthNpcTradeQuote q,out RebirthNpcTradeOriginal o){Retains++;o=Last=new RebirthNpcTradeOriginal{Operation=q.QuoteId,Quote=q};return true;}internal static bool IsRetained(EntityPlayerLocal p,RebirthNpcTradeOriginal o)=>true;internal static bool TryReadRetained(EntityPlayerLocal p,Guid id,out RebirthNpcTradeOriginal o){o=Last;return o!=null&&o.Operation==id;}internal static bool TryReadAcknowledgement(EntityPlayerLocal p,RebirthNpcTradeOriginal o,out string terminal){terminal="terminal";return true;}internal static bool TryReadFinalDisposition(EntityPlayerLocal p,RebirthNpcTradeOriginal o,out bool unused){unused=false;return Final;}}
class RebirthNpcTradeClosedHistory{internal bool TrySaveSocialCompletion()=>RebirthNpcSocialGameplayGateway.Ready;internal static bool TryReadLocal(RebirthNpcTradeLocalOwnerLoad.Source s,RebirthNpcTradeOriginal o,out RebirthNpcTradeClosedHistory h){h=RebirthNpcTradeOwnerOriginalStore.Final?new RebirthNpcTradeClosedHistory():null;return h!=null;}}
static class RebirthNpcTradeLocalOwnerLoad
{
 internal class Source{internal EntityPlayerLocal Player;internal Guid Operation;internal bool Valid=true;internal int Phase=2;internal bool Current(EntityPlayerLocal p)=>Valid&&ReferenceEquals(p,Player);internal bool Matches(RebirthNpcTradeOriginal o)=>Valid&&o.Operation==Operation;internal bool Transfer(RebirthNpcTradeService.OwnerAdmission.LocalPermission p)=>Valid;}
 internal static void Update(){}
 internal static object CaptureAuditState(EntityPlayerLocal p)=>new{adapter=true};
}
static class RebirthNpcTradeOwnerExchange
{
 internal sealed class Release{internal RebirthNpcTradeOriginal Original;internal bool Matches(RebirthNpcTradeOriginal original)=>ReferenceEquals(Original,original);}
 internal sealed class LocalUnusedBefore{internal static bool ReleaseReady;internal static int Released;internal static bool TryCapture(EntityPlayerLocal p,RebirthNpcTradeOriginal o,out LocalUnusedBefore source){source=new LocalUnusedBefore();return true;}internal bool TryRelease(RebirthNpcTradeService.HostUnused unused){if(!ReleaseReady)return false;Released++;return true;}}
 internal static bool ApplyReady,AckReady,ReleaseReady;internal static int Applies,Acks,Releases;
 internal static object CaptureAuditState(EntityPlayerLocal p)=>new{adapter=true};
 internal static bool TryHoldBefore(EntityPlayerLocal p,RebirthNpcTradeOriginal o)=>true;
 internal static bool TryHoldLoaded(RebirthNpcTradeLocalOwnerLoad.Source s,RebirthNpcTradeOriginal o,out int phase){phase=s.Phase;return s.Current(s.Player)&&s.Matches(o);}
 internal static bool TryReleaseHistorical(RebirthNpcTradeLocalOwnerLoad.Source s,RebirthNpcTradeOriginal o){Releases++;return s.Valid;}
 internal static bool TryReleaseLocalHostHistory(RebirthNpcTradeLocalOwnerLoad.Source s,RebirthNpcTradeOriginal o,RebirthNpcTradeClosedHistory h)=>false;
 internal static bool TryReleaseLocalUnused(RebirthNpcTradeLocalOwnerLoad.Source s,RebirthNpcTradeService.HostUnused u)=>false;
 internal static bool TryApplyLocal(EntityPlayerLocal p,RebirthNpcTradeService.OwnerAdmission.LocalPermission permission){if(!ApplyReady)return false;Applies++;return true;}
 internal static bool TryAcknowledgeLocal(EntityPlayerLocal p,RebirthNpcTradeService.OwnerAdmission.LocalPermission permission,RebirthNpcTradeService.HostTerminal terminal){if(!AckReady)return false;Acks++;return true;}
 internal static bool TryReleaseLocal(EntityPlayerLocal p,RebirthNpcTradeService.OwnerAdmission.LocalPermission permission,RebirthNpcTradeService.HostClosed closed){if(!ReleaseReady)return false;Releases++;return true;}
}
enum RebirthNpcSocialEventKind{Trade}
static class RebirthNpcSocialGameplayGateway{internal static bool Ready;internal static int Published;internal static void Publish(Guid op,Guid npc,string actor,string name,RebirthNpcSocialEventKind kind,float amount,float scale){if(!Ready)throw new Exception("deferred social");Published++;}}
public static partial class RebirthNpcTradeService
{
 private class Record{internal RebirthNpcTradeQuote Quote;internal bool Consumed;internal int Resolution;}
 private static object Sync=new object();private static Dictionary<Guid,Record> Quotes=new Dictionary<Guid,Record>();private static long committed;
 private static bool TradeScope()=>true;
 private static void TickClosedSocial(){}
 private static bool TryGet(Dictionary<string,string> values,string key,out string value)=>values.TryGetValue(key,out value);
 private static RebirthNpcInteractionCommandResponse Accept(string text,string payload)=>new RebirthNpcInteractionCommandResponse{Accepted=true};
 private static RebirthNpcInteractionCommandResponse Reject(string text)=>new RebirthNpcInteractionCommandResponse();
 private static Guid Derive(Guid op,int role)=>op;
 internal class OwnerAdmission
 {
  internal static bool AdmissionReady=true;internal RebirthNpcTradeOriginal Original;internal static bool TryCaptureLocal(RebirthNpcInteractionContext c,RebirthNpcTradeOriginal o,out OwnerAdmission a){a=AdmissionReady?new OwnerAdmission{Original=o}:null;return AdmissionReady;}
  internal class LocalPermission{internal RebirthNpcTradeOriginal Original;internal static bool TryCapture(OwnerAdmission a,HostPrepared p,out LocalPermission g){g=new LocalPermission{Original=a.Original};return true;}internal static bool TryCaptureRetained(OwnerAdmission a,HostPrepared p,RebirthNpcTradeLocalOwnerLoad.Source s,out LocalPermission g)=>TryCapture(a,p,out g);internal bool ClosedCurrent(HostClosed closed)=>closed!=null;}
  internal class SavedAfter{internal static bool TryCaptureLocal(LocalPermission p,out SavedAfter after){after=new SavedAfter();return true;}}
 }
 internal class HostPrepared
 {
  internal string PathName="prepared";internal HostTerminal Terminal;internal HostClosed ClosedRecord;
  internal static HostPrepared Recovered;internal static bool TryRecoverLocalPublication(OwnerAdmission a,out HostPrepared p){p=Recovered;return p!=null;}
  internal static bool TryReattachLocal(RebirthNpcTradeLocalOwnerLoad.Source s,out OwnerAdmission a,out HostPrepared p){a=new OwnerAdmission{Original=RebirthNpcTradeOwnerOriginalStore.Last};p=new HostPrepared();return s.Valid;}
  internal static bool TryRetain(OwnerAdmission a,out HostPrepared p){p=new HostPrepared();Quotes[a.Original.Operation].Consumed=true;return true;}
 }
 internal class HostTerminal{internal string Digest="terminal";internal static bool Ready;internal static int Settlements;internal static bool TrySettle(OwnerAdmission.SavedAfter a,out HostTerminal t){t=null;if(!Ready)return false;t=new HostTerminal();Settlements++;return true;}}
 internal class HostUnused{internal string Digest="unused";internal static bool WarmReady;internal static int Publications;internal static bool TryRetainLocalWarm(RebirthNpcTradeOwnerExchange.LocalUnusedBefore s,out HostUnused u){u=WarmReady?new HostUnused():null;if(u!=null)Publications++;return u!=null;}internal static bool TryRetainLocal(RebirthNpcTradeLocalOwnerLoad.Source s,RebirthNpcTradeOriginal o,out HostUnused u){u=null;return false;}}
 internal class HostClosed{internal bool CanRetireFromQuote=true;internal string TerminalDigest="terminal";internal static bool Ready;private bool socialSaved;internal bool TrySaveSocialCompletion(){if(!RebirthNpcSocialGameplayGateway.Ready)return false;if(!socialSaved){socialSaved=true;RebirthNpcSocialGameplayGateway.Published++;}return true;}internal static bool TryCloseLocal(OwnerAdmission.LocalPermission p,out HostClosed c){c=null;if(!Ready)return false;c=new HostClosed();foreach(var a in localAttempts.Values)if(ReferenceEquals(a.Permission,p)){a.Prepared.ClosedRecord=c;Quotes[a.Original.Operation].Resolution=1;}return true;}}
 // ACTUAL_LOCAL_FLOW
 internal static void Run()
 {
  int checks=0;void Check(bool value){checks++;if(!value)throw new Exception("Local flow assertion "+checks);}
  var world=new World();var player=new EntityPlayerLocal{world=world};world.Player=player;GameManager.Instance.World=world;
  var quote=new RebirthNpcTradeQuote();Quotes[quote.QuoteId]=new Record{Quote=quote};var context=new RebirthNpcInteractionContext{NpcId=quote.NpcId};var values=new Dictionary<string,string>{{"quote",quote.QuoteId.ToString("N")}};
  Check(!CommitLocalOriginal(context,values).Accepted&&localAttempts[quote.QuoteId].Stage==1&&RebirthNpcTradeOwnerOriginalStore.Retains==1);
  var original=localAttempts[quote.QuoteId].Original;quote.ExpiresClockTicks=0;
  RebirthNpcTradeOwnerExchange.ApplyReady=true;Check(!CommitLocalOriginal(context,values).Accepted&&localAttempts[quote.QuoteId].Stage==2&&RebirthNpcTradeOwnerExchange.Applies==1);
  Check(!CommitLocalOriginal(context,values).Accepted&&RebirthNpcTradeOwnerExchange.Applies==1&&ReferenceEquals(localAttempts[quote.QuoteId].Original,original));
  HostTerminal.Ready=true;Check(!CommitLocalOriginal(context,values).Accepted&&localAttempts[quote.QuoteId].Stage==3&&HostTerminal.Settlements==1);
  RebirthNpcTradeOwnerExchange.AckReady=true;Check(!CommitLocalOriginal(context,values).Accepted&&localAttempts[quote.QuoteId].Stage==4&&RebirthNpcTradeOwnerExchange.Acks==1);
  HostClosed.Ready=true;Check(!CommitLocalOriginal(context,values).Accepted&&localAttempts[quote.QuoteId].Stage==5);
  RebirthNpcTradeOwnerExchange.ReleaseReady=true;Check(!CommitLocalOriginal(context,values).Accepted&&localAttempts[quote.QuoteId].Stage==5&&RebirthNpcTradeOwnerExchange.Releases==0);
  Check(!CommitLocalOriginal(context,values).Accepted&&RebirthNpcTradeOwnerExchange.Releases==0);
  RebirthNpcSocialGameplayGateway.Ready=true;RebirthNpcTradeOwnerExchange.ReleaseReady=false;
  Check(!CommitLocalOriginal(context,values).Accepted&&localAttempts[quote.QuoteId].Stage==5&&RebirthNpcSocialGameplayGateway.Published==1&&RebirthNpcTradeOwnerExchange.Releases==0);
  RebirthNpcTradeOwnerExchange.ReleaseReady=true;Check(CommitLocalOriginal(context,values).Accepted&&localAttempts.Count==0&&committed==1&&RebirthNpcSocialGameplayGateway.Published==1);
  Check(RebirthNpcTradeOwnerOriginalStore.Retains==1&&RebirthNpcTradeOwnerExchange.Applies==1&&HostTerminal.Settlements==1&&RebirthNpcTradeOwnerExchange.Acks==1&&RebirthNpcTradeOwnerExchange.Releases==1);
  Check(!CommitLocalOriginal(context,values).Accepted&&RebirthNpcTradeOwnerOriginalStore.Retains==1);
  quote=new RebirthNpcTradeQuote();Quotes[quote.QuoteId]=new Record{Quote=quote,Consumed=true};
  RebirthNpcTradeOwnerOriginalStore.Last=new RebirthNpcTradeOriginal{Operation=quote.QuoteId,Quote=quote};
  var source=new RebirthNpcTradeLocalOwnerLoad.Source{Player=player,Operation=quote.QuoteId,Valid=false};Check(!ResumeLocalLoaded(source));source.Valid=true;
  int oldApplies=RebirthNpcTradeOwnerExchange.Applies,oldRetains=RebirthNpcTradeOwnerOriginalStore.Retains;
  Check(ResumeLocalLoaded(source)&&localAttempts.Count==0&&committed==2);
  Check(RebirthNpcTradeOwnerExchange.Applies==oldApplies&&RebirthNpcTradeOwnerOriginalStore.Retains==oldRetains);
  RebirthNpcTradeOwnerOriginalStore.Final=true;int oldSettles=HostTerminal.Settlements;
  Check(ResumeLocalLoaded(source)&&HostTerminal.Settlements==oldSettles&&RebirthNpcTradeOwnerExchange.Applies==oldApplies);
  quote=new RebirthNpcTradeQuote();Quotes[quote.QuoteId]=new Record{Quote=quote};context=new RebirthNpcInteractionContext{NpcId=quote.NpcId};values=new Dictionary<string,string>{{"quote",quote.QuoteId.ToString("N")}};
  OwnerAdmission.AdmissionReady=false;oldApplies=RebirthNpcTradeOwnerExchange.Applies;oldSettles=HostTerminal.Settlements;long oldCommitted=committed;
  Check(!CommitLocalOriginal(context,values).Accepted&&localAttempts[quote.QuoteId].Stage==0);
  quote.ExpiresClockTicks=0;HostUnused.WarmReady=false;
  Check(!CommitLocalOriginal(context,values).Accepted&&localAttempts[quote.QuoteId].UnusedBefore==null&&HostUnused.Publications==0);
  HostUnused.WarmReady=true;RebirthNpcTradeOwnerExchange.LocalUnusedBefore.ReleaseReady=false;
  Check(!CommitLocalOriginal(context,values).Accepted&&HostUnused.Publications==1&&localAttempts.Count==1);
  HostUnused.WarmReady=false;RebirthNpcTradeOwnerExchange.LocalUnusedBefore.ReleaseReady=true;
  Check(!CommitLocalOriginal(context,values).Accepted&&localAttempts.Count==0&&HostUnused.Publications==1&&RebirthNpcTradeOwnerExchange.LocalUnusedBefore.Released==1&&
   RebirthNpcTradeOwnerExchange.Applies==oldApplies&&HostTerminal.Settlements==oldSettles&&committed==oldCommitted);
  quote=new RebirthNpcTradeQuote{ExpiresClockTicks=0};Quotes[quote.QuoteId]=new Record{Quote=quote,Consumed=true};context=new RebirthNpcInteractionContext{NpcId=quote.NpcId};values=new Dictionary<string,string>{{"quote",quote.QuoteId.ToString("N")}};
  var preparedOriginal=new RebirthNpcTradeOriginal{Operation=quote.QuoteId,Quote=quote};var preparedAttempt=new LocalAttempt{Player=player,Context=context,Original=preparedOriginal,Admission=new OwnerAdmission{Original=preparedOriginal}};
  localAttempts.Add(quote.QuoteId,preparedAttempt);HostPrepared.Recovered=new HostPrepared();RebirthNpcTradeOwnerExchange.ApplyReady=false;
  Check(!CommitLocalOriginal(context,values).Accepted&&ReferenceEquals(preparedAttempt.Prepared,HostPrepared.Recovered)&&preparedAttempt.Stage==1&&
   preparedAttempt.UnusedBefore==null&&preparedAttempt.UnusedReceipt==null&&HostUnused.Publications==1);
  Console.WriteLine(checks+" actual local flow assertions passed; native permissions/conservation adapters NOT certified");
 }
}
class Program{static void Main()=>RebirthNpcTradeService.Run();}

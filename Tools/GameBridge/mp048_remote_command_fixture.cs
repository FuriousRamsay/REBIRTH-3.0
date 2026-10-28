using System;using System.Collections.Generic;
// Whole actual authenticated remote command dispatcher. Each native/receipt
// producer below is an explicit adapter; no inventory or network is executed.
namespace Platform{class PlatformUserIdentifierAbs{public override string ToString()=>"owner";}}
class NetPackage{internal bool Entity=true,User=true;internal bool ValidEntityIdForSender(int id)=>Entity;internal bool ValidUserIdForSender(Platform.PlatformUserIdentifierAbs user)=>User;}
enum RebirthNpcInteractionCommandKind{Trade,Other}
class RebirthNpcInteractionCommand{internal RebirthNpcInteractionCommandKind Kind=RebirthNpcInteractionCommandKind.Trade;internal string Payload;}
class RebirthNpcInteractionCommandResponse{internal bool Accepted;internal string Payload;}
class RebirthNpcTradeOriginal{internal string Digest=new string('a',64);}
static class RebirthNpcNetworkEpoch{internal static bool Valid=true;internal static bool ValidateConnectionEpoch(int p,uint c)=>Valid;internal static uint GetServerEpoch()=>1;}
static class RebirthRemoteResourceDurableJournal{internal static string Digest(string value)=>new string(value=="grant"?'d':'c',64);}
class RebirthNpcTradeClosedHistory
{
 internal static bool Available,Social=true;internal static int Reads,Saves;internal Guid Operation;internal string OriginalDigest,TerminalDigest;
 internal static bool TryRead(NetPackage p,Platform.PlatformUserIdentifierAbs u,int player,uint connection,Guid operation,string original,string terminal,out RebirthNpcTradeClosedHistory h)
 {Reads++;h=Available?new RebirthNpcTradeClosedHistory{Operation=operation,OriginalDigest=original,TerminalDigest=terminal}:null;return h!=null;}
 internal bool TrySaveSocialCompletion(){Saves++;return Social;}
}
class RebirthNpcTradeUnusedHistory
{
 internal static bool Available;internal string UnusedDigest=new string('e',64);
 internal static bool TryRead(NetPackage p,Platform.PlatformUserIdentifierAbs u,int player,uint connection,Guid operation,string original,out RebirthNpcTradeUnusedHistory h){h=Available?new RebirthNpcTradeUnusedHistory():null;return h!=null;}
}
public static partial class RebirthNpcTradeService
{
 private static readonly object Sync=new object();private static Dictionary<Guid,Record> Quotes=new Dictionary<Guid,Record>();
 private class Record{internal QuoteValue Quote=new QuoteValue();internal int Resolution;internal HostPrepared OwnerPreparation;}
 private class QuoteValue{internal string ActorId="owner";}
 private static bool Scope=true;private static bool TradeScope()=>Scope;
 private static Dictionary<string,string> Parse(string value){var result=new Dictionary<string,string>();foreach(string part in value.Split(';')){int eq=part.IndexOf('=');if(eq>0)result[part.Substring(0,eq)]=part.Substring(eq+1);}return result;}
 private static RebirthNpcInteractionCommandResponse Reject(string reason)=>new RebirthNpcInteractionCommandResponse();
 private static RebirthNpcInteractionCommandResponse Accept(string reason,string payload)=>new RebirthNpcInteractionCommandResponse{Accepted=true,Payload=payload};
 internal class OwnerAdmission
 {
  internal static bool Available=true;internal static int Captures;
  internal static bool TryCaptureFromSaved(NetPackage p,Platform.PlatformUserIdentifierAbs u,string s,uint c,uint e,int player,Guid operation,string digest,out OwnerAdmission a){Captures++;a=Available?new OwnerAdmission():null;return a!=null;}
 }
 internal class HostPrepared
 {
  internal RebirthNpcTradeOriginal Original=new RebirthNpcTradeOriginal();internal HostClosed ClosedRecord;internal HostTerminal Terminal;
  internal string Image="prepared";internal bool Issue=true,After=true;internal static bool Retain=true;internal static int Reattachments,Publications,Grants,AfterReads;
  internal static void TryReattach(NetPackage p,Platform.PlatformUserIdentifierAbs u,int player,uint connection,Guid operation,string digest){Reattachments++;}
  internal static bool TryRetain(OwnerAdmission a,out HostPrepared p){Publications++;p=Retain?new HostPrepared():null;return p!=null;}
  internal bool TryIssueGrant(out HostGrant g){Grants++;g=Issue?new HostGrant():null;return g!=null;}
  internal bool TryObserveAfter(HostGrant g,NetPackage p,Platform.PlatformUserIdentifierAbs u,out object a){AfterReads++;a=After?new object():null;return a!=null;}
  internal class HostGrant{internal string Image="grant";internal static HostGrant Create(HostPrepared p)=>new HostGrant();}
 }
 internal class HostTerminal
 {
  internal string Digest=new string('b',64);internal static bool Settle=true;internal static int Settlements;
  internal static bool TrySettle(object after,out HostTerminal t){Settlements++;t=Settle?new HostTerminal():null;return t!=null;}
 }
 internal class HostClosed
 {
  internal string TerminalDigest=new string('b',64);internal static bool Close=true,Reply=true,Social=true;internal static int Closes,Saves;
  internal static bool TryClose(HostPrepared p,string digest,NetPackage packet,Platform.PlatformUserIdentifierAbs user,out HostClosed c){Closes++;c=Close?new HostClosed():null;if(c!=null)p.ClosedRecord=c;return c!=null;}
  internal bool CanReply(NetPackage p,Platform.PlatformUserIdentifierAbs u)=>Reply;
  internal bool TrySaveSocialCompletion(){Saves++;return Social;}
 }
 internal class HostQuote{internal static int Restores;internal static void TryRestoreExpiredUnused(NetPackage p,Platform.PlatformUserIdentifierAbs u,int player,uint connection,Guid operation){Restores++;}}
 internal class HostUnused
 {
  internal static bool Available;internal string Digest=new string('e',64);
  internal static bool TryRetain(NetPackage p,Platform.PlatformUserIdentifierAbs u,uint connection,int player,Guid operation,string digest,out HostUnused h){h=Available?new HostUnused():null;return h!=null;}
 }
 internal static void Run()
 {
  int checks=0;void Check(bool value){checks++;if(!value)throw new Exception("Remote command assertion "+checks);}
  var id=Guid.NewGuid();string digest=new string('a',64),terminal=new string('b',64);var packet=new NetPackage();var user=new Platform.PlatformUserIdentifierAbs();
  RebirthNpcInteractionCommand Command(string operation)=>new RebirthNpcInteractionCommand{Payload="operation="+operation+";quote="+id.ToString("N")+";original="+digest+(operation=="ackNative"?";terminal="+terminal:"")};
  bool Call(RebirthNpcInteractionCommand c,out RebirthNpcInteractionCommandResponse r)=>TryRemoteOwnerCommand(packet,user,7,1,"session",c,out r);
  HostPrepared Seed()
  {
   Quotes.Clear();var p=new HostPrepared();Quotes[id]=new Record{OwnerPreparation=p};Scope=true;packet.Entity=packet.User=true;RebirthNpcNetworkEpoch.Valid=true;
   RebirthNpcTradeClosedHistory.Available=false;RebirthNpcTradeClosedHistory.Social=true;RebirthNpcTradeUnusedHistory.Available=false;
   HostUnused.Available=false;OwnerAdmission.Available=true;HostPrepared.Retain=true;HostTerminal.Settle=true;HostClosed.Close=HostClosed.Reply=HostClosed.Social=true;return p;
  }
  Check(!Call(new RebirthNpcInteractionCommand{Kind=RebirthNpcInteractionCommandKind.Other},out var response)&&response==null);
  Check(!Call(new RebirthNpcInteractionCommand{Payload="operation=other"},out response)&&response==null);
  Seed();var command=Command("prepareNative");command.Payload+=";extra=value";Check(Call(command,out response)&&!response.Accepted&&HostPrepared.Reattachments==0);
  command=Command("prepareNative");command.Payload=command.Payload.Replace(digest,new string('A',64));Check(Call(command,out response)&&!response.Accepted&&HostPrepared.Reattachments==0);
  Seed();packet.Entity=false;Check(Call(Command("prepareNative"),out response)&&!response.Accepted&&HostPrepared.Reattachments==0);
  Seed();packet.User=false;Check(Call(Command("prepareNative"),out response)&&!response.Accepted&&HostPrepared.Reattachments==0);
  Seed();RebirthNpcNetworkEpoch.Valid=false;Check(Call(Command("prepareNative"),out response)&&!response.Accepted&&HostPrepared.Reattachments==0);
  Seed();Scope=false;Check(Call(Command("prepareNative"),out response)&&!response.Accepted&&HostPrepared.Reattachments==0);
  Seed();RebirthNpcTradeClosedHistory.Available=true;RebirthNpcTradeClosedHistory.Social=false;Check(Call(Command("ackNative"),out response)&&!response.Accepted&&HostClosed.Closes==0);
  RebirthNpcTradeClosedHistory.Social=true;Check(Call(Command("ackNative"),out response)&&response.Accepted&&response.Payload.Contains("operation=ownerClosed")&&HostPrepared.Reattachments==0);
  Seed();RebirthNpcTradeUnusedHistory.Available=true;Check(Call(Command("prepareNative"),out response)&&response.Accepted&&response.Payload.Contains("operation=ownerUnused")&&HostPrepared.Reattachments==0);
  Seed();Quotes[id].Quote.ActorId="foreign";Check(Call(Command("prepareNative"),out response)&&!response.Accepted&&HostPrepared.Grants==0);
  var p=Seed();p.Issue=false;Check(Call(Command("prepareNative"),out response)&&!response.Accepted&&HostPrepared.Grants==1);
  p=Seed();Check(Call(Command("prepareNative"),out response)&&response.Accepted&&response.Payload.Contains("operation=ownerGrant")&&HostTerminal.Settlements==0);
  p=Seed();p.After=false;Check(Call(Command("settleNative"),out response)&&!response.Accepted&&HostTerminal.Settlements==0);
  p=Seed();HostTerminal.Settle=false;Check(Call(Command("settleNative"),out response)&&!response.Accepted&&p.Terminal==null);
  p=Seed();Check(Call(Command("settleNative"),out response)&&response.Accepted&&response.Payload.Contains("operation=ownerTerminal")&&p.Terminal!=null);
  p=Seed();HostClosed.Social=false;Check(Call(Command("ackNative"),out response)&&!response.Accepted&&p.ClosedRecord!=null);
  int closes=HostClosed.Closes;HostClosed.Social=true;Check(Call(Command("ackNative"),out response)&&response.Accepted&&HostClosed.Closes==closes);
  p=Seed();p.Original.Digest=new string('f',64);Check(Call(Command("ackNative"),out response)&&!response.Accepted);
  p=Seed();Quotes[id].OwnerPreparation=null;HostUnused.Available=true;Check(Call(Command("prepareNative"),out response)&&response.Accepted&&response.Payload.Contains("operation=ownerUnused")&&OwnerAdmission.Captures==0);
  Seed();Quotes[id].OwnerPreparation=null;OwnerAdmission.Available=false;Check(Call(Command("prepareNative"),out response)&&!response.Accepted&&HostPrepared.Publications==0);
  Seed();Quotes[id].OwnerPreparation=null;Check(Call(Command("prepareNative"),out response)&&response.Accepted&&HostPrepared.Publications==1);
  Console.WriteLine(checks+" actual remote command assertions passed; native/receipt producers NOT certified");
 }
}
class Program{static void Main()=>RebirthNpcTradeService.Run();}

using System;
using System.Collections.Generic;
using System.Threading;
public enum RebirthNpcExternalTransferResult { Applied, Replayed, InvalidRequest, AuthorityDenied, NpcNotFound, CapabilityDenied, RevisionConflict, InsufficientQuantity, ReservationConflict, ExternalRejected, ExternalCommitFailed, RollbackFailed, Indeterminate }
public enum RebirthNpcTradeDirection { BuyFromNpc, SellToNpc }
public enum RebirthNpcExternalTransferDirection { ExternalToNpc, NpcToExternal }
public enum RebirthNpcInventoryAuthorityOperations { Transfer=1, Mutate=2 }
public enum RebirthNpcInteractionCommandStatus { Accepted, Rejected }
public enum RebirthNpcSocialEventKind { Trade }
public class RebirthNpcInteractionCommandResponse { public RebirthNpcInteractionCommandStatus Status; public string Detail, ResponsePayload; }
public class RebirthNpcInteractionContext { public string ActorId="actor", NpcId="npc"; }
public class RebirthNpcTradeQuote {
 public Guid QuoteId=Guid.NewGuid(); public string ActorId="actor",NpcId="npc",CurrencyItemKey="coin",ItemKey="wood";
 public RebirthNpcTradeDirection Direction; public int Quantity=2,TotalPrice=10; public uint ExpectedNpcInventoryRevision=4;
 public long ExpiresClockTicks=DateTime.UtcNow.AddMinutes(1).Ticks;
}
public class RebirthNpcInventoryAuthorityLease : IDisposable { public string AuthorityKey="lease"; public void Dispose(){} }
public static class RebirthNpcInventoryAuthorityService { public static RebirthNpcInventoryAuthorityLease Issue(string a,RebirthNpcInventoryAuthorityOperations b,TimeSpan c,string d){return new RebirthNpcInventoryAuthorityLease();} }
public static class RebirthNpcSocialGameplayGateway { public static int Events; public static void Publish(Guid a,string b,string c,string d,RebirthNpcSocialEventKind e,float f,float g){Events++;} }
public static class Check {
 class QuoteRecord { public RebirthNpcTradeQuote Quote; public bool Consumed; }
 static object Sync=new object(); static Dictionary<Guid,QuoteRecord> Quotes=new Dictionary<Guid,QuoteRecord>(); static List<Guid> QuoteOrder=new List<Guid>();
 static long expired,committed,rejected,compensated,compensationFailures;
 static Queue<RebirthNpcExternalTransferResult> Outcomes=new Queue<RebirthNpcExternalTransferResult>();
 static List<string> Calls=new List<string>();
 static RebirthNpcExternalTransferResult Apply(RebirthNpcTradeQuote q,byte leg,string item,int count,RebirthNpcExternalTransferDirection direction,string authority,uint expected,out uint revision,out string error){
 Calls.Add(leg+":"+item+":"+direction); revision=expected+1;error="fixture";return Outcomes.Dequeue();
 }
// METHODS
 static void Assert(bool ok,string why){if(!ok)throw new Exception(why);}
 static void Run(RebirthNpcTradeDirection direction,RebirthNpcExternalTransferResult second,int expectedCalls){
 Quotes.Clear();Calls.Clear();Outcomes.Clear();RebirthNpcSocialGameplayGateway.Events=0;
 var q=new RebirthNpcTradeQuote{Direction=direction};Quotes[q.QuoteId]=new QuoteRecord{Quote=q};
 Outcomes.Enqueue(RebirthNpcExternalTransferResult.Applied);Outcomes.Enqueue(second);
 if(expectedCalls==3)Outcomes.Enqueue(RebirthNpcExternalTransferResult.Applied);
 var args=new Dictionary<string,string>{{"quote",q.QuoteId.ToString()}};
 var r=Commit(new RebirthNpcInteractionContext(),args);
 Assert(Calls.Count==expectedCalls,"wrong transfer count "+direction+" "+second);
 bool success=second==RebirthNpcExternalTransferResult.Applied;
 Assert(r.Status==(success?RebirthNpcInteractionCommandStatus.Accepted:RebirthNpcInteractionCommandStatus.Rejected),"status");
 Assert(RebirthNpcSocialGameplayGateway.Events==(success?1:0),"social event");
 if(expectedCalls==3)Assert(Calls[2]=="3:"+(direction==RebirthNpcTradeDirection.BuyFromNpc?"coin":"wood")+":NpcToExternal","refund wrong item");
 if(second==RebirthNpcExternalTransferResult.Indeterminate || second==RebirthNpcExternalTransferResult.RollbackFailed)Assert(r.Detail.Contains(q.QuoteId.ToString("N")),"missing recovery quote");
 Commit(new RebirthNpcInteractionContext(),args);Assert(Calls.Count==expectedCalls,"repeat mutated");
 }
 static void FirstFailure(RebirthNpcTradeDirection direction,RebirthNpcExternalTransferResult result){
 Quotes.Clear();Calls.Clear();Outcomes.Clear();RebirthNpcSocialGameplayGateway.Events=0;
 var q=new RebirthNpcTradeQuote{Direction=direction};Quotes[q.QuoteId]=new QuoteRecord{Quote=q};
 Outcomes.Enqueue(result);var args=new Dictionary<string,string>{{"quote",q.QuoteId.ToString()}};
 var response=Commit(new RebirthNpcInteractionContext(),args);
 Assert(response.Status==RebirthNpcInteractionCommandStatus.Rejected && Calls.Count==1,"first failure continued trade");
 Assert(RebirthNpcSocialGameplayGateway.Events==0,"first failure published trade event");
 if(result==RebirthNpcExternalTransferResult.Indeterminate || result==RebirthNpcExternalTransferResult.RollbackFailed)
  Assert(response.Detail.Contains(q.QuoteId.ToString("N")) && response.Detail.Contains("reconciliation"),"first failure missing recovery reference");
 Commit(new RebirthNpcInteractionContext(),args);Assert(Calls.Count==1,"first failure repeated transfer");
 }
 public static void Main(){
 foreach(RebirthNpcTradeDirection d in Enum.GetValues(typeof(RebirthNpcTradeDirection))){
 FirstFailure(d,RebirthNpcExternalTransferResult.Indeterminate);FirstFailure(d,RebirthNpcExternalTransferResult.RollbackFailed);FirstFailure(d,RebirthNpcExternalTransferResult.AuthorityDenied);
 Run(d,RebirthNpcExternalTransferResult.Indeterminate,2);Run(d,RebirthNpcExternalTransferResult.RollbackFailed,2);
 Run(d,RebirthNpcExternalTransferResult.ExternalCommitFailed,3);Run(d,RebirthNpcExternalTransferResult.ReservationConflict,3);Run(d,RebirthNpcExternalTransferResult.Applied,2);
 }
 Console.WriteLine("PASS actual Commit: sixteen buy/sell cases including first-leg recovery references, refund item/direction, uncertain no refund, social events and consumed-quote retry");
 }
}

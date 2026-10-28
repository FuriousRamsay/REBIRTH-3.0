using System;
using System.Collections.Generic;
// AUTHORED_NOT_RUN. Actual receipt-publication method; native commits are not simulated as proof.
interface IRemoteResourceSource {}
sealed class Source:IRemoteResourceSource {}
sealed class RemoteNpcResourceSource:IRemoteResourceSource {
 internal enum CommitDisposition:byte {NotInvoked,Refused,Published,Unknown}
 internal sealed class CommitEvent {internal RemoteNpcResourceSource Source;internal Guid Mutation;internal uint BeforeRevision,AfterRevision;internal CommitDisposition Disposition;}
 internal bool LastCommitSucceeded=true;internal uint OriginalRevision=4;internal CommitEvent LastCommitEvent;
}
static class RemoteResourceTransactions {
 internal sealed class ReturnReceipt {internal IRemoteResourceSource Source;internal int Slot;}
// ACTUAL_COMMITTED_RETURNS
}
static class Program {
 static void Need(bool value,string error){if(!value)throw new Exception(error);}
 public static void Main(){
  for(int variant=0;variant<10;variant++){
   var npc=new RemoteNpcResourceSource();var receipt=new RemoteNpcResourceSource.CommitEvent{Source=npc,Mutation=Guid.NewGuid(),BeforeRevision=4,AfterRevision=5,Disposition=RemoteNpcResourceSource.CommitDisposition.Published};npc.LastCommitEvent=receipt;
   Need(RemoteResourceTransactions.HasCommittedConsumption(npc),"actual published event refused");
   if(variant==0)npc.LastCommitSucceeded=false;
   if(variant==1)npc.LastCommitEvent=null;
   if(variant==2)receipt.Source=new RemoteNpcResourceSource();
   if(variant==3)receipt.Mutation=Guid.Empty;
   if(variant==4)receipt.Disposition=RemoteNpcResourceSource.CommitDisposition.NotInvoked;
   if(variant==5)receipt.Disposition=RemoteNpcResourceSource.CommitDisposition.Refused;
   if(variant==6)receipt.Disposition=RemoteNpcResourceSource.CommitDisposition.Unknown;
   if(variant==7)receipt.BeforeRevision=3;
   if(variant==8)receipt.AfterRevision=4;
   if(variant==9){npc.OriginalRevision=uint.MaxValue;receipt.BeforeRevision=uint.MaxValue;receipt.AfterRevision=0;}
   Need(!RemoteResourceTransactions.HasCommittedConsumption(npc),"unpublished consumption admitted variant "+variant);
  }
  Need(!RemoteResourceTransactions.HasCommittedConsumption(null),"null source accepted");
  var first=new Source();var other=new Source();
  var a=new RemoteResourceTransactions.ReturnReceipt{Source=first,Slot=1};
  var b=new RemoteResourceTransactions.ReturnReceipt{Source=other,Slot=2};
  var c=new RemoteResourceTransactions.ReturnReceipt{Source=first,Slot=3};
  var staged=new List<RemoteResourceTransactions.ReturnReceipt>{a,b,c};
  var published=new List<RemoteResourceTransactions.ReturnReceipt>();
  RemoteResourceTransactions.PublishCommittedReturns(new Source(),staged,published);
  Need(staged.Count==3&&published.Count==0,"foreign source published receipts");
  RemoteResourceTransactions.PublishCommittedReturns(first,staged,published);
  Need(staged.Count==1&&ReferenceEquals(staged[0],b),"uncommitted source custody changed");
  Need(published.Count==2&&ReferenceEquals(published[0],a)&&ReferenceEquals(published[1],c),"original committed order or identity changed");
  RemoteResourceTransactions.PublishCommittedReturns(first,staged,published);
  Need(published.Count==2,"repeat publication duplicated receipt");
  RemoteResourceTransactions.PublishCommittedReturns(other,staged,published);
  Need(staged.Count==0&&published.Count==3&&ReferenceEquals(published[2],b),"second source receipt missing");
  RemoteResourceTransactions.PublishCommittedReturns(null,staged,published);
  RemoteResourceTransactions.PublishCommittedReturns(first,null,published);
  RemoteResourceTransactions.PublishCommittedReturns(first,staged,null);
  Console.WriteLine("PASS actual per-source committed return receipt publication; native commit/save NOT RUN");
 }
}

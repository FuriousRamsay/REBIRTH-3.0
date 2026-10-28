// Assembly/scope doubles isolate the actual production inbox state machine.
// They do not certify wire framing, authentication, saved receipts or inventory.
public static class RebirthSurvivorRequestScope {
 public static bool TryNormalize(string value,out string normalized){normalized=value;return value=="creation";}
 public static bool Matches(string a,string b){return a==b;}
}
public sealed class RebirthGearTransferState {}
public sealed class RebirthGearOfferFragment {
 public string CreationId="creation",Digest="digest";public System.Guid TransactionId;
 public long ExpectedRevision;public int TotalBytes=1;public bool Finish=true;
}
public sealed class RebirthGearOfferAssembly {
 private bool finished;
 public RebirthGearOfferAssembly(string c,System.Guid t,long r,int l,string d){}
 public bool TryAdd(RebirthGearOfferFragment fragment){finished=fragment.Finish;return true;}
 public bool TryFinish(out RebirthGearTransferState value){value=finished?new RebirthGearTransferState():null;return finished;}
}
public static class MpAInboxFixture {
 static void Check(bool condition,string label){if(!condition)throw new System.Exception(label);}
 public static void Main(){
  var owner=new object();var world=new object();var session=new object();
  var inbox=new RebirthGearOfferInbox(owner,world,session,"creation");
  var a=new RebirthGearOfferFragment{TransactionId=System.Guid.NewGuid()};
  var b=new RebirthGearOfferFragment{TransactionId=System.Guid.NewGuid()};
  RebirthGearTransferState offer;
  Check(inbox.TryReceive(owner,world,session,"creation",0,a,out offer),"assemble A");
  Check(!inbox.TryReleaseSettled(owner,world,session,"creation",a.TransactionId,()=>false),"unverified remains held");
  Check(!inbox.TryReceive(owner,world,session,"creation",1,b,out offer),"B cannot evict unresolved A");
  Check(inbox.TryReleaseSettled(owner,world,session,"creation",a.TransactionId,()=>true),"verified A retires");
  Check(!inbox.TryReceive(owner,world,session,"creation",2,a,out offer),"retired A cannot occupy cleared inbox");
  Check(inbox.TryReceive(owner,world,session,"creation",3,b,out offer),"B progresses after A replay");
  Check(!inbox.TryReceive(owner,world,new object(),"creation",4,a,out offer),"foreign session rejected");
  var expiry=new RebirthGearOfferInbox(owner,world,session,"creation");
  var partial=new RebirthGearOfferFragment{TransactionId=System.Guid.NewGuid(),Finish=false};
  Check(!expiry.TryReceive(owner,world,session,"creation",0,partial,out offer),"partial stays incomplete");
  Check(!expiry.TryReceive(owner,world,session,"creation",1,b,out offer),"unexpired assembly retained");
  Check(expiry.TryReceive(owner,world,session,"creation",31,b,out offer),"expired partial permits B");
  System.Console.WriteLine("PASS actual inbox ordering and retirement with disclosed assembly/scope doubles");
 }
}

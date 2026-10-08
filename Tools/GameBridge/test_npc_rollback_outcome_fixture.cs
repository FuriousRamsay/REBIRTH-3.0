using System;
using System.Threading;
enum RebirthNpcExternalTransferResult {AuthorityDenied,RollbackFailed}
class RebirthNpcExternalTransferRequest {public Guid TransactionId=Guid.NewGuid();}
interface IRebirthNpcExternalInventoryReservation {bool Rollback(out string error);}
class Reservation:IRebirthNpcExternalInventoryReservation {
 public int Mode;public bool Rollback(out string error){error="endpoint failure";if(Mode==2)throw new Exception("endpoint exception");return Mode==0;}
}
class Test {
 static long rollbacks,rollbackFailures;static int remembered;static Guid saved;
 static void Remember(Guid id,RebirthNpcExternalTransferResult result,uint revision,string fingerprint,string directory,long generation){if(result!=RebirthNpcExternalTransferResult.RollbackFailed||revision!=8||fingerprint!="payload"||directory!="A"||generation!=3)throw new Exception("receipt identity lost");remembered++;saved=id;}
 static RebirthNpcExternalTransferResult Reject(RebirthNpcExternalTransferResult result){return result;}
 // SOURCE
 static void Main(){
  var request=new RebirthNpcExternalTransferRequest();string error="original";
  var result=FinishRejectedReservation(new Reservation(),request,RebirthNpcExternalTransferResult.AuthorityDenied,8,"payload","A",3,ref error);
  if(result!=RebirthNpcExternalTransferResult.AuthorityDenied||remembered!=0||rollbacks!=1)throw new Exception("clean rollback changed");
  for(int mode=1;mode<=2;mode++){
   error="original";result=FinishRejectedReservation(new Reservation{Mode=mode},request,RebirthNpcExternalTransferResult.AuthorityDenied,8,"payload","A",3,ref error);
   if(result!=RebirthNpcExternalTransferResult.RollbackFailed||saved!=request.TransactionId||!error.StartsWith("original; endpoint"))throw new Exception("uncertain rollback reported clean");
  }
  if(remembered!=2||rollbackFailures!=2)throw new Exception("failure not recorded");
  if(!RollbackReservation(null,ref error))throw new Exception("empty reservation");
  Console.WriteLine("PASS actual rollback/outcome helpers: clean rejection preserved; false and thrown rollback report/remember RollbackFailed with original identity and diagnostic. Persistence substituted.");
 }
}

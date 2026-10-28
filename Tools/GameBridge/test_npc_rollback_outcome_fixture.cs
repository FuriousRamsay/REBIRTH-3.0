using System;
using System.Threading;
enum RebirthNpcExternalTransferResult {AuthorityDenied,RollbackFailed}
class RebirthNpcExternalTransferRequest {public Guid TransactionId=Guid.NewGuid();}
interface IRebirthNpcExternalInventoryReservation {bool Rollback(out string error);}
enum RebirthTransactionState {Prepared,Committed,RolledBack,Failed,Indeterminate}
interface IRebirthNpcExternalInventoryReservationOutcome {RebirthTransactionState State{get;}string Detail{get;}}
class Reservation:IRebirthNpcExternalInventoryReservation {
 public int Mode;public bool Rollback(out string error){error="endpoint failure";if(Mode==2)throw new Exception("endpoint exception");return Mode==0;}
}
class OutcomeReservation:Reservation,IRebirthNpcExternalInventoryReservationOutcome {
 public RebirthTransactionState Value;public bool Throw;
 public RebirthTransactionState State{get{if(Throw)throw new Exception("outcome unavailable");return Value;}}public string Detail=>"";
}
class Test {
 static long rollbacks,rollbackFailures;static int remembered;static Guid saved;
 static void Remember(Guid id,RebirthNpcExternalTransferResult result,uint revision,string fingerprint,string directory,long generation){if(result!=RebirthNpcExternalTransferResult.RollbackFailed||revision!=8||fingerprint!="payload"||directory!="A"||generation!=3)throw new Exception("receipt identity lost");remembered++;saved=id;}
 // Publication is a boundary in this rollback-only fixture; cleanup fixture
 // extracts and qualifies the actual exact publication helper separately.
 static void RememberPublished(Guid id,RebirthNpcExternalTransferResult result,uint revision,string fingerprint,string directory,long generation,ref bool published){Remember(id,result,revision,fingerprint,directory,generation);published=true;}
 static RebirthNpcExternalTransferResult Reject(RebirthNpcExternalTransferResult result){return result;}
 // SOURCE
 static void Main(){
  if(!CommitRequiresReconciliation(new Reservation(),false)||CommitRequiresReconciliation(new Reservation(),true))throw new Exception("legacy commit return contract changed");
  foreach(RebirthTransactionState state in Enum.GetValues(typeof(RebirthTransactionState))){
   var outcome=new OutcomeReservation{Value=state};
   bool definiteFailure=state==RebirthTransactionState.Failed||state==RebirthTransactionState.RolledBack;
   if(CommitRequiresReconciliation(outcome,false)==definiteFailure)throw new Exception("failed outcome compensation classification "+state);
   if(CommitRequiresReconciliation(outcome,true)!=(state!=RebirthTransactionState.Committed))throw new Exception("positive contradictory outcome accepted "+state);
  }
  if(!CommitRequiresReconciliation(new OutcomeReservation{Throw=true},false)||!CommitRequiresReconciliation(new OutcomeReservation{Value=(RebirthTransactionState)255},true))throw new Exception("unknown outcome allowed financial continuation");
  var request=new RebirthNpcExternalTransferRequest();string error="original";bool published=false;
  var result=FinishRejectedReservation(new Reservation(),request,RebirthNpcExternalTransferResult.AuthorityDenied,8,"payload","A",3,ref error,ref published);
  if(result!=RebirthNpcExternalTransferResult.AuthorityDenied||remembered!=0||rollbacks!=1)throw new Exception("clean rollback changed");
  for(int mode=1;mode<=2;mode++){
   error="original";published=false;result=FinishRejectedReservation(new Reservation{Mode=mode},request,RebirthNpcExternalTransferResult.AuthorityDenied,8,"payload","A",3,ref error,ref published);
   if(!published)throw new Exception("rollback failure publication flag missing");
   if(result!=RebirthNpcExternalTransferResult.RollbackFailed||saved!=request.TransactionId||!error.StartsWith("original; endpoint"))throw new Exception("uncertain rollback reported clean");
  }
  if(remembered!=2||rollbackFailures!=2)throw new Exception("failure not recorded");
  if(!RollbackReservation(null,ref error))throw new Exception("empty reservation");
  error="original";if(RollbackReservation(new OutcomeReservation{Value=RebirthTransactionState.Committed},ref error))throw new Exception("contradictory rollback authorized compensation");
  error="original";if(!RollbackReservation(new OutcomeReservation{Value=RebirthTransactionState.RolledBack},ref error))throw new Exception("positive typed rollback refused");
  error="original";if(RollbackReservation(new OutcomeReservation{Throw=true},ref error))throw new Exception("throwing rollback outcome reported clean");
  Console.WriteLine("PASS actual rollback/outcome helpers: clean rejection preserved; false and thrown rollback report/remember RollbackFailed with original identity and diagnostic. Persistence substituted.");
 }
}

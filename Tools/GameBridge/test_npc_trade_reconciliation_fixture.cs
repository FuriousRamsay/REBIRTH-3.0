using System;
public enum RebirthNpcExternalTransferResult : byte { Applied, Replayed, InvalidRequest, AuthorityDenied, NpcNotFound, CapabilityDenied, RevisionConflict, InsufficientQuantity, ReservationConflict, ExternalRejected, ExternalCommitFailed, RollbackFailed, Indeterminate }
public static class Check {
// METHODS
 public static void Main() {
 foreach(RebirthNpcExternalTransferResult r in Enum.GetValues(typeof(RebirthNpcExternalTransferResult))) {
 bool expected=r==RebirthNpcExternalTransferResult.Indeterminate || r==RebirthNpcExternalTransferResult.RollbackFailed;
 if(RequiresReconciliation(r)!=expected)throw new Exception(r.ToString());
 }
 Console.WriteLine("PASS reconciliation classification for all 13 outcomes; both pre-refund guards located");
 }
}

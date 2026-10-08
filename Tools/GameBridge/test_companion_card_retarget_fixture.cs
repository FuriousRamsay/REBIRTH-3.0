using System;
using System.Collections.Generic;
struct RebirthNpcStableId {public int Value;public bool IsEmpty {get{return Value==0;}}}
class Runtime {public RebirthNpcStableId StableId;}
class EntityRebirthDogCompanion {public Runtime RebirthRuntimeState;public int entityId;}
class RebirthCompanionSnapshot {}
static class RebirthNpcWorldIntegrationService {public static string GetDisplayName(RebirthNpcStableId id){return "dog"+id.Value;}}
static class RebirthCompanionSnapshotService {public static Action<RebirthCompanionSnapshot> Cancelled;public static void Cancel(Action<RebirthCompanionSnapshot> c){Cancelled=c;}}
class Subject {public RebirthNpcStableId stableId;public int dogEntityId,actionOffset,snapshotGeneration;public string preparedName;public float renderTimer,snapshotTimer;public bool snapshotPending,cardOpen;public RebirthCompanionSnapshot remoteSnapshot;public Action<RebirthCompanionSnapshot> snapshotCallback;public List<int> actions=new List<int>();public int CancelDismiss,Rendered;void CancelDismissConfirmation(){CancelDismiss++;}void Render(){if(snapshotPending||remoteSnapshot!=null||actions.Count!=0)throw new Exception("stale render");Rendered++;}
// PREPARE
}
class Check {static void Main(){var cb=new Action<RebirthCompanionSnapshot>(s=>{});var x=new Subject {snapshotCallback=cb,snapshotPending=true,remoteSnapshot=new RebirthCompanionSnapshot(),snapshotTimer=4,cardOpen=true,snapshotGeneration=5};x.actions.Add(1);x.Prepare(new EntityRebirthDogCompanion {entityId=22,RebirthRuntimeState=new Runtime {StableId=new RebirthNpcStableId {Value=7}}});if(RebirthCompanionSnapshotService.Cancelled!=cb||x.snapshotCallback!=null||x.snapshotPending||x.remoteSnapshot!=null||x.snapshotTimer!=0||x.snapshotGeneration!=6||x.stableId.Value!=7||x.dogEntityId!=22||x.Rendered!=1||x.preparedName!="dog7")throw new Exception("retarget state");x.cardOpen=false;x.Prepare(null);if(x.Rendered!=1||!x.stableId.IsEmpty||x.dogEntityId!=-1||x.preparedName!=""||x.snapshotGeneration!=7)throw new Exception("closed/null preparation");Console.WriteLine("PASS retarget cancels old callback, resets pending/snapshot/actions, advances generation and renders only open cards");}}

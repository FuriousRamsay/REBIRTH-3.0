partial class Unused{}
class WatchStage:IStationMixedStagedPublication {
 public RebirthStationCompletionCapture.SuccessfulOutput OriginalEvent=>Expectation.OriginalEvent;public NativePaidOrdinaryRouteLink Route=>Expectation.Route;
 public IStationMixedFinalExpectation Expectation{get;set;}public RebirthStationSnapshotEvidence.Publication Snapshot{get;set;}public long Generation=>Expectation.Generation;
}
class WatchPublisher:IStationMixedAtomicPublication {
 public int Stages,Saves; public bool StageResult,SaveResult,ThrowSave;public IStationMixedStagedPublication Retained;public Action OnStage;
 public bool TryStage(IStationMixedFinalExpectation e,RebirthStationSnapshotEvidence.Publication p,out IStationMixedStagedPublication s){Stages++;s=Retained=new WatchStage{Expectation=e,Snapshot=p};OnStage?.Invoke();return StageResult;}
 public bool TrySaveAndWitness(IStationMixedStagedPublication s){if(!ReferenceEquals(s,Retained))throw new Exception("reminted stage");Saves++;if(ThrowSave)throw new Exception("uncertain persistence");return SaveResult;}
}

using System;
// TOOLS source integration candidate. One immutable dictionary entry commits publication/projection/chain together.
internal sealed class MixedAtomicPublicationCandidate : IStationMixedAtomicPublication
{
 internal sealed class Stage:IStationMixedStagedPublication {
  internal readonly MixedLiveRecordCandidate Live;internal readonly object Chunk;internal bool DirtyEstablished;
  public RebirthStationCompletionCapture.SuccessfulOutput OriginalEvent{get;private set;}
  public NativePaidOrdinaryRouteLink Route{get;private set;}
  public IStationMixedFinalExpectation Expectation{get;private set;}
  public RebirthStationSnapshotEvidence.Publication Snapshot{get;private set;}
  public long Generation{get;private set;}
  internal Stage(TypedMixedWatchExpectation e,RebirthStationSnapshotEvidence.Publication p,MixedLiveRecordCandidate live,object chunk){Chunk=chunk;OriginalEvent=e.OriginalEvent;Route=e.Route;Expectation=e;Snapshot=p;Generation=e.Generation;Live=live;}
 }
 private Stage stage;private readonly object chunk;internal MixedAtomicPublicationCandidate(object originalChunk){chunk=originalChunk;}
 private bool busy;
 public bool TryStage(IStationMixedFinalExpectation expectation,RebirthStationSnapshotEvidence.Publication snapshot,out IStationMixedStagedPublication retainedStage)
 {
  retainedStage=null;if(busy){retainedStage=stage;return false;}busy=true;
  try {
   if(stage!=null){retainedStage=stage;return ReferenceEquals(stage.Expectation,expectation)&&ReferenceEquals(stage.Snapshot,snapshot)&&Current(stage)&&CommitSame(stage);}
   var typed=expectation as TypedMixedWatchExpectation;var original=typed?.OriginalEvent?.Original;if(original==null||chunk==null||!ReferenceEquals(original.World.GetChunkSync(original.Position.x>>4,original.Position.z>>4),chunk)||!RebirthStationSnapshotEvidence.TryBindPublishedProof(snapshot,original.World,chunk,original.Station,out var preRequest,out var preBytes))return false;
   if(typed==null||snapshot==null||typed.Generation<=0||typed.NativeProjection==null||!typed.MatchesLive()||typed.OriginalEvent.Original.Progression.StationMixedCompletionRecords.ContainsKey(typed.OriginalEvent.Original.Admission.JobId)||
      !MixedLiveRecordCandidate.TryCapturePublished(typed,snapshot,out var live)||!live.RevalidatePublished()||
      !RebirthWorldCharacterRepository.ValidateStationMixedProspective(typed.OriginalEvent.Original.Owner,live.Data))return false;
   if(!RebirthStationSnapshotEvidence.TryBindPublishedProof(snapshot,original.World,chunk,original.Station,out var postRequest,out var postBytes)||!ReferenceEquals(preRequest,postRequest)||!System.Linq.Enumerable.SequenceEqual(preBytes,postBytes)||!ReferenceEquals(original.World.GetChunkSync(original.Position.x>>4,original.Position.z>>4),chunk))return false;var candidate=new Stage(typed,snapshot,live,chunk);
   // Expose exact retained stage BEFORE any original-progression mutation or subsequent uncertainty.
   stage=candidate;retainedStage=candidate;
   return Current(candidate)&&CommitSame(candidate)&&Current(candidate);
  }catch{retainedStage=stage;return false;}finally{busy=false;}
 }
 private static bool Current(Stage s){try{return s!=null&&s.Generation>0&&s.Expectation is TypedMixedWatchExpectation e&&
  ReferenceEquals(e.OriginalEvent,s.OriginalEvent)&&ReferenceEquals(e.Route,s.Route)&&e.Generation==s.Generation&&
  ReferenceEquals(s.OriginalEvent.Original.World.GetChunkSync(s.OriginalEvent.Original.Position.x>>4,s.OriginalEvent.Original.Position.z>>4),s.Chunk)&&s.OriginalEvent.Original.IsCurrent()&&RebirthWorldCharacterRepository.IsCurrentCachedRecord(s.OriginalEvent.Original.Owner)&&s.Live.RevalidatePublished();}catch{return false;}}
 private static bool CommitSame(Stage s){
  var original=s.OriginalEvent.Original;var records=original.Progression.StationMixedCompletionRecords;
  if(records.TryGetValue(s.Live.Data.Job,out var existing)){if(!ReferenceEquals(existing,s.Live.Data)||!Current(s))return false;}
  else {
   if(!Current(s)||!RebirthWorldCharacterRepository.ValidateStationMixedProspective(original.Owner,s.Live.Data))return false;
   records.Add(s.Live.Data.Job,s.Live.Data);
  }
  // Add may have committed before a prior MarkDirty threw. Retry the SAME retained obligation.
  if(!s.DirtyEstablished){RebirthWorldCharacterService.MarkDirty(original.Owner,"Original mixed station publication");if(!Current(s))return false;s.DirtyEstablished=true;}
  return records.TryGetValue(s.Live.Data.Job,out existing)&&ReferenceEquals(existing,s.Live.Data)&&Current(s);
 }
 public bool TrySaveAndWitness(IStationMixedStagedPublication retainedStage)
 {
  if(busy||stage==null||!ReferenceEquals(retainedStage,stage))return false;busy=true;
  try {
   if(!Current(stage)||!CommitSame(stage))return false;
   var original=stage.OriginalEvent.Original;
   // A false clean/uncertain write result cannot substitute for proof, nor destroy this stage.
   RebirthWorldCharacterRepository.SaveIfDirty(original.Identity,"Original mixed station publication");
   return Current(stage)&&RebirthWorldCharacterRepository.HasSavedStationMixedOriginal(original.Identity,original.Owner,stage.Live)&&Current(stage);
  }catch{return false;}finally{busy=false;}
 }
}

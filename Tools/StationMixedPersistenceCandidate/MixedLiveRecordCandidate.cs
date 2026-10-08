using System;
using System.Xml.Linq;
// TOOLS candidate: creation is bound to original typed paid exit and authenticated native-region publication.
internal sealed class MixedLiveRecordCandidate {
 readonly object chunk;readonly MixedCompletionRecord data;readonly TypedMixedWatchExpectation expectation;readonly MixedNativePublicationCandidate publication;
 readonly RebirthStationGridAdmission admission;readonly RebirthStationTerminalIntent intent;readonly RebirthStationPublicationRecord queued;
 private MixedLiveRecordCandidate(MixedCompletionRecord d,TypedMixedWatchExpectation e,MixedNativePublicationCandidate p,RebirthStationGridAdmission a,RebirthStationTerminalIntent i,RebirthStationPublicationRecord q,object originalChunk){chunk=originalChunk;data=d;expectation=e;publication=p;admission=a;intent=i;queued=q;}
 internal MixedCompletionRecord Data=>data;
 internal MixedNativePublicationCandidate Publication=>publication;
 internal TypedMixedWatchExpectation Expectation=>expectation;
 internal static bool TryCapturePublished(TypedMixedWatchExpectation expected,RebirthStationSnapshotEvidence.Publication proof,out MixedLiveRecordCandidate result){
  result=null;try{
   var original=expected?.OriginalEvent?.Original;var nativeChunk=original?.World.GetChunkSync(original.Position.x>>4,original.Position.z>>4);if(original==null||nativeChunk==null||!RebirthStationSnapshotEvidence.TryBindPublishedProof(proof,original.World,nativeChunk,original.Station,out var preRequest,out var preBytes))return false;
   if(original==null||expected.NativeProjection==null||!expected.MatchesLive()||!original.IsCurrent()||
      !original.Progression.StationPreparations.TryGetValue(original.Admission.JobId,out var a)||a==null||
      !original.Progression.StationPublications.TryGetValue(a.JobId,out var q)||q==null||
      !original.Progression.StationTerminalIntents.TryGetValue(a.JobId,out var i)||i==null||
      !MixedNativePublicationCandidate.ValidOriginals(a,i,q)||
      !MixedNativePublicationCandidate.TryCreate(original.SaveRoot,a,i,q,expected,proof,out var completed)||
      !completed.TryCopyValidated(original.SaveRoot,a,i,q,expected,out var native)||
      !NativePaidOrdinaryChainCodec.TryRead(expected.NativeExpectation.FrozenChainData,out var chain)||
      !MixedCompletionRecord.TryCreateData(a.Write(),i.Write(),q.Write(),completed.Write(),chain,native,out var data)||
      !original.Progression.StationPreparations.TryGetValue(a.JobId,out var aa)||!ReferenceEquals(a,aa)||
      !original.Progression.StationPublications.TryGetValue(a.JobId,out var qq)||!ReferenceEquals(q,qq)||
      !original.Progression.StationTerminalIntents.TryGetValue(a.JobId,out var ii)||!ReferenceEquals(i,ii)||!expected.MatchesLive()||!original.IsCurrent())return false;
   if(!RebirthStationSnapshotEvidence.TryBindPublishedProof(proof,original.World,nativeChunk,original.Station,out var postRequest,out var postBytes)||!ReferenceEquals(preRequest,postRequest)||!System.Linq.Enumerable.SequenceEqual(preBytes,postBytes)||!ReferenceEquals(original.World.GetChunkSync(original.Position.x>>4,original.Position.z>>4),nativeChunk))return false;result=new MixedLiveRecordCandidate(data,expected,completed,a,i,q,nativeChunk);return true;
  }catch{return false;}
 }
 internal bool RevalidatePublished(){try{var original=expectation.OriginalEvent.Original;return ReferenceEquals(original.World.GetChunkSync(original.Position.x>>4,original.Position.z>>4),chunk)&&original.IsCurrent()&&
  original.Progression.StationPreparations.TryGetValue(admission.JobId,out var a)&&ReferenceEquals(a,admission)&&
  original.Progression.StationPublications.TryGetValue(admission.JobId,out var q)&&ReferenceEquals(q,queued)&&
  original.Progression.StationTerminalIntents.TryGetValue(admission.JobId,out var i)&&ReferenceEquals(i,intent)&&
  publication.Revalidate(original.SaveRoot,admission,intent,queued,expectation)&&original.IsCurrent();}catch{return false;}}
}

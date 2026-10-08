using System;
using System.Linq;
using System.Xml.Linq;
// TOOLS ONLY: original live projection retained by reference; cold data cannot acquire it.
internal sealed class MixedLiveRecordCandidate {
 readonly MixedCompletionRecord data;
 readonly MixedProjectionCandidate projection;
 readonly MixedExitExpectationCandidate expectation;
 private MixedLiveRecordCandidate(MixedCompletionRecord d,MixedProjectionCandidate p,MixedExitExpectationCandidate e){data=d;projection=p;expectation=e;}
 internal MixedCompletionRecord Data=>data;
 internal static bool TryCapture(MixedExitExpectationCandidate original,XElement admission,XElement intent,XElement queued,XElement completed,byte[][] nativeSpans,out MixedLiveRecordCandidate result){
  result=null;try{
   if(original==null||!original.MatchesLive()||!NativePaidOrdinaryChainCodec.TryRead(original.FrozenChainData,out var chain)||
      !MixedProjectionCandidate.TryCapture(original,nativeSpans,out var projection)||
      !MixedCompletionRecord.TryCreateData(admission,intent,queued,completed,chain,projection.CopyNativeSpans(),out var data)||
      !projection.MatchesCurrentAndNative(nativeSpans)||!original.MatchesLive())return false;
   result=new MixedLiveRecordCandidate(data,projection,original);return true;
  }catch{return false;}
 }
 internal bool MatchesRetainedOriginalAndNative(MixedCompletionRecord exactRetained,byte[][] finalNativeSpans){
  try{return ReferenceEquals(exactRetained,data)&&expectation.MatchesLive()&&projection.MatchesCurrentAndNative(finalNativeSpans)&&expectation.MatchesLive();}catch{return false;}
 }
}

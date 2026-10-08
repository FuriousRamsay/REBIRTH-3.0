using System;
// Tools-only complete live state seal, issued only from retained typed original normal-exit witness.
internal sealed class MixedExitExpectationCandidate
{
 readonly MixedExpectationCandidate live;
 readonly NativePaidOrdinaryExitCandidate.ExitWitness exit;
 readonly RebirthStationCompletionCapture.SuccessfulOutput paid;
 readonly RebirthStationNativeInputSnapshot input;
 readonly RebirthStationNativeQueueSnapshot queue;
 MixedExitExpectationCandidate(MixedExpectationCandidate l,NativePaidOrdinaryExitCandidate.ExitWitness e,RebirthStationCompletionCapture.SuccessfulOutput p,RebirthStationNativeInputSnapshot i,RebirthStationNativeQueueSnapshot q){live=l;exit=e;paid=p;input=i;queue=q;}
 internal static bool TryCreate(MixedExpectationCandidate.Anchor anchor,RebirthStationCompletionCapture.SuccessfulOutput paid,NativePaidOrdinaryRouteLink route,NativePaidOrdinaryExitCandidate.ExitWitness exit,out MixedExitExpectationCandidate result)
 {
  result=null;
  try {
   if(paid==null||exit==null||!exit.MatchesOriginal(paid)||!exit.MatchesRoute(route)||!exit.IsCurrent()||
      !MixedExpectationCandidate.TryCreate(anchor,route,out var live)||
      !RebirthStationNativeInputSnapshot.TryCapture(paid.Original.Station.Input,out var input)||
      !RebirthStationNativeQueueSnapshot.TryCapture(paid.Original.Station.Queue,out var queue))return false;
   var candidate=new MixedExitExpectationCandidate(live,exit,paid,input,queue);
   if(!candidate.MatchesLive())return false;result=candidate;return true;
  }catch{return false;}
 }
 internal bool MatchesLive()
 {
  try{return exit.MatchesOriginal(paid)&&exit.IsCurrent()&&live.MatchesLive()&&input.Matches(paid.Original.Station.Input)&&queue.Matches(paid.Original.Station.Queue)&&exit.IsCurrent()&&live.MatchesLive()&&paid.Original.IsCurrent();}catch{return false;}
 }
 internal bool MatchesNativeSpans(byte[][] spans)
 {
  try{if(spans==null||spans.Length!=4||System.Linq.Enumerable.Any(spans,b=>b==null||b.Length<1||b.Length>256*1024))return false;var frozen=System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Select(spans,b=>(byte[])b.Clone()));return MatchesLive()&&input.MatchesSerializedInput(frozen[0])&&queue.MatchesSerializedQueue(frozen[1])&&MixedProjectionCandidate.MatchesNativeTerminal(live,frozen[2],frozen[3])&&MatchesLive()&&System.Linq.Enumerable.All(System.Linq.Enumerable.Zip(spans,frozen,(a,b)=>a!=null&&System.Linq.Enumerable.SequenceEqual(a,b)),x=>x);}catch{return false;}
 }
 internal string FrozenChainData=>live.FrozenChainData;
 internal string OriginalPaidReceiptImage=>live.OriginalPaidReceiptImage;
 internal int OriginalPaidQuantity=>live.OriginalPaidQuantity;
}


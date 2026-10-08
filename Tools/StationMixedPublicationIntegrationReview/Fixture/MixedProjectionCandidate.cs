using System;
using System.Linq;
// Tools-only native semantic projection. Cold bytes cannot construct typed original authority.
internal sealed class MixedProjectionCandidate
{
 readonly byte[][] spans;readonly MixedExitExpectationCandidate original;
 private MixedProjectionCandidate(MixedExitExpectationCandidate e,byte[][] bytes){original=e;spans=bytes.Select(x=>(byte[])x.Clone()).ToArray();}
 internal static bool TryCapture(MixedExitExpectationCandidate expectation,byte[][] nativeSpans,out MixedProjectionCandidate result)
 {
  result=null;try {
   if(expectation==null||nativeSpans==null||nativeSpans.Length!=4||nativeSpans.Any(b=>b==null||b.Length<1||b.Length>256*1024))return false;
   var copy=nativeSpans.Select(b=>(byte[])b.Clone()).ToArray();
   if(!expectation.MatchesNativeSpans(copy)||!nativeSpans.Zip(copy,(a,b)=>a.SequenceEqual(b)).All(x=>x)||!expectation.MatchesLive()||!nativeSpans.Zip(copy,(a,b)=>a!=null&&a.SequenceEqual(b)).All(x=>x))return false;
   result=new MixedProjectionCandidate(expectation,copy);return true;
  }catch{return false;}
 }
 internal bool MatchesCurrentAndNative(byte[][] bytes)
 {try{return bytes!=null&&bytes.Length==4&&bytes.Zip(spans,(a,b)=>a!=null&&a.SequenceEqual(b)).All(x=>x)&&original.MatchesNativeSpans(bytes)&&bytes.Zip(spans,(a,b)=>a!=null&&a.SequenceEqual(b)).All(x=>x)&&original.MatchesLive()&&bytes.Zip(spans,(a,b)=>a!=null&&a.SequenceEqual(b)).All(x=>x);}catch{return false;}}
 internal byte[][] CopyNativeSpans()=>spans.Select(b=>(byte[])b.Clone()).ToArray();
 internal static bool MatchesNativeTerminal(MixedExpectationCandidate expectation,byte[] output,byte[] completions)
 {
  try {
   if(expectation==null||output==null||output.Length<1||completions==null||!expectation.MatchesLive()||
      !RebirthStationTerminalContents.TryReadOutput(output,output[0],out var decodedOutput)||
      !RebirthNativeItemConformanceReader.TryDecodeStationCompletions(completions,out var decodedReceipts))return false;
   return expectation.MatchesDecodedTerminal(decodedOutput,decodedReceipts)&&expectation.MatchesLive();
  }catch{return false;}
 }
}



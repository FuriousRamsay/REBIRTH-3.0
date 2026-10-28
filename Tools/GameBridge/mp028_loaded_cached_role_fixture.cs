// AUTHORED_NOT_RUN. Actual cached-role gate; actual loaded source issuer is
// an explicit Boolean adapter. This cannot certify native load or dispatch.
using System;
class Cold{internal bool FinalCallbackSaved;}
class Observation{internal Cold RemoteOutputRestart=new Cold();internal bool FullBoundary=true,Continued,Accepted;}
class Frame{internal Observation Observation=new Observation();internal bool Loaded=true,NativeAccepted;internal object ConfirmedOriginal;}
static class RebirthWorkstationCompletionPipeline{
 static bool IsLoadedDeferredFrame(Frame frame)=>frame?.Loaded==true;
 // ACTUAL_CACHED_ROLE
 static int checks;static void Need(bool value){if(!value)throw new Exception("Loaded cached role "+checks);checks++;}
 static void Main(){var frame=new Frame();Need(IsLoadedDeferredCachedFrame(frame));
  frame=new Frame{Loaded=false};Need(!IsLoadedDeferredCachedFrame(frame));
  frame=new Frame();frame.Observation.RemoteOutputRestart.FinalCallbackSaved=true;Need(!IsLoadedDeferredCachedFrame(frame));
  frame=new Frame();frame.Observation.FullBoundary=false;Need(!IsLoadedDeferredCachedFrame(frame));
  frame=new Frame();frame.Observation.Continued=true;Need(!IsLoadedDeferredCachedFrame(frame));
  frame=new Frame();frame.Observation.Accepted=true;Need(!IsLoadedDeferredCachedFrame(frame));
  frame=new Frame{NativeAccepted=true};Need(!IsLoadedDeferredCachedFrame(frame));
  frame=new Frame{ConfirmedOriginal=new object()};Need(!IsLoadedDeferredCachedFrame(frame));
  Need(!IsLoadedDeferredCachedFrame(null));Console.WriteLine("PASS "+checks+" actual loaded cached role assertions; native issuer adapter excluded");}
}

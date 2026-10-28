// Actual callback evidence writer; pending/native callback invocation itself is
// an explicit adapter. This creates no response scope or financial permission.
public static partial class RebirthNpcInteractionNetworkClient
{
 private sealed class PendingRequest
 {
  internal uint ConnectionEpoch=3;internal ulong Generation;internal long DeadlineUtcTicks=7;internal Action Callback=()=>{};
  internal RebirthNpcTradeOriginal NativeTradeOriginal;internal string NativeTradeOperation,NativeTradeTerminal;
 }
 private static Dictionary<ulong,PendingRequest> Pending=new Dictionary<ulong,PendingRequest>();
 // ACTUAL_TRADE_CALLBACK_IMAGE
 private static bool CaptureRefused(){try{QuarantineInteractionCallbackImage();return false;}catch(InvalidOperationException){return true;}}
 private static void Need(bool value,string message){if(!value)throw new Exception(message);}
 internal static void RunPurposeFixture(RebirthNpcTradeOriginal original)
 {
  Pending.Clear();var request=new PendingRequest{Generation=original.CopyQuote().RecoveryGeneration};Pending.Add(1,request);
  try
  {
   var generic=QuarantineInteractionCallbackImage();Need(generic.SelectSingleNode("/interactionCallbackQuarantine/request/nativeTradeOriginal")==null,"generic callback became trade original");
   request.NativeTradeOriginal=original;request.NativeTradeOperation="prepareNative";
   var prepared=QuarantineInteractionCallbackImage();var row=(XmlElement)prepared.SelectSingleNode("/interactionCallbackQuarantine/request");
   Need(row.GetAttribute("nativeTradeDigest")==original.Digest&&row.SelectSingleNode("nativeTradeOriginal").InnerText==original.Image,"full native callback original not retained");
   Need(row.GetAttribute("nativeTradeOperation")=="prepareNative","native prepare purpose missing");
   request.NativeTradeOperation="settleNative";Need(((XmlElement)QuarantineInteractionCallbackImage().SelectSingleNode("/interactionCallbackQuarantine/request")).GetAttribute("nativeTradeOperation")=="settleNative","settle purpose lost");
   Need(row.GetAttribute("nativeTradeOperation")=="prepareNative","captured evidence changed with live request");
   request.NativeTradeOperation="ackNative";request.NativeTradeTerminal=new string('a',64);
   Need(((XmlElement)QuarantineInteractionCallbackImage().SelectSingleNode("/interactionCallbackQuarantine/request")).GetAttribute("nativeTradeTerminal")==request.NativeTradeTerminal,"terminal binding missing");
   request.NativeTradeTerminal=null;Need(CaptureRefused(),"ACK without terminal accepted");request.NativeTradeTerminal=new string('a',64);
   request.Generation++;Need(CaptureRefused(),"foreign recovery generation retained as same native purpose");request.Generation--;
   request.NativeTradeOperation="other";Need(CaptureRefused(),"unknown native operation retained");request.NativeTradeOperation="ackNative";
   request.NativeTradeOriginal=null;Need(CaptureRefused(),"native callback without original accepted");
  }
  finally{Pending.Clear();}
 }
}

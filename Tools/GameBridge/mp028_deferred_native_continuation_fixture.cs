// AUTHORED_NOT_RUN. Actual whole-body output-only comparison; native codec is
// a disclosed three-cell queue/completion/output adapter, not native authority.
using System;using System.Linq;
class Body{internal int Queue,Completions,Output;}
static class RebirthProductiveEarnedNativeBody{
 internal static bool TryRead(byte[] bytes,int offset,out Body body,out ulong elapsed){elapsed=0;body=null;if(bytes==null||bytes.Length!=3||offset!=4)return false;body=new Body{Queue=bytes[0],Completions=bytes[1],Output=bytes[2]};return true;}
 internal static byte[] Encode(Body body,ulong elapsed,out int offset){offset=4;return new[]{(byte)body.Queue,(byte)body.Completions,(byte)body.Output};}
 internal static bool SameExceptElapsed(byte[] first,int firstOffset,byte[] last,int lastOffset)=>firstOffset==lastOffset&&first.SequenceEqual(last);
}
static class RebirthWorkstationCompletionPipeline{
 // ACTUAL_OUTPUT_ONLY
 static int checks;static void Need(bool value){if(!value)throw new Exception("Deferred native continuation "+checks);checks++;}
 static void Main(){
  var initial=new byte[]{2,7,3};Need(SameDeferredNativeOutsideOutput(initial,4,new byte[]{2,7,8},4));
  Need(initial.SequenceEqual(new byte[]{2,7,3}));
  Need(!SameDeferredNativeOutsideOutput(initial,4,new byte[]{1,7,8},4));
  Need(!SameDeferredNativeOutsideOutput(initial,4,new byte[]{2,6,8},4));
  Need(!SameDeferredNativeOutsideOutput(initial,9,new byte[]{2,7,8},4));
  Need(!SameDeferredNativeOutsideOutput(initial,4,new byte[]{2,7},4));
  Console.WriteLine("PASS "+checks+" actual deferred whole-body output-only assertions; codec adapter excluded");
 }
}

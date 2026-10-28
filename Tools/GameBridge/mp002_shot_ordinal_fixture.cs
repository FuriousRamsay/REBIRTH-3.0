using System;
using System.Linq;
using System.Collections.Concurrent;
using System.Threading.Tasks;
// AUTHORED_NOT_RUN. Actual ordinal allocator, no native world/action mocked.
internal static class Program {
 private static int nativeShotSequence;
// ACTUAL_SHOT_ORDINAL
 static void Need(bool value,string error){if(!value)throw new Exception(error);}
 public static void Main(){
  Need(NextNativeShotOrdinal()==1,"first ordinal invalid");
  nativeShotSequence=int.MaxValue-1;Need(NextNativeShotOrdinal()==int.MaxValue,"last ordinal lost");
  for(int i=0;i<100;i++)Need(NextNativeShotOrdinal()==0&&nativeShotSequence==int.MaxValue,"exhausted allocator wrapped");
  nativeShotSequence=0;var seen=new ConcurrentDictionary<int,bool>();
  Parallel.For(0,1000,i=>{if(!seen.TryAdd(NextNativeShotOrdinal(),true))throw new Exception("duplicate concurrent ordinal");});
  Need(seen.Count==1000&&seen.Keys.Min()==1&&seen.Keys.Max()==1000,"concurrent sequence has gaps or invalid values");
  Console.WriteLine("PASS actual saturated native-shot ordinal allocator; no gameplay evidence");
 }
}

using System;
using System.Collections.Generic;
using System.Threading;
// SCRATCH_HELPER
public static class SelectionScratchChecks {
 static void Check(bool v,string m){if(!v)throw new Exception(m);}
 public static void Run(){
  List<double> first;
  using(var a=RebirthSpawnSelectionScratchLease.Acquire(4)){
   first=a.EntityWeights;Check(a.EntityWeights.Count==0&&a.LogWeights.Count==0,"initial empty");a.EntityWeights.Add(7);a.LogWeights.Add(2);
   using(var nested=RebirthSpawnSelectionScratchLease.Acquire(4)){Check(!Object.ReferenceEquals(first,nested.EntityWeights),"nested transient");nested.EntityWeights.Add(8);Check(a.EntityWeights[0]==7,"nested isolation");}
   Check(a.EntityWeights.Count==1,"nested release leaves outer");
  }
  using(var a=RebirthSpawnSelectionScratchLease.Acquire(256)){Check(Object.ReferenceEquals(first,a.EntityWeights),"same-thread reuse");Check(a.EntityWeights.Count==0&&a.LogWeights.Count==0,"release cleared");Check(a.EntityWeights.Capacity<=256&&a.LogWeights.Capacity<=256,"bounded capacity");}
  try{using(var a=RebirthSpawnSelectionScratchLease.Acquire(4)){a.EntityWeights.Add(9);throw new InvalidOperationException();}}catch(InvalidOperationException){}
  using(var a=RebirthSpawnSelectionScratchLease.Acquire(4)){Check(Object.ReferenceEquals(first,a.EntityWeights)&&a.EntityWeights.Count==0,"exception released");}
  var prior=RebirthSpawnSelectionScratchLease.Acquire(4);var copied=prior;prior.Dispose();using(var current=RebirthSpawnSelectionScratchLease.Acquire(4)){current.EntityWeights.Add(11);copied.Dispose();Check(current.EntityWeights.Count==1&&current.EntityWeights[0]==11,"stale token doesn't clear new lease");}
  List<double> oversized;using(var a=RebirthSpawnSelectionScratchLease.Acquire(257)){oversized=a.EntityWeights;Check(!Object.ReferenceEquals(first,oversized),"oversize transient");}
  using(var a=RebirthSpawnSelectionScratchLease.Acquire(257)){Check(!Object.ReferenceEquals(oversized,a.EntityWeights),"oversize not retained");}
  default(RebirthSpawnSelectionScratchLease).Dispose();
  bool negative=false;try{RebirthSpawnSelectionScratchLease.Acquire(-1);}catch(ArgumentOutOfRangeException){negative=true;}Check(negative,"negative capacity refused");
  var ownerLease=RebirthSpawnSelectionScratchLease.Acquire(4);ownerLease.EntityWeights.Add(12);Exception crossThread=null;var wrongThread=new Thread(()=>{try{ownerLease.Dispose();}catch(Exception e){crossThread=e;}});wrongThread.Start();wrongThread.Join();Check(crossThread is InvalidOperationException&&ownerLease.EntityWeights.Count==1,"cross-thread disposal refused before clear");ownerLease.Dispose();
  List<double> other=null;Exception error=null;var worker=new Thread(()=>{try{using(var a=RebirthSpawnSelectionScratchLease.Acquire(4)){other=a.EntityWeights;a.EntityWeights.Add(5);}using(var b=RebirthSpawnSelectionScratchLease.Acquire(4)){Check(Object.ReferenceEquals(other,b.EntityWeights)&&b.EntityWeights.Count==0,"worker reuse");}}catch(Exception e){error=e;}});worker.Start();worker.Join();if(error!=null)throw error;Check(other!=null&&!Object.ReferenceEquals(first,other),"thread isolation");
 }
}


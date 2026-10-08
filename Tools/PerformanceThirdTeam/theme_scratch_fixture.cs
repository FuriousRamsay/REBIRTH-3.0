using System;
using System.Collections.Generic;
using System.Threading;
// SCRATCH_HELPER
public static class ThemeScratchChecks {
 static void Check(bool v,string m){if(!v)throw new Exception(m);}
 public static void Run(){
  List<double> first;
  using(var a=RebirthThemeScratchLease.Acquire(4)){
   first=a.Multipliers;Check(a.Multipliers.Count==0&&a.Weights.Count==0,"initial empty");a.Multipliers.Add(7);a.Weights.Add(2);
   using(var nested=RebirthThemeScratchLease.Acquire(4)){Check(!Object.ReferenceEquals(first,nested.Multipliers),"nested transient");nested.Multipliers.Add(8);Check(a.Multipliers[0]==7,"nested isolation");}
   Check(a.Multipliers.Count==1,"nested release leaves outer");
  }
  using(var a=RebirthThemeScratchLease.Acquire(256)){Check(Object.ReferenceEquals(first,a.Multipliers),"same-thread reuse");Check(a.Multipliers.Count==0&&a.Weights.Count==0,"release cleared");Check(a.Multipliers.Capacity<=256&&a.Weights.Capacity<=256,"bounded capacity");}
  try{using(var a=RebirthThemeScratchLease.Acquire(4)){a.Multipliers.Add(9);throw new InvalidOperationException();}}catch(InvalidOperationException){}
  using(var a=RebirthThemeScratchLease.Acquire(4)){Check(Object.ReferenceEquals(first,a.Multipliers)&&a.Multipliers.Count==0,"exception released");}
  var prior=RebirthThemeScratchLease.Acquire(4);var copied=prior;prior.Dispose();using(var current=RebirthThemeScratchLease.Acquire(4)){current.Multipliers.Add(11);copied.Dispose();Check(current.Multipliers.Count==1&&current.Multipliers[0]==11,"stale token doesn't clear new lease");}
  List<double> oversized;using(var a=RebirthThemeScratchLease.Acquire(257)){oversized=a.Multipliers;Check(!Object.ReferenceEquals(first,oversized),"oversize transient");}
  using(var a=RebirthThemeScratchLease.Acquire(257)){Check(!Object.ReferenceEquals(oversized,a.Multipliers),"oversize not retained");}
  default(RebirthThemeScratchLease).Dispose();
  bool negative=false;try{RebirthThemeScratchLease.Acquire(-1);}catch(ArgumentOutOfRangeException){negative=true;}Check(negative,"negative capacity refused");
  var ownerLease=RebirthThemeScratchLease.Acquire(4);ownerLease.Multipliers.Add(12);Exception crossThread=null;var wrongThread=new Thread(()=>{try{ownerLease.Dispose();}catch(Exception e){crossThread=e;}});wrongThread.Start();wrongThread.Join();Check(crossThread is InvalidOperationException&&ownerLease.Multipliers.Count==1,"cross-thread disposal refused before clear");ownerLease.Dispose();
  List<double> other=null;Exception error=null;var worker=new Thread(()=>{try{using(var a=RebirthThemeScratchLease.Acquire(4)){other=a.Multipliers;a.Multipliers.Add(5);}using(var b=RebirthThemeScratchLease.Acquire(4)){Check(Object.ReferenceEquals(other,b.Multipliers)&&b.Multipliers.Count==0,"worker reuse");}}catch(Exception e){error=e;}});worker.Start();worker.Join();if(error!=null)throw error;Check(other!=null&&!Object.ReferenceEquals(first,other),"thread isolation");
 }
}

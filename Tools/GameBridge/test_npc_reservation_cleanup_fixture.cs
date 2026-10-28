using System;
using System.Collections.Generic;
using System.Threading;
// Native endpoint disposal/logger/journal IO are explicit doubles. Extracted
// production helpers cover cleanup, exact publication and session-owned release.
interface IRebirthNpcExternalInventoryReservation:IDisposable{}
class Reservation:IRebirthNpcExternalInventoryReservation {public bool Fail;public void Dispose(){if(Fail)throw new InvalidOperationException("rollback failed");}}
enum RebirthNpcExternalTransferResult {Applied,Indeterminate,RollbackFailed}
static class Log {internal static int Calls;internal static bool Fail;internal static void Warning(string message){Calls++;if(Fail)throw new Exception("logger failed");}}
class Test {
 class ReplayRecord {public RebirthNpcExternalTransferResult Result;public uint Revision;public string Fingerprint;}
 static readonly object Sync=new object();static readonly HashSet<Guid> InFlight=new HashSet<Guid>();
 static readonly Dictionary<Guid,ReplayRecord> Replay=new Dictionary<Guid,ReplayRecord>();
 static long journalGeneration=1;static int disposalWarnings;static bool FailPublication;
 // IO is a boundary: this does NOT claim native file durability or full Apply.
 static void Remember(Guid id,RebirthNpcExternalTransferResult result,uint revision,string fingerprint,string directory,long generation){if(FailPublication||generation!=journalGeneration)throw new InvalidOperationException("publication failed");if(!Replay.ContainsKey(id))Replay[id]=new ReplayRecord{Result=result,Revision=revision,Fingerprint=fingerprint};}
 // SOURCE
 // RELEASE
 // PUBLISHED
 static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
 static void Main(){
  Guid id=Guid.NewGuid();
  InFlight.Add(id);ReleaseReservation(null,id,1);Check(!InFlight.Contains(id),"null releases");
  InFlight.Add(id);ReleaseReservation(new Reservation(),id,1);Check(!InFlight.Contains(id),"normal releases");
  InFlight.Add(id);bool failed=false;try{ReleaseReservation(new Reservation{Fail=true},id,1);}catch(InvalidOperationException){failed=true;}
  Check(failed&&!InFlight.Contains(id),"unpublished exception preserved and releases");
  foreach(var outcome in new[]{RebirthNpcExternalTransferResult.Applied,RebirthNpcExternalTransferResult.Indeterminate,RebirthNpcExternalTransferResult.RollbackFailed}){
   Replay.Clear();bool published=false;RememberPublished(id,outcome,7,"exact","captured",1,ref published);Check(published,"successful publication flag");
   InFlight.Add(id);ReleaseReservation(new Reservation{Fail=true},id,1,published);Check(!InFlight.Contains(id)&&Replay[id].Result==outcome,"terminal outcome retained");
  }
  Replay.Clear();bool unpublished=false;FailPublication=true;failed=false;try{RememberPublished(id,RebirthNpcExternalTransferResult.Applied,7,"exact","captured",1,ref unpublished);}catch(InvalidOperationException){failed=true;}
  Check(failed&&!unpublished,"failed publication cannot authorize suppression");FailPublication=false;
  Replay[id]=new ReplayRecord{Result=RebirthNpcExternalTransferResult.Applied,Revision=99,Fingerprint="foreign"};failed=false;
  try{RememberPublished(id,RebirthNpcExternalTransferResult.Applied,7,"exact","captured",1,ref unpublished);}catch(InvalidOperationException){failed=true;}
  Check(failed&&!unpublished&&Replay[id].Revision==99,"different existing record refused untouched");
  InFlight.Add(id);ReleaseReservation(new Reservation{Fail=true},id,0,true);Check(InFlight.Contains(id),"old cleanup cannot release new session");InFlight.Clear();
  var original=new Exception("original failure");Exception observed=null;InFlight.Add(id);
  try{try{throw original;}finally{ReleaseReservation(new Reservation{Fail=true},id,1,false,true);}}catch(Exception ex){observed=ex;}
  Check(ReferenceEquals(original,observed)&&!InFlight.Contains(id),"original exception preserved");
  Log.Fail=true;InFlight.Add(id);ReleaseReservation(new Reservation{Fail=true},id,1,true);Check(!InFlight.Contains(id),"logger cannot mask outcome");Log.Fail=false;
  disposalWarnings=0;Log.Calls=0;for(int i=0;i<20;i++)ReleaseReservation(new Reservation{Fail=true},Guid.NewGuid(),1,true);Check(Log.Calls==16,"warning budget16");
  Console.WriteLine("PASS: actual publication/cleanup/release; unpublished throw, all terminal outcomes, mismatched publication, original/logger failures, stale release and bounded warnings");
 }
}
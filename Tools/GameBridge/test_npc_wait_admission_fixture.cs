using System;
using System.IO;
using System.Threading;
using System.Collections.Generic;
class GameIO {public static string GetSaveGameDir(){return "A";}}
class RebirthNpcPersistenceFile {public static void AssertWritable(string path){} }
class Test {
 static readonly object Sync=new object();static readonly HashSet<Guid> InFlight=new HashSet<Guid>();
 static long journalGeneration=1;static string loadedJournalDirectory="A";const string JournalFileName="journal.xml";
 // SOURCE
 static void Main(){
  var id=Guid.NewGuid();var started=new ManualResetEvent(false);var done=new ManualResetEvent(false);Exception failure=null;
  lock(Sync)InFlight.Add(id);
  var waiter=new Thread(()=>{lock(Sync){started.Set();try{WaitForReplayAdmission(id,"A",1);}catch(Exception ex){failure=ex;}finally{done.Set();}}});waiter.IsBackground=true;waiter.Start();
  if(!started.WaitOne(2000))throw new Exception("waiter not started");
  lock(Sync){journalGeneration=2;Monitor.PulseAll(Sync);} // Same ID is still in-flight in the replacement session.
  if(!done.WaitOne(2000))throw new Exception("stale waiter waited on replacement session");
  if(!(failure is InvalidOperationException))throw new Exception("stale waiter admitted");
  if(!waiter.Join(2000))throw new Exception("waiter did not exit");
  lock(Sync){InFlight.Remove(id);WaitForReplayAdmission(id,"A",2);}
  Console.WriteLine("PASS actual wait/admission helpers: old-session retry exits after wake despite same-ID in-flight replacement; current-session empty admission succeeds. Save/write checks substituted.");
 }
}

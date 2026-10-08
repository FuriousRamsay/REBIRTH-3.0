using System;
using System.Threading;
class Test {
 static readonly object Sync=new object();static string loadedJournalDirectory="A";static long journalGeneration=1;static bool Fail;static int Writes;
 static void EnsureReplayLoaded(){if(!Monitor.IsEntered(Sync))throw new Exception("load unlocked");}
 static void AssertReplayAdmission(string dir,long generation){if(Fail)throw new InvalidOperationException("unavailable");if(dir!="A"||generation!=1||!Monitor.IsEntered(Sync))throw new Exception("bad admission");}
 static void TryPersistReplayJournal(string dir){if(dir!="A"||!Monitor.IsEntered(Sync))throw new Exception("write unlocked");Writes++;}
 // SOURCE
 static void Main(){SaveCheckpoint();Fail=true;bool failed=false;try{SaveCheckpoint();}catch(InvalidOperationException){failed=true;}if(!failed||Writes!=1)throw new Exception("invalid checkpoint wrote");Console.WriteLine("PASS: checkpoint holds journal lock across load/admission/write and does not write unavailable history");}
}

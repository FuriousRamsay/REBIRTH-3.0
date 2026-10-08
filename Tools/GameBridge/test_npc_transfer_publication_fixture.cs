using System;
using System.Collections.Generic;
enum RebirthNpcExternalTransferResult {Applied}
class Test {
 class ReplayRecord {public RebirthNpcExternalTransferResult Result;public uint Revision;public string Fingerprint;}
 static readonly object Sync=new object();static readonly Dictionary<Guid,ReplayRecord> Replay=new Dictionary<Guid,ReplayRecord>();static readonly Queue<Guid> ReplayOrder=new Queue<Guid>();const int MaxReplayRecords=1024;static int Saves;static string Saved;
 static void AssertReplayAdmission(string dir,long gen){if(dir!="A"||gen!=2)throw new InvalidOperationException("stale");}
 static void TryPersistReplayJournal(string dir){Saves++;Saved=dir;}
 // SOURCE
 static void Main(){Guid id=Guid.NewGuid();bool failed=false;try{Remember(id,RebirthNpcExternalTransferResult.Applied,1,"f","A",1);}catch(InvalidOperationException){failed=true;}if(!failed||Replay.Count!=0||Saves!=0)throw new Exception("stale published");Remember(id,RebirthNpcExternalTransferResult.Applied,1,"f","A",2);Remember(id,RebirthNpcExternalTransferResult.Applied,1,"f","A",2);if(Replay.Count!=1||Saves!=1||Saved!="A")throw new Exception("publication broken");Console.WriteLine("PASS: stale publication rejected before mutation/write; current result persisted once to originating directory");}
}

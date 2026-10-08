using System;
using System.Collections.Generic;
using System.Threading;
class Test {
 static readonly object Sync=new object();static readonly HashSet<Guid> InFlight=new HashSet<Guid>();static long journalGeneration=2;
 // SOURCE
 static void Main(){Guid id=Guid.NewGuid();InFlight.Add(id);ReleaseInFlight(id,1);if(!InFlight.Contains(id))throw new Exception("old session released new lock");ReleaseInFlight(id,2);if(InFlight.Contains(id))throw new Exception("current session lock retained");Console.WriteLine("PASS: stale-session completion cannot release current-session request; current completion releases it");}
}

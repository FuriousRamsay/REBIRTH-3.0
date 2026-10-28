// AUTHORED_NOT_RUN. Actual failed-attempt retirement; domain restoration doubled.
using System;
using System.Collections.Generic;
class Actual {
 class Retained {internal bool Detached,RollbackIncomplete;internal object Domains;internal string EvidencePath;}
 static readonly List<Retained> retained=new List<Retained>();
 static bool RestoreOriginalDomains(object domains)=>domains is bool result&&result;
 // ACTUAL_FAILED_ATTEMPT
 static void Require(bool value,string message){if(!value)throw new Exception(message);}
 internal static void Run(){
  var restored=new Retained{Domains=true,EvidencePath="unchanged-forensic-file"};retained.Add(restored);
  Require(RestoreFailedAttempt(restored)&&!retained.Contains(restored)&&!restored.RollbackIncomplete,"restored attempt consumed capacity");
  Require(restored.EvidencePath=="unchanged-forensic-file","forensic identity changed");
  var uncertain=new Retained{Domains=false};retained.Add(uncertain);
  Require(!RestoreFailedAttempt(uncertain)&&retained.Contains(uncertain)&&uncertain.RollbackIncomplete,"unknown original evicted");
  var committed=new Retained{Domains=true,Detached=true};retained.Add(committed);
  Require(!RestoreFailedAttempt(committed)&&retained.Contains(committed),"completed detached custody retired by failure path");
  for(int i=0;i<32;i++){var retry=new Retained{Domains=true};retained.Add(retry);Require(RestoreFailedAttempt(retry),"restored retry failed");}
  Require(retained.Count==2,"failed retries leaked capacity or erased retained originals");
  Require(!RestoreFailedAttempt(null),"missing original accepted");
  Console.WriteLine("PASS actual failed-attempt retention with domain doubles; not whole lifecycle transfer");
 }
}
class Program {static void Main()=>Actual.Run();}

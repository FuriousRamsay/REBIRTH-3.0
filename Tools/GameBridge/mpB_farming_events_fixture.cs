// Actual farming event journal Save/Read/Archive methods. Native events, record
// codec and durable rename adapter are disclosed doubles; no native exactly-once claim.
using System;using System.IO;using System.Text;
static class GameIO {public static string Root;public static string GetSaveGameDir()=>Root;}
static class RebirthDurableFileCommit {public static bool Fail;public static bool TryPublish(string temporary,string path,out string reason){reason="";if(Fail)return false;if(File.Exists(path))File.Replace(temporary,path,path+".bak");else File.Move(temporary,path);return true;}}
static class AdvancedFarmingHarvestEvents {
 internal sealed class State {internal Guid Operation;internal string Signature;internal int Index;internal bool OwnerDone;}
 private sealed class Original {internal Guid Operation;}
 private static Original Record(State s)=>new Original{Operation=s.Operation};
 private static string Encode(State s)=>s.Operation.ToString("N")+"|"+s.Signature+"|"+s.Index+"|"+s.OwnerDone;
 private static State Decode(string image){var p=image.Split('|');if(p.Length!=4)throw new InvalidDataException();return new State{Operation=Guid.ParseExact(p[0],"N"),Signature=p[1],Index=int.Parse(p[2]),OwnerDone=bool.Parse(p[3])};}
// PRODUCTION_METHODS
 internal static void Run(){
  var s=new State{Operation=Guid.NewGuid(),Signature="original"};Check(Save(s,true),"original obligation not persisted");string before=Encode(s);s.Index=1;
  RebirthDurableFileCommit.Fail=true;Check(!Save(s,true,before),"failed durable publication accepted");Check(Read(s.Operation,true,out var old)&&old.Index==0,"failed publication lost original obligation");RebirthDurableFileCommit.Fail=false;
  Check(Save(s,true,before),"retry did not retain exact original");Check(!Save(s,true,before),"stale comparison accepted");
  string current=Encode(s);s.Index=0;Check(!Save(s,true,current),"event ordinal rewound");s.Index=1;s.OwnerDone=true;Check(Save(s,true,current)&&Archive(s,true),"completed obligation not retained");Check(Read(s.Operation,true,out var done)&&done.OwnerDone,"archived evidence lost");
  s=new State{Operation=Guid.NewGuid(),Signature="corrupt"};Check(Save(s,true),"corrupt setup");File.WriteAllText(PathFor(s.Operation,true),"invalid");Check(!Save(s,true),"corrupt original overwritten");
 }
 private static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
}
static class Fixture {static void Main(string[] args){GameIO.Root=Path.Combine(args[0],"events");AdvancedFarmingHarvestEvents.Run();Console.WriteLine("PASS actual event journal: original retention, durable failure/retry, stale CAS/rewind refusal, completion archive and corruption quarantine; codec/native callbacks doubled");}}

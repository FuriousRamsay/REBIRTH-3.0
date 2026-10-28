// AUTHORED_NOT_RUN. Both complete production journal classes are injected.
// Only unrelated history-index lookup is adapted; binary codec/archive are real.
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Collections.Generic;
// ACTUAL_JOURNALS
static class RebirthCamperTransferHistoryScope {
 internal sealed class Index {internal RebirthCamperTransferJournal.Record FindPending(Guid vehicle)=>null;internal IEnumerable<RebirthCamperTransferJournal.Record> FindApplied(Guid vehicle,string owner)=>new RebirthCamperTransferJournal.Record[0];internal IEnumerable<RebirthCamperTransferJournal.Record> FindRejected(Guid vehicle,string owner)=>new RebirthCamperTransferJournal.Record[0];}
 internal static bool TryGet(RebirthCamperTransferJournal journal,out Index index){index=null;return false;}
}
static class Program {
 static void Need(bool value,string error){if(!value)throw new Exception(error);}
 static string[] Snapshot(string root)=>Directory.Exists(root)?Directory.GetFiles(root,"*",SearchOption.AllDirectories).OrderBy(path=>path,StringComparer.Ordinal).Select(path=>path.Substring(root.Length)+":"+File.GetLastWriteTimeUtc(path).Ticks+":"+Convert.ToBase64String(File.ReadAllBytes(path))).ToArray():new string[0];
 static int writes;
 static void ObserveUnchanged(string root,Guid operation,RebirthCamperTransferJournal.Phase expected){
  var before=Snapshot(root);int count=writes;
  var observed=RebirthRemoteResourceClientOriginalJournal.ObserveExisting(root,operation);
  Need(observed!=null&&observed.Operation==operation&&observed.State==expected,"wrong canonical phase");
  Need(before.SequenceEqual(Snapshot(root))&&writes==count,"observation wrote/archived journal evidence");
 }
 static void RefuseUnchanged(string root,Guid operation){var before=Snapshot(root);bool refused=false;try{RebirthRemoteResourceClientOriginalJournal.ObserveExisting(root,operation);}catch(InvalidDataException){refused=true;}Need(refused&&before.SequenceEqual(Snapshot(root)),"conflicting history accepted or mutated");}
 public static void Main(){
  string root=Path.Combine(Path.GetTempPath(),"rebirth-readonly-journal-"+Guid.NewGuid().ToString("N"));
  var operation=Guid.NewGuid();
  try{
   Need(!Directory.Exists(root)&&RebirthCamperTransferJournal.ReadExisting(root,operation)==null&&!Directory.Exists(root),"native reader initialized missing store");
   Need(RebirthRemoteResourceClientOriginalJournal.ObserveExisting(root,operation)==null&&!Directory.Exists(root),"client observer initialized missing store");
   RebirthCamperTransferJournal.PublicationStarting+=path=>writes++;
   var journal=new RebirthRemoteResourceClientOriginalJournal(root);var before=new byte[]{1,2,3};var after=new byte[]{4,5,6};
   journal.Prepare(operation,Guid.NewGuid(),"creation",0,before,after);ObserveUnchanged(root,operation,RebirthCamperTransferJournal.Phase.Prepared);
   journal.Advance(operation,RebirthCamperTransferJournal.Phase.Prepared,RebirthCamperTransferJournal.Phase.Uncertain);ObserveUnchanged(root,operation,RebirthCamperTransferJournal.Phase.Uncertain);
   journal.Advance(operation,RebirthCamperTransferJournal.Phase.Uncertain,RebirthCamperTransferJournal.Phase.Applied);ObserveUnchanged(root,operation,RebirthCamperTransferJournal.Phase.Applied);
   string archived=Path.Combine(root,"completed",operation.ToString("N"),operation.ToString("N")+".0.rcj");
   string duplicate=Path.Combine(root,operation.ToString("N")+".0.rcj");var conflicting=File.ReadAllBytes(archived);conflicting[0]^=1;File.WriteAllBytes(duplicate,conflicting);RefuseUnchanged(root,operation);

   string partial=Path.Combine(root,"partial");var native=new RebirthCamperTransferJournal(partial);var second=Guid.NewGuid();native.Prepare(second,Guid.NewGuid(),"creation",0,before,after);
   native.Advance(second,RebirthCamperTransferJournal.Phase.Prepared,RebirthCamperTransferJournal.Phase.Uncertain);native.Advance(second,RebirthCamperTransferJournal.Phase.Uncertain,RebirthCamperTransferJournal.Phase.Applied);
   string mirror=Path.Combine(partial,"completed",second.ToString("N"));Directory.CreateDirectory(mirror);
   string prefix=Path.Combine(partial,second.ToString("N")+".0.rcj"),copied=Path.Combine(mirror,Path.GetFileName(prefix));File.Copy(prefix,copied);
   File.WriteAllText(Path.Combine(partial,"unknown.tmp"),"unresolved evidence");ObserveUnchanged(partial,second,RebirthCamperTransferJournal.Phase.Applied);
   var corrupt=File.ReadAllBytes(copied);corrupt[0]^=1;File.WriteAllBytes(copied,corrupt);RefuseUnchanged(partial,second);
   Console.WriteLine("PASS actual immutable binary journal/terminal archive observation with no write/create/archive effects; native inventory and multiplayer NOT RUN");
  }finally{if(Directory.Exists(root))Directory.Delete(root,true);}
 }
}

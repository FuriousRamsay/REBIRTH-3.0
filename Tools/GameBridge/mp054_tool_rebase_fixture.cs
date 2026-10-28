using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Security.Cryptography;
// AUTHORED_NOT_RUN. Complete actual RTP1 codec; journal/native image are
// explicit adapters. This cohort does not qualify actual owner/region saves.
// ACTUAL_TOOL_REBASE_CODEC
internal static class RebirthProductiveEarnedNativeSuccessorRecord {
 internal const int MaximumBytes=5*1024*1024;
 internal static bool IsCurrent(string path,byte[] image)=>!File.Exists(path+".tmp")&&File.ReadAllBytes(path).SequenceEqual(image);
}
internal static class RebirthProductiveEarnedArtifact {internal static bool SafePath(string path)=>Path.IsPathRooted(path);}
internal static class RebirthProductiveEarnedNativeBody {
 internal static bool ValidPendingToolReplacement(byte[] before,int beforeOffset,byte[] after,int afterOffset,int index,float timer)=>
  beforeOffset==1&&afterOffset==1&&index==2&&before.Length==after.Length&&before[0]!=after[0]&&before.Skip(1).SequenceEqual(after.Skip(1));
}
internal static partial class RebirthProductiveEarnedPendingToolGestures {
 internal sealed class ParsedGestureJournal:IDisposable {
  internal bool PendingInvocation,StationPending;internal int Returned,OwnerStep,StationStep,Index;
  internal Guid Birth,SourceOperation,Operation;internal float BeforeTimer,TargetTimer;
  internal byte[] OriginalDigest,RootDigest,HeadDigest,OwnerAfterDigest,NativeDigest,StationAfterDigest;
  public void Dispose(){}
 }
 internal static ParsedGestureJournal Journal;
 internal static bool TryInspectGestureJournal(string path,out ParsedGestureJournal journal){journal=Journal;return journal!=null;}
}
internal static class Program {
 static void Need(bool value,string message){if(!value)throw new Exception(message);}
 public static void Main(){
  string root=Path.Combine(Path.GetTempPath(),"rb-tool-rebase-case-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
  // Keep the synthetic native bodies above the production record's lower bound.
  string path=Path.Combine(root,"original.rew");byte[] original={1,2,3},before={4,5,6,7,8,9,10},after={7,5,6,7,8,9,10};
  var value=new RebirthProductiveEarnedPendingToolRebaseRecord.Description{Birth=Guid.NewGuid(),Source=Guid.NewGuid(),Gesture=Guid.NewGuid(),Index=2,
   BeforeOffset=1,AfterOffset=1,BeforeTimer=-2,TargetTimer=8,Step=1,Original=RebirthProductiveEarnedPendingToolRebaseRecord.Digest(original),
   JournalRoot=new byte[32],JournalHead=new byte[32],OwnerAfter=new byte[32],Before=before,After=after};
  var journal=new RebirthProductiveEarnedPendingToolGestures.ParsedGestureJournal{Birth=value.Birth,SourceOperation=value.Source,Operation=value.Gesture,
   Index=2,BeforeTimer=-2,TargetTimer=8,Returned=1,OwnerStep=1,StationStep=1,OriginalDigest=value.Original,RootDigest=value.JournalRoot,
   HeadDigest=value.JournalHead,OwnerAfterDigest=value.OwnerAfter,NativeDigest=RebirthProductiveEarnedPendingToolRebaseRecord.Digest(before),
   StationAfterDigest=RebirthProductiveEarnedPendingToolRebaseRecord.Digest(after)};
  RebirthProductiveEarnedPendingToolGestures.Journal=journal;
  try {
   byte[] record=RebirthProductiveEarnedPendingToolRebaseRecord.Encode(value);File.WriteAllBytes(path+".tools-rebased",record);
   Need(RebirthProductiveEarnedPendingToolRebaseRecord.TryRead(path,original,after,1,out var restored,out _)&&restored.Source==value.Source,"complete original chain refused");
   journal.PendingInvocation=true;Need(!RebirthProductiveEarnedPendingToolRebaseRecord.TryRead(path,original,after,1,out _,out _),"entered unknown gesture reauthorized");journal.PendingInvocation=false;
   journal.StationPending=true;Need(!RebirthProductiveEarnedPendingToolRebaseRecord.TryRead(path,original,after,1,out _,out _),"entered unknown sync reauthorized");journal.StationPending=false;
   journal.OwnerStep=0;Need(!RebirthProductiveEarnedPendingToolRebaseRecord.TryRead(path,original,after,1,out _,out _),"missing positive saved owner accepted");journal.OwnerStep=1;
   journal.SourceOperation=Guid.NewGuid();Need(!RebirthProductiveEarnedPendingToolRebaseRecord.TryRead(path,original,after,1,out _,out _),"foreign source debt accepted");journal.SourceOperation=value.Source;
   Need(!RebirthProductiveEarnedPendingToolRebaseRecord.TryRead(path,new byte[]{2,2,3},after,1,out _,out _),"changed original accepted");
   Need(!RebirthProductiveEarnedPendingToolRebaseRecord.TryRead(path,original,new byte[]{7,5,9},1,out _,out _),"unrelated native mutation accepted");
   File.WriteAllBytes(path+".tools-rebased.tmp",record);Need(!RebirthProductiveEarnedPendingToolRebaseRecord.TryRead(path,original,after,1,out _,out _),"staged uncertain successor accepted");File.Delete(path+".tools-rebased.tmp");
   value.TargetTimer=-4;journal.TargetTimer=-4;File.WriteAllBytes(path+".tools-rebased",RebirthProductiveEarnedPendingToolRebaseRecord.Encode(value));
   Need(RebirthProductiveEarnedPendingToolRebaseRecord.TryRead(path,original,after,1,out _,out _),"native cycled carryover direction rejected");
   Console.WriteLine("PASS actual RTP1 codec with explicit journal/native adapters; actual owner/region/load NOT_RUN");
  }finally{foreach(string file in Directory.EnumerateFiles(root))File.Delete(file);Directory.Delete(root);}
 }
}

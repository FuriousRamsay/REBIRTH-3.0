using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
// AUTHORED_NOT_RUN. Actual registration partial; canonical file decoder and
// saved record identity are explicit doubles. No native load/setter is emulated.
// ACTUAL_EARNED_REGISTRATION_SOURCE
public static class ThreadManager {public static bool Main=true;public static bool IsMainThread()=>Main;}
public static class RebirthStationCompletionOriginalScope {public static readonly StringComparison PathComparison=StringComparison.OrdinalIgnoreCase;}
internal static partial class RebirthProductiveEarnedSavedNativeSource
{
 internal sealed class Evidence
 {
  internal string Path;internal bool Active=true,Terminal,PendingNextWork,ToolsRebased;internal byte[] Image,Original;internal Request Load;
  internal sealed class FrameData {internal Guid Birth,Operation;}
  internal FrameData Frame=new FrameData();
  internal bool PrimaryCurrent()=>Active&&Matches(Path,Image);
  internal void Dispose(){Active=false;}
 }
 internal sealed class Request {internal Evidence Custody;internal string Directory;internal object Loaded;internal bool PredictedNative;}
 private static void MarkStartupRefillNeeded(){} // Refill has a separate actual-source fixture.
 private static readonly object cachedLoadGate=new object();
 private static readonly List<Request> cachedLoadRequests=new List<Request>();
 internal static bool Decode=true;internal static int Decodes;
 private static bool Matches(string path,byte[] bytes)=>bytes!=null&&File.Exists(path)&&File.ReadAllBytes(path).SequenceEqual(bytes);
 private static void ReadCandidate(string path,string save,bool starting=false,bool replacePending=false,Evidence preserve=null)
 {Decodes++;if(Decode)cachedLoadRequests.Add(new Request{Directory=save,Custody=new Evidence{Path=path,Image=File.ReadAllBytes(path),Terminal=!path.EndsWith(".earned-load",StringComparison.Ordinal)}});}
 internal static void Reset(){cachedLoadRequests.Clear();Decodes=0;Decode=true;ThreadManager.Main=true;}
 internal static int Count=>cachedLoadRequests.Count;
 internal static bool HasLoaded=>cachedLoadRequests.Any(value=>value.Loaded!=null);
 internal static bool CanLoadFirst()=>CanLoadSavedCandidate(cachedLoadRequests[0].Custody);
 internal static Evidence First=>cachedLoadRequests[0].Custody;
 internal static void MarkLoaded()=>cachedLoadRequests[0].Loaded=new object();
 internal static void Fill()
 {for(int i=0;i<4;i++)cachedLoadRequests.Add(new Request{Custody=new Evidence{Path="other"+i}});}
}
internal static class Program
{
 private static void Need(bool value,string message){if(!value)throw new Exception(message);}
 public static void Main()
 {
  string directory=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"rb-earned-register-"+Guid.NewGuid().ToString("N"));
  Directory.CreateDirectory(directory);
  string path=Path.Combine(directory,"original.rew.earned-load"),terminal=Path.Combine(directory,"original.rew.terminal-load"),successor=Path.Combine(directory,"original.rew.successor-load");
  byte[] image={3,8,13};File.WriteAllBytes(path,image);
  try
  {
   RebirthProductiveEarnedSavedNativeSource.Reset();
   Need(RebirthProductiveEarnedSavedNativeSource.RegisterPublishedEarned(path,directory,image),"published source not registered");
   Need(RebirthProductiveEarnedSavedNativeSource.Count==1&&!RebirthProductiveEarnedSavedNativeSource.HasLoaded,"registration fabricated native load");
   Need(RebirthProductiveEarnedSavedNativeSource.CanLoadFirst(),"actual earned candidate refused load admission");
   RebirthProductiveEarnedSavedNativeSource.MarkLoaded();
   Need(RebirthProductiveEarnedSavedNativeSource.RegisterPublishedEarned(path,directory,image)&&RebirthProductiveEarnedSavedNativeSource.Decodes==1,"retry duplicated/lost actual load");
   Need(!RebirthProductiveEarnedSavedNativeSource.RegisterPublishedEarned(path,Path.Combine(directory,"different"),image),"different save adopted custody");
   Need(!RebirthProductiveEarnedSavedNativeSource.RegisterPublishedEarned(path,directory,new byte[]{0}),"changed saved image accepted");
   ThreadManager.Main=false;Need(!RebirthProductiveEarnedSavedNativeSource.RegisterPublishedEarned(path,directory,image),"background registration accepted");ThreadManager.Main=true;
   File.WriteAllBytes(terminal+".tmp",image);
   Need(!RebirthProductiveEarnedSavedNativeSource.CanLoadFirst(),"retained earned request survived uncertain terminal");
   Need(!RebirthProductiveEarnedSavedNativeSource.RegisterPublishedEarned(path,directory,image),"uncertain terminal replaced");File.Delete(terminal+".tmp");
   File.WriteAllBytes(terminal,image);
   Need(!RebirthProductiveEarnedSavedNativeSource.CanLoadFirst(),"retained earned request survived saved terminal");
   Need(!RebirthProductiveEarnedSavedNativeSource.RegisterPublishedEarned(path,directory,image),"saved terminal replaced");
   var preserved=RebirthProductiveEarnedSavedNativeSource.First;
   Need(RebirthProductiveEarnedSavedNativeSource.RegisterPublishedTerminal(terminal,directory,image,preserved)&&
    RebirthProductiveEarnedSavedNativeSource.Count==1&&preserved.Active,"terminal replaced lookup by revoking active artifact source");
   Need(RebirthProductiveEarnedSavedNativeSource.RegisterPublishedTerminal(terminal,directory,image,preserved),"terminal publication retry not idempotent");
   var priorTerminal=RebirthProductiveEarnedSavedNativeSource.First;
   File.WriteAllBytes(successor+".tmp",image);
   Need(!RebirthProductiveEarnedSavedNativeSource.CanLoadFirst(),"terminal load survived uncertain saved successor");File.Delete(successor+".tmp");
   File.WriteAllBytes(successor,image);
   Need(!RebirthProductiveEarnedSavedNativeSource.CanLoadFirst(),"terminal load survived saved successor");
   Need(RebirthProductiveEarnedSavedNativeSource.RegisterPublishedTerminal(successor,directory,image,preserved)&&
    RebirthProductiveEarnedSavedNativeSource.Count==1&&!priorTerminal.Active&&preserved.Active&&
    RebirthProductiveEarnedSavedNativeSource.CanLoadFirst(),"partial successor lookup handoff failed");
   RebirthProductiveEarnedSavedNativeSource.ReleaseRegisteredTerminal(Guid.Empty,Guid.Empty);
   Need(RebirthProductiveEarnedSavedNativeSource.Count==0,"positive terminal retained lookup root");
   File.Delete(successor);File.Delete(terminal);
   RebirthProductiveEarnedSavedNativeSource.Reset();RebirthProductiveEarnedSavedNativeSource.Fill();
   Need(!RebirthProductiveEarnedSavedNativeSource.RegisterPublishedEarned(path,directory,image)&&RebirthProductiveEarnedSavedNativeSource.Decodes==0,"root pressure bypassed bound");
   RebirthProductiveEarnedSavedNativeSource.Reset();RebirthProductiveEarnedSavedNativeSource.Decode=false;
   Need(!RebirthProductiveEarnedSavedNativeSource.RegisterPublishedEarned(path,directory,image)&&RebirthProductiveEarnedSavedNativeSource.Count==0,"invalid canonical candidate accepted");
   Console.WriteLine("PASS earned registration: authored source fixture");
  }
  finally{File.Delete(path);Directory.Delete(directory);}
 }
}

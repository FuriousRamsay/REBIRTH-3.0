using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
// AUTHORED_NOT_RUN. Entire actual streaming startup partial is injected.
// Canonical candidate/initial-original admission are explicit adapters. No native Loaded is issued here.
// ACTUAL_STARTUP_CANDIDATES_SOURCE
internal static class RebirthProductiveEarnedArtifact
{internal static string Root;internal static string WorldIdentity(World world)=>world.Key;internal static bool SafePath(string path)=>Path.GetFullPath(path).StartsWith(Root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase);}
internal sealed class World {internal string Key="world";internal bool IsRemote()=>false;}
internal sealed class GameManager {internal static GameManager Instance=new GameManager();internal World World=new World();}
internal static class GameIO {internal static string Save;internal static string GetSaveGameDir()=>Save;}
internal static class ThreadManager {internal static bool Main=true;internal static bool IsMainThread()=>Main;}
internal static class RebirthProductiveEarnedObservationBudget
{
 internal static bool Available=true;internal static IDisposable TryEnterDecoder()=>Available?new Lease():null;
 private sealed class Lease:IDisposable {public void Dispose(){}}
}
internal static class RebirthStationCompletionOriginalScope {internal static readonly StringComparison PathComparison=StringComparison.OrdinalIgnoreCase;}
internal static partial class RebirthProductiveEarnedSavedNativeSource
{
 private const int MaximumRecord=1024;
 private static readonly object cachedLoadGate=new object();
 private sealed class Evidence {internal string Path;}
 private sealed class Request {internal string StartupOrigin;internal Evidence Custody;internal object Loaded;}
 private static readonly List<Request> cachedLoadRequests=new List<Request>();
 internal static readonly List<string> CanonicalReads=new List<string>(),OriginalReads=new List<string>();
 internal static readonly HashSet<string> Accepted=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
 private static void ReadCandidate(string path,string save,bool starting)
 {if(!starting)throw new Exception("startup adapter scope");CanonicalReads.Add(path);if(Accepted.Contains(path))cachedLoadRequests.Add(new Request{StartupOrigin=path,Custody=new Evidence{Path=path}});}
 private static bool RegisterPreparedOriginal(string path,string save)
 {OriginalReads.Add(path);if(!Accepted.Contains(path))return false;cachedLoadRequests.Add(new Request{StartupOrigin=path,Custody=new Evidence{Path=path}});return true;}
 internal static void Reset(){cachedLoadRequests.Clear();CanonicalReads.Clear();OriginalReads.Clear();Accepted.Clear();}
 internal static string[] Scan(string save){ScanStartupCandidates(save,Path.Combine(save,"RebirthProductiveEarned"));return Selected;}
 internal static string[] Selected=>cachedLoadRequests.Select(value=>value.Custody.Path).ToArray();
 internal static bool HasLoaded=>cachedLoadRequests.Any(value=>value.Loaded!=null);
 internal static void Configure(string save)=>ConfigureStartupRefill(save);
 internal static void ReleaseOne(string path){cachedLoadRequests.RemoveAll(value=>value.Custody.Path==path);MarkStartupRefillNeeded();}
 internal static void AddAlias(string path,string child){cachedLoadRequests.Add(new Request{StartupOrigin=path,Custody=new Evidence{Path=child}});}
}
internal static class Program
{
 private static void Need(bool value,string message){if(!value)throw new Exception(message);}
 private static string Case(string root,string name)
 {RebirthProductiveEarnedSavedNativeSource.Reset();string save=Path.Combine(root,name);Directory.CreateDirectory(Path.Combine(save,"RebirthProductiveEarned"));GameIO.Save=save;GameManager.Instance.World=new World();RebirthProductiveEarnedSavedNativeSource.Configure(save);return save;}
 private static string Original(string save)
 {string folder=Path.Combine(save,"RebirthProductiveEarned",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);return Path.Combine(folder,Guid.NewGuid().ToString("N")+".rew");}
 private static void Record(string path,bool terminal,bool pending,bool accepted=true)
 {
  using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream))
  {writer.Write(0x324C4552u);writer.Write(terminal);writer.Write(pending);writer.Write(new byte[122]);File.WriteAllBytes(path,stream.ToArray());}
  if(accepted)RebirthProductiveEarnedSavedNativeSource.Accepted.Add(path);
 }
 public static void Main()
 {
  string root=Path.Combine(Path.GetTempPath(),"rb-startup-candidates-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);RebirthProductiveEarnedArtifact.Root=root;
  try
  {
   string save=Case(root,"history"),original;
   for(int i=0;i<140;i++){original=Original(save);Record(original+".earned-load",false,false);Record(original+".terminal-load",true,false);}
   original=Original(save);Record(original+".earned-load",false,false);string pending=original+".terminal-load";Record(pending,true,true);
   var selected=RebirthProductiveEarnedSavedNativeSource.Scan(save);
   Need(selected.Length==4&&selected[0]==pending&&RebirthProductiveEarnedSavedNativeSource.CanonicalReads.Count==4,
    "completed history or old first-128 cutoff hid pending original");
   Need(Directory.EnumerateFiles(Path.Combine(save,"RebirthProductiveEarned"),"*",SearchOption.AllDirectories).Count()==282,"startup changed retained history");
   save=Case(root,"earned-before-complete");original=Original(save);Record(original+".earned-load",false,false);Record(original+".terminal-load",true,false);
   original=Original(save);string earned=original+".earned-load";Record(earned,false,false);
   original=Original(save);File.WriteAllBytes(original+".native-location",new byte[]{1});RebirthProductiveEarnedSavedNativeSource.Accepted.Add(original);string initial=original;
   selected=RebirthProductiveEarnedSavedNativeSource.Scan(save);
   Need(selected.Length==3&&Array.IndexOf(selected,earned)<2&&Array.IndexOf(selected,initial)<2,"completed history preceded earned/unpublished originals");
   save=Case(root,"uncertain-newer");original=Original(save);Record(original+".earned-load",false,false);File.WriteAllBytes(original+".terminal-load.tmp",new byte[]{1});File.WriteAllBytes(original+".native-location",new byte[]{1});RebirthProductiveEarnedSavedNativeSource.Accepted.Add(original);
   selected=RebirthProductiveEarnedSavedNativeSource.Scan(save);Need(selected.Length==0&&RebirthProductiveEarnedSavedNativeSource.CanonicalReads.Count==0&&RebirthProductiveEarnedSavedNativeSource.OriginalReads.Count==0,"unknown terminal fell back to earned/initial source");
   save=Case(root,"invalid-header");original=Original(save);Record(original+".earned-load",false,false);byte[] bad=File.ReadAllBytes(original+".earned-load");bad[4]=2;File.WriteAllBytes(original+".earned-load",bad);
   selected=RebirthProductiveEarnedSavedNativeSource.Scan(save);Need(selected.Length==0&&RebirthProductiveEarnedSavedNativeSource.CanonicalReads.Count==0,"invalid scheduling header admitted candidate");
   save=Case(root,"canonical-refusal");original=Original(save);Record(original+".earned-load",false,false);string rejected=original+".terminal-load";Record(rejected,true,true,false);
   original=Original(save);Record(original+".earned-load",false,false);pending=original+".terminal-load";Record(pending,true,true);
   selected=RebirthProductiveEarnedSavedNativeSource.Scan(save);Need(selected.Length==1&&selected[0]==pending&&RebirthProductiveEarnedSavedNativeSource.CanonicalReads.Contains(rejected),"header replaced full canonical admission or refusal blocked later candidate");
   save=Case(root,"advanced-parent");original=Original(save);Record(original+".earned-load",false,false);Record(original+".terminal-load",true,true,false);
   original=Original(save);Record(original+".earned-load",false,false);string child=original+".terminal-load";Record(child,true,false);
   selected=RebirthProductiveEarnedSavedNativeSource.Scan(save);Need(selected.Length==1&&selected[0]==child,"refused historical parent hid complete child source");
   save=Case(root,"capacity");for(int i=0;i<5;i++){original=Original(save);Record(original+".earned-load",false,false);Record(original+".terminal-load",true,true);}
   selected=RebirthProductiveEarnedSavedNativeSource.Scan(save);Need(selected.Length==4&&RebirthProductiveEarnedSavedNativeSource.CanonicalReads.Count==4,"live source capacity changed");
   string released=selected[0];Record(released.Substring(0,released.Length-".terminal-load".Length)+".successor-load",true,false);
   RebirthProductiveEarnedSavedNativeSource.ReleaseOne(released);int before=RebirthProductiveEarnedSavedNativeSource.CanonicalReads.Count;
   RebirthProductiveEarnedObservationBudget.Available=false;RebirthProductiveEarnedSavedNativeSource.PumpStartupRefill();
   Need(RebirthProductiveEarnedSavedNativeSource.Selected.Length==3&&RebirthProductiveEarnedSavedNativeSource.CanonicalReads.Count==before,"active observation phase triggered filesystem/admission scan");
   RebirthProductiveEarnedObservationBudget.Available=true;RebirthProductiveEarnedSavedNativeSource.PumpStartupRefill();
   Need(RebirthProductiveEarnedSavedNativeSource.Selected.Length==4&&RebirthProductiveEarnedSavedNativeSource.CanonicalReads.Count==before+1&&!RebirthProductiveEarnedSavedNativeSource.HasLoaded,"released slot not refilled, existing custody duplicated, or native Loaded invented");
   save=Case(root,"history-refill");original=Original(save);Record(original+".earned-load",false,false);string completed=original+".terminal-load";Record(completed,true,false);
   selected=RebirthProductiveEarnedSavedNativeSource.Scan(save);RebirthProductiveEarnedSavedNativeSource.ReleaseOne(completed);before=RebirthProductiveEarnedSavedNativeSource.CanonicalReads.Count;RebirthProductiveEarnedSavedNativeSource.PumpStartupRefill();
   Need(RebirthProductiveEarnedSavedNativeSource.Selected.Length==0&&RebirthProductiveEarnedSavedNativeSource.CanonicalReads.Count==before,"completed history cycled back into released slot");
   save=Case(root,"origin-alias");original=Original(save);Record(original+".earned-load",false,false);pending=original+".terminal-load";Record(pending,true,true);RebirthProductiveEarnedSavedNativeSource.AddAlias(pending,"derived-child.rew");
   RebirthProductiveEarnedSavedNativeSource.Scan(save);Need(RebirthProductiveEarnedSavedNativeSource.CanonicalReads.Count==0,"derived child publication root decoded twice");
   save=Case(root,"original-identity");original=Original(save);Record(original+".earned-load",false,false);RebirthProductiveEarnedSavedNativeSource.AddAlias("parent.terminal-load",original);
   RebirthProductiveEarnedSavedNativeSource.Scan(save);Need(RebirthProductiveEarnedSavedNativeSource.CanonicalReads.Count==0,"same original's canonical publication replaced registered derived custody");
   save=Case(root,"scope");original=Original(save);Record(original+".earned-load",false,false);earned=original+".earned-load";
   RebirthProductiveEarnedSavedNativeSource.ReleaseOne("none");string saved=GameIO.Save;GameIO.Save=Path.Combine(root,"other");RebirthProductiveEarnedSavedNativeSource.PumpStartupRefill();Need(RebirthProductiveEarnedSavedNativeSource.Selected.Length==0,"changed save admitted startup custody");
   GameIO.Save=saved;RebirthProductiveEarnedSavedNativeSource.PumpStartupRefill();Need(RebirthProductiveEarnedSavedNativeSource.Selected.Length==1,"same-scope deferred refill failed");
   RebirthProductiveEarnedSavedNativeSource.ReleaseOne(earned);GameManager.Instance.World=new World();RebirthProductiveEarnedSavedNativeSource.PumpStartupRefill();Need(RebirthProductiveEarnedSavedNativeSource.Selected.Length==0,"replacement World adopted refill scope");
   Console.WriteLine("PASS extracted streaming startup ordering; disclosed canonical admission adapters; no native load proof");
  }
  finally
  {
   foreach(string file in Directory.EnumerateFiles(root,"*",SearchOption.AllDirectories))File.Delete(file);
   foreach(string directory in Directory.EnumerateDirectories(root,"*",SearchOption.AllDirectories).OrderByDescending(value=>value.Length))Directory.Delete(directory);
   Directory.Delete(root);
  }
 }
}

using System;
using System.IO;
using System.Collections.Generic;
// AUTHORED_NOT_RUN. Entire actual saved-pending tools observation is injected.
// Terminal/artifact/native/decoder objects are explicit adapters; no native action/save receipt is produced.
// ACTUAL_PENDING_TOOLS_OBSERVATION_SOURCE
internal sealed class World {internal bool Remote;internal bool IsRemote()=>Remote;}
internal sealed class GameManager {internal static GameManager Instance=new GameManager();internal World World=new World();}
internal static class ThreadManager {internal static bool Main=true;internal static bool IsMainThread()=>Main;}
internal sealed class RecipeQueueItem {}
internal class TileEntityWorkstation {internal RecipeQueueItem[] Queue=new[]{new RecipeQueueItem()};}
internal sealed class RebirthCamperStationState:TileEntityWorkstation {}
internal static class RebirthProductiveEarnedObservationBudget
{
 internal sealed class Decoder {internal bool IsCurrent=true;}
 internal static bool Available=true;internal static int Live;
 internal static bool TryRetainDetached(long bytes,out IDisposable retained){retained=Available?new Lease():null;return retained!=null;}
 private sealed class Lease:IDisposable {internal Lease(){Live++;}public void Dispose(){Live--;}}
}
internal static class RebirthProductiveEarnedArtifact
{
 internal sealed class Prepared {internal Guid Birth=Guid.NewGuid(),Operation=Guid.NewGuid();internal bool Valid=true;internal int ImageLength=128;internal string OriginalPath;internal bool Current()=>Valid;internal byte[] OriginalDigest()=>new byte[32];internal byte[] CopyImage()=>new byte[ImageLength];}
 internal static bool SameTools(int[] tools,byte[] image)=>image?.Length==1&&tools[0]==image[0];
}
internal static class RebirthProductiveEarnedNativeBody
{
 internal sealed class Image {internal int[] Tools;}
 internal static bool TryRead(byte[] bytes,int offset,out Image image,out ulong elapsed){elapsed=0;image=bytes?.Length==1?new Image{Tools=new[]{(int)bytes[0]}}:null;return image!=null;}
}
internal static partial class RebirthProductiveEarnedCoordinator
{
 // This cohort exercises static workstations. Mobile custody is explicitly
 // unavailable; these adapters cannot issue a mobile observation or authority.
 internal sealed partial class SavedPendingToolsObservation
 {
  private NativeTerminal mobileTerminal;
  private bool MobileRetainedContext()=>false;
  private bool MobileMatchesBeforeTools(byte[] tools,RebirthProductiveEarnedObservationBudget.Decoder decoder)=>false;
 }
 private static bool TryObserveMobilePendingTools(TileEntityWorkstation station,RebirthProductiveEarnedObservationBudget.Decoder decoder,out SavedPendingToolsObservation observation){observation=null;return false;}
 private static bool Enabled=true;internal static bool DefinitionMatches=true;
 private static bool MatchesPendingDefinition(Attempt original,RecipeQueueItem row)=>DefinitionMatches;
 internal sealed class NativeTerminal
 {internal bool PendingNextWork=true,SavedPending=true,Valid=true;internal object SavedOrigin;internal byte[] Body=new byte[]{7};internal int ElapsedOffset;internal bool Current()=>Valid;}
 internal sealed class Attempt
 {
  internal TileEntityWorkstation Station=new TileEntityWorkstation();internal RebirthProductiveEarnedArtifact.Prepared Artifact=new RebirthProductiveEarnedArtifact.Prepared();
  internal NativeTerminal Terminal=new NativeTerminal();internal bool OutputTimerObserved=true,PendingTimerObserved=true,ClearedNativeMarker,PendingTimerAmbiguous;
  internal NativeTerminal MobileTerminal;
  internal object PartialSuccessor,EarnedChild;internal int PendingTimerIndex;internal float PendingTimerBefore=-12,PendingTimerTarget=-7;
  internal string PendingRecipeBinding="binding",PendingExtras="extras";
 }
 private static readonly Dictionary<TileEntityWorkstation,Attempt> retained=new Dictionary<TileEntityWorkstation,Attempt>();
 internal static Attempt Setup(string path)
 {retained.Clear();Enabled=ThreadManager.Main=DefinitionMatches=RebirthProductiveEarnedObservationBudget.Available=true;GameManager.Instance.World=new World();var a=new Attempt();a.Artifact.OriginalPath=path;retained.Add(a.Station,a);return a;}
 internal static bool Observe(Attempt a,RebirthProductiveEarnedObservationBudget.Decoder decoder,out SavedPendingToolsObservation source)=>TryObserveSavedPendingTools(a.Station,decoder,out source);
 internal static void Forget(Attempt a)=>retained.Remove(a.Station);
}
internal static class Program
{
 private static void Need(bool value,string message){if(!value)throw new Exception(message);}
 public static void Main()
 {
  string root=Path.Combine(Path.GetTempPath(),"rb-pending-tools-observation-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);string path=Path.Combine(root,"original.rew");
  var decoder=new RebirthProductiveEarnedObservationBudget.Decoder();
  try
  {
   var a=RebirthProductiveEarnedCoordinator.Setup(path);
   Need(RebirthProductiveEarnedCoordinator.Observe(a,decoder,out var source)&&source.CurrentContext()&&source.Birth==a.Artifact.Birth&&source.Operation==a.Artifact.Operation&&source.BeforeTimer==-12&&source.TargetTimer==-7,"qualified saved pending source not observed exactly");
   Need(source.MatchesBeforeTools(new byte[]{7},decoder)&&!source.MatchesBeforeTools(new byte[]{8},decoder),"observation substituted current tools or accepted different source");
   var copy=source.CopyNativeBefore();copy[0]=9;Need(source.MatchesBeforeTools(new byte[]{7},decoder),"caller changed private captured native image");
   a.PendingTimerTarget=-6;Need(!source.CurrentContext(),"changed pending debt adopted old observation");a.PendingTimerTarget=-7;
   source.Dispose();source.Dispose();Need(!source.CurrentContext()&&source.CopyNativeBefore()==null&&source.CopyOriginalDigest()==null&&RebirthProductiveEarnedObservationBudget.Live==0,"disposed observation retained charged arrays or issued copies");
   for(int variant=0;variant<8;variant++)
   {
    a=RebirthProductiveEarnedCoordinator.Setup(path);
    if(variant==0)a.Terminal.SavedPending=false;
    if(variant==1)a.ClearedNativeMarker=true;
    if(variant==2)a.PartialSuccessor=new object();
    if(variant==3)a.EarnedChild=new object();
    if(variant==4)a.Terminal.Valid=false;
    if(variant==5)RebirthProductiveEarnedCoordinator.DefinitionMatches=false;
    if(variant==6)RebirthProductiveEarnedObservationBudget.Available=false;
    if(variant==7)GameManager.Instance.World.Remote=true;
    Need(!RebirthProductiveEarnedCoordinator.Observe(a,decoder,out _)&&RebirthProductiveEarnedObservationBudget.Live==0,"unqualified pending observation admitted: "+variant);
   }
   foreach(string suffix in new[]{".partial-successor",".partial-successor.invoking.tmp",".earned-child",".earned-child.invoking"})
   {a=RebirthProductiveEarnedCoordinator.Setup(path);File.WriteAllBytes(path+suffix,new byte[]{1});Need(!RebirthProductiveEarnedCoordinator.Observe(a,decoder,out _),"staged successor custody bypassed: "+suffix);File.Delete(path+suffix);}
   a=RebirthProductiveEarnedCoordinator.Setup(path);a.Terminal.SavedPending=false;a.Terminal.SavedOrigin=new object();Need(RebirthProductiveEarnedCoordinator.Observe(a,decoder,out source),"actual loaded saved-pending source unsupported");
   RebirthProductiveEarnedCoordinator.Forget(a);Need(!source.CurrentContext(),"removed source still qualified");source.Dispose();
   a=RebirthProductiveEarnedCoordinator.Setup(path);Need(RebirthProductiveEarnedCoordinator.Observe(a,decoder,out source),"World scope setup");GameManager.Instance.World=new World();Need(!source.CurrentContext(),"replacement World adopted pending observation");source.Dispose();
   a=RebirthProductiveEarnedCoordinator.Setup(path);decoder.IsCurrent=false;Need(!RebirthProductiveEarnedCoordinator.Observe(a,decoder,out _),"unadmitted decoder issued source");
   Need(RebirthProductiveEarnedObservationBudget.Live==0,"observation failure leaked retained account");
   Console.WriteLine("PASS extracted saved-pending tools observation/context lifetime; disclosed native/artifact/budget adapters, no owner/rebase authority");
  }
  finally{foreach(string file in Directory.EnumerateFiles(root))File.Delete(file);Directory.Delete(root);}
 }
}

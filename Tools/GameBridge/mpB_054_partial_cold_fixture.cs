using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
// AUTHORED_NOT_RUN. Complete production invoked-child candidate partial.
// Parent REL2 admission and native frame/codec/budget are explicit doubles.
// Actual native loading and saved item mutation are not simulated here.
// ACTUAL_CHILD_COLD_SOURCE
internal sealed class Lease:IDisposable {public void Dispose(){}}
internal static class RebirthProductiveEarnedObservationBudget
{
 internal static bool Resize=true;internal static IDisposable TryEnterDecoder()=>new Lease();internal static IDisposable TryEnterCandidateDecoder()=>new Lease();
 internal static bool TryRetainDetached(long count,out IDisposable retained){retained=new Lease();return true;}
 internal static bool TryResizeDetached(IDisposable retained,long count)=>Resize;
}
internal sealed class RecipeQueueItem {internal float CraftingTimeLeft,OneItemCraftTime=5;}
internal static class RebirthProductiveEarnedRowProof
{
 internal const int MaximumArtifactBytes=6*1024*1024;
 internal const int MaximumFrameBytes=262144;
 internal sealed class Frame
 {
  internal string FullRecipeBinding,ExtrasDefinition;
  internal bool HasNativeBodies=true,HasRecipeDefinition=true;internal Guid Birth,Operation;internal string World="world";
  internal int RowIndex;internal float WorkedSeconds=5;internal byte[] BeforeNative,AfterNative;internal int BeforeElapsedOffset,AfterElapsedOffset;
 }
 internal static Frame Decoded;
 internal static Frame Decode(byte[] image)=>Decoded;
 internal static bool TryDecodeNative(Frame frame,out RecipeQueueItem before,out RecipeQueueItem after,out object beforeTools,out object afterTools)
 {before=new RecipeQueueItem{CraftingTimeLeft=-12};after=new RecipeQueueItem{CraftingTimeLeft=-7};beforeTools=afterTools=null;return true;}
 internal static byte[] Marker(Frame frame,byte[] image){using(var sha=SHA256.Create())return sha.ComputeHash(image);}
}
internal static class RebirthProductiveEarnedNativeBody
{
 internal const int MaximumBodyBytes=2097088;internal static bool ValidDelta=true;
 internal static bool ValidPartialSuccessor(byte[] before,int bo,byte[] after,int ao,int index,float beforeTimer,float target,float work)
 =>ValidDelta&&index==0&&beforeTimer==-2&&target==3&&work==2;
 internal static byte[] ComparisonDigest(byte[] bytes,int offset){using(var sha=SHA256.Create())return sha.ComputeHash(bytes);}
 internal static bool SameExceptElapsed(byte[] a,int ao,byte[] b,int bo)=>a.SequenceEqual(b);
 internal static bool TryMeasure(byte[] bytes,int offset,out long graph,out long scratch){graph=256;scratch=64;return true;}
}
internal static class RebirthProductiveEarnedCoordinator
{
 internal static bool TryEarnedNativeChildWork(float before,float target,float unit,out float work,out float normalized)
 {work=unit;normalized=target;return before==-12&&target==-7&&unit==5;}
}
internal static class RebirthProductiveEarnedArtifact
{
 internal static string Root;
 internal static bool SafePath(string path)=>Path.GetFullPath(path).StartsWith(Root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase);
}
internal static class RebirthProductiveEarnedNativeSuccessorRecord {internal const int MaximumBytes=5*1024*1024;}
internal static class RebirthSurvivorNetworkCodec
{internal static string ReadBoundedString(BinaryReader reader,int maximum){var value=reader.ReadString();if(Encoding.UTF8.GetByteCount(value)>maximum)throw new Exception("string bound");return value;}}
internal static partial class RebirthProductiveEarnedSavedNativeSource
{
 private const int MaximumRecord=12*1024*1024;
 internal sealed class Evidence:IDisposable
 {
  internal bool ToolsRebased;
  internal byte[] StagedChildLink;
  internal byte[] StagedPartialRecord;
  internal string PendingRecipeBinding,PendingExtras;
  internal Evidence(IDisposable retained){}
  internal string Path,Directory,WorldGuid,MarkerKey;internal bool Terminal,PendingNextWork,PendingTimerAmbiguous,Active=true;
  internal byte[] Image,Original,Native,Marker;internal int NativeOffset,PendingTimerIndex;internal float PendingTimerBefore,PendingTimerTarget;
  internal RebirthProductiveEarnedRowProof.Frame Frame;internal NativeChildDependency[] Dependencies;internal CachedLoadRequest Load;
  internal bool PrimaryCurrent()=>Active&&File.Exists(Path)&&File.ReadAllBytes(Path).SequenceEqual(Image)&&NativeChildDependenciesCurrent(Dependencies);
  public void Dispose(){Active=false;}
 }
 internal sealed class CachedLoadRequest
 {
  internal Evidence Custody;internal string Directory;internal int X,Z;internal byte[] Snapshot,Tile;internal bool PredictedNative;
  internal long MaximumGraph,MaximumScratch;internal object Loaded;
 }
 private static byte[] Read(string path,int maximum)
 {if(!RebirthProductiveEarnedArtifact.SafePath(path))throw new Exception("scope");var bytes=File.ReadAllBytes(path);if(bytes.Length<1||bytes.Length>maximum)throw new Exception("bound");return bytes;}
 internal static bool ReadChild(Evidence parent,out Evidence child)=>TryReadInvokedNativeChild(parent,out child);
 private static byte[] ReadBytes(BinaryReader reader,int maximum)
 {int length=reader.ReadInt32();if(length<1||length>maximum)throw new Exception("native bound");var bytes=reader.ReadBytes(length);if(bytes.Length!=length)throw new Exception("short");return bytes;}
 internal static bool ReadPartial(Evidence parent,out Evidence partial)=>TryReadInvokedNativePartial(parent,out partial);
}
internal static class Program
{
 private static void Need(bool value,string label){if(!value)throw new Exception(label);}
 public static void Main()
 {
  string root=Path.Combine(Path.GetTempPath(),"rb-partial-cold-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);RebirthProductiveEarnedArtifact.Root=root;
  try
  {
   Guid birth=Guid.NewGuid(),op=Guid.NewGuid();string original=Path.Combine(root,op.ToString("N")+".rew"),path=original+".terminal-load",recordPath=original+".partial-successor";
   byte[] image=Enumerable.Repeat((byte)1,200).ToArray(),before=Enumerable.Repeat((byte)2,64).ToArray(),after=Enumerable.Repeat((byte)3,64).ToArray();File.WriteAllBytes(path,image);
   var parent=new RebirthProductiveEarnedSavedNativeSource.Evidence(new Lease()){Path=path,Directory=root,Image=image,Original=new byte[100],Marker=new byte[]{9},Native=before,
    Terminal=true,PendingNextWork=true,PendingTimerBefore=-2,PendingTimerTarget=3,WorldGuid="world-guid",MarkerKey="marker",
    Frame=new RebirthProductiveEarnedRowProof.Frame{Birth=birth,Operation=op}};
   parent.Load=new RebirthProductiveEarnedSavedNativeSource.CachedLoadRequest{X=1,Z=2};
   byte[] record;
   using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream))
   {
    writer.Write(0x31535052u);writer.Write(birth.ToByteArray());writer.Write(op.ToByteArray());writer.Write(0);writer.Write(2f);writer.Write(-2f);writer.Write(3f);
    writer.Write("full-binding");writer.Write("extras");writer.Write(0);writer.Write(before.Length);writer.Write(before);writer.Write(0);writer.Write(after.Length);writer.Write(after);writer.Flush();record=stream.ToArray();
   }
   File.WriteAllBytes(recordPath,record);
   Need(RebirthProductiveEarnedSavedNativeSource.TryReadStagedNativePartial(parent,out var staged)&&staged.StagedPartialRecord.SequenceEqual(record)&&
    staged.PendingRecipeBinding=="full-binding"&&staged.PendingExtras=="extras"&&staged.Dependencies.Length==2&&staged.Load.Loaded==null,
    "unentered exact partial plan did not retain staged custody");
   Need(!RebirthProductiveEarnedSavedNativeSource.ReadPartial(parent,out _),"unentered plan impersonated invoked native After");
   Need(RebirthProductiveEarnedSavedNativeSource.TryReadStagedNativePartial(parent,()=>new Lease(),out _),"own predecessor callback could not admit bounded staged parser");
   Need(!RebirthProductiveEarnedSavedNativeSource.TryReadStagedNativePartial(parent,()=>null,out _),"missing callback phase admitted staged parser");
   File.WriteAllBytes(recordPath+".invoking.tmp",record);
   Need(!RebirthProductiveEarnedSavedNativeSource.TryReadStagedNativePartial(parent,out _),"uncertain invocation treated as unentered");File.Delete(recordPath+".invoking.tmp");
   parent.PendingRecipeBinding="changed";parent.PendingExtras="extras";
   Need(!RebirthProductiveEarnedSavedNativeSource.TryReadStagedNativePartial(parent,out _),"staged plan changed saved definition");parent.PendingRecipeBinding=parent.PendingExtras=null;
   File.WriteAllBytes(recordPath+".invoking",record);
   Need(!RebirthProductiveEarnedSavedNativeSource.TryReadStagedNativePartial(parent,out _),"entered plan obtained another application permit");
   Need(RebirthProductiveEarnedSavedNativeSource.ReadPartial(parent,out var partial)&&partial.Terminal&&!partial.PendingNextWork&&partial.Load.PredictedNative&&partial.Load.Loaded==null,"partial record minted loaded/earned proof or failed candidate admission");
   Need(partial.Native.SequenceEqual(after)&&partial.Marker.SequenceEqual(parent.Marker)&&partial.Load.Tile.Length==74&&partial.Load.X==1&&partial.Load.Z==2,"partial native After/source mismatch");
   File.WriteAllBytes(recordPath+".invoking",new byte[]{0});Need(!partial.PrimaryCurrent()&&!RebirthProductiveEarnedSavedNativeSource.ReadPartial(parent,out _),"changed invoking record accepted");File.WriteAllBytes(recordPath+".invoking",record);
   File.WriteAllBytes(recordPath+".invoking.tmp",record);Need(!RebirthProductiveEarnedSavedNativeSource.ReadPartial(parent,out _),"unknown invoking candidate accepted");File.Delete(recordPath+".invoking.tmp");
   RebirthProductiveEarnedNativeBody.ValidDelta=false;Need(!RebirthProductiveEarnedSavedNativeSource.ReadPartial(parent,out _),"invalid full native delta accepted");RebirthProductiveEarnedNativeBody.ValidDelta=true;
   parent.PendingTimerTarget=4;Need(!RebirthProductiveEarnedSavedNativeSource.ReadPartial(parent,out _),"different pending native target accepted");parent.PendingTimerTarget=3;
   RebirthProductiveEarnedObservationBudget.Resize=false;Need(!RebirthProductiveEarnedSavedNativeSource.ReadPartial(parent,out _),"native graph reservation bypassed");RebirthProductiveEarnedObservationBudget.Resize=true;
   File.WriteAllBytes(original+".earned-child",new byte[]{1});Need(!RebirthProductiveEarnedSavedNativeSource.ReadPartial(parent,out _),"conflicting child/partial records guessed");File.Delete(original+".earned-child");
   File.WriteAllBytes(path,new byte[]{0});Need(!partial.PrimaryCurrent(),"changed saved predecessor dependency remained current");
   Console.WriteLine("PASS extracted partial cold candidate/dependencies with disclosed native/parser/budget doubles");
  }
  finally{foreach(string file in Directory.EnumerateFiles(root))File.Delete(file);Directory.Delete(root);}
 }
}

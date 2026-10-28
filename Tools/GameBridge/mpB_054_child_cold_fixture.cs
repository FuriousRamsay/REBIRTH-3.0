using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
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
internal static partial class RebirthProductiveEarnedSavedNativeSource
{
 private const int MaximumRecord=12*1024*1024;
 internal sealed class Evidence:IDisposable
 {
  internal byte[] StagedChildLink;
  internal bool ToolsRebased;
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
}
internal static class Program
{
 private static void Need(bool value,string label){if(!value)throw new Exception(label);}
 public static void Main()
 {
  string root=Path.Combine(Path.GetTempPath(),"rb-child-cold-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);RebirthProductiveEarnedArtifact.Root=root;
  try
  {
   Guid parentBirth=Guid.NewGuid(),parentOp=Guid.NewGuid(),birth=Guid.NewGuid(),op=Guid.NewGuid();
   string folder=Path.Combine(root,"RebirthProductiveEarned",parentBirth.ToString("N"));Directory.CreateDirectory(folder);
   string original=Path.Combine(folder,parentOp.ToString("N")+".rew"),parentPath=original+".terminal-load",linkPath=original+".earned-child";
   string childFolder=Path.Combine(root,"RebirthProductiveEarned",birth.ToString("N"));Directory.CreateDirectory(childFolder);
   string childPath=Path.Combine(childFolder,op.ToString("N")+".rew");
   byte[] parentImage=Enumerable.Repeat((byte)1,200).ToArray(),parentOriginal=Enumerable.Repeat((byte)2,100).ToArray(),childImage=Enumerable.Repeat((byte)3,100).ToArray();
   byte[] before=Enumerable.Repeat((byte)4,64).ToArray(),after=Enumerable.Repeat((byte)5,64).ToArray();
   File.WriteAllBytes(parentPath,parentImage);File.WriteAllBytes(childPath,childImage);
   byte[] link;
   using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream))using(var sha=SHA256.Create())
   {
    writer.Write(0x31434552u);writer.Write(parentBirth.ToByteArray());writer.Write(parentOp.ToByteArray());writer.Write(birth.ToByteArray());writer.Write(op.ToByteArray());
    writer.Write(sha.ComputeHash(parentOriginal));writer.Write(sha.ComputeHash(childImage));writer.Write(sha.ComputeHash(before));writer.Flush();link=stream.ToArray();
   }
   File.WriteAllBytes(linkPath,link);File.WriteAllBytes(linkPath+".invoking",link);
   var frame=new RebirthProductiveEarnedRowProof.Frame{Birth=birth,Operation=op,BeforeNative=before,AfterNative=after};RebirthProductiveEarnedRowProof.Decoded=frame;
   var parent=new RebirthProductiveEarnedSavedNativeSource.Evidence(new Lease()){Path=parentPath,Directory=root,WorldGuid="world-guid",MarkerKey="marker",
    Terminal=true,PendingNextWork=true,PendingTimerBefore=-12,PendingTimerTarget=-7,Image=parentImage,Original=parentOriginal,Native=before,
    Frame=new RebirthProductiveEarnedRowProof.Frame{Birth=parentBirth,Operation=parentOp}};
   parent.Load=new RebirthProductiveEarnedSavedNativeSource.CachedLoadRequest{X=1,Z=2};
   File.Delete(linkPath+".invoking");
   Need(RebirthProductiveEarnedSavedNativeSource.TryReadStagedNativeChild(parent,()=>new Lease(),out var staged)&&
    staged.StagedChildLink.SequenceEqual(link)&&staged.Dependencies.Length==2&&staged.Load.Loaded==null,
    "unentered exact child did not retain candidate-only custody");
   Need(!RebirthProductiveEarnedSavedNativeSource.ReadChild(parent,out _),"unentered child impersonated invoked After");
   Need(!RebirthProductiveEarnedSavedNativeSource.TryReadStagedNativeChild(parent,()=>null,out _),"absent callback phase admitted child");
   File.WriteAllBytes(linkPath+".invoking.tmp",link);
   Need(!RebirthProductiveEarnedSavedNativeSource.TryReadStagedNativeChild(parent,()=>new Lease(),out _),"uncertain child invocation became unentered");File.Delete(linkPath+".invoking.tmp");
   File.WriteAllBytes(childPath+".earned-load",new byte[]{1});
   Need(!RebirthProductiveEarnedSavedNativeSource.TryReadStagedNativeChild(parent,()=>new Lease(),out _),"published child offered unentered work");File.Delete(childPath+".earned-load");
   File.WriteAllBytes(linkPath+".invoking",link);
   Need(!RebirthProductiveEarnedSavedNativeSource.TryReadStagedNativeChild(parent,()=>new Lease(),out _),"entered child obtained first-application custody");
   Need(RebirthProductiveEarnedSavedNativeSource.ReadChild(parent,out var child)&&child.Load.PredictedNative&&!child.Terminal&&child.Load.Loaded==null,"exact invoked child failed candidate-only admission");
   Need(child.Native.SequenceEqual(after)&&child.Load.Tile.Length==after.Length+10&&child.Load.X==1&&child.Load.Z==2,"expected native span/source coordinates lost");
   Need(child.PrimaryCurrent(),"fresh immutable dependencies refused");
   File.WriteAllBytes(linkPath+".invoking",new byte[]{0});Need(!child.PrimaryCurrent()&&!RebirthProductiveEarnedSavedNativeSource.ReadChild(parent,out _),"changed invoking record accepted");File.WriteAllBytes(linkPath+".invoking",link);
   File.WriteAllBytes(linkPath+".invoking.tmp",link);Need(!RebirthProductiveEarnedSavedNativeSource.ReadChild(parent,out _),"uncertain invoking candidate accepted");File.Delete(linkPath+".invoking.tmp");
   frame.WorkedSeconds=4;Need(!RebirthProductiveEarnedSavedNativeSource.ReadChild(parent,out _),"wrong actual child work accepted");frame.WorkedSeconds=5;
   frame.BeforeNative=after;Need(!RebirthProductiveEarnedSavedNativeSource.ReadChild(parent,out _),"unrelated native preimage accepted");frame.BeforeNative=before;
   RebirthProductiveEarnedObservationBudget.Resize=false;Need(!RebirthProductiveEarnedSavedNativeSource.ReadChild(parent,out _),"graph pressure bypassed");RebirthProductiveEarnedObservationBudget.Resize=true;
   childImage[0]=99;File.WriteAllBytes(childPath,childImage);Need(!RebirthProductiveEarnedSavedNativeSource.ReadChild(parent,out _),"changed full child original accepted");
   Console.WriteLine("PASS extracted invoked-child candidate and dependency checks; no load or setter permission qualified");
  }
  finally
  {
   foreach(string file in Directory.EnumerateFiles(root,"*",SearchOption.AllDirectories))File.Delete(file);
   foreach(string directory in Directory.EnumerateDirectories(root,"*",SearchOption.AllDirectories).OrderByDescending(value=>value.Length))Directory.Delete(directory);
   Directory.Delete(root);
  }
 }
}

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Security.Cryptography;
// AUTHORED_NOT_RUN. Both complete production definition partials are injected.
// Native invocation, budget, bounded-string codec and outer REL2 admission are explicit doubles.
// ACTUAL_PENDING_DEFINITION_SOURCE
internal sealed class Lease:IDisposable
{internal static int Live;internal Lease(){Live++;}public void Dispose(){Live--;}}
internal static class RebirthProductiveEarnedObservationBudget
{
 internal static bool Available=true;
 internal static bool TryRetainDetached(long bytes,out IDisposable retained){retained=Available?new Lease():null;return retained!=null;}
}
internal sealed class Budget
{
 internal IDisposable TryEnterCallbacks()=>new Lease();
 internal bool TryReserveAllocation(long bytes,out IDisposable retained)=>RebirthProductiveEarnedObservationBudget.TryRetainDetached(bytes,out retained);
}
internal sealed class Recipe {internal string Extras="successor-extras";}
internal sealed class RecipeQueueItem {internal Recipe Recipe=new Recipe();internal string Binding="successor-binding";}
internal sealed class TileEntityWorkstation {}
internal static class RebirthRecyclerNativeLoop
{internal static object Token=new object();internal static object InvocationToken(TileEntityWorkstation station)=>Token;}
internal static class RebirthProductiveEarnedUiQueueOwner
{internal static string Binding(RecipeQueueItem row)=>row.Binding;}
internal static class RebirthRecipeOutputs
{internal static string DefinitionIdentity(Recipe recipe)=>recipe.Extras;}
internal static class RebirthProductiveEarnedRowProof
{
 internal const int MaximumFrameBytes=1024;
 internal sealed class Frame {internal Guid Birth,Operation;}
}
internal static class RebirthProductiveEarnedNativeBody
{internal const int MaximumBodyBytes=4096;internal static byte[] ComparisonDigest(byte[] bytes,int offset){using(var sha=SHA256.Create())return sha.ComputeHash(bytes);}}
internal static class RebirthProductiveEarnedArtifact
{internal static string Root;internal static bool SafePath(string path)=>Path.GetFullPath(path).StartsWith(Root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase);}
internal static class RebirthSurvivorNetworkCodec
{
 // Disclosed bounded-string adapter; the actual codec is covered separately.
 internal static string ReadBoundedString(BinaryReader reader,int maximum)
 {string value=reader.ReadString();if(Encoding.UTF8.GetByteCount(value)>maximum)throw new InvalidDataException();return value;}
}
internal static partial class RebirthProductiveEarnedCoordinator
{
 internal sealed class Artifact {internal Guid Birth=Guid.NewGuid(),Operation=Guid.NewGuid();internal string OriginalPath;}
 internal sealed class Terminal {internal bool Valid=true;internal byte[] Body=new byte[]{1,2,3};internal int ElapsedOffset;internal Budget Memory=new Budget();internal bool Current()=>Valid;}
 internal sealed class Attempt
 {
  internal bool OutputTimerObserved=true,PendingTimerObserved=true,PendingTimerAmbiguous;
  internal object NativeOutput=RebirthRecyclerNativeLoop.Token;internal TileEntityWorkstation Station=new TileEntityWorkstation();
  internal string PendingRecipeBinding,PendingExtras;internal IDisposable PendingDefinitionRetention;
  internal Terminal Terminal=new Terminal();internal Artifact Artifact=new Artifact();internal int PendingTimerIndex=2;
  internal float PendingTimerBefore=4,PendingTimerTarget=1;
 }
 internal static bool Capture(Attempt a,RecipeQueueItem row)=>CapturePendingDefinition(a,row);
 internal static bool Publish(Attempt a)=>PublishPendingDefinition(a);
 internal static bool MatchesRow(Attempt a,RecipeQueueItem row)=>MatchesPendingDefinition(a,row);
}
internal static partial class RebirthProductiveEarnedSavedNativeSource
{
 internal sealed class NativeChildDependency {internal string Path;internal byte[] Image;}
 private static NativeChildDependency NativeChildDependencyFor(string path,byte[] bytes)=>new NativeChildDependency{Path=path,Image=bytes};
 internal sealed class Evidence:IDisposable
 {
  internal bool Terminal=true,PendingNextWork=true,Valid=true;internal string Path,PendingRecipeBinding,PendingExtras;
  internal int PendingTimerIndex=2,NativeOffset;internal float PendingTimerBefore=4,PendingTimerTarget=1;
  internal byte[] Native=new byte[]{1,2,3};internal RebirthProductiveEarnedRowProof.Frame Frame;
  internal bool ToolsRebased;internal byte[] ToolsBefore;internal int ToolsBeforeOffset;
  internal IDisposable DefinitionRetention;internal NativeChildDependency[] Dependencies;
  internal bool PrimaryCurrent()=>Valid&&(Dependencies==null||Dependencies.All(d=>!File.Exists(d.Path+".tmp")&&File.ReadAllBytes(d.Path).SequenceEqual(d.Image)));
  public void Dispose(){DefinitionRetention?.Dispose();DefinitionRetention=null;}
 }
 private static byte[] Read(string path,int maximum){byte[] bytes=File.ReadAllBytes(path);if(bytes.Length>maximum)throw new InvalidDataException();return bytes;}
 private static bool Matches(string path,byte[] bytes)=>File.ReadAllBytes(path).SequenceEqual(bytes);
 internal static bool ReadDefinition(Evidence source)=>TryReadPendingDefinition(source);
}
internal static class Program
{
 private static void Need(bool value,string message){if(!value)throw new Exception(message);}
 private static RebirthProductiveEarnedSavedNativeSource.Evidence Source(RebirthProductiveEarnedCoordinator.Attempt a)
 =>new RebirthProductiveEarnedSavedNativeSource.Evidence{Path=a.Artifact.OriginalPath+".terminal-load",
  Frame=new RebirthProductiveEarnedRowProof.Frame{Birth=a.Artifact.Birth,Operation=a.Artifact.Operation}};
 public static void Main()
 {
  string root=Path.Combine(Path.GetTempPath(),"rb-pending-definition-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);RebirthProductiveEarnedArtifact.Root=root;
  var a=new RebirthProductiveEarnedCoordinator.Attempt();a.Artifact.OriginalPath=Path.Combine(root,"parent.rew");var row=new RecipeQueueItem();
  string path=a.Artifact.OriginalPath+".pending-definition";
  try
  {
   using(var legacy=Source(a))Need(RebirthProductiveEarnedSavedNativeSource.ReadDefinition(legacy)&&legacy.PendingRecipeBinding==null,"legacy definition invented");
   a.NativeOutput=null;RebirthRecyclerNativeLoop.Token=null;Need(!RebirthProductiveEarnedCoordinator.Capture(a,row),"two absent tokens captured definition");RebirthRecyclerNativeLoop.Token=new object();
   a.NativeOutput=new object();Need(!RebirthProductiveEarnedCoordinator.Capture(a,row),"foreign invocation captured");a.NativeOutput=RebirthRecyclerNativeLoop.Token;
   RebirthProductiveEarnedObservationBudget.Available=false;Need(!RebirthProductiveEarnedCoordinator.Capture(a,row),"uncharged capture");RebirthProductiveEarnedObservationBudget.Available=true;
   row.Binding=new string('x',1025);Need(!RebirthProductiveEarnedCoordinator.Capture(a,row),"oversized binding captured");row.Binding="successor-binding";
   Need(RebirthProductiveEarnedCoordinator.Capture(a,row)&&RebirthProductiveEarnedCoordinator.Capture(a,row),"exact capture retry failed");
   row.Recipe.Extras="changed";Need(!RebirthProductiveEarnedCoordinator.Capture(a,row)&&!RebirthProductiveEarnedCoordinator.MatchesRow(a,row),"current getter replaced frozen definition");row.Recipe.Extras="successor-extras";
   Need(RebirthProductiveEarnedCoordinator.Publish(a)&&RebirthProductiveEarnedCoordinator.Publish(a),"immutable definition publication failed");byte[] record=File.ReadAllBytes(path);
   using(var source=Source(a))
   {
    Need(RebirthProductiveEarnedSavedNativeSource.ReadDefinition(source)&&source.PendingRecipeBinding==row.Binding&&source.PendingExtras==row.Recipe.Extras,"actual writer/parser roundtrip lost successor definition");
    Need(source.Dependencies.Length==1&&source.PrimaryCurrent(),"missing dependency");File.WriteAllText(path+".tmp","unknown");Need(!source.PrimaryCurrent(),"unknown temporary remained current");File.Delete(path+".tmp");
   }
   int baseline=Lease.Live;RebirthProductiveEarnedObservationBudget.Available=false;
   using(var source=Source(a))Need(!RebirthProductiveEarnedSavedNativeSource.ReadDefinition(source),"uncharged record parsed");RebirthProductiveEarnedObservationBudget.Available=true;
   for(int variant=0;variant<10;variant++)
   {
    File.WriteAllBytes(path,record);
    using(var source=Source(a))
    {
     if(variant==0)source.Frame.Operation=Guid.NewGuid();
     if(variant==1)source.PendingTimerIndex++;
     if(variant==2)source.PendingTimerTarget++;
     if(variant==3)source.Native=new byte[]{9};
     if(variant==4)File.WriteAllBytes(path,record.Concat(new byte[]{0}).ToArray());
     if(variant==5)File.WriteAllText(path+".tmp","unknown");
     if(variant==6)source.Valid=false;
     if(variant==7){var bad=(byte[])record.Clone();bad[49]=255;File.WriteAllBytes(path,bad);} // First binding byte: strict UTF8.
     if(variant==8)File.WriteAllBytes(path,record.Take(60).ToArray());
     if(variant==9)File.WriteAllBytes(path,new byte[2177]);
     Need(!RebirthProductiveEarnedSavedNativeSource.ReadDefinition(source)&&source.PendingRecipeBinding==null,"invalid definition accepted: "+variant);
    }
    if(File.Exists(path+".tmp"))File.Delete(path+".tmp");Need(Lease.Live==baseline,"failed parse retained allocation");
   }
   File.WriteAllBytes(path,record);a.PendingTimerTarget=0;Need(!RebirthProductiveEarnedCoordinator.Publish(a),"conflicting immutable publication overwritten");
   Console.WriteLine("PASS extracted pending definition capture/publication/parser and immutable WAL; disclosed native/budget/codec adapters");
  }
  finally{a.PendingDefinitionRetention?.Dispose();foreach(string file in Directory.EnumerateFiles(root))File.Delete(file);Directory.Delete(root);}
 }
}

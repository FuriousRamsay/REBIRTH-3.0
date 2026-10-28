using System;
using System.Linq;
using System.Collections.Generic;
// AUTHORED_NOT_RUN. Entire actual metadata cleanup pump is injected.
// Actual-load receipt admission is separately extracted by earned_load.
// No station/inventory/marker setter API exists in these disposal doubles.
// ACTUAL_LOADED_CLEANUP_SOURCE
internal static class ThreadManager {internal static bool Main=true;internal static bool IsMainThread()=>Main;}
internal sealed class TileEntityWorkstation {}
internal sealed class Lease:IDisposable
{internal int Disposals;public void Dispose(){Disposals++;}}
internal static class RebirthProductiveEarnedArtifact
{
 internal sealed class Prepared
 {internal Guid Birth=Guid.NewGuid(),Operation=Guid.NewGuid();internal object LoadedSource=new object();internal byte[] Digest=new byte[32];internal byte[] OriginalDigest()=>Digest;}
}
internal static class RebirthProductiveEarnedSavedNativeSource
{
 internal sealed class CompletedNativeLoad:IDisposable
 {internal bool Valid=true;internal int Disposals;internal bool Current()=>Valid;public void Dispose(){Valid=false;Disposals++;}}
 internal static CompletedNativeLoad Proof;internal static Guid Birth,Operation;internal static byte[] Body,Digest;internal static int Releases;
 internal static bool TryObservedCompletedNativeLoad(Guid birth,Guid operation,byte[] digest,byte[] body,int offset,out CompletedNativeLoad proof)
 {proof=Proof;return proof?.Current()==true&&birth==Birth&&operation==Operation&&digest.SequenceEqual(Digest)&&body!=null&&body.SequenceEqual(Body);}
 internal static void Release(object source){Releases++;}
}
internal static partial class RebirthProductiveEarnedCoordinator
{
 internal sealed class Terminal:IDisposable
 {
  internal bool PendingNextWork,ThrowOnce;internal byte[] Body=new byte[]{1,2,3};internal int ElapsedOffset,Disposals;
  public void Dispose(){if(ThrowOnce){ThrowOnce=false;throw new Exception("disposal fault");}Disposals++;Body=null;}
 }
 internal sealed class Partial:IDisposable
 {internal bool Returned;internal byte[] After=new byte[]{4,5,6};internal int AfterOffset,Disposals;public void Dispose(){Disposals++;After=null;}}
 internal sealed class Attempt
 {
  internal RebirthProductiveEarnedArtifact.Prepared Artifact=new RebirthProductiveEarnedArtifact.Prepared();
  internal bool OutputTimerObserved=true,ClearedNativeMarker;internal object EarnedChild;internal Terminal Terminal=new Terminal();internal Partial PartialSuccessor;
  internal TileEntityWorkstation Station=new TileEntityWorkstation();internal Lease Observation=new Lease(),Retention=new Lease(),PendingDefinitionRetention=new Lease();
  internal object Tools=new object(),Frame=new object();internal RebirthProductiveEarnedSavedNativeSource.CompletedNativeLoad LoadedCleanup;
 }
 private static readonly Dictionary<TileEntityWorkstation,Attempt> retained=new Dictionary<TileEntityWorkstation,Attempt>();
 internal static int Count=>retained.Count;
 internal static Attempt Setup(bool partial=false)
 {
  retained.Clear();ThreadManager.Main=true;var a=new Attempt();retained.Add(a.Station,a);
  if(partial){a.Terminal.PendingNextWork=true;a.PartialSuccessor=new Partial{Returned=true};}
  RebirthProductiveEarnedSavedNativeSource.Proof=new RebirthProductiveEarnedSavedNativeSource.CompletedNativeLoad();
  RebirthProductiveEarnedSavedNativeSource.Birth=a.Artifact.Birth;RebirthProductiveEarnedSavedNativeSource.Operation=a.Artifact.Operation;
  RebirthProductiveEarnedSavedNativeSource.Digest=(byte[])a.Artifact.Digest.Clone();
  RebirthProductiveEarnedSavedNativeSource.Body=(byte[])(partial?a.PartialSuccessor.After:a.Terminal.Body).Clone();RebirthProductiveEarnedSavedNativeSource.Releases=0;return a;
 }
 internal static void Pump()=>PumpLoadedNativeCleanup();
}
internal static class Program
{
 private static void Need(bool value,string message){if(!value)throw new Exception(message);}
 public static void Main()
 {
  var a=RebirthProductiveEarnedCoordinator.Setup();var terminal=a.Terminal;var observation=a.Observation;var retention=a.Retention;var definition=a.PendingDefinitionRetention;var proof=RebirthProductiveEarnedSavedNativeSource.Proof;
  Need(RebirthProductiveEarnedCoordinator.NeedsLoadedNativeCleanup(a.Artifact.Birth,a.Artifact.Operation),"complete retained terminal not eligible for metadata receipt");
  RebirthProductiveEarnedCoordinator.Pump();
  Need(RebirthProductiveEarnedCoordinator.Count==0&&terminal.Disposals==1&&observation.Disposals==1&&retention.Disposals==1&&definition.Disposals==1&&proof.Disposals==1&&RebirthProductiveEarnedSavedNativeSource.Releases==1,
   "completed retained resources not disposed exactly once");
  RebirthProductiveEarnedCoordinator.Pump();Need(proof.Disposals==1,"metadata cleanup replayed");
  a=RebirthProductiveEarnedCoordinator.Setup(true);var partial=a.PartialSuccessor;
  RebirthProductiveEarnedCoordinator.Pump();Need(RebirthProductiveEarnedCoordinator.Count==0&&partial.Disposals==1,"returned partial did not retire through exact loaded terminal");
  a=RebirthProductiveEarnedCoordinator.Setup();a.Terminal.PendingNextWork=true;
  Need(!RebirthProductiveEarnedCoordinator.NeedsLoadedNativeCleanup(a.Artifact.Birth,a.Artifact.Operation),"pending native work requested cleanup receipt");
  RebirthProductiveEarnedCoordinator.Pump();Need(RebirthProductiveEarnedCoordinator.Count==1,"pending native work disposed");
  a=RebirthProductiveEarnedCoordinator.Setup();a.EarnedChild=new object();RebirthProductiveEarnedCoordinator.Pump();Need(RebirthProductiveEarnedCoordinator.Count==1,"child operation discarded with predecessor");
  a=RebirthProductiveEarnedCoordinator.Setup();a.ClearedNativeMarker=true;RebirthProductiveEarnedCoordinator.Pump();Need(RebirthProductiveEarnedCoordinator.Count==1&&!RebirthProductiveEarnedCoordinator.NeedsLoadedNativeCleanup(a.Artifact.Birth,a.Artifact.Operation),"warm cleared-marker cleanup interleaved with loaded receipt disposal");
  a=RebirthProductiveEarnedCoordinator.Setup(true);a.PartialSuccessor.Returned=false;RebirthProductiveEarnedCoordinator.Pump();Need(RebirthProductiveEarnedCoordinator.Count==1,"unknown partial discarded");
  a=RebirthProductiveEarnedCoordinator.Setup();RebirthProductiveEarnedSavedNativeSource.Body=new byte[]{9};RebirthProductiveEarnedCoordinator.Pump();Need(RebirthProductiveEarnedCoordinator.Count==1,"different native body disposed attempt");
  a=RebirthProductiveEarnedCoordinator.Setup();RebirthProductiveEarnedSavedNativeSource.Operation=Guid.NewGuid();RebirthProductiveEarnedCoordinator.Pump();Need(RebirthProductiveEarnedCoordinator.Count==1,"different operation disposed attempt");
  a=RebirthProductiveEarnedCoordinator.Setup();a.Artifact.Digest[0]=9;RebirthProductiveEarnedCoordinator.Pump();Need(RebirthProductiveEarnedCoordinator.Count==1,"same operation/body but different original disposed attempt");
  a=RebirthProductiveEarnedCoordinator.Setup();RebirthProductiveEarnedSavedNativeSource.Proof.Valid=false;RebirthProductiveEarnedCoordinator.Pump();Need(RebirthProductiveEarnedCoordinator.Count==1,"stale receipt disposed attempt");
  a=RebirthProductiveEarnedCoordinator.Setup(true);partial=a.PartialSuccessor;a.Terminal.ThrowOnce=true;proof=RebirthProductiveEarnedSavedNativeSource.Proof;
  RebirthProductiveEarnedCoordinator.Pump();Need(RebirthProductiveEarnedCoordinator.Count==1&&a.LoadedCleanup==proof&&partial.Disposals==1&&proof.Disposals==0,"disposal interruption lost receipt or disposed twice");
  RebirthProductiveEarnedCoordinator.Pump();Need(RebirthProductiveEarnedCoordinator.Count==0&&partial.Disposals==1&&proof.Disposals==1,"retry required cleared native comparison bytes or repeated disposal");
  a=RebirthProductiveEarnedCoordinator.Setup();ThreadManager.Main=false;RebirthProductiveEarnedCoordinator.Pump();Need(RebirthProductiveEarnedCoordinator.Count==1,"cleanup ran off main thread");
  Console.WriteLine("PASS extracted actual-loaded metadata cleanup pump; disclosed receipt/native/disposal adapters, no gameplay setters");
 }
}

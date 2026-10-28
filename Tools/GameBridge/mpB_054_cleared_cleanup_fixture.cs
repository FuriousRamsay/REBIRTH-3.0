using System;
using System.Collections.Generic;
// AUTHORED_NOT_RUN. Entire actual cleared-marker metadata finalizer is injected.
// Positive saved/clear admission is tested separately by actual partial completion.
// These doubles expose disposal/retention only: no native marker/timer/output setter API.
// ACTUAL_CLEARED_CLEANUP_SOURCE
internal sealed class Lease:IDisposable
{internal bool ThrowOnce;internal int Disposals;public void Dispose(){if(ThrowOnce){ThrowOnce=false;throw new Exception("disposal fault");}Disposals++;}}
internal sealed class TileEntityWorkstation {}
internal static class RebirthProductiveEarnedArtifact
{
 internal sealed class CompletionRetention {internal int Calls;internal bool Result=true;internal bool TryRetain(){Calls++;return Result;}}
 internal sealed class Prepared {internal Guid Birth=Guid.NewGuid(),Operation=Guid.NewGuid();internal object LoadedSource=new object();}
}
internal static class RebirthProductiveEarnedSavedNativeSource
{
 internal static bool ThrowRelease;internal static int ReleaseAttempts,ReleaseSuccesses,RegisteredReleases;private static bool released;
 internal static void Reset(){ThrowRelease=false;ReleaseAttempts=ReleaseSuccesses=RegisteredReleases=0;released=false;}
 internal static void Release(object source){ReleaseAttempts++;if(ThrowRelease){ThrowRelease=false;throw new Exception("source release fault");}if(!released){released=true;ReleaseSuccesses++;}}
 internal static void ReleaseRegisteredTerminal(Guid birth,Guid operation){RegisteredReleases++;}
}
internal static partial class RebirthProductiveEarnedCoordinator
{
 internal sealed class Attempt
 {
  internal bool ClearedNativeMarker=true,NativeMetadataClosed;internal object EarnedChild;
  internal Lease PartialSuccessor=new Lease(),Terminal=new Lease(),Observation=new Lease(),PendingDefinitionRetention=new Lease(),Retention=new Lease();
  internal object Tools=new object(),Frame=new object();internal string PendingRecipeBinding="binding",PendingExtras="extras";
  internal RebirthProductiveEarnedArtifact.CompletionRetention CompletionRetention=new RebirthProductiveEarnedArtifact.CompletionRetention();
  internal RebirthProductiveEarnedArtifact.Prepared Artifact=new RebirthProductiveEarnedArtifact.Prepared();internal TileEntityWorkstation Station=new TileEntityWorkstation();
 }
 private static readonly Dictionary<TileEntityWorkstation,Attempt> retained=new Dictionary<TileEntityWorkstation,Attempt>();
 internal static int Count=>retained.Count;
 internal static Attempt Setup(){retained.Clear();RebirthProductiveEarnedSavedNativeSource.Reset();var a=new Attempt();retained.Add(a.Station,a);return a;}
 internal static bool Finish(Attempt original)=>TryFinishClearedNativeMetadata(original);
}
internal static class Program
{
 private static void Need(bool value,string message){if(!value)throw new Exception(message);}
 public static void Main()
 {
  var a=RebirthProductiveEarnedCoordinator.Setup();var partial=a.PartialSuccessor;var terminal=a.Terminal;var observation=a.Observation;var definition=a.PendingDefinitionRetention;var retention=a.Retention;var archive=a.CompletionRetention;
  Need(RebirthProductiveEarnedCoordinator.Finish(a)&&a.NativeMetadataClosed&&RebirthProductiveEarnedCoordinator.Count==0&&partial.Disposals==1&&terminal.Disposals==1&&observation.Disposals==1&&definition.Disposals==1&&retention.Disposals==1&&archive.Calls==1,"cleared metadata not retired exactly once");
  Need(RebirthProductiveEarnedCoordinator.Finish(a)&&archive.Calls==1&&RebirthProductiveEarnedSavedNativeSource.ReleaseAttempts==1,"finished metadata cleanup replayed");
  a=RebirthProductiveEarnedCoordinator.Setup();a.ClearedNativeMarker=false;partial=a.PartialSuccessor;
  Need(!RebirthProductiveEarnedCoordinator.Finish(a)&&RebirthProductiveEarnedCoordinator.Count==1&&partial.Disposals==0,"missing positive exact-clear latch disposed custody");
  a=RebirthProductiveEarnedCoordinator.Setup();a.EarnedChild=new object();Need(!RebirthProductiveEarnedCoordinator.Finish(a)&&RebirthProductiveEarnedCoordinator.Count==1,"different child operation discarded");
  a=RebirthProductiveEarnedCoordinator.Setup();partial=a.PartialSuccessor;partial.ThrowOnce=true;archive=a.CompletionRetention;
  Need(!RebirthProductiveEarnedCoordinator.Finish(a)&&RebirthProductiveEarnedCoordinator.Count==1&&a.ClearedNativeMarker&&archive.Calls==0,"partial disposal fault lost positive-clear attempt");
  Need(RebirthProductiveEarnedCoordinator.Finish(a)&&partial.Disposals==1&&archive.Calls==1,"partial disposal retry failed");
  a=RebirthProductiveEarnedCoordinator.Setup();a.Observation.ThrowOnce=true;partial=a.PartialSuccessor;terminal=a.Terminal;archive=a.CompletionRetention;
  Need(!RebirthProductiveEarnedCoordinator.Finish(a)&&RebirthProductiveEarnedCoordinator.Count==1&&a.PartialSuccessor==null&&a.Terminal==null,"observation fault lost latch or repeated earlier fields");
  Need(RebirthProductiveEarnedCoordinator.Finish(a)&&partial.Disposals==1&&terminal.Disposals==1&&archive.Calls==1,"retry required removed marker or old terminal body");
  a=RebirthProductiveEarnedCoordinator.Setup();archive=a.CompletionRetention;RebirthProductiveEarnedSavedNativeSource.ThrowRelease=true;
  Need(!RebirthProductiveEarnedCoordinator.Finish(a)&&RebirthProductiveEarnedCoordinator.Count==1&&archive.Calls==1&&a.CompletionRetention==null&&a.Frame==null,"source release fault lost retry or retention token disposition");
  Need(RebirthProductiveEarnedCoordinator.Finish(a)&&archive.Calls==1&&RebirthProductiveEarnedSavedNativeSource.ReleaseSuccesses==1,"source release retry repeated archive action");
  a=RebirthProductiveEarnedCoordinator.Setup();retention=a.Retention;retention.ThrowOnce=true;archive=a.CompletionRetention;
  Need(!RebirthProductiveEarnedCoordinator.Finish(a)&&RebirthProductiveEarnedCoordinator.Count==1&&a.Retention==retention,"base retention fault lost retry custody");
  Need(RebirthProductiveEarnedCoordinator.Finish(a)&&archive.Calls==1&&retention.Disposals==1&&RebirthProductiveEarnedSavedNativeSource.ReleaseSuccesses==1,"final retention retry duplicated cleanup");
  a=RebirthProductiveEarnedCoordinator.Setup();archive=a.CompletionRetention;archive.Result=false;
  Need(RebirthProductiveEarnedCoordinator.Finish(a)&&archive.Calls==1,"preserved archive conflict invalidated already confirmed native completion");
  Console.WriteLine("PASS extracted exact-cleared metadata finalizer and failure retries; disclosed positive-clear/disposal/file adapters, no native setters");
 }
}

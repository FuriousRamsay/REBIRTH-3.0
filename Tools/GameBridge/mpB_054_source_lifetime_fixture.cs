using System;
using System.Collections.Generic;
using System.Linq;
// AUTHORED_NOT_RUN. Entire production Evidence class is extracted.
// Source admission/marker/native identity are explicit unused adapters; only ownership/disposal is qualified here.
internal sealed class World {}
internal sealed class TileEntityWorkstation {}
internal sealed class ChunkCustomData
{internal object TriggerWriteDataDelegate;internal bool isSavedToNetwork;internal ulong expiresInWorldTime=ulong.MaxValue;internal byte[] data;}
internal sealed class ChunkData {internal Dictionary<string,ChunkCustomData> dict=new Dictionary<string,ChunkCustomData>();}
internal sealed class Chunk {internal ChunkData ChunkCustomData=new ChunkData();}
internal static class RebirthLogisticsEndpointIdentity
{internal static bool TryGetIncarnation(World world,TileEntityWorkstation station,out string value){value=null;return false;}}
internal static class RebirthProductiveEarnedRowProof
{internal sealed class Frame {internal Guid Birth,Operation;}}
internal sealed class Lease:IDisposable
{internal int Disposals;internal bool Throw;public void Dispose(){Disposals++;if(Throw)throw new Exception("disposal fault");}}
internal static partial class RebirthProductiveEarnedSavedNativeSource
{
 // ACTUAL_SOURCE_EVIDENCE
 internal sealed class NativeChildDependency {}
 internal sealed class Loaded {internal TileEntityWorkstation Tile;internal bool Installed(World world)=>false;}
 internal sealed class CachedLoadRequest {internal byte[] Snapshot,Tile;internal Loaded Loaded;}
 private static bool Matches(string path,byte[] image)=>false;
 private static bool NativeChildDependenciesCurrent(NativeChildDependency[] dependencies)=>false;
 private static bool CanLoadSavedCandidate(Evidence source)=>false;
}
internal static class Program
{
 private static void Need(bool value,string message){if(!value)throw new Exception(message);}
 private static RebirthProductiveEarnedSavedNativeSource.Evidence Make(Lease memory,Lease definition)
 =>new RebirthProductiveEarnedSavedNativeSource.Evidence(memory){DefinitionRetention=definition,Image=new byte[]{1},Original=new byte[]{2},Native=new byte[]{3},Marker=new byte[]{4},
  PendingRecipeBinding="binding",PendingExtras="extras",StagedPartialRecord=new byte[]{5},StagedChildLink=new byte[]{6},
  Load=new RebirthProductiveEarnedSavedNativeSource.CachedLoadRequest{Snapshot=new byte[]{7},Tile=new byte[]{8}}};
 public static void Main()
 {
  var memory=new Lease();var definition=new Lease{Throw=true};var source=Make(memory,definition);var load=source.Load;
  var held=source.RetainNativeLoad();Need(held!=null,"live evidence did not retain native load lease");source.Dispose();
  Need(!source.Active&&source.Image!=null&&memory.Disposals==0&&definition.Disposals==0&&source.RetainNativeLoad()==null,"inactive owner freed in-flight bytes or issued another lease");
  bool threw=false;try{held.Dispose();}catch{threw=true;}
  Need(threw&&memory.Disposals==1&&definition.Disposals==1&&source.DefinitionRetention==null&&source.Image==null&&source.Original==null&&source.Native==null&&source.Marker==null&&source.Load==null&&
   source.PendingRecipeBinding==null&&source.PendingExtras==null&&source.StagedPartialRecord==null&&source.StagedChildLink==null&&load.Tile==null&&load.Snapshot==null,"definition failure stranded retained base memory or detached source arrays");
  held.Dispose();source.Dispose();Need(memory.Disposals==1&&definition.Disposals==1,"failed final release replayed ownership disposal");
  memory=new Lease();definition=new Lease{Throw=true};source=Make(memory,definition);threw=false;try{source.Dispose();}catch{threw=true;}
  Need(threw&&!source.Active&&memory.Disposals==1&&source.Load==null,"direct release failed to retire base allocation after definition exception");source.Dispose();Need(memory.Disposals==1,"inactive source disposed twice");
  memory=new Lease();definition=new Lease();source=Make(memory,definition);var first=source.RetainNativeLoad();var second=source.RetainNativeLoad();source.Dispose();first.Dispose();
  Need(memory.Disposals==0&&source.Image!=null,"first of two native readers freed shared original bytes");second.Dispose();Need(memory.Disposals==1&&definition.Disposals==1&&source.Image==null,"last native reader did not retire retained evidence");
  Console.WriteLine("PASS extracted saved Evidence reference lifetime and exception-safe final release; no native/source admission claimed");
 }
}

// PREPARED NOT RUN. Tests the actual allocation class, not native publication.
// Doubles: main-thread=true; immutable artifact Current=true/128-byte image;
// frame identity only; native structure measure returns graph64/scratch32.
// No file, game, native inventory, authority or receipt behavior is simulated.
using System;
using System.Collections.Generic;
interface IRebirthStationSnapshotByteBudget {int MaximumSnapshotBytes{get;}bool TryReserveAllocation(long n,out IDisposable token);}
static class ThreadManager {internal static bool IsMainThread()=>true;}
static class RebirthNativeItemConformanceReader { // ACTUAL_ITEM_BOUNDS
internal struct StructureMeasure {internal long CloneAllocationBound=>64;internal long ParserScratchBound=>32;}
internal static bool TryMeasureCanonicalV9PrefixStructure(byte[] b,int offset,out StructureMeasure m,out int n){m=new StructureMeasure();n=1;return offset<b.Length&&b[offset]==9;}
}
static class RebirthRemoteResourcePlacedStationArtifact { // ACTUAL_WORKSPACE_BOUND
}
static class RebirthProductiveEarnedNativeBody { // ACTUAL_BODY_BOUND
internal static bool TryMeasure(byte[] n,int o,out long g,out long s){g=64;s=32;return n!=null;}}
static class RebirthProductiveEarnedRowProof { // ACTUAL_ROW_BOUNDS
internal static byte[] Marker(Frame f,byte[] image)=>new byte[]{1};
internal sealed class Frame {internal Guid Operation,Birth;internal string World;internal byte[] BeforeNative,AfterNative;internal int BeforeElapsedOffset,AfterElapsedOffset;}}
class World {internal EntityPlayerLocal Actor;internal bool IsRemote()=>true;internal object GetPrimaryPlayer()=>Actor;}
class EntityPlayerLocal {internal World world;internal bool IsDead()=>false;}
class GameManager {internal static GameManager Instance=new GameManager();internal World World;}
class RebirthCamperStationState {}
static class RebirthCamperStationSnapshot {internal static byte[] CaptureNativeMeasurement(RebirthCamperStationState s,int maximum)=>new byte[4];}
static class RebirthRemoteResourcePlacedStationNativeReader {internal static bool TryMeasure(byte[] b,out long graph,out long scratch){graph=64;scratch=32;return b!=null;}}
static class RebirthStationSnapshotEvidence { // ACTUAL_SNAPSHOT_BOUND
}
static class RebirthStationRegionPayload { // ACTUAL_PAYLOAD_BOUND
}
static class RebirthProductiveEarnedArtifact {internal static string WorldIdentity(World world)=>"fixture";internal sealed class Prepared {internal Guid Operation,Birth;internal int ImageLength=128;internal bool Current()=>true;internal byte[] Marker(RebirthProductiveEarnedRowProof.Frame f)=>new byte[]{1};}}
// ACTUAL_BUDGET
class Program {
static void Need(bool b,string n){if(!b)throw new Exception(n);Console.WriteLine("PASS "+n);}
static RebirthProductiveEarnedObservationBudget Open(){var p=new RebirthProductiveEarnedArtifact.Prepared{Operation=Guid.NewGuid(),Birth=Guid.NewGuid()};return RebirthProductiveEarnedObservationBudget.TryOpen(p,new RebirthProductiveEarnedRowProof.Frame{Operation=p.Operation,Birth=p.Birth,World="fixture"});}
static void Main(){
Need(RebirthProductiveEarnedObservationBudget.TryMeasureTools(new byte[]{1,1,1,0,9},out long measured,out _)&&measured==224,"actual-array-framing-with-explicit-item-measure-double");
Need(!RebirthProductiveEarnedObservationBudget.TryMeasureTools(new byte[]{1,1,1,0,9,0},out _,out _),"array-trailing-refused");
var a=Open();Need(a!=null,"root-open");var d=RebirthProductiveEarnedObservationBudget.TryEnterDecoder();Need(d!=null&&d.Narrow(4096)&&!d.Narrow(8192),"decoder-narrow-monotone");Need(a.TryObserve(64,32,4)==null,"decoder-excludes-observation");
using(var candidate=RebirthProductiveEarnedObservationBudget.TryEnterCandidateDecoder())Need(candidate!=null,"same-thread-candidate-borrows-decoder");
Need(RebirthProductiveEarnedObservationBudget.TryEnterDecoder()==null,"candidate-borrow-disposed-outer-decoder");
bool refusedForeign=false;var foreignThread=new System.Threading.Thread(()=>{using(var foreign=RebirthProductiveEarnedObservationBudget.TryEnterCandidateDecoder())refusedForeign=foreign==null;});foreignThread.Start();foreignThread.Join();Need(refusedForeign,"foreign-thread-borrowed-candidate-decoder");
d.Dispose();d.Dispose();using(var standaloneCandidate=RebirthProductiveEarnedObservationBudget.TryEnterCandidateDecoder())Need(standaloneCandidate!=null,"candidate-can-open-own-decoder");
var o=a.TryObserve(64,32,4);Need(o!=null,"observation-open");Need(RebirthProductiveEarnedObservationBudget.TryEnterCandidateDecoder()==null,"candidate-borrowed-observation-phase");using(var own=a.TryEnterCallbacks())Need(own!=null,"own-callback-borrows-charged-workspace");var other=Open();Need(other!=null&&other.TryEnterCallbacks()==null,"foreign-callback-cannot-borrow-own-observer");other.Dispose();Need(a.TryReserveAllocation(4096,out var token),"actual-callback-allocation");o.Dispose();Need(RebirthProductiveEarnedObservationBudget.TryEnterDecoder()==null,"closed-watch-retains-phase-until-buffer-release");Need(!a.TryReserveAllocation(1,out _),"closed-watch-no-new-allocation");token.Dispose();token.Dispose();
using(var next=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())Need(next!=null,"last-buffer-releases-phase");
var second=a.TryObserve(64,32,4);Need(second!=null,"second-watch");Need(a.TryReserveAllocation(1024,out var late),"second-watch-buffer");a.Dispose();second.Dispose();Need(RebirthProductiveEarnedObservationBudget.TryEnterDecoder()==null,"closed-root-keeps-live-buffer-charge");late.Dispose();
var roots=new List<RebirthProductiveEarnedObservationBudget>();for(int i=0;i<4;i++)roots.Add(Open());Need(roots.TrueForAll(x=>x!=null)&&Open()==null,"four-original-bound");foreach(var r in roots)r.Dispose();
Need(RebirthProductiveEarnedObservationBudget.TryRetainDetached(RebirthProductiveEarnedObservationBudget.MaximumAllBytes,out var retained),"whole-aggregate-reservation-without-real-allocation");Need(RebirthProductiveEarnedObservationBudget.TryEnterDecoder()==null,"aggregate-pressure-refuses-new-decoder");retained.Dispose();retained.Dispose();
using(var final=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())Need(final!=null,"idempotent-disposal-restores-capacity");
}}

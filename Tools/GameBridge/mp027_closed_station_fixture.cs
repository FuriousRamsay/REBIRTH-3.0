// AUTHORED_NOT_RUN. Entire actual station consumer; native save/owner producers doubled.
using System;
using System.IO;
using System.Text;
using System.Xml.Linq;
// ACTUAL_CLOSED_STATION
class Peer {internal object latestPlayerData=new object();}
class FakeChunk {internal bool InProgressSaving,IsLocked,NeedsDecoration,InProgressDecorating,NeedsLightCalculation,InProgressLighting;}
class FakeStation {
 internal int[] Input,Output,Tools,Queue;internal int Cycles,Resets,Modified;internal bool ThrowCycle;
 internal void SetDisableModifiedCheck(bool value){}
 internal void cycleRecipeQueue(){Cycles++;if(ThrowCycle)throw new InvalidOperationException("actual native failure double");}
 internal void ResetTickTime(){Resets++;}internal void setModified(){Modified++;}
}
static class RebirthWorkstationCompletionPublication {
 internal static bool CanPublish=true,BeforeSaved=true;internal static XElement Last;
 internal static bool MatchesPublishedBytes(string path,byte[] bytes)=>BeforeSaved;
 internal static bool Write(string path,XElement value){if(!CanPublish)return false;Last=new XElement(value);return true;}
 internal static XElement ReadPrimary(string path)=>Last==null?null:new XElement(Last);
}
static class RebirthWorkstationCompletionOutbox {internal static string Root(string purpose)=>Path.GetTempPath();}
static class RebirthProductiveEarnedNativeBody {
 internal static bool Matches=true;
 internal static bool MatchesCurrent(FakeStation station,byte[] expected,int offset)=>Matches;
}
class FakeWatch:IDisposable {public void Dispose(){}}
static class RebirthRemoteResourceNativePublication {
 internal static FakeWatch Begin(object binding,Func<bool> current,string marker)=>new FakeWatch();
}
internal static partial class RebirthRecyclerPlacedSettlement {
 internal class ClosedAuthorityCompletion {
  internal RemoteHostOriginal Attached;internal Guid Ticket=Guid.NewGuid();internal int[] Input={1},Output={2},Tools={3},Queue={4};
 }
 internal class OwnerCheckpoint {
  internal int Phase=1,Completed;internal bool Saved=true;internal WeakReference<object> AcceptedHead;
  internal bool CurrentSaved()=>Saved;internal XElement CopySavedDescriptor()=>new XElement("actualSavedOwnerBefore");
 }
 internal class RemoteHostOriginal {
  internal ClosedAuthorityCompletion ClosedAuthority=new ClosedAuthorityCompletion();internal OwnerCheckpoint OwnerBeforeCheckpoint=new OwnerCheckpoint();
  internal Peer Peer=new Peer();internal FakeStation Station=new FakeStation();internal bool AcceptedStation,UnusedRequested,Admission=true;
  internal Guid Operation=Guid.NewGuid();internal string WorldKey="actual-world";internal byte[] Image=Encoding.UTF8.GetBytes("<placedRecyclerOriginal/>"),AfterNative={2};internal int AfterOffset;
  internal bool HasOwnerAdmission()=>Admission;
 }
 internal sealed partial class StationPublication {
  internal RemoteHostOriginal Original;private FakeChunk chunk=new FakeChunk();
  private bool attempted,unknown,after,chunkOwned;private byte[] beforeReceipt={1};private string beforePath="before";
  private object binding=new object(),cold;private string markerKey="native-marker";private FakeWatch watch;
  private bool Current()=>true; // Explicit native binding/save producer double.
  private void ReleaseChunk(){if(chunkOwned)chunk.InProgressSaving=false;chunkOwned=false;}
  internal static StationPublication New(out RemoteHostOriginal source){
   source=new RemoteHostOriginal();source.ClosedAuthority.Attached=source;source.OwnerBeforeCheckpoint.AcceptedHead=new WeakReference<object>(source.Peer.latestPlayerData);
   RebirthWorkstationCompletionPublication.CanPublish=true;RebirthWorkstationCompletionPublication.BeforeSaved=true;RebirthProductiveEarnedNativeBody.Matches=true;
   return new StationPublication{Original=source};
  }
  internal void Busy()=>chunk.InProgressSaving=true;
  internal bool Unknown=>unknown;internal bool Attempted=>attempted;internal bool After=>after;
 }
}
class Program {
 static void Need(bool value,string message){if(!value)throw new Exception(message);}
 static void Main(){
  var publication=RebirthRecyclerPlacedSettlement.StationPublication.New(out var source);
  source.OwnerBeforeCheckpoint.Saved=false;
  Need(!publication.ApplyClosedAuthorityAfter(source)&&source.Station.Cycles==0,"unsaved owner Before permitted native mutation");
  source.OwnerBeforeCheckpoint.Saved=true;source.Peer.latestPlayerData=new object();
  Need(!publication.ApplyClosedAuthorityAfter(source)&&source.Station.Cycles==0,"foreign accepted owner head admitted");
  publication=RebirthRecyclerPlacedSettlement.StationPublication.New(out source);publication.Busy();
  Need(!publication.ApplyClosedAuthorityAfter(source)&&!publication.Attempted,"busy native chunk consumed action");
  publication=RebirthRecyclerPlacedSettlement.StationPublication.New(out source);RebirthWorkstationCompletionPublication.CanPublish=false;
  Need(!publication.ApplyClosedAuthorityAfter(source)&&!publication.Attempted&&source.Station.Cycles==0,"failed intent publication mutated station");
  RebirthWorkstationCompletionPublication.CanPublish=true;
  Need(publication.ApplyClosedAuthorityAfter(source)&&source.AcceptedStation&&publication.After&&source.Station.Cycles==1&&source.Station.Modified==1,"actual station leg not observed once");
  Need(!publication.ApplyClosedAuthorityAfter(source)&&source.Station.Cycles==1,"station leg replayed");
  publication=RebirthRecyclerPlacedSettlement.StationPublication.New(out source);source.Station.ThrowCycle=true;
  Need(!publication.ApplyClosedAuthorityAfter(source)&&publication.Unknown&&publication.Attempted&&!source.AcceptedStation,"native exception not retained unknown");
  source.Station.ThrowCycle=false;Need(!publication.ApplyClosedAuthorityAfter(source)&&source.Station.Cycles==1,"unknown native attempt replayed");
  publication=RebirthRecyclerPlacedSettlement.StationPublication.New(out source);RebirthProductiveEarnedNativeBody.Matches=false;
  Need(!publication.ApplyClosedAuthorityAfter(source)&&publication.Unknown&&!source.AcceptedStation,"wrong native After accepted");
  Console.WriteLine("PASS actual closed station consumer with explicit save/native doubles; real owner/native save/gameplay NOT RUN");
 }
}

// AUTHORED_NOT_RUN. Extracts the actual automatic checkpoint transition; native
// envelope/auth/read/save boundaries remain separate production requirements.
using System;
using System.Xml.Linq;
static class RebirthProductiveEarnedObservationBudget
{
 internal static bool Available=true;internal static bool TryRetainDetached(long bytes,out IDisposable memory)
 {memory=Available?new Lease():null;return Available;}sealed class Lease:IDisposable{public void Dispose(){}}
}
class PlayerDataFile{}
class Peer{internal PlayerDataFile latestPlayerData;}
class RemoteHostOriginal
{
 internal bool Valid=true,UnusedRequested;internal long AutomaticOwnerSequence;internal Peer Peer=new Peer();
 internal RemoteOwnerCheckpoint OwnerCheckpoint,OwnerBeforeCheckpoint;
 internal bool Current()=>Valid;
 internal bool AutomaticOwnerCurrent()=>Valid;
}
class RemoteOwnerCheckpoint
{
 internal RemoteHostOriginal Source;internal int Phase,Completed,WireLength;internal string WireDigest,FinancialWireDigest;
 internal long AutomaticSequence;internal bool Saved=true,Disposed;
 internal WeakReference<PlayerDataFile> AcceptedHead;
 internal string ReceiptPath="actual-saved-original";internal long ReceiptLength=42;internal string ReceiptDigest="saved-readback";
 private string previousReceipt,previousDigest;private long previousLength;private IDisposable memory;
 internal RemoteOwnerCheckpoint(RemoteHostOriginal source,int phase,int completed,int length,string digest,IDisposable memory)
 {Source=source;Phase=phase;Completed=completed;WireLength=length;WireDigest=digest;FinancialWireDigest=digest;this.memory=memory;Saved=false;}
 internal bool CurrentSaved()=>Saved&&!Disposed;
 internal void Dispose(){Disposed=true;memory?.Dispose();}
 // PRODUCTION_AUTOMATIC
 internal bool LinkedTo(RemoteOwnerCheckpoint previous)=>previousReceipt==previous.ReceiptPath&&previousLength==previous.ReceiptLength&&previousDigest==previous.ReceiptDigest;
}
class Program
{
 static void Check(bool condition,string label){if(!condition)throw new Exception(label);Console.WriteLine("PASS "+label);}
 static RemoteHostOriginal Original()
 {
  var source=new RemoteHostOriginal();var head=new PlayerDataFile();source.Peer.latestPlayerData=head;
  source.OwnerCheckpoint=new RemoteOwnerCheckpoint(source,1,0,70,"financial",null){Saved=true,AcceptedHead=new WeakReference<PlayerDataFile>(head)};
  source.OwnerBeforeCheckpoint=source.OwnerCheckpoint;return source;
 }
 static XElement Header(long sequence=1,int phase=1,int count=0,string basis="financial")=>new XElement("header",new XAttribute("sequence",sequence),new XAttribute("phase",phase),new XAttribute("completed",count),new XAttribute("basis",basis));
 static void Main()
 {
  var source=Original();var old=source.OwnerCheckpoint;
  source.Valid=false;Check(RemoteOwnerCheckpoint.BeginAutomatic(source,Header(),80,"richer")==null&&!old.Disposed,"invalid actual source preserves original");source.Valid=true;
  source.AutomaticOwnerSequence=2;Check(RemoteOwnerCheckpoint.BeginAutomatic(source,Header(2),80,"richer")==null,"old or repeated native sequence refused");source.AutomaticOwnerSequence=0;
  Check(RemoteOwnerCheckpoint.BeginAutomatic(source,Header(1,2,1),80,"richer")==null,"automatic progression cannot advance financial slots");
  Check(RemoteOwnerCheckpoint.BeginAutomatic(source,Header(1,1,0,"foreign"),80,"richer")==null,"different financial original refused");
  var actualHead=source.Peer.latestPlayerData;source.Peer.latestPlayerData=new PlayerDataFile();
  Check(RemoteOwnerCheckpoint.BeginAutomatic(source,Header(),80,"richer")==null&&!old.Disposed,"intervening native owner head refused");source.Peer.latestPlayerData=actualHead;
  RebirthProductiveEarnedObservationBudget.Available=false;
  Check(RemoteOwnerCheckpoint.BeginAutomatic(source,Header(),80,"richer")==null&&ReferenceEquals(source.OwnerCheckpoint,old)&&!old.Disposed,"budget denial preserves saved original");
  RebirthProductiveEarnedObservationBudget.Available=true;
  var next=RemoteOwnerCheckpoint.BeginAutomatic(source,Header(),80,"richer");
  Check(next!=null&&next.AutomaticSequence==1&&next.Phase==1&&next.Completed==0&&next.FinancialWireDigest=="financial"&&next.WireDigest=="richer"&&next.LinkedTo(old),"genuine same-financial transition retains full prior save link");
  Check(old.Disposed&&ReferenceEquals(source.OwnerBeforeCheckpoint,next)&&ReferenceEquals(source.OwnerCheckpoint,next),"new actual save obligation owns head and before lineage");
  Check(RemoteOwnerCheckpoint.BeginAutomatic(source,Header(2),90,"later")==null,"pending native save cannot lend a saved head");
  Check(source.AutomaticOwnerSequence==0,"entering automatic native upload does not publish acknowledgement");
 }
}

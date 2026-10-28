// AUTHORED_NOT_RUN. Full production progress/finish/continuation registration.
// Actual native sync observation, current scope/body and publication are adapters.
using System;using System.Linq;using System.Text;using System.Collections.Generic;
class World{internal bool Remote;internal bool IsRemote()=>Remote;}
class FixtureWindow{internal Action Sync;internal void syncTEfromUI()=>Sync();}
class ConnectionManager{internal bool IsClient,IsServer;internal object[] connectionToServer={new object()};internal void SendToServer(object value){}}
static class SingletonMonoBehaviour<T> where T:new(){internal static T Instance=new T();}
static class RebirthLogisticsOriginalTransport{internal static bool CanSendNative(object value)=>true;}
enum StreamModeWrite{ToServer}
class NetPackageTileEntity{internal NetPackageTileEntity Setup(object station,StreamModeWrite mode,byte handle)=>this;}
static class NetPackageManager{internal static T GetPackage<T>() where T:new()=>new T();}
static class RebirthProductiveEarnedObservationBudget{internal static bool Available=true;internal sealed class Lease:IDisposable{public void Dispose(){}}internal static IDisposable TryEnterDecoder()=>Available?new Lease():null;}
static class RebirthProductiveEarnedNativeBody{internal static bool Matches=true;internal static bool MatchesCurrent(object station,byte[] body,int offset)=>Matches;}
static class RebirthProductiveEarnedUiQueueOwner{internal static int Observations;internal static void ObserveHandoffOwners(object owners){Observations++;}}
static class RebirthProductiveEarnedWire{
 internal enum Status{UiQueueAccepted,UiQueueHeader}internal enum Kind{UiQueueQuery,UiQueueHandoff}
 internal class Reply{internal string Digest,Position,Owner,Creation;internal Guid Birth,Nonce;internal int Deaths;internal Status State;}
 internal static bool TryReadReply(Guid operation,out Reply reply){reply=null;return false;}
 internal static void Send(object actor,object station,Kind kind,Guid operation,Guid nonce,byte[] image,string hash){}
}
static class RebirthProductiveEarnedUiOwnerFile{internal class LocalOwnerTerminalPublication{internal Guid Operation=Guid.NewGuid();}}
internal static partial class RebirthProductiveEarnedUiCoordinator{
 private class Context{internal World World=new World();internal FixtureWindow Window=new FixtureWindow();internal object Queue=new object(),Actor=new object(),Station=new object();internal object RecipientSource;internal RebirthProductiveEarnedUiOwnerFile.LocalOwnerTerminalPublication LocalOwnerSource;internal Guid Operation,Birth;internal string Position,Owner,Creation;internal int Deaths;}
 private class Card{internal object GetRecipe()=>null;internal int GetRecipeCount()=>0;}
 private sealed class QueueHandoff:IDisposable{internal Context Binding=new Context();internal bool Live=true,Attempted,Entered,Observed,EmptyLocal,Submitted,ResponseObserved,NoChange,Closed;internal Guid Nonce;internal double Next;internal string Path="handoff";internal byte[] Image={1},After={2};internal int AfterOffset;internal object Owners=new object(),Connection;internal Card[] Cards=new Card[0];internal bool Current()=>Live;public void Dispose(){Closed=true;}}
 private sealed class LocalQueueContinuation{internal RebirthProductiveEarnedUiOwnerFile.LocalOwnerTerminalPublication Terminal;internal object Owners;internal string Path;internal byte[] Image;internal IDisposable Retention;}
 static Dictionary<Guid,LocalQueueContinuation> localQueueContinuations=new Dictionary<Guid,LocalQueueContinuation>();static Dictionary<object,QueueHandoff> handoffs=new Dictionary<object,QueueHandoff>();
 static QueueHandoff handoffSync;static bool ui=true,writes=true;static int syncs,checks,acceptedWrites,emptyFinishes;
 static bool HandoffUiCurrent(QueueHandoff value)=>ui;static double Monotonic()=>1;static string Hash(byte[] bytes)=>"digest";
 static bool WriteExact(string path,byte[] bytes){if(path.EndsWith(".accepted")){acceptedWrites++;return writes;}return true;}
 static void FinishEmptyLocalQueue(QueueHandoff value){emptyFinishes++;}
 static bool ReleaseEmptyRecipientQueue(QueueHandoff value)=>false;static bool RetainAcceptedRecipientQueue(QueueHandoff value)=>false;static bool TryGetHandoffExplicitHandle(QueueHandoff value,out byte handle){handle=0;return false;}
 // ACTUAL_PROGRESS
 // ACTUAL_FINISH
 // ACTUAL_LOCAL_RETENTION
 static QueueHandoff Seed(bool owner=true){localQueueContinuations.Clear();handoffs.Clear();writes=ui=RebirthProductiveEarnedObservationBudget.Available=RebirthProductiveEarnedNativeBody.Matches=true;syncs=acceptedWrites=emptyFinishes=RebirthProductiveEarnedUiQueueOwner.Observations=0;var value=new QueueHandoff();if(owner)value.Binding.LocalOwnerSource=new RebirthProductiveEarnedUiOwnerFile.LocalOwnerTerminalPublication();value.Binding.Window.Sync=()=>{syncs++;value.Entered=value.Observed=true;};handoffs.Add(value.Binding.Queue,value);return value;}
 static void Need(bool condition){if(!condition)throw new Exception("local handoff finish "+checks);checks++;}
 public static void Main(){
  var value=Seed();writes=false;ProgressQueueHandoff(value);Need(syncs==1&&value.Observed&&handoffs.ContainsKey(value.Binding.Queue)&&localQueueContinuations.Count==0);writes=true;ProgressQueueHandoff(value);Need(syncs==1&&!handoffs.ContainsKey(value.Binding.Queue)&&localQueueContinuations.Count==1&&!value.Closed);Need(localQueueContinuations.Values.Single().Retention==value&&localQueueContinuations.Values.Single().Path=="handoff.accepted");Need(RebirthProductiveEarnedUiQueueOwner.Observations==1);
  value=Seed();for(int i=0;i<4;i++)localQueueContinuations.Add(Guid.NewGuid(),new LocalQueueContinuation());ProgressQueueHandoff(value);Need(syncs==1&&handoffs.ContainsKey(value.Binding.Queue));localQueueContinuations.Clear();ProgressQueueHandoff(value);Need(syncs==1&&localQueueContinuations.Count==1&&!handoffs.ContainsKey(value.Binding.Queue));
  value=Seed(false);writes=false;ProgressQueueHandoff(value);Need(syncs==1&&!value.Closed);writes=true;ProgressQueueHandoff(value);Need(syncs==1&&value.Closed&&handoffs.Count==0);
  value=Seed();value.Attempted=true;ProgressQueueHandoff(value);Need(syncs==0&&acceptedWrites==0&&handoffs.Count==1);
  value=Seed();value.Attempted=value.Entered=value.Observed=true;ui=false;ProgressQueueHandoff(value);Need(syncs==0&&acceptedWrites==0);ui=true;RebirthProductiveEarnedNativeBody.Matches=false;ProgressQueueHandoff(value);Need(syncs==0&&acceptedWrites==0);RebirthProductiveEarnedNativeBody.Matches=true;RebirthProductiveEarnedObservationBudget.Available=false;ProgressQueueHandoff(value);Need(syncs==0&&acceptedWrites==0);RebirthProductiveEarnedObservationBudget.Available=true;ProgressQueueHandoff(value);Need(syncs==0&&handoffs.Count==0&&localQueueContinuations.Count==1);
  value=Seed();value.Attempted=value.Observed=true;FinishObservedLocalQueue(value);Need(acceptedWrites==0&&handoffs.Count==1);
  value=Seed();value.Attempted=value.Entered=value.Observed=true;value.Binding.World.Remote=true;FinishObservedLocalQueue(value);Need(acceptedWrites==0&&handoffs.Count==1);
  value=Seed();value.Attempted=value.Entered=value.Observed=value.EmptyLocal=true;ProgressQueueHandoff(value);Need(emptyFinishes==1&&syncs==0);
  Console.WriteLine("PASS "+checks+" production local queue finish assertions; native producers adapted");
 }
}

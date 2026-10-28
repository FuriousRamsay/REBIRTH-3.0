using System;using System.IO;using System.Collections.Generic;
// Actual server boundary receipt/canonical scope/capture. Successful complete
// detach registration, native world/role and retained destination are adapters.
class World{internal bool Remote;internal bool IsRemote()=>Remote;}
class GameManager{internal static GameManager Instance=new GameManager();internal World World;}
class ConnectionManager{internal bool IsServer=true;}
static class SingletonMonoBehaviour<T> where T:new(){internal static T Instance=new T();}
static class GameIO{internal static string Directory;internal static string GetSaveGameDir()=>Directory;}
static class ThreadManager{internal static bool Main=true;internal static bool IsMainThread()=>Main;}
class RebirthNpcSaveQuarantineDomain{internal bool IsDetached=true;}
static class RebirthNpcSaveQuarantineBundle
{
 private class Retained{internal World Original,Admission;internal string Directory;internal RebirthNpcSaveQuarantineDomain[] Domains;internal bool Detached=true,RollbackIncomplete;}
 private static List<Retained> retained=new List<Retained>();private static ServerBoundary serverBoundary;
 private static bool AdmissionEnabled=true,transferring,held,stageable;
 private static bool HasRetainedOriginal(World world,string directory)=>held||ActualHasRetainedOriginal(world,directory);
 private static bool CanStageRetainedClientDestination(World world,string directory)=>stageable;
 private static bool CanStageRetainedServerDestination(World world,string directory)=>stageable;
 // No committed native namespace receipt is manufactured by this cohort.
 private static bool HasQualifiedIncomingClient(World world,string directory)=>false;
 private static bool HasQualifiedIncomingServer(World world,string directory)=>false;
 // ACTUAL_SERVER_BOUNDARY
 internal static void Run()
 {
  int checks=0;void Check(bool value){checks++;if(!value)throw new Exception("Server boundary assertion "+checks);}
  var outgoing=new World();var incoming=new World();string old=Path.Combine(Path.GetTempPath(),"rebirth-boundary-A"),next=Path.Combine(Path.GetTempPath(),"rebirth-boundary-B");
  Retained Seed()
  {
   var value=new Retained{Original=outgoing,Admission=incoming,Directory=old,Domains=new[]{new RebirthNpcSaveQuarantineDomain(),new RebirthNpcSaveQuarantineDomain()}};
   retained.Clear();retained.Add(value);GameManager.Instance.World=incoming;GameIO.Directory=next;incoming.Remote=false;SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer=true;ThreadManager.Main=AdmissionEnabled=true;transferring=held=false;
   stageable=false;serverBoundary=new ServerBoundary(value,outgoing,old,incoming,next,value.Domains);return value;
  }
  var bundle=Seed();var receipt=serverBoundary;Check(ReferenceEquals(CaptureServerBoundary(outgoing,old,incoming,next),receipt));
  Check(!new ServerBoundary(bundle,outgoing,old,incoming,next,bundle.Domains).Current(outgoing,old,incoming,next));
  bundle.Detached=false;Check(!receipt.Current(outgoing,old,incoming,next));bundle.Detached=true;
  bundle.RollbackIncomplete=true;Check(!receipt.Current(outgoing,old,incoming,next));bundle.RollbackIncomplete=false;
  var originalDomains=bundle.Domains;bundle.Domains=(RebirthNpcSaveQuarantineDomain[])bundle.Domains.Clone();Check(!receipt.Current(outgoing,old,incoming,next));bundle.Domains=originalDomains;
  bundle.Domains[0].IsDetached=false;Check(!receipt.Current(outgoing,old,incoming,next));bundle.Domains[0].IsDetached=true;
  retained.Clear();Check(!receipt.Current(outgoing,old,incoming,next));retained.Add(bundle);
  AdmissionEnabled=false;Check(!receipt.Current(outgoing,old,incoming,next));AdmissionEnabled=true;
  transferring=true;Check(!receipt.Current(outgoing,old,incoming,next));transferring=false;
  ThreadManager.Main=false;Check(!receipt.Current(outgoing,old,incoming,next));ThreadManager.Main=true;
  incoming.Remote=true;Check(!receipt.Current(outgoing,old,incoming,next));incoming.Remote=false;
  SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer=false;Check(!receipt.Current(outgoing,old,incoming,next));SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer=true;
  held=true;Check(CaptureServerBoundary(outgoing,old,incoming,next)==null);held=false;
  Check(!receipt.Current(new World(),old,incoming,next));Check(!receipt.Current(outgoing,old,new World(),next));
  Check(!receipt.Current(outgoing,old+"foreign",incoming,next));Check(!receipt.Current(outgoing,old,incoming,next+"foreign"));
  Check(receipt.Current(outgoing,old+Path.DirectorySeparatorChar,incoming,next+Path.DirectorySeparatorChar));
  GameIO.Directory=next+"foreign";Check(!receipt.Current(outgoing,old,incoming,next));GameIO.Directory=next;
  Check(CanonicalDirectory("relative")==null);
  receipt.Commit(outgoing,old,incoming,next);Check(serverBoundary==null&&!receipt.Current(outgoing,old,incoming,next));
  bundle=Seed();incoming.Remote=true;SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer=false;
  receipt=serverBoundary=new ServerBoundary(bundle,outgoing,old,incoming,next,bundle.Domains);
  Check(receipt.Current(outgoing,old,incoming,next));
  held=stageable=true;Check(receipt.CanInstallIncomingEpoch(outgoing,old,incoming,next)&&!receipt.Current(outgoing,old,incoming,next));
  stageable=false;Check(!receipt.CanInstallIncomingEpoch(outgoing,old,incoming,next));held=false;
  SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer=true;Check(!receipt.Current(outgoing,old,incoming,next));
  Seed();Check(ActualHasRetainedOriginal(new World(),Path.Combine(old,".")));
  Check(ActualHasRetainedOriginal(new World(),old+Path.DirectorySeparatorChar));
  Check(!ActualHasRetainedOriginal(new World(),next));
  Check(ActualHasRetainedOriginal(new World(),"relative"));
  Check(SameDirectory(Path.Combine(Path.GetTempPath(),"CaseSave"),Path.Combine(Path.GetTempPath(),"casesave"))==(Path.DirectorySeparatorChar=='\\'));
  Console.WriteLine(checks+" actual server boundary assertions passed; complete-detach/native lifecycle adapters NOT certified");
 }
}
class Program{static void Main()=>RebirthNpcSaveQuarantineBundle.Run();}

using System;
// Actual boundary receipt and capture. Successful full detach registration and
// retained-destination/native world/thread/path are explicit adapters.
class World{internal bool IsRemote()=>true;}
class GameManager{internal static GameManager Instance=new GameManager();internal World World;}
static class GameIO{internal static string Directory="incoming";internal static string GetSaveGameDir()=>Directory;}
static class ThreadManager{internal static bool Main=true;internal static bool IsMainThread()=>Main;}
static class RebirthNpcSaveQuarantineBundle
{
 private static ClientBoundary clientBoundary;private static bool AdmissionEnabled=true,transferring,held;
 private static bool HasRetainedOriginal(World world,string directory)=>held;
 private static bool IncomingRoleCurrent(World world)=>world!=null;
 private static bool SameDirectory(string left,string right)=>left==right; // Canonical helper has its own actual-source cohort.
 private static bool CanStageRetainedClientDestination(World world,string directory)=>held;
 // Native namespace receipts/server staging are outside this boundary cohort.
 private static bool CanStageRetainedServerDestination(World world,string directory)=>false;
 private static bool HasQualifiedIncomingClient(World world,string directory)=>false;
 private static bool HasQualifiedIncomingServer(World world,string directory)=>false;
 // ACTUAL_CLIENT_BOUNDARY
 internal static void Run()
 {
  int checks=0;void Check(bool value){checks++;if(!value)throw new Exception("Client boundary assertion "+checks);}
  var outgoing=new World();var incoming=new World();GameManager.Instance.World=incoming;
  ClientBoundary Seed(){AdmissionEnabled=true;transferring=held=false;ThreadManager.Main=true;GameIO.Directory="incoming";GameManager.Instance.World=incoming;return clientBoundary=new ClientBoundary(outgoing,"outgoing",incoming,"incoming");}
  var receipt=Seed();Check(ReferenceEquals(CaptureClientBoundary(outgoing,"outgoing",incoming,"incoming"),receipt));
  Check(!new ClientBoundary(outgoing,"outgoing",incoming,"incoming").Current(outgoing,"outgoing",incoming,"incoming"));
  held=true;Check(CaptureClientBoundary(outgoing,"outgoing",incoming,"incoming")==null);held=false;
  AdmissionEnabled=false;Check(!receipt.Current(outgoing,"outgoing",incoming,"incoming"));AdmissionEnabled=true;
  transferring=true;Check(!receipt.Current(outgoing,"outgoing",incoming,"incoming"));transferring=false;
  ThreadManager.Main=false;Check(!receipt.Current(outgoing,"outgoing",incoming,"incoming"));ThreadManager.Main=true;
  Check(!receipt.Current(new World(),"outgoing",incoming,"incoming"));Check(!receipt.Current(outgoing,"foreign",incoming,"incoming"));
  Check(!receipt.Current(outgoing,"outgoing",new World(),"incoming"));Check(!receipt.Current(outgoing,"outgoing",incoming,"foreign"));
  GameIO.Directory="changed";Check(!receipt.Current(outgoing,"outgoing",incoming,"incoming"));GameIO.Directory="incoming";
  held=true;bool refused=false;try{receipt.Commit(outgoing,"outgoing",incoming,"incoming");}catch(InvalidOperationException){refused=true;}Check(refused&&ReferenceEquals(clientBoundary,receipt));held=false;
  receipt.Commit(outgoing,"outgoing",incoming,"incoming");Check(clientBoundary==null&&!receipt.Current(outgoing,"outgoing",incoming,"incoming"));
  Console.WriteLine(checks+" actual client boundary assertions passed; detach registration/native lifecycle adapters NOT certified");
 }
}
class Program{static void Main()=>RebirthNpcSaveQuarantineBundle.Run();}

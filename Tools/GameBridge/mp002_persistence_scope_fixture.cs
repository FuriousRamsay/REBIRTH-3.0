// AUTHORED_NOT_RUN. Entire production scope; native world/path identities doubled.
using System;
using System.IO;
// ACTUAL_PERSISTENCE_SCOPE
class WorldState {internal string Guid="world-a";}
class World {internal bool Remote;internal WorldState worldState=new WorldState();internal bool IsRemote()=>Remote;}
class GameManager {internal static GameManager Instance=new GameManager();internal World World;}
class ConnectionManager {internal bool IsServer=true;}
static class SingletonMonoBehaviour<T>where T:new(){internal static T Instance=new T();}
static class ThreadManager {internal static bool Main=true;internal static bool IsMainThread()=>Main;}
static class GameIO {internal static string Directory=Path.Combine(Path.GetTempPath(),"mp002-scope-a");internal static string GetSaveGameDir()=>Directory;}
class Program {
 static void Need(bool value,string message){if(!value)throw new Exception(message);}
 static void Refuses(Action action,string message){try{action();}catch(InvalidOperationException){return;}throw new Exception(message);}
 static void Main(){
  var a=new World();GameManager.Instance.World=a;RebirthFirePersistenceScope.Reset();
  Refuses(()=>RebirthFirePersistenceScope.CurrentPath(),"unbound state adopted current path");
  RebirthFirePersistenceScope.Bind(a);string original=RebirthFirePersistenceScope.CurrentPath();
  Need(RebirthFirePersistenceScope.IsCurrentWorld(a),"bound native simulation world refused");
  Need(original==Path.Combine(Path.GetFullPath(GameIO.Directory),"RebirthFireManager.dat"),"actual original path not retained");
  RebirthFirePersistenceScope.Bind(a);
  string directory=GameIO.Directory;GameIO.Directory=directory+"-b";
  Refuses(()=>RebirthFirePersistenceScope.CurrentPath(),"old fire state redirected into new directory");
  Refuses(()=>RebirthFirePersistenceScope.Bind(a),"same native world rebound after path change");GameIO.Directory=directory;
  a.worldState.Guid="changed";Refuses(()=>RebirthFirePersistenceScope.CurrentPath(),"same reference adopted changed world GUID");a.worldState.Guid="world-a";
  var b=new World();GameManager.Instance.World=b;
  Need(!RebirthFirePersistenceScope.IsCurrentWorld(a)&&!RebirthFirePersistenceScope.IsCurrentWorld(b),"old simulation state crossed into unexpected current world");
  Refuses(()=>RebirthFirePersistenceScope.CurrentPath(),"different native world adopted original fire state");
  Refuses(()=>RebirthFirePersistenceScope.Bind(b),"different world rebound without lifecycle reset");
  RebirthFirePersistenceScope.Reset();RebirthFirePersistenceScope.Bind(b);
  Need(RebirthFirePersistenceScope.IsCurrentWorld(b)&&!RebirthFirePersistenceScope.IsCurrentWorld(a),"new actual session not distinguished from prior world");
  ThreadManager.Main=false;Refuses(()=>RebirthFirePersistenceScope.CurrentPath(),"off-thread persistence admitted");ThreadManager.Main=true;
  b.Remote=true;Refuses(()=>RebirthFirePersistenceScope.CurrentPath(),"remote world persistence admitted");b.Remote=false;
  SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer=false;
  Refuses(()=>RebirthFirePersistenceScope.CurrentPath(),"client persistence admitted");
  Console.WriteLine("PASS actual fire persistence scope with native identity doubles; actual disk/native lifecycle NOT RUN");
 }
}

// AUTHORED_NOT_RUN. Complete production original-scope witness. Native current
// world/save generation and generation-file absence are explicit adapters.
using System;using System.IO;using System.Security.Cryptography;
class World{internal bool IsRemote()=>false;}
class GameManager{internal static GameManager Instance=new GameManager();internal World World=new World();}
class ConnectionManager{internal bool IsServer=true;}
static class SingletonMonoBehaviour<T> where T:new(){internal static T Instance=new T();}
static class ThreadManager{internal static bool IsMainThread()=>true;}
static class GameIO{internal static string Directory;internal static string GetSaveGameDir()=>Directory;}
static class RebirthNpcRecoveryGeneration{internal static ulong RequireCurrent()=>1;}
class Save{internal string SaveDirectory,Fingerprint="fingerprint";}
static class RebirthNpcSaveScope{internal static Save ObserveCurrent()=>new Save{SaveDirectory=GameIO.Directory};internal static string ComputeFingerprint(string directory)=>"fingerprint";}
static class File{internal static bool Exists(string path)=>false;}
static class Directory{internal static bool Exists(string path)=>false;internal static string[] EnumerateFiles(string path,string pattern)=>new string[0];}
// ACTUAL_ORIGINAL_SCOPE
class Program{
 static int checks;static void Need(bool value){if(!value)throw new Exception("Original scope "+checks);checks++;}
 static void Main(){
  string root=Path.GetPathRoot(Path.GetTempPath());GameIO.Directory=root;var world=GameManager.Instance.World;
  var scope=RebirthNpcSaveQuarantineScope.CaptureCurrent();Need(scope.Directory==root&&scope.IsExactCurrentWorld);
  Need(scope.MatchesBinding(world,Path.Combine(root,"npc.xml"),true));
  GameIO.Directory=Path.Combine(Path.GetTempPath(),"ScopeA");scope=RebirthNpcSaveQuarantineScope.CaptureCurrent();
  GameIO.Directory=Path.Combine(GameIO.Directory,".")+Path.DirectorySeparatorChar;Need(scope.IsExactCurrentWorld);
  Need(scope.MatchesBinding(world,Path.Combine(GameIO.Directory,"npc.xml"),true));
  Need(!scope.MatchesBinding(new World(),Path.Combine(GameIO.Directory,"npc.xml"),true));
  Need(!scope.MatchesBinding(world,Path.Combine(Path.GetTempPath(),"ScopeB","npc.xml"),true));
  GameIO.Directory=Path.Combine(Path.GetTempPath(),"scopea");Need(scope.IsExactCurrentWorld==(Path.DirectorySeparatorChar=='\\'));
  Need(scope.MatchesBinding(null,null,false));Need(!scope.MatchesBinding(null,null,true));
  Need(RebirthNpcSaveQuarantineScope.SameDirectory(root,Path.Combine(root,".")));
  Need(!RebirthNpcSaveQuarantineScope.SameDirectory("relative","relative"));
  Console.WriteLine("PASS "+checks+" actual original-scope assertions; native world/generation adapters excluded");
 }
}

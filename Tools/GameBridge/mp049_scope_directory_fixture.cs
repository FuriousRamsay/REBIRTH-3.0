using System;
using System.IO;
// AUTHORED_NOT_RUN. Actual production path qualifier with explicit native adapters.
internal sealed class World {}
internal static class ThreadManager {internal static bool Main=true;internal static bool IsMainThread()=>Main;}
internal sealed class GameManager {internal static GameManager Instance=new GameManager();internal World World;}
internal static class GameIO {
 internal static int Reads;internal static string Directory;internal static Action DuringRead;
 internal static string GetSaveGameDir(){Reads++;DuringRead?.Invoke();return Directory;}
}
internal static class Fixture {
// ACTUAL_SCOPE_DIRECTORY
 private static void Need(bool value,string message){if(!value)throw new Exception(message);}
 private static void Refuse(World world,string label){bool refused=false;try{RequireCurrentWorldDirectory(world);}catch(InvalidOperationException){refused=true;}Need(refused,label);}
 public static void Main(){
  var world=new World();GameManager.Instance.World=world;GameIO.Directory=Path.GetFullPath("native-save");
  Refuse(null,"null native World accepted");Refuse(new World(),"stale native World accepted");
  ThreadManager.Main=false;Refuse(world,"worker admitted");Need(GameIO.Reads==0,"invalid actor reached native save lookup");ThreadManager.Main=true;
  Need(RequireCurrentWorldDirectory(world)==GameIO.Directory,"positive current directory lost");
  foreach(var missing in new string[]{null,"","  "}){GameIO.Directory=missing;Refuse(world,"unknown path admitted");}
  GameIO.Directory="bad\0path";Refuse(world,"invalid path admitted");
  GameIO.Directory=Path.GetFullPath("native-save");GameIO.DuringRead=()=>{throw new IOException("native read failed");};Refuse(world,"failed lookup admitted");
  GameIO.DuringRead=()=>GameManager.Instance.World=new World();Refuse(world,"World replacement inside lookup admitted");
  GameManager.Instance.World=world;GameIO.DuringRead=()=>ThreadManager.Main=false;Refuse(world,"thread qualification lost inside lookup");
  Console.WriteLine("PASS actual NPC directory qualifier; native lifecycle and all-domain reentry NOT RUN");
 }
}

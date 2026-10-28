using System;
// AUTHORED_NOT_RUN. Actual repair world reset; runtime/cache/save-path adapters.
internal sealed class World {}
internal static class ThreadManager {internal static bool Main=true;internal static bool IsMainThread()=>Main;}
internal sealed class GameManager {internal static GameManager Instance=new GameManager();internal World World;}
internal static class GameIO {internal static string Directory="current-save";internal static int Reads;internal static string GetSaveGameDir(){Reads++;return Directory;}}
internal sealed class Cache {internal int Count=1,Clears;internal void Clear(){Count=0;Clears++;}}
internal static class RebirthRepairOriginalCorrection {
 private static World activeWorld;private static string activeDirectory;private static object activation;
 private static Cache Cards,Staged,PreparedRemote,Clients,Uploads,ownedCandidates;private static float nextScan;
// ACTUAL_WORLD_SCOPE
 private static void Reset(World world){ThreadManager.Main=true;GameManager.Instance.World=activeWorld=world;GameIO.Directory=activeDirectory="current-save";GameIO.Reads=0;activation=new object();nextScan=9;
  Cards=new Cache();Staged=new Cache();PreparedRemote=new Cache();Clients=new Cache();Uploads=new Cache();ownedCandidates=new Cache();}
 internal static void Exercise(){
  var world=new World();Reset(world);WorldScope(new World());Need(GameIO.Reads==0&&Clients.Count==1&&activation!=null&&nextScan==9&&ReferenceEquals(activeWorld,world),"stale world cleared repair state");
  ThreadManager.Main=false;WorldScope(world);Need(GameIO.Reads==0&&Cards.Count==1,"off-thread callback changed scope");ThreadManager.Main=true;
  WorldScope(world);Need(GameIO.Reads==1&&Cards.Clears==0&&nextScan==9,"unchanged scope reset live originals");
  GameIO.Directory="new-save";WorldScope(world);Need(activeDirectory=="new-save"&&activation==null&&nextScan==0&&new[]{Cards,Staged,PreparedRemote,Clients,Uploads,ownedCandidates}.All(c=>c.Clears==1),"actual save transition did not retire transient state");
  Reset(world);var next=new World();GameManager.Instance.World=next;WorldScope(next);Need(ReferenceEquals(activeWorld,next)&&Clients.Clears==1&&activation==null,"actual world transition not installed");
  Console.WriteLine("PASS actual repair world-scope reset boundaries; native journal preservation and real world lifecycle NOT RUN");
 }
 private static void Need(bool value,string message){if(!value)throw new Exception(message);}
}
internal static class Program {public static void Main()=>RebirthRepairOriginalCorrection.Exercise();}

using System;
// AUTHORED_NOT_RUN. Actual shared service authority predicate, native adapters.
internal static class ThreadManager {internal static bool Main=true;internal static bool IsMainThread()=>Main;}
internal sealed class ConnectionManager {internal static ConnectionManager Instance;internal bool IsServer;}
internal sealed class GameManager {internal static GameManager Instance;internal World World;}
internal sealed class World {internal bool Remote;internal bool IsRemote()=>Remote;}
internal static class Program {
// ACTUAL_WORLD_AUTHORITY
 private static void Need(bool value,string message){if(!value)throw new Exception(message);}
 public static void Main(){
  for(int main=0;main<2;main++)for(int manager=0;manager<3;manager++)for(int world=0;world<4;world++){
   ThreadManager.Main=main==1;
   ConnectionManager.Instance=manager==0?null:new ConnectionManager{IsServer=manager==2};
   GameManager.Instance=world==0?null:new GameManager{World=world==1?null:new World{Remote=world==3}};
   Need(IsServer()==(main==1&&manager==2&&world==2),"native authority matrix mismatch");
  }
  Console.WriteLine("PASS actual world integration authority matrix; native topology/transport NOT RUN");
 }
}

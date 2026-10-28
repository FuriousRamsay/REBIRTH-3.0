using System;
static class Program {
 static World world;static ConnectionManager connection;
 static void Reset(){ThreadManager.Main=true;world=new World();connection=new ConnectionManager();SingletonMonoBehaviour<ConnectionManager>.Instance=connection;GameManager.Instance=new GameManager{World=world};RebirthTraderQuestGraceManager.Calls=RebirthWorkstationSecurityService.Calls=0;}
 static void Both(World incoming){new NetPackageRebirthWorkstationOwnerSync().Setup(default,"owner",true,"public",new string[0]).ProcessPackage(incoming,GameManager.Instance);new NetPackageRebirthQuestGraceSync().Setup(7,"quest",123,true).ProcessPackage(incoming,GameManager.Instance);}
 static void Check(int calls,string message){if(RebirthTraderQuestGraceManager.Calls!=calls||RebirthWorkstationSecurityService.Calls!=calls)throw new Exception(message);}
 static void Main(){
  Reset();Both(world);Check(1,"current client without actor prerequisite");
  if(new NetPackageRebirthWorkstationOwnerSync().PackageDirection!=NetPackageDirection.ToClient||new NetPackageRebirthQuestGraceSync().PackageDirection!=NetPackageDirection.ToClient)throw new Exception("actual ToClient declaration");
  Reset();Both(new World());Check(0,"old remote world");
  Reset();ThreadManager.Main=false;Both(world);Check(0,"off thread");
  Reset();world.Remote=false;Both(world);Check(0,"authoritative world");
  Reset();connection.IsServer=true;Both(world);Check(0,"server role with remote-shaped world");
  Reset();SingletonMonoBehaviour<ConnectionManager>.Instance=null;Both(world);Check(0,"missing transport");
  Reset();Both(null);Check(0,"missing world");
  Reset();GameManager.Instance=null;Both(world);Check(0,"missing current manager");
  Console.WriteLine("PASS actual client projection packet admission: current/no-actor delegates; stale world/thread/role/missing context refuses. Native downstream/wire remain doubles.");
 }
}

using System;
static class Program {
 static World world;static ConnectionManager connection;static ClientInfo sender;
 static void Reset(){ThreadManager.Main=true;world=new World();world.Player=new EntityPlayer{world=world};GameManager.Instance=new GameManager{World=world};sender=new ClientInfo{entityId=1};connection=new ConnectionManager();connection.Clients.Current=sender;SingletonMonoBehaviour<ConnectionManager>.Instance=connection;RebirthTraderQuestGraceManager.Parties=RebirthTraderQuestGraceManager.Updates=0;RebirthTraderQuestGraceManager.LastPlayer=null;}
 static void Both(World incoming=null){var party=new NetPackageRebirthQuestGracePartyComplete().Setup(12,"quest");party.Sender=sender;party.ProcessPackage(incoming??world,GameManager.Instance);var update=new NetPackageRebirthQuestGraceUpdate().Setup(12,"quest","native",123,RebirthTraderQuestGraceReason.Death,false);update.Sender=sender;update.ProcessPackage(incoming??world,GameManager.Instance);}
 static void Check(int parties,int updates,string label){if(RebirthTraderQuestGraceManager.Parties!=parties||RebirthTraderQuestGraceManager.Updates!=updates)throw new Exception(label);}
 static void Main(){
  Reset();Both();Check(1,1,"current admission");
  Reset();world.Player.Dead=true;Both();Check(1,1,"legitimate dead quest holder");
  Reset();world.Player=null;Both();Check(0,1,"offline-record manager path preserved");
  Reset();connection.Clients.Current=new ClientInfo{entityId=1};Both();Check(0,0,"stale same-ID connection");
  Reset();Both(new World());Check(0,0,"old world packet");
  Reset();ThreadManager.Main=false;Both();Check(0,0,"off thread");
  Reset();world.Remote=true;Both();Check(0,0,"remote world");
  Reset();connection.IsServer=false;Both();Check(0,0,"client role");
  Reset();world.Player.world=new World();Both();Check(0,0,"foreign actor world");
  Reset();connection.Clients=null;Both();Check(0,0,"missing native registry");
  Reset();sender=null;Both();Check(0,0,"missing sender");
  Reset();world.Player.FinalPhase=false;Both();Check(0,1,"party authorization stays downstream");
  Console.WriteLine("PASS: actual packet guards, stale connection/world/thread refusals and dead/offline delegation. Native manager/persistence remain boundary doubles.");
 }
}

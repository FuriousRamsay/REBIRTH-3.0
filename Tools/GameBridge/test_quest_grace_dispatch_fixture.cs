using System;
using System.Collections.Generic;
public class NetPackage { }
public class NetPackagePlayerData:NetPackage {public NetPackagePlayerData Setup(EntityPlayerLocal p){return this;}}
public class NetPackageRebirthQuestGraceUpdate:NetPackage {public NetPackageRebirthQuestGraceUpdate Setup(int c,string id,string poi,long d,RebirthTraderQuestGraceReason r,bool clear){return this;}}
public static class NetPackageManager {public static T GetPackage<T>() where T:new(){return new T();}}
public enum RebirthTraderQuestGraceReason {LeftArea}
public class ConnectionManager {public bool IsServer;public List<NetPackage> sent=new List<NetPackage>();public void SendToServer(NetPackage p){sent.Add(p);}}
public static class SingletonMonoBehaviour<T>{public static T Instance;}
public static class LogisticsTransferService {public static int reject;public static bool ClientChannelReady(ConnectionManager c,NetPackage p){return !(reject==1&&p is NetPackagePlayerData)&&!(reject==2&&p is NetPackageRebirthQuestGraceUpdate);}}
public class World {public bool remote=true;public EntityPlayerLocal player;public bool IsRemote(){return remote;}public EntityPlayerLocal GetPrimaryPlayer(){return player;}}
public class GameManager {public static GameManager Instance;public World World;}
public class EntityPlayerLocal {public World world;public QuestJournal QuestJournal;}
public class QuestJournal {public EntityPlayerLocal OwnerPlayer;}
public class Quest {public QuestJournal OwnerJournal;public int QuestCode;public string ID;}
public static class Service {static string GetPoiReservationKey(Quest q){return "poi";}
// METHODS
}
public static class Checks {public static void Main(){
 for(int mode=0;mode<10;mode++){
 var w=new World();var p=new EntityPlayerLocal{world=w};w.player=p;var j=new QuestJournal{OwnerPlayer=p};p.QuestJournal=j;var q=new Quest{OwnerJournal=j};var c=new ConnectionManager();SingletonMonoBehaviour<ConnectionManager>.Instance=c;GameManager.Instance=new GameManager{World=w};LogisticsTransferService.reject=0;
 switch(mode){case 1:c.IsServer=true;break;case 2:w.remote=false;break;case 3:j.OwnerPlayer=null;break;case 4:p.world=new World();break;case 5:w.player=new EntityPlayerLocal();break;case 6:p.QuestJournal=new QuestJournal();break;case 7:LogisticsTransferService.reject=1;break;case 8:LogisticsTransferService.reject=2;break;case 9:q=null;break;}
 Service.SendGraceUpdateToServer(q,123,RebirthTraderQuestGraceReason.LeftArea,false);
 if(mode==0){if(c.sent.Count!=2||!(c.sent[0] is NetPackagePlayerData)||!(c.sent[1] is NetPackageRebirthQuestGraceUpdate))throw new Exception("snapshot order");}
 else if(c.sent.Count!=0)throw new Exception("guard "+mode);
 }
 Console.WriteLine("PASS: actual grace dispatch sends snapshot before transition; nine invalid contexts send neither packet; native transport stubbed");
}}

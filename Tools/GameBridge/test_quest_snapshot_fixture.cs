using System;
using System.Collections.Generic;
public class ConnectionManager { public bool IsServer=true; }
public static class SingletonMonoBehaviour<T> { public static T Instance; }
public class ClientInfo { public int entityId=1; }
public class PlayerDataFile { public int id=1; public QuestJournal questJournal; }
public class EntityPlayer { public QuestJournal QuestJournal; }
public class QuestJournal { public List<Quest> quests=new List<Quest>(); }
public class Quest { }
public class World { public bool remote; public EntityPlayer player; public bool IsRemote(){return remote;} public object GetEntity(int id){return id==1?player:null;} }
public class GameManager { public World World; }
public static class RebirthTraderQuestGraceManager {
 public static bool Enabled=true;
 public static List<Quest> observed=new List<Quest>();
 public static void NotifyPartySiteCompleted(Quest quest){observed.Add(quest);}
}
public static class Hook {
// METHODS
}
public static class Checks {
 public static void Main(){
 for(int mode=0;mode<13;mode++){
  var journal=new QuestJournal();var a=new Quest();var b=new Quest();journal.quests.Add(a);journal.quests.Add(b);
  var world=new World{player=new EntityPlayer{QuestJournal=journal}};
  var gm=new GameManager{World=world};var sender=new ClientInfo();var data=new PlayerDataFile{questJournal=journal};
  SingletonMonoBehaviour<ConnectionManager>.Instance=new ConnectionManager();
  RebirthTraderQuestGraceManager.Enabled=true;RebirthTraderQuestGraceManager.observed.Clear();
  switch(mode){case 1:SingletonMonoBehaviour<ConnectionManager>.Instance=null;break;case 2:SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer=false;break;case 3:gm=null;break;case 4:gm.World=null;break;case 5:world.remote=true;break;case 6:RebirthTraderQuestGraceManager.Enabled=false;break;case 7:sender=null;break;case 8:data=null;break;case 9:data.id=2;break;case 10:world.player=null;break;case 11:data.questJournal=new QuestJournal();break;case 12:journal.quests=null;break;}
  Hook.Postfix(gm,sender,data);
  var seen=RebirthTraderQuestGraceManager.observed;
  if(mode==0){if(seen.Count!=2||!ReferenceEquals(seen[0],a)||!ReferenceEquals(seen[1],b))throw new Exception("accepted journal routing");}
  else if(seen.Count!=0)throw new Exception("guard "+mode);
 }
 Console.WriteLine("PASS: actual native snapshot hook, accepted journal routing and 12 rejected contexts; completion service stubbed");
 }
}

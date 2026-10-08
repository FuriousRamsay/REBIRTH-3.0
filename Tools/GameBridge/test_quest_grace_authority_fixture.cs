using System;
using System.Globalization;
using System.Collections.Generic;
public class ConnectionManager {public bool IsServer=true;}
public static class SingletonMonoBehaviour<T> {public static T Instance;}
public class Identity {public string CombinedString="owner";}
public class ClientInfo {public int entityId=1;public Identity InternalId=new Identity();}
public class World {public bool remote;public EntityPlayer player;public bool IsRemote(){return remote;}public object GetEntity(int id){return id==1?player:null;}}
public class GameManager {public static GameManager Instance;public World World;}
public class EntityPlayer {public QuestJournal QuestJournal=new QuestJournal();}
public class QuestJournal {public List<Quest> quests=new List<Quest>();}
public class Quest {public int QuestCode=7;public string ID="job",poi="1,2";public bool eligible=true;public Dictionary<string,string> DataVariables=new Dictionary<string,string>();}
public enum RebirthTraderQuestGraceReason {LeftArea,Death,Disconnect}
public class RebirthTraderOfflineGraceRecord {public string PlayerId,QuestId,PoiReservationKey;public int QuestCode;public long DeadlineUtcTicks;public bool PartySiteCompleted;}
public static class Service {
 public static HashSet<string> PartyCompletionSignals=new HashSet<string>();
 public static int Saves; public static bool Enabled=true,offlineDirty;public const int GraceSeconds=60;public const string DeadlineKey="deadline",ReasonKey="reason";public static object Sync=new object();public static List<RebirthTraderOfflineGraceRecord> Offline=new List<RebirthTraderOfflineGraceRecord>();
 static void EnsureOfflineLoaded(){}static void PruneOfflineLocked(){}static void SaveOfflineLocked(){Saves++;}static void ObserveQuestMutation(Quest q){}
 static Quest FindQuestByCode(QuestJournal j,int code){return j==null?null:j.quests.Find(q=>q.QuestCode==code);}
 static bool IsEligibleQuest(Quest q){return q.eligible;}static bool IsSiteActive(Quest q){return q.eligible;}
 static RebirthTraderOfflineGraceRecord FindOfflineLocked(string p,int c,string id){return Offline.Find(r=>r.PlayerId==p&&r.QuestCode==c&&r.QuestId==id);}
 static bool TryGetDeadline(Quest q,out long d){string s;d=0;return q.DataVariables.TryGetValue(DeadlineKey,out s)&&long.TryParse(s,out d);}
 static void SetQuestGraceData(Quest q,long d,RebirthTraderQuestGraceReason r){q.DataVariables[DeadlineKey]=d.ToString();q.DataVariables[ReasonKey]=r.ToString();}
 static string GetPoiReservationKey(Quest q){return q.poi;}
// METHODS
}
public static class Checks {
 static ClientInfo sender;static EntityPlayer player;static Quest quest;static World world;
 static void Reset(){Service.Saves=0;Service.Offline.Clear();Service.PartyCompletionSignals.Clear();Service.Enabled=true;Service.offlineDirty=false;sender=new ClientInfo();player=new EntityPlayer();quest=new Quest();player.QuestJournal.quests.Add(quest);world=new World{player=player};GameManager.Instance=new GameManager{World=world};SingletonMonoBehaviour<ConnectionManager>.Instance=new ConnectionManager();}
 static void Send(bool clear=false,RebirthTraderQuestGraceReason reason=RebirthTraderQuestGraceReason.LeftArea){Service.ApplyServerGraceUpdate(sender,player,7,"job","spoofed",long.MaxValue,reason,clear);}
 static void Check(bool v,string m){if(!v)throw new Exception(m);}
 static long Live(){return long.Parse(quest.DataVariables[Service.DeadlineKey]);}
 static void Record(long deadline){Service.Offline.Add(new RebirthTraderOfflineGraceRecord{PlayerId="owner",QuestCode=7,QuestId="job",PoiReservationKey="stored",DeadlineUtcTicks=deadline});}
 public static void Main(){
 Reset();Send();long first=Live();Check(first<DateTime.UtcNow.AddSeconds(61).Ticks&&Service.Offline[0].DeadlineUtcTicks==first&&Service.Offline[0].PoiReservationKey=="1,2","server policy/poi");Send();Check(Live()==first&&Service.Offline[0].DeadlineUtcTicks==first,"repeat extension");
 Reset();long early=DateTime.UtcNow.AddSeconds(-10).Ticks;Record(early);Send();Check(Live()==early&&Service.Offline[0].DeadlineUtcTicks==early,"earlier offline");
 Reset();quest.DataVariables[Service.DeadlineKey]=early.ToString();Record(DateTime.UtcNow.AddSeconds(30).Ticks);Send();Check(Live()==early&&Service.Offline[0].DeadlineUtcTicks==early,"earlier live");
 Reset();Record(early);quest.ID="different";Send();Check(quest.DataVariables.Count==0&&Service.Offline[0].PoiReservationKey=="stored","wrong live quest mutation/poi");Send(true);Check(quest.DataVariables.Count==0&&Service.Offline.Count==0,"wrong live clear");
 Reset();quest.poi="";Record(early);Send();Check(Service.Offline[0].PoiReservationKey=="stored","missing authoritative poi");
 Reset();Send();Send(true);Check(Service.Offline.Count==0&&quest.DataVariables.Count==0,"clear");Send();Check(Service.Offline.Count==1,"new grace");Service.Enabled=false;Send();Check(Service.Offline.Count==0&&quest.DataVariables.Count==0,"disabled clear");
 Reset();Send(false,(RebirthTraderQuestGraceReason)99);Check(Service.Offline.Count==0&&quest.DataVariables.Count==0,"invalid reason");world.remote=true;Send();Check(Service.Offline.Count==0,"remote world");world.remote=false;SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer=false;Send();Check(Service.Offline.Count==0,"client role");
 Reset();world.player=new EntityPlayer();Send();Check(Service.Offline.Count==0,"wrong sender entity");
 Reset();Record(early);player=null;Send();Check(Service.Offline[0].DeadlineUtcTicks==early&&Service.Offline[0].PoiReservationKey=="stored","mirror-only");
 Reset();Record(early);quest.DataVariables[Service.DeadlineKey]=DateTime.UtcNow.AddSeconds(30).Ticks.ToString();Service.CaptureDisconnect(sender);Check(Live()==early&&Service.Offline[0].DeadlineUtcTicks==early,"disconnect consistency");
 Reset();Service.PartyCompletionSignals.Add("7|job");Send();Check(Service.Offline[0].PartySiteCompleted,"grace inherits known completion");
 Reset();Service.PartyCompletionSignals.Add("7|job");Service.CaptureDisconnect(sender);Check(Service.Offline[0].PartySiteCompleted,"disconnect inherits known completion");
 Reset();Service.PartyCompletionSignals.Add("7|different");Send();Check(!Service.Offline[0].PartySiteCompleted,"different quest completion isolation");
 Reset();Service.MarkOfflinePartySiteCompleted(7,"job");Check(Service.PartyCompletionSignals.Contains("7|job")&&Service.Saves==0,"completion retained without holders");Send();Check(Service.Offline[0].PartySiteCompleted,"late holder inherits actual completion signal");
 Reset();Record(early);Service.Offline.Add(new RebirthTraderOfflineGraceRecord{PlayerId="other",QuestCode=7,QuestId="other",DeadlineUtcTicks=early});Service.MarkOfflinePartySiteCompleted(7,"job");Check(Service.Offline[0].PartySiteCompleted&&!Service.Offline[1].PartySiteCompleted&&Service.Saves==1,"exact completion and persistence");Service.MarkOfflinePartySiteCompleted(7,"job");Check(Service.Saves==1,"repeated completion does not rewrite");
 Reset();SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer=false;Service.MarkOfflinePartySiteCompleted(7,"job");Check(Service.PartyCompletionSignals.Count==0&&Service.Saves==0,"client cannot mark completion");
 Console.WriteLine("PASS: quest grace authority, consistent deadlines, POI identity, mismatched quest, clear/restart and disconnect");
 }
}
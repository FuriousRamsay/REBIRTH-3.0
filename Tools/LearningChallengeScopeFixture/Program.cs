using System;
using System.Collections.Generic;
namespace UnityEngine {public static class Time {public static float realtimeSinceStartup;}}
public class RebirthSurvivorOwnerSkillSnapshot {public string Id;public float Value,Progress;}
public class RebirthSurvivorOwnerSkillKnowledgeSnapshot {public string Id;public float Value;}
public class RebirthSurvivorOwnerStateSnapshot {
 public bool RebirthModeEnabled=true,HasCharacter=true,DefinitionsCompatible=true;public string CreationId;
 public List<RebirthSurvivorOwnerSkillSnapshot> Skills=new();public List<RebirthSurvivorOwnerSkillKnowledgeSnapshot> SkillKnowledge=new();public List<string> KnowledgeIds=new();
}
public class QuestEventManager {public static QuestEventManager Current=new();public List<string> Events=new();public Action Callback;public void ChallengeAwardCredited(string s,int n){for(int i=0;i<n;i++)Events.Add(s);Callback?.Invoke();}}
public static class RebirthLiteratureService {public static bool IsInternalReadMarker(string s)=>s.StartsWith("internal");}
public static class RebirthLearningReadEvidence {public static bool IsTutorialMarker(string s)=>false;public static void CreditVerified(HashSet<string> p,HashSet<string> c,Func<string,bool> credit){}}
public class GameManager {public static GameManager Instance=new();public World World=new();}
public class World {public Player GetPrimaryPlayer()=>new Player();}
public class Player {public Journal challengeJournal;}
public class Journal {public Dictionary<string,Challenges.Challenge> ChallengeDictionary=new();}
namespace Challenges {public class Challenge {public List<object> ObjectiveList=new();}public class ChallengeObjectiveChallengeStatAwarded {public string challengeStat;public bool Complete;public float Current,MaxCount;}}
public class RebirthMetabolismSnapshot {public string CreationId;public bool AutoSipEnabled;public float NutritionGainPointsPerRealMinute,HydrationGainPointsPerRealMinute,HydrationSlotVolumeMl,GastricFluidTransferMlPerRealMinute,GastricSolidTransferMlPerRealMinute,Energy,EnergyRecoveryPerRealMinute;}
public static class RebirthMetabolismClientState {public static RebirthMetabolismSnapshot Value;public static bool TryGet(out RebirthMetabolismSnapshot s){s=Value;return s!=null;}}
public static class RebirthSurvivorClientState {public static string Creation;public static string GetProjectedCreationId(Player p)=>Creation;}
public static class RebirthSurvivorRequestScope {public static bool Matches(string a,string b)=>!string.IsNullOrEmpty(a)&&a==b;}
class Program {
 static int passed;
 static void Check(bool ok,string why){if(!ok)throw new Exception(why);passed++;QuestEventManager.Current.Events.Clear();}
 static RebirthSurvivorOwnerStateSnapshot Snapshot(string creation,float value,string knowledge){
  RebirthSurvivorClientState.Creation=creation;return new RebirthSurvivorOwnerStateSnapshot{CreationId=creation,Skills=new(){new(){Id="skill.teaching",Value=value}},SkillKnowledge=new(){new(){Id="skill.teaching",Value=value}},KnowledgeIds=new(){knowledge}};
 }
 static int Count=>QuestEventManager.Current.Events.Count;
 static void Main(){
  RebirthLearningChallenges.Reset();RebirthLearningChallenges.Observe(Snapshot("A",1,"known.a"));Check(Count==0,"starter snapshot establishes baseline");
  RebirthLearningChallenges.Observe(Snapshot("A",2,"known.b"));Check(Count==5,"same character actual skill theory knowledge gains credit");
  RebirthLearningChallenges.Observe(Snapshot("B",50,"known.c"));Check(Count==0,"new character high starter values do not credit");
  RebirthLearningChallenges.Observe(Snapshot("B",51,"known.d"));Check(Count==5,"new character subsequent gains credit");
  RebirthLearningChallenges.Observe(Snapshot("A",60,"known.a"));Check(Count==0,"switch back establishes new baseline");
  RebirthLearningChallenges.Observe(Snapshot(null,99,"unknown"));Check(Count==0,"missing character scope refused");
  RebirthLearningChallenges.Observe(Snapshot("A",70,"known.a"));Check(Count==0,"after invalid scope next snapshot is baseline");
  var incompatible=Snapshot("A",80,"known.e");incompatible.DefinitionsCompatible=false;RebirthLearningChallenges.Observe(incompatible);Check(Count==0,"incompatible definitions reset");
  RebirthLearningChallenges.Observe(Snapshot("A",90,"known.e"));Check(Count==0,"post-reset starter state does not credit");
  RebirthLearningChallenges.Reset();RebirthLearningChallenges.Observe(Snapshot("C",1,"known.c"));
  var manager=QuestEventManager.Current;QuestEventManager.Current=null;
  RebirthLearningChallenges.Observe(Snapshot("C",2,"known.d"));RebirthLearningChallenges.Observe(Snapshot("C",3,"known.e"));
  QuestEventManager.Current=manager;Check(Count==0,"absent event manager loses no immediate callback");
  RebirthLearningChallenges.Tick();Check(Count==10,"retained event counts delivered once after manager returns");
  RebirthLearningChallenges.Tick();Check(Count==0,"repeated tick cannot replay delivered counts");
  QuestEventManager.Current=null;RebirthLearningChallenges.Observe(Snapshot("C",4,"known.f"));
  RebirthLearningChallenges.Observe(Snapshot("D",90,"known.g"));QuestEventManager.Current=manager;
  RebirthLearningChallenges.Tick();Check(Count==0,"old character pending counts discarded on switch");
  RebirthLearningChallenges.Reset();RebirthLearningChallenges.Observe(Snapshot("E",1,"known.e"));
  manager.Callback=()=>{manager.Callback=null;throw new Exception("after native effect");};
  try{RebirthLearningChallenges.Observe(Snapshot("E",2,"known.f"));throw new Exception("Expected callback failure");}
  catch(Exception error){if(error.Message!="after native effect")throw;}
  Check(Count==1,"uncertain callback dispatched only once");
  RebirthLearningChallenges.Tick();Check(Count==4,"remaining accepted counts survive callback failure");
  RebirthLearningChallenges.Observe(Snapshot("E",2,"known.f"));Check(Count==0,"identical snapshot cannot recreate failed callback credit");
  manager.Callback=()=>{manager.Callback=null;RebirthLearningChallenges.Observe(Snapshot("F",90,"known.g"));};
  RebirthLearningChallenges.Observe(Snapshot("E",3,"known.h"));Check(Count==1,"reentrant character switch stops old event batch");
  RebirthLearningChallenges.Observe(Snapshot("F",91,"known.i"));Check(Count==5,"new character baseline preserved after callback switch");
  RebirthLearningChallenges.Reset();RebirthLearningChallenges.Observe(Snapshot("G",1,"known.g"));
  QuestEventManager.Current=null;RebirthLearningChallenges.Observe(Snapshot("G",2,"known.h"));QuestEventManager.Current=manager;
  RebirthSurvivorClientState.Creation="H";RebirthLearningChallenges.Tick();Check(Count==0,"new projected owner refuses old pending counts before Observe");
  RebirthLearningChallenges.Observe(Snapshot("H",90,"known.i"));RebirthLearningChallenges.Tick();Check(Count==0,"switch reset cannot replay pending counts");
  UnityEngine.Time.realtimeSinceStartup+=2;RebirthMetabolismClientState.Value=new(){CreationId="G",NutritionGainPointsPerRealMinute=1};
  RebirthLearningChallenges.Tick();Check(Count==0,"foreign metabolism evidence refused");
  UnityEngine.Time.realtimeSinceStartup+=2;RebirthMetabolismClientState.Value.CreationId="H";
  RebirthLearningChallenges.Tick();Check(Count==1,"matching metabolism evidence credited");
  Console.WriteLine(passed+" actual challenge scope checks; native projection/event/journal adapters doubled.");
 }
}
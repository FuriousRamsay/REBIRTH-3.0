using System;
using System.Collections.Generic;
namespace UnityEngine{public static class Time{public static float realtimeSinceStartup;}}
public class EntityPlayer{public int entityId=1;public World world;public bool Dead;public bool IsDead()=>Dead;}
public class PlayerList{public List<EntityPlayer> list=new List<EntityPlayer>();}
public class World{public EntityPlayer Player;public PlayerList Players=new PlayerList();public bool Remote;public bool IsRemote()=>Remote;public object GetEntity(int id)=>id==1?Player:null;}
public class GameManager{public static GameManager Instance;public World World;public bool Paused;public bool IsPaused()=>Paused;}
public class RebirthStablePlayerIdentity{public string StorageKey="owner";}
public class Theory{public float Value=20;}
public class Origin{public string CreationId;}
public class RebirthTeachingHistoryRuntimeState{public int CompletionCount;}
public class Progression{public RebirthTheoryStudyOutcome PendingTheoryStudy;public Dictionary<string,Theory> SkillKnowledge=new Dictionary<string,Theory>();public Dictionary<string,RebirthTeachingHistoryRuntimeState> TeachingHistory=new Dictionary<string,RebirthTeachingHistoryRuntimeState>();}
public class Record{public Progression Progression=new Progression();public Origin Origin=new Origin();public void Touch(string reason){}}
public class RebirthNpcInteractionContext{public string ActorId="actor",SessionKey="session",NpcId="npc";}
public enum RebirthNpcInteractionAction{Dialogue}
public enum RebirthNpcInteractionResult{Allowed,Denied}
public static class RebirthNpcInteractionSessionService{public static bool Allowed=true;public static RebirthNpcInteractionResult Authorize(string session,string actor,RebirthNpcInteractionAction action,out RebirthNpcInteractionContext context){context=new RebirthNpcInteractionContext();return Allowed?RebirthNpcInteractionResult.Allowed:RebirthNpcInteractionResult.Denied;}}
public static class RebirthNpcRuntimeRegistry{public static bool TryGetEntityId(string id,out int entity){entity=2;return true;}}
public static class RebirthNpcInteractionMutationHandlers{public static EntityPlayer FindActorPlayer(string actor)=>GameManager.Instance.World.Player;}
public static class RebirthSkillAwardService{public static Record Current;public static bool TryGetEligible(EntityPlayer player,out RebirthStablePlayerIdentity identity,out Record record){identity=new RebirthStablePlayerIdentity();record=Current;return player!=null&&!player.Dead;}}
public static class RebirthTheorySpecialistResolver{public static bool Available=true;public static string Source="specialist";public static bool TryResolve(EntityPlayer student,int entity,string subject,out string source,out float theory){source=Source;theory=80;return Available;}}
public static class RebirthTheoryProgressionService{public static bool Allowed=true;public static float Transfer=3;public static bool TryPreviewNpcInstruction(EntityPlayer student,int entity,string subject,out float transfer,out double cooldown,out string reason){transfer=Transfer;cooldown=0;reason="";return Allowed;}}
public static class RebirthSkillKnowledgeService{public static object SyncRoot=new object();}
public static class RebirthWorldCharacterRepository{public static bool IsServerAuthority=true;}
public static class RebirthTeachingService{public const float SessionDurationSeconds=20;public static int Notices;public static void NotifyStudy(EntityPlayer player,bool success,string message){Notices++;}public static string FriendlySkill(string skill)=>skill;}
public static class Localization{public static string Get(string key)=>key;}
public static class RebirthTheoryStudySettlement{public static int Calls;public static bool TrySettle(EntityPlayer student,out string reason){Calls++;reason="";return false;}}
public static class SpecialistTimedFixture{
 static int checks;static Record r;static RebirthNpcInteractionContext context=new RebirthNpcInteractionContext();
 static void Check(bool value,string label){if(!value)throw new Exception(label);checks++;}
 static void Reset(){RebirthTheorySpecialistLessonService.Reset();UnityEngine.Time.realtimeSinceStartup=0;var world=new World();world.Player=new EntityPlayer{world=world};world.Players.list.Add(world.Player);GameManager.Instance=new GameManager{World=world};r=new Record();r.Origin.CreationId=Guid.NewGuid().ToString("N");r.Progression.SkillKnowledge["skill.cooking"]=new Theory();RebirthSkillAwardService.Current=r;RebirthNpcInteractionSessionService.Allowed=true;RebirthTheorySpecialistResolver.Available=true;RebirthTheorySpecialistResolver.Source="specialist";RebirthTheoryProgressionService.Allowed=true;RebirthTheoryProgressionService.Transfer=3;RebirthTheoryStudySettlement.Calls=0;RebirthTeachingService.Notices=0;}
 static bool Begin(){return RebirthTheorySpecialistLessonService.TryBegin(context,"skill.cooking",out _);}
 static void Steps(int count){for(int i=0;i<count;i++){UnityEngine.Time.realtimeSinceStartup+=0.25f;RebirthTheorySpecialistLessonService.Tick();}}
 public static string Run(){
  Reset();Check(Begin(),"initial begin rejected");Steps(80);Check(r.Progression.PendingTheoryStudy!=null,"first tick discarded initial session");
  Reset();RebirthTheorySpecialistLessonService.Tick();Check(Begin(),"begin rejected");Steps(40);Check(Begin(),"repeated begin rejected");Steps(39);Check(r.Progression.PendingTheoryStudy==null,"early completion");Steps(1);Check(r.Progression.PendingTheoryStudy!=null&&r.Progression.PendingTheoryStudy.Target==23&&RebirthTheoryStudySettlement.Calls==1,"repeat reset or duplicated outcome");Steps(4);Check(RebirthTheoryStudySettlement.Calls==1,"timer replayed completed session");
  Reset();RebirthTheorySpecialistLessonService.Tick();Begin();GameManager.Instance.Paused=true;Steps(80);Check(r.Progression.PendingTheoryStudy==null,"paused time completed lesson");GameManager.Instance.Paused=false;Steps(80);Check(r.Progression.PendingTheoryStudy!=null,"resume failed");
  Reset();RebirthTheorySpecialistLessonService.Tick();Begin();UnityEngine.Time.realtimeSinceStartup=30;RebirthTheorySpecialistLessonService.Tick();Steps(79);Check(r.Progression.PendingTheoryStudy==null,"stall credited elapsed");Steps(1);Check(r.Progression.PendingTheoryStudy!=null,"post-stall completion failed");
  foreach(string change in new[]{"permission","specialist","creation","source","dead","world","policy","historyOverflow"}){
   Reset();RebirthTheorySpecialistLessonService.Tick();Begin();Steps(40);
   switch(change){case "permission":RebirthNpcInteractionSessionService.Allowed=false;break;case "specialist":RebirthTheorySpecialistResolver.Available=false;break;case "creation":r.Origin.CreationId=Guid.NewGuid().ToString("N");break;case "source":RebirthTheorySpecialistResolver.Source="other";break;case "dead":GameManager.Instance.World.Player.Dead=true;break;case "world":GameManager.Instance.World=new World();break;case "policy":RebirthTheoryProgressionService.Allowed=false;break;case "historyOverflow":r.Progression.TeachingHistory["npc|specialist|skill.cooking"]=new RebirthTeachingHistoryRuntimeState{CompletionCount=int.MaxValue};break;}
   Steps(80);Check(r.Progression.PendingTheoryStudy==null,change+" awarded lesson");
  }
  Reset();RebirthTheorySpecialistLessonService.Tick();Begin();Steps(40);RebirthTheoryProgressionService.Transfer=1;Steps(40);Check(r.Progression.PendingTheoryStudy.Target==21,"current cap ignored");
  return "PASS "+checks+" actual specialist timer/outcome checks with explicit authority/world/permission/policy/settlement doubles; native interaction/disk not exercised";
 }
}
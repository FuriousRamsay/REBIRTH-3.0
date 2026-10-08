using System;
using System.Collections.Generic;
public class World { public bool IsRemote()=>false; }
public class EntityPlayer { public World world=new World();public bool IsDead()=>false; }
public class RebirthStablePlayerIdentity {public string StorageKey="owner";}
public class RebirthTeachingHistoryRuntimeState {public string Key,StudentStorageKey,SkillId;public int CompletionCount;public long LastCompletedUtcTicks;}
public class Theory {public float Value=20;}
public class Origin {public string CreationId;}
public class Progression {
 public RebirthTheoryStudyOutcome PendingTheoryStudy;
 public Dictionary<string,Theory> SkillKnowledge=new Dictionary<string,Theory>();
 public Dictionary<string,RebirthTeachingHistoryRuntimeState> TeachingHistory=new Dictionary<string,RebirthTeachingHistoryRuntimeState>();
 public HashSet<string> SkillAwardReceipts=new HashSet<string>();
}
public class Record {public Progression Progression=new Progression();public Origin Origin=new Origin();public bool Dirty;public void Touch(string reason){Dirty=true;}}
public static class RebirthSkillKnowledgeService {public static object SyncRoot=new object();}
public static class RebirthSkillAwardService {
 public static Record Current;public static int Publications;
 public static bool TryGetEligible(EntityPlayer player,out RebirthStablePlayerIdentity identity,out Record record){identity=new RebirthStablePlayerIdentity();record=Current;return true;}
 public static void QueueOwnerPublication(EntityPlayer player){Publications++;}
}
public static class RebirthTeachingOutcomeStore {public static void PruneAwardReceipts(HashSet<string> receipts,string protectedReceipt=null){} }
public static class RebirthWorldCharacterRepository {
 public static Action OnSave;public static int Stage;public static int FailAt;public static bool ThrowAt;public static bool Witness=true,TerminalWitness=true;
 public static bool SaveIfDirty(RebirthStablePlayerIdentity identity,string reason){Stage++;OnSave?.Invoke();if(Stage==FailAt){if(Stage==3)TerminalWitness=false;if(ThrowAt)throw new Exception("save fault");return false;}RebirthSkillAwardService.Current.Dirty=false;return true;}
 public static bool HasSavedTheoryStudy(RebirthStablePlayerIdentity identity,RebirthTheoryStudyOutcome outcome,bool applied,bool settled=false)=>Witness&&(!settled||TerminalWitness);
}
public static class TheoryStudySettlementFixture {
 static int checks;
 static void Check(bool condition,string label){if(!condition)throw new Exception(label);checks++;}
 static Record Reset(int fail,bool throws=false){
  var r=new Record();r.Origin.CreationId=Guid.NewGuid().ToString("N");r.Progression.SkillKnowledge["skill.cooking"]=new Theory();
  RebirthTheoryStudyOutcome.TryCreate(Guid.NewGuid(),r.Origin.CreationId,"skill.cooking","npc","teacher",20,23,1,DateTime.UtcNow.Ticks,20,out r.Progression.PendingTheoryStudy);
  RebirthSkillAwardService.Current=r;RebirthSkillAwardService.Publications=0;
  RebirthWorldCharacterRepository.OnSave=null;RebirthWorldCharacterRepository.Stage=0;RebirthWorldCharacterRepository.FailAt=fail;RebirthWorldCharacterRepository.ThrowAt=throws;RebirthWorldCharacterRepository.Witness=true;RebirthWorldCharacterRepository.TerminalWitness=true;return r;
 }
 public static string Run(){
  string reason;var p=new EntityPlayer();var r=Reset(1);
  Check(!RebirthTheoryStudySettlement.TrySettle(p,out reason)&&r.Progression.SkillKnowledge["skill.cooking"].Value==20&&r.Progression.SkillAwardReceipts.Count==0,"prepare failure applied effect");
  r=Reset(2);var id=r.Progression.PendingTheoryStudy.Id;
  Check(!RebirthTheoryStudySettlement.TrySettle(p,out reason)&&r.Progression.PendingTheoryStudy.Id==id&&r.Progression.SkillKnowledge["skill.cooking"].Value==23,"apply failure lost intent");
  RebirthWorldCharacterRepository.FailAt=0;
  Check(RebirthTheoryStudySettlement.TrySettle(p,out reason)&&r.Progression.PendingTheoryStudy==null&&r.Progression.SkillKnowledge["skill.cooking"].Value==23&&r.Progression.TeachingHistory["npc|teacher|skill.cooking"].CompletionCount==1,"retry repeated effect");
  Check(!RebirthTheoryStudySettlement.TrySettle(p,out reason)&&RebirthSkillAwardService.Publications==1,"settled retry published twice");
  foreach(bool throws in new[]{false,true}){
   r=Reset(3,throws);id=r.Progression.PendingTheoryStudy.Id;
   Check(!RebirthTheoryStudySettlement.TrySettle(p,out reason)&&r.Progression.PendingTheoryStudy.Id==id&&r.Dirty,"terminal failure lost pending");
  }
  r=Reset(0);RebirthWorldCharacterRepository.Witness=false;
  Check(!RebirthTheoryStudySettlement.TrySettle(p,out reason)&&r.Progression.SkillKnowledge["skill.cooking"].Value==20,"missing witness applied effect");
  r=Reset(0);r.Origin.CreationId=Guid.NewGuid().ToString("N");
  Check(!RebirthTheoryStudySettlement.TrySettle(p,out reason)&&RebirthWorldCharacterRepository.Stage==0,"wrong owner saved");
  r=Reset(0);RebirthWorldCharacterRepository.OnSave=()=>RebirthSkillAwardService.Current=new Record();
  Check(!RebirthTheoryStudySettlement.TrySettle(p,out reason)&&r.Progression.SkillKnowledge["skill.cooking"].Value==20&&RebirthSkillAwardService.Publications==0,"record replacement during prepare cannot apply or publish");
  r=Reset(0);RebirthWorldCharacterRepository.OnSave=()=>p.world=new World();
  Check(!RebirthTheoryStudySettlement.TrySettle(p,out reason)&&r.Progression.SkillKnowledge["skill.cooking"].Value==20,"world replacement during prepare cannot apply");
  r=Reset(0);RebirthWorldCharacterRepository.OnSave=()=>{if(RebirthWorldCharacterRepository.Stage==3)RebirthSkillAwardService.Current=new Record();};
  Check(!RebirthTheoryStudySettlement.TrySettle(p,out reason)&&RebirthSkillAwardService.Publications==0,"terminal save record replacement cannot publish old study");
  r=Reset(0);var pendingBeforeTerminal=r.Progression.PendingTheoryStudy;RebirthWorldCharacterRepository.TerminalWitness=false;
  Check(!RebirthTheoryStudySettlement.TrySettle(p,out reason)&&ReferenceEquals(r.Progression.PendingTheoryStudy,pendingBeforeTerminal)&&RebirthSkillAwardService.Publications==0,"save return without terminal file witness retains study custody");
  RebirthWorldCharacterRepository.TerminalWitness=true;
  Check(RebirthTheoryStudySettlement.TrySettle(p,out reason)&&r.Progression.PendingTheoryStudy==null&&r.Progression.TeachingHistory["npc|teacher|skill.cooking"].CompletionCount==1,"terminal witness retry settles once without repeated lesson");
  return "PASS "+checks+" actual settlement fault/retry checks with explicit repository/player/progression doubles; disk/native authority not exercised";
 }
}
public static class Localization {public static string Get(string key)=>key;}
public static class RebirthTeachingService {public static void NotifyStudy(EntityPlayer player,bool success,string message){}public static string FriendlySkill(string skill)=>skill;}
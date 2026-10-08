using System;
using System.Collections.Generic;
class EntityPlayer { public int entityId; }
class RebirthStablePlayerIdentity { public string StorageKey; public RebirthWorldCharacterRecord Record; }
class RebirthWorldCharacterRecord {public OriginState Origin=new OriginState(); public bool IsComplete=true,Dirty; public ProgressionState Progression=new ProgressionState(); public void Touch(string why){Dirty=true;} }
class OriginState {public string CreationId;}
class ProgressionState {
 public HashSet<string> SkillAwardReceipts=new HashSet<string>();
 public Dictionary<string,RebirthSkillKnowledgeRuntimeState> SkillKnowledge=new Dictionary<string,RebirthSkillKnowledgeRuntimeState>();
 public Dictionary<string,RebirthTeachingLessonRuntimeState> TeachingLessons=new Dictionary<string,RebirthTeachingLessonRuntimeState>();
 public Dictionary<string,RebirthTeachingHistoryRuntimeState> TeachingHistory=new Dictionary<string,RebirthTeachingHistoryRuntimeState>();
}
class RebirthSkillKnowledgeRuntimeState {public float Value;}
class RebirthTeachingLessonRuntimeState {public string SkillId,InstructorStorageKey,OutcomeId;public float RemainingActiveSeconds,GainMultiplier;}
class RebirthTeachingHistoryRuntimeState {public string Key,StudentStorageKey,SkillId;public long LastCompletedUtcTicks;public int CompletionCount;}
class RebirthTeachingDurableOutcome {
 public string OutcomeId="test",InstructorStorageKey="teacher",StudentStorageKey="student",InstructorCreationId,StudentCreationId,SkillId="skill",HistoryKey="history";
 public bool StudentApplied,InstructorApplied,RewardApplied,HasLastingLesson=true;
 public float StudentKnowledgeTarget=10,LastingLessonSeconds=60,LastingLessonMultiplier=2,TeacherAward=1;
 public int HistoryTargetCount=1; public long CompletedUtcTicks=1;
}
static class RebirthWorldCharacterRepository {public static bool Fail;public static int Saves;public static bool SaveIfDirty(RebirthStablePlayerIdentity id,string why){Saves++;if(Fail)return false;id.Record.Dirty=false;return true;}}
static class RebirthTeachingOutcomeStore {public static int Acks;public static bool AcknowledgeStudent(string id){Acks++;return true;}public static bool AcknowledgeInstructor(string id){Acks++;return true;}public static bool AcknowledgeReward(string id){Acks++;return true;}}
static class RebirthSkillAwardService {public static int Calls;public static bool TryAwardMigratedTrainingEvidence(EntityPlayer p,RebirthSkillTrainingEvidence e,out RebirthSkillTrainingComputation c,out float s,out float a){Calls++;c=null;s=a=0;return true;}}
class RebirthSkillTrainingEvidence {public string SkillId,SourceKey,DurableReceiptId,ReferenceDescription;public bool AuthoritativeSuccess;public RebirthSkillTrainingEvidenceMode Mode;public float CreditedWork,DiscreteRawAward;}
class RebirthSkillTrainingComputation {}
enum RebirthSkillTrainingEvidenceMode {DiscreteAward}
static class RebirthSurvivorIds {public const string SkillTeaching="teaching";}
static class Time {public static float realtimeSinceStartup=1;}
static class Localization {public static string Get(string key){return key;}}
// SCOPE
class Check {
 static readonly object Gate=new object();
 static readonly HashSet<int> LessonPlayers=new HashSet<int>(),DirtyLessonPlayers=new HashSet<int>();
 static readonly Dictionary<int,float> NextLessonRevisionPublish=new Dictionary<int,float>();
 static int Publications;
 static void PublishSavedOutcome(EntityPlayer p,string reason){Publications++;}
 static void AddBoundedReceipt(HashSet<string> receipts,string receipt){receipts.Add(receipt);}
 static void PruneHistory(ProgressionState p){}
 // SOURCE
 static void Assert(bool value,string why){if(!value)throw new Exception(why);}
 static void Main(){
  for(int missing=0;missing<3;missing++){
   var ir=new RebirthWorldCharacterRecord();var sr=new RebirthWorldCharacterRecord();
   ir.Origin.CreationId=Guid.NewGuid().ToString("N");sr.Origin.CreationId=Guid.NewGuid().ToString("N");
   var o=new RebirthTeachingDurableOutcome{InstructorCreationId=ir.Origin.CreationId,StudentCreationId=sr.Origin.CreationId};
   o.StudentApplied=missing==0;o.InstructorApplied=missing==1;o.RewardApplied=missing==2;
   sr.Progression.SkillKnowledge["skill"]=new RebirthSkillKnowledgeRuntimeState();
   var ii=new RebirthStablePlayerIdentity{StorageKey="teacher",Record=ir};var si=new RebirthStablePlayerIdentity{StorageKey="student",Record=sr};
   RebirthTeachingOutcomeStore.Acks=RebirthSkillAwardService.Calls=RebirthWorldCharacterRepository.Saves=Publications=0;
   Assert(!TryApplyDurableOutcome(o,new EntityPlayer(),new EntityPlayer(),ii,ir,si,sr),"missing acknowledged receipt accepted "+missing);
   Assert(RebirthTeachingOutcomeStore.Acks==0 && RebirthSkillAwardService.Calls==0 && RebirthWorldCharacterRepository.Saves==0 && Publications==0,"mismatch caused side effect");
   Assert(sr.Progression.SkillKnowledge["skill"].Value==0 && sr.Progression.TeachingLessons.Count==0 && ir.Progression.TeachingHistory.Count==0,"mismatch mutated records");
   // Restored evidence permits the remaining stages, without restarting acknowledged work.
   if(missing==0)sr.Progression.SkillAwardReceipts.Add("teaching:test:student");
   if(missing==1)ir.Progression.SkillAwardReceipts.Add("teaching:test:instructor");
   if(missing==2)ir.Progression.SkillAwardReceipts.Add("teaching:test:reward");
   Assert(TryApplyDurableOutcome(o,new EntityPlayer(),new EntityPlayer(),ii,ir,si,sr),"matching evidence refused");
   Assert(RebirthTeachingOutcomeStore.Acks==2,"remaining stages not acknowledged");
   Assert(RebirthSkillAwardService.Calls==(missing==2?0:1),"acknowledged reward replayed");
   if(missing==0)Assert(sr.Progression.TeachingLessons.Count==0,"acknowledged lesson restarted");
  }
  // A receipt written before a journal ACK suppresses mutation but still flushes dirty save.
  var teacher=new RebirthWorldCharacterRecord();var student=new RebirthWorldCharacterRecord();
  teacher.Origin.CreationId=student.Origin.CreationId=Guid.NewGuid().ToString("N");
  var lesson=new RebirthTeachingDurableOutcome{InstructorCreationId=teacher.Origin.CreationId,StudentCreationId=student.Origin.CreationId};
  student.Progression.SkillAwardReceipts.Add("teaching:test:student");student.Dirty=true;
  RebirthWorldCharacterRepository.Fail=true;RebirthTeachingOutcomeStore.Acks=0;
  Assert(!TryApplyDurableOutcome(lesson,new EntityPlayer(),new EntityPlayer(),new RebirthStablePlayerIdentity{StorageKey="teacher",Record=teacher},teacher,new RebirthStablePlayerIdentity{StorageKey="student",Record=student},student),"failed participant save accepted");
  Assert(RebirthTeachingOutcomeStore.Acks==0 && !lesson.StudentApplied,"failed save acknowledged");
  Console.WriteLine("PASS actual teaching recovery: three acknowledged/missing-receipt refusal paths have no effects; matching receipts resume remaining stages; acknowledged lesson/reward not replayed; failed dirty receipt save not acknowledged. Native/repository/award adapters doubled.");
 }
}
using System;using System.Collections.Generic;
namespace UnityEngine{public static class Time{public static float time;}}
public static class Log{public static void Out(string s){}}
public static class RebirthSkillEvalDiagnostics{public static bool On=>false;}
// Isolate realized medical outcomes; progression multipliers/Constitution have their own harnesses.
public static class RebirthConstitutionTrainingService{
 public class ExposureEvidence{}
 public static ExposureEvidence BeginExposure(EntityPlayer p,string item)=>null;
 public static void CompleteExposure(ExposureEvidence e){}
 public static void OnHealthRestored(EntityAlive p,float amount){}
}
public enum RebirthSkillTrainingEvidenceMode{DiscreteAward}
public class RebirthSkillTrainingEvidence{public string SkillId,SourceKey,ReferenceDescription;public bool AuthoritativeSuccess;public RebirthSkillTrainingEvidenceMode Mode;public float CreditedWork,DiscreteRawAward;}
public class RebirthSkillTrainingComputation{public float ExpectedFinalGain;}
namespace HarmonyLib{[AttributeUsage(AttributeTargets.Class)]public class HarmonyPatch:Attribute{public HarmonyPatch(Type t,string s){}}}
public class World{public Dictionary<int,EntityAlive> Entities=new();public bool IsRemote()=>false;public EntityAlive GetEntity(int id)=>Entities.TryGetValue(id,out var p)?p:null;}
public class Stat{public float Value;}
public class Stats{public Stat Health=new Stat();}
public class Buffs{public HashSet<string> Active=new();public Dictionary<string,float> Vars=new();public bool HasBuff(string s)=>Active.Contains(s);public float GetCustomVar(string s)=>Vars.TryGetValue(s,out var v)?v:0;}
public class EntityAlive{public int entityId;public World world;public Stats Stats=new();public Buffs Buffs=new();public bool IsDead()=>false;public void OnUpdateLive(){}}
public class EntityPlayer:EntityAlive{}
public class ItemClass{public string GetItemName()=>"";}
public class ItemValue{public ItemClass ItemClass;}
public class ItemStack{public ItemValue itemValue;}
public class InventoryData{public EntityAlive holdingEntity;public ItemValue itemValue;}
public class ItemActionData{public InventoryData invData;}
public class ItemActionEat{public void ExecuteInstantAction(){} public void ExecuteAction(){}}
public static class RebirthServiceCraftSkillService{
 public static bool TryGetPracticalSkillProgress(EntityPlayer p,string id,out float l,out float v){l=v=0;return true;}
 public static void AdjustNewAbrasionTreatment(EntityPlayer healer,EntityAlive patient,bool before,bool treated){}
 public static void AdjustMedicalTreatmentOutcome(EntityPlayer healer,EntityAlive patient,string item,float reserve,float infection,float cure,bool leg,bool arm,float legBase,float armBase){}
}
public static class RebirthSkillAwardService{
 public static float Total; public static EntityPlayer LastRecipient;
 public static bool TryAwardMigratedTrainingEvidence(EntityPlayer p,RebirthSkillTrainingEvidence e,out RebirthSkillTrainingComputation c,out float gained,out float attribute){gained=e.DiscreteRawAward;attribute=0;Total+=gained;LastRecipient=p;c=new(){ExpectedFinalGain=gained};return true;}
 public static bool TryPreviewTrainingEvidence(EntityPlayer p,RebirthSkillTrainingEvidence e,out RebirthSkillTrainingComputation c){c=new(){ExpectedFinalGain=e.DiscreteRawAward};return true;}
}
public static class RebirthTheoryProgressionService {public static bool TryAwardInsight(EntityPlayer p,string id,string source,out float applied,out bool already){applied=0;already=false;return true;}}
class Program{
static int entitySequence;
static EntityPlayer Patient(EntityPlayer healer){var p=new EntityPlayer{world=healer.world,entityId=++entitySequence};p.world.Entities[p.entityId]=p;return p;}
static EntityPlayer Player(){RebirthSkillAwardService.Total=0;var p=new EntityPlayer{world=new World(),entityId=++entitySequence};p.world.Entities[p.entityId]=p;p.Stats.Health.Value=50;p.Buffs.Vars["medRegHealthIncSpeed"]=2;return p;}
static void Done(RebirthMedicalPractice.Evidence e){RebirthMedicalPractice.Complete(e);RebirthMedicalPractice.Exit();}
static void Eq(float n,string s){if(Math.Abs(RebirthSkillAwardService.Total-n)>.000001)throw new Exception(s+": "+RebirthSkillAwardService.Total);}
static void Main(){
var p=Player();p.Buffs.Active.Add("buffInjuryBleeding");var e=RebirthMedicalPractice.Begin(p,p,"medicalBandage");p.Buffs.Active.Clear();Done(e);Eq(.01f,"Bleed stopped");
p=Player();e=RebirthMedicalPractice.Begin(p,p,"medicalBandage");p.Buffs.Active.Add("unrelated");Done(e);Eq(0,"No arbitrary buff XP");
p=Player();e=RebirthMedicalPractice.Begin(p,p,"medicalFirstAidBandage");p.Buffs.Vars["medicalRegHealthAmount"]=30;Done(e);Eq(0,"No advance healing XP");p.Stats.Health.Value+=15;p.Buffs.Vars["medicalRegHealthAmount"]=15;RebirthMedicalPractice.Observe(p);Eq(.015f,"Partial healing");p.Stats.Health.Value+=15;p.Buffs.Vars["medicalRegHealthAmount"]=0;RebirthMedicalPractice.Observe(p);Eq(.03f,"Full healing");RebirthMedicalPractice.Observe(p);Eq(.03f,"No double reward");
p=Player();e=RebirthMedicalPractice.Begin(p,p,"medicalFirstAidKit");p.Buffs.Vars["medicalRegHealthAmount"]=100;Done(e);p.Buffs.Vars["medicalRegHealthAmount"]=0;RebirthMedicalPractice.Observe(p);Eq(0,"Wasted healing earns nothing");
p=Player();p.Buffs.Vars["infectionCounter"]=5;e=RebirthMedicalPractice.Begin(p,p,"drugHerbalAntibiotics");p.Buffs.Vars["$infectionCureCounter"]=100;Done(e);p.Buffs.Vars["infectionCounter"]=0;p.Buffs.Vars["$infectionCureCounter"]=0;RebirthMedicalPractice.Observe(p);Eq(.01f,"Partial infection cure");
p=Player();e=RebirthMedicalPractice.Begin(p,p,"drugSteroids");p.Buffs.Active.Add("buffDrugSteroids");Done(e);Eq(0,"Preventive steroids");
p=Player();p.Buffs.Active.Add("buffLegSprained");e=RebirthMedicalPractice.Begin(p,p,"drugSteroids");p.Buffs.Active.Add("buffDrugSteroids");Done(e);Eq(.01f,"Useful steroids");e=RebirthMedicalPractice.Begin(p,p,"drugSteroids");Done(e);Eq(.01f,"No repeat steroid reward");
p=Player();p.Buffs.Active.Add("buffArmBroken");e=RebirthMedicalPractice.Begin(p,p,"medicalPlasterCast");p.Buffs.Active.Add("buffArmCast");Done(e);Eq(.05f,"New fracture treatment");e=RebirthMedicalPractice.Begin(p,p,"medicalPlasterCast");Done(e);Eq(.05f,"No repeated fracture reward");
p=Player();e=RebirthMedicalPractice.Begin(p,p,"drugFortBites");p.Buffs.Active.Add("buffDrugFortBites");Done(e);Eq(0,"Performance buff");
p=Player();var patient=Patient(p);patient.Buffs.Active.Add("buffLegBroken");
e=RebirthMedicalPractice.Begin(p,patient,"medicalSplint");patient.Buffs.Active.Add("buffLegSplinted");Done(e);
Eq(.02f,"Other-player splint practice with active fracture");
if(!ReferenceEquals(RebirthSkillAwardService.LastRecipient,p))throw new Exception("Practice credited patient rather than healer");
e=RebirthMedicalPractice.Begin(p,patient,"medicalSplint");Done(e);Eq(.02f,"Repeated other-player splint earns no additional practice");
p=Player();patient=Patient(p);patient.Buffs.Active.Add("buffLaceration");
e=RebirthMedicalPractice.Begin(p,patient,"resourceSewingKit");patient.Buffs.Active.Remove("buffLaceration");Done(e);
Eq(.01f,"Realized other-player laceration repair");
if(!ReferenceEquals(RebirthSkillAwardService.LastRecipient,p))throw new Exception("Sewing credited patient");
e=RebirthMedicalPractice.Begin(p,patient,"resourceSewingKit");Done(e);Eq(.01f,"No repeated sewing credit");
p=Player();p.Buffs.Active.Add("buffLaceration");e=RebirthMedicalPractice.Begin(p,p,"resourceSewingKit");Done(e);Eq(0,"Failed sewing earns nothing");
p=Player();p.Buffs.Active.Add("buffLaceration");p.Buffs.Active.Add("buffInjuryBleeding");e=RebirthMedicalPractice.Begin(p,p,"resourceSewingKit");p.Buffs.Active.Clear();Done(e);Eq(.01f,"Combined wound and bleed credited once");
p=Player();patient=new EntityPlayer{world=new World()};e=RebirthMedicalPractice.Begin(p,patient,"medicalBandage");if(e!=null)throw new Exception("Cross-world treatment admitted");RebirthMedicalPractice.Exit();
p=Player();patient=Patient(p);patient.Buffs.Active.Add("buffLaceration");e=RebirthMedicalPractice.Begin(p,patient,"resourceSewingKit");patient.Buffs.Active.Clear();patient.world=new World();Done(e);Eq(0,"Patient world change invalidates completion");
p=Player();patient=Patient(p);patient.Buffs.Active.Add("buffLaceration");e=RebirthMedicalPractice.Begin(p,patient,"resourceSewingKit");patient.Buffs.Active.Clear();p.world=patient.world=new World();Done(e);Eq(0,"Moving both participants cannot rebind old treatment");
p=Player();patient=Patient(p);e=RebirthMedicalPractice.Begin(p,patient,"medicalFirstAidBandage");patient.Buffs.Vars["medicalRegHealthAmount"]=30;Done(e);p.world=new World();patient.Stats.Health.Value+=30;patient.Buffs.Vars["medicalRegHealthAmount"]=0;RebirthMedicalPractice.Observe(patient);Eq(0,"Healer world change invalidates deferred credit");
p=Player();patient=Patient(p);e=RebirthMedicalPractice.Begin(p,patient,"medicalFirstAidBandage");patient.Buffs.Vars["medicalRegHealthAmount"]=30;Done(e);p.world.Entities.Remove(p.entityId);patient.Stats.Health.Value+=30;patient.Buffs.Vars["medicalRegHealthAmount"]=0;RebirthMedicalPractice.Observe(patient);Eq(0,"Disconnected healer cannot receive old deferred credit");
p=Player();patient=Patient(p);patient.Buffs.Active.Add("buffLaceration");e=RebirthMedicalPractice.Begin(p,patient,"resourceSewingKit");patient.Buffs.Active.Clear();p.world.Entities[patient.entityId]=new EntityPlayer{world=p.world,entityId=patient.entityId};Done(e);Eq(0,"Reused patient ID cannot complete old evidence");
p=Player();p.world.Entities.Remove(p.entityId);e=RebirthMedicalPractice.Begin(p,p,"medicalBandage");if(e!=null)throw new Exception("Detached player admitted");RebirthMedicalPractice.Exit();
Console.WriteLine("Medical effect scenarios passed: useful effects, partial recovery, wasted use, repeat prevention, performance-only use.");}}

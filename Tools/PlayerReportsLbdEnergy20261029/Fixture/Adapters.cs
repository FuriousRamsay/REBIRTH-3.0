// Explicit test doubles: no game scene, network endpoint or real persistent record.
using System;using System.Collections.Generic;
namespace UnityEngine { public static class Mathf {
public static float Max(float a,float b)=>Math.Max(a,b);public static float Min(float a,float b)=>Math.Min(a,b);
public static float Clamp(float v,float a,float b)=>Math.Clamp(v,a,b);public static float Clamp01(float v)=>Clamp(v,0,1);
public static float Lerp(float a,float b,float t)=>a+(b-a)*Clamp01(t);public static float Abs(float a)=>Math.Abs(a);
public static float InverseLerp(float a,float b,float v)=>a==b?0:Clamp01((v-a)/(b-a));
} }
public class World { public bool Remote;public EntityPlayer Player; public bool IsRemote()=>Remote;public EntityAlive GetEntity(int id)=>id==7?Player:null; }
public class ItemClass {public string Name,Tags;public string ItemTags=>Tags;public string GetItemName()=>Name;}
public class ItemValue {public int type;public ItemClass ItemClass;}
public class DamageSource {public ItemValue AttackingItem;public int Actor=7;public int getEntityId()=>Actor;}
public struct DamageResponse {public DamageSource Source;public int Strength;}
public class EntityAlive {public World world;public int entityId,Health=100;public bool Dead;public bool IsDead()=>Dead||Health<=0;}
public class Stat {public float Value=100,ModifiedMax=100;}
public class Stats {public Stat Water=new(),Food=new(),Stamina=new();}
public class BoolValue {public bool Value;}
public class EntityPlayer:EntityAlive {public Stats Stats=new();public BoolValue IsGodMode=new();public FastTags<TagGroup.Global> CurrentMovementTag;}
public class RebirthStablePlayerIdentity {public string StorageKey="owner";}
public class Origin {public string CreationId="creation";}
public class Progression {public Dictionary<string,RebirthSkillRuntimeState> Skills=new(StringComparer.OrdinalIgnoreCase);}
public class RebirthWorldCharacterRecord {public Origin Origin=new();public Progression Progression=new();}
public class RebirthSkillRuntimeState {public float Value,Progress;}
public static class RebirthSkillAwardService {
public static RebirthStablePlayerIdentity Owner=new();public static RebirthWorldCharacterRecord Record=new();public static bool Eligible=true,Save=true;public static HashSet<string> Receipts=new();
public static bool TryGetEligible(EntityPlayer p,out RebirthStablePlayerIdentity o,out RebirthWorldCharacterRecord r){o=Owner;r=Record;return Eligible&&p?.world!=null&&!p.world.IsRemote();}
public static bool TryAwardCombatDurable(EntityPlayer p,string skill,float amount,string key,float interval,string receipt,out float gain,out float attribute){attribute=0;gain=0;if(!Receipts.Add(receipt))return Save;Record.Progression.Skills[skill].Progress+=amount;gain=amount;return Save;}
}
public static class RebirthBlackMagicService {public static void OnDominatedCombatDamage(EntityAlive t,DamageResponse r,int a,float d){} }
public static class RebirthHumanCombatPractice {public static void OnDamageCommitted(EntityAlive t,DamageResponse r,float d,int h){} }
public static class RebirthComplexSkillSystemService {public static bool IsRemoteOwnedDeviceSource(World w,DamageSource s)=>false;}
public static class RebirthSkillWaveAService {public static void OnArmoredCombat(EntityPlayer p){} }
public static class RebirthSkillEvalDiagnostics {public static bool On=false;}
public static class Log {public static void Out(string s){} }
public static class RebirthStealthTrainingService {public static bool IsPreHitStealthQualified(EntityAlive t,DamageSource s)=>false;public static void OnQualifiedDamage(EntityPlayer p,EntityAlive t,ItemValue i,float d,int h,RebirthWorldCharacterRecord r){} }
public static class RebirthRageService {public static void OnMeaningfulMeleeDamage(EntityPlayer p,EntityAlive t,float d,string s,ItemValue i){} }
public static class RebirthTheorySoloCombatService {public class Witness{} public static Witness Before(EntityAlive t,DamageSource s)=>null;public static void Unknown(Witness w,Exception e){}public static void After(Witness w,EntityAlive t,DamageResponse r,bool ran){} }
public static class RebirthStatisticsService {public static int Calls;public static float Damage;public static void RecordDamage(EntityAlive t,DamageResponse r,DamageSource s,float raw,float d,float blocked){if(t.world.IsRemote())return;Calls++;Damage+=Math.Min(d,100);}}
public class RebirthSkillSourceMap {public string SkillId;public string[] ItemNames,ItemTokens,TagTokens,ToolTokens,BlockTokens;}
public static partial class RebirthProgressionRuntimeConfig {
public static float CombatSkillMultiplier0To24=1,CombatSkillMultiplier25To49=.8f,CombatSkillMultiplier50To74=.6f,CombatSkillMultiplier75To89=.45f,CombatSkillMultiplier90To100=.3f,CombatMaxEquivalentSecondsPerHit=6,CombatProgressPerSustainedSecond=.005f;
}
public class TagGroup {public class Global{} }
public struct FastTags<T>{string value;public static FastTags<T> Parse(string s)=>new(){value=s};public bool Test_AnySet(FastTags<T> other)=>value==other.value&&value!=null;}
public enum RebirthIngestionKind{Food,Drink}
public class RebirthIngestionEntry {public RebirthIngestionKind Kind;public float IntestinalNutritionUnits,PreparedMealEnergyEfficiencyMultiplier;}
public class RebirthMetabolismState {
public float Energy=100,SmoothedActivity,LastStamina=-1,LastEnergyRecoveryNutritionUsePerRealMinute,LastEnergyUsePerRealMinute,LastEnergyRecoveryPerRealMinute;public bool HasEnergyRateSample;
public List<RebirthIngestionEntry> IngestionEntries=new();
}
public class RebirthMetabolismModifiers {public float EnergyUse=1,EnergyRecovery=1,HydrationRequirement=1,TotalHydrationDemand=1,FoodRequirement=1,TotalNutritionDemand=1,StomachCapacity=1500,FluidAbsorption=1,DigestionSpeed=1;}
public struct RebirthMetabolismSnapshot {public float EnergyUsePerRealMinute,EnergyRecoveryPerRealMinute,FoodUsePerReal60Minutes;}
public static class RebirthMetabolismStateRepository {public static RebirthMetabolismState State;public static RebirthMetabolismState GetOrCreate(EntityPlayer p)=>State;}
public static class RebirthMetabolismModifierResolver {public static RebirthMetabolismModifiers Mods=new();public static RebirthMetabolismModifiers Resolve(EntityPlayer p,RebirthMetabolismState s)=>Mods;}
public static class RebirthEfficientMovementService {public static float Apply(EntityPlayer p,float v)=>v;}
public static partial class RebirthMetabolismService {
static bool IsServerAuthority=true;
private static readonly FastTags<TagGroup.Global> TagWalking=FastTags<TagGroup.Global>.Parse("walking"),TagRunning=FastTags<TagGroup.Global>.Parse("running"),TagSwimming=FastTags<TagGroup.Global>.Parse("swimming"),TagSwimmingRun=FastTags<TagGroup.Global>.Parse("swimmingRun"),TagClimbing=FastTags<TagGroup.Global>.Parse("climbing"),TagJumping=FastTags<TagGroup.Global>.Parse("jumping");
static void ResolveTemperature(EntityPlayer p,RebirthMetabolismModifiers m,out float a,out float b){a=b=0;}
static void ResolveIllness(EntityPlayer p,out float a,out float b,out float c){a=b=c=0;}
static float EstimateCurrentGastricEmptyingMlPerRealMinute(RebirthMetabolismState s,RebirthMetabolismModifiers m,float c)=>0;
static RebirthMetabolismSnapshot BuildSnapshot(EntityPlayer p,RebirthMetabolismState s,RebirthMetabolismModifiers m,float hyd,float food,float use,float recovery,float a,float b,float c,float d)=>new(){FoodUsePerReal60Minutes=food,EnergyUsePerRealMinute=use,EnergyRecoveryPerRealMinute=recovery};
}

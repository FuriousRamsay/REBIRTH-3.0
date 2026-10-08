using System;
namespace HarmonyLib { public class Harmony {public Harmony(string s){}} public class HarmonyPatch:Attribute {public HarmonyPatch(Type t,string n){}} }
public enum RebirthAlwaysStaggerMode { Disabled,HeadshotsOnly,AllQualifyingHits }
public enum EnumDamageSource { External,Internal }
public enum EnumDamageTypes { Bashing,BloodLoss,Heat }
public enum EnumEntityStunType { None,Prone,Kneel }
[Flags] public enum EnumBodyPartHit { None=0,Head=1,Torso=2 }
public class DamageSource {public EnumDamageSource damageSource;public EnumDamageTypes damageType;public object BuffClass;}
public struct DamageResponse {public DamageSource Source;public int Strength;public bool Fatal,PainHit;public EnumBodyPartHit HitBodyPart;public EnumEntityStunType Stun;public float StunDuration;}
public class EntityClass {public string entityClassName;}
public class EntityAlive {public void ProcessDamageResponseLocal(DamageResponse response){} public EntityClass EntityClass=new EntityClass();public EModel emodel=new EModel();public string EntityName="fixture";public float painResistPercent=.5f,painHitsFelt=3;}
public class EntityEnemy:EntityAlive{} public class EntityZombie:EntityEnemy{} public class EntityAnimal:EntityAlive{}
public class EModel {public AvatarController avatarController=new AvatarZombieController();}
public class AvatarController{}
public class AvatarZombieController:AvatarController {public bool isAttackImpact=true;public float attackPlayingTime=2;}
public class AvatarSDCSController:AvatarController {public bool isAttackImpact;public float timeAttackAnimationPlaying;}
public class LegacyAvatarController:AvatarController {public bool isAttackImpact;public float timeAttackAnimationPlaying;}
public class ConnectionManager {public bool IsServer=true;}
public static class SingletonMonoBehaviour<T> where T:new(){public static T Instance=new T();}
public static class RebirthLogSettings {public static bool RuntimeInstallLoggingEnabled;}
public static class RebirthHarmonyBootstrap {public static void PatchClassOnce(HarmonyLib.Harmony h,Type t){}}
public static class Log {public static void Out(string s){}}
class Program {
 static int checks;
 static void Check(bool x,string why){if(!x)throw new Exception(why);checks++;}
 static DamageResponse Hit()=>new DamageResponse {Source=new DamageSource(),Strength=3,HitBodyPart=EnumBodyPartHit.Torso,Stun=EnumEntityStunType.Prone,StunDuration=4,PainHit=true};
 static void Main(){
  var zombie=new EntityZombie();var bleed=Hit();bleed.Source.damageSource=EnumDamageSource.Internal;bleed.Source.damageType=EnumDamageTypes.BloodLoss;bleed.Source.BuffClass=new object();
  RebirthAlwaysStaggerDecisionReason reason;
  Check(!RebirthAlwaysStaggerPolicy.ShouldApply(zombie,bleed,out reason)&&reason==RebirthAlwaysStaggerDecisionReason.NotDirectHit,"bleed forced stagger");
  var state=default(RebirthAlwaysStaggerPainOverrideState);
  RebirthAlwaysStaggerPatches.ProcessDamageResponsePrefix(zombie,ref bleed,ref state);
  Check(!state.Applied&&bleed.Stun==EnumEntityStunType.None&&!bleed.PainHit&&bleed.StunDuration==0,"bleed renews knockdown");
  Check(bleed.Strength==3&&zombie.painResistPercent==.5f&&((AvatarZombieController)zombie.emodel.avatarController).isAttackImpact,"blood-loss correction changed damage or existing attack");
  bleed.Stun=EnumEntityStunType.Prone;bleed.PainHit=true;
  RebirthAlwaysStaggerPatches.DamageEntityLocalPostfix(zombie,ref bleed);
  Check(bleed.Stun==EnumEntityStunType.None&&!bleed.PainHit,"replicated response still stunned");
  var hit=Hit();Check(RebirthAlwaysStaggerPolicy.ShouldApply(zombie,hit,out reason),"weapon stagger lost");
  RebirthAlwaysStaggerPatches.ProcessDamageResponsePrefix(zombie,ref hit,ref state);
  Check(state.Applied&&hit.Stun==EnumEntityStunType.Prone&&zombie.painResistPercent==0,"weapon knockdown lost");
  RebirthAlwaysStaggerPatches.RestorePainOverride(zombie,ref state);Check(zombie.painResistPercent==.5f,"resistance not restored");
  var burning=Hit();burning.Source.BuffClass=new object();burning.Source.damageType=EnumDamageTypes.Heat;
  Check(!RebirthAlwaysStaggerPolicy.ShouldApply(zombie,burning,out reason),"burning forced stagger");
  var fatal=bleed;fatal.Fatal=true;fatal.Stun=EnumEntityStunType.Prone;
  RebirthAlwaysStaggerPatches.NormalizeZombieBleedResponse(zombie,ref fatal);Check(fatal.Fatal&&fatal.Stun==EnumEntityStunType.Prone,"fatal response modified");
  var npc=bleed;npc.Stun=EnumEntityStunType.Prone;RebirthAlwaysStaggerPatches.NormalizeZombieBleedResponse(new EntityEnemy(),ref npc);Check(npc.Stun==EnumEntityStunType.Prone,"NPC response modified");
  var absent=Hit();absent.Source=null;Check(!RebirthAlwaysStaggerPolicy.ShouldApply(zombie,absent,out reason),"missing source forced stagger");
  RebirthAlwaysStaggerRuntimePolicy.SetMode(RebirthAlwaysStaggerMode.HeadshotsOnly);
  Check(!RebirthAlwaysStaggerPolicy.ShouldApply(zombie,Hit(),out reason),"body hit accepted in headshots mode");
  hit=Hit();hit.HitBodyPart=EnumBodyPartHit.Head;Check(RebirthAlwaysStaggerPolicy.ShouldApply(zombie,hit,out reason),"head hit lost");
  foreach(var target in new EntityAlive[]{new EntityAlive(),new EntityAnimal()})
  {
   var unchanged=bleed;unchanged.Stun=EnumEntityStunType.Prone;unchanged.StunDuration=4;unchanged.PainHit=true;
   RebirthAlwaysStaggerPatches.NormalizeZombieBleedResponse(target,ref unchanged);
   Check(unchanged.Stun==EnumEntityStunType.Prone&&unchanged.StunDuration==4&&unchanged.PainHit,"non-zombie bleed response changed");
  }
  foreach(var source in new[]{
   new DamageSource{damageSource=EnumDamageSource.External,damageType=EnumDamageTypes.BloodLoss,BuffClass=new object()},
   new DamageSource{damageSource=EnumDamageSource.Internal,damageType=EnumDamageTypes.BloodLoss},
   new DamageSource{damageSource=EnumDamageSource.Internal,damageType=EnumDamageTypes.Heat,BuffClass=new object()}
  })
  {
   var unchanged=Hit();unchanged.Source=source;
   RebirthAlwaysStaggerPatches.NormalizeZombieBleedResponse(zombie,ref unchanged);
   Check(unchanged.Stun==EnumEntityStunType.Prone&&unchanged.StunDuration==4&&unchanged.PainHit,"unrelated damage response changed");
  }
  RebirthAlwaysStaggerRuntimePolicy.SetMode(RebirthAlwaysStaggerMode.Disabled);
  Check(!RebirthAlwaysStaggerPolicy.ShouldApply(zombie,hit,out reason)&&reason==RebirthAlwaysStaggerDecisionReason.Disabled,"disabled stagger applied");
  var disabledBleed=bleed;disabledBleed.Stun=EnumEntityStunType.Prone;
  RebirthAlwaysStaggerPatches.NormalizeZombieBleedResponse(zombie,ref disabledBleed);
  Check(disabledBleed.Stun==EnumEntityStunType.None,"bleed correction depends on stagger option");
  RebirthAlwaysStaggerRuntimePolicy.SetMode(RebirthAlwaysStaggerMode.AllQualifyingHits);
  SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer=false;
  Check(!RebirthAlwaysStaggerPolicy.ShouldApply(zombie,hit,out reason)&&reason==RebirthAlwaysStaggerDecisionReason.NotAuthoritative,"client forced weapon stagger");
  SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer=true;
  Console.WriteLine("PASS "+checks+" checks of actual production policy using native-type doubles; no game/animation validation.");
 }
}
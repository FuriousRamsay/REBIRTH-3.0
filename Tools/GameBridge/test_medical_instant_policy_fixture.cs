using System;
using System.Collections.Generic;
class Buffs {public float Pending;public bool Bleeding;public List<int> ActiveBuffs=new List<int>();public float GetCustomVar(string n){return Pending;}public bool HasBuff(string n){return Bleeding;}}
class EntityAlive {public int Health=50;public Buffs Buffs=new Buffs();}
class EntityPlayer:EntityAlive {}
class ItemClass {public string GetItemName(){return "medicine";}}
class ItemValue {public ItemClass ItemClass=new ItemClass();}
class ItemStack {public int count=3;public ItemValue itemValue=new ItemValue();}
class XUiC_ItemStack {}
class ItemActionEat {public static int Effects;public virtual bool ExecuteInstantAction(EntityAlive e,ItemStack s,bool h,XUiC_ItemStack c){Effects++;s.count--;return true;}}
static class RebirthServiceCraftSkillService {public static int Adjustments;public static void AdjustMedicalReserveDelta(EntityPlayer a,EntityAlive b,float c,string d){Adjustments++;}}
enum RebirthHealingOverlapThreshold {Off,Health10}
static class RebirthHealingOverlapThresholdPolicy {public static RebirthHealingOverlapThreshold Normalize(RebirthHealingOverlapThreshold t){return t;}public static float ToHealth(RebirthHealingOverlapThreshold t){return t==RebirthHealingOverlapThreshold.Off?0:10;}}
// PRODUCTION_POLICY
class ItemActionUseMedRebirth:ItemActionEat {
 protected virtual bool TreatmentStopsBleeding {get{return false;}}
 // PRODUCTION_METHODS
}
class BleedingTreatment:ItemActionUseMedRebirth {protected override bool TreatmentStopsBleeding {get{return true;}}}
class Check {
 static void Assert(bool b,string m){if(!b)throw new Exception(m);}
 static void Main(){var p=new EntityPlayer();p.Buffs.Pending=20;var item=new ItemStack();var med=new ItemActionUseMedRebirth();
 Assert(!med.CanUseTreatment(p)&&!med.ExecuteInstantAction(p,item,false,null)&&item.count==3&&ItemActionEat.Effects==0&&RebirthServiceCraftSkillService.Adjustments==0,"blocked treatment had side effects");
 p.Buffs.Pending=10;Assert(med.ExecuteInstantAction(p,item,false,null)&&item.count==2&&ItemActionEat.Effects==1&&RebirthServiceCraftSkillService.Adjustments==1,"threshold equality denied");
 p.Buffs.Pending=30;p.Buffs.Bleeding=true;Assert(!med.CanUseTreatment(p),"ordinary medicine bypassed bleeding gate");var bleed=new BleedingTreatment();Assert(bleed.ExecuteInstantAction(p,item,false,null)&&item.count==1,"bleeding treatment denied");
 p.Buffs.Bleeding=false;Assert(!bleed.CanUseTreatment(p),"false bleeding exception");RebirthHealingOverlapRuntimePolicy.SetThreshold(RebirthHealingOverlapThreshold.Off);Assert(med.ExecuteInstantAction(p,item,false,null),"option off denied");Assert(!med.CanUseTreatment(null),"null patient allowed");p.Buffs=null;Assert(!med.CanUseTreatment(p),"missing buffs allowed");Console.WriteLine("PASS: denied instant use has no effects/consumption, threshold, bleeding exception, option off and invalid patient.");}
}

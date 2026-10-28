// AUTHORED_NOT_RUN. Actual ConsumeAt preamble/drink branch, serving decisions,
// source plan and model types. Food/supplement branches are deliberately excluded.
// Native inventory, resolver, modifiers, persistence and effects are adapters.
using System;using System.IO;using System.Linq;using System.Collections.Generic;using System.Globalization;using UnityEngine;
namespace UnityEngine{struct Vector3{internal static Vector3 zero=>new Vector3();}static class Mathf{internal static float Max(float a,float b)=>Math.Max(a,b);internal static float Min(params float[] values)=>values.Min();internal static float Clamp01(float v)=>Math.Max(0,Math.Min(1,v));}}
class ItemValue{internal bool Normalized;internal float Remaining=150;internal ItemValue Clone()=>(ItemValue)MemberwiseClone();}
class ItemStack{internal ItemValue itemValue=new ItemValue();internal int count=1;internal ItemStack(){}internal ItemStack(ItemValue value,int quantity){itemValue=value;count=quantity;}internal static ItemStack Empty=>new ItemStack{count=0};internal bool IsEmpty()=>count<=0;internal ItemStack Clone()=>new ItemStack{count=count,itemValue=itemValue.Clone()};}
// This fresh drink-decision cohort excludes outstanding durable originals.
static class RebirthConsumptionDurableDelivery{internal static bool HoldsInventoryOriginal(EntityPlayer player)=>false;}
static class ItemClass{internal static ItemValue GetItem(string name)=>new ItemValue();}
class Stat{internal float ModifiedMax=100;}
class Stats{internal Stat Water=new Stat();}
class Slots{internal bool AddItem(ItemStack stack)=>false;}
class GameManager{internal int Drops;internal void ItemDropServer(ItemStack stack,Vector3 position,Vector3 velocity){Drops++;}}
class World{internal GameManager gameManager=new GameManager();internal ulong GetWorldTime()=>1;}
class EntityPlayer{internal Stats Stats=new Stats();internal World world=new World();internal Slots bag=new Slots(),inventory=new Slots();internal ItemStack Source=new ItemStack();internal Vector3 GetPosition()=>new Vector3();}
class EntityPlayerLocal:EntityPlayer{}
// ACTUAL_MODELS
// ACTUAL_SERVING
// ACTUAL_SOURCE_PLAN
class RebirthMetabolismState{internal float Fullness;internal int Touches,Ids;internal List<RebirthIngestionEntry> Entries=new List<RebirthIngestionEntry>();internal int AllocateEntryId()=>++Ids;internal void Touch(){Touches++;}}
static class RebirthMetabolismStateRepository{internal static RebirthMetabolismState State;internal static RebirthMetabolismState GetOrCreate(EntityPlayer player)=>State;}
class RebirthMetabolismSnapshot{internal float ProjectedHydration;}
static class RebirthConsumableResolver{internal static RebirthConsumableDefinition Definition;internal static bool TryResolve(ItemValue value,out RebirthConsumableDefinition definition){definition=Definition;return true;}}
static class RebirthMetabolismModifierResolver{internal static RebirthMetabolismModifiers Resolve(EntityPlayer p,RebirthMetabolismState s)=>RebirthMetabolismModifiers.Neutral(1000);}
static class RebirthMetabolismConfig{internal const float BaseIntestinalFluidAbsorptionMlPerRealMinute=1,BaseSolidGastricEmptyingMlPerRealMinute=1,BaseIntestinalNutrientAbsorptionUnitsPerRealMinute=1,AutoSipDefaultMl=20,AutoSipStopProjectedPercent=1,MlPerHydrationPoint=10,MealFluidHoldRealSeconds=1,ClearLiquidHoldRealSeconds=1;internal static float GetFluidGastricHalfTimeRealSeconds(bool meal)=>1;}
static class RebirthLiquidContainerService{internal static int Normalizations;internal static bool NormalizeState(ItemValue value,RebirthConsumableDefinition definition,out float remaining){Normalizations++;value.Normalized=true;remaining=value.Remaining;return true;}internal static void SetRemainingMl(ItemValue value,RebirthConsumableDefinition definition,float remaining){value.Remaining=remaining;}internal static string GetLiquidProfile(ItemValue value,RebirthConsumableDefinition definition)=>"liquid";}
static class RebirthDrinkPreparationSignatureService{internal static float GetPreparedSpecialEffectDurationSeconds(ItemValue value)=>0;}
static class RebirthHydrationOwnerHold{internal static bool HoldsMetabolismOwner(EntityPlayerLocal player)=>false;}
static class RebirthHydrationServerSourceHold{internal static bool HoldsPlayer(EntityPlayer player)=>false;}
static class RebirthNpcMetabolismCustody{internal static bool HoldsPlayer(EntityPlayer player)=>false;}
static class RebirthConsumptionSourceWriteback{
 internal static int Calls;internal static bool Refuse;
 internal static bool TryCommit(EntityPlayer player,RebirthMetabolismSourceKind kind,int slot,ItemStack expected,ItemStack replacement,RebirthMetabolismState state){Calls++;if(expected.itemValue.Normalized||expected.count!=player.Source.count||expected.itemValue.Remaining!=player.Source.itemValue.Remaining)throw new Exception("final commit lost original raw preimage");if(Refuse)return false;player.Source=replacement.Clone();return true;}
}
static class RebirthMetabolismService{
 private class Plan{internal RebirthMetabolismState State;}
 private static Plan PlanningNpcSource;
 private enum NpcDeferredKind{Consumption,StressDrink,Trait}
 private static bool QueueAllowed=true;private static float Projected;private static int NativeEffects,DeferredEffects,Publications;
 private static bool TryReserveIngestionCapacity(RebirthMetabolismState state)=>QueueAllowed;
 private static float GetFullnessMl(RebirthMetabolismState state)=>state.Fullness;
 private static RebirthMetabolismSnapshot BuildSnapshot(EntityPlayer player,RebirthMetabolismState state,RebirthMetabolismModifiers mods,params float[] values)=>new RebirthMetabolismSnapshot{ProjectedHydration=Projected};
 private static RebirthConsumeResult Fail(RebirthMetabolismConsumeFailure reason,string message)=>new RebirthConsumeResult{Failure=reason,Message=message};
 private static void ConsumeEffect(NpcDeferredKind kind,EntityPlayer player,string item,float value=0,bool drink=false,string context=null){DeferredEffects++;}
 private static void ConsumeNativeEffect(EntityPlayer player,ItemValue item,bool drink){NativeEffects++;}
 private static void MergeOrAddEntry(RebirthMetabolismState state,RebirthIngestionEntry entry){state.Entries.Add(entry);}
 private static void PublishConsumeState(EntityPlayer player){Publications++;}
 // ACTUAL_DRINK_BRANCH
 private static void Need(bool value,string text){if(!value)throw new Exception(text);}
 internal static void Run(){
  for(int mode=0;mode<6;mode++){
   var actor=new EntityPlayer();var state=new RebirthMetabolismState();RebirthMetabolismStateRepository.State=state;
   RebirthConsumableResolver.Definition=new RebirthConsumableDefinition{IsDrink=true,ItemName="drink",ManualSipMl=50,AutoSipMl=20,ReusableContainer=true,ContainerCapacityMl=250,LiquidHydrationYield=1};
   RebirthLiquidContainerService.Normalizations=RebirthConsumptionSourceWriteback.Calls=NativeEffects=DeferredEffects=Publications=0;RebirthConsumptionSourceWriteback.Refuse=mode==3;QueueAllowed=mode!=4;Projected=mode==2?100:0;
   if(mode==0)actor.Source.itemValue.Remaining=0;if(mode==1)state.Fullness=1000;
   var result=ConsumeAt(actor,RebirthMetabolismSourceKind.Backpack,0,actor.Source,mode==2);
   if(mode==5){Need(result.Success&&result.ConsumedMl==50&&actor.Source.itemValue.Normalized&&actor.Source.itemValue.Remaining==100&&RebirthConsumptionSourceWriteback.Calls==1&&state.Entries.Count==1&&NativeEffects==1&&DeferredEffects==3&&Publications==1,"accepted drink must make one raw-before to normalized-after commit");}
   else{Need(!result.Success&&!actor.Source.itemValue.Normalized&&state.Entries.Count==0&&state.Touches==0&&NativeEffects==0&&DeferredEffects==0&&Publications==0,"refused drink mutated source or added effects "+mode);Need(RebirthConsumptionSourceWriteback.Calls==(mode==3?1:0),"normalization reached an early native commit "+mode);}
  }
  Console.WriteLine("PASS actual drink admission branch: refusal leaves raw source unchanged; accepted serving has one final normalized commit. Native owner protocol/effects/persistence NOT_RUN");
 }
}
static class Program{static void Main()=>RebirthMetabolismService.Run();}

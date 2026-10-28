using System;
using System.Collections.Generic;
using System.Linq;

static class Program
{
    static int checks;
    static void Check(bool value,string label){if(!value)throw new Exception(label);checks++;Console.WriteLine("PASS "+label);}
    static EntityPlayer Actor(int type,int count,bool belt=false)
    {
        var player=new EntityPlayer{isEntityRemote=true};player.world=new World();
        var source=new ItemStack(new ItemValue{type=type,Seed=42,Metadata="crafted"},count);
        if(belt)player.inventory.ItemGrid.items[0]=source;else player.bag.ItemGrid.items[0]=source;
        player.EffectOwner=new EntityPlayerLocal();return player;
    }
    static ItemStack Source(EntityPlayer player,bool belt)=>belt?player.inventory.ItemGrid.items[0]:player.bag.ItemGrid.items[0];
    static void Reset(){SingletonMonoBehaviour<ConnectionManager>.Instance=new ConnectionManager{IsServer=true};RebirthCharacterCreationHoldService.Held=false;RebirthBackpackLibraryReservation.Blocked=false;RebirthMetabolismModifierResolver.BeforeResolve=null;RebirthStatisticsService.Calls=0;RebirthTraitSupportService.Calls=0;RebirthMetabolismService.Snapshots=0;}
    static void Refused(EntityPlayer player,bool belt,RebirthMetabolismConsumeFailure expected,string label)
    {
        int count=Source(player,belt).count,entries=player.State.IngestionEntries.Count;
        var result=RebirthMetabolismService.ConsumeMatchingInventoryItem(player,1,42,false);
        Check(!result.Success&&result.Failure==expected,label+" refusal");
        Check(Source(player,belt).count==count&&player.State.IngestionEntries.Count==entries&&player.Events==0&&RebirthStatisticsService.Calls==0&&RebirthMetabolismService.Snapshots==0,label+" no debit/material/effects/snapshot");
    }
    static void Main()
    {
        foreach(bool belt in new[]{false,true})foreach(int count in new[]{1,2})foreach(int type in new[]{1,2})
        {
            Reset();var player=Actor(type,count,belt);
            for(int use=1;use<=count;use++)
            {
                player.ExpectedSourceCount=count-use;player.SourceOnBelt=belt;
                var result=RebirthMetabolismService.ConsumeMatchingInventoryItem(player,type,42,false);
                Check(result.Success&&Source(player,belt).count==count-use,"actual admission/decrement type="+type+" count="+count+" use="+use+" belt="+belt);
                Check(player.State.IngestionEntries.Count==use&&player.State.IngestionEntries.Sum(e=>e.RemainingNutritionUnits)==8*use,"actual queue retains full nutrition once per serving");
                Check(player.Events==use&&RebirthStatisticsService.Calls==use&&RebirthMetabolismService.Snapshots==use&&player.State.Revision==use,"effects/statistics/snapshot/touch exactly once");
                Check(player.Stats.Food.Value==10&&player.Buffs.GetCustomVar("$foodAmountAdd")==0&&!player.MinEventContext.IsLocal,"actual wrapper prevents immediate food and restores event scope");
                Check(count-use==0?Source(player,belt).IsEmpty():Source(player,belt).itemValue.Metadata=="crafted"&&Source(player,belt).itemValue.Seed==42,"final or remaining source identity conserved");
                if(type==2)Check(player.EffectOwner.Buffs.GetCustomVar("$buffInfectionAddCurePerc")==5*use&&player.EffectOwner.Buffs.HasBuff("buffInfectionAddCure"),"remote honey native sync double receives preserved cure");
            }
            var repeated=RebirthMetabolismService.ConsumeMatchingInventoryItem(player,type,42,false);
            Check(!repeated.Success&&player.Events==count&&player.State.IngestionEntries.Count==count,"repeat request after authoritative empty grants nothing");
        }
        Reset();var wrong=Actor(1,2);wrong.bag.ItemGrid.items[0].itemValue.Seed=99;Refused(wrong,false,RebirthMetabolismConsumeFailure.ItemChanged,"strict seed");
        foreach(bool belt in new[]{false,true})
        {Reset();var ignored=Actor(1,2,belt);if(belt)ignored.inventory.IgnoreWrites=true;else ignored.bag.IgnoreWrites=true;Refused(ignored,belt,RebirthMetabolismConsumeFailure.ItemChanged,"ignored native setter "+belt);}
        Reset();var changed=Actor(1,2);RebirthMetabolismModifierResolver.BeforeResolve=p=>p.bag.ItemGrid.items[0].itemValue.Metadata="changed-after-capture";
        Refused(changed,false,RebirthMetabolismConsumeFailure.ItemChanged,"source changes between clone and write");
        Reset();var held=Actor(1,2);RebirthBackpackLibraryReservation.Blocked=true;Refused(held,false,RebirthMetabolismConsumeFailure.ItemChanged,"custody hold");
        Reset();var full=Actor(1,2);full.State.IngestionEntries.Add(new RebirthIngestionEntry{RemainingSolidVolumeMl=990,RemainingNutritionUnits=1});
        Refused(full,false,RebirthMetabolismConsumeFailure.TooFull,"actual stomach fit");
        Reset();var queue=Actor(1,2);for(int i=0;i<RebirthMetabolismConfig.MaxIngestionEntries;i++)queue.State.IngestionEntries.Add(new RebirthIngestionEntry{RemainingSolidVolumeMl=1,RemainingNutritionUnits=1});
        Refused(queue,false,RebirthMetabolismConsumeFailure.IngestionQueueFull,"actual queue capacity");
        Reset();var ownerOnly=Actor(1,2);SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer=false;Refused(ownerOnly,false,RebirthMetabolismConsumeFailure.NotAuthoritative,"client cannot ingest");
        Reset();var creation=Actor(1,2);RebirthCharacterCreationHoldService.Held=true;Refused(creation,false,RebirthMetabolismConsumeFailure.NotAuthoritative,"creation hold");
        Console.WriteLine("PASS "+checks+" actual-source ConsumeAt/admission/queue/source writeback/serving/preserved effects. Native world/item/stats/buff action/config/resolver/repository/modifier/statistics/transport doubles explicit. No native infection tick, codecs, sockets, save or gameplay proof.");
    }
}

public static partial class RebirthMetabolismService
{
    public static bool DebugEnabled;public static int Snapshots;
    // Transport projection and drink-only detailed snapshot are doubles; core admission is generated actual source.
    static void SendSnapshotToOwner(EntityPlayer player,bool force){Snapshots++;if(player.State.IngestionEntries.Count==0)throw new Exception("snapshot preceded admission");}
    static RebirthMetabolismSnapshot BuildSnapshot(EntityPlayer p,RebirthMetabolismState s,RebirthMetabolismModifiers m,params float[] rates)=>new RebirthMetabolismSnapshot();
}
static class RebirthMetabolismStateRepository{public static RebirthMetabolismState GetOrCreate(EntityPlayer p)=>p.State;}
static class RebirthMetabolismModifierResolver{public static Action<EntityPlayer> BeforeResolve;public static RebirthMetabolismModifiers Resolve(EntityPlayer p,RebirthMetabolismState s){BeforeResolve?.Invoke(p);return RebirthMetabolismModifiers.Neutral(1000);}}
static class RebirthConsumableResolver
{
    public static bool TryResolve(ItemValue item,out RebirthConsumableDefinition def){def=null;if(item.type!=1&&item.type!=2)return false;def=new RebirthConsumableDefinition{IsFood=true,IsMetabolismItem=true,Kind=RebirthIngestionKind.Food,ItemName=item.type==2?"foodHoney":"foodCanChili",NutritionUnits=8,EnergyUnits=2,StomachVolumeMl=50,FoodWaterMl=2,DigestionProfile="normal",FoodSafetyProfile="safe"};return true;}
    public static float GetFloat(ItemClass item,string name,float fallback)=>fallback;
}
static class RebirthMetabolismConfig
{
    public const int MaxIngestionEntries=4;
    public const float FoodMinimumFitFraction=.8f,AutoSipDefaultMl=125,AutoSipStopProjectedPercent=.9f,MlPerHydrationPoint=10,
        BaseIntestinalFluidAbsorptionMlPerRealMinute=1,BaseSolidGastricEmptyingMlPerRealMinute=1,BaseIntestinalNutrientAbsorptionUnitsPerRealMinute=1,
        MealFluidHoldRealSeconds=10,ClearLiquidHoldRealSeconds=5,MealFluidGastricHalfTimeRealSeconds=10,
        DigestiveEnzymesRealSeconds=30,ElectrolytesRealSeconds=30,ProbioticsRealSeconds=30,PrebioticRealSeconds=30;
    public static float GetFluidGastricHalfTimeRealSeconds(bool meal)=>10;
    public static float GetFoodGastricLagRealSeconds(string profile)=>10;
    public static float GetDigestionProfileMultiplier(string profile)=>1;
}
static class RebirthLiquidContainerService{public static bool NormalizeState(ItemValue value,RebirthConsumableDefinition def,out float remaining){remaining=100;return false;}public static string GetLiquidProfile(ItemValue value,RebirthConsumableDefinition def)=>"water";public static void SetRemainingMl(ItemValue value,RebirthConsumableDefinition def,float remaining){}}
static class RebirthDrinkPreparationSignatureService{public static float GetPreparedSpecialEffectDurationSeconds(ItemValue value)=>0;}
static class RebirthFoodFarmingButcherySignatureService{public static float GetPreparedMealEnergyEfficiency(ItemValue value)=>1;}
static class RebirthStatisticsService{public static int Calls;public static void RecordConsumption(EntityPlayer p,string item,bool drink,float ml){Calls++;}public static void RecordMetabolismTreatment(EntityPlayer p,string item){Calls++;}}
static class RebirthTraitSupportService{public static int Calls;public static void TryApplyFromItem(EntityPlayer p,string item,string context){Calls++;}}
static class RebirthStressService{public static void Drink(EntityPlayer p,string item,float fraction){}}
static class RebirthCharacterCreationHoldService{public static bool Held;public static bool IsHeld(EntityPlayer p)=>Held;}
static class RebirthBackpackLibraryReservation{public static bool Blocked;public static bool BlocksResourceUse(EntityPlayer p)=>Blocked;}
static class RebirthToolbeltCapacity{public static int GetOwnedSlotCount(EntityPlayer p,int length)=>length;}
public sealed class World{public GameManager gameManager=new GameManager();public ulong GetWorldTime()=>100;}
public sealed class GameManager{public void ItemDropServer(ItemStack stack,UnityEngine.Vector3 position,UnityEngine.Vector3 velocity){}}
public class EntityPlayer
{
    public bool isEntityRemote;public World world;public Bag bag=new Bag();public Inventory inventory=new Inventory();public Stats Stats=new Stats();public Buffs Buffs=new Buffs();public Context MinEventContext=new Context();public RebirthMetabolismState State=new RebirthMetabolismState();public EntityPlayer EffectOwner;public int Events,ExpectedSourceCount;public bool SourceOnBelt;
    public UnityEngine.Vector3 GetPosition()=>UnityEngine.Vector3.zero;
    public void FireEvent(int evt){if((SourceOnBelt?inventory.ItemGrid.items[0]:bag.ItemGrid.items[0]).count!=ExpectedSourceCount)throw new Exception("native effect before committed source");Events++;Stats.Food.Value+=8;Buffs.SetCustomVar("$foodAmountAdd",8);if(MinEventContext.ItemValue.type==2){Buffs.SetCustomVar("$buffInfectionAddCurePerc",Buffs.GetCustomVar("$buffInfectionAddCurePerc")+5);if(MinEventContext.IsLocal&&EffectOwner!=null){EffectOwner.Buffs.SetCustomVar("$buffInfectionAddCurePerc",EffectOwner.Buffs.GetCustomVar("$buffInfectionAddCurePerc")+5);EffectOwner.Buffs.AddBuff("buffInfectionAddCure");}}}
}
public sealed class EntityPlayerLocal:EntityPlayer{public Stealth Stealth=new Stealth();}
public sealed class Stealth{public void SetSmellEat(float amount){}}
public sealed class Context{public bool IsLocal;public ItemValue ItemValue;}
public sealed class Stats{public Stat Food=new Stat{Value=10},Water=new Stat{Value=20};}
public sealed class Stat{public float Value,ModifiedMax=100;}
public sealed class Buffs{readonly Dictionary<string,float> values=new Dictionary<string,float>();readonly HashSet<string> active=new HashSet<string>();public float GetCustomVar(string name)=>values.TryGetValue(name,out var value)?value:0;public void SetCustomVar(string name,float value)=>values[name]=value;public void AddBuff(string name)=>active.Add(name);public bool HasBuff(string name)=>active.Contains(name);}
public sealed class Grid{public ItemStack[] items=new[]{ItemStack.Empty.Clone()};}
public sealed class Bag{public Grid ItemGrid=new Grid();public bool IgnoreWrites;public void SetSlot(int index,ItemStack stack){if(!IgnoreWrites)ItemGrid.items[index]=stack.Clone();}public bool AddItem(ItemStack stack)=>false;}
public sealed class Inventory{public Grid ItemGrid=new Grid();public bool IgnoreWrites;public int holdingItemIdx;public void SetItem(int index,ItemStack stack){if(!IgnoreWrites)ItemGrid.items[index]=stack.Clone();}public bool AddItem(ItemStack stack)=>false;}
public sealed class ItemValue{public int type;public ushort Seed;public string Metadata="";public ItemClass ItemClass=new ItemClass();public ItemValue Clone()=>new ItemValue{type=type,Seed=Seed,Metadata=Metadata};public override bool Equals(object value)=>value is ItemValue other&&type==other.type&&Seed==other.Seed&&Metadata==other.Metadata;public override int GetHashCode()=>HashCode.Combine(type,Seed,Metadata);}
public sealed class ItemClass{public object[] Actions=Array.Empty<object>();public static ItemValue GetItem(string name)=>new ItemValue();}
public sealed class ItemActionEat{public float smellUse;}
public sealed class ItemStack{public static readonly ItemStack Empty=new ItemStack(new ItemValue(),0);public ItemValue itemValue;public int count;public ItemStack(ItemValue value,int amount){itemValue=value;count=amount;}public bool IsEmpty()=>count<=0||itemValue.type==0;public ItemStack Clone()=>new ItemStack(itemValue.Clone(),count);}
public sealed class ConnectionManager{public bool IsServer;}
static class SingletonMonoBehaviour<T>{public static T Instance;}
static class QuestEventManager{public static QuestEvents Current=new QuestEvents();}
sealed class QuestEvents{public void UsedItem(ItemValue item){}}
static class MinEvent{public static int[] End={1};}
static class Log{public static void Warning(string message){}public static void Out(string message){}}
namespace UnityEngine
{
    public struct Vector3{public static Vector3 zero=>new Vector3();}
    public static class Mathf{public static float Max(float a,float b)=>Math.Max(a,b);public static float Min(params float[] values)=>values.Min();public static float Clamp01(float value)=>Math.Clamp(value,0,1);}
}

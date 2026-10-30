using System;
using System.Collections.Generic;
using System.Linq;

// Actual complete production Count/Take/Compatible/FullIngredient linked in
// csproj. Native grid/setter/liquid definitions/provenance/Harmony are doubles.
public static class Program
{
    private static int passed;
    private static void Check(bool value,string name){if(!value)throw new Exception("FAIL "+name);passed++;}
    private static ItemValue Request(int type,int marker=1){var v=new ItemValue(type);v.SetMetadata("rebirth.cooking.fullIngredient",marker);return v;}
    private static EntityPlayer Player(ItemStack bag,ItemStack belt=null)
        =>new EntityPlayer{bag=new Bag{ItemGrid=new Grid{items=new[]{bag}}},inventory=new Inventory{ItemGrid=new Grid{items=new[]{belt??ItemStack.Empty.Clone()}}}};
    public static void Main()
    {
        var request=Request(1);var physical=new ItemValue(1){Seed=73};
        Check(!RebirthCookingItemStats.Compatible(physical,request),"actual liquid comparator reproduces request marker mismatch");
        var player=Player(new ItemStack(physical,2));var removed=new List<ItemStack>();var receipts=new List<RemoteResourceTransactions.ReturnReceipt>();
        Check(RebirthCookingLocalIngredients.Count(player,request)==2,"full fresh water count ignores required request marker");
        Check(RebirthCookingLocalIngredients.Take(player,new List<ItemStack>{new ItemStack(request,1)},removed,receipts),"full water request taken");
        Check(player.bag.ItemGrid.items[0].count==1&&removed.Single().count==1,"exact count debit/fragment");
        Check(removed[0].itemValue.Seed==73&&player.bag.ItemGrid.items[0].itemValue.Seed==73,"physical seed retained");
        Check(!physical.HasMetadata("rebirth.cooking.fullIngredient")&&!removed[0].itemValue.HasMetadata("rebirth.cooking.fullIngredient"),"request marker never materialized on actual source");
        Check(request.TryGetMetadata("rebirth.cooking.fullIngredient",out int marker)&&marker==1,"original requirement unchanged");
        Check(receipts.Count==1&&receipts[0].Slot==0&&!receipts[0].Toolbelt&&receipts[0].Item.itemValue.Seed==73,"return provenance retained");

        var normalized=new ItemValue(1);normalized.SetMetadata(RebirthLiquidContainerService.VolumeKey,250f);normalized.SetMetadata(RebirthLiquidContainerService.VersionKey,1);
        player=Player(new ItemStack(normalized,1));removed.Clear();
        Check(RebirthCookingLocalIngredients.Count(player,request)==1,"effective full normalized water admitted");
        Check(RebirthCookingLocalIngredients.Take(player,new List<ItemStack>{new ItemStack(request,1)},removed),"normalized full water taken");
        Check(player.bag.ItemGrid.items[0].IsEmpty()&&removed.Single().itemValue.HasMetadata(RebirthLiquidContainerService.VolumeKey),"final full serving native payload preserved");

        var initializedMurky=normalized.Clone();initializedMurky.SetMetadata(RebirthLiquidContainerService.MurkyFullInitializationKey,1);
        player=Player(new ItemStack(initializedMurky,1));removed.Clear();
        Check(RebirthCookingLocalIngredients.Count(player,request)==1,"technical murky initialization marker does not prevent full ingredient count");
        Check(RebirthCookingLocalIngredients.Take(player,new List<ItemStack>{new ItemStack(request,1)},removed),"initialized full murky matches unmarked required clone");
        Check(removed.Single().itemValue.TryGetMetadata(RebirthLiquidContainerService.MurkyFullInitializationKey,out int initialized)&&initialized==1,
            "actual murky initialization state retained on fragment");
        var depletedMurky=initializedMurky.Clone();depletedMurky.SetMetadata(RebirthLiquidContainerService.VolumeKey,100f);
        player=Player(new ItemStack(depletedMurky,1));removed.Clear();
        Check(RebirthCookingLocalIngredients.Count(player,request)==0,"depleted marked murky remains unavailable");
        Check(!RebirthCookingLocalIngredients.Take(player,new List<ItemStack>{new ItemStack(request,1)},removed)&&removed.Count==0&&player.bag.ItemGrid.items[0].count==1,
            "depleted marked murky refuses without debit");

        var partial=new ItemValue(1);partial.SetMetadata(RebirthLiquidContainerService.VolumeKey,100f);
        var partialRequest=Request(1);partialRequest.SetMetadata(RebirthLiquidContainerService.VolumeKey,100f);
        player=Player(new ItemStack(partial,1));removed.Clear();
        Check(RebirthCookingLocalIngredients.Count(player,partialRequest)==0,"fullness requirement remains even matched partial metadata");
        Check(!RebirthCookingLocalIngredients.Take(player,new List<ItemStack>{new ItemStack(partialRequest,1)},removed),"partial bottle refused");
        Check(removed.Count==0&&player.bag.ItemGrid.items[0].count==1,"partial refusal no debit");

        var prepared=new ItemValue(1);prepared.SetMetadata("prepared.native.signature","different");
        player=Player(new ItemStack(prepared,1));removed.Clear();
        Check(RebirthCookingLocalIngredients.Count(player,request)==0,"other physical liquid provenance still compared");
        Check(!RebirthCookingLocalIngredients.Take(player,new List<ItemStack>{new ItemStack(request,1)},removed)&&removed.Count==0,"prepared mismatch refusal");
        player=Player(new ItemStack(new ItemValue(1),1));
        Check(RebirthCookingLocalIngredients.Count(player,Request(1,2))==0,"malformed marker not removed");

        // A food plus water meal uses the same all-demand preflight: one water
        // mismatch previously prevented every fetched ingredient from moving.
        var food=new ItemValue(2){Seed=99};food.SetMetadata("physical.custom","preserve");
        player=Player(new ItemStack(food,2),new ItemStack(new ItemValue(1){Seed=101},1));removed.Clear();receipts.Clear();
        Check(RebirthCookingLocalIngredients.Count(player,Request(2))==2,"food count unchanged by request marker");
        var needs=new List<ItemStack>{new ItemStack(Request(2),1),new ItemStack(Request(1),1)};
        Check(RebirthCookingLocalIngredients.Take(player,needs,removed,receipts),"mixed food/water request completes");
        Check(player.bag.ItemGrid.items[0].count==1&&player.inventory.ItemGrid.items[0].IsEmpty(),"bag food and belt water debited exactly once");
        Check(removed.Count==2&&removed[0].itemValue.TryGetMetadata("physical.custom",out string custom)&&custom=="preserve"&&removed[1].itemValue.Seed==101,"all actual fragment identities retained");
        Check(receipts.Count==2&&receipts[1].Toolbelt,"belt return origin retained");
        Check(!RebirthCookingLocalIngredients.Take(player,needs,new List<ItemStack>())&&player.bag.ItemGrid.items[0].count==1,"insufficient repeat does not debit remaining food");
        player=Player(new ItemStack(new ItemValue(1),1));removed.Clear();
        Check(!RebirthCookingLocalIngredients.Take(player,new List<ItemStack>{new ItemStack(request,1),new ItemStack(request.Clone(),1)},removed)&&removed.Count==0,"duplicate demand aggregation preflights complete total");
        player.Busy=true;
        Check(RebirthCookingLocalIngredients.Count(player,request)==0&&!RebirthCookingLocalIngredients.Take(player,new List<ItemStack>{new ItemStack(request,1)},removed),"existing custody hold still refuses");

        // Plain-food-only is unaffected by liquid comparator fix. Simulate its
        // once-returned owner items being blocked by an independent custody hold.
        player=Player(new ItemStack(new ItemValue(2){Seed=313},2));removed.Clear();
        var foodNeeds=new List<ItemStack>{new ItemStack(Request(2),1)};
        Check(RebirthCookingLocalIngredients.Count(player,foodNeeds[0].itemValue)==2,"plain food-only marker count");
        player.Busy=true;bool scope=true;int placements=0;
        var continuation=new RebirthCookingPullContinuation(()=>scope,()=>player.Busy,()=>
        {placements++;Check(RebirthCookingLocalIngredients.Take(player,foodNeeds,removed),"plain food local take after settlement");});
        Check(continuation.Advance()==RebirthCookingPullContinuation.Outcome.Waiting&&placements==0&&player.bag.ItemGrid.items[0].count==2,"actual busy receipt waits without local debit");
        player.Busy=false;
        Check(continuation.Advance()==RebirthCookingPullContinuation.Outcome.Completed&&placements==1&&player.bag.ItemGrid.items[0].count==1&&removed.Single().itemValue.Seed==313,"settled food-only placement once with physical identity");
        continuation.Advance();
        Check(placements==1&&removed.Count==1&&player.bag.ItemGrid.items[0].count==1,"repeated Update cannot repeat debit/placement");
        scope=true;player.Busy=true;
        var stale=new RebirthCookingPullContinuation(()=>scope,()=>player.Busy,()=>placements++);
        stale.Advance();scope=false;player.Busy=false;
        Check(stale.Advance()==RebirthCookingPullContinuation.Outcome.ScopeChanged&&placements==1&&player.bag.ItemGrid.items[0].count==1,"changed UI scope discards continuation only");
        scope=true;
        var reentrant=new RebirthCookingPullContinuation(()=>scope,()=>{scope=false;return false;},()=>placements++);
        Check(reentrant.Advance()==RebirthCookingPullContinuation.Outcome.ScopeChanged&&placements==1,"changed scope during settlement guard is fenced");
        var throws=new RebirthCookingPullContinuation(()=>true,()=>false,()=>{placements++;throw new Exception("explicit setter double failure");});
        try{throws.Advance();}catch(Exception){}
        throws.Advance();
        Check(placements==2&&!throws.IsPending,"throwing placement cannot replay");
        Console.WriteLine("PASS "+passed+" actual-source identity/admission assertions; native runtime and multiplayer NOT qualified");
    }
}

public sealed class TypedMetadataValue{private readonly object value;public TypedMetadataValue(object v){value=v;}public object GetValue()=>value;}
public sealed class ItemValue
{
    public int type,Seed,Meta,Quality,SelectedAmmoTypeIndex;public bool Activated;
    public ItemValue[] modifications,cosmeticMods;public Dictionary<string,TypedMetadataValue> Metadata=new Dictionary<string,TypedMetadataValue>();
    public ItemClass ItemClass=>new ItemClass();public ItemValue(int t=0){type=t;}public bool IsEmpty()=>type==0;
    public bool HasMetadata(string name)=>Metadata.ContainsKey(name);
    public bool TryGetMetadata<T>(string name,out T value){value=default;if(!Metadata.TryGetValue(name,out var v)||!(v.GetValue() is T typed))return false;value=typed;return true;}
    public void SetMetadata(string name,object value)=>Metadata[name]=new TypedMetadataValue(value);
    public ItemValue Clone(){var v=(ItemValue)MemberwiseClone();v.Metadata=Metadata.ToDictionary(p=>p.Key,p=>new TypedMetadataValue(p.Value.GetValue()));v.modifications=modifications?.Select(m=>m?.Clone()).ToArray();v.cosmeticMods=cosmeticMods?.Select(m=>m?.Clone()).ToArray();return v;}
    public bool EqualsForMerging(ItemValue other)=>other?.type==type;
}
public sealed class ItemClass{public bool HasQuality;public string GetItemName()=>"double";public static ItemValue GetItem(string name)=>new ItemValue(2);}
public sealed class ItemStack
{
    public int count;public ItemValue itemValue;public ItemStack(ItemValue v,int n){itemValue=v;count=n;}
    public static readonly ItemStack Empty=new ItemStack(new ItemValue(),0);public bool IsEmpty()=>count<=0||itemValue.IsEmpty();
    public static ItemStack[] Clone(ItemStack[] values)=>values.Select(v=>v?.Clone()).ToArray();
    public ItemStack Clone()=>new ItemStack(itemValue.Clone(),count);public bool CanStackWith(ItemStack other,bool limit)=>other?.itemValue.type==itemValue.type;
}
public sealed class Grid{public ItemStack[] items;}
public class Bag{public Grid ItemGrid;public void SetSlot(int slot,ItemStack item)=>ItemGrid.items[slot]=item;}
public sealed class Inventory:Bag{public void SetItem(int slot,ItemStack item)=>ItemGrid.items[slot]=item;}
public class EntityPlayer{public Bag bag;public Inventory inventory;public bool Busy;public World world=new World();}
public sealed class World{public bool IsRemote()=>false;}
public sealed class EntityPlayerLocal:EntityPlayer{public PlayerUi PlayerUI;}
public sealed class PlayerUi{public Xui xui;}
public sealed class Xui{public InventoryModel PlayerInventory;}
public sealed class InventoryModel{public void SetBackpackItemStacks(ItemStack[] items){}public void SetToolbeltItemStacks(ItemStack[] items){}}
public static class RebirthBackpackLibraryReservation{public static bool BlocksResourceUse(EntityPlayer player)=>player.Busy;}
public static class RebirthToolbeltCapacity{public static int GetOwnedSlotCount(EntityPlayer player,int length)=>length;}
public static class RemoteResourceTransactions{public sealed class ReturnReceipt{public ReturnReceipt(EntityPlayer p){}public bool Toolbelt;public int Slot,Remaining;public ItemStack Item;}}
public static class RebirthCookingMergeScope{public static bool NeedsCheck(ItemValue item)=>RebirthCookingItemStats.NeedsStateAwareStacking(item);}
public sealed class RebirthConsumableDefinition{public bool IsDrink;public float ContainerCapacityMl=250,InitialVolumeMl=250,NutritionUnits,EnergyUnits,ManualSipMl=125,LiquidHydrationYield=1,FoodWaterMl;}
public static class RebirthConsumableResolver{public static bool TryResolve(ItemValue v,out RebirthConsumableDefinition d){d=new RebirthConsumableDefinition{IsDrink=v.type==1};return v.type!=0;}}
public static class RebirthLiquidContainerService
{
    public const string VolumeKey="native.volume",ProfileKey="native.profile",VersionKey="native.version";
    public const string MurkyFullInitializationKey="RebirthMetMurkyFullInitialized";
    public static float GetRemainingMl(ItemValue v,RebirthConsumableDefinition d)=>v.TryGetMetadata(VolumeKey,out float volume)?volume:d.ContainerCapacityMl;
    public static string GetLiquidProfile(ItemValue v,RebirthConsumableDefinition d)=>"doubleWater";
}
public sealed class RebirthFoodMoodDefinition{public float BaseMoodInfluence;public string VarietyFamilyId,MoodProfileId;public HashSet<string> DietTags=new HashSet<string>();}
public static class RebirthFoodMoodResolver{public static bool TryResolve(ItemValue v,out RebirthFoodMoodDefinition d){d=null;return false;}}
public static class RebirthContainerPackUp{public static bool IsPacked(ItemValue v)=>false;}
public static class RebirthItemProvenanceAdapter{public static bool AreStackCompatible(ItemValue a,ItemValue b)=>true;}
public static class RebirthElectricalItemProvenance{public static bool AreStackCompatible(ItemValue a,ItemValue b)=>true;}
public static class RebirthLiquidStackCapacityPatch{public static void Apply(ItemClass c){}}
public sealed class XUiC_ItemActionList{public void AddActionActions(){}}
namespace HarmonyLib
{
    [AttributeUsage(AttributeTargets.Class)]public sealed class HarmonyPatch:Attribute{public HarmonyPatch(Type t,string name){}public HarmonyPatch(Type t,string name,Type[] args){}}
    [AttributeUsage(AttributeTargets.Method)]public sealed class HarmonyPrefix:Attribute{}
    [AttributeUsage(AttributeTargets.Method)]public sealed class HarmonyPostfix:Attribute{}
}

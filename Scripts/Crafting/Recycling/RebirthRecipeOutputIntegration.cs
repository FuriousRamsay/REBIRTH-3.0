using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;

// Additional outputs belong to the same native completion application as the
// primary. They never create another recipe, payment, XP or completion callback.
public static class RebirthRecipeOutputIntegration
{
    internal const string ReceiptField="outputs";
    private sealed class NativeScope {internal TileEntityWorkstation Tile;internal NativeScope Previous;}
    private sealed class UiScope {internal XUiC_RecipeStack Card;internal XUiC_WorkstationOutputGrid Grid;internal IReadOnlyList<ItemStack> Outputs;internal UiScope Previous;internal bool Inserted;}
    [ThreadStatic] private static NativeScope native;
    [ThreadStatic] private static UiScope ui;
    private static bool installed;
    public static void Install()
    {
        if(installed)return;var harmony=new Harmony("rebirth.recipe.additional.outputs");
        foreach(var type in new[]{typeof(RebirthRecipeOutputNativeScope),typeof(RebirthRecipeOutputNativeInsert),typeof(RebirthRecipeOutputUiScope),typeof(RebirthRecipeOutputUiInsert),typeof(RebirthRecipeOutputInventoryInsert),typeof(RebirthRecipeOutputUiRetryScope),typeof(RebirthRecipeOutputUiUpdateScope)})
            RebirthHarmonyBootstrap.PatchClassOnce(harmony,type);
        installed=true;
    }
    internal static object EnterNative(TileEntityWorkstation station)
    {var scope=new NativeScope {Tile=station,Previous=native};native=scope;return scope;}
    internal static void LeaveNative(object identity)
    {if(identity is NativeScope scope&&ReferenceEquals(native,scope))native=scope.Previous;}
    // Detached, read-only prediction of the exact supplied native AddToItemStackArray implementation.
    internal static bool Predict(ItemStack[] cells,ItemStack primary,int maxItemCount,IReadOnlyList<ItemStack> outputs,out ItemStack[] after,out int destination)
    {
        after=null;destination=-1;if(cells==null||primary?.itemValue==null||primary.count<1)return false;
        var copy=ItemStack.Clone(cells);
        if(!Insert(copy,primary,maxItemCount,out destination))return false;
        foreach(var extra in outputs)if(!Insert(copy,extra,-1,out _))return false;
        after=copy;return true;
    }
    private static bool Insert(ItemStack[] cells,ItemStack incoming,int maximum,out int destination)
    {
        destination=-1;if(incoming?.itemValue==null||incoming.count<1)return false;
        for(int i=0;i<cells.Length;i++)if(cells[i].CanStackWith(incoming))
        {long total=(long)cells[i].count+incoming.count;if(total>int.MaxValue)return false;cells[i]=new ItemStack(cells[i].itemValue.Clone(),(int)total);destination=i;return true;}
        for(int i=0;i<cells.Length&&(maximum==-1||i!=maximum);i++)if(cells[i].IsEmpty())
        {cells[i]=incoming.Clone();destination=i;return true;}
        return false;
    }
    internal static bool NativeInsert(ItemStack[] cells,ItemStack primary,int maximum,ref int result)
    {
        var station=native?.Tile;var recipe=station?.Queue?.LastOrDefault()?.Recipe;
        if(station==null||!ReferenceEquals(cells,station.Output)||primary?.itemValue?.type!=recipe?.itemValueType
            ||primary.count!=recipe.count||!RebirthRecipeOutputs.HasOutputs(recipe))return true;
        if(!RebirthRecipeOutputs.TryPlan(recipe,1,out var outputs,out _)
            ||!Predict(cells,primary,maximum,outputs,out var after,out var index))
        {result=-1;return false;}
        bool stacked=cells[index].CanStackWith(primary);
        // Native signatures may decorate the primary ItemValue in AddCraftComplete.
        // Retain its exact reference when the primary occupies a new slot.
        if(!stacked)after[index].itemValue=primary.itemValue;
        for(int i=0;i<cells.Length;i++)cells[i]=after[i];
        if(stacked)primary.count=0;
        result=index;return false;
    }
    internal static bool StampReceipt(ItemValue receiptValue,Recipe recipe)
    {
        if(!RebirthRecipeOutputs.HasOutputs(recipe))return true;
        if(!RebirthRecipeOutputs.TryPlan(recipe,1,out var outputs,out _))return false;
        receiptValue.SetMetadata(RebirthStationCompletionReceipt.Prefix+ReceiptField,
            Convert.ToBase64String(RebirthCamperInventorySnapshot.Capture(outputs.Select(stack=>stack.Clone()).ToArray())));
        return true;
    }
    internal static bool ReadReceipt(CraftCompleteData receipt,out IReadOnlyList<ItemStack> outputs)
    {
        outputs=new List<ItemStack>();var value=receipt?.CraftedItemStack?.itemValue;
        if(value==null)return false;
        if(!value.HasMetadata(RebirthStationCompletionReceipt.Prefix+ReceiptField))return true; // Earlier receipts retain their exact primary-only semantics.
        if(!value.TryGetMetadata(RebirthStationCompletionReceipt.Prefix+ReceiptField,out string image)||image.Length>1048576)return false;
        try
        {
            var items=RebirthCamperInventorySnapshot.Restore(Convert.FromBase64String(image));
            if(items.Length<1||items.Length>64||items.Any(stack=>stack==null||stack.IsEmpty()||stack.count<1))return false;
            if(Convert.ToBase64String(RebirthCamperInventorySnapshot.Capture(items))!=image)return false;
            outputs=items;return true;
        }
        catch{return false;}
    }
    internal static bool BeginUi(XUiC_RecipeStack card,ref bool result,out object identity)
    {
        identity=null;var recipe=card?.GetRecipe();
        if(!RebirthRecipeOutputs.HasOutputs(recipe)||card.AmountToRepair>0)return true;
        var station=card.GetParentByType<XUiC_WorkstationWindowGroup>()?.WorkstationData?.TileEntity;
        // Mobile UI cards remain projections; the mobile native clock uses NativeInsert.
        if(station is RebirthCamperStationState){result=false;return false;}
        var grid=card.GetParentByType<XUiC_WorkstationWindowGroup>()?.GetChildByType<XUiC_WorkstationOutputGrid>();
        if(!RebirthRecipeOutputs.TryPlan(recipe,1,out var extras,out _))
        {result=false;return false;}
        // giveExp runs before native UI insertion and can decorate the primary.
        // Reserve one slot for that exact future value, then every additional output,
        // so a failed capacity check cannot grant XP or partially deliver.
        var primary=new ItemStack(new ItemValue(recipe.itemValueType,card.outputQuality,card.outputQuality),recipe.count);
        primary.itemValue.SetMetadata("rebirth.output.capacity.reservation","primary");
        if(grid!=null)
        {if(!Predict(grid.GetSlots(),primary,-1,extras,out _,out _)){result=false;return false;}}
        else if(!PlanInventory(card,primary,extras,out _,out _)){result=false;return false;}
        var scope=new UiScope {Card=card,Grid=grid,Outputs=extras,Previous=ui};ui=scope;identity=scope;return true;
    }
    internal static void UiInsert(XUiC_WorkstationOutputGrid grid,ref ItemStack[] cells)
    {
        var scope=ui;
        if(scope==null||scope.Inserted||!ReferenceEquals(scope.Grid,grid))return;
        var after=ItemStack.Clone(cells);
        foreach(var output in scope.Outputs)if(!Insert(after,output,-1,out _))
            throw new InvalidOperationException("Additional recipe output capacity changed inside native completion.");
        cells=after;grid.SetSlots(after);scope.Inserted=true;
    }
    private static bool PlanInventory(XUiC_RecipeStack card,ItemStack primary,IReadOnlyList<ItemStack> extras,out ItemStack[] bag,out ItemStack[] belt)
    {
        bag=null;belt=null;var actor=card?.xui?.playerUI?.entityPlayer;
        if(actor==null||actor.IsDead()||RebirthCamperStationNativeActorPayment.Blocks(actor.bag)||RebirthCamperStationNativeActorPayment.Blocks(actor.inventory)
            ||RebirthBackpackLibraryReservation.IsHeld(actor)||RebirthGearOwnerReservation.IsHeld(actor)
            ||RemoteResourceClientTransactionCoordinator.HasPendingInventoryOperation(actor))return false;
        var items=new List<ItemStack>{primary};foreach(var item in extras)items.Add(item.Clone());
        return RebirthRecyclerOwnerDelivery.Plan(actor.bag.ItemGrid.items,actor.inventory.ItemGrid.items,
            RebirthCamperStationNativeActorPayment.Locks(actor.bag.LockedSlots),RebirthCamperStationNativeActorPayment.Locks(actor.inventory.ItemGrid.SlotLocks),
            actor.inventory.Length,items,out bag,out belt);
    }
    internal static bool InventoryInsert(XUiM_PlayerInventory inventory,ItemStack primary,ref bool result)
    {
        var scope=ui;
        if(scope==null||scope.Inserted||scope.Grid!=null||!ReferenceEquals(scope.Card.xui.PlayerInventory,inventory)
            ||primary?.itemValue?.type!=scope.Card.GetRecipe()?.itemValueType)return true;
        if(!PlanInventory(scope.Card,primary,scope.Outputs,out var bag,out var belt))
        {result=false;return false;}
        // Keep native owner insertion/callbacks and remote owner publication.
        // The synchronous preflight reserves the complete set before any write.
        scope.Inserted=true;
        if(!inventory.AddItemNoPartial(primary,false))throw new InvalidOperationException("Native primary insertion contradicted combined output preflight.");
        foreach(var extra in scope.Outputs)if(!inventory.AddItemNoPartial(extra.Clone(),false))
            throw new InvalidOperationException("Native additional output insertion contradicted combined output preflight.");
        primary.count=0;result=true;return false;
    }
    internal static bool BeginRetry(XUiC_RecipeStack card,ref bool result,out object identity)
    {
        identity=null;if(ui!=null)return true;
        if(!RebirthRecipeOutputs.HasOutputs(card?.GetRecipe()))return true;
        if(!RebirthRecipeOutputs.TryPlan(card.GetRecipe(),1,out var outputs,out _)||!PlanInventory(card,new ItemStack(card.outputItemValue,card.GetRecipe().count),outputs,out _,out _))
        {result=false;return false;}
        var scope=new UiScope {Card=card,Outputs=outputs,Previous=ui};ui=scope;identity=scope;return true;
    }
    internal static bool BeginUpdateRetry(XUiC_RecipeStack card,out object identity)
    {
        identity=null;if(!card.isInventoryFull||!RebirthRecipeOutputs.HasOutputs(card.GetRecipe()))return true;
        if(card.GetParentByType<XUiC_WorkstationWindowGroup>()?.WorkstationData?.TileEntity is RebirthCamperStationState)return false;
        if(!RebirthRecipeOutputs.TryPlan(card.GetRecipe(),1,out var outputs,out _))return false;
        var grid=card.GetParentByType<XUiC_WorkstationWindowGroup>()?.GetChildByType<XUiC_WorkstationOutputGrid>();
        var primary=new ItemStack(card.outputItemValue,card.GetRecipe().count);
        if(grid!=null)
        {if(!Predict(grid.GetSlots(),primary,-1,outputs,out _,out _))return false;}
        else if(!PlanInventory(card,primary,outputs,out _,out _))return false;
        var scope=new UiScope {Card=card,Grid=grid,Outputs=outputs,Previous=ui};ui=scope;identity=scope;return true;
    }
    internal static void EndUi(object identity)
    {if(identity is UiScope scope&&ReferenceEquals(ui,scope))ui=scope.Previous;}
}

[HarmonyPatch(typeof(TileEntityWorkstation),"HandleRecipeQueue")]
internal static class RebirthRecipeOutputNativeScope
{
    private static void Prefix(TileEntityWorkstation __instance,out object __state)=>__state=RebirthRecipeOutputIntegration.EnterNative(__instance);
    private static Exception Finalizer(Exception __exception,object __state){RebirthRecipeOutputIntegration.LeaveNative(__state);return __exception;}
}
[HarmonyPatch(typeof(ItemStack),nameof(ItemStack.AddToItemStackArray),new[]{typeof(ItemStack[]),typeof(ItemStack),typeof(int)})]
internal static class RebirthRecipeOutputNativeInsert
{
    [HarmonyPriority(1000)]
    private static bool Prefix(ItemStack[] __0,ItemStack __1,int __2,ref int __result)
        =>RebirthRecipeOutputIntegration.NativeInsert(__0,__1,__2,ref __result);
}
[HarmonyPatch(typeof(XUiC_RecipeStack),"outputStack")]
internal static class RebirthRecipeOutputUiScope
{
    [HarmonyPriority(1000)]
    private static bool Prefix(XUiC_RecipeStack __instance,ref bool __result,out object __state)
        =>RebirthRecipeOutputIntegration.BeginUi(__instance,ref __result,out __state);
    private static Exception Finalizer(Exception __exception,object __state){RebirthRecipeOutputIntegration.EndUi(__state);return __exception;}
}
[HarmonyPatch(typeof(XUiC_WorkstationOutputGrid),nameof(XUiC_WorkstationOutputGrid.UpdateData))]
internal static class RebirthRecipeOutputUiInsert
{
    private static void Prefix(XUiC_WorkstationOutputGrid __instance,ref ItemStack[] __0)=>RebirthRecipeOutputIntegration.UiInsert(__instance,ref __0);
}
[HarmonyPatch(typeof(XUiM_PlayerInventory),nameof(XUiM_PlayerInventory.AddItemNoPartial),new[]{typeof(ItemStack),typeof(bool)})]
internal static class RebirthRecipeOutputInventoryInsert
{
    [HarmonyPriority(1000)]
    private static bool Prefix(XUiM_PlayerInventory __instance,ItemStack __0,ref bool __result)
        =>RebirthRecipeOutputIntegration.InventoryInsert(__instance,__0,ref __result);
}
[HarmonyPatch(typeof(XUiC_RecipeStack),nameof(XUiC_RecipeStack.AddItemToInventory))]
internal static class RebirthRecipeOutputUiRetryScope
{
    [HarmonyPriority(1000)]
    private static bool Prefix(XUiC_RecipeStack __instance,ref bool __result,out object __state)
        =>RebirthRecipeOutputIntegration.BeginRetry(__instance,ref __result,out __state);
    private static Exception Finalizer(Exception __exception,object __state){RebirthRecipeOutputIntegration.EndUi(__state);return __exception;}
}
[HarmonyPatch(typeof(XUiC_RecipeStack),nameof(XUiC_RecipeStack.Update))]
internal static class RebirthRecipeOutputUiUpdateScope
{
    [HarmonyPriority(1000)]
    private static bool Prefix(XUiC_RecipeStack __instance,out object __state)
        =>RebirthRecipeOutputIntegration.BeginUpdateRetry(__instance,out __state);
    private static Exception Finalizer(Exception __exception,object __state){RebirthRecipeOutputIntegration.EndUi(__state);return __exception;}
}

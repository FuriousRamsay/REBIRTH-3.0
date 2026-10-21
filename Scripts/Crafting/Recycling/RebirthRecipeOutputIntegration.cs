using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;

// Additional outputs belong to the same native completion application as the
// primary. They never create another recipe, payment, XP or completion callback.
public static partial class RebirthRecipeOutputIntegration
{
    internal const string ReceiptField="outputs";
    private sealed class NativeScope {internal TileEntityWorkstation Tile;internal NativeScope Previous;}
    private sealed class UiScope {internal XUiC_RecipeStack Card;internal XUiC_WorkstationOutputGrid Grid;internal IReadOnlyList<ItemStack> Outputs;internal UiScope Previous;internal bool Inserted,Cached,CachedContinuation,CachedPrimaryConsumed;internal ItemStack[] PreparedArgument;internal UiOutputOriginal Original;}
    [ThreadStatic] private static NativeScope native;
    [ThreadStatic] private static UiScope ui;
    private static bool installed;
    public static void Install()
    {
        if(installed)return;var harmony=new Harmony("rebirth.recipe.additional.outputs");
        foreach(var type in new[]{typeof(RebirthRecipeOutputNativeScope),typeof(RebirthRecipeOutputNativeInsert),typeof(RebirthRecipeOutputUiScope),typeof(RebirthRecipeOutputUiPrepareSlots),typeof(RebirthRecipeOutputUiInsert),typeof(RebirthRecipeOutputInventoryInsert),typeof(RebirthRecipeOutputUiRetryScope),typeof(RebirthRecipeOutputUiUpdateScope)})
            RebirthHarmonyBootstrap.PatchClassOnce(harmony,type);
        RebirthWorkstationCompletionPipeline.InstallNativeViewerEffectObserver(harmony);
        RebirthProductiveEarnedUiDeferredTransport.InstallAuthorityNativeReceiptObserver(harmony);
        RebirthHarmonyBootstrap.PatchClassOnce(harmony,typeof(RebirthRecipeOutputBeforeExperience));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony,typeof(RebirthRecipeOutputBeforeDeferredCompletion));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony,typeof(RebirthRecipeOutputPersonalCraftedItem));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony,typeof(RebirthRecipeOutputPersonalScrappedItem));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony,typeof(RebirthRecipeOutputScrapUnlock));
        RebirthProductiveEarnedNativeBody.Install();
        RebirthRemoteResourcePlacedStationOriginal.InstallCompletionCachedNativeLoadObservers();
        ModEvents.GameUpdate.RegisterHandler(PumpPersonalOutputQueues);
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
    // Native cached Update scans once: an earlier empty slot wins over a
    // later stack. Fresh outputStack scans all stackable slots first.
    internal static bool PredictOwningOutput(ItemStack[] cells,ItemStack primary,IReadOnlyList<ItemStack> extras,bool cached,out ItemStack[] after)
    {
        after=null;if(!cached)return Predict(cells,primary,-1,extras,out after,out _);
        if(cells==null||primary?.itemValue==null||primary.count<1)return false;
        var copy=ItemStack.Clone(cells);bool inserted=false;
        for(int i=0;i<copy.Length;i++)
        {
            if(copy[i].CanStackWith(primary))
            {long count=(long)copy[i].count+primary.count;if(count>int.MaxValue)return false;copy[i].count=(int)count;inserted=true;break;}
            if(copy[i].IsEmpty()){copy[i]=primary.Clone();inserted=true;break;}
        }
        if(!inserted)return false;
        foreach(var extra in extras)if(!Insert(copy,extra,-1,out _))return false;
        after=copy;return true;
    }
    internal static bool NativeInsert(ItemStack[] cells,ItemStack primary,int maximum,ref int result)
    {
        var station=native?.Tile;var recipe=station?.Queue?.LastOrDefault()?.Recipe;
        if(station==null||!ReferenceEquals(cells,station.Output)||primary?.itemValue?.type!=recipe?.itemValueType
            ||primary.count!=recipe.count||!RebirthRecipeOutputs.HasOutputs(recipe))return true;
        if(!RebirthRecipeOutputs.TryPlan(recipe,1,out var outputs,out _)
            ||!Predict(cells,primary,maximum,outputs,out var after,out var index))
        {result=-1;return false;}
        RebirthProductiveEarnedRemoteClient.ObserveNativeOutputProjection(station,primary,outputs);
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
        if(!CanAdvancePersonalOutputRecovery(card)){result=false;return false;}
        if(HasPendingUiOutput(card)){result=CompleteUiOutputReturnedNative(card)||ResumeUiOutput(card)&&FinishUiOutputInventoryResume(card);return false;}
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
        if(grid!=null&&!EnsureStationOutputBefore(card)){result=false;return false;}
        var scope=new UiScope {Card=card,Grid=grid,Outputs=extras,Previous=ui};ui=scope;identity=scope;return true;
    }
    // Read-only capacity seam for an actual owning-card completion original.
    // Reserve primary separately before giveExp can decorate its native value;
    // then preflight the complete extras set using the existing insertion rules.
    // This returns no earned, admission, receipt, callback or write permission.
    internal static bool CanFitOwningCompletion(XUiC_RecipeStack card,bool cached=false)
    {
        try
        {
            var recipe=card?.GetRecipe();var window=card?.GetParentByType<XUiC_WorkstationWindowGroup>();
            if(recipe==null||recipe.IsScrap&&!HasNativeScrapOutputs(recipe)||card.AmountToRepair!=0||card.OriginalItem!=null&&!card.OriginalItem.IsEmpty()||
                window?.WorkstationData?.TileEntity==null||window.WorkstationData.TileEntity is RebirthCamperStationState)return false;
            IReadOnlyList<ItemStack> extras=Array.Empty<ItemStack>();
            if(RebirthRecipeOutputs.HasOutputs(recipe)&&!RebirthRecipeOutputs.TryPlan(recipe,1,out extras,out _))return false;
            if(cached&&(!card.isInventoryFull||card.outputItemValue==null||card.outputItemValue.IsEmpty()))return false;
            var primary=new ItemStack(cached?card.outputItemValue.Clone():new ItemValue(recipe.itemValueType,card.OutputQuality,card.OutputQuality),recipe.count);
            if(!cached)primary.itemValue.SetMetadata("rebirth.output.capacity.reservation","primary");
            var grid=window.GetChildByType<XUiC_WorkstationOutputGrid>();
            if(grid!=null)
            {
                if(PredictOwningOutput(grid.GetSlots(),primary,extras,cached,out _))return true;
                // The native cached branch falls back to inventory only when
                // its primary alone cannot enter the workstation output.
                if(!cached||PredictOwningOutput(grid.GetSlots(),primary,Array.Empty<ItemStack>(),true,out _))return false;
            }
            if(cached&&!CachedCursorAllowsInventory(card))return false;
            return PlanInventory(card,primary,extras,out _,out _);
        }
        catch{return false;}
    }
    internal static void PrepareUiSlots(XUiC_WorkstationGrid grid,ItemStack[] cells)
    {
        var scope=ui;
        GuardUiCachedGrid(scope,grid);
        if(scope==null||scope.Inserted||scope.PreparedArgument!=null||!ReferenceEquals(scope.Grid,grid))return;
        EnterUiCachedOutput(scope);
        var after=ItemStack.Clone(cells);
        foreach(var output in scope.Outputs)if(!Insert(after,output,-1,out _))
            throw new InvalidOperationException("Additional recipe output capacity changed before native grid publication.");
        // outputStack/cached Update subsequently passes this SAME array to
        // UpdateData. Publish the coupled set in the first native SetSlots;
        // never publish primary first and then project the grid a second time.
        for(int i=0;i<cells.Length;i++)cells[i]=after[i];
        scope.PreparedArgument=cells;
        scope.Original?.EnterGrid(cells);
    }
    internal static void UiInsert(XUiC_WorkstationOutputGrid grid,ref ItemStack[] cells)
    {
        var scope=ui;
        GuardUiCachedGrid(scope,grid);
        if(scope==null||scope.Inserted||!ReferenceEquals(scope.Grid,grid))return;
        EnterUiCachedOutput(scope);
        if(scope.PreparedArgument!=null)
        {
            if(!ReferenceEquals(scope.PreparedArgument,cells))
                throw new InvalidOperationException("Native additional output publication changed its prepared array.");
            scope.Inserted=true;return;
        }
        var after=ItemStack.Clone(cells);
        foreach(var output in scope.Outputs)if(!Insert(after,output,-1,out _))
            throw new InvalidOperationException("Additional recipe output capacity changed inside native completion.");
        cells=after;scope.PreparedArgument=after;scope.Original?.EnterGrid(after);grid.SetSlots(after);scope.Inserted=true;
    }
    private static bool PlanInventory(XUiC_RecipeStack card,ItemStack primary,IReadOnlyList<ItemStack> extras,out ItemStack[] bag,out ItemStack[] belt)
        =>PlanInventory(card,primary,extras,out bag,out belt,out _);
    private static bool CanInsertOutputs(EntityPlayerLocal actor)=>actor!=null&&!actor.IsDead()&&
        !RebirthCamperStationNativeActorPayment.Blocks(actor.bag)&&!RebirthCamperStationNativeActorPayment.Blocks(actor.inventory)&&
        !RebirthBackpackLibraryReservation.IsHeld(actor)&&!RebirthGearOwnerReservation.IsHeld(actor)&&
        !RemoteResourceClientTransactionCoordinator.HasPendingInventoryOperation(actor);
    private static bool PlanInventory(XUiC_RecipeStack card,ItemStack primary,IReadOnlyList<ItemStack> extras,out ItemStack[] bag,out ItemStack[] belt,out ItemValue[] equipment)
    {
        bag=null;belt=null;equipment=null;var actor=card?.xui?.playerUI?.entityPlayer;
        if(!CanInsertOutputs(actor))return false;
        var items=new List<ItemStack>{primary};foreach(var item in extras)items.Add(item.Clone());
        return RebirthRecipeOutputInventoryPrediction.TryPlan(actor,items,out bag,out belt,out equipment);
    }
    internal static bool InventoryInsert(XUiM_PlayerInventory inventory,ItemStack primary,ref bool result)
    {
        var scope=ui;
        if(scope?.CachedContinuation==true)return ConsumeUiOutputCachedPrimary(inventory,primary,ref result);
        if(scope?.Cached==true&&!scope.Inserted&&scope.Grid!=null&&ReferenceEquals(scope.Card.xui.PlayerInventory,inventory))
            throw new InvalidOperationException("Cached additional-output sink changed from its prepared native grid.");
        if(scope==null||scope.Inserted||scope.Grid!=null||!ReferenceEquals(scope.Card.xui.PlayerInventory,inventory)
            ||primary?.itemValue?.type!=scope.Card.GetRecipe()?.itemValueType)return true;
        EnterUiCachedOutput(scope);
        if(!PlanInventory(scope.Card,primary,scope.Outputs,out var bag,out var belt,out var equipment))
        {result=false;return false;}
        var player=scope.Card.xui.playerUI.entityPlayer;var world=player.world;int deaths=player.Died;
        string creation=RebirthCamperCharacterAccess.CurrentCreation(player);
        bool Current()=>ThreadManager.IsMainThread()&&ReferenceEquals(GameManager.Instance?.World,world)&&ReferenceEquals(player.world,world)&&
            ReferenceEquals(world.GetPrimaryPlayer(),player)&&ReferenceEquals(world.GetEntity(player.entityId),player)&&player.IsSpawned()&&!player.IsDead()&&
            player.Died==deaths&&RebirthCamperCharacterAccess.CurrentCreation(player)==creation&&ReferenceEquals(scope.Card.xui.playerUI.entityPlayer,player)&&ReferenceEquals(scope.Card.xui.PlayerInventory,inventory);
        if(!Current()){result=false;return false;}
        // Keep native owner insertion/callbacks and remote owner publication.
        // The synchronous preflight reserves the complete set before any write.
        scope.Inserted=true;
        scope.Original?.EnterOutput(0,primary);
        bool accepted=inventory.AddItemNoPartial(primary,false);
        scope.Original?.ObserveOutput(0,accepted);
        if(!accepted)throw new InvalidOperationException("Native primary insertion contradicted combined output preflight.");
        int outputIndex=1;
        foreach(var extra in scope.Outputs)
        {
            if(!Current())throw new InvalidOperationException("Native output owner changed during combined insertion.");
            var incoming=extra.Clone();scope.Original?.EnterOutput(outputIndex,incoming);
            accepted=inventory.AddItemNoPartial(incoming,false);scope.Original?.ObserveOutput(outputIndex,accepted);outputIndex++;
            if(!accepted)throw new InvalidOperationException("Native additional output insertion contradicted combined output preflight.");
        }
        if(!Current()||player.equipment.GetSlotCount()!=equipment.Length||
            !SameOutputGrid(player.bag.ItemGrid.items,bag)||!SameOutputGrid(player.inventory.ItemGrid.items,belt))
            throw new InvalidOperationException("Native combined output grids did not match the accepted plan.");
        for(int index=0;index<equipment.Length;index++)
            if(RebirthNativeItemCodec.Encode(player.equipment.GetSlotItem(index))!=RebirthNativeItemCodec.Encode(equipment[index]))
                throw new InvalidOperationException("Native combined equipment output did not match the accepted plan.");
        if(!Current())throw new InvalidOperationException("Native output owner changed before completion publication.");
        primary.count=0;result=true;return false;
    }
    private static bool SameOutputGrid(ItemStack[] actual,ItemStack[] expected)
    {
        if(actual==null||expected==null||actual.Length!=expected.Length)return false;
        for(int index=0;index<actual.Length;index++)if(!RebirthStationGridIngredients.IsSameStackSnapshot(actual[index],expected[index]))return false;
        return true;
    }
    internal static bool BeginRetry(XUiC_RecipeStack card,ref bool result,out object identity)
    {
        identity=null;if(ui!=null)return true;
        if(!CanAdvancePersonalOutputRecovery(card)){result=false;return false;}
        if(HasPendingUiOutput(card)){result=false;return false;}
        if(!RebirthRecipeOutputs.HasOutputs(card?.GetRecipe()))return true;
        if(!RebirthRecipeOutputs.TryPlan(card.GetRecipe(),1,out var outputs,out _)||!PlanInventory(card,new ItemStack(card.outputItemValue,card.GetRecipe().count),outputs,out _,out _))
        {result=false;return false;}
        var scope=new UiScope {Card=card,Outputs=outputs,Previous=ui,Cached=true};ui=scope;
        if(!PrepareUiCachedOutput(scope)){ui=scope.Previous;return false;}
        identity=scope;return true;
    }
    internal static bool BeginUpdateRetry(XUiC_RecipeStack card,out object identity)
    {
        identity=null;
        TryAdoptStationOutputCold(card);
        if(!CanAdvanceStationOutputColdCard(card))return false;
        if(HoldsUiOutputStationQueue(card))return false;
        if(!CanAdvanceStationOutputBefore(card))return false;
        if(!CanAdvancePendingStationOutput(card))return false;
        // A withheld personal continuation must not let native Update advance
        // its timer/card preimage while its exact original still owns the row.
        if(!card.isInventoryFull&&!CanAdvancePendingPersonalOutput(card))return false;
        if(!card.isInventoryFull||!RebirthRecipeOutputs.HasOutputs(card.GetRecipe()))return true;
        if(HasPendingUiOutput(card))return BeginUiOutputCachedResume(card,out identity);
        if(card.GetParentByType<XUiC_WorkstationWindowGroup>()?.WorkstationData?.TileEntity is RebirthCamperStationState)return false;
        if(!RebirthRecipeOutputs.TryPlan(card.GetRecipe(),1,out var outputs,out _))return false;
        var grid=card.GetParentByType<XUiC_WorkstationWindowGroup>()?.GetChildByType<XUiC_WorkstationOutputGrid>();
        var primary=new ItemStack(card.outputItemValue,card.GetRecipe().count);
        if(grid!=null)
        {
            if(!PredictOwningOutput(grid.GetSlots(),primary,outputs,true,out _))
            {
                if(PredictOwningOutput(grid.GetSlots(),primary,Array.Empty<ItemStack>(),true,out _)||!CachedCursorAllowsInventory(card)||
                    !PlanInventory(card,primary,outputs,out _,out _))return false;
                grid=null; // Native full-output branch will use its actual owner inventory.
            }
        }
        else if(!CachedCursorAllowsInventory(card)||!PlanInventory(card,primary,outputs,out _,out _))return false;
        var scope=new UiScope {Card=card,Grid=grid,Outputs=outputs,Previous=ui,Cached=true};ui=scope;
        if(!PrepareUiCachedOutput(scope)){ui=scope.Previous;return false;}
        identity=scope;return true;
    }
    internal static void EndUi(object identity)
    {if(identity is UiScope scope&&ReferenceEquals(ui,scope)){ui=scope.Previous;if(!RetainsUiOutput(scope.Original))scope.Original?.Dispose();}}
    private static bool CachedCursorAllowsInventory(XUiC_RecipeStack card)
    {
        var cursor=card?.xui?.DragAndDropWindow?.CurrentStack;
        return cursor!=null&&(cursor.IsEmpty()||cursor.itemValue?.ItemClass is not ItemClassQuest);
    }
}
[HarmonyPatch(typeof(XUiC_WorkstationGrid),nameof(XUiC_WorkstationGrid.SetSlots),new[]{typeof(ItemStack[])})]
internal static class RebirthRecipeOutputUiPrepareSlots
{
    private static void Prefix(XUiC_WorkstationGrid __instance,ItemStack[] __0,bool __runOriginal)
    {if(__runOriginal)RebirthRecipeOutputIntegration.PrepareUiSlots(__instance,__0);}
    private static void Postfix(XUiC_WorkstationGrid __instance,bool __runOriginal)
        =>RebirthRecipeOutputIntegration.ObserveUiOutputGrid(__instance,__runOriginal);
}

[HarmonyPatch(typeof(TileEntityWorkstation),"HandleRecipeQueue")]
internal static class RebirthRecipeOutputNativeScope
{
    private static bool Prefix(TileEntityWorkstation __instance,out object __state)
    {__state=null;if(!RebirthRecipeOutputIntegration.CanAdvanceStationOutputColdNative(__instance)||!RebirthRecipeOutputIntegration.CanAdvanceStationOutputQueueNative(__instance))return false;__state=RebirthRecipeOutputIntegration.EnterNative(__instance);return true;}
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
    private static bool Prefix(XUiC_RecipeStack __instance,ref bool __result,bool __runOriginal,out object __state)
    {
        __state=null;
        if(!__runOriginal)return true;
        return RebirthRecipeOutputIntegration.BeginUi(__instance,ref __result,out __state);
    }
    private static void Postfix(XUiC_RecipeStack __instance,bool __runOriginal,bool __result)
    {
        RebirthRecipeOutputIntegration.ObserveUiOutputReturn(__instance,__runOriginal,__result);
        RebirthRecipeOutputIntegration.NoteOwningOutputStackReturn(__instance,__runOriginal,__result);
    }
    private static Exception Finalizer(Exception __exception,object __state){RebirthRecipeOutputIntegration.EndUi(__state);return __exception;}
}
[HarmonyPatch(typeof(XUiC_WorkstationOutputGrid),nameof(XUiC_WorkstationOutputGrid.UpdateData))]
internal static class RebirthRecipeOutputUiInsert
{
    private sealed class Scopes {internal object Owning,CachedTransfer;}
    private static void Prefix(XUiC_WorkstationOutputGrid __instance,ref ItemStack[] __0,bool __runOriginal,out Scopes __state)
    {
        __state=new Scopes{Owning=RebirthRecipeOutputIntegration.BeginOwningGridInsert(__instance,__runOriginal)};
        if(!__runOriginal)return;
        RebirthRecipeOutputIntegration.UiInsert(__instance,ref __0);
        RebirthRecipeOutputIntegration.BindOwningGridArgument(__state.Owning,__0);
        __state.CachedTransfer=RebirthWorkstationCompletionPipeline.BeginRemoteCachedOutputTransfer(__instance,__0,__runOriginal);
    }
    private static void Postfix(XUiC_WorkstationOutputGrid __instance,bool __runOriginal,Scopes __state)
    {
        RebirthRecipeOutputIntegration.ObserveOwningGridInsert(__instance,__runOriginal,__state?.Owning);
        RebirthWorkstationCompletionPipeline.ObserveRemoteCachedOutputTransfer(__instance,__runOriginal,__state?.CachedTransfer);
    }
    private static Exception Finalizer(Exception __exception,Scopes __state)
    {RebirthWorkstationCompletionPipeline.EndRemoteCachedOutputTransfer(__state?.CachedTransfer);return __exception;}
}
[HarmonyPatch(typeof(XUiM_PlayerInventory),nameof(XUiM_PlayerInventory.AddItemNoPartial),new[]{typeof(ItemStack),typeof(bool)})]
internal static class RebirthRecipeOutputInventoryInsert
{
    [HarmonyPriority(1000)]
    private static bool Prefix(XUiM_PlayerInventory __instance,ItemStack __0,ref bool __result,bool __runOriginal,out object __state)
    {
        __state=RebirthRecipeOutputIntegration.BeginOwningInventoryInsert(__instance,__0,__runOriginal);
        if(!__runOriginal)return true;
        return RebirthRecipeOutputIntegration.InventoryInsert(__instance,__0,ref __result);
    }
    private static void Postfix(XUiM_PlayerInventory __instance,bool __runOriginal,bool __result,object __state)
        =>RebirthRecipeOutputIntegration.ObserveOwningInventoryInsert(__instance,__runOriginal,__result,__state);
    private static Exception Finalizer(Exception __exception,object __state)
    {RebirthRecipeOutputIntegration.EndOwningInventoryInsert(__state);return __exception;}
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
    private struct Scopes {internal bool NativeCooking;internal object Ui;internal RebirthRecipeOutputIntegration.OwningUpdateStamp Native;}
    [HarmonyPriority(1000)]
    private static bool Prefix(XUiC_RecipeStack __instance,out Scopes __state)
    {
        // Cooking owns its timer and manual Take operation. It does not use
        // the additional-output receipt/recovery pipeline for native card updates.
        if(__instance.GetParentByType<XUiC_RebirthCookingStation>() is XUiC_RebirthCookingStation cooking&&!cooking.IsMilling)
        {__state=new Scopes{NativeCooking=true};return true;}
        __state=new Scopes{Native=RebirthRecipeOutputIntegration.BeginOwningUpdateStamp(__instance)};
        return !RebirthRecyclerPlacedSettlement.HoldsStation(__instance.GetParentByType<XUiC_WorkstationWindowGroup>()?.WorkstationData?.TileEntity)&&RebirthCookingMillingPublication.CanAdvance(__instance)&&RebirthProductiveEarnedUiDeferredTransport.CanAdvanceAuthorityNative(__instance)&&RebirthWorkstationCompletionPipeline.CanAdvanceCachedPrimary(__instance)&&RebirthProductiveEarnedUiDeferredTransport.CanAdvanceKnownReceiptQueue(__instance)&&RebirthProductiveEarnedUiCoordinator.CanAdvanceQueueHandoff(__instance)&&
            RebirthRecipeOutputIntegration.CanAdvanceRemoteOutputRecovery(__instance)&&RebirthRecipeOutputIntegration.CanAdvancePersonalOutputRecovery(__instance)&&RebirthRecipeOutputIntegration.BeginUpdateRetry(__instance,out __state.Ui);
    }
    private static void Postfix(XUiC_RecipeStack __instance,bool __runOriginal,Scopes __state)
    {if(__state.NativeCooking)return;RebirthRecipeOutputIntegration.MarkOwningNativeUpdate(__instance,__runOriginal);}
    private static Exception Finalizer(Exception __exception,XUiC_RecipeStack __instance,bool __runOriginal,Scopes __state)
    {
        if(__state.NativeCooking)return __exception;
        try{RebirthRecipeOutputIntegration.ObserveStationOutputBeforeUpdate(__instance,__runOriginal,__exception);}catch{/* Retain the pre-generation source. */}
        if(__exception!=null)RebirthRecipeOutputIntegration.MarkOwningNativeUpdate(__instance,__runOriginal);
        RebirthRecipeOutputIntegration.EndUi(__state.Ui);RebirthRecipeOutputIntegration.EndOwningUpdateStamp(__state.Native);return __exception;
    }
}
